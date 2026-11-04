namespace TiemChe.Application.Services;

public sealed class CheckoutSettings
{
    public long ShippingFeeVnd { get; init; } = 15_000;
    public string? BankName { get; init; }
    public string? AccountNumber { get; init; }
    public string? AccountName { get; init; }

    public bool AllowsBankTransfer => !string.IsNullOrWhiteSpace(BankName)
        && !string.IsNullOrWhiteSpace(AccountNumber) && !string.IsNullOrWhiteSpace(AccountName);
}
