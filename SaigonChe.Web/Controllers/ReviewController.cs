using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaigonChe.Web.Data;
using SaigonChe.Web.Models;
using SaigonChe.Web.ViewModels;
using System.Security.Claims;

namespace SaigonChe.Web.Controllers
{
    [Authorize]
    public class ReviewController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReviewController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ReviewFormViewModel model)
        {
            if (model.ProductId <= 0)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                TempData["ReviewError"] = GetValidationMessage();
                return RedirectToDetails(model.ProductId);
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

            var productExists = await _context.Products
                .AsNoTracking()
                .AnyAsync(product => product.Id == model.ProductId);
            if (!productExists)
            {
                return NotFound();
            }

            var review = await _context.Reviews
                .SingleOrDefaultAsync(item => item.ProductId == model.ProductId && item.UserId == userId);
            if (review == null)
            {
                review = new Review
                {
                    ProductId = model.ProductId,
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Reviews.Add(review);
            }

            review.Rating = model.Rating;
            review.Comment = model.Comment.Trim();
            await _context.SaveChangesAsync();

            TempData["ReviewSuccess"] = "Đánh giá của bạn đã được lưu.";
            return RedirectToDetails(model.ProductId);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(ReviewFormViewModel model)
        {
            if (model.ProductId <= 0 || !model.ReviewId.HasValue)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

            var review = await _context.Reviews
                .SingleOrDefaultAsync(item => item.Id == model.ReviewId && item.UserId == userId);
            if (review == null)
            {
                return NotFound();
            }

            if (review.ProductId != model.ProductId)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                TempData["ReviewError"] = GetValidationMessage();
                return RedirectToDetails(model.ProductId);
            }

            review.Rating = model.Rating;
            review.Comment = model.Comment.Trim();
            await _context.SaveChangesAsync();

            TempData["ReviewSuccess"] = "Đánh giá của bạn đã được cập nhật.";
            return RedirectToDetails(model.ProductId);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, int productId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

            var review = await _context.Reviews
                .SingleOrDefaultAsync(item => item.Id == id && item.UserId == userId);
            if (review == null)
            {
                return NotFound();
            }

            if (review.ProductId != productId)
            {
                return BadRequest();
            }

            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();

            TempData["ReviewSuccess"] = "Đánh giá của bạn đã được xóa.";
            return RedirectToDetails(productId);
        }

        private string GetValidationMessage()
        {
            var errors = ModelState.Values
                .SelectMany(value => value.Errors)
                .Select(error => error.ErrorMessage)
                .Where(message => !string.IsNullOrWhiteSpace(message));
            return string.Join(" ", errors);
        }

        private IActionResult RedirectToDetails(int productId)
        {
            return RedirectToAction("Details", "Menu", new { id = productId });
        }
    }
}
