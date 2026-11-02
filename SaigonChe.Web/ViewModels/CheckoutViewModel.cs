using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using SaigonChe.Web.Models;

namespace SaigonChe.Web.ViewModels
{
    public class CheckoutViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ tên người nhận.")]
        [StringLength(100, ErrorMessage = "Họ tên không được vượt quá 100 ký tự.")]
        [Display(Name = "Họ và tên")]
        public string CustomerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
        [StringLength(30, ErrorMessage = "Số điện thoại không hợp lệ.")]
        [Display(Name = "Số điện thoại")]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập địa chỉ giao hàng.")]
        [StringLength(500, ErrorMessage = "Địa chỉ không được vượt quá 500 ký tự.")]
        [Display(Name = "Địa chỉ nhận chè")]
        public string Address { get; set; } = string.Empty;

        [Display(Name = "Ghi chú đơn hàng")]
        public string? Note { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn phương thức thanh toán.")]
        [Display(Name = "Phương thức thanh toán")]
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.COD;

        public List<CartItemViewModel> Items { get; set; } = new();

        public decimal SubTotal => Items.Sum(item => item.LineTotal);
        public decimal DiscountAmount { get; set; }
        public decimal Total => (SubTotal - DiscountAmount) > 0 ? (SubTotal - DiscountAmount) : 0;
    }
}