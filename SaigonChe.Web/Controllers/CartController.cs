using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SaigonChe.Web.Interfaces;
using SaigonChe.Web.Models;
using SaigonChe.Web.ViewModels;

namespace SaigonChe.Web.Controllers
{
    public class CartController : Controller
    {
        private readonly ICartService _cartService;
        private readonly IOrderService _orderService;

        public CartController(ICartService cartService, IOrderService orderService)
        {
            _cartService = cartService;
            _orderService = orderService;
        }

        // GET: /Cart (Trang xem giỏ hàng)
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var cart = await _cartService.GetCartAsync();
            return View(cart);
        }

        // GET & POST: /Cart/Add (Hỗ trợ cả gọi trực tiếp từ URL trình duyệt lẫn gọi AJAX)
        [HttpGet, HttpPost]
        public async Task<IActionResult> Add(int productId = 1, int quantity = 1)
        {
            var success = await _cartService.AddToCartAsync(productId, quantity);

            // Nếu người dùng gõ trực tiếp trên thanh địa chỉ URL (GET request), chuyển hướng thẳng về giỏ hàng để xem kết quả
            if (HttpMethods.IsGet(Request.Method))
            {
                return RedirectToAction(nameof(Index));
            }

            // Nếu gọi qua AJAX (POST request)
            if (!success)
            {
                return Json(new { success = false, message = "Không thể thêm món vào giỏ hàng." });
            }

            var cart = await _cartService.GetCartAsync();
            return Json(new
            {
                success = true,
                message = "Đã thêm món vào giỏ hàng thành công!",
                totalItems = cart.Items.Sum(x => x.Quantity),
                subTotal = cart.SubTotal
            });
        }

        // POST: /Cart/Update (Cập nhật số lượng qua AJAX)
        [HttpPost]
        public async Task<IActionResult> Update(int productId, int quantity)
        {
            var success = await _cartService.UpdateQuantityAsync(productId, quantity);
            if (!success)
            {
                return Json(new { success = false, message = "Không thể cập nhật số lượng." });
            }

            var cart = await _cartService.GetCartAsync();
            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);

            return Json(new
            {
                success = true,
                itemTotalPrice = item != null ? item.TotalPrice.ToString("N0") + " đ" : "0 đ",
                subTotal = cart.SubTotal.ToString("N0") + " đ",
                discountAmount = cart.DiscountAmount.ToString("N0") + " đ",
                finalTotal = cart.FinalTotal.ToString("N0") + " đ",
                totalItems = cart.Items.Sum(x => x.Quantity)
            });
        }

        // POST: /Cart/Remove (Xóa món qua AJAX)
        [HttpPost]
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
                totalItems = cart.Items.Sum(x => x.Quantity)
            });
        }

        // POST: /Cart/ApplyCoupon (Áp dụng mã ưu đãi)
        [HttpPost]
        public async Task<IActionResult> ApplyCoupon(string couponCode)
        {
            if (string.IsNullOrWhiteSpace(couponCode))
            {
                return Json(new { success = false, message = "Vui lòng nhập mã ưu đãi." });
            }

            var success = await _cartService.ApplyCouponAsync(couponCode.Trim().ToUpper());
            if (!success)
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

        // GET: /Cart/GetCount (Đếm tổng số món hiển thị badge icon giỏ hàng)
        [HttpGet]
        public async Task<IActionResult> GetCount()
        {
            var cart = await _cartService.GetCartAsync();
            return Json(new { count = cart.Items.Sum(x => x.Quantity) });
        }

        // GET: /Cart/Checkout (Hiển thị trang điền thông tin giao hàng)
        [HttpGet]
        public async Task<IActionResult> Checkout()
        {
            var cart = await _cartService.GetCartAsync();
            if (!cart.Items.Any())
            {
                return RedirectToAction(nameof(Index));
            }

            var model = new CheckoutViewModel
            {
                Items = cart.Items,
                DiscountAmount = cart.DiscountAmount
            };

            return View(model);
        }

        // POST: /Cart/Checkout (Xử lý đặt hàng khi khách bấm nút Xác nhận)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(CheckoutViewModel model)
        {
            var cart = await _cartService.GetCartAsync();
            if (!cart.Items.Any())
            {
                ModelState.AddModelError("", "Giỏ hàng của bạn đang trống.");
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                model.Items = cart.Items;
                model.DiscountAmount = cart.DiscountAmount;
                return View(model);
            }

            // Lấy ID tài khoản nếu đã đăng nhập, ngược lại là null (khách vãng lai)
            string? userId = User.Identity?.IsAuthenticated == true
                ? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                : null;

            // Tạo đơn hàng mới trong cơ sở dữ liệu
            var order = await _orderService.CreateOrderAsync(model, cart, userId);

            // Xóa sạch giỏ hàng sau khi đặt thành công
            await _cartService.ClearCartAsync();

            if (order.PaymentMethod == PaymentMethod.VietQR)
            {
                // Điều hướng sang trang hiển thị mã QR động để chuyển khoản
                return RedirectToAction("VietQrPay", "Payment", new { orderCode = order.OrderCode });
            }

            // Đơn COD: Điều hướng sang trang xác nhận đặt hàng thành công
            return RedirectToAction("Success", "Order", new { orderCode = order.OrderCode });
        }
    }
}