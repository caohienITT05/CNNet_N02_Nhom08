using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.Win32;
using TiemChe.Contracts.Operations;
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
        document.Blocks.Add(new Paragraph(new Run(order.Channel == TiemChe.Domain.Enums.OrderChannel.InStore
            ? $"{order.Recipient} • {order.Phone}" : $"{order.Recipient} • {order.Phone}\n{order.Address}")));
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

    public void PrintKitchenTicket(OrderDetails order)
    {
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true)
            return;
        var document = NewDocument(dialog);
        document.Blocks.Add(new Paragraph(new Bold(new Run("TIỆM CHÈ • PHIẾU BẾP"))) { FontSize = 18 });
        document.Blocks.Add(new Paragraph(new Run(
            $"Mã đơn: {order.OrderNumber}\nGiờ đặt: {Display.LocalTime(order.PlacedAtUtc):dd/MM/yyyy HH:mm}\n" +
            $"Thanh toán: {Display.Label(order.Payment.Method)} • {Display.Label(order.Payment.Status)}")));
        document.Blocks.Add(new Paragraph(new Bold(new Run("MÓN CẦN LÀM"))) { FontSize = 14, Margin = new Thickness(0, 12, 0, 4) });
        foreach (var item in order.Items)
        {
            var toppings = item.Toppings.Count == 0
                ? "" : "\n   Topping: " + string.Join(", ", item.Toppings.Select(x => x.Name));
            document.Blocks.Add(new Paragraph(new Run(
                $"{item.Quantity} × {item.ProductName} ({item.SizeName}){toppings}")) { Margin = new Thickness(0, 0, 0, 6) });
        }
        document.Blocks.Add(new Paragraph(new Run(
            $"Ghi chú giao hàng: {order.Recipient} • {order.Phone}\n{order.Address}"))
            { Margin = new Thickness(0, 12, 0, 0) });
        dialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, $"Tiệm Chè {order.OrderNumber}");
    }

    public void PrintCupLabels(OrderDetails order)
    {
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true)
            return;
        var document = NewDocument(dialog);
        document.Blocks.Add(new Paragraph(new Bold(new Run("TIỆM CHÈ • TEM CỐC"))) { FontSize = 18 });
        foreach (var item in order.Items)
        {
            var toppings = item.Toppings.Count == 0
                ? "" : "Topping: " + string.Join(", ", item.Toppings.Select(x => x.Name));
            for (var cup = 1; cup <= item.Quantity; cup++)
            {
                var content = new StackPanel();
                content.Children.Add(new TextBlock
                {
                    Text = order.OrderNumber, FontWeight = FontWeights.SemiBold, FontSize = 11
                });
                content.Children.Add(new TextBlock
                {
                    Text = item.ProductName, FontWeight = FontWeights.Bold, FontSize = 16,
                    TextWrapping = TextWrapping.Wrap
                });
                content.Children.Add(new TextBlock { Text = $"Size: {item.SizeName}", FontSize = 12 });
                if (toppings.Length != 0)
                    content.Children.Add(new TextBlock
                    {
                        Text = toppings, FontSize = 12, TextWrapping = TextWrapping.Wrap
                    });
                document.Blocks.Add(new BlockUIContainer(new Border
                {
                    BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Padding = new Thickness(10),
                    Margin = new Thickness(0, 0, 0, 8), Child = content
                }));
            }
        }
        dialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, $"Tiệm Chè Tem cốc {order.OrderNumber}");
    }

    public void PrintCashShift(CashShiftDto shift)
    {
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true)
            return;
        var document = NewDocument(dialog);
        document.Blocks.Add(new Paragraph(new Bold(new Run("TIỆM CHÈ • BIÊN BẢN CHỐT CA"))) { FontSize = 18 });
        document.Blocks.Add(new Paragraph(new Run(
            $"Nhân viên: {shift.StaffName}\nMở ca: {Display.LocalTime(shift.OpenedAtUtc):dd/MM/yyyy HH:mm}\n" +
            $"Đóng ca: {(shift.ClosedAtUtc is DateTime closed ? Display.LocalTime(closed).ToString("dd/MM/yyyy HH:mm", Display.Vietnamese) : "Chưa đóng")}")));
        document.Blocks.Add(new Paragraph(new Run($"Tiền đầu ca: {Display.Money(shift.OpeningCashVnd)}")));
        document.Blocks.Add(new Paragraph(new Run($"Tiền mặt đã thu: {Display.Money(shift.CashCollectedVnd)}")));
        document.Blocks.Add(new Paragraph(new Run($"Chuyển khoản đã xác nhận: {Display.Money(shift.ConfirmedTransferVnd)}")));
        document.Blocks.Add(new Paragraph(new Run($"Số đơn đã xử lý: {shift.HandledOrderCount}")));
        document.Blocks.Add(new Paragraph(new Run($"Tiền dự kiến: {Display.Money(shift.ExpectedCashVnd ?? 0)}")));
        if (shift.CountedCashVnd is long counted)
            document.Blocks.Add(new Paragraph(new Run($"Tiền kiểm đếm: {Display.Money(counted)}")));
        if (shift.DifferenceVnd is long difference)
            document.Blocks.Add(new Paragraph(new Bold(new Run($"Chênh lệch: {Display.Money(difference)}"))));
        if (!string.IsNullOrWhiteSpace(shift.Note))
            document.Blocks.Add(new Paragraph(new Run($"Ghi chú: {shift.Note}")));
        dialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, $"Tiệm Chè Chốt ca {shift.Id}");
    }

    private static FlowDocument NewDocument(PrintDialog dialog) => new()
    {
        FontFamily = new FontFamily("Segoe UI"), FontSize = 12, PagePadding = new Thickness(32),
        ColumnWidth = double.PositiveInfinity, PageWidth = dialog.PrintableAreaWidth,
        PageHeight = dialog.PrintableAreaHeight
    };
}
