using System.ComponentModel.DataAnnotations;

namespace QL_HieuSuat.Models.ViewModels;

public class SuaTaiKhoanViewModel
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email là bắt buộc")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Mã nhân viên")]
    public int? MaNhanVien { get; set; }

    [Required(ErrorMessage = "Vai trò là bắt buộc")]
    public string Role { get; set; } = "NhanVien";
}
