namespace SaigonChe.Web.Interfaces
{
    public interface IVietQrService
    {
        string GenerateQuickLink(string bankId, string accountNo, string accountName, decimal amount, string orderCode);
    }
}