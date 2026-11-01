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
public sealed class OrderOperationsController(
    ApplicationDbContext context,
    OrderCompletionService completionService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var orders = await context.Orders.AsNoTracking().Include(order => order.Items)
            .Where(order => order.Type == OrderType.Online && order.Status != OrderStatus.Cancelled)
            .OrderBy(order => order.Status == OrderStatus.Completed)
            .ThenBy(order => order.CreatedAt)
            .ToListAsync();
        return View(orders);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(StaffOrderUpdateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["AdminError"] = "Thông tin cập nhật đơn chưa hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var order = await context.Orders.Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.Id == model.OrderId && item.Type == OrderType.Online);
        if (order is null) return NotFound();
        if (!TryMatchVersion(model.RowVersion, order.RowVersion))
        {
            await transaction.RollbackAsync();
            TempData["AdminError"] = "Đơn vừa được cập nhật bởi người khác. Hãy tải lại hàng đợi.";
            return RedirectToAction(nameof(Index));
        }

        if (!IsAllowed(order.Status, model.Status) ||
            (model.Status == OrderStatus.Cancelled && string.IsNullOrWhiteSpace(model.Note)))
        {
            TempData["AdminError"] = "Trạng thái không hợp lệ; nhập lý do khi hủy đơn.";
            return RedirectToAction(nameof(Index));
        }

        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (model.Status == OrderStatus.Completed)
        {
            if (order.PaymentMethod != PaymentMethod.CashOnDelivery)
            {
                TempData["AdminError"] = "Đơn trực tuyến COD cần có phương thức thanh toán xác nhận.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                await completionService.CompleteAsync(order, PaymentMethod.Cash, actorId,
                    "Thu tiền COD và hoàn tất đơn trực tuyến.");
            }
            catch (OrderCompletionException exception)
            {
                await transaction.RollbackAsync();
                TempData["AdminError"] = exception.Message;
                return RedirectToAction(nameof(Index));
            }
        }
        else
        {
            var previous = order.Status;
            order.Status = model.Status;
            order.AssignedStaffId ??= actorId;
            context.OrderStatusHistory.Add(new OrderStatusHistory
            {
                OrderId = order.Id,
                PreviousStatus = previous,
                Status = model.Status,
                ActorId = actorId,
                Note = model.Note?.Trim(),
                CreatedAt = DateTime.UtcNow
            });
        }

        try
        {
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
            TempData["AdminSuccess"] = $"Đã cập nhật đơn #{order.Id}.";
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            TempData["AdminError"] = "Đơn vừa được cập nhật bởi người khác. Hãy tải lại hàng đợi.";
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            TempData["AdminError"] = "Không thể lưu trạng thái đơn. Hãy tải lại và thử lại.";
        }
        return RedirectToAction(nameof(Index));
    }

    private static bool IsAllowed(OrderStatus current, OrderStatus next) => current switch
    {
        OrderStatus.Pending => next is OrderStatus.Confirmed or OrderStatus.Cancelled,
        OrderStatus.Confirmed => next is OrderStatus.Preparing or OrderStatus.Cancelled,
        OrderStatus.Preparing => next is OrderStatus.Completed or OrderStatus.Cancelled,
        _ => false
    };

    private static bool TryMatchVersion(string submitted, byte[] current)
    {
        try { return Convert.FromBase64String(submitted).SequenceEqual(current); }
        catch (FormatException) { return false; }
    }
}
