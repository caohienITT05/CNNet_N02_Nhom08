using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SaigonChe.Web.Controllers;

// Compatibility routes for old bookmarks. Product management now uses the
// protected MVC admin controller and no longer calls the legacy API.
[Authorize(Roles = "Admin")]
public sealed class ProductController : Controller
{
    [HttpGet]
    public IActionResult Index() => RedirectToAction("Products", "Admin");

    [HttpGet]
    public IActionResult Create() => RedirectToAction("CreateProduct", "Admin");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(IFormCollection form) => RedirectToAction("CreateProduct", "Admin");

    [HttpGet]
    public IActionResult Edit(int id) => RedirectToAction("EditProduct", "Admin", new { id });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, IFormCollection form) => RedirectToAction("EditProduct", "Admin", new { id });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id) => RedirectToAction("Products", "Admin");
}
