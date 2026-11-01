using Microsoft.AspNetCore.Mvc.Rendering;

namespace SaigonChe.Web.ViewModels
{
    public class AdminReviewsViewModel
    {
        public int? ProductId { get; set; }
        public int? Rating { get; set; }
        public bool? IsHidden { get; set; }
        public List<SelectListItem> Products { get; set; } = new();
        public List<AdminReviewItemViewModel> Reviews { get; set; } = new();
    }

    public class AdminReviewItemViewModel
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public int Rating { get; set; }
        public string Comment { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool IsHidden { get; set; }
        public string? AdminReply { get; set; }
        public DateTime? RepliedAt { get; set; }
    }

    public class AdminReviewReplyViewModel
    {
        public int ReviewId { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Vui lòng nhập câu trả lời.")]
        [System.ComponentModel.DataAnnotations.StringLength(1000, ErrorMessage = "Câu trả lời không được vượt quá 1000 ký tự.")]
        public string AdminReply { get; set; } = string.Empty;

        public int? ProductId { get; set; }
        public int? Rating { get; set; }
        public bool? IsHidden { get; set; }
    }
}
