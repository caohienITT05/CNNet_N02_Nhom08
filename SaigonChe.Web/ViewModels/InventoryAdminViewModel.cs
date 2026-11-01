using System.ComponentModel.DataAnnotations;

namespace SaigonChe.Web.ViewModels;

public sealed class InventoryAdminViewModel
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int? Stock { get; set; }
}

public sealed class InventoryChangeViewModel
{
    [Range(1, int.MaxValue)] public int ProductId { get; set; }
    [Range(-10000, 10000)] public int QuantityChange { get; set; }
    [Required, StringLength(100)] public string Reason { get; set; } = string.Empty;
}

public sealed class DiningTableFormViewModel
{
    public int Id { get; set; }
    [Required, StringLength(30)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(80)] public string Name { get; set; } = string.Empty;
    [Range(1, 100)] public int Capacity { get; set; } = 4;
    public bool IsActive { get; set; } = true;
}

public sealed class VoucherFormViewModel
{
    public int Id { get; set; }
    [Required, StringLength(40)] public string Code { get; set; } = string.Empty;
    public SaigonChe.Web.Models.VoucherType Type { get; set; }
    [Range(typeof(decimal), "0.01", "100000000")] public decimal Amount { get; set; }
    [DataType(DataType.Date)] public DateTime ValidFrom { get; set; }
    [DataType(DataType.Date)] public DateTime ValidTo { get; set; }
    [Range(typeof(decimal), "0", "100000000")] public decimal MinimumOrderTotal { get; set; }
    [Range(1, int.MaxValue)] public int? UseLimit { get; set; }
    public int UseCount { get; set; }
    public bool IsActive { get; set; } = true;
}
