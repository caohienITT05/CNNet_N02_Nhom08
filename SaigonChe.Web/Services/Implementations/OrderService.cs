using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SaigonChe.Web.Data;
using SaigonChe.Web.Interfaces;
using SaigonChe.Web.Models;
using SaigonChe.Web.ViewModels;

namespace SaigonChe.Web.Services.Implementations
{
    public class OrderService : IOrderService
    {
        private readonly ApplicationDbContext _context;

        public OrderService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Order> CreateOrderAsync(CheckoutViewModel model, CartViewModel cart, string? userId)
        {
            // Sinh mã đơn định dạng TF + NămThángNgàyGiờPhútGiây (VD: TF20261101235900)
            string orderCode = $"TF{DateTime.Now:yyyyMMddHHmmss}";

            var order = new Order
            {
                OrderCode = orderCode,
                UserId = userId,
                CustomerName = model.CustomerName,
                Phone = model.Phone,
                Address = model.Address,
                Note = model.Note,
                DiscountAmount = cart.DiscountAmount,
                Total = cart.FinalTotal,
                PaymentMethod = model.PaymentMethod,
                PaymentStatus = PaymentStatus.Pending,
                Status = OrderStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            // Lưu snapshot từng món chè tại thời điểm mua
            foreach (var item in cart.Items)
            {
                order.Items.Add(new OrderItem
                {
                    ProductId = item.ProductId,
                    ProductName = item.Name,
                    Price = item.Price,
                    Quantity = item.Quantity
                });
            }

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();
            return order;
        }

        public async Task<Order?> GetOrderByCodeAsync(string orderCode)
        {
            return await _context.Orders
                .Include(o => o.Items)
                .Include(o => o.Transactions)
                .FirstOrDefaultAsync(o => o.OrderCode == orderCode);
        }

        public async Task<bool> UpdateOrderStatusAsync(int orderId, OrderStatus newStatus)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null) return false;

            // Kiểm soát chuyển trạng thái đơn hợp lệ
            bool canTransition = (order.Status, newStatus) switch
            {
                (OrderStatus.Pending, OrderStatus.Confirmed) => true,
                (OrderStatus.Pending, OrderStatus.Cancelled) => true,
                (OrderStatus.Confirmed, OrderStatus.Preparing) => true,
                (OrderStatus.Confirmed, OrderStatus.Cancelled) => true,
                (OrderStatus.Preparing, OrderStatus.Shipping) => true,
                (OrderStatus.Preparing, OrderStatus.Cancelled) => true,
                (OrderStatus.Shipping, OrderStatus.Completed) => true,
                (OrderStatus.Shipping, OrderStatus.Cancelled) => true,
                _ => false
            };

            if (!canTransition) return false;

            order.Status = newStatus;
            if (newStatus == OrderStatus.Completed)
            {
                order.CompletedAt = DateTime.UtcNow;
                if (order.PaymentMethod == PaymentMethod.COD)
                {
                    order.PaymentStatus = PaymentStatus.Paid;
                    order.PaidAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();
            return true;
        }
    }
}