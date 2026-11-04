using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Microsoft.Win32;
using TiemChe.Contracts.Shopping;

namespace TiemChe.Wpf.Services;

public sealed class DesktopDialogs
{
    public bool Confirm(string text) => MessageBox.Show(Application.Current.MainWindow, text, "Tiệm Chè",
        MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public string? PickImage()
    {
        var dialog = new OpenFileDialog { Filter = "Ảnh PNG hoặc JPEG|*.png;*.jpg;*.jpeg", CheckFileExists = true };
        return dialog.ShowDialog(Application.Current.MainWindow) == true ? dialog.FileName : null;
    }

    public async Task<bool> SaveExcelAsync(byte[] workbook, CancellationToken ct)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Báo cáo Excel|*.xlsx", FileName = "TiemChe-DoanhThu.xlsx", DefaultExt = ".xlsx"
        };
        if (dialog.ShowDialog(Application.Current.MainWindow) != true)
            return false;
        await File.WriteAllBytesAsync(dialog.FileName, workbook, ct);
        return true;
    }

    public void Print(OrderDetails order)
    {
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true)
            return;
        var document = new FlowDocument
        {
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            FontSize = 12, PagePadding = new Thickness(32), ColumnWidth = double.PositiveInfinity,
            PageWidth = dialog.PrintableAreaWidth, PageHeight = dialog.PrintableAreaHeight
        };
        document.Blocks.Add(new Paragraph(new Bold(new Run("TIỆM CHÈ • HÓA ĐƠN"))));
        document.Blocks.Add(new Paragraph(new Run($"{order.OrderNumber} | {Display.LocalTime(order.PlacedAtUtc):dd/MM/yyyy HH:mm}")));
        document.Blocks.Add(new Paragraph(new Run($"{order.Recipient} • {order.Phone}\n{order.Address}")));
        foreach (var item in order.Items)
        {
            var toppings = item.Toppings.Count == 0 ? "" :
                "\n  + " + string.Join(", ", item.Toppings.Select(x => $"{x.Name} ({Display.Money(x.PriceVnd)})"));
            document.Blocks.Add(new Paragraph(new Run(
                $"{item.ProductName} ({item.SizeName}) × {item.Quantity}   {Display.Money(item.LineTotalVnd)}{toppings}")));
        }
        var amounts = order.Amounts;
        document.Blocks.Add(new Paragraph(new Run($"Tiền món: {Display.Money(amounts.SubtotalVnd)}\n" +
            $"Giảm giá: {Display.Money(amounts.DiscountVnd)}\nGiao hàng: {Display.Money(amounts.ShippingFeeVnd)}")));
        document.Blocks.Add(new Paragraph(new Bold(new Run($"Tổng tiền: {Display.Money(amounts.TotalVnd)}"))));
        document.Blocks.Add(new Paragraph(new Run(
            $"{Display.Label(order.Payment.Method)} • {Display.Label(order.Payment.Status)}")));
        dialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, $"Tiệm Chè {order.OrderNumber}");
    }
}
