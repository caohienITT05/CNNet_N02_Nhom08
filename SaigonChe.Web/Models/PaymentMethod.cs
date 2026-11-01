using System.ComponentModel.DataAnnotations;

namespace SaigonChe.Web.Models
{
    public enum PaymentMethod
    {
        [Display(Name = "Tiền mặt khi nhận hàng (COD)")]
        COD = 1,

        [Display(Name = "Chuyển khoản VietQR")]
        VietQR = 2
    }
}