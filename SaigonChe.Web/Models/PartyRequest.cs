using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SaigonChe.Web.Models
{
    public class PartyRequest
    {
        public int Id { get; set; }

        public string? CustomerUserId { get; set; }
        public ApplicationUser? Customer { get; set; }

        [Required, StringLength(100)]
        public string CustomerName { get; set; } = string.Empty;

        [Required, Phone, StringLength(30)]
        public string Phone { get; set; } = string.Empty;

        [Required, StringLength(300)]
        public string Location { get; set; } = string.Empty;

        public DateTime EventAt { get; set; }

        [Range(1, 10000)]
        public int Headcount { get; set; }

        [StringLength(2000)]
        public string? Notes { get; set; }

        public PartyRequestStatus Status { get; set; } = PartyRequestStatus.Pending;

        [Column(TypeName = "decimal(18,2)")]
        public decimal? QuoteAmount { get; set; }

        [StringLength(2000)]
        public string? Consultation { get; set; }

        [StringLength(500)]
        public string? CancellationReason { get; set; }

        public int? OrderId { get; set; }
        public Order? Order { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<PartyRequestStatusHistory> StatusHistory { get; set; } = new List<PartyRequestStatusHistory>();
    }
}
