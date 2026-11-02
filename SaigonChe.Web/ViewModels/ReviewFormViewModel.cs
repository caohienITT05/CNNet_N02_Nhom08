using System.ComponentModel.DataAnnotations;

namespace SaigonChe.Web.ViewModels
{
    public class ReviewFormViewModel
    {
        public int? ReviewId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Món chè không hợp lệ.")]
        public int ProductId { get; set; }

        [Range(1, 5, ErrorMessage = "Vui lòng chọn số sao từ 1 đến 5.")]
        [Display(Name = "Đánh giá")]
        public int Rating { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập nội dung đánh giá.")]
        [StringLength(1000, ErrorMessage = "Nội dung đánh giá không được vượt quá 1000 ký tự.")]
        [Display(Name = "Bình luận")]
        public string Comment { get; set; } = string.Empty;
    }
}
