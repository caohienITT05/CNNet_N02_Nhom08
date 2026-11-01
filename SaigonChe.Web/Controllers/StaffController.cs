using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaigonChe.Web.Models;
using SaigonChe.Web.ViewModels;

namespace SaigonChe.Web.Controllers;

[Authorize(Roles = "Admin")]
public sealed class StaffController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;

    public StaffController(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

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

    [HttpGet]
    public IActionResult Create() => View(new StaffFormViewModel());

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

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var user = await FindStaffAsync(id);
        if (user is null) return NotFound();

        return View(new StaffEditViewModel { Id = user.Id, Email = user.Email ?? string.Empty });
    }

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

    [HttpGet]
    public async Task<IActionResult> ResetPassword(string id)
    {
        if (await FindStaffAsync(id) is null) return NotFound();
        return View(new StaffPasswordViewModel { Id = id });
    }

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
}
