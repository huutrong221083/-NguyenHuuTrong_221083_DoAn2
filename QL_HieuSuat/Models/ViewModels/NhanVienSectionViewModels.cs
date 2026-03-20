using QL_HieuSuat.Models;

namespace QL_HieuSuat.Models.ViewModels;

public class NhanVienThongTinXepLoaiViewModel
{
    public Nhanvien HoSoNhanVien { get; set; } = null!;

    public string? KyDanhGiaGanNhat { get; set; }

    public double? DiemDanhGiaGanNhat { get; set; }

    public string XepLoaiGanNhat { get; set; } = "Chưa có dữ liệu";

    public List<NhanVienKpiChiTietViewModel> ChiTietDanhGiaGanNhat { get; set; } = new();
}

public class NhanVienKpiChiTietViewModel
{
    public string? TenDanhMuc { get; set; }

    public double? DiemSo { get; set; }

    public double? TrongSo { get; set; }
}

public class NhanVienDuAnTomTatViewModel
{
    public int MaDuAn { get; set; }

    public string? TenDuAn { get; set; }

    public DateTime? NgayBatDau { get; set; }

    public DateTime? NgayKetThuc { get; set; }

    public int TongCongViec { get; set; }

    public int DangThucHien { get; set; }

    public int ChoDuyet { get; set; }

    public int HoanThanh { get; set; }

    public int QuaHan { get; set; }

    public List<NhanVienCongViecItemViewModel> CongViecs { get; set; } = new();
}

public class NhanVienDuAnDangThamGiaViewModel
{
    public string? TenNhanVien { get; set; }

    public List<NhanVienDuAnTomTatViewModel> DuAnDangThamGia { get; set; } = new();
}

public class NhanVienDuAnDaThamGiaViewModel
{
    public string? TenNhanVien { get; set; }

    public List<NhanVienDuAnTomTatViewModel> DuAnDaThamGia { get; set; } = new();
}

public class NhanVienDanhGiaKyViewModel
{
    public int? Thang { get; set; }

    public int? Nam { get; set; }

    public double DiemTongHop { get; set; }

    public string XepLoai { get; set; } = "Chưa có dữ liệu";

    public int SoDanhMuc { get; set; }

    public string MaKy { get; set; } = string.Empty;
}

public class NhanVienDanhGiaCaNhanViewModel
{
    public string? TenNhanVien { get; set; }

    public List<NhanVienDanhGiaKyViewModel> LichSuDanhGia { get; set; } = new();

    public string? KyDangXem { get; set; }

    public double? DiemKyDangXem { get; set; }

    public string XepLoaiKyDangXem { get; set; } = "Chưa có dữ liệu";

    public List<NhanVienKpiChiTietViewModel> ChiTietKyDangXem { get; set; } = new();

    public List<string> BieuDoNhan { get; set; } = new();

    public List<double> BieuDoDiem { get; set; } = new();
}

public class NhanVienDonViQuanLyItemViewModel
{
    public string LoaiDonVi { get; set; } = string.Empty;

    public int MaDonVi { get; set; }

    public string TenDonVi { get; set; } = string.Empty;

    public int SoNhanVien { get; set; }

    public List<NhanVienDonViThanhVienViewModel> ThanhVien { get; set; } = new();
}

public class NhanVienDonViThanhVienViewModel
{
    public int MaNhanVien { get; set; }

    public string HoTen { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? SoDienThoai { get; set; }

    public string? TenPhongBan { get; set; }

    public string? VaiTroTrongDonVi { get; set; }
}

public class NhanVienThongTinDonViQuanLyViewModel
{
    public string? TenQuanLy { get; set; }

    public List<NhanVienDonViQuanLyItemViewModel> DonViPhuTrach { get; set; } = new();

    public int TongNhanVienQuanLy { get; set; }
}

public class NhanVienDanhGiaDonViItemViewModel
{
    public int MaNhanVien { get; set; }

    public string HoTen { get; set; } = string.Empty;

    public string? TenPhongBan { get; set; }

    public bool CoTrongPhongQuanLy { get; set; }

    public bool CoTrongNhomQuanLy { get; set; }

    public string DanhSachNhomQuanLy { get; set; } = string.Empty;

    public double? DiemTongHop { get; set; }

    public string XepLoai { get; set; } = "Chưa có dữ liệu";
}

public class NhanVienDanhGiaDonViQuanLyViewModel
{
    public string? TenQuanLy { get; set; }

    public string? KyDangXem { get; set; }

    public List<string> DanhSachKy { get; set; } = new();

    public List<NhanVienDanhGiaDonViItemViewModel> DanhGiaNhanVien { get; set; } = new();
}
