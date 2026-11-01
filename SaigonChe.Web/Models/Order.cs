using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SaigonChe.Web.Models
{
    public class Order
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
        public string CustomerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập địa chỉ giao hàng.")]
        public string Address { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Total { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountAmount { get; set; }

        public OrderStatus Status { get; set; } = OrderStatus.Pending;

        public OrderType Type { get; set; } = OrderType.Online;

        public PaymentMethod? PaymentMethod { get; set; }

        public bool IsPaid { get; set; }

        public DateTime? PaidAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string? CustomerUserId { get; set; }

        public ApplicationUser? CustomerUser { get; set; }

        public string? AssignedStaffId { get; set; }

        public ApplicationUser? AssignedStaff { get; set; }

        public int? TableId { get; set; }

        public DiningTable? Table { get; set; }

        public int? VoucherId { get; set; }

        public Voucher? Voucher { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();

        public ICollection<OrderStatusHistory> StatusHistory { get; set; } = new List<OrderStatusHistory>();
    }
}
