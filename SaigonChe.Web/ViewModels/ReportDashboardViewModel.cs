using SaigonChe.Web.Models;

namespace SaigonChe.Web.ViewModels;

public sealed class ReportDashboardViewModel
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public string? Type { get; init; }
    public decimal Revenue { get; init; }
    public int OrderCount { get; init; }
    public int CustomerCount { get; init; }
    public int GuestPosOrderCount { get; init; }
    public IReadOnlyList<ReportOrderRow> Orders { get; init; } = [];
    public IReadOnlyList<ReportProductRow> BestSellers { get; init; } = [];
    public IReadOnlyList<ReportBucket> DailyRevenue { get; init; } = [];
    public IReadOnlyList<ReportBucket> MonthlyRevenue { get; init; } = [];
}

public sealed record ReportOrderRow(int Id, DateTime CompletedAtUtc, DateTime CompletedAtLocal, OrderType Type, string Customer, decimal Revenue, int Units);
public sealed record ReportProductRow(string Name, int Units, decimal Revenue);
public sealed record ReportBucket(string Label, decimal Revenue);
