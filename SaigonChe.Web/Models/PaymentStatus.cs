using System.ComponentModel.DataAnnotations;

namespace SaigonChe.Web.Models
{
    public enum PaymentStatus
    {
        [Display(Name = "Chưa thanh toán")]
        Pending = 1,

        [Display(Name = "Đã thanh toán")]
        Paid = 2,

        [Display(Name = "Thanh toán thất bại")]
        Failed = 3
    }
}