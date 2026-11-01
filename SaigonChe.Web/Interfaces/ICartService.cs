using System.Threading.Tasks;
using SaigonChe.Web.ViewModels;

namespace SaigonChe.Web.Interfaces
{
    public interface ICartService
    {
        Task<CartViewModel> GetCartAsync();
        Task<bool> AddToCartAsync(int productId, int quantity);
        Task<bool> UpdateQuantityAsync(int productId, int quantity);
        Task<bool> RemoveFromCartAsync(int productId);
        Task<bool> ApplyCouponAsync(string couponCode);
        Task ClearCartAsync();
        Task MergeSessionCartToUserAsync(string userId);
    }
}