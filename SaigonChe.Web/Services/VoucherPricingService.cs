using SaigonChe.Web.Models;

namespace SaigonChe.Web.Services;

public static class VoucherPricingService
{
    public static decimal CalculateTotal(IEnumerable<OrderItem> items, decimal discountAmount)
    {
        var subtotal = items.Sum(item => item.Price * item.Quantity);
        return Math.Max(0, subtotal - discountAmount);
    }

    public static bool TryCalculateDiscount(
        Voucher voucher,
        IEnumerable<OrderItem> items,
        DateTime utcNow,
        out decimal discount)
    {
        var subtotal = items.Sum(item => item.Price * item.Quantity);
        if (!voucher.IsActive || voucher.ValidFrom > utcNow || voucher.ValidTo < utcNow
            || (voucher.UseLimit.HasValue && voucher.UseCount >= voucher.UseLimit.Value)
            || subtotal < voucher.MinimumOrderTotal
            || (voucher.Type == VoucherType.Percentage && voucher.Amount > 100))
        {
            discount = 0;
            return false;
        }

        discount = voucher.Type == VoucherType.Percentage
            ? subtotal * voucher.Amount / 100m
            : voucher.Amount;
        discount = Math.Min(subtotal, Math.Max(0, decimal.Round(discount, 2, MidpointRounding.AwayFromZero)));
        return true;
    }
}

public static class CashPaymentCalculator
{
    public static bool TryCalculateChange(decimal total, decimal? amountReceived, out decimal change)
    {
        if (total < 0 || !amountReceived.HasValue || amountReceived.Value < total)
        {
            change = 0;
            return false;
        }

        change = amountReceived.Value - total;
        return true;
    }
}
