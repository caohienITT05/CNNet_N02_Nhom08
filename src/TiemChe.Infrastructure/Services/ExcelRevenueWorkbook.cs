using ClosedXML.Excel;
using TiemChe.Application.Interfaces;
using TiemChe.Contracts.Management;

namespace TiemChe.Infrastructure.Services;

public sealed class ExcelRevenueWorkbook : IRevenueWorkbook
{
    public byte[] Create(DashboardDto dashboard)
    {
        using var workbook = new XLWorkbook();
        var revenue = workbook.AddWorksheet("Doanh thu");
        Header(revenue, "Kỳ", "Đơn hoàn thành", "Doanh thu (VNĐ)");
        var row = 2;
        foreach (var point in dashboard.Revenue)
        {
            revenue.Cell(row, 1).Value = point.Period;
            revenue.Cell(row, 2).Value = point.CompletedOrders;
            revenue.Cell(row++, 3).Value = point.RevenueVnd;
        }
        revenue.Cell(row, 1).Value = "Tổng";
        revenue.Cell(row, 2).Value = dashboard.CompletedOrders;
        revenue.Cell(row, 3).Value = dashboard.RevenueVnd;
        revenue.Row(row).Style.Font.Bold = true;
        revenue.Column(3).Style.NumberFormat.Format = "#,##0";
        revenue.Columns().AdjustToContents();

        var best = workbook.AddWorksheet("Món bán chạy");
        Header(best, "Mã món", "Tên món", "Số phần", "Tiền món (VNĐ)");
        row = 2;
        foreach (var item in dashboard.BestSellers)
        {
            best.Cell(row, 1).Value = item.ProductId;
            best.Cell(row, 2).Value = item.Name;
            best.Cell(row, 3).Value = item.Quantity;
            best.Cell(row++, 4).Value = item.ItemRevenueVnd;
        }
        best.Column(4).Style.NumberFormat.Format = "#,##0";
        best.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void Header(IXLWorksheet sheet, params string[] labels)
    {
        for (var index = 0; index < labels.Length; index++)
            sheet.Cell(1, index + 1).Value = labels[index];
        var row = sheet.Range(1, 1, 1, labels.Length);
        row.Style.Font.Bold = true;
        row.Style.Font.FontColor = XLColor.White;
        row.Style.Fill.BackgroundColor = XLColor.DarkGreen;
        sheet.SheetView.FreezeRows(1);
    }
}
