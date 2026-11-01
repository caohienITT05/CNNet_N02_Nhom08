using System.ComponentModel.DataAnnotations;
using SaigonChe.Web.Models;

namespace SaigonChe.Web.ViewModels
{
    public class UpdateOrderStatusViewModel
    {
        public int OrderId { get; set; }

        [EnumDataType(typeof(OrderStatus), ErrorMessage = "Trạng thái đơn hàng không hợp lệ.")]
        [Display(Name = "Trạng thái")]
        public OrderStatus Status { get; set; }

        [StringLength(500)]
        [Display(Name = "Ghi chú / lý do hủy")]
        public string? Note { get; set; }
    }
}
