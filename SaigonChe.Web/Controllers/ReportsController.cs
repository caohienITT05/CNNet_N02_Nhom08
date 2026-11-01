using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaigonChe.Web.Data;
using SaigonChe.Web.Models;
using SaigonChe.Web.ViewModels;

namespace SaigonChe.Web.Controllers;

[Authorize(Roles = "Admin")]
public sealed class ReportsController(ApplicationDbContext context) : Controller
{
    private static readonly TimeZoneInfo VietnamTimeZone = ResolveVietnamTimeZone();

    [HttpGet]
    public async Task<IActionResult> Index(DateOnly? from, DateOnly? to, string? type)
    {
        var range = NormalizeRange(from, to);
        var report = await BuildReportAsync(range.From, range.To, type);
        return View(report);
    }

    [HttpGet]
    public async Task<IActionResult> ExportExcel(DateOnly? from, DateOnly? to, string? type)
    {
        var range = NormalizeRange(from, to);
        var report = await BuildReportAsync(range.From, range.To, type);
        var bytes = CreateWorkbook(report);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "bao-cao-saigonche.xlsx");
    }

    private async Task<ReportDashboardViewModel> BuildReportAsync(DateOnly from, DateOnly to, string? type)
    {
        if (to < from) (from, to) = (to, from);
        if ((to.DayNumber - from.DayNumber) > 365) from = to.AddDays(-365);

        var startLocal = from.ToDateTime(TimeOnly.MinValue);
        var endLocalExclusive = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, VietnamTimeZone);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(endLocalExclusive, VietnamTimeZone);

        var ordersQuery = context.Orders.AsNoTracking()
            .Where(order => order.IsPaid && order.Status == OrderStatus.Completed &&
                order.CompletedAt != null && order.CompletedAt >= startUtc && order.CompletedAt < endUtc);
        if (Enum.TryParse<OrderType>(type, ignoreCase: true, out var orderType))
            ordersQuery = ordersQuery.Where(order => order.Type == orderType);
        else
            type = null;

        var orders = await ordersQuery
            .Include(order => order.Items)
            .OrderByDescending(order => order.CompletedAt)
            .ToListAsync();

        var rows = orders.Select(order => new ReportOrderRow(
            order.Id,
            DateTime.SpecifyKind(order.CompletedAt!.Value, DateTimeKind.Utc),
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(order.CompletedAt.Value, DateTimeKind.Utc), VietnamTimeZone),
            order.Type,
            order.CustomerName,
            // Total stores the final amount due (POS keeps the discount separately
            // for display/audit; subtracting it again would double-count it).
            order.Total,
            order.Items.Sum(item => item.Quantity))).ToList();

        var localRows = rows.Select(row => new
        {
            Row = row,
            LocalDate = DateOnly.FromDateTime(row.CompletedAtLocal)
        }).ToList();
        var months = new List<ReportBucket>();
        var cursor = new DateOnly(from.Year, from.Month, 1);
        var lastMonth = new DateOnly(to.Year, to.Month, 1);
        while (cursor <= lastMonth)
        {
            months.Add(new ReportBucket(cursor.ToString("MM/yyyy"),
                localRows.Where(item => item.LocalDate.Year == cursor.Year && item.LocalDate.Month == cursor.Month).Sum(item => item.Row.Revenue)));
            cursor = cursor.AddMonths(1);
        }

        var itemRows = orders.SelectMany(order => order.Items.Select(item => new
        {
            item.ProductName,
            item.Quantity,
            Revenue = item.Price * item.Quantity
        }));

        return new ReportDashboardViewModel
        {
            From = from,
            To = to,
            Type = type,
            Revenue = rows.Sum(row => row.Revenue),
            OrderCount = rows.Count,
            CustomerCount = orders.Where(order => order.CustomerUserId != null).Select(order => order.CustomerUserId).Distinct().Count(),
            GuestPosOrderCount = orders.Count(order => order.Type == OrderType.Pos && order.CustomerUserId == null),
            Orders = rows,
            BestSellers = itemRows.GroupBy(item => item.ProductName)
                .Select(group => new ReportProductRow(group.Key, group.Sum(item => item.Quantity), group.Sum(item => item.Revenue)))
                .OrderByDescending(item => item.Units).Take(10).ToList(),
            DailyRevenue = localRows.GroupBy(item => item.LocalDate)
                .OrderBy(group => group.Key)
                .Select(group => new ReportBucket(group.Key.ToString("dd/MM"), group.Sum(item => item.Row.Revenue))).ToList(),
            MonthlyRevenue = months
        };
    }

    private static (DateOnly From, DateOnly To) NormalizeRange(DateOnly? from, DateOnly? to)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamTimeZone));
        var end = to ?? today;
        return (from ?? end.AddDays(-29), end);
    }

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
    }

    private static byte[] CreateWorkbook(ReportDashboardViewModel report)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddXml(archive, "[Content_Types].xml", new XDocument(
                new XElement("Types", new XAttribute("xmlns", "http://schemas.openxmlformats.org/package/2006/content-types"),
                    new XElement("Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                    new XElement("Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                    new XElement("Override", new XAttribute("PartName", "/xl/workbook.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
                    new XElement("Override", new XAttribute("PartName", "/xl/worksheets/sheet1.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")))));
            AddXml(archive, "_rels/.rels", new XDocument(
                new XElement("Relationships", new XAttribute("xmlns", "http://schemas.openxmlformats.org/package/2006/relationships"),
                    new XElement("Relationship", new XAttribute("Id", "rId1"), new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"), new XAttribute("Target", "xl/workbook.xml")))));
            AddXml(archive, "xl/workbook.xml", new XDocument(
                new XElement("workbook", new XAttribute("xmlns", "http://schemas.openxmlformats.org/spreadsheetml/2006/main"),
                    new XAttribute(XNamespace.Xmlns + "r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships"),
                    new XElement("sheets", new XElement("sheet", new XAttribute("name", "Đơn hoàn tất"), new XAttribute("sheetId", 1), new XAttribute(XName.Get("id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships"), "rId1"))))));
            AddXml(archive, "xl/_rels/workbook.xml.rels", new XDocument(
                new XElement("Relationships", new XAttribute("xmlns", "http://schemas.openxmlformats.org/package/2006/relationships"),
                    new XElement("Relationship", new XAttribute("Id", "rId1"), new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"), new XAttribute("Target", "worksheets/sheet1.xml")))));

            var ns = XNamespace.Get("http://schemas.openxmlformats.org/spreadsheetml/2006/main");
            var rows = new List<object?[]> { new object?[] { "Mã đơn", "Hoàn tất (giờ Việt Nam)", "Loại", "Khách hàng", "Doanh thu sau giảm", "Số phần" } };
            rows.AddRange(report.Orders.Select(order => new object?[]
            {
                order.Id, order.CompletedAtLocal.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), order.Type.ToString(), order.Customer, order.Revenue, order.Units
            }));
            var sheetData = new XElement(ns + "sheetData");
            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var row = new XElement(ns + "row", new XAttribute("r", rowIndex + 1));
                for (var colIndex = 0; colIndex < rows[rowIndex].Length; colIndex++)
                {
                    var value = rows[rowIndex][colIndex];
                    var cellRef = ColumnName(colIndex + 1) + (rowIndex + 1).ToString(CultureInfo.InvariantCulture);
                    if (value is int or decimal or double)
                        row.Add(new XElement(ns + "c", new XAttribute("r", cellRef), new XElement(ns + "v", Convert.ToString(value, CultureInfo.InvariantCulture))));
                    else
                        row.Add(new XElement(ns + "c", new XAttribute("r", cellRef), new XAttribute("t", "inlineStr"), new XElement(ns + "is", new XElement(ns + "t", value?.ToString() ?? string.Empty))));
                }
                sheetData.Add(row);
            }
            AddXml(archive, "xl/worksheets/sheet1.xml", new XDocument(new XElement(ns + "worksheet", sheetData)));
        }
        return output.ToArray();
    }

    private static void AddXml(ZipArchive archive, string path, XDocument document)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
        using var stream = entry.Open();
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new System.Text.UTF8Encoding(false), Indent = false });
        document.Save(writer);
    }

    private static string ColumnName(int number)
    {
        var result = string.Empty;
        while (number > 0) { number--; result = (char)('A' + number % 26) + result; number /= 26; }
        return result;
    }
}
