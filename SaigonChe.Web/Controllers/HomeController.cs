using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaigonChe.Web.Data;
using SaigonChe.Web.Models;
using SaigonChe.Web.Services;
using SaigonChe.Web.ViewModels;
using System.Diagnostics;
using System.Security.Claims;

namespace SaigonChe.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(WeatherCondition? weather)
        {
            var products = await LoadMenuAsync();
            var latestReviews = await _context.Reviews
                .AsNoTracking()
                .Where(review => !review.IsHidden)
                .OrderByDescending(review => review.CreatedAt)
                .Take(3)
                .Select(review => new TestimonialViewModel
                {
                    Name = review.User!.FullName ?? review.User.UserName ?? "Khách hàng",
                    Rating = review.Rating,
                    Content = review.Comment
                })
                .ToListAsync();
            foreach (var review in latestReviews)
            {
                review.Avatar = CreateAvatar(review.Name);
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
            {
                var favoriteProductIds = await _context.Favorites
                    .AsNoTracking()
                    .Where(favorite => favorite.UserId == userId)
                    .Select(favorite => favorite.ProductId)
                    .ToHashSetAsync();

                foreach (var product in products)
                {
                    product.IsFavorite = favoriteProductIds.Contains(product.Id);
                }
            }

            var selectedWeather = weather.HasValue && Enum.IsDefined(weather.Value)
                ? weather
                : null;
            var model = new LandingPageViewModel
            {
                Products = products,
                BestSellers = products.Where(p => p.IsFeatured).Take(4).ToList(),
                SelectedWeather = selectedWeather,
                WeatherDescription = selectedWeather.HasValue
                    ? WeatherRecommendationService.GetDescription(selectedWeather.Value)
                    : null,
                WeatherRecommendations = selectedWeather.HasValue
                    ? WeatherRecommendationService.GetRecommendations(products, selectedWeather.Value)
                    : new List<ProductViewModel>(),
                Categories = new List<string>
                {
                    "Tất cả",
                    "Chè truyền thống",
                    "Chè hiện đại",
                    "Món bán chạy"
                },
                Testimonials = latestReviews.Count > 0
                    ? latestReviews
                    : GetSampleTestimonials(),
                HasLiveTestimonials = latestReviews.Count > 0
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Contact(ContactFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ContactError"] = "Vui lòng kiểm tra lại thông tin liên hệ trước khi gửi.";
                return RedirectToAction(nameof(Index));
            }

            _context.ContactMessages.Add(new ContactMessage
            {
                FullName = model.FullName.Trim(),
                Contact = model.Contact.Trim(),
                Message = model.Message.Trim(),
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            TempData["ContactSuccess"] = "Cảm ơn bạn đã liên hệ với Chè Sài Gòn. Chúng tôi sẽ phản hồi sớm nhất!";
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        private async Task<List<ProductViewModel>> LoadMenuAsync()
        {
            var dbProducts = await _context.Products
                .Include(p => p.Category)
                .Where(p => p.IsAvailable)
                .OrderBy(p => p.Name)
                .ToListAsync();

            if (dbProducts.Count > 0)
            {
                return dbProducts.Select(product => new ProductViewModel
                {
                    Id = product.Id,
                    Name = product.Name,
                    Price = product.Price,
                    Description = product.Description,
                    IsAvailable = product.IsAvailable,
                    IsFeatured = product.IsFeatured,
                    CategoryId = product.CategoryId,
                    CategoryName = product.Category?.Name ?? "Chè truyền thống",
                    CategoryTag = DetermineCategoryTag(product.Category?.Name),
                    ImageUrl = product.ImageUrl
                }).ToList();
            }

            return new List<ProductViewModel>
            {
                new() { Id = 1, Name = "Chè ba màu", Description = "Sữa dừa, đậu xanh và thạch ngọt, tạo nên vị ngọt thanh và màu sắc bắt mắt.", Price = 25000m, CategoryName = "Chè truyền thống", CategoryTag = "traditional", IsAvailable = true, IsFeatured = true, ImageUrl = "/images/menu/che-ba-mau.jpg" },
                new() { Id = 2, Name = "Chè khúc bạch", Description = "Khúc bạch mềm mịn, hương thơm dịu nhẹ, phủ đầy sữa dừa đặc trưng.", Price = 30000m, CategoryName = "Chè truyền thống", CategoryTag = "traditional", IsAvailable = true, IsFeatured = true, ImageUrl = "/images/menu/che-khuc-bach.jpg" },
                new() { Id = 3, Name = "Chè sầu riêng", Description = "Vị sầu riêng đậm đà, quyện cùng đá xay mát lạnh cho hương vị hấp dẫn.", Price = 45000m, CategoryName = "Chè hiện đại", CategoryTag = "modern", IsAvailable = true, IsFeatured = true, ImageUrl = "/images/menu/che-sau-rieng.jpg" },
                new() { Id = 4, Name = "Chè đậu xanh", Description = "Đậu xanh mềm, đường phèn ngọt thanh, điểm xuyết dừa nạo thơm béo.", Price = 22000m, CategoryName = "Chè truyền thống", CategoryTag = "traditional", IsAvailable = true, IsFeatured = false, ImageUrl = "/images/menu/che-dau-xanh.jpg" },
                new() { Id = 5, Name = "Chè bưởi", Description = "Bưởi sợi tươi mát, nước chè thanh nhẹ và chút mùi thơm của hoa quế.", Price = 28000m, CategoryName = "Chè hiện đại", CategoryTag = "modern", IsAvailable = true, IsFeatured = false, ImageUrl = "/images/menu/che-buoi.jpg" },
                new() { Id = 6, Name = "Chè khoai dẻo", Description = "Khoai dẻo bùi, sền sệt và đậm vị trong từng ngụm, rất được giới trẻ yêu thích.", Price = 26000m, CategoryName = "Chè truyền thống", CategoryTag = "traditional", IsAvailable = true, IsFeatured = false, ImageUrl = "/images/menu/che-khoai-deo.jpg" },
                new() { Id = 7, Name = "Chè thái", Description = "Vị chua ngọt đậm đà, kết hợp đa dạng nguyên liệu và topping thơm ngon.", Price = 32000m, CategoryName = "Chè hiện đại", CategoryTag = "modern", IsAvailable = true, IsFeatured = false, ImageUrl = "/images/menu/che-thai.jpg" },
                new() { Id = 8, Name = "Chè dừa dầm", Description = "Dừa nạo béo ngậy, sữa dừa ngọt dịu, là lựa chọn ưa chuộng của nhiều khách hàng.", Price = 27000m, CategoryName = "Chè truyền thống", CategoryTag = "traditional", IsAvailable = true, IsFeatured = false, ImageUrl = "/images/menu/che-dua-dam.jpg" }
            };
        }

        private static string DetermineCategoryTag(string? categoryName)
        {
            return string.Equals(categoryName, "Chè hiện đại", StringComparison.OrdinalIgnoreCase)
                ? "modern"
                : "traditional";
        }

        private static string CreateAvatar(string name)
        {
            var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return words.Length switch
            {
                0 => "KH",
                1 => words[0][..Math.Min(2, words[0].Length)].ToUpperInvariant(),
                _ => $"{char.ToUpperInvariant(words[0][0])}{char.ToUpperInvariant(words[^1][0])}"
            };
        }

        private static List<TestimonialViewModel> GetSampleTestimonials()
        {
            return new List<TestimonialViewModel>
            {
                new() { Name = "Minh Anh", Rating = 5, Content = "Ly chè ba màu ở đây rất ngon, phần đường ngọt vừa phải và vị bùi rất đậm chất Sài Gòn.", Avatar = "MA" },
                new() { Name = "Huyền Trân", Rating = 5, Content = "Không gian quán ấm cúng, phục vụ nhanh, đặc biệt thích chè sầu riêng và chè khúc bạch.", Avatar = "HT" },
                new() { Name = "Đức Nam", Rating = 4, Content = "Đặt món dễ dàng, giá hợp lý và hương vị rất ổn. Tôi sẽ quay lại với bạn bè.", Avatar = "DN" }
            };
        }
    }
}
