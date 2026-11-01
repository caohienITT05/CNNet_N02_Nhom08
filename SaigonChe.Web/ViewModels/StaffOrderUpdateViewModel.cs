using System.ComponentModel.DataAnnotations;
using SaigonChe.Web.Models;

namespace SaigonChe.Web.ViewModels;

public sealed class StaffOrderUpdateViewModel
{
    [Range(1, int.MaxValue)] public int OrderId { get; set; }
    [EnumDataType(typeof(OrderStatus))] public OrderStatus Status { get; set; }
    [StringLength(500)] public string? Note { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
}
