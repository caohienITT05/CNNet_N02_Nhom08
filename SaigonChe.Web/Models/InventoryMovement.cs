using System.ComponentModel.DataAnnotations;

namespace SaigonChe.Web.Models
{
    public class InventoryMovement
    {
        public int Id { get; set; }

        public int ProductId { get; set; }
        public Product? Product { get; set; }

        public int? OrderId { get; set; }
        public Order? Order { get; set; }

        public string? ActorId { get; set; }
        public ApplicationUser? Actor { get; set; }

        // Positive values add stock; negative values record sales or corrections.
        public int QuantityChange { get; set; }

        [Required, StringLength(100)]
        public string Reason { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
