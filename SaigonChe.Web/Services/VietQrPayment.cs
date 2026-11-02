using System.Globalization;
namespace SaigonChe.Web.Services;
public static class VietQrPayment
{
    public static bool IsConfigured(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration["Payment:BankId"])
        && !string.IsNullOrWhiteSpace(configuration["Payment:AccountNumber"])
        && !string.IsNullOrWhiteSpace(configuration["Payment:AccountName"]);

    public static string? MakeUrl(IConfiguration configuration, int orderId, decimal amount, string prefix)
    {
        if (!IsConfigured(configuration)) return null;
        var bank = Uri.EscapeDataString(configuration["Payment:BankId"]!);
        var account = Uri.EscapeDataString(configuration["Payment:AccountNumber"]!);
        var name = Uri.EscapeDataString(configuration["Payment:AccountName"]!);
        var template = Uri.EscapeDataString(configuration["Payment:QrTemplate"] ?? "compact2");
        var description = Uri.EscapeDataString($"{prefix} {orderId}");
        return $"https://img.vietqr.io/image/{bank}-{account}-{template}.png?amount={amount.ToString("0", CultureInfo.InvariantCulture)}&addInfo={description}&accountName={name}";
    }
}
