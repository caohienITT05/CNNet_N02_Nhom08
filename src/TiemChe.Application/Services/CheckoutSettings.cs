namespace TiemChe.Application.Services;

public sealed class CheckoutSettings
{
    private static readonly Dictionary<string, string> BankBins = new(StringComparer.OrdinalIgnoreCase)
    {
        ["vietcombank"] = "970436", ["vcb"] = "970436",
        ["techcombank"] = "970407", ["tcb"] = "970407",
        ["bidv"] = "970418",
        ["vietinbank"] = "970415", ["ctg"] = "970415",
        ["agribank"] = "970405", ["vba"] = "970405",
        ["mb bank"] = "970422", ["mbbank"] = "970422", ["mb"] = "970422",
        ["acb"] = "970416",
        ["sacombank"] = "970403", ["stb"] = "970403",
        ["tpbank"] = "970423", ["tpb"] = "970423",
        ["vpbank"] = "970432", ["vpb"] = "970432",
        ["shb"] = "970443",
        ["hdbank"] = "970437", ["hdb"] = "970437",
        ["eximbank"] = "970431", ["eib"] = "970431",
        ["msb"] = "970426",
        ["ocb"] = "970448",
        ["vib"] = "970441",
        ["seabank"] = "970440", ["seab"] = "970440",
        ["lpbank"] = "970449", ["lienvietpostbank"] = "970449",
        ["bacabank"] = "970409", ["baca"] = "970409",
        ["kienlongbank"] = "970452", ["klb"] = "970452",
        ["namabank"] = "970428", ["nama"] = "970428",
        ["saigonbank"] = "970400", ["sgb"] = "970400",
        ["vietbank"] = "970433", ["vietcapitalbank"] = "970454",
        ["abbank"] = "970425", ["ab bank"] = "970425",
        ["baovietbank"] = "970438", ["bvb"] = "970438",
        ["ncb"] = "970419",
        ["oceanbank"] = "970414", ["ocean"] = "970414",
        ["gpbank"] = "970408", ["gpb"] = "970408",
        ["cbbank"] = "970444", ["cimb"] = "970454",
        ["coopbank"] = "970446",
        ["dongabank"] = "970406", ["dab"] = "970406",
        ["pvcombank"] = "970412", ["pvcb"] = "970412",
        ["scb"] = "970429",
        ["shinhanbank"] = "970424", ["shinhan"] = "970424"
    };

    public long ShippingFeeVnd { get; init; } = 15_000;
    public string? BankName { get; init; }
    public string? AccountNumber { get; init; }
    public string? AccountName { get; init; }
    public string? BankBin { get; init; }

    public bool AllowsBankTransfer => !string.IsNullOrWhiteSpace(BankName)
        && !string.IsNullOrWhiteSpace(AccountNumber) && !string.IsNullOrWhiteSpace(AccountName);

    public bool SupportsQr => AllowsBankTransfer && ResolveBankBin() is not null;

    public string? QrImageUrl(long amountVnd, string reference)
    {
        var bin = ResolveBankBin();
        if (bin is null || amountVnd <= 0)
            return null;
        return "https://img.vietqr.io/image/" +
            $"{Uri.EscapeDataString(bin)}-{Uri.EscapeDataString(AccountNumber!.Trim())}-compact2.svg" +
            $"?amount={amountVnd}" +
            $"&addInfo={Uri.EscapeDataString(reference)}" +
            $"&accountName={Uri.EscapeDataString(AccountName!.Trim())}";
    }

    private string? ResolveBankBin()
    {
        if (!AllowsBankTransfer)
            return null;
        var configured = BankBin?.Trim();
        if (!string.IsNullOrEmpty(configured))
            return configured;
        var name = BankName!.Trim();
        return BankBins.TryGetValue(name, out var bin) ? bin : null;
    }
}
