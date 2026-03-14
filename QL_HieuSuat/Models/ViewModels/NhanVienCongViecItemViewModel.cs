using System;

namespace QL_HieuSuat.Models.ViewModels;

public class NhanVienCongViecItemViewModel
{
    public int MaCongViec { get; set; }

    public string? TenCongViec { get; set; }

    public string? TenDuAn { get; set; }

    public int Matrangthai { get; set; }

    public DateTime? HanHoanThanh { get; set; }

    public double TienDoPhanTram { get; set; }

    public bool LaCongViecCaNhan { get; set; }
}
