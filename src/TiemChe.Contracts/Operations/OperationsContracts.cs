using TiemChe.Contracts.Catalog;
using TiemChe.Domain.Enums;

namespace TiemChe.Contracts.Operations;

public sealed record AvailabilityRequest(bool IsActive);
public sealed record AvailabilitySize(int Id, string Name, long PriceVnd, bool IsActive);
public sealed record AvailabilityProduct(int Id, string Name, int CategoryId, string CategoryName,
    bool IsActive, IReadOnlyList<AvailabilitySize> Sizes);
public sealed record AvailabilityCatalog(IReadOnlyList<AvailabilityProduct> Products,
    IReadOnlyList<ToppingDto> Toppings);
public sealed record ShiftStatusCount(OrderStatus Status, int Count);
public sealed record ShiftSummary(DateOnly Date, IReadOnlyList<ShiftStatusCount> Statuses, int PendingCount,
    int PendingUnpaidCount, long CashCollectedVnd, long ConfirmedTransferVnd, int MyHandledOrders);
public sealed record StaffCheckoutOptions(bool AllowsBankTransfer, bool SupportsQr, string? BankName);
public sealed record StaffOrderLineRequest(int SizeId, int Quantity, IReadOnlyList<int>? ToppingIds);
public sealed record StaffOrderRequest(string? RecipientName, string? Phone, string? Note,
    PaymentMethod PaymentMethod, IReadOnlyList<StaffOrderLineRequest>? Items, Guid? RequestId = null,
    int? CustomerId = null, string? CouponCode = null, bool CompleteNow = true, bool PaymentReceived = false,
    long? ExpectedTotalVnd = null);
public sealed record StaffCustomerDto(int Id, string FullName, string? Phone, string Email);
public sealed record OpenCashShiftRequest(long OpeningCashVnd);
public sealed record CloseCashShiftRequest(long CountedCashVnd, string? Note);
public sealed record CashShiftDto(int Id, int UserId, string StaffName, DateTime OpenedAtUtc, long OpeningCashVnd,
    DateTime? ClosedAtUtc, long? CountedCashVnd, long? ExpectedCashVnd, long? DifferenceVnd, string? Note,
    long CashCollectedVnd, long ConfirmedTransferVnd, int HandledOrderCount);
public sealed record CashShiftStateDto(CashShiftDto? Shift);
public sealed record AvailabilityLogDto(int Id, AvailabilityEntityType EntityType, int EntityId, string EntityName,
    bool OldValue, bool NewValue, int ChangedByUserId, string ChangedByName, DateTime ChangedAtUtc);
