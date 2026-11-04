using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TiemChe.Application.Interfaces;
using TiemChe.Application.Validation;
using TiemChe.Contracts.Management;
using TiemChe.Domain.Enums;

namespace TiemChe.Application.Services;

public sealed class ReportService(ITiemCheData data, TimeProvider time)
{
    public async Task<DashboardDto> DashboardAsync(DateOnly? from, DateOnly? to, bool byMonth, CancellationToken ct)
    {
        var endDate = to ?? DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime.AddHours(7));
        if (endDate < new DateOnly(2000, 1, 1) || endDate >= DateOnly.MaxValue)
            throw Check.Invalid("Ngày kết thúc báo cáo không hợp lệ.");
        var startDate = from ?? endDate.AddDays(-29);
        if (startDate > endDate || endDate.DayNumber - startDate.DayNumber > (byMonth ? 1826 : 365))
            throw Check.Invalid("Khoảng thời gian vượt giới hạn của báo cáo.");
        var start = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-7);
        var end = endDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(-7);
        var all = await data.Orders.CountAsync(x => x.PlacedAtUtc >= start && x.PlacedAtUtc < end, ct);
        var completed = data.Orders.AsNoTracking().Where(x => x.Status == OrderStatus.Completed
            && x.Payment.Status == PaymentStatus.Paid && x.CompletedAtUtc >= start && x.CompletedAtUtc < end);
        var rows = await completed.GroupBy(x => x.CompletedAtUtc!.Value.AddHours(7).Date)
            .Select(x => new { Date = x.Key, Count = x.Count(), Revenue = x.Sum(y => y.TotalVnd) }).ToListAsync(ct);
        var format = byMonth ? "yyyy-MM" : "yyyy-MM-dd";
        var byPeriod = rows.GroupBy(x => x.Date.ToString(format, CultureInfo.InvariantCulture))
            .ToDictionary(x => x.Key, x => new RevenuePoint(x.Key, x.Sum(y => y.Count), x.Sum(y => y.Revenue)));
        var periods = new List<RevenuePoint>();
        for (var date = byMonth ? new DateOnly(startDate.Year, startDate.Month, 1) : startDate; date <= endDate;)
        {
            var label = date.ToString(format, CultureInfo.InvariantCulture);
            periods.Add(byPeriod.GetValueOrDefault(label) ?? new RevenuePoint(label, 0, 0));
            if (byMonth && date.Year == endDate.Year && date.Month == endDate.Month)
                break;
            date = byMonth ? date.AddMonths(1) : date.AddDays(1);
        }
        var best = await data.OrderItems.AsNoTracking().Where(x => completed.Any(o => o.Id == x.OrderId))
            .GroupBy(x => new { x.ProductSize.ProductId, x.ProductSize.Product.Name })
            .OrderByDescending(x => x.Sum(y => y.Quantity)).ThenBy(x => x.Key.ProductId).Take(10)
            .Select(x => new BestSeller(x.Key.ProductId, x.Key.Name, x.Sum(y => y.Quantity), x.Sum(y => y.LineTotalVnd)))
            .ToListAsync(ct);
        return new DashboardDto(all, rows.Sum(x => x.Count), rows.Sum(x => x.Revenue), periods, best);
    }
}
