using System.ComponentModel.DataAnnotations;

namespace SaigonChe.Web.Models
{
    public enum OrderStatus
    {
        [Display(Name = "Chờ xác nhận")]
        Pending,

        [Display(Name = "Đã xác nhận")]
        Confirmed,

        [Display(Name = "Đang chuẩn bị")]
        Preparing,

        [Display(Name = "Đã hoàn thành")]
        Completed,

        [Display(Name = "Đã hủy")]
        Cancelled
    }
}
