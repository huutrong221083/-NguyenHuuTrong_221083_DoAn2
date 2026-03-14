using System.Collections.Generic;

namespace QL_HieuSuat.Models.ViewModels;

public class NhanVienDashboardViewModel
{
    public string? TenNhanVien { get; set; }

    public string? Email { get; set; }

    public int TongCongViec { get; set; }

    public int DangThucHien { get; set; }

    public int ChoDuyet { get; set; }

    public int HoanThanh { get; set; }

    public int QuaHan { get; set; }

    public int? TrangThaiFilter { get; set; }

    public string? HanFilter { get; set; }

    public List<NhanVienCongViecItemViewModel> CongViecGanNhat { get; set; } = new();

    public List<NhanVienCongViecItemViewModel> CongViecSapDenHan { get; set; } = new();

    public List<NhanVienThongBaoItemViewModel> ThongBaoLienQuan { get; set; } = new();
}
