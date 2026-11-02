using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaigonChe.Web.Data;
using SaigonChe.Web.Models;
using SaigonChe.Web.ViewModels;
using System.Diagnostics;

namespace SaigonChe.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var products = await LoadMenuAsync();
            var categories = await _context.Categories.AsNoTracking()
                .OrderBy(category => category.Name)
                .Select(category => new MenuCategoryFilter(category.Id, category.Name))
                .ToListAsync();
            var model = new LandingPageViewModel
            {
                Products = products,
                BestSellers = products.Where(p => p.IsFeatured).Take(4).ToList(),
                Categories = categories,
                Testimonials = new List<TestimonialViewModel>
                {
                    new() { Name = "Minh Anh", Rating = 5, Content = "Ly chè ba màu ở đây rất ngon, phần đường ngọt vừa phải và vị bùi rất đậm chất Sài Gòn.", Avatar = "MA" },
                    new() { Name = "Huyền Trân", Rating = 5, Content = "Không gian quán ấm cúng, phục vụ nhanh, đặc biệt thích chè sầu riêng và chè khúc bạch.", Avatar = "HT" },
                    new() { Name = "Đức Nam", Rating = 4, Content = "Đặt món dễ dàng, giá hợp lý và hương vị rất ổn. Tôi sẽ quay lại với bạn bè.", Avatar = "DN" }
                }
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
                    CategoryName = product.Category?.Name ?? "Chưa phân loại",
                    ImageUrl = product.ImageUrl
                }).ToList();
            }

            return new List<ProductViewModel>();
        }
    }
}
