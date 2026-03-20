using System;
using System.Collections.Generic;

namespace QL_HieuSuat.Models.ViewModels;

public class QuanLyNhanSuThongTinXepLoaiViewModel
{
    public Nhanvien? HoSoNhanVien { get; set; }
    public string? KyDanhGiaGanNhat { get; set; }
    public double? DiemDanhGiaGanNhat { get; set; }
    public string XepLoaiGanNhat { get; set; } = "Chưa có dữ liệu";
    public List<QuanLyNhanSuKpiChiTietViewModel> ChiTietDanhGiaGanNhat { get; set; } = new();
}

public class QuanLyNhanSuDanhGiaCaNhanViewModel
{
    public string? TenNhanVien { get; set; }
    public string? KyDangXem { get; set; }
    public double? DiemKyDangXem { get; set; }
    public string XepLoaiKyDangXem { get; set; } = "Chưa có dữ liệu";
    public List<QuanLyNhanSuDanhGiaKyViewModel> LichSuDanhGia { get; set; } = new();
    public List<QuanLyNhanSuKpiChiTietViewModel> ChiTietKyDangXem { get; set; } = new();
    public List<string> BieuDoNhan { get; set; } = new();
    public List<double> BieuDoDiem { get; set; } = new();
}

public class QuanLyNhanSuDanhGiaKyViewModel
{
    public int? Thang { get; set; }
    public int? Nam { get; set; }
    public string MaKy { get; set; } = string.Empty;
    public double DiemTongHop { get; set; }
    public string XepLoai { get; set; } = string.Empty;
    public int SoDanhMuc { get; set; }
}

public class QuanLyNhanSuKpiChiTietViewModel
{
    public string? TenDanhMuc { get; set; }
    public double DiemSo { get; set; }
    public double? TrongSo { get; set; }
}

public class QuanLyNhanSuYeuCauChinhSuaViewModel
{
    public int TongYeuCau { get; set; }
    public List<QuanLyNhanSuYeuCauChinhSuaItemViewModel> YeuCaus { get; set; } = new();
}

public class QuanLyNhanSuYeuCauChinhSuaItemViewModel
{
    public int MaYeuCau { get; set; }
    public int? MaNhanVien { get; set; }
    public string? TenNhanVien { get; set; }
    public string? EmailNhanVien { get; set; }
    public string? NoiDungYeuCau { get; set; }
    public DateTime? ThoiGianGui { get; set; }
}
