using System;
using System.Net;
using SaigonChe.Web.Interfaces;

namespace SaigonChe.Web.Services.Implementations
{
    public class VietQrService : IVietQrService
    {
        public string GenerateQuickLink(string bankId, string accountNo, string accountName, decimal amount, string orderCode)
        {
            // Chuẩn hóa tham số theo quy chuẩn VietQR QuickLink (Napas247)
            string encodedOrderCode = WebUtility.UrlEncode(orderCode);
            string encodedAccountName = WebUtility.UrlEncode(accountName);
            long amountLong = Convert.ToInt64(amount);

            // Template compact2: ảnh QR kèm logo VietQR, Napas và thông tin chuyển khoản
            return $"https://img.vietqr.io/image/{bankId}-{accountNo}-compact2.jpg?amount={amountLong}&addInfo={encodedOrderCode}&accountName={encodedAccountName}";
        }
    }
}