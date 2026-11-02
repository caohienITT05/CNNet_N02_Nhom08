using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SaigonChe.Web.Data;
using SaigonChe.Web.Models;
using SaigonChe.Web.ViewModels;
using System.Security.Claims;

namespace SaigonChe.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminReviewsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminReviewsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? productId, int? rating, bool? isHidden)
        {
            var model = new AdminReviewsViewModel
            {
                ProductId = productId,
                Rating = rating,
                IsHidden = isHidden,
                Products = await _context.Products
                    .AsNoTracking()
                    .OrderBy(product => product.Name)
                    .Select(product => new SelectListItem(product.Name, product.Id.ToString()))
                    .ToListAsync()
            };

            var reviews = _context.Reviews
                .AsNoTracking()
                .Include(review => review.Product)
                .Include(review => review.User)
                .AsQueryable();
            if (productId.HasValue)
            {
                reviews = reviews.Where(review => review.ProductId == productId.Value);
            }

            if (rating.HasValue)
            {
                reviews = reviews.Where(review => review.Rating == rating.Value);
            }

            if (isHidden.HasValue)
            {
                reviews = reviews.Where(review => review.IsHidden == isHidden.Value);
            }

            model.Reviews = await reviews
                .OrderByDescending(review => review.CreatedAt)
                .Select(review => new AdminReviewItemViewModel
                {
                    Id = review.Id,
                    ProductId = review.ProductId,
                    ProductName = review.Product!.Name,
                    CustomerName = review.User!.FullName ?? review.User.UserName ?? review.User.Email ?? "Khách hàng",
                    Rating = review.Rating,
                    Comment = review.Comment,
                    CreatedAt = review.CreatedAt,
                    IsHidden = review.IsHidden,
                    AdminReply = review.AdminReply,
                    RepliedAt = review.RepliedAt
                })
                .ToListAsync();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetVisibility(
            int id,
            bool isHidden,
            int? productId,
            int? rating,
            bool? hiddenFilter)
        {
            var review = await _context.Reviews.FindAsync(id);
            if (review == null)
            {
                return NotFound();
            }

            review.IsHidden = isHidden;
            await _context.SaveChangesAsync();
            TempData["AdminReviewMessage"] = isHidden
                ? "Đã ẩn đánh giá."
                : "Đã hiển thị đánh giá.";
            return RedirectToAction(nameof(Index), new { productId, rating, isHidden = hiddenFilter });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reply(AdminReviewReplyViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["AdminReviewError"] = string.Join(
                    " ",
                    ModelState.Values.SelectMany(value => value.Errors).Select(error => error.ErrorMessage));
                return RedirectToAction(nameof(Index), new
                {
                    productId = model.ProductId,
                    rating = model.Rating,
                    isHidden = model.IsHidden
                });
            }

            var review = await _context.Reviews.FindAsync(model.ReviewId);
            if (review == null)
            {
                return NotFound();
            }

            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(adminId))
            {
                return Forbid();
            }

            review.AdminReply = model.AdminReply.Trim();
            review.RepliedAt = DateTime.UtcNow;
            review.RepliedById = adminId;
            await _context.SaveChangesAsync();

            TempData["AdminReviewMessage"] = "Đã lưu câu trả lời.";
            return RedirectToAction(nameof(Index), new
            {
                productId = model.ProductId,
                rating = model.Rating,
                isHidden = model.IsHidden
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteReply(
            int id,
            int? productId,
            int? rating,
            bool? isHidden)
        {
            var review = await _context.Reviews.FindAsync(id);
            if (review == null)
            {
                return NotFound();
            }

            review.AdminReply = null;
            review.RepliedAt = null;
            review.RepliedById = null;
            await _context.SaveChangesAsync();

            TempData["AdminReviewMessage"] = "Đã xóa câu trả lời.";
            return RedirectToAction(nameof(Index), new { productId, rating, isHidden });
        }
    }
}
