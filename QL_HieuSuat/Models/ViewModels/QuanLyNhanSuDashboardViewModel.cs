namespace QL_HieuSuat.Models.ViewModels;

public class QuanLyNhanSuDashboardViewModel
{
    public string? Keyword { get; set; }
    public string? AlphabetFilter { get; set; }
    public double? KinhNghiemTu { get; set; }
    public int? DoTuoi { get; set; }
    public int? PhongBanFilter { get; set; }
    public bool? CoTaiKhoanFilter { get; set; }

    public int TongNhanSu { get; set; }
    public int TongPhongBan { get; set; }
    public int TongNhom { get; set; }
    public int SoNhanSuCoTaiKhoan { get; set; }
    public int SoNhanSuChuaCoTaiKhoan { get; set; }

    public List<QuanLyNhanSuNhanVienItemViewModel> NhanViens { get; set; } = new();
}

public class QuanLyNhanSuNhanVienItemViewModel
{
    public int MaNhanVien { get; set; }
    public string? HoTen { get; set; }
    public string? EmailNhanVien { get; set; }
    public string? TenPhongBan { get; set; }
    public int SoNhomThamGia { get; set; }
    public bool CoTaiKhoan { get; set; }
    public string? EmailTaiKhoan { get; set; }
    public int CongViecDangThucHien { get; set; }
    public double KinhNghiem { get; set; }
    public int? Tuoi { get; set; }
}
