using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaigonChe.Web.Data;
using SaigonChe.Web.Models;
using SaigonChe.Web.ViewModels;

namespace SaigonChe.Web.Controllers;

[Authorize(Roles = "Staff,Admin")]
public sealed class StaffController : Controller
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

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var staff = await _userManager.GetUsersInRoleAsync("Staff");
        var rows = new List<StaffRowViewModel>();
        foreach (var user in staff)
        {
            rows.Add(new StaffRowViewModel
            {
                Id = user.Id,
                Email = user.Email ?? user.UserName ?? string.Empty,
                IsLocked = await _userManager.IsLockedOutAsync(user)
            });
        }

        return View(rows.OrderBy(row => row.Email).ToList());
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public IActionResult Create() => View(new StaffFormViewModel());

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StaffFormViewModel model)
    {
        var email = model.Email.Trim();
        if (await _userManager.FindByEmailAsync(email) is not null)
        {
            ModelState.AddModelError(nameof(model.Email), "Email này đã được sử dụng.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
        var createResult = await _userManager.CreateAsync(user, model.Password);
        if (!createResult.Succeeded)
        {
            AddIdentityErrors(createResult);
            return View(model);
        }

        var roleResult = await _userManager.AddToRoleAsync(user, "Staff");
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            AddIdentityErrors(roleResult);
            return View(model);
        }

        TempData["AdminSuccess"] = "Đã tạo tài khoản nhân viên.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var user = await FindStaffAsync(id);
        if (user is null) return NotFound();

        return View(new StaffEditViewModel { Id = user.Id, Email = user.Email ?? string.Empty });
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(StaffEditViewModel model)
    {
        var user = await FindStaffAsync(model.Id);
        if (user is null) return NotFound();

        var email = model.Email.Trim();
        var existing = await _userManager.FindByEmailAsync(email);
        if (existing is not null && existing.Id != user.Id)
        {
            ModelState.AddModelError(nameof(model.Email), "Email này đã được sử dụng.");
        }

        if (!ModelState.IsValid) return View(model);

        user.Email = email;
        user.UserName = email;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            AddIdentityErrors(result);
            return View(model);
        }

        await _userManager.UpdateSecurityStampAsync(user);
        TempData["AdminSuccess"] = "Đã cập nhật tài khoản nhân viên.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> ResetPassword(string id)
    {
        if (await FindStaffAsync(id) is null) return NotFound();
        return View(new StaffPasswordViewModel { Id = id });
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(StaffPasswordViewModel model)
    {
        var user = await FindStaffAsync(model.Id);
        if (user is null) return NotFound();
        if (!ModelState.IsValid) return View(model);

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, model.Password);
        if (!result.Succeeded)
        {
            AddIdentityErrors(result);
            return View(model);
        }

        await _userManager.UpdateSecurityStampAsync(user);
        TempData["AdminSuccess"] = "Đã đặt lại mật khẩu và thu hồi phiên đăng nhập cũ.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetLocked(string id, bool locked)
    {
        var user = await FindStaffAsync(id);
        if (user is null) return NotFound();

        await _userManager.SetLockoutEnabledAsync(user, true);
        await _userManager.SetLockoutEndDateAsync(
            user,
            locked ? DateTimeOffset.UtcNow.AddYears(100) : null);
        await _userManager.ResetAccessFailedCountAsync(user);
        await _userManager.UpdateSecurityStampAsync(user);

        TempData["AdminSuccess"] = locked
            ? "Đã khóa tài khoản và thu hồi phiên đăng nhập."
            : "Đã mở khóa tài khoản.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<ApplicationUser?> FindStaffAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        return user is not null && await _userManager.IsInRoleAsync(user, "Staff") ? user : null;
    }

    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
    }

    [Authorize(Roles = "Staff,Admin")]
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
                    .ThenInclude(history => history.Actor)
                    .Include(order => order.StaffStatusHistory)
                    .OrderBy(order => order.Status)
                    .ThenBy(order => order.CreatedAt)
                    .ToListAsync()
            };

        return View(model);
    }

    [Authorize(Roles = "Staff,Admin")]
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

    [Authorize(Roles = "Staff,Admin")]
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
            _context.OrderStatusHistories.Add(new StaffOrderStatusHistory
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
