using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaigonChe.Web.Data;
using SaigonChe.Web.Models;
using SaigonChe.Web.ViewModels;

namespace SaigonChe.Web.Controllers
{
    [Authorize(Roles = "Staff,Admin")]
    public class StaffController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<StaffController> _logger;

        public StaffController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<StaffController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Orders(OrderStatus? status)
        {
            if (status.HasValue && !Enum.IsDefined(status.Value))
            {
                return BadRequest();
            }

            var query = _context.Orders.AsNoTracking().AsQueryable();
            if (!User.IsInRole("Admin"))
            {
                var staffId = _userManager.GetUserId(User);
                query = query.Where(order =>
                    order.AssignedStaffId == staffId
                    || (order.AssignedStaffId == null && order.Status == OrderStatus.Pending));
            }

            if (status.HasValue)
            {
                query = query.Where(order => order.Status == status.Value);
            }

            var model = new StaffOrdersViewModel
            {
                FilterStatus = status,
                Orders = await query
                    .Include(order => order.Items)
                    .Include(order => order.AssignedStaff)
                    .Include(order => order.StatusHistory)
                    .OrderBy(order => order.Status)
                    .ThenBy(order => order.CreatedAt)
                    .ToListAsync()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Accept(int id)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(item => item.Id == id);
            if (order == null)
            {
                return NotFound();
            }

            if (order.Status != OrderStatus.Pending || order.AssignedStaffId != null)
            {
                TempData["StaffError"] = $"Đơn #{id} đã được một nhân viên khác nhận hoặc không còn chờ xác nhận.";
                return RedirectToAction(nameof(Orders));
            }

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Forbid();
            }

            order.AssignedStaffId = userId;
            order.Status = OrderStatus.Confirmed;
            AddStatusHistory(order.Id, OrderStatus.Pending, OrderStatus.Confirmed, "Nhân viên đã tiếp nhận đơn.");

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException exception)
            {
                _logger.LogInformation(exception, "Concurrent attempt to accept order {OrderId}.", id);
                TempData["StaffError"] = $"Đơn #{id} vừa được một nhân viên khác nhận. Danh sách đã được làm mới.";
                return RedirectToAction(nameof(Orders));
            }

            TempData["StaffSuccess"] = $"Đã tiếp nhận đơn #{id}.";
            return RedirectToAction(nameof(Orders));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, OrderStatus status, string? note)
        {
            if (!Enum.IsDefined(status))
            {
                TempData["StaffError"] = "Trạng thái đơn hàng không hợp lệ.";
                return RedirectToAction(nameof(Orders));
            }

            if (note?.Length > 500)
            {
                TempData["StaffError"] = "Ghi chú không được vượt quá 500 ký tự.";
                return RedirectToAction(nameof(Orders));
            }

            var order = await _context.Orders.FirstOrDefaultAsync(item => item.Id == id);
            if (order == null)
            {
                return NotFound();
            }

            var userId = _userManager.GetUserId(User);
            if (!User.IsInRole("Admin") && order.AssignedStaffId != userId)
            {
                return Forbid();
            }

            if (!IsNextStatus(order.Status, status))
            {
                TempData["StaffError"] = $"Không thể chuyển đơn #{id} từ {order.Status} sang {status}.";
                return RedirectToAction(nameof(Orders));
            }

            var previousStatus = order.Status;
            order.Status = status;
            AddStatusHistory(order.Id, previousStatus, status, note);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException exception)
            {
                _logger.LogInformation(exception, "Concurrent status update for order {OrderId}.", id);
                TempData["StaffError"] = $"Đơn #{id} vừa được cập nhật ở nơi khác. Hãy kiểm tra trạng thái mới.";
                return RedirectToAction(nameof(Orders));
            }

            TempData["StaffSuccess"] = $"Đã cập nhật đơn #{id} sang trạng thái {status}.";
            return RedirectToAction(nameof(Orders));
        }

        private void AddStatusHistory(int orderId, OrderStatus? previousStatus, OrderStatus status, string? note)
        {
            _context.OrderStatusHistories.Add(new OrderStatusHistory
            {
                OrderId = orderId,
                PreviousStatus = previousStatus,
                Status = status,
                ChangedByUserId = _userManager.GetUserId(User),
                ChangedBy = User.Identity?.Name,
                ChangedAt = DateTime.UtcNow,
                Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()
            });
        }

        private static bool IsNextStatus(OrderStatus current, OrderStatus next)
        {
            return (current, next) switch
            {
                (OrderStatus.Confirmed, OrderStatus.Preparing) => true,
                (OrderStatus.Preparing, OrderStatus.Delivering) => true,
                (OrderStatus.Delivering, OrderStatus.Completed) => true,
                _ => false
            };
        }
    }
}
