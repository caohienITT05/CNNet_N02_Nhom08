using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SaigonChe.Web.Models
{
    public class Voucher
    {
        public int Id { get; set; }

        [Required, StringLength(40)]
        public string Code { get; set; } = string.Empty;

        public VoucherType Type { get; set; }

        [Column(TypeName = "decimal(18,2)"), Range(0.01, 100000000)]
        public decimal Amount { get; set; }

        public DateTime ValidFrom { get; set; }
        public DateTime ValidTo { get; set; }

        [Column(TypeName = "decimal(18,2)"), Range(0, 100000000)]
        public decimal MinimumOrderTotal { get; set; }

        [Range(1, int.MaxValue)]
        public int? UseLimit { get; set; }

        [Range(0, int.MaxValue)]
        public int UseCount { get; set; }

        public bool IsActive { get; set; } = true;

        public ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}
