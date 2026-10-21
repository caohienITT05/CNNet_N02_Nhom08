using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaigonChe.Web.Data;
using SaigonChe.Web.Models;
using SaigonChe.Web.ViewModels;
using System.Text.Json;

namespace SaigonChe.Web.Controllers
{
    public class CartController : Controller
    {
        private const int MaxQuantityPerProduct = 20;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CartController> _logger;

        public CartController(ApplicationDbContext context, ILogger<CartController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var cart = await RefreshCartAsync();
            return View(cart);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToCart(int productId, int quantity = 1)
        {
            if (productId <= 0 || quantity is < 1 or > MaxQuantityPerProduct)
            {
                TempData["CartError"] = $"Số lượng mỗi món phải từ 1 đến {MaxQuantityPerProduct}.";
                return RedirectToAction(nameof(Index));
            }

            var product = await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == productId);
            if (product == null || !product.IsAvailable)
            {
                TempData["CartError"] = "Sản phẩm không tồn tại hoặc đã ngừng bán.";
                return RedirectToAction("Index", "Home");
            }

            var cart = GetCart();
            var item = cart.FirstOrDefault(x => x.ProductId == productId);
            if (item != null && item.Quantity + quantity > MaxQuantityPerProduct)
            {
                TempData["CartError"] = $"Bạn chỉ có thể đặt tối đa {MaxQuantityPerProduct} phần cho mỗi món.";
                return RedirectToAction(nameof(Index));
            }

            if (item == null)
            {
                cart.Add(new CartItemViewModel
                {
                    ProductId = product.Id,
                    Name = product.Name,
                    Price = product.Price,
                    Quantity = quantity,
                    ImageUrl = product.ImageUrl
                });
            }
            else
            {
                item.Quantity += quantity;
            }

            item ??= cart.First(cartItem => cartItem.ProductId == productId);
            item.Name = product.Name;
            item.Price = product.Price;
            item.ImageUrl = product.ImageUrl;
            SaveCart(cart);
            TempData["CartSuccess"] = $"Đã thêm {product.Name} vào giỏ hàng.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateQuantity(int productId, int quantity)
        {
            if (productId <= 0 || quantity is < 1 or > MaxQuantityPerProduct)
            {
                TempData["CartError"] = $"Số lượng mỗi món phải từ 1 đến {MaxQuantityPerProduct}.";
                return RedirectToAction(nameof(Index));
            }

            var cart = GetCart();
            var item = cart.FirstOrDefault(x => x.ProductId == productId);
            if (item == null) return RedirectToAction(nameof(Index));

            var product = await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == productId && item.IsAvailable);
            if (product == null)
            {
                cart.Remove(item);
                SaveCart(cart);
                TempData["CartError"] = $"Món {item.Name} hiện không còn khả dụng và đã được bỏ khỏi giỏ.";
                return RedirectToAction(nameof(Index));
            }

            item.Name = product.Name;
            item.Price = product.Price;
            item.ImageUrl = product.ImageUrl;
            item.Quantity = quantity;

            SaveCart(cart);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RemoveItem(int productId)
        {
            var cart = GetCart();
            var item = cart.FirstOrDefault(x => x.ProductId == productId);
            if (item != null)
            {
                cart.Remove(item);
                SaveCart(cart);
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Checkout()
        {
            var cart = await RefreshCartAsync();
            if (!cart.Any())
            {
                TempData["CartError"] = "Giỏ hàng của bạn đang trống.";
                return RedirectToAction(nameof(Index));
            }

            return View(CreateCheckoutViewModel(cart));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(CheckoutViewModel model)
        {
            var cart = GetCart();
            if (!cart.Any())
            {
                TempData["CartError"] = "Giỏ hàng của bạn đang trống.";
                return RedirectToAction(nameof(Index));
            }

            var products = await _context.Products
                .Where(product => cart.Select(item => item.ProductId).Contains(product.Id))
                .ToDictionaryAsync(product => product.Id);

            var items = new List<OrderItem>();
            foreach (var cartItem in cart)
            {
                if (cartItem.ProductId <= 0 || cartItem.Quantity is < 1 or > MaxQuantityPerProduct)
                {
                    _logger.LogWarning("Invalid cart item found during checkout for product {ProductId}.", cartItem.ProductId);
                    ModelState.AddModelError(string.Empty, "Giỏ hàng có số lượng không hợp lệ. Vui lòng kiểm tra lại.");
                    continue;
                }

                if (!products.TryGetValue(cartItem.ProductId, out var product) || !product.IsAvailable)
                {
                    ModelState.AddModelError(string.Empty, $"Món {cartItem.Name} hiện không còn khả dụng.");
                    continue;
                }

                items.Add(new OrderItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Price = product.Price,
                    Quantity = cartItem.Quantity
                });
            }

            if (!ModelState.IsValid || items.Count != cart.Count)
            {
                model.Items = CreateCheckoutViewModel(cart).Items;
                return View(model);
            }

            var order = new Order
            {
                CustomerName = model.CustomerName.Trim(),
                Phone = model.Phone.Trim(),
                Address = model.Address.Trim(),
                Total = items.Sum(item => item.Price * item.Quantity),
                CreatedAt = DateTime.UtcNow,
                Status = OrderStatus.Pending,
                Items = items
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            HttpContext.Session.Remove("Cart");
            TempData["OrderId"] = order.Id;
            TempData["OrderSuccess"] = "Đặt món thành công. Chúng tôi sẽ liên hệ xác nhận đơn hàng sớm nhất.";
            return RedirectToAction("Index", "Home");
        }

        private List<CartItemViewModel> GetCart()
        {
            var cartJson = HttpContext.Session.GetString("Cart");
            if (string.IsNullOrWhiteSpace(cartJson))
            {
                return new List<CartItemViewModel>();
            }

            try
            {
                var cart = JsonSerializer.Deserialize<List<CartItemViewModel>>(cartJson) ?? new List<CartItemViewModel>();
                if (cart.Any(item => item.ProductId <= 0 || item.Quantity is < 1 or > MaxQuantityPerProduct))
                {
                    _logger.LogWarning("Invalid cart data found in session. Clearing the cart.");
                    HttpContext.Session.Remove("Cart");
                    TempData["CartError"] = "Giỏ hàng không hợp lệ đã được làm mới. Vui lòng chọn món lại.";
                    return new List<CartItemViewModel>();
                }

                return cart;
            }
            catch (JsonException exception)
            {
                _logger.LogWarning(exception, "Could not read cart data from session. Clearing the cart.");
                HttpContext.Session.Remove("Cart");
                TempData["CartError"] = "Không thể đọc giỏ hàng đã lưu. Vui lòng chọn món lại.";
                return new List<CartItemViewModel>();
            }
        }

        private async Task<List<CartItemViewModel>> RefreshCartAsync()
        {
            var cart = GetCart();
            if (cart.Count == 0)
            {
                return cart;
            }

            var productIds = cart.Select(item => item.ProductId).Distinct().ToList();
            var products = await _context.Products
                .AsNoTracking()
                .Where(product => productIds.Contains(product.Id) && product.IsAvailable)
                .ToDictionaryAsync(product => product.Id);

            var unavailableItems = cart.Where(item => !products.ContainsKey(item.ProductId)).ToList();
            if (unavailableItems.Count > 0)
            {
                cart.RemoveAll(item => !products.ContainsKey(item.ProductId));
                TempData["CartError"] = "Một số món không còn bán và đã được bỏ khỏi giỏ hàng.";
            }

            foreach (var item in cart)
            {
                var product = products[item.ProductId];
                item.Name = product.Name;
                item.Price = product.Price;
                item.ImageUrl = product.ImageUrl;
            }

            SaveCart(cart);
            return cart;
        }

        private static CheckoutViewModel CreateCheckoutViewModel(List<CartItemViewModel> cart)
        {
            return new CheckoutViewModel
            {
                Items = cart.Select(item => new CartItemViewModel
                {
                    ProductId = item.ProductId,
                    Name = item.Name,
                    Price = item.Price,
                    Quantity = item.Quantity,
                    ImageUrl = item.ImageUrl
                }).ToList()
            };
        }

        private void SaveCart(List<CartItemViewModel> cart)
        {
            HttpContext.Session.SetString("Cart", System.Text.Json.JsonSerializer.Serialize(cart));
        }
    }
}
