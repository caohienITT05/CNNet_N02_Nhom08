using SaigonChe.Web.Models;
using SaigonChe.Web.Services;
using Xunit;

namespace SaigonChe.Web.Tests.Services;

public sealed class PosPricingTests
{
    private static readonly DateTime UtcNow = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(2500, 22500)]
    [InlineData(30000, 0)]
    public void CalculateTotal_AppliesDiscountWithoutGoingBelowZero(decimal discount, decimal expected)
    {
        Assert.Equal(expected, VoucherPricingService.CalculateTotal(Items(), discount));
    }

    [Theory]
    [InlineData(VoucherType.FixedAmount, 5000, 0, 5000)]
    [InlineData(VoucherType.Percentage, 10, 0, 2500)]
    [InlineData(VoucherType.FixedAmount, 50000, 0, 25000)]
    public void TryCalculateDiscount_ReturnsCappedDiscount(
        VoucherType type, decimal amount, decimal minimumOrderTotal, decimal expected)
    {
        var voucher = CreateVoucher(type, amount, minimumOrderTotal);

        var valid = VoucherPricingService.TryCalculateDiscount(voucher, Items(), UtcNow, out var discount);

        Assert.True(valid);
        Assert.Equal(expected, discount);
    }

    [Fact]
    public void TryCalculateDiscount_RejectsExpiredInactiveExhaustedAndBelowMinimumVouchers()
    {
        var expired = CreateVoucher(VoucherType.FixedAmount, 1000, 0);
        expired.ValidTo = UtcNow.AddTicks(-1);
        var notStarted = CreateVoucher(VoucherType.FixedAmount, 1000, 0);
        notStarted.ValidFrom = UtcNow.AddTicks(1);
        var inactive = CreateVoucher(VoucherType.FixedAmount, 1000, 0);
        inactive.IsActive = false;
        var exhausted = CreateVoucher(VoucherType.FixedAmount, 1000, 0);
        exhausted.UseLimit = 2;
        exhausted.UseCount = 2;
        var belowMinimum = CreateVoucher(VoucherType.FixedAmount, 1000, 25001);

        foreach (var voucher in new[] { expired, notStarted, inactive, exhausted, belowMinimum })
        {
            Assert.False(VoucherPricingService.TryCalculateDiscount(voucher, Items(), UtcNow, out var discount));
            Assert.Equal(0, discount);
        }
    }

    [Fact]
    public void TryCalculateDiscount_RejectsPercentageAboveOneHundred()
    {
        var voucher = CreateVoucher(VoucherType.Percentage, 100.01m, 0);

        Assert.False(VoucherPricingService.TryCalculateDiscount(voucher, Items(), UtcNow, out var discount));
        Assert.Equal(0, discount);
    }

    [Theory]
    [InlineData(25000, 25000, true, 0)]
    [InlineData(25000, 30000, true, 5000)]
    [InlineData(25000, 24999, false, 0)]
    public void TryCalculateChange_ValidatesCashAndComputesChange(
        decimal total, decimal received, bool expectedValid, decimal expectedChange)
    {
        var valid = CashPaymentCalculator.TryCalculateChange(total, received, out var change);

        Assert.Equal(expectedValid, valid);
        Assert.Equal(expectedChange, change);
    }

    [Fact]
    public void TryCalculateChange_RejectsMissingAmountAndNegativeTotal()
    {
        Assert.False(CashPaymentCalculator.TryCalculateChange(25000, null, out var missingChange));
        Assert.Equal(0, missingChange);
        Assert.False(CashPaymentCalculator.TryCalculateChange(-1, 10000, out var negativeTotalChange));
        Assert.Equal(0, negativeTotalChange);
    }

    private static Voucher CreateVoucher(VoucherType type, decimal amount, decimal minimumOrderTotal) => new()
    {
        Code = "TEST",
        Type = type,
        Amount = amount,
        ValidFrom = UtcNow.AddDays(-1),
        ValidTo = UtcNow.AddDays(1),
        MinimumOrderTotal = minimumOrderTotal,
        IsActive = true
    };

    private static IReadOnlyList<OrderItem> Items() =>
    [
        new OrderItem { Price = 12500, Quantity = 2 }
    ];
}
