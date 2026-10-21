using System.ComponentModel.DataAnnotations;

namespace SaigonChe.Web.ViewModels
{
    public class ContactFormViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
        [StringLength(100, ErrorMessage = "Họ tên không được vượt quá 100 ký tự.")]
        [Display(Name = "Họ tên")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại hoặc email.")]
        [StringLength(200, ErrorMessage = "Thông tin liên hệ không được vượt quá 200 ký tự.")]
        [Display(Name = "Số điện thoại / Email")]
        public string Contact { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập nội dung cần hỗ trợ.")]
        [StringLength(2000, ErrorMessage = "Nội dung không được vượt quá 2000 ký tự.")]
        [Display(Name = "Nội dung")]
        [MinLength(10, ErrorMessage = "Nội dung phải có ít nhất 10 ký tự.")]
        public string Message { get; set; } = string.Empty;
    }
}
