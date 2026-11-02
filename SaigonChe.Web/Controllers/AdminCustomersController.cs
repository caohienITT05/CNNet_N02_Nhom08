using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaigonChe.Web.Data;
using SaigonChe.Web.Models;
using SaigonChe.Web.ViewModels;

namespace SaigonChe.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminCustomersController : Controller
    {
        private const int PageSize = 20;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminCustomersController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? search, int page = 1)
        {
            var customers = await GetCustomersQueryAsync();
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                customers = customers.Where(user =>
                    (user.Email != null && user.Email.Contains(search))
                    || (user.FullName != null && user.FullName.Contains(search)));
            }

            var count = await customers.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(count / (double)PageSize));
            page = Math.Clamp(page, 1, totalPages);
            var customerItems = await customers
                .OrderBy(user => user.Email)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .Select(user => new CustomerListItemViewModel
                {
                    Id = user.Id,
                    Name = user.FullName ?? user.UserName ?? "Khách hàng",
                    Email = user.Email ?? string.Empty,
                    PhoneNumber = user.PhoneNumber,
                    IsLocked = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow
                })
                .ToListAsync();

            return View(new CustomerListViewModel
            {
                Search = search,
                Page = page,
                TotalPages = totalPages,
                Customers = customerItems
            });
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new CreateCustomerViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateCustomerViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var email = model.Email.Trim();
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = model.FullName.Trim(),
                PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim()
            };

            var createResult = await _userManager.CreateAsync(user, model.Password);
            if (!createResult.Succeeded)
            {
                AddIdentityErrors(createResult);
                return View(model);
            }

            var roleResult = await _userManager.AddToRoleAsync(user, "Customer");
            if (!roleResult.Succeeded)
            {
                var cleanupResult = await _userManager.DeleteAsync(user);
                AddIdentityErrors(roleResult);
                if (!cleanupResult.Succeeded)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "Không thể gán vai trò và dọn tài khoản mới tạo. Vui lòng kiểm tra lại trong danh sách khách hàng.");
                }

                return View(model);
            }

            TempData["CustomerMessage"] = "Đã tạo tài khoản khách hàng.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Details(string id)
        {
            var user = await FindCustomerAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var hasKnownPhone = !string.IsNullOrWhiteSpace(user.PhoneNumber);
            var orderCount = await _context.Orders
                .AsNoTracking()
                .CountAsync(order => order.CustomerId == user.Id
                    || (hasKnownPhone && order.Phone == user.PhoneNumber));
            var reviewCount = await _context.Reviews
                .AsNoTracking()
                .CountAsync(review => review.UserId == user.Id);

            return View(new CustomerDetailsViewModel
            {
                Id = user.Id,
                Name = user.FullName ?? user.UserName ?? "Khách hàng",
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber,
                IsLocked = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow,
                OrderCount = orderCount,
                ReviewCount = reviewCount
            });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            var user = await FindCustomerAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            return View(new EditCustomerViewModel
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                FullName = user.FullName ?? string.Empty,
                PhoneNumber = user.PhoneNumber
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditCustomerViewModel model)
        {
            if (IsCurrentUser(model.Id))
            {
                return Forbid();
            }

            var user = await FindCustomerAsync(model.Id);
            if (user == null)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var email = model.Email.Trim();
            user.Email = email;
            user.NormalizedEmail = _userManager.NormalizeEmail(email);
            user.UserName = email;
            user.NormalizedUserName = _userManager.NormalizeName(email);
            user.FullName = model.FullName.Trim();
            user.PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim();

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                AddIdentityErrors(result);
                return View(model);
            }

            TempData["CustomerMessage"] = "Đã cập nhật thông tin khách hàng.";
            return RedirectToAction(nameof(Details), new { id = user.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Lock(string id)
        {
            return await SetLockoutAsync(id, isLocked: true);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unlock(string id)
        {
            return await SetLockoutAsync(id, isLocked: false);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            if (IsCurrentUser(id))
            {
                return Forbid();
            }

            var user = await FindCustomerAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var hasKnownPhone = !string.IsNullOrWhiteSpace(user.PhoneNumber);
            var hasOrders = await _context.Orders
                .AsNoTracking()
                .AnyAsync(order => order.CustomerId == user.Id
                    || (hasKnownPhone && order.Phone == user.PhoneNumber));
            var hasReviews = await _context.Reviews
                .AsNoTracking()
                .AnyAsync(review => review.UserId == user.Id);
            if (hasOrders || hasReviews)
            {
                TempData["CustomerError"] =
                    "Không thể xóa tài khoản đã có đơn hàng hoặc đánh giá. Hãy khóa tài khoản để ngừng sử dụng.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                TempData["CustomerError"] = "Không thể xóa tài khoản. Vui lòng thử lại hoặc khóa tài khoản.";
                return RedirectToAction(nameof(Details), new { id });
            }

            TempData["CustomerMessage"] = "Đã xóa tài khoản khách hàng.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<IActionResult> SetLockoutAsync(string id, bool isLocked)
        {
            if (IsCurrentUser(id))
            {
                return Forbid();
            }

            var user = await FindCustomerAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var enableResult = await _userManager.SetLockoutEnabledAsync(user, true);
            if (!enableResult.Succeeded)
            {
                TempData["CustomerError"] = "Không thể cập nhật trạng thái khóa tài khoản.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var lockoutEnd = isLocked ? DateTimeOffset.UtcNow.AddYears(100) : (DateTimeOffset?)null;
            var lockResult = await _userManager.SetLockoutEndDateAsync(user, lockoutEnd);
            if (!lockResult.Succeeded)
            {
                TempData["CustomerError"] = "Không thể cập nhật trạng thái khóa tài khoản.";
                return RedirectToAction(nameof(Details), new { id });
            }

            TempData["CustomerMessage"] = isLocked
                ? "Đã khóa tài khoản khách hàng."
                : "Đã mở khóa tài khoản khách hàng.";
            return RedirectToAction(nameof(Details), new { id });
        }

        private async Task<ApplicationUser?> FindCustomerAsync(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null
                || await _userManager.IsInRoleAsync(user, "Admin")
                || await _userManager.IsInRoleAsync(user, "Staff"))
            {
                return null;
            }

            return user;
        }

        private async Task<IQueryable<ApplicationUser>> GetCustomersQueryAsync()
        {
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            var staff = await _userManager.GetUsersInRoleAsync("Staff");
            var excludedIds = admins.Select(user => user.Id)
                .Concat(staff.Select(user => user.Id))
                .Distinct()
                .ToList();

            return _userManager.Users
                .AsNoTracking()
                .Where(user => !excludedIds.Contains(user.Id));
        }

        private bool IsCurrentUser(string userId)
        {
            return string.Equals(_userManager.GetUserId(User), userId, StringComparison.Ordinal);
        }

        private void AddIdentityErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                var message = error.Code switch
                {
                    "DuplicateEmail" or "DuplicateUserName" => "Email này đã được sử dụng.",
                    "PasswordTooShort" => "Mật khẩu phải có ít nhất 8 ký tự.",
                    "PasswordRequiresDigit" => "Mật khẩu cần có ít nhất một chữ số.",
                    "PasswordRequiresLower" => "Mật khẩu cần có ít nhất một chữ thường.",
                    "PasswordRequiresUpper" => "Mật khẩu cần có ít nhất một chữ hoa.",
                    "PasswordRequiresNonAlphanumeric" => "Mật khẩu cần có ít nhất một ký tự đặc biệt.",
                    _ => error.Description
                };
                ModelState.AddModelError(string.Empty, message);
            }
        }
    }
}
