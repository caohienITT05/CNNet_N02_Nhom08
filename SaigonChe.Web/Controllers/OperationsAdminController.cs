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

[Authorize(Roles = "Admin")]
public sealed class OperationsAdminController(ApplicationDbContext context) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Inventory()
    {
        var products = await context.Products.AsNoTracking().OrderBy(product => product.Name)
            .Select(product => new InventoryAdminViewModel { ProductId = product.Id, ProductName = product.Name, Stock = product.Stock })
            .ToListAsync();
        return View(products);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AdjustStock(InventoryChangeViewModel model)
    {
        if (!ModelState.IsValid || model.QuantityChange == 0)
        {
            TempData["AdminError"] = "Số lượng thay đổi phải khác 0 và nằm trong giới hạn cho phép.";
            return RedirectToAction(nameof(Inventory));
        }

        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var product = await context.Products.SingleOrDefaultAsync(item => item.Id == model.ProductId);
        if (product is null) return NotFound();
        var currentStock = product.Stock ?? 0;
        if ((long)currentStock + model.QuantityChange < 0 || (long)currentStock + model.QuantityChange > int.MaxValue)
        {
            TempData["AdminError"] = "Tồn kho không thể nhỏ hơn 0.";
            return RedirectToAction(nameof(Inventory));
        }

        product.Stock = currentStock + model.QuantityChange;
        context.InventoryMovements.Add(new InventoryMovement
        {
            ProductId = product.Id,
            ActorId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            QuantityChange = model.QuantityChange,
            Reason = model.Reason.Trim(),
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        TempData["AdminSuccess"] = $"Đã cập nhật tồn kho món {product.Name}: {product.Stock} phần.";
        return RedirectToAction(nameof(Inventory));
    }

    [HttpGet]
    public async Task<IActionResult> DiningTables()
    {
        var tables = await context.DiningTables.AsNoTracking().OrderBy(table => table.Code).ToListAsync();
        return View(tables);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveDiningTable(DiningTableFormViewModel model)
    {
        if (!ModelState.IsValid)
            return View("DiningTables", await context.DiningTables.AsNoTracking().OrderBy(table => table.Code).ToListAsync());
        var code = model.Code.Trim().ToUpperInvariant();
        if (await context.DiningTables.AnyAsync(table => table.Code == code && table.Id != model.Id))
            ModelState.AddModelError(nameof(model.Code), "Mã bàn đã tồn tại.");
        if (!ModelState.IsValid) return View("DiningTables", await context.DiningTables.AsNoTracking().OrderBy(table => table.Code).ToListAsync());

        var table = model.Id == 0 ? new DiningTable() : await context.DiningTables.FindAsync(model.Id);
        if (table is null) return NotFound();
        if (table.IsActive && !model.IsActive && await context.Orders.AnyAsync(order =>
                order.TableId == table.Id && order.Type == OrderType.Pos && !order.IsPaid && order.Status != OrderStatus.Cancelled))
        {
            ModelState.AddModelError(string.Empty, "Không thể ngừng bàn đang có đơn POS chưa thanh toán.");
            return View("DiningTables", await context.DiningTables.AsNoTracking().OrderBy(item => item.Code).ToListAsync());
        }
        table.Code = code;
        table.Name = model.Name.Trim();
        table.Capacity = model.Capacity;
        table.IsActive = model.IsActive;
        if (model.Id == 0) context.DiningTables.Add(table);
        await context.SaveChangesAsync();
        TempData["AdminSuccess"] = "Đã lưu thông tin bàn.";
        return RedirectToAction(nameof(DiningTables));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleDiningTable(int id)
    {
        var table = await context.DiningTables.FindAsync(id);
        if (table is null) return NotFound();
        if (table.IsActive && await context.Orders.AnyAsync(order => order.TableId == id && !order.IsPaid && order.Status != OrderStatus.Cancelled))
        {
            TempData["AdminError"] = "Không thể ngừng bàn đang có đơn POS chưa thanh toán.";
            return RedirectToAction(nameof(DiningTables));
        }
        table.IsActive = !table.IsActive;
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(DiningTables));
    }

    [HttpGet]
    public async Task<IActionResult> Vouchers()
    {
        var vouchers = await context.Vouchers.AsNoTracking().OrderByDescending(voucher => voucher.ValidTo).ToListAsync();
        return View(vouchers);
    }

    [HttpGet]
    public IActionResult CreateVoucher()
    {
        var today = DateOnly.FromDateTime(VietnamTime.ToLocal(DateTime.UtcNow)).ToDateTime(TimeOnly.MinValue);
        return View("VoucherForm", new VoucherFormViewModel { ValidFrom = today, ValidTo = today.AddDays(30) });
    }

    [HttpGet]
    public async Task<IActionResult> EditVoucher(int id)
    {
        var voucher = await context.Vouchers.FindAsync(id);
        if (voucher is null) return NotFound();
        return View("VoucherForm", new VoucherFormViewModel
        {
            Id = voucher.Id, Code = voucher.Code, Type = voucher.Type, Amount = voucher.Amount,
            ValidFrom = VietnamTime.ToLocal(voucher.ValidFrom).Date, ValidTo = VietnamTime.ToLocal(voucher.ValidTo).Date, MinimumOrderTotal = voucher.MinimumOrderTotal,
            UseLimit = voucher.UseLimit, UseCount = voucher.UseCount, IsActive = voucher.IsActive
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveVoucher(VoucherFormViewModel model)
    {
        if (!ModelState.IsValid) return View("VoucherForm", model);
        if (!Enum.IsDefined(model.Type)) ModelState.AddModelError(nameof(model.Type), "Loại voucher không hợp lệ.");
        if (model.ValidFrom.Date == DateTime.MinValue.Date || model.ValidTo.Date == DateTime.MaxValue.Date)
            ModelState.AddModelError(nameof(model.ValidTo), "Khoảng ngày nằm ngoài giới hạn được hỗ trợ.");
        model.Code = model.Code.Trim().ToUpperInvariant();
        if (await context.Vouchers.AnyAsync(voucher => voucher.Code == model.Code && voucher.Id != model.Id))
            ModelState.AddModelError(nameof(model.Code), "Mã voucher đã tồn tại.");
        if (model.ValidTo < model.ValidFrom) ModelState.AddModelError(nameof(model.ValidTo), "Ngày kết thúc phải sau ngày bắt đầu.");
        if (model.Type == VoucherType.Percentage && model.Amount > 100) ModelState.AddModelError(nameof(model.Amount), "Mức giảm phần trăm tối đa là 100%.");
        if (!ModelState.IsValid) return View("VoucherForm", model);

        var voucher = model.Id == 0 ? new Voucher() : await context.Vouchers.FindAsync(model.Id);
        if (voucher is null) return NotFound();
        voucher.Code = model.Code;
        voucher.Type = model.Type;
        voucher.Amount = model.Amount;
        voucher.ValidFrom = VietnamTime.ToUtc(model.ValidFrom.Date);
        voucher.ValidTo = VietnamTime.ToUtc(model.ValidTo.Date.AddDays(1).AddTicks(-1));
        voucher.MinimumOrderTotal = model.MinimumOrderTotal;
        voucher.UseLimit = model.UseLimit;
        voucher.IsActive = model.IsActive;
        if (model.Id == 0) context.Vouchers.Add(voucher);
        await context.SaveChangesAsync();
        TempData["AdminSuccess"] = "Đã lưu voucher.";
        return RedirectToAction(nameof(Vouchers));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleVoucher(int id)
    {
        var voucher = await context.Vouchers.FindAsync(id);
        if (voucher is null) return NotFound();
        voucher.IsActive = !voucher.IsActive;
        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Vouchers));
    }
}
