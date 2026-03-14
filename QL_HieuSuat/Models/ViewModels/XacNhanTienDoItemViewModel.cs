using System;
using System.Collections.Generic;

namespace QL_HieuSuat.Models.ViewModels;

public class XacNhanTienDoItemViewModel
{
    public int MaCongViec { get; set; }

    public string? TenCongViec { get; set; }

    public string? TenDuAn { get; set; }

    public DateTime? HanHoanThanh { get; set; }

    public double TienDoPhanTram { get; set; }

    public int? MucUuTien { get; set; }

    public List<string> NguoiNhan { get; set; } = new();
}
