using System.Text.Json.Serialization;

namespace SaigonChe.Web.ViewModels
{
    public class WebhookPayloadDto
    {
        [JsonPropertyName("transactionNumber")]
        public string? TransactionNumber { get; set; }

        [JsonPropertyName("amount")]
        public decimal Amount { get; set; }

        [JsonPropertyName("content")]
        public string? Content { get; set; } // Nội dung chuyển khoản chứa mã đơn

        [JsonPropertyName("secretKey")]
        public string? SecretKey { get; set; }
    }
}