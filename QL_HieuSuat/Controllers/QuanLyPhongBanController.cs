using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_HieuSuat.Models;
using QL_HieuSuat.Models.ViewModels;

namespace QL_HieuSuat.Controllers;

[Authorize(Roles = "TruongPhong,Admin")]
public class QuanLyPhongBanController : Controller
{
    private readonly ApplicationDbContext _context;

    public QuanLyPhongBanController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var currentUserName = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(currentUserName))
        {
            return View(new QuanLyPhongBanDashboardViewModel());
        }

        var currentNhanVienId = await _context.Users
            .Where(u => u.UserName == currentUserName || u.Email == currentUserName)
            .Select(u => u.MaNhanVien)
            .FirstOrDefaultAsync();

        if (!currentNhanVienId.HasValue)
        {
            var nv = await _context.Nhanviens.AsNoTracking().FirstOrDefaultAsync(x => x.Email == currentUserName);
            currentNhanVienId = nv?.Manhanvien;
        }

        if (!currentNhanVienId.HasValue)
        {
            return View(new QuanLyPhongBanDashboardViewModel());
        }

        var phongBan = await _context.Phongbans
            .Include(pb => pb.MatruongphongNavigation)
            .Include(pb => pb.Nhanviens)
            .FirstOrDefaultAsync(pb => pb.Matruongphong == currentNhanVienId.Value);

        if (phongBan == null)
        {
            return View(new QuanLyPhongBanDashboardViewModel());
        }

        var employeeIds = phongBan.Nhanviens.Select(nv => nv.Manhanvien).ToList();

        var accountIds = await _context.Users
            .Where(u => u.MaNhanVien.HasValue && employeeIds.Contains(u.MaNhanVien.Value))
            .Select(u => u.MaNhanVien!.Value)
            .Distinct()
            .ToListAsync();

        var taskIds = await _context.Phancongcongviecs
            .Where(pc =>
                (pc.Maphongban.HasValue && pc.Maphongban.Value == phongBan.Maphongban) ||
                (pc.Manhanvien.HasValue && employeeIds.Contains(pc.Manhanvien.Value)))
            .Select(pc => pc.Macongviec)
            .Distinct()
            .ToListAsync();

        var tasks = await _context.Congviecs
            .Where(cv => taskIds.Contains(cv.Macongviec))
            .Include(cv => cv.MaduanNavigation)
            .Include(cv => cv.Nhatkycongviecs)
            .AsSplitQuery()
            .ToListAsync();

        var taskCountByEmployee = await _context.Phancongcongviecs
            .Where(pc => pc.Manhanvien.HasValue && employeeIds.Contains(pc.Manhanvien.Value))
            .Join(_context.Congviecs.Where(cv => cv.Matrangthai != 3),
                pc => pc.Macongviec,
                cv => cv.Macongviec,
                (pc, cv) => pc.Manhanvien)
            .Where(id => id.HasValue)
            .GroupBy(id => id!.Value)
            .Select(g => new { MaNhanVien = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.MaNhanVien, x => x.Count);

        var model = new QuanLyPhongBanDashboardViewModel
        {
            MaPhongBan = phongBan.Maphongban,
            TenPhongBan = phongBan.Tenphongban,
            MoTa = phongBan.Mota,
            TenTruongPhong = phongBan.MatruongphongNavigation?.Hoten,
            TongNhanVien = phongBan.Nhanviens.Count,
            TongCongViec = tasks.Count,
            CongViecDangThucHien = tasks.Count(t => t.Matrangthai == 2),
            CongViecChoDuyet = tasks.Count(t => t.Matrangthai == 4),
            CongViecHoanThanh = tasks.Count(t => t.Matrangthai == 3),
            NhanViens = phongBan.Nhanviens
                .OrderBy(nv => nv.Hoten)
                .Select(nv => new QuanLyPhongBanNhanVienItemViewModel
                {
                    MaNhanVien = nv.Manhanvien,
                    HoTen = nv.Hoten,
                    Email = nv.Email,
                    CoTaiKhoan = accountIds.Contains(nv.Manhanvien),
                    SoCongViecDangLam = taskCountByEmployee.GetValueOrDefault(nv.Manhanvien, 0)
                })
                .ToList(),
            CongViecs = tasks
                .Where(t => t.Matrangthai != 3)
                .OrderBy(t => t.Hanhoanthanh ?? DateTime.MaxValue)
                .ThenBy(t => t.Macongviec)
                .Select(t => new QuanLyPhongBanCongViecItemViewModel
                {
                    MaCongViec = t.Macongviec,
                    TenCongViec = t.Tencongviec,
                    TenDuAn = t.MaduanNavigation?.Tenduan,
                    Matrangthai = t.Matrangthai,
                    TienDo = (int)Math.Round(t.Nhatkycongviecs.OrderByDescending(nk => nk.Ngaycapnhat).FirstOrDefault()?.Phantramhoanthanh ?? 0),
                    HanHoanThanh = t.Hanhoanthanh
                })
                .ToList(),
            CongViecHoanThanhList = tasks
                .Where(t => t.Matrangthai == 3)
                .OrderByDescending(t => t.Hanhoanthanh ?? DateTime.MinValue)
                .ThenByDescending(t => t.Macongviec)
                .Select(t => new QuanLyPhongBanCongViecItemViewModel
                {
                    MaCongViec = t.Macongviec,
                    TenCongViec = t.Tencongviec,
                    TenDuAn = t.MaduanNavigation?.Tenduan,
                    Matrangthai = t.Matrangthai,
                    TienDo = (int)Math.Round(t.Nhatkycongviecs.OrderByDescending(nk => nk.Ngaycapnhat).FirstOrDefault()?.Phantramhoanthanh ?? 0),
                    HanHoanThanh = t.Hanhoanthanh
                })
                .ToList()
        };

        return View(model);
    }
}
