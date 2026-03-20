using System.ComponentModel.DataAnnotations;

namespace QL_HieuSuat.Models.ViewModels;

public class VerifyResetCodeViewModel
{
    [Required(ErrorMessage = "Email là bắt buộc")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mã xác nhận là bắt buộc")]
    [RegularExpression("^[0-9]{6}$", ErrorMessage = "Mã xác nhận phải gồm 6 chữ số")]
    public string VerificationCode { get; set; } = string.Empty;
}
