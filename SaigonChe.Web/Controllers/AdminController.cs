using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Security.Claims;
using SaigonChe.Web.Data;
using SaigonChe.Web.Models;
using SaigonChe.Web.Services;
using SaigonChe.Web.ViewModels;

namespace SaigonChe.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ProductImageService _images;
        private readonly ILogger<AdminController> _logger;
        private readonly OrderCompletionService _orderCompletion;

        public AdminController(ApplicationDbContext context, ProductImageService images, ILogger<AdminController> logger, OrderCompletionService orderCompletion)
        {
            _context = context;
            _images = images;
            _logger = logger;
            _orderCompletion = orderCompletion;
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
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
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
                Description = model.Description?.Trim() ?? string.Empty
            });
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
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
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
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
            category.Description = model.Description?.Trim() ?? string.Empty;
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
            TempData["AdminSuccess"] = "Cập nhật danh mục thành công.";
            return RedirectToAction(nameof(Categories));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
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

            await transaction.CommitAsync();
            TempData["AdminSuccess"] = "Xóa danh mục thành công.";
            return RedirectToAction(nameof(Categories));
        }

        [HttpGet]
        public async Task<IActionResult> Orders()
        {
            var orders = await _context.Orders
                .Include(order => order.Items)
                .Where(order => !(order.Type == OrderType.Pos
                    && !order.IsPaid
                    && order.Status == OrderStatus.Pending
                    && !order.Items.Any()))
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

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var order = await _context.Orders.Include(item => item.Items)
                .FirstOrDefaultAsync(item => item.Id == model.OrderId);
            if (order == null) return NotFound();
            if (!OrderWorkflowRules.Matches(model.RowVersion, order.RowVersion))
            {
                TempData["AdminError"] = "Đơn vừa được cập nhật. Hãy tải lại trước khi tiếp tục.";
                return RedirectToAction(nameof(Orders));
            }
            if (order.Type != OrderType.Online)
            {
                TempData["AdminError"] = "Cập nhật đơn POS hoặc đặt tiệc tại màn hình nghiệp vụ tương ứng.";
                return RedirectToAction(nameof(Orders));
            }

            if (!OrderWorkflowRules.IsAllowed(order.Status, model.Status))
            {
                TempData["AdminError"] = "Luồng trạng thái đơn hàng không hợp lệ hoặc đơn đã kết thúc.";
                return RedirectToAction(nameof(Orders));
            }
            if (model.Status == OrderStatus.Cancelled && string.IsNullOrWhiteSpace(model.Note))
            {
                TempData["AdminError"] = "Vui lòng nhập lý do hủy đơn.";
                return RedirectToAction(nameof(Orders));
            }

            var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (model.Status == OrderStatus.Completed)
            {
                if (order.PaymentMethod != PaymentMethod.CashOnDelivery)
                {
                    TempData["AdminError"] = "Chỉ có thể hoàn tất đơn COD sau khi xác nhận tiền mặt đã thu.";
                    return RedirectToAction(nameof(Orders));
                }

                try
                {
                    await _orderCompletion.CompleteAsync(order, PaymentMethod.Cash, actorId, "Thu tiền COD và hoàn tất đơn.");
                }
                catch (OrderCompletionException exception)
                {
                    await transaction.RollbackAsync();
                    TempData["AdminError"] = exception.Message;
                    return RedirectToAction(nameof(Orders));
                }
            }
            else
            {
                var previous = order.Status;
                order.Status = model.Status;
                order.AssignedStaffId ??= actorId;
                _context.OrderStatusHistory.Add(new OrderStatusHistory
                {
                    OrderId = order.Id,
                    PreviousStatus = previous,
                    Status = order.Status,
                    ActorId = actorId,
                    Note = model.Note?.Trim(),
                    CreatedAt = DateTime.UtcNow
                });
            }

            try
            {
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                TempData["AdminError"] = "Đơn vừa được cập nhật bởi nhân viên khác. Hãy tải lại trước khi tiếp tục.";
                return RedirectToAction(nameof(Orders));
            }

            TempData["AdminSuccess"] = $"Đã cập nhật trạng thái đơn #{order.Id}.";
            return RedirectToAction(nameof(Orders));
        }



        [HttpGet]
        public async Task<IActionResult> CreateProduct()
        {
            await LoadCategoriesAsync();
            return View(new ProductFormViewModel());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public Task<IActionResult> CreateProduct(ProductFormViewModel model) => SaveProductAsync(model, true);

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

        [HttpPost, ValidateAntiForgeryToken]
        public Task<IActionResult> EditProduct(ProductFormViewModel model) => SaveProductAsync(model, false);

        private async Task<IActionResult> SaveProductAsync(ProductFormViewModel model, bool create)
        {
            var view = create ? "CreateProduct" : "EditProduct";
            var product = create ? new Product() : await _context.Products.FindAsync(model.Id);
            if (product is null) return NotFound();
            model.ImageUrl = product.ImageUrl;
            if (model.CategoryId > 0 && !await _context.Categories.AnyAsync(item => item.Id == model.CategoryId))
                ModelState.AddModelError(nameof(model.CategoryId), "Danh mục không tồn tại. Vui lòng chọn lại.");
            if (!ModelState.IsValid)
            {
                await LoadCategoriesAsync(model.CategoryId);
                return View(view, model);
            }

            string? newImage = null;
            var oldImage = product.ImageUrl;
            var committed = false;
            try
            {
                if (model.ImageFile is { Length: > 0 })
                    newImage = await _images.SaveAsync(model.ImageFile, HttpContext.RequestAborted);
                product.Name = model.Name.Trim();
                product.Description = model.Description.Trim();
                product.Price = model.Price;
                product.CategoryId = model.CategoryId;
                product.IsAvailable = model.IsAvailable;
                product.IsFeatured = model.IsFeatured;
                product.ImageUrl = newImage ?? oldImage;
                if (create) _context.Products.Add(product);
                await _context.SaveChangesAsync();
                committed = true;
            }
            catch (ProductImageException exception) { ModelState.AddModelError(nameof(model.ImageFile), exception.Message); }
            catch (Exception exception) when (exception is DbUpdateException or IOException or UnauthorizedAccessException
                || DatabaseErrors.FindSqlException(exception) is not null)
            {
                _logger.LogWarning("Could not save a product ({ErrorType}).", exception.GetType().Name);
                ModelState.AddModelError(string.Empty, "Chưa thể lưu món. Hãy kiểm tra dữ liệu và thử lại.");
            }
            finally
            {
                if (!committed) _images.Delete(newImage);
            }
            if (!ModelState.IsValid)
            {
                await LoadCategoriesAsync(model.CategoryId);
                return View(view, model);
            }
            if (newImage is not null) _images.Delete(oldImage);
            TempData["AdminSuccess"] = create ? "Thêm món thành công." : "Cập nhật món thành công.";
            return RedirectToAction(nameof(Products));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var product = await _context.Products
                .Include(item => item.OrderItems)
                .Include(item => item.InventoryMovements)
                .FirstOrDefaultAsync(item => item.Id == id);
            if (product == null) return NotFound();

            if (product.OrderItems.Count > 0 || product.InventoryMovements.Count > 0)
            {
                product.IsAvailable = false;
                await _context.SaveChangesAsync();
                TempData["AdminSuccess"] = "Món đã được ẩn khỏi thực đơn để giữ nguyên lịch sử đơn hàng.";
                return RedirectToAction(nameof(Products));
            }

            var imageUrl = product.ImageUrl;
            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            _images.Delete(imageUrl);

            TempData["AdminSuccess"] = "Xóa món thành công.";
            return RedirectToAction(nameof(Products));
        }

        private async Task LoadCategoriesAsync(int selectedId = 0)
        {
            var categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();
            ViewBag.Categories = new SelectList(categories, "Id", "Name", selectedId);
        }
    }
}
