namespace SaigonChe.Web.ViewModels
{
    public class VietQrPaymentViewModel
    {
        public string OrderCode { get; set; } = string.Empty;
        public decimal FinalAmount { get; set; }
        public string BankId { get; set; } = string.Empty;
        public string AccountNo { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string QrImageUrl { get; set; } = string.Empty;
    }
}