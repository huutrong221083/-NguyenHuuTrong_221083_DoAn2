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

}
