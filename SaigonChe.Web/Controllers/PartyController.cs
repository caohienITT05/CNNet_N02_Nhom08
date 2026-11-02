using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaigonChe.Web.Data;
using SaigonChe.Web.Models;
using SaigonChe.Web.Services;
using SaigonChe.Web.ViewModels;

namespace SaigonChe.Web.Controllers;

[Authorize]
public sealed class PartyController(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration,
    OrderCompletionService orderCompletionService) : Controller
{
    private static readonly TimeZoneInfo VietnamTimeZone = ResolveVietnamTimeZone();

    [Authorize(Roles = "Customer")]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var user = await userManager.GetUserAsync(User);
        return View(new PartyRequestCreateViewModel
        {
            CustomerName = user?.UserName ?? string.Empty,
            Phone = user?.PhoneNumber ?? string.Empty,
            EventAt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone).AddDays(2).Date.AddHours(12),
            Headcount = 10
        });
    }

    [Authorize(Roles = "Customer")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PartyRequestCreateViewModel model)
    {
        if (model.EventAt is { } localEventAt)
        {
            if (localEventAt <= TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone))
                ModelState.AddModelError(nameof(model.EventAt), "Thời gian tổ chức phải ở phía trước.");
        }
        if (!ModelState.IsValid)
            return View(model);

        var local = DateTime.SpecifyKind(model.EventAt!.Value, DateTimeKind.Unspecified);
        var request = new PartyRequest
        {
            CustomerUserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            CustomerName = model.CustomerName.Trim(),
            Phone = model.Phone.Trim(),
            Location = model.Location.Trim(),
            EventAt = TimeZoneInfo.ConvertTimeToUtc(local, VietnamTimeZone),
            Headcount = model.Headcount,
            Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.PartyRequests.Add(request);
        context.PartyRequestStatusHistory.Add(new PartyRequestStatusHistory
        {
            PartyRequest = request,
            PreviousStatus = null,
            Status = PartyRequestStatus.Pending,
            ActorId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            Note = "Khách gửi yêu cầu đặt tiệc.",
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(MyRequests));
    }

    [Authorize(Roles = "Customer")]
    [HttpGet]
    public async Task<IActionResult> MyRequests()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var requests = await context.PartyRequests.AsNoTracking()
            .Where(request => request.CustomerUserId == userId)
            .OrderByDescending(request => request.CreatedAt)
            .Select(request => new PartyRequestQueueRowViewModel
            {
                Id = request.Id,
                CustomerName = request.CustomerName,
                Phone = request.Phone,
                Location = request.Location,
                EventAt = request.EventAt,
                Headcount = request.Headcount,
                Status = request.Status,
                CreatedAt = request.CreatedAt
            })
            .ToListAsync();
        return View("Index", requests);
    }

    [Authorize(Roles = "Staff,Admin")]
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var requests = await context.PartyRequests.AsNoTracking()
            .OrderBy(request => request.Status == PartyRequestStatus.Completed || request.Status == PartyRequestStatus.Cancelled)
            .ThenBy(request => request.EventAt)
            .Select(request => new PartyRequestQueueRowViewModel
            {
                Id = request.Id,
                CustomerName = request.CustomerName,
                Phone = request.Phone,
                Location = request.Location,
                EventAt = request.EventAt,
                Headcount = request.Headcount,
                Status = request.Status,
                CreatedAt = request.CreatedAt
            })
            .ToListAsync();
        return View("Index", requests);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var request = await FindVisibleRequestAsync(id);
        if (request == null) return NotFound();
        return View(await BuildDetailsAsync(request));
    }

    [Authorize(Roles = "Staff,Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(PartyRequestUpdateViewModel model)
    {
        if (!Enum.IsDefined(model.Status))
        {
            TempData["PartyError"] = "Trạng thái yêu cầu không hợp lệ.";
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        if (!ModelState.IsValid)
        {
            var invalidRequest = await context.PartyRequests.AsNoTracking().FirstOrDefaultAsync(item => item.Id == model.Id);
            if (invalidRequest == null) return NotFound();
            return View("Details", await BuildDetailsAsync(invalidRequest, model));
        }

        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var request = await context.PartyRequests.FirstOrDefaultAsync(item => item.Id == model.Id);
        if (request == null) return NotFound();
        if (!OrderWorkflowRules.Matches(model.RowVersion, request.RowVersion))
        {
            TempData["PartyError"] = "Yêu cầu vừa được cập nhật. Hãy tải lại trước khi tiếp tục.";
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }
        var previousRequestStatus = request.Status;

        if (request.Status is PartyRequestStatus.Completed or PartyRequestStatus.Cancelled ||
            !IsAllowedTransition(request.Status, model.Status))
        {
            await transaction.RollbackAsync();
            TempData["PartyError"] = "Yêu cầu đã kết thúc hoặc không thể chuyển sang trạng thái này.";
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        if (request.Status == PartyRequestStatus.Confirmed && model.QuoteAmount != request.QuoteAmount)
        {
            await transaction.RollbackAsync();
            TempData["PartyError"] = "Không thể thay đổi báo giá sau khi đã xác nhận đơn.";
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        if (model.Status == PartyRequestStatus.Cancelled && string.IsNullOrWhiteSpace(model.CancellationReason))
        {
            await transaction.RollbackAsync();
            ModelState.AddModelError(nameof(model.CancellationReason), "Vui lòng ghi lý do hủy.");
            request.Consultation = model.Consultation;
            request.QuoteAmount = model.QuoteAmount;
            return View("Details", await BuildDetailsAsync(request, model));
        }

        request.Consultation = string.IsNullOrWhiteSpace(model.Consultation) ? null : model.Consultation.Trim();
        request.QuoteAmount = model.QuoteAmount;
        request.UpdatedAt = DateTime.UtcNow;

        if (model.Status == PartyRequestStatus.Confirmed && request.OrderId == null)
        {
            var lines = model.Lines.Where(line => line.Selected).ToList();
            if (lines.Count == 0 || lines.Any(line => line.ProductId <= 0 || line.Quantity is < 1 or > 10000) ||
                lines.Select(line => line.ProductId).Distinct().Count() != lines.Count)
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError(string.Empty, "Chọn ít nhất một món, số lượng hợp lệ và không lặp món.");
                return View("Details", await BuildDetailsAsync(request, model));
            }

            var ids = lines.Select(line => line.ProductId).ToList();
            var products = await context.Products.Where(product => ids.Contains(product.Id) && product.IsAvailable)
                .ToDictionaryAsync(product => product.Id);
            if (products.Count != ids.Count)
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError(string.Empty, "Có món không còn bán. Hãy tải lại danh sách và chọn món khác.");
                return View("Details", await BuildDetailsAsync(request, model));
            }

            var orderItems = lines.Select(line =>
            {
                var product = products[line.ProductId];
                return new OrderItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Price = product.Price,
                    Quantity = line.Quantity
                };
            }).ToList();
            var itemSubtotal = orderItems.Sum(item => item.Price * item.Quantity);
            var order = new Order
            {
                CustomerName = request.CustomerName,
                Phone = request.Phone,
                Address = request.Location,
                CustomerUserId = request.CustomerUserId,
                AssignedStaffId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                Type = OrderType.Party,
                Status = OrderStatus.Pending,
                Total = request.QuoteAmount ?? itemSubtotal,
                Items = orderItems,
                CreatedAt = DateTime.UtcNow
            };
            context.Orders.Add(order);
            context.OrderStatusHistory.Add(new OrderStatusHistory
            {
                Order = order,
                PreviousStatus = null,
                Status = OrderStatus.Pending,
                ActorId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                Note = $"Tạo từ yêu cầu đặt tiệc #{request.Id}",
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
            request.OrderId = order.Id;
        }
        else if (model.Status == PartyRequestStatus.Confirmed && request.OrderId.HasValue)
        {
            // Retrying confirmation preserves the existing order instead of creating another one.
        }
        else if (model.Status == PartyRequestStatus.Cancelled)
        {
            request.CancellationReason = model.CancellationReason!.Trim();
            if (request.OrderId is int orderId)
            {
                var order = await context.Orders.FirstOrDefaultAsync(item => item.Id == orderId);
                if (order is { IsPaid: false } && order.Status != OrderStatus.Cancelled)
                {
                    var previousStatus = order.Status;
                    order.Status = OrderStatus.Cancelled;
                    context.OrderStatusHistory.Add(new OrderStatusHistory
                    {
                        OrderId = order.Id,
                        PreviousStatus = previousStatus,
                        Status = OrderStatus.Cancelled,
                        ActorId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                        Note = $"Hủy yêu cầu tiệc #{request.Id}: {request.CancellationReason}",
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
        }
        else if (model.Status == PartyRequestStatus.Completed && request.OrderId is int completedOrderId)
        {
            if (model.PaymentMethod is not (PaymentMethod.Cash or PaymentMethod.VietQr)
                || (model.PaymentMethod == PaymentMethod.VietQr && !VietQrPayment.IsConfigured(configuration)))
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError(nameof(model.PaymentMethod), "Chọn và xác nhận phương thức thanh toán.");
                return View("Details", await BuildDetailsAsync(request, model));
            }
            var order = await context.Orders.Include(item => item.Items).FirstOrDefaultAsync(item => item.Id == completedOrderId);
            if (order == null || order.IsPaid)
            {
                await transaction.RollbackAsync();
                TempData["PartyError"] = "Đơn liên quan không tồn tại hoặc đã được thanh toán.";
                return RedirectToAction(nameof(Details), new { id = model.Id });
            }
            try
            {
                await orderCompletionService.CompleteAsync(order, model.PaymentMethod.Value,
                    User.FindFirstValue(ClaimTypes.NameIdentifier), "Hoàn tất đơn đặt tiệc", HttpContext.RequestAborted);
            }
            catch (OrderCompletionException exception)
            {
                await transaction.RollbackAsync();
                TempData["PartyError"] = exception.Message;
                return RedirectToAction(nameof(Details), new { id = model.Id });
            }
        }

        request.Status = model.Status;
        request.UpdatedAt = DateTime.UtcNow;
        context.PartyRequestStatusHistory.Add(new PartyRequestStatusHistory
        {
            PartyRequestId = request.Id,
            PreviousStatus = previousRequestStatus,
            Status = request.Status,
            ActorId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            Note = model.Status == PartyRequestStatus.Cancelled
                ? request.CancellationReason
                : model.Consultation?.Trim(),
            CreatedAt = DateTime.UtcNow
        });
        try
        {
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
            TempData["PartySuccess"] = model.Status == PartyRequestStatus.Confirmed && request.OrderId.HasValue
                ? $"Đã cập nhật yêu cầu và liên kết đơn #{request.OrderId}."
                : "Đã cập nhật yêu cầu đặt tiệc.";
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            TempData["PartyError"] = "Dữ liệu vừa được nhân viên khác cập nhật. Hãy tải lại trước khi tiếp tục.";
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            TempData["PartyError"] = "Không thể cập nhật đơn do thay đổi đồng thời. Hãy tải lại và thử lại.";
        }

        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    private async Task<PartyRequest?> FindVisibleRequestAsync(int id)
    {
        var query = context.PartyRequests.AsNoTracking().Where(request => request.Id == id);
        if (!User.IsInRole("Admin") && !User.IsInRole("Staff"))
            query = query.Where(request => request.CustomerUserId == User.FindFirstValue(ClaimTypes.NameIdentifier));
        return await query.FirstOrDefaultAsync();
    }

    private async Task<PartyRequestDetailsViewModel> BuildDetailsAsync(PartyRequest request, PartyRequestUpdateViewModel? update = null)
    {
        var isStaff = User.IsInRole("Admin") || User.IsInRole("Staff");
        var orderTotal = request.OrderId.HasValue
            ? await context.Orders.AsNoTracking().Where(order => order.Id == request.OrderId.Value)
                .Select(order => (decimal?)order.Total).FirstOrDefaultAsync()
            : null;
        var products = isStaff
            ? await context.Products.AsNoTracking().Where(product => product.IsAvailable).OrderBy(product => product.Name)
                .Select(product => new PartyProductChoiceViewModel { Id = product.Id, Name = product.Name, Price = product.Price }).ToListAsync()
            : [];
        var existingLines = request.OrderId is int orderId
            ? await context.OrderItems.AsNoTracking().Where(line => line.OrderId == orderId)
                .Select(line => new PartyOrderLineViewModel { ProductId = line.ProductId, Selected = true, Quantity = line.Quantity }).ToListAsync()
            : [];

        var cancellationReason = request.CancellationReason;
        if (update?.Status == PartyRequestStatus.Cancelled && !string.IsNullOrWhiteSpace(update.CancellationReason))
            cancellationReason = update.CancellationReason;

        return new PartyRequestDetailsViewModel
        {
            RowVersion = Convert.ToBase64String(request.RowVersion),
            Id = request.Id,
            CustomerName = request.CustomerName,
            Phone = isStaff ? request.Phone : string.Empty,
            Location = request.Location,
            EventAt = request.EventAt,
            Headcount = request.Headcount,
            Notes = request.Notes,
            Status = request.Status,
            QuoteAmount = update?.QuoteAmount ?? request.QuoteAmount,
            Consultation = update?.Consultation ?? request.Consultation,
            CancellationReason = cancellationReason,
            OrderId = request.OrderId,
            PaymentMethod = update?.PaymentMethod,
            VietQrUrl = MakeVietQrUrl(request.OrderId, orderTotal),
            IsStaffView = isStaff,
            Products = products,
            SelectedLines = update?.Lines ?? existingLines,
            StatusHistory = await context.PartyRequestStatusHistory.AsNoTracking()
                .Where(history => history.PartyRequestId == request.Id)
                .OrderBy(history => history.CreatedAt)
                .Select(history => new PartyRequestStatusHistoryRowViewModel
                {
                    Status = history.Status,
                    CreatedAt = history.CreatedAt,
                    Note = history.Note,
                    IsCustomerAction = history.ActorId == request.CustomerUserId
                })
                .ToListAsync()
        };
    }

    private string? MakeVietQrUrl(int? orderId, decimal? amount) =>
        orderId.HasValue && amount.HasValue ? VietQrPayment.MakeUrl(configuration, orderId.Value, amount.Value, "PARTY") : null;

    private static bool IsAllowedTransition(PartyRequestStatus current, PartyRequestStatus next) => current switch
    {
        PartyRequestStatus.Pending => next is PartyRequestStatus.InProgress or PartyRequestStatus.Cancelled,
        PartyRequestStatus.InProgress => next is PartyRequestStatus.Confirmed or PartyRequestStatus.Cancelled,
        PartyRequestStatus.Confirmed => next is PartyRequestStatus.Completed or PartyRequestStatus.Cancelled,
        _ => false
    };

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
    }
}
