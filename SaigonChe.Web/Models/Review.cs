using System.ComponentModel.DataAnnotations;

namespace SaigonChe.Web.Models
{
    public class Review
    {
        public int Id { get; set; }

        public int ProductId { get; set; }
        public Product? Product { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        [Range(1, 5, ErrorMessage = "Vui lòng chọn số sao từ 1 đến 5.")]
        public int Rating { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập nội dung đánh giá.")]
        [StringLength(1000, ErrorMessage = "Nội dung đánh giá không được vượt quá 1000 ký tự.")]
        public string Comment { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsHidden { get; set; }

        [StringLength(1000, ErrorMessage = "Câu trả lời không được vượt quá 1000 ký tự.")]
        public string? AdminReply { get; set; }
        public DateTime? RepliedAt { get; set; }
        public string? RepliedById { get; set; }
        public ApplicationUser? RepliedBy { get; set; }
    }
}
