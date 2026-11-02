using System.ComponentModel.DataAnnotations;

namespace SaigonChe.Web.Models
{
    public enum OrderStatus
    {
        [Display(Name = "Chờ xác nhận")]
        Pending = 1,

        [Display(Name = "Đã xác nhận")]
        Confirmed = 2,

        [Display(Name = "Đang chuẩn bị")]
        Preparing = 3,

        [Display(Name = "Đang giao hàng")]
        Shipping = 4,

        [Display(Name = "Đã hoàn thành")]
        Completed = 5,

        [Display(Name = "Đã hủy")]
        Cancelled = 6,

        [Display(Name = "Đang giao")]
        Delivering = 7
    }
}
