using System;

namespace QL_HieuSuat.Models.ViewModels;

public class NhanVienThongBaoItemViewModel
{
    public string TieuDe { get; set; } = string.Empty;

    public string NoiDung { get; set; } = string.Empty;

    public DateTime ThoiGian { get; set; }

    public int? MaCongViec { get; set; }
}
