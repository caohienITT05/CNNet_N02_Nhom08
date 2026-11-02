using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaigonChe.Web.Data;
using SaigonChe.Web.Models;
using SaigonChe.Web.ViewModels;

namespace SaigonChe.Web.Controllers
{
    [Authorize]
    public class FavoriteController : Controller
    {
        private readonly ApplicationDbContext _context;

        public FavoriteController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

            var favorites = await _context.Favorites
                .AsNoTracking()
                .Where(favorite => favorite.UserId == userId)
                .OrderByDescending(favorite => favorite.CreatedAt)
                .Select(favorite => new FavoriteProductViewModel
                {
                    ProductId = favorite.ProductId,
                    Name = favorite.Product!.Name,
                    ImageUrl = favorite.Product.ImageUrl,
                    Price = favorite.Product.Price,
                    IsAvailable = favorite.Product.IsAvailable,
                    CategoryName = favorite.Product.Category!.Name
                })
                .ToListAsync();

            return View(favorites);
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle(int productId, string? returnUrl)
        {
            var isAjaxRequest = Request.Headers.XRequestedWith == "XMLHttpRequest";
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                var destination = Url.IsLocalUrl(returnUrl)
                    ? returnUrl
                    : Url.Action("Index", "Home");
                var loginUrl = Url.Action("Login", "Account", new
                {
                    returnUrl = destination
                });

                if (isAjaxRequest)
                {
                    return Unauthorized(new
                    {
                        requiresLogin = true,
                        loginUrl
                    });
                }

                return RedirectToAction("Login", "Account", new
                {
                    returnUrl = destination
                });
            }

            if (productId <= 0)
            {
                return isAjaxRequest
                    ? BadRequest(new { message = "Món chè không hợp lệ." })
                    : NotFound();
            }

            var productExists = await _context.Products
                .AsNoTracking()
                .AnyAsync(product => product.Id == productId);
            if (!productExists)
            {
                return isAjaxRequest
                    ? NotFound(new { message = "Không tìm thấy món chè." })
                    : NotFound();
            }

            var favorite = await _context.Favorites
                .SingleOrDefaultAsync(item => item.UserId == userId && item.ProductId == productId);
            bool isFavorite;
            if (favorite == null)
            {
                isFavorite = true;
                _context.Favorites.Add(new Favorite
                {
                    UserId = userId,
                    ProductId = productId,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                isFavorite = false;
                _context.Favorites.Remove(favorite);
            }

            await _context.SaveChangesAsync();

            if (isAjaxRequest)
            {
                return Json(new
                {
                    isFavorite,
                    message = isFavorite ? "Đã thêm vào món yêu thích." : "Đã bỏ khỏi món yêu thích."
                });
            }

            TempData["FavoriteMessage"] = isFavorite
                ? "Đã thêm vào món yêu thích."
                : "Đã bỏ khỏi món yêu thích.";
            return Url.IsLocalUrl(returnUrl)
                ? LocalRedirect(returnUrl)
                : RedirectToAction(nameof(Index));
        }
    }
}
