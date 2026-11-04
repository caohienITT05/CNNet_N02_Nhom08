using TiemChe.Domain.Enums;

namespace TiemChe.Contracts.Shopping;

public sealed record AddressDto(int Id, string RecipientName, string Phone, string AddressLine, bool IsDefault);
public sealed record AddressRequest(string? RecipientName, string? Phone, string? AddressLine, bool IsDefault);
public sealed record AddCartItemRequest(int SizeId, int Quantity, IReadOnlyList<int>? ToppingIds, Guid? Version);
public sealed record CartQuantityRequest(int Quantity, Guid Version);
public sealed record CartToppingDto(int Id, string Name, long PriceVnd);
public sealed record CartItemDto(int Id, int ProductId, int SizeId, string ProductName, string SizeName,
    long PriceVnd, int Quantity, IReadOnlyList<CartToppingDto> Toppings, long LineTotalVnd, bool IsAvailable);
public sealed record CartDto(Guid? Version, IReadOnlyList<CartItemDto> Items, long SubtotalVnd);
public sealed record CheckoutRequest(Guid RequestId, Guid CartVersion, int AddressId, PaymentMethod PaymentMethod,
    string? CouponCode, long? ExpectedTotalVnd = null);
public sealed record CheckoutQuote(long SubtotalVnd, long DiscountVnd, long ShippingFeeVnd, long TotalVnd,
    string? CouponCode);
public sealed record BankTransferDto(string BankName, string AccountNumber, string AccountName, string Reference);
public sealed record PaymentDto(PaymentMethod Method, PaymentStatus Status, long AmountVnd,
    DateTime? ConfirmedAtUtc, BankTransferDto? BankTransfer);
public sealed record OrderToppingDto(string Name, long PriceVnd);
public sealed record OrderItemDto(int Id, int ProductId, string ProductName, string SizeName, long UnitPriceVnd,
    int Quantity, long LineTotalVnd, IReadOnlyList<OrderToppingDto> Toppings);
public sealed record OrderHistoryDto(OrderStatus? PreviousStatus, OrderStatus Status, DateTime ChangedAtUtc, string? Note);
public sealed record OrderSummary(int Id, string OrderNumber, OrderStatus Status, PaymentStatus PaymentStatus,
    long TotalVnd, DateTime PlacedAtUtc, string Recipient);
public sealed record OrderDetails(int Id, string OrderNumber, Guid Version, OrderStatus Status, DateTime PlacedAtUtc,
    string Recipient, string Phone, string Address, CheckoutQuote Amounts, PaymentDto Payment,
    IReadOnlyList<OrderItemDto> Items, IReadOnlyList<OrderHistoryDto> History);
public sealed record OrderStatusRequest(Guid Version, OrderStatus Status, string? Note);
public sealed record VersionRequest(Guid Version);
public sealed record ReorderRequest(Guid? CartVersion);
public sealed record ReorderResult(CartDto Cart, IReadOnlyList<string> SkippedItems);
