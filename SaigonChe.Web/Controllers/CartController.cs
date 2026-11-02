using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaigonChe.Web.Data;
using SaigonChe.Web.Interfaces;
using SaigonChe.Web.Models;
using SaigonChe.Web.ViewModels;

namespace SaigonChe.Web.Controllers
{
    public class CartController : Controller
    {
        private const int MaxQuantityPerProduct = 20;

        private readonly ICartService _cartService;
        private readonly IOrderService _orderService;
        private readonly ApplicationDbContext _context;

        public CartController(
            ICartService cartService,
            IOrderService orderService,
            ApplicationDbContext context)
        {
            _cartService = cartService;
            _orderService = orderService;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var cart = await _cartService.GetCartAsync();
            return View(cart);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToCart(int productId, int quantity = 1)
        {
            if (!IsValidQuantity(productId, quantity))
            {
                TempData["CartError"] = $"Số lượng mỗi món phải từ 1 đến {MaxQuantityPerProduct}.";
                return RedirectToAction(nameof(Index));
            }

            var cart = await _cartService.GetCartAsync();
            var currentQuantity = cart.Items
                .Where(item => item.ProductId == productId)
                .Sum(item => item.Quantity);
            if (currentQuantity + quantity > MaxQuantityPerProduct)
            {
                TempData["CartError"] = $"Bạn chỉ có thể đặt tối đa {MaxQuantityPerProduct} phần cho mỗi món.";
                return RedirectToAction(nameof(Index));
            }

            if (!await _cartService.AddToCartAsync(productId, quantity))
            {
                TempData["CartError"] = "Sản phẩm không tồn tại hoặc đã ngừng bán.";
                return RedirectToAction("Index", "Home");
            }

            TempData["CartSuccess"] = "Đã thêm món vào giỏ hàng.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet, HttpPost]
        [AutoValidateAntiforgeryToken]
        public async Task<IActionResult> Add(int productId, int quantity = 1)
        {
            var isGetRequest = HttpMethods.IsGet(Request.Method);

            if (!IsValidQuantity(productId, quantity))
            {
                if (isGetRequest)
                {
                    TempData["CartError"] = $"Số lượng mỗi món phải từ 1 đến {MaxQuantityPerProduct}.";
                    return RedirectToAction(nameof(Index));
                }

                return Json(new { success = false, message = $"Số lượng mỗi món phải từ 1 đến {MaxQuantityPerProduct}." });
            }

            var cart = await _cartService.GetCartAsync();
            var currentQuantity = cart.Items
                .Where(item => item.ProductId == productId)
                .Sum(item => item.Quantity);
            if (currentQuantity + quantity > MaxQuantityPerProduct)
            {
                if (isGetRequest)
                {
                    TempData["CartError"] = $"Bạn chỉ có thể đặt tối đa {MaxQuantityPerProduct} phần cho mỗi món.";
                    return RedirectToAction(nameof(Index));
                }

                return Json(new { success = false, message = $"Bạn chỉ có thể đặt tối đa {MaxQuantityPerProduct} phần cho mỗi món." });
            }

            if (!await _cartService.AddToCartAsync(productId, quantity))
            {
                if (isGetRequest)
                {
                    TempData["CartError"] = "Không thể thêm món vào giỏ hàng.";
                    return RedirectToAction(nameof(Index));
                }

                return Json(new { success = false, message = "Không thể thêm món vào giỏ hàng." });
            }

            if (isGetRequest)
            {
                TempData["CartSuccess"] = "Đã thêm món vào giỏ hàng.";
                return RedirectToAction(nameof(Index));
            }

            cart = await _cartService.GetCartAsync();
            return Json(new
            {
                success = true,
                message = "Đã thêm món vào giỏ hàng thành công!",
                totalItems = cart.Items.Sum(item => item.Quantity),
                subTotal = cart.SubTotal
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateQuantity(int productId, int quantity)
        {
            if (!IsValidQuantity(productId, quantity))
            {
                TempData["CartError"] = $"Số lượng mỗi món phải từ 1 đến {MaxQuantityPerProduct}.";
                return RedirectToAction(nameof(Index));
            }

            if (!await _cartService.UpdateQuantityAsync(productId, quantity))
            {
                TempData["CartError"] = "Không thể cập nhật món trong giỏ hàng.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveItem(int productId)
        {
            await _cartService.RemoveFromCartAsync(productId);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(int productId, int quantity)
        {
            if (productId <= 0 || quantity is < 1 or > MaxQuantityPerProduct)
            {
                return Json(new { success = false, message = $"Số lượng mỗi món phải từ 1 đến {MaxQuantityPerProduct}." });
            }

            if (!await _cartService.UpdateQuantityAsync(productId, quantity))
            {
                return Json(new { success = false, message = "Không thể cập nhật số lượng." });
            }

            var cart = await _cartService.GetCartAsync();
            var item = cart.Items.FirstOrDefault(cartItem => cartItem.ProductId == productId);
            return Json(new
            {
                success = true,
                itemTotalPrice = item != null ? item.LineTotal.ToString("N0") + " đ" : "0 đ",
                subTotal = cart.SubTotal.ToString("N0") + " đ",
                discountAmount = cart.DiscountAmount.ToString("N0") + " đ",
                finalTotal = cart.FinalTotal.ToString("N0") + " đ",
                totalItems = cart.Items.Sum(cartItem => cartItem.Quantity)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(int productId)
        {
            var success = await _cartService.RemoveFromCartAsync(productId);
            var cart = await _cartService.GetCartAsync();
            return Json(new
            {
                success,
                message = success ? "Đã xóa món khỏi giỏ hàng." : "Lỗi khi xóa món.",
                subTotal = cart.SubTotal.ToString("N0") + " đ",
                discountAmount = cart.DiscountAmount.ToString("N0") + " đ",
                finalTotal = cart.FinalTotal.ToString("N0") + " đ",
                totalItems = cart.Items.Sum(item => item.Quantity)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApplyCoupon(string couponCode)
        {
            if (string.IsNullOrWhiteSpace(couponCode))
            {
                return Json(new { success = false, message = "Vui lòng nhập mã ưu đãi." });
            }

            if (!await _cartService.ApplyCouponAsync(couponCode.Trim().ToUpperInvariant()))
            {
                return Json(new { success = false, message = "Mã ưu đãi không hợp lệ hoặc đã hết hạn." });
            }

            var cart = await _cartService.GetCartAsync();
            return Json(new
            {
                success = true,
                message = "Áp dụng mã ưu đãi thành công!",
                discountAmount = cart.DiscountAmount.ToString("N0") + " đ",
                finalTotal = cart.FinalTotal.ToString("N0") + " đ"
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetCount()
        {
            var cart = await _cartService.GetCartAsync();
            return Json(new { count = cart.Items.Sum(item => item.Quantity) });
        }

        [HttpGet]
        public async Task<IActionResult> Checkout()
        {
            var cart = await _cartService.GetCartAsync();
            if (!cart.Items.Any())
            {
                return RedirectToAction(nameof(Index));
            }

            return View(new CheckoutViewModel
            {
                Items = cart.Items,
                DiscountAmount = cart.DiscountAmount
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(CheckoutViewModel model)
        {
            var cart = await _cartService.GetCartAsync();
            if (!cart.Items.Any())
            {
                TempData["CartError"] = "Giỏ hàng của bạn đang trống.";
                return RedirectToAction(nameof(Index));
            }

            if (cart.Items.Any(item =>
                    !IsValidQuantity(item.ProductId, item.Quantity)))
            {
                ModelState.AddModelError(string.Empty, "Giỏ hàng có số lượng không hợp lệ. Vui lòng kiểm tra lại.");
            }

            if (!Enum.IsDefined(typeof(PaymentMethod), model.PaymentMethod))
            {
                ModelState.AddModelError(nameof(model.PaymentMethod), "Vui lòng chọn phương thức thanh toán hợp lệ.");
            }

            var productIds = cart.Items.Select(item => item.ProductId).Distinct().ToList();
            var availableProductIds = await _context.Products
                .Where(product => productIds.Contains(product.Id) && product.IsAvailable)
                .Select(product => product.Id)
                .ToListAsync();
            if (availableProductIds.Count != productIds.Count)
            {
                ModelState.AddModelError(string.Empty, "Một hoặc nhiều món trong giỏ hiện không còn khả dụng.");
            }

            if (!ModelState.IsValid)
            {
                model.Items = cart.Items;
                model.DiscountAmount = cart.DiscountAmount;
                return View(model);
            }

            var userId = User.Identity?.IsAuthenticated == true
                ? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                : null;
            var order = await _orderService.CreateOrderAsync(model, cart, userId);

            order.CustomerUserId = userId;
            _context.OrderStatusHistory.Add(new OrderStatusHistory
            {
                OrderId = order.Id,
                Status = OrderStatus.Pending,
                ActorId = userId,
                Note = "Khách tạo đơn online.",
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
            await _cartService.ClearCartAsync();

            if (order.PaymentMethod == PaymentMethod.VietQR)
            {
                return RedirectToAction("VietQrPay", "Payment", new { orderCode = order.OrderCode });
            }

            return RedirectToAction("Success", "Order", new { orderCode = order.OrderCode });
        }

        private static bool IsValidQuantity(int productId, int quantity)
        {
            return productId > 0 && quantity is >= 1 and <= MaxQuantityPerProduct;
        }
    }
}
