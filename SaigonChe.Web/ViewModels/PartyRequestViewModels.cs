using System.ComponentModel.DataAnnotations;
using SaigonChe.Web.Models;

namespace SaigonChe.Web.ViewModels;

public sealed class PartyRequestCreateViewModel
{
    [Required, StringLength(100)]
    [Display(Name = "Họ tên liên hệ")]
    public string CustomerName { get; set; } = string.Empty;

    [Required, Phone, StringLength(30)]
    [Display(Name = "Số điện thoại")]
    public string Phone { get; set; } = string.Empty;

    [Required, StringLength(300)]
    [Display(Name = "Địa điểm tổ chức")]
    public string Location { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Thời gian tổ chức")]
    public DateTime? EventAt { get; set; }

    [Range(1, 10000)]
    [Display(Name = "Số khách dự kiến")]
    public int Headcount { get; set; }

    [StringLength(2000)]
    [Display(Name = "Yêu cầu riêng")]
    public string? Notes { get; set; }
}

public sealed class PartyRequestUpdateViewModel
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    public int Id { get; set; }
    public PartyRequestStatus Status { get; set; }

    [Range(0, 1000000000)]
    [Display(Name = "Báo giá")]
    public decimal? QuoteAmount { get; set; }

    [StringLength(2000)]
    [Display(Name = "Nội dung tư vấn")]
    public string? Consultation { get; set; }

    [StringLength(500)]
    [Display(Name = "Lý do hủy")]
    public string? CancellationReason { get; set; }

    [Display(Name = "Phương thức thanh toán")]
    public PaymentMethod? PaymentMethod { get; set; }

    public List<PartyOrderLineViewModel> Lines { get; set; } = [];
}

public sealed class PartyOrderLineViewModel
{
    public int ProductId { get; set; }
    public bool Selected { get; set; }

    [Range(0, 10000)]
    public int Quantity { get; set; }
}

public sealed class PartyRequestQueueRowViewModel
{
    public int Id { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public DateTime EventAt { get; init; }
    public int Headcount { get; init; }
    public PartyRequestStatus Status { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class PartyRequestDetailsViewModel
{
    public string RowVersion { get; init; } = string.Empty;
    public int Id { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public DateTime EventAt { get; init; }
    public int Headcount { get; init; }
    public string? Notes { get; init; }
    public PartyRequestStatus Status { get; init; }
    public decimal? QuoteAmount { get; init; }
    public string? Consultation { get; init; }
    public string? CancellationReason { get; init; }
    public int? OrderId { get; init; }
    public PaymentMethod? PaymentMethod { get; init; }
    public string? VietQrUrl { get; init; }
    public bool IsStaffView { get; init; }
    public List<PartyProductChoiceViewModel> Products { get; init; } = [];
    public List<PartyOrderLineViewModel> SelectedLines { get; init; } = [];
    public IReadOnlyList<PartyRequestStatusHistoryRowViewModel> StatusHistory { get; init; } = [];
}

public sealed class PartyRequestStatusHistoryRowViewModel
{
    public PartyRequestStatus Status { get; init; }
    public DateTime CreatedAt { get; init; }
    public string? Note { get; init; }
    public bool IsCustomerAction { get; init; }
}

public sealed class PartyProductChoiceViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal Price { get; init; }
}
