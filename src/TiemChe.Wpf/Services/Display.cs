using System.Globalization;
using TiemChe.Contracts.Catalog;
using TiemChe.Domain.Enums;

namespace TiemChe.Wpf.Services;

public static class Display
{
    public static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");
    public static string Money(long value) => value.ToString("N0", Vietnamese) + " ₫";
    public static DateTime LocalTime(DateTime value) => value.AddHours(7);

    public static string Label(object? value) => value switch
    {
        RoleName.Customer => "Khách hàng", RoleName.Staff => "Nhân viên", RoleName.Admin => "Quản trị viên",
        OrderStatus.Pending => "Chờ xác nhận", OrderStatus.Preparing => "Đang chuẩn bị",
        OrderStatus.Delivering => "Đang giao", OrderStatus.Completed => "Hoàn thành", OrderStatus.Cancelled => "Đã hủy",
        OrderChannel.Online => "Online", OrderChannel.InStore => "Tại quầy",
        AvailabilityEntityType.Product => "Món", AvailabilityEntityType.ProductSize => "Size",
        AvailabilityEntityType.Topping => "Topping",
        PaymentStatus.Unpaid => "Chưa thanh toán", PaymentStatus.Paid => "Đã thanh toán",
        PaymentStatus.Cancelled => "Đã hủy", PaymentStatus.Refunded => "Đã hoàn tiền",
        PaymentMethod.CashOnDelivery => "Tiền mặt khi nhận", PaymentMethod.BankTransfer => "Chuyển khoản",
        CouponKind.FixedAmount => "Số tiền cố định", CouponKind.Percentage => "Phần trăm",
        ProductSort.Newest => "Mới nhất", ProductSort.Popular => "Bán chạy",
        ProductSort.PriceAscending => "Giá tăng dần", ProductSort.PriceDescending => "Giá giảm dần",
        ProductSort.Name => "Tên món", true => "Có", false => "Không",
        _ => value?.ToString() ?? ""
    };
}

public sealed record Choice<T>(string Label, T Value);
