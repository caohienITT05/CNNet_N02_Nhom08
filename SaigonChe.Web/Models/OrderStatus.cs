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
        Shipping = 4, // <-- Thành viên 2 bổ sung để đúng luồng giao chè

        [Display(Name = "Đã hoàn thành")]
        Completed = 5,

        [Display(Name = "Đã hủy")]
        Cancelled = 6
    }
}