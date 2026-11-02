using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace SaigonChe.Web.Models
{
    public class PaymentTransaction
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public Order? Order { get; set; }

        public string TransactionCode { get; set; } = string.Empty; // Mã giao dịch do VietQR/Ngân hàng gửi về
        public string ReferenceCode { get; set; } = string.Empty;   // Nội dung chuyển khoản thu được

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }                         // Số tiền thực chuyển

        public string Provider { get; set; } = "VietQR";
        public string Status { get; set; } = "Success";             // Success, AmountMismatch
        public string? RawWebhookData { get; set; }                 // Lưu toàn bộ payload JSON từ Webhook
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}