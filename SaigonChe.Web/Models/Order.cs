using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SaigonChe.Web.Models
{
    public class Order
    {
        public int Id { get; set; }

        // --- CÁC TRƯỜNG THÀNH VIÊN 2 BỔ SUNG ---
        // Mã đơn hàng công khai (VD: TF261031001) dùng làm nội dung chuyển khoản VietQR và tra cứu
        [Required]
        [StringLength(50)]
        public string OrderCode { get; set; } = string.Empty;

        // Null nếu là khách vãng lai (Guest), có giá trị nếu User đã đăng nhập
        public string? UserId { get; set; }

        public string? Note { get; set; } // Ghi chú của khách

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; } = 0; // Tiền giảm giá từ Coupon

        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.COD;
        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

        public DateTime? PaidAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        // ----------------------------------------

        // CÁC TRƯỜNG CŨ CỦA NHÓM ĐƯỢC GIỮ NGUYÊN
        [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
        public string CustomerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập địa chỉ giao hàng.")]
        public string Address { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Total { get; set; } // Tổng tiền thanh toán cuối cùng

        public OrderStatus Status { get; set; } = OrderStatus.Pending;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
        
        // Liên kết đến lịch sử thanh toán VietQR
        public ICollection<PaymentTransaction> Transactions { get; set; } = new List<PaymentTransaction>();
    }
}