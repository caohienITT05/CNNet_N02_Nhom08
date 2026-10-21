using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SaigonChe.Web.Data;
using SaigonChe.Web.Models;
using SaigonChe.Web.ViewModels;

namespace SaigonChe.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public AdminController(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        [HttpGet]
        public async Task<IActionResult> Products()
        {
            var products = await _context.Products
                .Include(p => p.Category)
                .OrderBy(p => p.Name)
                .ToListAsync();

            return View(products);
        }

        [HttpGet]
        public async Task<IActionResult> Categories()
        {
            var categories = await _context.Categories
                .Include(category => category.Products)
                .OrderBy(category => category.Name)
                .ToListAsync();

            return View(categories);
        }

        [HttpGet]
        public IActionResult CreateCategory()
        {
            return View(new CategoryFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCategory(CategoryFormViewModel model)
        {
            if (!string.IsNullOrWhiteSpace(model.Name)
                && await _context.Categories.AnyAsync(category => category.Name == model.Name.Trim()))
            {
                ModelState.AddModelError(nameof(model.Name), "Tên danh mục đã tồn tại.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            _context.Categories.Add(new Category
            {
                Name = model.Name.Trim(),
                Description = model.Description.Trim()
            });
            await _context.SaveChangesAsync();

            TempData["AdminSuccess"] = "Thêm danh mục thành công.";
            return RedirectToAction(nameof(Categories));
        }

        [HttpGet]
        public async Task<IActionResult> EditCategory(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();

            return View(new CategoryFormViewModel
            {
                Id = category.Id,
                Name = category.Name,
                Description = category.Description
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCategory(CategoryFormViewModel model)
        {
            if (!string.IsNullOrWhiteSpace(model.Name)
                && await _context.Categories.AnyAsync(category =>
                    category.Id != model.Id && category.Name == model.Name.Trim()))
            {
                ModelState.AddModelError(nameof(model.Name), "Tên danh mục đã tồn tại.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var category = await _context.Categories.FindAsync(model.Id);
            if (category == null) return NotFound();

            category.Name = model.Name.Trim();
            category.Description = model.Description.Trim();
            await _context.SaveChangesAsync();

            TempData["AdminSuccess"] = "Cập nhật danh mục thành công.";
            return RedirectToAction(nameof(Categories));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var category = await _context.Categories
                .Include(item => item.Products)
                .FirstOrDefaultAsync(item => item.Id == id);
            if (category == null) return NotFound();

            if (category.Products.Count > 0)
            {
                TempData["AdminError"] = "Không thể xóa danh mục đang có sản phẩm. Hãy chuyển hoặc xóa các sản phẩm trước.";
                return RedirectToAction(nameof(Categories));
            }

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();

            TempData["AdminSuccess"] = "Xóa danh mục thành công.";
            return RedirectToAction(nameof(Categories));
        }

        [HttpGet]
        public async Task<IActionResult> Orders()
        {
            var orders = await _context.Orders
                .Include(order => order.Items)
                .OrderByDescending(order => order.CreatedAt)
                .ToListAsync();

            return View(orders);
        }

        [HttpGet]
        public async Task<IActionResult> ContactMessages()
        {
            var messages = await _context.ContactMessages
                .OrderBy(message => message.IsRead)
                .ThenByDescending(message => message.CreatedAt)
                .ToListAsync();

            return View(messages);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkContactMessageRead(int id)
        {
            var message = await _context.ContactMessages.FindAsync(id);
            if (message == null) return NotFound();

            message.IsRead = true;
            await _context.SaveChangesAsync();

            TempData["AdminSuccess"] = "Đã đánh dấu liên hệ là đã xem.";
            return RedirectToAction(nameof(ContactMessages));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteContactMessage(int id)
        {
            var message = await _context.ContactMessages.FindAsync(id);
            if (message == null) return NotFound();

            _context.ContactMessages.Remove(message);
            await _context.SaveChangesAsync();

            TempData["AdminSuccess"] = "Đã xóa liên hệ.";
            return RedirectToAction(nameof(ContactMessages));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateOrderStatus(UpdateOrderStatusViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["AdminError"] = "Trạng thái đơn hàng không hợp lệ.";
                return RedirectToAction(nameof(Orders));
            }

            var order = await _context.Orders.FindAsync(model.OrderId);
            if (order == null) return NotFound();

            order.Status = model.Status;
            await _context.SaveChangesAsync();

            TempData["AdminSuccess"] = $"Đã cập nhật trạng thái đơn #{order.Id}.";
            return RedirectToAction(nameof(Orders));
        }

        [HttpGet]
        public async Task<IActionResult> CreateProduct()
        {
            await LoadCategoriesAsync();
            return View(new ProductFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateProduct(ProductFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadCategoriesAsync(model.CategoryId);
                return View(model);
            }

            var product = new Product
            {
                Name = model.Name,
                Description = model.Description,
                Price = model.Price,
                IsAvailable = model.IsAvailable,
                IsFeatured = model.IsFeatured,
                CategoryId = model.CategoryId,
            };

            if (model.ImageFile is { Length: > 0 })
            {
                var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                var ext = Path.GetExtension(model.ImageFile.FileName).ToLowerInvariant();

                if (!allowed.Contains(ext))
                {
                    ModelState.AddModelError(nameof(model.ImageFile), "Chỉ chấp nhận JPG, JPEG, PNG, WEBP.");
                    await LoadCategoriesAsync(model.CategoryId);
                    return View(model);
                }
                if (model.ImageFile.Length > 5 * 1024 * 1024)
                {
                    ModelState.AddModelError(nameof(model.ImageFile), "Dung lượng ảnh tối đa là 5MB.");
                    await LoadCategoriesAsync(model.CategoryId);
                    return View(model);
                }

                var uploadDir = Path.Combine(_environment.WebRootPath, "uploads", "products");
                Directory.CreateDirectory(uploadDir);
                var fileName = $"{Guid.NewGuid():N}{ext}";
                var filePath = Path.Combine(uploadDir, fileName);

                await using var stream = new FileStream(filePath, FileMode.Create);
                await model.ImageFile.CopyToAsync(stream);

                product.ImageUrl = $"/uploads/products/{fileName}";
            }

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            TempData["AdminSuccess"] = "Thêm món thành công.";
            return RedirectToAction(nameof(Products));
        }

        [HttpGet]
        public async Task<IActionResult> EditProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            await LoadCategoriesAsync(product.CategoryId);

            var model = new ProductFormViewModel
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                IsAvailable = product.IsAvailable,
                IsFeatured = product.IsFeatured,
                CategoryId = product.CategoryId,
                ImageUrl = product.ImageUrl
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProduct(ProductFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadCategoriesAsync(model.CategoryId);
                return View(model);
            }

            var product = await _context.Products.FindAsync(model.Id);
            if (product == null) return NotFound();

            var oldImageUrl = product.ImageUrl;
            product.Name = model.Name;
            product.Description = model.Description;
            product.Price = model.Price;
            product.IsAvailable = model.IsAvailable;
            product.IsFeatured = model.IsFeatured;
            product.CategoryId = model.CategoryId;

            if (model.ImageFile is { Length: > 0 })
            {
                var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                var ext = Path.GetExtension(model.ImageFile.FileName).ToLowerInvariant();

                if (!allowed.Contains(ext))
                {
                    ModelState.AddModelError(nameof(model.ImageFile), "Chỉ chấp nhận JPG, JPEG, PNG, WEBP.");
                    await LoadCategoriesAsync(model.CategoryId);
                    return View(model);
                }
                if (model.ImageFile.Length > 5 * 1024 * 1024)
                {
                    ModelState.AddModelError(nameof(model.ImageFile), "Dung lượng ảnh tối đa là 5MB.");
                    await LoadCategoriesAsync(model.CategoryId);
                    return View(model);
                }

                var uploadDir = Path.Combine(_environment.WebRootPath, "uploads", "products");
                Directory.CreateDirectory(uploadDir);
                var fileName = $"{Guid.NewGuid():N}{ext}";
                var filePath = Path.Combine(uploadDir, fileName);

                await using var stream = new FileStream(filePath, FileMode.Create);
                await model.ImageFile.CopyToAsync(stream);

                product.ImageUrl = $"/uploads/products/{fileName}";

            }
            else
            {
                product.ImageUrl = oldImageUrl;
            }

            await _context.SaveChangesAsync();
            var oldImagePath = GetUploadedImagePath(oldImageUrl);
            if (model.ImageFile is { Length: > 0 }
                && oldImagePath != null
                && System.IO.File.Exists(oldImagePath))
            {
                System.IO.File.Delete(oldImagePath);
            }
            TempData["AdminSuccess"] = "Cập nhật món thành công.";
            return RedirectToAction(nameof(Products));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var product = await _context.Products
                .Include(item => item.OrderItems)
                .FirstOrDefaultAsync(item => item.Id == id);
            if (product == null) return NotFound();

            if (product.OrderItems.Count > 0)
            {
                product.IsAvailable = false;
                await _context.SaveChangesAsync();
                TempData["AdminSuccess"] = "Món đã được ẩn khỏi thực đơn để giữ nguyên lịch sử đơn hàng.";
                return RedirectToAction(nameof(Products));
            }

            var imagePath = GetUploadedImagePath(product.ImageUrl);
            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            if (imagePath != null && System.IO.File.Exists(imagePath))
            {
                System.IO.File.Delete(imagePath);
            }

            TempData["AdminSuccess"] = "Xóa món thành công.";
            return RedirectToAction(nameof(Products));
        }

        private string? GetUploadedImagePath(string? imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl) || !imageUrl.StartsWith("/uploads/products/", StringComparison.Ordinal))
            {
                return null;
            }

            var fileName = Path.GetFileName(imageUrl);
            return Path.Combine(_environment.WebRootPath, "uploads", "products", fileName);
        }

        private async Task LoadCategoriesAsync(int selectedId = 0)
        {
            var categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();
            ViewBag.Categories = new SelectList(categories, "Id", "Name", selectedId);
        }
    }
}
