using TiemChe.Domain.Enums;

namespace TiemChe.Contracts.Management;

public sealed record CouponRequest(string? Code, CouponKind Kind, long DiscountValue, long? MaxDiscountVnd,
    long MinSubtotalVnd, DateTimeOffset StartsAt, DateTimeOffset? EndsAt, int? UsageLimit, bool IsActive);
public sealed record CouponDto(int Id, string Code, CouponKind Kind, long DiscountValue, long? MaxDiscountVnd,
    long MinSubtotalVnd, DateTime StartsAtUtc, DateTime? EndsAtUtc, int? UsageLimit, int UsedCount, bool IsActive);
public sealed record RevenuePoint(string Period, int CompletedOrders, long RevenueVnd);
public sealed record BestSeller(int ProductId, string Name, int Quantity, long ItemRevenueVnd);
public sealed record DashboardDto(int TotalOrders, int CompletedOrders, long RevenueVnd,
    IReadOnlyList<RevenuePoint> Revenue, IReadOnlyList<BestSeller> BestSellers);
