using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaigonChe.Web.Data;
using SaigonChe.Web.ViewModels;
using System.Security.Claims;

namespace SaigonChe.Web.Controllers
{
    public class MenuController : Controller
    {
        private const int PageSize = 12;
        private const string SearchCollation = "Latin1_General_100_CI_AI";
        private readonly ApplicationDbContext _context;

        public MenuController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(MenuIndexViewModel model)
        {
            model.Search = model.Search?.Trim();
            if (model.MinPrice.HasValue && model.MaxPrice.HasValue
                && model.MinPrice.Value > model.MaxPrice.Value)
            {
                ModelState.AddModelError(nameof(model.MaxPrice), "Giá đến phải lớn hơn hoặc bằng giá từ.");
            }

            if (model.Status is not ("all" or "available" or "unavailable"))
            {
                ModelState.AddModelError(nameof(model.Status), "Trạng thái lọc không hợp lệ.");
                model.Status = "all";
            }

            if (model.Sort is not ("name" or "price-asc" or "price-desc"))
            {
                ModelState.AddModelError(nameof(model.Sort), "Kiểu sắp xếp không hợp lệ.");
                model.Sort = "name";
            }

            model.Categories = await _context.Categories
                .AsNoTracking()
                .OrderBy(category => category.Name)
                .Select(category => new MenuCategoryOptionViewModel
                {
                    Id = category.Id,
                    Name = category.Name
                })
                .ToListAsync();

            var products = _context.Products.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(model.Search))
            {
                products = products.Where(product =>
                    EF.Functions.Collate(product.Name, SearchCollation).Contains(model.Search));
            }

            if (model.CategoryId.HasValue)
            {
                products = products.Where(product => product.CategoryId == model.CategoryId.Value);
            }

            if (model.MinPrice.HasValue)
            {
                products = products.Where(product => product.Price >= model.MinPrice.Value);
            }

            if (model.MaxPrice.HasValue)
            {
                products = products.Where(product => product.Price <= model.MaxPrice.Value);
            }

            products = model.Status switch
            {
                "available" => products.Where(product => product.IsAvailable),
                "unavailable" => products.Where(product => !product.IsAvailable),
                _ => products
            };

            model.TotalProducts = await products.CountAsync();
            model.TotalPages = Math.Max(1, (int)Math.Ceiling(model.TotalProducts / (double)PageSize));
            model.Page = Math.Clamp(model.Page, 1, model.TotalPages);

            var orderedProducts = model.Sort switch
            {
                "price-asc" => products.OrderBy(product => product.Price).ThenBy(product => product.Name),
                "price-desc" => products.OrderByDescending(product => product.Price).ThenBy(product => product.Name),
                _ => products.OrderBy(product => product.Name)
            };
            model.Products = await orderedProducts
                .Skip((model.Page - 1) * PageSize)
                .Take(PageSize)
                .Select(product => new MenuProductItemViewModel
                {
                    Id = product.Id,
                    Name = product.Name,
                    Description = product.Description,
                    Price = product.Price,
                    CategoryName = product.Category!.Name,
                    IsAvailable = product.IsAvailable,
                    IsFeatured = product.IsFeatured,
                    ImageUrl = product.ImageUrl
                })
                .ToListAsync();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId) && model.Products.Count > 0)
            {
                var productIds = model.Products.Select(product => product.Id).ToList();
                var favoriteIds = await _context.Favorites
                    .AsNoTracking()
                    .Where(favorite => favorite.UserId == userId && productIds.Contains(favorite.ProductId))
                    .Select(favorite => favorite.ProductId)
                    .ToListAsync();
                var favoriteIdSet = favoriteIds.ToHashSet();
                foreach (var product in model.Products)
                {
                    product.IsFavorite = favoriteIdSet.Contains(product.Id);
                }
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var product = await _context.Products
                .AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => new MenuProductDetailsViewModel
                {
                    Id = item.Id,
                    Name = item.Name,
                    Description = item.Description,
                    Price = item.Price,
                    CategoryName = item.Category!.Name,
                    IsAvailable = item.IsAvailable,
                    ImageUrl = item.ImageUrl
                })
                .SingleOrDefaultAsync();
            if (product == null)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
            {
                product.IsFavorite = await _context.Favorites
                    .AsNoTracking()
                    .AnyAsync(favorite => favorite.UserId == userId && favorite.ProductId == id);
            }

            var visibleReviews = _context.Reviews
                .AsNoTracking()
                .Where(review => review.ProductId == id && !review.IsHidden);
            product.Reviews.ReviewCount = await visibleReviews.CountAsync();
            product.Reviews.AverageRating = await visibleReviews
                .AverageAsync(review => (double?)review.Rating) ?? 0;
            product.Reviews.Reviews = await visibleReviews
                .OrderByDescending(review => review.CreatedAt)
                .Select(review => new ProductReviewItemViewModel
                {
                    Id = review.Id,
                    UserId = review.UserId,
                    UserName = review.User!.FullName ?? review.User.UserName ?? "Khách hàng",
                    Rating = review.Rating,
                    Comment = review.Comment,
                    CreatedAt = review.CreatedAt,
                    AdminReply = review.AdminReply,
                    RepliedAt = review.RepliedAt,
                    IsCurrentUserReview = userId != null && review.UserId == userId
                })
                .ToListAsync();
            product.Reviews.ProductId = id;
            product.Reviews.IsAuthenticated = User.Identity?.IsAuthenticated == true;
            if (!string.IsNullOrEmpty(userId))
            {
                product.Reviews.CurrentUserReview = await _context.Reviews
                    .AsNoTracking()
                    .Where(review => review.ProductId == id && review.UserId == userId)
                    .Select(review => new ReviewFormViewModel
                    {
                        ReviewId = review.Id,
                        ProductId = review.ProductId,
                        Rating = review.Rating,
                        Comment = review.Comment
                    })
                    .SingleOrDefaultAsync();
            }

            return View(product);
        }
    }
}
