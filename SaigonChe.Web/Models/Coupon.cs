using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SaigonChe.Web.Models
{
    public class Coupon
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Code { get; set; } = string.Empty;

        public DiscountType DiscountType { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DiscountValue { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal MinOrderAmount { get; set; } // Giá trị đơn tối thiểu để dùng mã

        [Column(TypeName = "decimal(18,2)")]
        public decimal? MaxDiscountAmount { get; set; } // Mức giảm tối đa (nếu dùng %)

        public DateTime EndDate { get; set; }
        public int UsageLimit { get; set; } = 100;
        public int UsedCount { get; set; } = 0;
        public bool IsActive { get; set; } = true;
    }
}