using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_HieuSuat.Models;
using QL_HieuSuat.Models.ViewModels;

namespace QL_HieuSuat.Controllers
{
    [Authorize(Roles = "NhanVien,TruongPhong,TruongNhom,QuanLyNhanSu,QuanLyDuAn")]
    public class NhanVienController : Controller
    {
        private readonly ApplicationDbContext _context;

        public NhanVienController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int? trangThaiFilter, string? hanFilter)
        {
            var model = new NhanVienDashboardViewModel
            {
                TrangThaiFilter = trangThaiFilter,
                HanFilter = hanFilter
            };

            var currentUserName = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(currentUserName))
            {
                return View(model);
            }

            var appUser = await _context.Users
                .Include(u => u.Nhanvien)
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

            if (!nhanVienId.HasValue)
            {
                return View(model);
            }

            var groupIds = await _context.Thanhviennhoms
                .Where(tv => tv.Manhanvien == nhanVienId.Value)
                .Select(tv => tv.Manhom)
                .ToListAsync();

            var departmentId = nhanVien?.Maphongban;

            var assignedTaskIds = await _context.Phancongcongviecs
                .Where(pc =>
                    (pc.Manhanvien.HasValue && pc.Manhanvien.Value == nhanVienId.Value) ||
                    (pc.Manhom.HasValue && groupIds.Contains(pc.Manhom.Value)) ||
                    (departmentId.HasValue && pc.Maphongban.HasValue && pc.Maphongban.Value == departmentId.Value))
                .Select(pc => pc.Macongviec)
                .Distinct()
                .ToListAsync();

            var tasks = await _context.Congviecs
                .Where(cv => assignedTaskIds.Contains(cv.Macongviec))
                .Include(cv => cv.MaduanNavigation)
                .Include(cv => cv.Nhatkycongviecs)
                .Include(cv => cv.Phancongcongviecs)
                .AsSplitQuery()
                .ToListAsync();

            model.TenNhanVien = nhanVien?.Hoten;
            model.Email = appUser?.Email ?? nhanVien?.Email;
            model.TongCongViec = tasks.Count;
            model.DangThucHien = tasks.Count(t => t.Matrangthai == 2);
            model.ChoDuyet = tasks.Count(t => t.Matrangthai == 4);
            model.HoanThanh = tasks.Count(t => t.Matrangthai == 3);
            model.QuaHan = tasks.Count(t => t.Hanhoanthanh.HasValue && t.Hanhoanthanh.Value.Date < DateTime.Today && t.Matrangthai != 3);

            var mappedTasks = tasks
                .OrderBy(t => t.Hanhoanthanh ?? DateTime.MaxValue)
                .ThenBy(t => t.Macongviec)
                .Select(t => new NhanVienCongViecItemViewModel
                {
                    MaCongViec = t.Macongviec,
                    TenCongViec = t.Tencongviec,
                    TenDuAn = t.MaduanNavigation?.Tenduan,
                    Matrangthai = t.Matrangthai,
                    HanHoanThanh = t.Hanhoanthanh,
                    LaCongViecCaNhan = t.Phancongcongviecs.Any() &&
                        t.Phancongcongviecs.All(pc => pc.Loaidoituong == "NhanVien") &&
                        t.Phancongcongviecs.Select(pc => pc.Manhanvien).Distinct().Count() == 1,
                    TienDoPhanTram = t.Nhatkycongviecs
                        .OrderByDescending(nk => nk.Ngaycapnhat)
                        .FirstOrDefault()?.Phantramhoanthanh ?? 0
                })
                .ToList();

            var filteredTasks = mappedTasks.AsQueryable();

            if (trangThaiFilter.HasValue)
            {
                filteredTasks = filteredTasks.Where(t => t.Matrangthai == trangThaiFilter.Value);
            }

            if (!string.IsNullOrWhiteSpace(hanFilter))
            {
                var today = DateTime.Today;

                filteredTasks = hanFilter switch
                {
                    "homnay" => filteredTasks.Where(t => t.HanHoanThanh.HasValue && t.HanHoanThanh.Value.Date == today),
                    "3ngay" => filteredTasks.Where(t => t.HanHoanThanh.HasValue && t.HanHoanThanh.Value.Date >= today && t.HanHoanThanh.Value.Date <= today.AddDays(3)),
                    "quahan" => filteredTasks.Where(t => t.HanHoanThanh.HasValue && t.HanHoanThanh.Value.Date < today && t.Matrangthai != 3),
                    _ => filteredTasks
                };
            }

            model.CongViecGanNhat = filteredTasks
                .Take(20)
                .ToList();

            model.CongViecSapDenHan = mappedTasks
                .Where(t => t.HanHoanThanh.HasValue
                    && t.HanHoanThanh.Value.Date >= DateTime.Today
                    && t.HanHoanThanh.Value.Date <= DateTime.Today.AddDays(3)
                    && t.Matrangthai != 3)
                .OrderBy(t => t.HanHoanThanh)
                .Take(5)
                .ToList();

            var latestComments = await _context.Binhluans
                .Where(b => assignedTaskIds.Contains(b.Macongviec))
                .Include(b => b.MacongviecNavigation)
                .OrderByDescending(b => b.Ngaytao)
                .Take(5)
                .ToListAsync();

            var thongBao = new List<NhanVienThongBaoItemViewModel>();

            thongBao.AddRange(model.CongViecSapDenHan.Select(cv => new NhanVienThongBaoItemViewModel
            {
                TieuDe = "Sắp đến hạn",
                NoiDung = $"{cv.TenCongViec} sắp đến hạn vào {cv.HanHoanThanh:dd/MM/yyyy}.",
                ThoiGian = DateTime.Now,
                MaCongViec = cv.MaCongViec
            }));

            thongBao.AddRange(latestComments.Select(c => new NhanVienThongBaoItemViewModel
            {
                TieuDe = "Bình luận mới",
                NoiDung = $"Công việc {(c.MacongviecNavigation?.Tencongviec ?? c.Macongviec.ToString())} có cập nhật trao đổi.",
                ThoiGian = c.Ngaytao,
                MaCongViec = c.Macongviec
            }));

            model.ThongBaoLienQuan = thongBao
                .OrderByDescending(t => t.ThoiGian)
                .Take(8)
                .ToList();

            return View(model);
        }
    }
}
