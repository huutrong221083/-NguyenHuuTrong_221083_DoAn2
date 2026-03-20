using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text;
using QL_HieuSuat.Models;
using QL_HieuSuat.Models.ViewModels;

namespace QL_HieuSuat.Controllers;

[Authorize(Roles = "QuanLyNhanSu,Admin")]
public class QuanLyNhanSuController : Controller
{
    private readonly ApplicationDbContext _context;

    public QuanLyNhanSuController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> ThongTinVaXepLoai()
    {
        var context = await GetCurrentNhanVienContextAsync();
        if (!context.NhanVienId.HasValue || context.NhanVien == null)
        {
            TempData["UploadError"] = "Không tìm thấy hồ sơ nhân sự hiện tại.";
            return RedirectToAction(nameof(Index));
        }

        var nhanVien = await _context.Nhanviens
            .AsNoTracking()
            .Include(nv => nv.MaphongbanNavigation)
            .Include(nv => nv.Thanhviennhoms)
                .ThenInclude(tv => tv.ManhomNavigation)
            .Include(nv => nv.Kynangnhanviens)
                .ThenInclude(kn => kn.MakynangNavigation)
            .FirstOrDefaultAsync(nv => nv.Manhanvien == context.NhanVienId.Value);

        if (nhanVien == null)
        {
            TempData["UploadError"] = "Không tìm thấy hồ sơ nhân sự hiện tại.";
            return RedirectToAction(nameof(Index));
        }

        var groupedKpis = await GetGroupedKpiByPeriodAsync(context.NhanVienId.Value);
        var latestGroup = groupedKpis
            .OrderByDescending(g => g.Nam ?? 0)
            .ThenByDescending(g => g.Thang ?? 0)
            .FirstOrDefault();

        var latestScore = latestGroup is null ? null : CalculateWeightedScore(latestGroup.Items);

        var model = new QuanLyNhanSuThongTinXepLoaiViewModel
        {
            HoSoNhanVien = nhanVien,
            KyDanhGiaGanNhat = latestGroup is null ? null : BuildPeriodLabel(latestGroup.Thang, latestGroup.Nam),
            DiemDanhGiaGanNhat = latestScore,
            XepLoaiGanNhat = latestScore.HasValue ? XepLoaiFromScore(latestScore.Value) : "Chưa có dữ liệu",
            ChiTietDanhGiaGanNhat = latestGroup?.Items
                .OrderByDescending(x => x.MadoanhmucNavigation.Trongso ?? 0)
                .ThenBy(x => x.MadoanhmucNavigation.Tendoanhmuc)
                .Select(x => new QuanLyNhanSuKpiChiTietViewModel
                {
                    TenDanhMuc = x.MadoanhmucNavigation.Tendoanhmuc,
                    DiemSo = NormalizeScore(x.Diemso),
                    TrongSo = x.MadoanhmucNavigation.Trongso
                })
                .ToList() ?? new List<QuanLyNhanSuKpiChiTietViewModel>()
        };

        return View(model);
    }

    public async Task<IActionResult> Index(
        string? keyword,
        string? alphabetFilter,
        double? kinhNghiemTu,
        int? doTuoi,
        int? phongBanFilter,
        bool? coTaiKhoanFilter)
    {
        var items = await BuildEmployeeItemsAsync(
            keyword,
            alphabetFilter,
            kinhNghiemTu,
            doTuoi,
            phongBanFilter,
            coTaiKhoanFilter);
        var tongNhanSu = await _context.Nhanviens.CountAsync();
        var soNhanSuCoTaiKhoan = items.Count(i => i.CoTaiKhoan);

        var model = new QuanLyNhanSuDashboardViewModel
        {
            Keyword = keyword,
            AlphabetFilter = alphabetFilter,
            KinhNghiemTu = kinhNghiemTu,
            DoTuoi = doTuoi,
            PhongBanFilter = phongBanFilter,
            CoTaiKhoanFilter = coTaiKhoanFilter,
            TongNhanSu = tongNhanSu,
            TongPhongBan = await _context.Phongbans.CountAsync(),
            TongNhom = await _context.Nhoms.CountAsync(),
            SoNhanSuCoTaiKhoan = soNhanSuCoTaiKhoan,
            SoNhanSuChuaCoTaiKhoan = tongNhanSu - soNhanSuCoTaiKhoan,
            NhanViens = items
        };

        var phongBanOptions = await _context.Phongbans
            .OrderBy(pb => pb.Tenphongban)
            .Select(pb => new SelectListItem
            {
                Value = pb.Maphongban.ToString(),
                Text = pb.Tenphongban
            })
            .ToListAsync();

        ViewBag.PhongBanOptions = phongBanOptions;

        return View(model);
    }

    public async Task<IActionResult> ExportCsv(
        string? keyword,
        string? alphabetFilter,
        double? kinhNghiemTu,
        int? doTuoi,
        int? phongBanFilter,
        bool? coTaiKhoanFilter)
    {
        var items = await BuildEmployeeItemsAsync(
            keyword,
            alphabetFilter,
            kinhNghiemTu,
            doTuoi,
            phongBanFilter,
            coTaiKhoanFilter);

        var sb = new StringBuilder();
        sb.AppendLine("MaNhanVien,HoTen,Tuoi,KinhNghiemNam,EmailNhanSu,PhongBan,SoNhomThamGia,CoTaiKhoan,EmailTaiKhoan,CongViecDangThucHien");

        foreach (var i in items)
        {
            sb.AppendLine(string.Join(",",
                Csv(i.MaNhanVien.ToString()),
                Csv(i.HoTen),
                Csv(i.Tuoi?.ToString()),
                Csv(i.KinhNghiem.ToString("0.##")),
                Csv(i.EmailNhanVien),
                Csv(i.TenPhongBan),
                Csv(i.SoNhomThamGia.ToString()),
                Csv(i.CoTaiKhoan ? "Co" : "Chua"),
                Csv(i.EmailTaiKhoan),
                Csv(i.CongViecDangThucHien.ToString())));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        var fileName = $"NhanSu_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

        return File(bytes, "text/csv; charset=utf-8", fileName);
    }

    public async Task<IActionResult> DanhGiaCaNhan(string? ky)
    {
        var context = await GetCurrentNhanVienContextAsync();
        if (!context.NhanVienId.HasValue)
        {
            TempData["UploadError"] = "Không tìm thấy hồ sơ nhân sự hiện tại.";
            return RedirectToAction(nameof(Index));
        }

        var groupedKpis = await GetGroupedKpiByPeriodAsync(context.NhanVienId.Value);

        var history = groupedKpis
            .OrderByDescending(g => g.Nam ?? 0)
            .ThenByDescending(g => g.Thang ?? 0)
            .Select(g =>
            {
                var score = CalculateWeightedScore(g.Items) ?? 0;
                return new QuanLyNhanSuDanhGiaKyViewModel
                {
                    Thang = g.Thang,
                    Nam = g.Nam,
                    DiemTongHop = Math.Round(score, 2),
                    XepLoai = XepLoaiFromScore(score),
                    SoDanhMuc = g.Items.Count,
                    MaKy = BuildPeriodCode(g.Thang, g.Nam)
                };
            })
            .ToList();

        var selectedPeriod = history.FirstOrDefault(h => h.MaKy == ky)?.MaKy ?? history.FirstOrDefault()?.MaKy;
        var selectedGroup = groupedKpis.FirstOrDefault(g => BuildPeriodCode(g.Thang, g.Nam) == selectedPeriod);
        var selectedScore = selectedGroup is null ? null : CalculateWeightedScore(selectedGroup.Items);

        var model = new QuanLyNhanSuDanhGiaCaNhanViewModel
        {
            TenNhanVien = context.NhanVien?.Hoten,
            LichSuDanhGia = history,
            KyDangXem = selectedGroup is null ? null : BuildPeriodLabel(selectedGroup.Thang, selectedGroup.Nam),
            DiemKyDangXem = selectedScore,
            XepLoaiKyDangXem = selectedScore.HasValue ? XepLoaiFromScore(selectedScore.Value) : "Chưa có dữ liệu",
            ChiTietKyDangXem = selectedGroup?.Items
                .OrderByDescending(x => x.MadoanhmucNavigation.Trongso ?? 0)
                .ThenBy(x => x.MadoanhmucNavigation.Tendoanhmuc)
                .Select(x => new QuanLyNhanSuKpiChiTietViewModel
                {
                    TenDanhMuc = x.MadoanhmucNavigation.Tendoanhmuc,
                    DiemSo = NormalizeScore(x.Diemso),
                    TrongSo = x.MadoanhmucNavigation.Trongso
                })
                .ToList() ?? new List<QuanLyNhanSuKpiChiTietViewModel>(),
            BieuDoNhan = history
                .OrderBy(h => h.Nam ?? 0)
                .ThenBy(h => h.Thang ?? 0)
                .Select(h => BuildPeriodLabel(h.Thang, h.Nam))
                .ToList(),
            BieuDoDiem = history
                .OrderBy(h => h.Nam ?? 0)
                .ThenBy(h => h.Thang ?? 0)
                .Select(h => h.DiemTongHop)
                .ToList()
        };

        return View(model);
    }

    public async Task<IActionResult> YeuCauChinhSuaThongTin()
    {
        var keywords = new[]
        {
            "yêu cầu chỉnh sửa",
            "yeu cau chinh sua",
            "cập nhật thông tin",
            "cap nhat thong tin",
            "chỉnh sửa hồ sơ",
            "chinh sua ho so"
        };

        var logs = await _context.Nhatkyhoatdongs
            .AsNoTracking()
            .Include(nk => nk.ManhanvienNavigation)
            .OrderByDescending(nk => nk.Thoigian)
            .Take(300)
            .ToListAsync();

        var requests = logs
            .Where(nk => !string.IsNullOrWhiteSpace(nk.Hanhdong)
                && keywords.Any(k => nk.Hanhdong!.Contains(k, StringComparison.OrdinalIgnoreCase)))
            .Select(nk => new QuanLyNhanSuYeuCauChinhSuaItemViewModel
            {
                MaYeuCau = nk.Manhatkyhoatdong,
                MaNhanVien = nk.Manhanvien,
                TenNhanVien = nk.ManhanvienNavigation?.Hoten,
                EmailNhanVien = nk.ManhanvienNavigation?.Email,
                NoiDungYeuCau = nk.Hanhdong,
                ThoiGianGui = nk.Thoigian
            })
            .ToList();

        var model = new QuanLyNhanSuYeuCauChinhSuaViewModel
        {
            TongYeuCau = requests.Count,
            YeuCaus = requests
        };

        return View(model);
    }

    private async Task<List<QuanLyNhanSuNhanVienItemViewModel>> BuildEmployeeItemsAsync(
        string? keyword,
        string? alphabetFilter,
        double? kinhNghiemTu,
        int? doTuoi,
        int? phongBanFilter,
        bool? coTaiKhoanFilter)
    {
        var nhanVienQuery = _context.Nhanviens
            .Include(nv => nv.MaphongbanNavigation)
            .Include(nv => nv.Thanhviennhoms)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLower();
            nhanVienQuery = nhanVienQuery.Where(nv =>
                (nv.Hoten != null && nv.Hoten.ToLower().Contains(kw)) ||
                (nv.Email != null && nv.Email.ToLower().Contains(kw)) ||
                (nv.Sdt != null && nv.Sdt.Contains(kw)));
        }

        if (!string.IsNullOrWhiteSpace(alphabetFilter))
        {
            var letter = alphabetFilter.Trim().ToLower();
            nhanVienQuery = nhanVienQuery.Where(nv =>
                nv.Hoten != null && nv.Hoten.ToLower().StartsWith(letter));
        }

        if (kinhNghiemTu.HasValue)
        {
            nhanVienQuery = nhanVienQuery.Where(nv => (nv.Sonamkinhnghiem ?? 0) >= kinhNghiemTu.Value);
        }

        if (phongBanFilter.HasValue)
        {
            nhanVienQuery = nhanVienQuery.Where(nv => nv.Maphongban == phongBanFilter.Value);
        }

        var nhanViens = await nhanVienQuery.ToListAsync();

        if (doTuoi.HasValue)
        {
            nhanViens = nhanViens
                .Where(nv =>
                {
                    var tuoi = CalculateAge(nv.Ngaysinh);
                    return tuoi.HasValue && tuoi.Value == doTuoi.Value;
                })
                .ToList();
        }

        var accountByNhanVienId = await _context.Users
            .Where(u => u.MaNhanVien.HasValue)
            .Select(u => new { u.MaNhanVien, u.Email })
            .ToDictionaryAsync(u => u.MaNhanVien!.Value, u => u.Email);

        var activeTaskCountByNhanVien = await _context.Phancongcongviecs
            .Where(pc => pc.Manhanvien.HasValue)
            .Join(_context.Congviecs.Where(cv => cv.Matrangthai != 3),
                pc => pc.Macongviec,
                cv => cv.Macongviec,
                (pc, cv) => pc.Manhanvien)
            .Where(id => id.HasValue)
            .GroupBy(id => id!.Value)
            .Select(g => new { MaNhanVien = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.MaNhanVien, x => x.Count);

        var items = nhanViens.Select(nv =>
        {
            var hasAccount = accountByNhanVienId.ContainsKey(nv.Manhanvien);
            return new QuanLyNhanSuNhanVienItemViewModel
            {
                MaNhanVien = nv.Manhanvien,
                HoTen = nv.Hoten,
                EmailNhanVien = nv.Email,
                TenPhongBan = nv.MaphongbanNavigation?.Tenphongban,
                SoNhomThamGia = nv.Thanhviennhoms.Select(tv => tv.Manhom).Distinct().Count(),
                CoTaiKhoan = hasAccount,
                EmailTaiKhoan = hasAccount ? accountByNhanVienId[nv.Manhanvien] : null,
                CongViecDangThucHien = activeTaskCountByNhanVien.GetValueOrDefault(nv.Manhanvien, 0),
                KinhNghiem = nv.Sonamkinhnghiem ?? 0,
                Tuoi = CalculateAge(nv.Ngaysinh)
            };
        }).ToList();

        if (coTaiKhoanFilter.HasValue)
        {
            items = items.Where(i => i.CoTaiKhoan == coTaiKhoanFilter.Value).ToList();
        }

        if (!string.IsNullOrWhiteSpace(alphabetFilter))
        {
            items = items.OrderBy(i => i.HoTen).ThenBy(i => i.MaNhanVien).ToList();
        }
        else
        {
            items = items.OrderBy(i => i.MaNhanVien).ToList();
        }

        return items;
    }

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static int? CalculateAge(DateTime? birthDate)
    {
        if (!birthDate.HasValue)
        {
            return null;
        }

        var today = DateTime.Today;
        var age = today.Year - birthDate.Value.Year;
        if (birthDate.Value.Date > today.AddYears(-age))
        {
            age--;
        }

        return age < 0 ? 0 : age;
    }

    private sealed class KpiPeriodGroup
    {
        public int? Thang { get; set; }
        public int? Nam { get; set; }
        public List<Ketquakpi> Items { get; set; } = new();
    }

    private async Task<(Nhanvien? NhanVien, int? NhanVienId)> GetCurrentNhanVienContextAsync()
    {
        var currentUserName = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(currentUserName))
        {
            return (null, null);
        }

        var appUser = await _context.Users
            .Include(u => u.Nhanvien)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserName == currentUserName || u.Email == currentUserName);

        var nhanVienId = appUser?.MaNhanVien ?? appUser?.Nhanvien?.Manhanvien;
        var nhanVien = appUser?.Nhanvien;

        if (!nhanVienId.HasValue)
        {
            nhanVien = await _context.Nhanviens
                .AsNoTracking()
                .FirstOrDefaultAsync(nv => nv.Email == currentUserName);

            nhanVienId = nhanVien?.Manhanvien;
        }

        return (nhanVien, nhanVienId);
    }

    private async Task<List<KpiPeriodGroup>> GetGroupedKpiByPeriodAsync(int nhanVienId)
    {
        var kpiData = await _context.Ketquakpis
            .Where(k => k.Manhanvien == nhanVienId)
            .Include(k => k.MadoanhmucNavigation)
            .AsNoTracking()
            .ToListAsync();

        return kpiData
            .GroupBy(k => new { k.Thang, k.Nam })
            .Select(g => new KpiPeriodGroup
            {
                Thang = g.Key.Thang,
                Nam = g.Key.Nam,
                Items = g.ToList()
            })
            .ToList();
    }

    private static double NormalizeScore(double? rawScore)
    {
        var score = rawScore ?? 0;
        return score <= 10 ? score * 10 : score;
    }

    private static double? CalculateWeightedScore(List<Ketquakpi> kpis)
    {
        if (!kpis.Any())
        {
            return null;
        }

        var totalWeight = kpis.Sum(k => k.MadoanhmucNavigation.Trongso ?? 1);
        if (totalWeight <= 0)
        {
            return Math.Round(kpis.Average(k => NormalizeScore(k.Diemso)), 2);
        }

        var totalScore = kpis.Sum(k => NormalizeScore(k.Diemso) * (k.MadoanhmucNavigation.Trongso ?? 1));
        return Math.Round(totalScore / totalWeight, 2);
    }

    private static string BuildPeriodCode(int? thang, int? nam)
    {
        return $"{nam ?? 0:D4}-{thang ?? 0:D2}";
    }

    private static string BuildPeriodLabel(int? thang, int? nam)
    {
        if (thang.HasValue && nam.HasValue)
        {
            return $"Tháng {thang.Value:D2}/{nam.Value}";
        }

        if (nam.HasValue)
        {
            return $"Năm {nam.Value}";
        }

        return "Không xác định kỳ";
    }

    private static string XepLoaiFromScore(double score)
    {
        if (score >= 90) return "Xuất sắc";
        if (score >= 80) return "Tốt";
        if (score >= 65) return "Khá";
        if (score >= 50) return "Trung bình";
        return "Cần cải thiện";
    }

}
