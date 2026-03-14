using System.ComponentModel.DataAnnotations;

namespace QL_HieuSuat.Models.ViewModels;

public class TaoTaiKhoanViewModel
{
    [Required(ErrorMessage = "Email là bắt buộc")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu là bắt buộc")]
    [DataType(DataType.Password)]
    [MinLength(6, ErrorMessage = "Mật khẩu tối thiểu 6 ký tự")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Xác nhận mật khẩu là bắt buộc")]
    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "Mật khẩu xác nhận không khớp")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Display(Name = "Mã nhân viên")]
    public int? MaNhanVien { get; set; }

    [Required(ErrorMessage = "Vai trò là bắt buộc")]
    public string Role { get; set; } = "NhanVien";
}
