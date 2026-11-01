using System.ComponentModel.DataAnnotations;

namespace SaigonChe.Web.Models
{
    public class OrderStatusHistory
    {
        public int Id { get; set; }

        public int OrderId { get; set; }

        public Order? Order { get; set; }

        public OrderStatus? PreviousStatus { get; set; }

        public OrderStatus Status { get; set; }

        [StringLength(450)]
        public string? ChangedByUserId { get; set; }

        [StringLength(256)]
        public string? ChangedBy { get; set; }

        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

        [StringLength(500)]
        public string? Note { get; set; }
    }
}
