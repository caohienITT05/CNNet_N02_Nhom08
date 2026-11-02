using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaigonChe.Web.Data;
using SaigonChe.Web.Models;
using SaigonChe.Web.Services;
using SaigonChe.Web.ViewModels;

namespace SaigonChe.Web.Controllers;

[Authorize(Roles = "Admin,Staff")]
[Route("Pos")]
public sealed class PosController : Controller
{
    private const int MaxQuantityPerProduct = 50;
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PosController> _logger;
    private readonly OrderCompletionService _completionService;

    public PosController(
        ApplicationDbContext db,
        IConfiguration configuration,
        ILogger<PosController> logger,
        OrderCompletionService completionService)
    {
        _db = db;
        _configuration = configuration;
        _logger = logger;
        _completionService = completionService;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = new PosDashboardViewModel
        {
            Products = await _db.Products.AsNoTracking()
                .Include(product => product.Category)
                .Where(product => product.IsAvailable)
                .OrderBy(product => product.Category!.Name)
                .ThenBy(product => product.Name)
                .ToListAsync(cancellationToken),
            Tables = await _db.DiningTables.AsNoTracking()
                .Where(table => table.IsActive)
                .OrderBy(table => table.Code)
                .ToListAsync(cancellationToken),
            Categories = await _db.Categories.AsNoTracking()
                .OrderBy(category => category.Name)
                .ToListAsync(cancellationToken),
            OpenOrders = await _db.Orders.AsNoTracking()
                .Include(order => order.Table)
                .Include(order => order.Items)
                .Where(order => order.Type == OrderType.Pos
                    && !order.IsPaid
                    && order.Status != OrderStatus.Cancelled)
                .OrderBy(order => order.CreatedAt)
                .ToListAsync(cancellationToken),
            BankId = _configuration["Payment:BankId"],
            AccountNumber = _configuration["Payment:AccountNumber"],
            AccountName = _configuration["Payment:AccountName"]
        };

        return View("Index", model);
    }

    [HttpPost("Start")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(int? tableId, CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var staffId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        DiningTable? table = null;
        if (tableId.HasValue)
        {
            table = await _db.DiningTables.FirstOrDefaultAsync(
                item => item.Id == tableId.Value && item.IsActive, cancellationToken);
            if (table == null)
            {
                TempData["PosError"] = "Bàn không tồn tại hoặc đã ngừng phục vụ.";
                return RedirectToAction(nameof(Index));
            }

            var existingOrderId = await _db.Orders
                .Where(order => order.Type == OrderType.Pos
                    && order.TableId == table.Id
                    && !order.IsPaid
                    && order.Status != OrderStatus.Cancelled)
                .OrderByDescending(order => order.CreatedAt)
                .Select(order => (int?)order.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (existingOrderId.HasValue)
            {
                await transaction.CommitAsync(cancellationToken);
                return RedirectToAction(nameof(Order), new { id = existingOrderId.Value });
            }
        }
        else
        {
            // Reuse the current staff's empty takeaway draft instead of creating
            // a trail of abandoned 0-item orders every time "Tạo đơn POS" is pressed.
            var emptyDraftId = await _db.Orders
                .Where(order => order.Type == OrderType.Pos
                    && order.TableId == null
                    && !order.IsPaid
                    && order.Status != OrderStatus.Cancelled
                    && order.AssignedStaffId == staffId
                    && !order.Items.Any())
                .OrderByDescending(order => order.CreatedAt)
                .Select(order => (int?)order.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (emptyDraftId.HasValue)
            {
                await transaction.CommitAsync(cancellationToken);
                return RedirectToAction(nameof(Order), new { id = emptyDraftId.Value });
            }
        }

        var order = new Order
        {
            Type = OrderType.Pos,
            CustomerName = "Khách tại quầy",
            Phone = "0000000000",
            Address = table?.Name ?? "Mua mang về",
            TableId = table?.Id,
            AssignedStaffId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        _db.OrderStatusHistory.Add(new OrderStatusHistory
        {
            Order = order,
            PreviousStatus = null,
            Status = OrderStatus.Pending,
            ActorId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            Note = "Tạo đơn nháp POS.",
            CreatedAt = DateTime.UtcNow
        });
        _db.Orders.Add(order);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException) when (table is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            var activeOrderId = await _db.Orders.AsNoTracking()
                .Where(existing => existing.Type == OrderType.Pos
                    && existing.TableId == table.Id
                    && !existing.IsPaid
                    && existing.Status != OrderStatus.Cancelled)
                .Select(existing => (int?)existing.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (activeOrderId.HasValue)
                return RedirectToAction(nameof(Order), new { id = activeOrderId.Value });

            TempData["PosError"] = "Không thể mở đơn cho bàn này. Tải lại danh sách và thử lại.";
            return RedirectToAction(nameof(Index));
        }

        return RedirectToAction(nameof(Order), new { id = order.Id });
    }

    [HttpGet("Order/{id:int}")]
    public async Task<IActionResult> Order(int id, CancellationToken cancellationToken)
    {
        var order = await FindOpenOrderAsync(id, cancellationToken);
        if (order == null)
        {
            var closedOrder = await _db.Orders.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == id && item.Type == OrderType.Pos, cancellationToken);
            if (closedOrder == null)
            {
                return NotFound();
            }

            if (closedOrder.IsPaid)
            {
                return RedirectToAction(nameof(Receipt), new { id });
            }

            TempData["PosError"] = closedOrder.Status == OrderStatus.Cancelled
                ? $"Đơn #{id} đã bị hủy."
                : $"Đơn #{id} không còn mở để chỉnh sửa.";
            return RedirectToAction(nameof(Index));
        }

        var now = DateTime.UtcNow;
        var model = new PosOrderViewModel
        {
            Order = order,
            RowVersion = Convert.ToBase64String(order.RowVersion),
            Products = await _db.Products.AsNoTracking()
                .Include(product => product.Category)
                .Where(product => product.IsAvailable)
                .OrderBy(product => product.Category!.Name)
                .ThenBy(product => product.Name)
                .ToListAsync(cancellationToken),
            Vouchers = await _db.Vouchers.AsNoTracking()
                .Where(voucher => voucher.IsActive
                    && voucher.ValidFrom <= now
                    && voucher.ValidTo >= now
                    && (!voucher.UseLimit.HasValue || voucher.UseCount < voucher.UseLimit.Value))
                .OrderBy(voucher => voucher.Code)
                .ToListAsync(cancellationToken),
            VietQrConfigured = IsVietQrConfigured(),
            VietQrUrl = MakeVietQrUrl(order)
        };

        return View("Order", model);
    }

    [HttpPost("AddItem")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddItem(PosItemPostViewModel model, CancellationToken cancellationToken)
    {
        if (model.Quantity is < 1 or > MaxQuantityPerProduct)
        {
            TempData["PosError"] = $"Số lượng phải từ 1 đến {MaxQuantityPerProduct}.";
            return RedirectToAction(nameof(Order), new { id = model.OrderId });
        }

        var order = await FindOpenOrderAsync(model.OrderId, cancellationToken);
        if (order == null)
        {
            return NotFound();
        }

        if (!TryMatchVersion(model.RowVersion, order.RowVersion))
        {
            return StaleOrder(order.Id);
        }

        var product = await _db.Products.FirstOrDefaultAsync(
            item => item.Id == model.ProductId && item.IsAvailable, cancellationToken);
        if (product == null)
        {
            TempData["PosError"] = "Món không tồn tại hoặc đã ngừng bán.";
            return RedirectToAction(nameof(Order), new { id = order.Id });
        }

        var note = model.Notes?.Trim();
        if (note?.Length > 500)
        {
            TempData["PosError"] = "Ghi chú tối đa 500 ký tự.";
            return RedirectToAction(nameof(Order), new { id = order.Id });
        }

        var item = order.Items.FirstOrDefault(existing =>
            existing.ProductId == product.Id && existing.Notes == note);
        if (item == null)
        {
            item = new OrderItem
            {
                OrderId = order.Id,
                ProductId = product.Id,
                ProductName = product.Name,
                Price = product.Price,
                Quantity = model.Quantity,
                Notes = note
            };
            order.Items.Add(item);
            _db.OrderItems.Add(item);
        }
        else
        {
            if (item.Quantity + model.Quantity > MaxQuantityPerProduct)
            {
                TempData["PosError"] = $"Tối đa {MaxQuantityPerProduct} phần cho cùng món và ghi chú.";
                return RedirectToAction(nameof(Order), new { id = order.Id });
            }

            item.Quantity += model.Quantity;
            item.ProductName = product.Name;
            item.Price = product.Price;
        }

        RecalculateOrder(order);
        if (!await SaveWithConcurrencyAsync(order, cancellationToken))
        {
            return StaleOrder(order.Id);
        }

        TempData["PosSuccess"] = $"Đã thêm {product.Name} vào đơn #{order.Id}.";
        return RedirectToAction(nameof(Order), new { id = order.Id });
    }

    [HttpPost("QuickAdd")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickAdd(int productId, CancellationToken cancellationToken)
    {
        var product = await _db.Products.FirstOrDefaultAsync(
            item => item.Id == productId && item.IsAvailable, cancellationToken);
        if (product == null)
        {
            TempData["PosError"] = "Món không tồn tại hoặc đã ngừng bán.";
            return RedirectToAction(nameof(Index));
        }

        var staffId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var order = await _db.Orders
            .Include(item => item.Items)
            .Where(item => item.Type == OrderType.Pos
                && !item.IsPaid
                && item.Status != OrderStatus.Cancelled
                && item.AssignedStaffId == staffId)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (order == null)
        {
            order = new Order
            {
                Type = OrderType.Pos,
                CustomerName = "Khách tại quầy",
                Phone = "0000000000",
                Address = "Mua mang về",
                AssignedStaffId = staffId,
                Status = OrderStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
            _db.OrderStatusHistory.Add(new OrderStatusHistory
            {
                Order = order,
                PreviousStatus = null,
                Status = OrderStatus.Pending,
                ActorId = staffId,
                Note = "Tạo đơn nháp POS.",
                CreatedAt = DateTime.UtcNow
            });
            _db.Orders.Add(order);
        }

        var existing = order.Items.FirstOrDefault(item =>
            item.ProductId == product.Id && item.Notes == null);
        if (existing == null)
        {
            var item = new OrderItem
            {
                OrderId = order.Id,
                ProductId = product.Id,
                ProductName = product.Name,
                Price = product.Price,
                Quantity = 1
            };
            order.Items.Add(item);
            _db.OrderItems.Add(item);
        }
        else if (existing.Quantity >= MaxQuantityPerProduct)
        {
            TempData["PosError"] = $"Đơn đã có tối đa {MaxQuantityPerProduct} phần cho {product.Name}.";
            return RedirectToAction(nameof(Order), new { id = order.Id });
        }
        else
        {
            existing.Quantity += 1;
            existing.ProductName = product.Name;
            existing.Price = product.Price;
        }

        RecalculateOrder(order);
        await _db.SaveChangesAsync(cancellationToken);
        TempData["PosSuccess"] = $"Đã thêm {product.Name} vào đơn #{order.Id}.";
        return RedirectToAction(nameof(Order), new { id = order.Id });
    }

    [HttpPost("UpdateItem")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateItem(int orderId, int itemId, int quantity, string? notes,
        string rowVersion, CancellationToken cancellationToken)
    {
        var order = await FindOpenOrderAsync(orderId, cancellationToken);
        if (order == null) return NotFound();
        if (!TryMatchVersion(rowVersion, order.RowVersion)) return StaleOrder(order.Id);

        var item = order.Items.FirstOrDefault(existing => existing.Id == itemId);
        if (item == null) return NotFound();

        if (quantity is < 1 or > MaxQuantityPerProduct || notes?.Length > 500)
        {
            TempData["PosError"] = $"Số lượng phải từ 1 đến {MaxQuantityPerProduct} và ghi chú tối đa 500 ký tự.";
            return RedirectToAction(nameof(Order), new { id = order.Id });
        }

        item.Quantity = quantity;
        item.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        RecalculateOrder(order);
        if (!await SaveWithConcurrencyAsync(order, cancellationToken)) return StaleOrder(order.Id);
        return RedirectToAction(nameof(Order), new { id = order.Id });
    }

    [HttpPost("RemoveItem")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveItem(int orderId, int itemId, string rowVersion,
        CancellationToken cancellationToken)
    {
        var order = await FindOpenOrderAsync(orderId, cancellationToken);
        if (order == null) return NotFound();
        if (!TryMatchVersion(rowVersion, order.RowVersion)) return StaleOrder(order.Id);

        var item = order.Items.FirstOrDefault(existing => existing.Id == itemId);
        if (item == null) return NotFound();
        _db.OrderItems.Remove(item);
        order.Items.Remove(item);
        RecalculateOrder(order);
        if (!await SaveWithConcurrencyAsync(order, cancellationToken)) return StaleOrder(order.Id);
        return RedirectToAction(nameof(Order), new { id = order.Id });
    }

    [HttpPost("Customer")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Customer(int orderId, string customerName, string phone,
        string address, string rowVersion, CancellationToken cancellationToken)
    {
        var order = await FindOpenOrderAsync(orderId, cancellationToken);
        if (order == null) return NotFound();
        if (!TryMatchVersion(rowVersion, order.RowVersion)) return StaleOrder(order.Id);

        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(customerName)
            || string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(address))
        {
            TempData["PosError"] = "Vui lòng nhập đầy đủ thông tin khách hàng.";
            return RedirectToAction(nameof(Order), new { id = order.Id });
        }
        customerName = customerName.Trim();
        phone = phone.Trim();
        address = address.Trim();
        if (customerName.Length is < 1 or > 100 || phone.Length is < 1 or > 30
            || address.Length is < 1 or > 300)
        {
            TempData["PosError"] = "Vui lòng kiểm tra tên khách, số điện thoại và địa chỉ/bàn.";
            return RedirectToAction(nameof(Order), new { id = order.Id });
        }

        order.CustomerName = customerName;
        order.Phone = phone;
        order.Address = address;
        if (!await SaveWithConcurrencyAsync(order, cancellationToken)) return StaleOrder(order.Id);
        TempData["PosSuccess"] = "Đã lưu thông tin khách hàng.";
        return RedirectToAction(nameof(Order), new { id = order.Id });
    }

    [HttpPost("Voucher")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Voucher(int orderId, int? voucherId, string rowVersion,
        CancellationToken cancellationToken)
    {
        var order = await FindOpenOrderAsync(orderId, cancellationToken);
        if (order == null) return NotFound();
        if (!TryMatchVersion(rowVersion, order.RowVersion)) return StaleOrder(order.Id);

        if (voucherId.HasValue)
        {
            var voucher = await _db.Vouchers.FirstOrDefaultAsync(
                item => item.Id == voucherId.Value && item.IsActive, cancellationToken);
            if (voucher == null || !VoucherPricingService.TryCalculateDiscount(voucher, order.Items, DateTime.UtcNow, out _))
            {
                TempData["PosError"] = "Voucher đã hết hạn, hết lượt hoặc đơn chưa đạt giá trị tối thiểu.";
                return RedirectToAction(nameof(Order), new { id = order.Id });
            }

            order.VoucherId = voucher.Id;
        }
        else
        {
            order.VoucherId = null;
        }

        RecalculateOrder(order);
        if (!await SaveWithConcurrencyAsync(order, cancellationToken)) return StaleOrder(order.Id);
        TempData["PosSuccess"] = "Đã cập nhật voucher.";
        return RedirectToAction(nameof(Order), new { id = order.Id });
    }

    [HttpPost("Pay")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(int orderId, PaymentMethod method, decimal? amountReceived,
        string rowVersion, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || method is not (PaymentMethod.Cash or PaymentMethod.VietQr))
        {
            TempData["PosError"] = "Phương thức thanh toán không hợp lệ.";
            return RedirectToAction(nameof(Order), new { id = orderId });
        }

        if (method == PaymentMethod.VietQr && !IsVietQrConfigured())
        {
            TempData["PosError"] = "Chưa cấu hình tài khoản nhận VietQR.";
            return RedirectToAction(nameof(Order), new { id = orderId });
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var order = await _db.Orders
            .Include(item => item.Items)
            .Include(item => item.Table)
            .FirstOrDefaultAsync(item => item.Id == orderId
                && item.Type == OrderType.Pos
                && item.Status != OrderStatus.Cancelled, cancellationToken);
        if (order == null) return NotFound();

        // A retried browser request after a successful commit is harmless.
        if (order.IsPaid)
        {
            await transaction.CommitAsync(cancellationToken);
            return RedirectToAction(nameof(Receipt), new { id = order.Id });
        }

        if (!TryMatchVersion(rowVersion, order.RowVersion))
        {
            await transaction.RollbackAsync(cancellationToken);
            return StaleOrder(order.Id);
        }

        if (order.Items.Count == 0)
        {
            TempData["PosError"] = $"Đơn #{order.Id} chưa có món. Hãy thêm ít nhất một món trước khi thanh toán.";
            return RedirectToAction(nameof(Order), new { id = order.Id });
        }

        var products = await _db.Products
            .Where(product => order.Items.Select(item => item.ProductId).Contains(product.Id))
            .ToDictionaryAsync(product => product.Id, cancellationToken);

        foreach (var item in order.Items)
        {
            if (!products.TryGetValue(item.ProductId, out var product) || !product.IsAvailable)
            {
                TempData["PosError"] = $"Món {item.ProductName} đã ngừng bán. Hãy sửa đơn trước khi thanh toán.";
                return RedirectToAction(nameof(Order), new { id = order.Id });
            }

            item.ProductName = product.Name;
            item.Price = product.Price;
        }

        Voucher? voucher = null;
        if (order.VoucherId.HasValue)
        {
            voucher = await _db.Vouchers.FirstOrDefaultAsync(
                item => item.Id == order.VoucherId.Value, cancellationToken);
            if (voucher == null || !VoucherPricingService.TryCalculateDiscount(voucher, order.Items, DateTime.UtcNow, out var discount))
            {
                TempData["PosError"] = "Voucher không còn hợp lệ. Hãy bỏ voucher hoặc chọn voucher khác.";
                return RedirectToAction(nameof(Order), new { id = order.Id });
            }

            order.DiscountAmount = discount;
        }
        else
        {
            order.DiscountAmount = 0;
        }

        order.Total = VoucherPricingService.CalculateTotal(order.Items, order.DiscountAmount);

        if (method == PaymentMethod.Cash
            && !CashPaymentCalculator.TryCalculateChange(order.Total, amountReceived, out _))
        {
            TempData["PosError"] = "Số tiền khách đưa chưa đủ thanh toán.";
            return RedirectToAction(nameof(Order), new { id = order.Id });
        }

        order.AmountReceived = method == PaymentMethod.Cash ? amountReceived : order.Total;

        if (voucher != null)
        {
            voucher.UseCount++;
        }

        try
        {
            await _completionService.CompleteAsync(
                order,
                method,
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                $"POS sale #{order.Id}",
                cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (OrderCompletionException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            TempData["PosError"] = exception.Message;
            return RedirectToAction(nameof(Order), new { id = order.Id });
        }
        catch (DbUpdateConcurrencyException exception)
        {
            _logger.LogInformation(exception, "Concurrent POS payment rejected for order {OrderId}.", order.Id);
            await transaction.RollbackAsync(cancellationToken);
            return StaleOrder(order.Id);
        }
        catch (DbUpdateException exception)
        {
            _logger.LogWarning(exception, "POS payment could not be saved for order {OrderId}.", order.Id);
            await transaction.RollbackAsync(cancellationToken);
            TempData["PosError"] = "Không thể hoàn tất thanh toán. Tải lại đơn và kiểm tra tồn kho.";
            return RedirectToAction(nameof(Order), new { id = order.Id });
        }

        TempData["PosSuccess"] = $"Đã xác nhận thanh toán đơn #{order.Id}.";
        return RedirectToAction(nameof(Receipt), new { id = order.Id });
    }

    [HttpPost("Cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int orderId, string rowVersion, CancellationToken cancellationToken)
    {
        var order = await FindOpenOrderAsync(orderId, cancellationToken);
        if (order == null) return NotFound();
        if (!TryMatchVersion(rowVersion, order.RowVersion)) return StaleOrder(order.Id);

        var previousStatus = order.Status;
        order.Status = OrderStatus.Cancelled;
        _db.OrderStatusHistory.Add(new OrderStatusHistory
        {
            OrderId = order.Id,
            PreviousStatus = previousStatus,
            Status = OrderStatus.Cancelled,
            ActorId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            Note = "POS draft cancelled",
            CreatedAt = DateTime.UtcNow
        });
        if (!await SaveWithConcurrencyAsync(order, cancellationToken)) return StaleOrder(order.Id);
        TempData["PosSuccess"] = $"Đã hủy đơn nháp #{order.Id}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Receipt/{id:int}")]
    public async Task<IActionResult> Receipt(int id, CancellationToken cancellationToken)
    {
        var order = await _db.Orders.AsNoTracking()
            .Include(item => item.Items)
            .Include(item => item.Table)
            .Include(item => item.Voucher)
            .FirstOrDefaultAsync(item => item.Id == id
                && item.Type == OrderType.Pos
                && item.IsPaid, cancellationToken);
        if (order == null) return NotFound();

        var amountReceived = order.AmountReceived ?? order.Total;

        return View("Receipt", new PosReceiptViewModel
        {
            Order = order,
            AmountReceived = amountReceived,
            Change = Math.Max(0, amountReceived - order.Total),
            VietQrUrl = MakeVietQrUrl(order)
        });
    }

    private async Task<Order?> FindOpenOrderAsync(int id, CancellationToken cancellationToken)
    {
        return await _db.Orders
            .Include(order => order.Items)
            .Include(order => order.Table)
            .Include(order => order.Voucher)
            .FirstOrDefaultAsync(order => order.Id == id
                && order.Type == OrderType.Pos
                && !order.IsPaid
                && order.Status != OrderStatus.Cancelled, cancellationToken);
    }

    private void RecalculateOrder(Order order)
    {
        if (!order.VoucherId.HasValue)
        {
            order.DiscountAmount = 0;
            order.Total = VoucherPricingService.CalculateTotal(order.Items, order.DiscountAmount);
            return;
        }

        var voucher = _db.Vouchers.Local.FirstOrDefault(item => item.Id == order.VoucherId.Value);
        if (voucher == null)
        {
            // The selected voucher is reloaded and validated on every checkout.
            order.DiscountAmount = 0;
            order.Total = VoucherPricingService.CalculateTotal(order.Items, order.DiscountAmount);
            return;
        }

        if (VoucherPricingService.TryCalculateDiscount(voucher, order.Items, DateTime.UtcNow, out var discount))
        {
            order.DiscountAmount = discount;
        }
        else
        {
            order.DiscountAmount = 0;
        }

        order.Total = VoucherPricingService.CalculateTotal(order.Items, order.DiscountAmount);
    }

    private bool IsVietQrConfigured() => VietQrPayment.IsConfigured(_configuration);
    private string? MakeVietQrUrl(Order order) => VietQrPayment.MakeUrl(_configuration, order.Id, order.Total, "POS");

    private async Task<bool> SaveWithConcurrencyAsync(Order order, CancellationToken cancellationToken)
    {
        try
        {
            // Child-only edits must also advance the aggregate concurrency token.
            _db.Entry(order).Property(item => item.Total).IsModified = true;
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException exception)
        {
            _logger.LogInformation(exception, "Concurrent POS edit rejected for order {OrderId}.", order.Id);
            return false;
        }
    }

    private bool TryMatchVersion(string? version, byte[] current)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(version)
                && Convert.FromBase64String(version).SequenceEqual(current);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private IActionResult StaleOrder(int orderId)
    {
        TempData["PosError"] = "Đơn vừa được nhân viên khác cập nhật. Đơn đã tải lại; hãy kiểm tra trước khi thao tác tiếp.";
        return RedirectToAction(nameof(Order), new { id = orderId });
    }
}
