using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SaigonChe.Web.Controllers;

// Compatibility routes for old bookmarks. Category management now goes
// through the protected MVC admin controller and no longer calls the API.
[Authorize(Roles = "Admin")]
public sealed class CategoryController : Controller
{
    [HttpGet]
    public IActionResult Index() => RedirectToAction("Categories", "Admin");

    [HttpGet]
    public IActionResult Create() => RedirectToAction("CreateCategory", "Admin");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(IFormCollection form) => RedirectToAction("CreateCategory", "Admin");

    [HttpGet]
    public IActionResult Edit(int id) => RedirectToAction("EditCategory", "Admin", new { id });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, IFormCollection form) => RedirectToAction("EditCategory", "Admin", new { id });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id) => RedirectToAction("Categories", "Admin");
}
