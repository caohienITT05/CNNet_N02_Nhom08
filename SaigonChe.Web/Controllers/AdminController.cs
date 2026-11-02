using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
        private const int AdminProductPageSize = 10;
        private const long MaxProductImageSize = 2 * 1024 * 1024;
        private const string SearchCollation = "Latin1_General_100_CI_AI";

        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly OrderCompletionService _orderCompletion;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            ApplicationDbContext context,
            IWebHostEnvironment environment,
            OrderCompletionService orderCompletion,
            UserManager<ApplicationUser> userManager,
            ILogger<AdminController> logger)
        {
            _context = context;
            _environment = environment;
            _orderCompletion = orderCompletion;
            _userManager = userManager;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Dashboard(DateOnly? fromDate, DateOnly? toDate)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var startDate = fromDate ?? new DateOnly(today.Year, today.Month, 1);
            var endDate = toDate ?? today;
            var model = new AdminDashboardViewModel
            {
                FromDate = startDate,
                ToDate = endDate
            };

            model.NewReviewCount = await _context.Reviews.CountAsync(review =>
                review.CreatedAt >= DateTime.UtcNow.AddDays(-7));
            var adminUserIds = (await _userManager.GetUsersInRoleAsync("Admin"))
                .Select(user => user.Id)
                .ToList();
            var staffUserIds = (await _userManager.GetUsersInRoleAsync("Staff"))
                .Select(user => user.Id)
                .ToList();
            var excludedUserIds = adminUserIds.Concat(staffUserIds).Distinct().ToList();
            model.CustomerCount = await _userManager.Users
                .CountAsync(user => !excludedUserIds.Contains(user.Id));

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (startDate > endDate)
            {
                ModelState.AddModelError(string.Empty, "Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.");
                return View(model);
            }

            if (endDate > today)
            {
                ModelState.AddModelError(string.Empty, "Ngày kết thúc không được ở tương lai.");
                return View(model);
            }

            if (endDate.DayNumber - startDate.DayNumber >= 366)
            {
                ModelState.AddModelError(string.Empty, "Khoảng thời gian báo cáo không được vượt quá 366 ngày.");
                return View(model);
            }

            var startUtc = DateTime.SpecifyKind(startDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local)
                .ToUniversalTime();
            var endUtc = DateTime.SpecifyKind(endDate.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Local)
                .ToUniversalTime();
            var ordersInPeriod = _context.Orders
                .Where(order => order.CreatedAt >= startUtc && order.CreatedAt < endUtc);
            var completedOrders = ordersInPeriod.Where(order => order.Status == OrderStatus.Completed);

            model.Revenue = await completedOrders.SumAsync(order => (decimal?)order.Total) ?? 0m;
            model.CompletedOrderCount = await completedOrders.CountAsync();
            model.AverageOrderValue = await completedOrders.AverageAsync(order => (decimal?)order.Total) ?? 0m;
            model.ProcessingOrderCount = await _context.Orders.CountAsync(order =>
                order.Status == OrderStatus.Pending
                || order.Status == OrderStatus.Confirmed
                || order.Status == OrderStatus.Preparing);

            var completedOrderTotals = await completedOrders
                .Select(order => new { order.CreatedAt, order.Total })
                .ToListAsync();
            var revenueByDate = completedOrderTotals
                .GroupBy(order => DateOnly.FromDateTime(
                    DateTime.SpecifyKind(order.CreatedAt, DateTimeKind.Utc).ToLocalTime()))
                .ToDictionary(group => group.Key, group => group.Sum(order => order.Total));

            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                model.DailyRevenue.Add(new DailyRevenueViewModel
                {
                    Date = date,
                    Revenue = revenueByDate.GetValueOrDefault(date)
                });
            }

            model.PopularProducts = await _context.OrderItems
                .Where(item => item.Order!.Status == OrderStatus.Completed
                    && item.Order.CreatedAt >= startUtc
                    && item.Order.CreatedAt < endUtc)
                .GroupBy(item => item.ProductName)
                .Select(group => new PopularProductViewModel
                {
                    ProductName = group.Key,
                    Quantity = group.Sum(item => item.Quantity),
                    Revenue = group.Sum(item => item.Price * item.Quantity)
                })
                .OrderByDescending(product => product.Quantity)
                .ThenBy(product => product.ProductName)
                .Take(5)
                .ToListAsync();

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Products(
            string? search,
            int? categoryId,
            bool? isAvailable,
            int page = 1)
        {
            search = search?.Trim();
            var products = _context.Products.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                products = products.Where(product =>
                    EF.Functions.Collate(product.Name, SearchCollation).Contains(search));
            }

            if (categoryId.HasValue)
            {
                products = products.Where(product => product.CategoryId == categoryId.Value);
            }

            if (isAvailable.HasValue)
            {
                products = products.Where(product => product.IsAvailable == isAvailable.Value);
            }

            var model = new AdminProductListViewModel
            {
                Search = search,
                CategoryId = categoryId,
                IsAvailable = isAvailable,
                TotalProducts = await products.CountAsync(),
                Categories = await _context.Categories
                    .AsNoTracking()
                    .OrderBy(category => category.Name)
                    .Select(category => new CategoryOptionViewModel
                    {
                        Id = category.Id,
                        Name = category.Name
                    })
                    .ToListAsync()
            };
            model.TotalPages = Math.Max(1, (int)Math.Ceiling(model.TotalProducts / (double)AdminProductPageSize));
            model.Page = Math.Clamp(page, 1, model.TotalPages);
            model.Products = await products
                .OrderBy(product => product.Name)
                .Skip((model.Page - 1) * AdminProductPageSize)
                .Take(AdminProductPageSize)
                .Select(product => new AdminProductListItemViewModel
                {
                    Id = product.Id,
                    Name = product.Name,
                    CategoryName = product.Category!.Name,
                    CategoryId = product.CategoryId,
                    Price = product.Price,
                    IsAvailable = product.IsAvailable,
                    IsFeatured = product.IsFeatured,
                    ImageUrl = product.ImageUrl
                })
                .ToListAsync();

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Categories()
        {
            var categories = await _context.Categories
                .AsNoTracking()
                .Include(category => category.Products)
                .OrderBy(category => category.Name)
                .ToListAsync();

            return View(categories);
        }

        [HttpGet]
        public async Task<IActionResult> CategoryDetails(int id)
        {
            var category = await _context.Categories
                .AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => new CategoryDetailsViewModel
                {
                    Id = item.Id,
                    Name = item.Name,
                    Description = item.Description,
                    Products = item.Products
                        .OrderBy(product => product.Name)
                        .Select(product => new AdminProductListItemViewModel
                        {
                            Id = product.Id,
                            Name = product.Name,
                            CategoryName = item.Name,
                            CategoryId = product.CategoryId,
                            Price = product.Price,
                            IsAvailable = product.IsAvailable,
                            IsFeatured = product.IsFeatured,
                            ImageUrl = product.ImageUrl
                        })
                        .ToList()
                })
                .SingleOrDefaultAsync();
            return category == null ? NotFound() : View(category);
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
                && await _context.Categories.AnyAsync(category =>
                    EF.Functions.Collate(category.Name, SearchCollation) == model.Name.Trim()))
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
                    category.Id != model.Id
                    && EF.Functions.Collate(category.Name, SearchCollation) == model.Name.Trim()))
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
            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();

            if (await _context.Products.AnyAsync(product => product.CategoryId == id))
            {
                TempData["AdminError"] =
                    "Không thể xóa danh mục đang có món chè. Hãy chuyển món sang danh mục khác trước.";
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
                .Include(order => order.AssignedStaff)
                .Include(order => order.StatusHistory)
                    .ThenInclude(history => history.Actor)
                .Include(order => order.StaffStatusHistory)
                .OrderByDescending(order => order.CreatedAt)
                .ToListAsync();

            return View(orders);
        }

        [HttpGet]
        public async Task<IActionResult> Staff()
        {
            return View(await LoadStaffAccountsAsync(new StaffAccountsViewModel()));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStaff(StaffAccountsViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadStaffAccountsAsync(model);
                return View(nameof(Staff), model);
            }

            var email = model.Email.Trim();
            if (await _userManager.FindByEmailAsync(email) != null)
            {
                ModelState.AddModelError(nameof(model.Email), "Email này đã được sử dụng.");
                await LoadStaffAccountsAsync(model);
                return View(nameof(Staff), model);
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };
            var createResult = await _userManager.CreateAsync(user, model.Password);
            if (!createResult.Succeeded)
            {
                AddIdentityErrors(createResult);
                await LoadStaffAccountsAsync(model);
                return View(nameof(Staff), model);
            }

            var roleResult = await _userManager.AddToRoleAsync(user, "Staff");
            if (!roleResult.Succeeded)
            {
                var deleteResult = await _userManager.DeleteAsync(user);
                if (!deleteResult.Succeeded)
                {
                    var errors = string.Join("; ", deleteResult.Errors.Select(error => error.Description));
                    throw new InvalidOperationException($"Could not clean up an unassigned staff account: {errors}");
                }

                AddIdentityErrors(roleResult);
                await LoadStaffAccountsAsync(model);
                return View(nameof(Staff), model);
            }

            TempData["AdminSuccess"] = $"Đã tạo tài khoản nhân viên {email}.";
            return RedirectToAction(nameof(Staff));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveStaff(string userId)
        {
            var staff = await _userManager.FindByIdAsync(userId);
            if (staff == null || !await _userManager.IsInRoleAsync(staff, "Staff"))
            {
                return NotFound();
            }

            var hasActiveOrders = await _context.Orders.AnyAsync(order =>
                order.AssignedStaffId == userId
                && order.Status != OrderStatus.Completed
                && order.Status != OrderStatus.Cancelled);
            if (hasActiveOrders)
            {
                TempData["AdminError"] = "Nhân viên còn đơn đang xử lý; hãy hoàn tất hoặc hủy đơn trước khi gỡ quyền.";
                return RedirectToAction(nameof(Staff));
            }

            var result = await _userManager.RemoveFromRoleAsync(staff, "Staff");
            if (!result.Succeeded)
            {
                TempData["AdminError"] = string.Join(" ", result.Errors.Select(error => error.Description));
                return RedirectToAction(nameof(Staff));
            }

            var stampResult = await _userManager.UpdateSecurityStampAsync(staff);
            if (!stampResult.Succeeded)
            {
                TempData["AdminError"] = "Đã gỡ quyền nhân viên nhưng không thể thu hồi phiên đăng nhập ngay lập tức.";
                return RedirectToAction(nameof(Staff));
            }

            TempData["AdminSuccess"] = $"Đã gỡ quyền nhân viên của {staff.Email}.";
            return RedirectToAction(nameof(Staff));
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

            var order = await _context.Orders.Include(item => item.Items)
                .FirstOrDefaultAsync(item => item.Id == model.OrderId);
            if (order == null) return NotFound();
            if (order.Type != OrderType.Online)
            {
                TempData["AdminError"] = "Cập nhật đơn POS hoặc đặt tiệc tại màn hình nghiệp vụ tương ứng.";
                return RedirectToAction(nameof(Orders));
            }

            if (!IsValidOrderTransition(order.Status, model.Status))
            {
                TempData["AdminError"] = "Luồng trạng thái đơn hàng không hợp lệ hoặc đơn đã kết thúc.";
                return RedirectToAction(nameof(Orders));
            }
            if (model.Status == OrderStatus.Cancelled && string.IsNullOrWhiteSpace(model.Note))
            {
                TempData["AdminError"] = "Vui lòng nhập lý do hủy đơn.";
                return RedirectToAction(nameof(Orders));
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
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
                _context.OrderStatusHistories.Add(new StaffOrderStatusHistory
                {
                    OrderId = order.Id,
                    PreviousStatus = previous,
                    Status = order.Status,
                    ChangedByUserId = actorId,
                    ChangedBy = User.Identity?.Name,
                    ChangedAt = DateTime.UtcNow,
                    Note = model.Note?.Trim()
                });
            }

            try
            {
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (DbUpdateConcurrencyException exception)
            {
                await transaction.RollbackAsync();
                _logger.LogInformation(exception, "Concurrent admin status update for order {OrderId}.", order.Id);
                TempData["AdminError"] = $"Đơn #{order.Id} vừa được cập nhật ở nơi khác. Hãy kiểm tra trạng thái mới.";
                return RedirectToAction(nameof(Orders));
            }

            TempData["AdminSuccess"] = $"Đã cập nhật trạng thái đơn #{order.Id}.";
            return RedirectToAction(nameof(Orders));
        }

        private static bool IsValidOrderTransition(OrderStatus current, OrderStatus next) => current switch
        {
            OrderStatus.Pending => next is OrderStatus.Confirmed or OrderStatus.Cancelled,
            OrderStatus.Confirmed => next is OrderStatus.Preparing or OrderStatus.Cancelled,
            OrderStatus.Preparing => next is OrderStatus.Delivering or OrderStatus.Shipping or OrderStatus.Completed or OrderStatus.Cancelled,
            OrderStatus.Delivering or OrderStatus.Shipping => next is OrderStatus.Completed or OrderStatus.Cancelled,
            _ => false
        };

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
            if (!await _context.Categories.AnyAsync(category => category.Id == model.CategoryId))
            {
                ModelState.AddModelError(nameof(model.CategoryId), "Danh mục đã chọn không tồn tại.");
            }

            if (!ModelState.IsValid)
            {
                await LoadCategoriesAsync(model.CategoryId);
                return View(model);
            }

            var product = new Product
            {
                Name = model.Name.Trim(),
                Description = model.Description.Trim(),
                Price = model.Price,
                IsAvailable = model.IsAvailable,
                IsFeatured = model.IsFeatured,
                CategoryId = model.CategoryId
            };

            if (model.ImageFile is { Length: > 0 })
            {
                var validationError = await ValidateProductImageAsync(model.ImageFile);
                if (validationError != null)
                {
                    ModelState.AddModelError(nameof(model.ImageFile), validationError);
                    await LoadCategoriesAsync(model.CategoryId);
                    return View(model);
                }

                var savedImage = await SaveProductImageAsync(model.ImageFile);
                product.ImageUrl = savedImage;
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
            if (!await _context.Categories.AnyAsync(category => category.Id == model.CategoryId))
            {
                ModelState.AddModelError(nameof(model.CategoryId), "Danh mục đã chọn không tồn tại.");
            }

            if (!ModelState.IsValid)
            {
                await LoadCategoriesAsync(model.CategoryId);
                return View(model);
            }

            var product = await _context.Products.FindAsync(model.Id);
            if (product == null) return NotFound();

            var oldImageUrl = product.ImageUrl;
            string? newImageUrl = null;
            if (model.ImageFile is { Length: > 0 })
            {
                var validationError = await ValidateProductImageAsync(model.ImageFile);
                if (validationError != null)
                {
                    ModelState.AddModelError(nameof(model.ImageFile), validationError);
                    await LoadCategoriesAsync(model.CategoryId);
                    return View(model);
                }

                var savedImage = await SaveProductImageAsync(model.ImageFile);
                newImageUrl = savedImage;
            }

            product.Name = model.Name.Trim();
            product.Description = model.Description.Trim();
            product.Price = model.Price;
            product.IsAvailable = model.IsAvailable;
            product.IsFeatured = model.IsFeatured;
            product.CategoryId = model.CategoryId;
            product.ImageUrl = newImageUrl ?? oldImageUrl;

            await _context.SaveChangesAsync();
            var oldImagePath = GetUploadedImagePath(oldImageUrl);
            if (newImageUrl != null
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
        public async Task<IActionResult> ToggleProductAvailability(
            int id,
            string? search,
            int? categoryId,
            bool? isAvailable,
            int page = 1)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            product.IsAvailable = !product.IsAvailable;
            await _context.SaveChangesAsync();
            TempData["AdminSuccess"] = product.IsAvailable
                ? $"Đã mở bán món {product.Name}."
                : $"Đã chuyển món {product.Name} sang hết hàng.";
            return RedirectToAction(nameof(Products), new { search, categoryId, isAvailable, page });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProduct(
            int id,
            string? search,
            int? categoryId,
            bool? isAvailable,
            int page = 1)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            if (await _context.OrderItems.AnyAsync(item => item.ProductId == id))
            {
                product.IsAvailable = false;
                await _context.SaveChangesAsync();
                TempData["AdminError"] =
                    "Không thể xóa món vì món đã xuất hiện trong đơn hàng. Món đã được chuyển sang Hết hàng để giữ lịch sử.";
                return RedirectToAction(nameof(Products), new { search, categoryId, isAvailable, page });
            }

            var imagePath = GetUploadedImagePath(product.ImageUrl);
            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            if (imagePath != null && System.IO.File.Exists(imagePath))
            {
                System.IO.File.Delete(imagePath);
            }

            TempData["AdminSuccess"] = "Xóa món thành công.";
            return RedirectToAction(nameof(Products), new { search, categoryId, isAvailable, page });
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

        private async Task<string?> ValidateProductImageAsync(IFormFile imageFile)
        {
            if (imageFile.Length <= 0)
            {
                return "Tệp ảnh không hợp lệ.";
            }

            if (imageFile.Length > MaxProductImageSize)
            {
                return "Dung lượng ảnh tối đa là 2MB.";
            }

            var extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
            var expectedContentType = extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => null
            };
            if (expectedContentType == null)
            {
                return "Chỉ chấp nhận ảnh JPG, JPEG, PNG hoặc WEBP.";
            }

            if (!string.Equals(imageFile.ContentType, expectedContentType, StringComparison.OrdinalIgnoreCase))
            {
                return "Loại nội dung của tệp ảnh không khớp với phần mở rộng.";
            }

            var header = new byte[12];
            await using var stream = imageFile.OpenReadStream();
            var bytesRead = 0;
            while (bytesRead < header.Length)
            {
                var read = await stream.ReadAsync(header.AsMemory(bytesRead));
                if (read == 0)
                {
                    break;
                }

                bytesRead += read;
            }

            if (!HasValidImageSignature(extension, header, bytesRead))
            {
                return "Nội dung tệp không phải ảnh hợp lệ.";
            }

            return null;
        }

        private async Task<string> SaveProductImageAsync(IFormFile imageFile)
        {
            var extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
            var fileName = $"{Guid.NewGuid():N}{extension}";
            var uploadDirectory = Path.Combine(_environment.WebRootPath, "uploads", "products");
            Directory.CreateDirectory(uploadDirectory);
            var filePath = Path.Combine(uploadDirectory, fileName);

            await using var stream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await imageFile.CopyToAsync(stream);
            return $"/uploads/products/{fileName}";
        }

        private static bool HasValidImageSignature(string extension, byte[] header, int bytesRead)
        {
            return extension switch
            {
                ".jpg" or ".jpeg" => bytesRead >= 3
                    && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
                ".png" => bytesRead >= 8
                    && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47
                    && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A,
                ".webp" => bytesRead >= 12
                    && header[0] == (byte)'R' && header[1] == (byte)'I'
                    && header[2] == (byte)'F' && header[3] == (byte)'F'
                    && header[8] == (byte)'W' && header[9] == (byte)'E'
                    && header[10] == (byte)'B' && header[11] == (byte)'P',
                _ => false
            };
        }

        private async Task LoadCategoriesAsync(int selectedId = 0)
        {
            var categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();
            ViewBag.Categories = new SelectList(categories, "Id", "Name", selectedId);
        }

        private async Task<StaffAccountsViewModel> LoadStaffAccountsAsync(StaffAccountsViewModel model)
        {
            var staffUsers = await _userManager.GetUsersInRoleAsync("Staff");
            var staffIds = staffUsers.Select(user => user.Id).ToList();
            var activeStaffIds = await _context.Orders
                .Where(order => order.AssignedStaffId != null
                    && staffIds.Contains(order.AssignedStaffId)
                    && order.Status != OrderStatus.Completed
                    && order.Status != OrderStatus.Cancelled)
                .Select(order => order.AssignedStaffId!)
                .Distinct()
                .ToListAsync();

            model.Staff = staffUsers
                .OrderBy(user => user.Email)
                .Select(user => new StaffAccountItemViewModel
                {
                    Id = user.Id,
                    Email = user.Email ?? string.Empty,
                    HasActiveOrders = activeStaffIds.Contains(user.Id)
                })
                .ToList();
            return model;
        }

        private void AddIdentityErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }
    }
}
