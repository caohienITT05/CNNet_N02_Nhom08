using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaigonChe.Web.Data;
using SaigonChe.Web.Interfaces;

namespace SaigonChe.Web.Controllers
{
    public class OrderController : Controller
    {
        private readonly IOrderService _orderService;
        private readonly ApplicationDbContext _context;

        public OrderController(IOrderService orderService, ApplicationDbContext context)
        {
            _orderService = orderService;
            _context = context;
        }

        // GET: /Order/Success?orderCode=TFxxxx
        [HttpGet]
        public async Task<IActionResult> Success(string orderCode)
        {
            var order = await _orderService.GetOrderByCodeAsync(orderCode);
            if (order == null) return RedirectToAction("Index", "Home");

            return View(order);
        }

        // GET: /Order/Tracking?orderCode=...&phone=...
        [HttpGet]
        public async Task<IActionResult> Tracking(string? orderCode, string? phone)
        {
            if (string.IsNullOrWhiteSpace(orderCode) && string.IsNullOrWhiteSpace(phone))
            {
                return View();
            }

            var query = _context.Orders
                .Include(o => o.Items)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(orderCode))
            {
                query = query.Where(o => o.OrderCode == orderCode.Trim());
            }
            else if (!string.IsNullOrWhiteSpace(phone))
            {
                query = query.Where(o => o.Phone == phone.Trim()).OrderByDescending(o => o.CreatedAt);
            }

            var orders = await query.ToListAsync();
            return View(orders);
        }
    }
}