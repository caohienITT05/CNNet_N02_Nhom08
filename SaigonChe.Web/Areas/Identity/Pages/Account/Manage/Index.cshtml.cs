using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SaigonChe.Web.Models;

namespace SaigonChe.Web.Areas.Identity.Pages.Account.Manage;

[Authorize]
public class ProfileModel(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager) : PageModel
{
    public string? UserName { get; private set; }

    [BindProperty]
    [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
    public string? PhoneNumber { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return NotFound();
        }

        UserName = await userManager.GetUserNameAsync(user);
        PhoneNumber = await userManager.GetPhoneNumberAsync(user);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return NotFound();
        }

        UserName = await userManager.GetUserNameAsync(user);
        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (PhoneNumber != await userManager.GetPhoneNumberAsync(user))
        {
            var result = await userManager.SetPhoneNumberAsync(user, PhoneNumber);
            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, "Không thể lưu số điện thoại. Vui lòng thử lại.");
                return Page();
            }

            await signInManager.RefreshSignInAsync(user);
        }

        TempData["ProfileSaved"] = "Đã lưu thay đổi.";
        return RedirectToPage();
    }
}
