using System.ComponentModel.DataAnnotations;

namespace SaigonChe.Web.ViewModels
{
    public class StaffAccountsViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập email nhân viên.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng đặt mật khẩu.")]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Mật khẩu phải có ít nhất 8 ký tự.")]
        public string Password { get; set; } = string.Empty;

        public List<StaffAccountItemViewModel> Staff { get; set; } = new();
    }

    public class StaffAccountItemViewModel
    {
        public string Id { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public bool HasActiveOrders { get; set; }
    }
}
