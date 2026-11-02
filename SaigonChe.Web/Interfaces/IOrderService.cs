using System.Threading.Tasks;
using SaigonChe.Web.Models;
using SaigonChe.Web.ViewModels;

namespace SaigonChe.Web.Interfaces
{
    public interface IOrderService
    {
        Task<Order> CreateOrderAsync(CheckoutViewModel model, CartViewModel cart, string? userId);
        Task<Order?> GetOrderByCodeAsync(string orderCode);
        Task<bool> UpdateOrderStatusAsync(int orderId, OrderStatus newStatus);
    }
}