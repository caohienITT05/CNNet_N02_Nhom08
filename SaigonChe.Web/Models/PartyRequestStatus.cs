using System.ComponentModel.DataAnnotations;

namespace SaigonChe.Web.Models
{
    public enum PartyRequestStatus
    {
        [Display(Name = "Mới gửi")]
        Pending = 0,
        [Display(Name = "Đang xử lý")]
        InProgress = 1,
        [Display(Name = "Đã xác nhận")]
        Confirmed = 2,
        [Display(Name = "Hoàn thành")]
        Completed = 3,
        [Display(Name = "Đã hủy")]
        Cancelled = 4
    }
}

namespace SaigonChe.Web.Models
{
    public static class PartyRequestStatusExtensions
    {
        public static string ToDisplayName(this PartyRequestStatus status) => status switch
        {
            PartyRequestStatus.Pending => "Mới gửi",
            PartyRequestStatus.InProgress => "Đang xử lý",
            PartyRequestStatus.Confirmed => "Đã xác nhận",
            PartyRequestStatus.Completed => "Hoàn thành",
            PartyRequestStatus.Cancelled => "Đã hủy",
            _ => "Không xác định"
        };
    }
}
