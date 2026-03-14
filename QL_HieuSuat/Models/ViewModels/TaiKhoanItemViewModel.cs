namespace QL_HieuSuat.Models.ViewModels;

public class TaiKhoanItemViewModel
{
    public string UserId { get; set; } = string.Empty;

    public string? UserName { get; set; }

    public string? Email { get; set; }

    public string VaiTro { get; set; } = string.Empty;

    public int? MaNhanVien { get; set; }

    public string? TenNhanVien { get; set; }
}
