using SaigonChe.Web.Models;

namespace SaigonChe.Web.ViewModels;

public sealed class PosDashboardViewModel
{
    public IReadOnlyList<Product> Products { get; init; } = [];
    public IReadOnlyList<DiningTable> Tables { get; init; } = [];
    public IReadOnlyList<Order> OpenOrders { get; init; } = [];
    public IReadOnlyList<Category> Categories { get; init; } = [];
    public string? BankId { get; init; }
    public string? AccountNumber { get; init; }
    public string? AccountName { get; init; }
}

public sealed class PosOrderViewModel
{
    public Order Order { get; init; } = new();
    public IReadOnlyList<Product> Products { get; init; } = [];
    public IReadOnlyList<Voucher> Vouchers { get; init; } = [];
    public string RowVersion { get; init; } = string.Empty;
    public string? VietQrUrl { get; init; }
    public bool VietQrConfigured { get; init; }
}

public sealed class PosReceiptViewModel
{
    public Order Order { get; init; } = new();
    public decimal AmountReceived { get; init; }
    public decimal Change { get; init; }
    public string? VietQrUrl { get; init; }
}

public sealed class PosItemPostViewModel
{
    public int OrderId { get; set; }
    public int ProductId { get; set; }

    [System.ComponentModel.DataAnnotations.Range(1, 50)]
    public int Quantity { get; set; } = 1;

    [System.ComponentModel.DataAnnotations.StringLength(500)]
    public string? Notes { get; set; }

    public string RowVersion { get; set; } = string.Empty;
}
