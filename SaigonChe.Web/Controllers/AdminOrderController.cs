using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaigonChe.Web.Data;
using SaigonChe.Web.Interfaces;
using SaigonChe.Web.Models;

namespace SaigonChe.Web.Controllers
{
    public class AdminOrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IOrderService _orderService;

        public AdminOrderController(ApplicationDbContext context, IOrderService orderService)
        {
            _context = context;
            _orderService = orderService;
        }

        // GET: /AdminOrder (Danh sách đơn hàng + bộ lọc trạng thái & COD/VietQR)
        [HttpGet]
        public async Task<IActionResult> Index(OrderStatus? status, PaymentMethod? paymentMethod, string? search)
        {
            var query = _context.Orders
                .Include(o => o.Items)
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(o => o.Status == status.Value);
            }

            if (paymentMethod.HasValue)
            {
                query = query.Where(o => o.PaymentMethod == paymentMethod.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                query = query.Where(o => o.OrderCode.Contains(search) || o.Phone.Contains(search) || o.CustomerName.Contains(search));
            }

            var orders = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();

            ViewBag.CurrentStatus = status;
            ViewBag.CurrentPaymentMethod = paymentMethod;
            ViewBag.CurrentSearch = search;

            // Thống kê nhanh tiền thu hộ COD đang chờ thu
            ViewBag.TotalCodPending = await _context.Orders
                .Where(o => o.PaymentMethod == PaymentMethod.COD && o.PaymentStatus == PaymentStatus.Pending && o.Status != OrderStatus.Cancelled)
                .SumAsync(o => o.Total);

            return View(orders);
        }

        // POST: /AdminOrder/UpdateStatus (Chuyển trạng thái vòng đời đơn hàng)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int orderId, OrderStatus newStatus)
        {
            var success = await _orderService.UpdateOrderStatusAsync(orderId, newStatus);
            if (!success)
            {
                TempData["Error"] = "Chuyển trạng thái không hợp lệ theo quy trình vòng đời đơn!";
            }
            else
            {
                TempData["Success"] = "Cập nhật trạng thái đơn hàng thành công!";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: /AdminOrder/ConfirmCodPayment (Xác nhận shipper đã nộp tiền COD)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmCodPayment(int orderId)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order != null && order.PaymentMethod == PaymentMethod.COD)
            {
                order.PaymentStatus = PaymentStatus.Paid;
                order.PaidAt = System.DateTime.UtcNow;
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Đã xác nhận thu tiền COD cho đơn {order.OrderCode}!";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}