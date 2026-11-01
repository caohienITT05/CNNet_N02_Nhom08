using System.Collections.Generic;
using System.Linq;

namespace SaigonChe.Web.ViewModels
{
    public class CartViewModel
    {
        public List<CartItemViewModel> Items { get; set; } = new List<CartItemViewModel>();
        public decimal SubTotal => Items.Sum(x => x.TotalPrice);
        public string? CouponCode { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal FinalTotal => (SubTotal - DiscountAmount) > 0 ? (SubTotal - DiscountAmount) : 0;
    }
}