namespace QL_HieuSuat.Models.ViewModels;

public class QuanLyPhongBanDashboardViewModel
{
    public int MaPhongBan { get; set; }
    public string? TenPhongBan { get; set; }
    public string? MoTa { get; set; }
    public string? TenTruongPhong { get; set; }

    public int TongNhanVien { get; set; }
    public int TongCongViec { get; set; }
    public int CongViecDangThucHien { get; set; }
    public int CongViecChoDuyet { get; set; }

    public List<QuanLyPhongBanNhanVienItemViewModel> NhanViens { get; set; } = new();
    public List<QuanLyPhongBanCongViecItemViewModel> CongViecs { get; set; } = new();
}

public class QuanLyPhongBanNhanVienItemViewModel
{
    public int MaNhanVien { get; set; }
    public string? HoTen { get; set; }
    public string? Email { get; set; }
    public bool CoTaiKhoan { get; set; }
    public int SoCongViecDangLam { get; set; }
}

public class QuanLyPhongBanCongViecItemViewModel
{
    public int MaCongViec { get; set; }
    public string? TenCongViec { get; set; }
    public string? TenDuAn { get; set; }
    public int Matrangthai { get; set; }
    public int TienDo { get; set; }
    public DateTime? HanHoanThanh { get; set; }
}
