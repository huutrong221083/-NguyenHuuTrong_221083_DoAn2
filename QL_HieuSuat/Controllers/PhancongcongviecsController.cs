using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QL_HieuSuat.Models;

namespace QL_HieuSuat.Controllers
{
    [Authorize(Roles = "Admin,TruongPhong,TruongNhom")]
    public class PhancongcongviecsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PhancongcongviecsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Phancongcongviecs
        public async Task<IActionResult> Index()
        {
            if (!User.IsInRole("Admin"))
            {
                return Forbid();
            }

            var applicationDbContext = _context.Phancongcongviecs.Include(p => p.MacongviecNavigation).Include(p => p.ManhanvienNavigation).Include(p => p.ManhomNavigation).Include(p => p.MaphongbanNavigation);
            return View(await applicationDbContext.ToListAsync());
        }

        // GET: Phancongcongviecs/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (!User.IsInRole("Admin"))
            {
                return Forbid();
            }

            var pc = await _context.Phancongcongviecs
                .Include(p => p.MacongviecNavigation)
                .Include(p => p.ManhanvienNavigation)
                .Include(p => p.ManhomNavigation)
                .Include(p => p.MaphongbanNavigation)
                .FirstOrDefaultAsync(m => m.Maphancong == id);

            return View(pc);
        }

        // GET: Phancongcongviecs/Create
        public async Task<IActionResult> Create(int macongviec)
        {
            var scope = await GetAssignmentScopeAsync();

            // Lấy công việc để biết nó thuộc dự án nào
            var congviec = _context.Congviecs
                .Include(c => c.MaduanNavigation)
                .FirstOrDefault(c => c.Macongviec == macongviec);

            if (congviec == null)
            {
                return NotFound();
            }

            // truyền sang View
            ViewBag.MaCongViec = macongviec;
            ViewBag.MaDuAn = congviec.Maduan;

            // danh sách nhân viên + công việc đang làm
            var nhanviensQuery = _context.Nhanviens
                .Include(nv => nv.Phancongcongviecs)
                .ThenInclude(pc => pc.MacongviecNavigation)
                .ThenInclude(cv => cv.MaduanNavigation)
                .AsQueryable();

            if (!scope.IsAdmin)
            {
                nhanviensQuery = nhanviensQuery.Where(nv => scope.EmployeeIds.Contains(nv.Manhanvien));
            }

            var nhanviens = nhanviensQuery
                .OrderBy(nv => nv.Hoten)
                .ToList();

            ViewBag.NhanViens = nhanviens;

            // nhóm
            var nhomsQuery = _context.Nhoms.AsQueryable();
            if (!scope.IsAdmin)
            {
                nhomsQuery = nhomsQuery.Where(n => scope.GroupIds.Contains(n.Manhom));
            }
            var nhoms = nhomsQuery.OrderBy(n => n.Tennhom).ToList();
            ViewBag.Nhoms = nhoms;

            // phòng ban
            var phongbanQuery = _context.Phongbans.AsQueryable();
            if (!scope.IsAdmin)
            {
                phongbanQuery = phongbanQuery.Where(pb => scope.DepartmentIds.Contains(pb.Maphongban));
            }
            var phongbans = phongbanQuery.OrderBy(pb => pb.Tenphongban).ToList();
            ViewBag.Phongbans = phongbans;

            ViewBag.CanAssignNhanVien = nhanviens.Any();
            ViewBag.CanAssignNhom = nhoms.Any();
            ViewBag.CanAssignPhongBan = phongbans.Any();

            if (scope.IsAdmin)
            {
                ViewBag.ScopeDescription = "Bạn có thể phân công cho toàn bộ nhân viên, nhóm và phòng ban trong hệ thống.";
            }
            else
            {
                var scopeParts = new List<string>();

                if (nhoms.Any())
                {
                    var tenNhoms = string.Join(", ", nhoms.Select(n => n.Tennhom).Where(x => !string.IsNullOrWhiteSpace(x)));
                    if (!string.IsNullOrWhiteSpace(tenNhoms))
                    {
                        scopeParts.Add($"Bạn đang quản lý nhóm: {tenNhoms}.");
                    }
                }

                if (phongbans.Any())
                {
                    var tenPhongBans = string.Join(", ", phongbans.Select(p => p.Tenphongban).Where(x => !string.IsNullOrWhiteSpace(x)));
                    if (!string.IsNullOrWhiteSpace(tenPhongBans))
                    {
                        scopeParts.Add($"Bạn đang quản lý phòng: {tenPhongBans}.");
                    }
                }

                ViewBag.ScopeDescription = scopeParts.Any()
                    ? string.Join(" ", scopeParts)
                    : "Bạn không có phạm vi phân công hợp lệ.";
            }

            return View();
        }

        // POST: Phancongcongviecs/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
    Phancongcongviec model,
    List<int> selectedNhanVien,
    List<int> selectedNhom,
    List<int> selectedPhongBan,
    int? nguoiDuyet)
        {
            selectedNhanVien ??= new List<int>();
            selectedNhom ??= new List<int>();
            selectedPhongBan ??= new List<int>();

            var scope = await GetAssignmentScopeAsync();

            if (!scope.IsAdmin)
            {
                selectedNhanVien = selectedNhanVien
                    .Where(id => scope.EmployeeIds.Contains(id))
                    .Distinct()
                    .ToList();

                selectedNhom = selectedNhom
                    .Where(id => scope.GroupIds.Contains(id))
                    .Distinct()
                    .ToList();

                selectedPhongBan = selectedPhongBan
                    .Where(id => scope.DepartmentIds.Contains(id))
                    .Distinct()
                    .ToList();
            }

            if (!selectedNhanVien.Any() && !selectedNhom.Any() && !selectedPhongBan.Any())
            {
                return Forbid();
            }

            DateTime now = DateTime.Now; // thời gian hệ thống

            // PHÂN CÔNG CHO NHÂN VIÊN
            if (selectedNhanVien != null && selectedNhanVien.Any())
            {
                foreach (var nv in selectedNhanVien)
                {
                    var pc = new Phancongcongviec
                    {
                        Macongviec = model.Macongviec,
                        Manhanvien = nv,
                        Maphongban = nguoiDuyet, // tạm dùng lưu người duyệt

                        Loaidoituong = "NhanVien",

                        Ngaygiao = now, // tự động lấy thời gian hệ thống
                        Ngaybatdaudukien = model.Ngaybatdaudukien,
                        Ngayketthucdukien = model.Ngayketthucdukien
                    };

                    _context.Phancongcongviecs.Add(pc);
                }
            }

            // PHÂN CÔNG CHO NHÓM
            if (selectedNhom != null && selectedNhom.Any())
            {
                foreach (var nhomId in selectedNhom)
                {
                    var nhanviens = _context.Thanhviennhoms
                        .Where(tv => tv.Manhom == nhomId)
                        .Include(tv => tv.ManhanvienNavigation)
                        .Select(tv => tv.ManhanvienNavigation)
                        .ToList();

                    foreach (var nv in nhanviens)
                    {
                        var pc = new Phancongcongviec
                        {
                            Macongviec = model.Macongviec,
                            Manhanvien = nv.Manhanvien,
                            Manhom = nhomId,
                            Loaidoituong = "Nhom",
                            Ngaygiao = now,
                            Ngaybatdaudukien = model.Ngaybatdaudukien,
                            Ngayketthucdukien = model.Ngayketthucdukien
                        };

                        _context.Phancongcongviecs.Add(pc);
                    }
                }
            }

            // PHÂN CÔNG CHO PHÒNG BAN
            if (selectedPhongBan != null && selectedPhongBan.Any())
            {
                foreach (var pbId in selectedPhongBan)
                {
                    var nhanviens = _context.Nhanviens
                        .Where(nv => nv.Maphongban == pbId)
                        .ToList();

                    foreach (var nv in nhanviens)
                    {
                        var pc = new Phancongcongviec
                        {
                            Macongviec = model.Macongviec,
                            Manhanvien = nv.Manhanvien,
                            Maphongban = pbId,
                            Loaidoituong = "PhongBan",
                            Ngaygiao = now,
                            Ngaybatdaudukien = model.Ngaybatdaudukien,
                            Ngayketthucdukien = model.Ngayketthucdukien
                        };

                        _context.Phancongcongviecs.Add(pc);
                    }
                }
            }

            await _context.SaveChangesAsync();

            var maduan = _context.Congviecs
    .Where(c => c.Macongviec == model.Macongviec)
    .Select(c => c.Maduan)
    .FirstOrDefault();

            return RedirectToAction("Details", "Duans", new { id = maduan });
        }


        // POST: Phancongcongviecs/RemoveNhanVien/5
        [HttpPost]
        public async Task<IActionResult> RemoveNhanVien(int id)
        {
            if (!User.IsInRole("Admin"))
            {
                return Forbid();
            }

            var pc = await _context.Phancongcongviecs.FindAsync(id);

            if (pc == null)
                return NotFound();

            int macongviec = pc.Macongviec;

            _context.Phancongcongviecs.Remove(pc);
            await _context.SaveChangesAsync();

            return RedirectToAction("Details", "Congviecs", new { id = macongviec });
        }



        // GET: Phancongcongviecs/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (!User.IsInRole("Admin"))
            {
                return Forbid();
            }

            if (id == null)
            {
                return NotFound();
            }

            var phancongcongviec = await _context.Phancongcongviecs.FindAsync(id);
            if (phancongcongviec == null)
            {
                return NotFound();
            }
            ViewData["Macongviec"] = new SelectList(_context.Congviecs, "Macongviec", "Macongviec", phancongcongviec.Macongviec);
            ViewData["Manhanvien"] = new SelectList(_context.Nhanviens, "Manhanvien", "Manhanvien", phancongcongviec.Manhanvien);
            ViewData["Manhom"] = new SelectList(_context.Nhoms, "Manhom", "Manhom", phancongcongviec.Manhom);
            ViewData["Maphongban"] = new SelectList(_context.Phongbans, "Maphongban", "Maphongban", phancongcongviec.Maphongban);
            return View(phancongcongviec);
        }

        // POST: Phancongcongviecs/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Maphancong,Macongviec,Maphongban,Manhom,Manhanvien,Loaidoituong,Ngaygiao,Ngaybatdaudukien,Ngayketthucdukien,Ngaybatdauthucte,Ngayketthucthucte")] Phancongcongviec phancongcongviec)
        {
            if (!User.IsInRole("Admin"))
            {
                return Forbid();
            }

            if (id != phancongcongviec.Maphancong)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(phancongcongviec);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PhancongcongviecExists(phancongcongviec.Maphancong))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["Macongviec"] = new SelectList(_context.Congviecs, "Macongviec", "Macongviec", phancongcongviec.Macongviec);
            ViewData["Manhanvien"] = new SelectList(_context.Nhanviens, "Manhanvien", "Manhanvien", phancongcongviec.Manhanvien);
            ViewData["Manhom"] = new SelectList(_context.Nhoms, "Manhom", "Manhom", phancongcongviec.Manhom);
            ViewData["Maphongban"] = new SelectList(_context.Phongbans, "Maphongban", "Maphongban", phancongcongviec.Maphongban);
            return View(phancongcongviec);
        }

        // GET: Phancongcongviecs/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (!User.IsInRole("Admin"))
            {
                return Forbid();
            }

            if (id == null)
            {
                return NotFound();
            }

            var phancongcongviec = await _context.Phancongcongviecs
                .Include(p => p.MacongviecNavigation)
                .Include(p => p.ManhanvienNavigation)
                .Include(p => p.ManhomNavigation)
                .Include(p => p.MaphongbanNavigation)
                .FirstOrDefaultAsync(m => m.Maphancong == id);
            if (phancongcongviec == null)
            {
                return NotFound();
            }

            return View(phancongcongviec);
        }

        // POST: Phancongcongviecs/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!User.IsInRole("Admin"))
            {
                return Forbid();
            }

            var phancongcongviec = await _context.Phancongcongviecs.FindAsync(id);
            if (phancongcongviec != null)
            {
                _context.Phancongcongviecs.Remove(phancongcongviec);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool PhancongcongviecExists(int id)
        {
            return _context.Phancongcongviecs.Any(e => e.Maphancong == id);
        }

        private async Task<int?> GetCurrentNhanVienIdAsync()
        {
            var currentUserName = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(currentUserName))
            {
                return null;
            }

            return await _context.Users
                .Where(u => u.UserName == currentUserName || u.Email == currentUserName)
                .Select(u => u.MaNhanVien)
                .FirstOrDefaultAsync();
        }

        private async Task<(bool IsAdmin, List<int> EmployeeIds, List<int> GroupIds, List<int> DepartmentIds)> GetAssignmentScopeAsync()
        {
            if (User.IsInRole("Admin"))
            {
                return (true, new List<int>(), new List<int>(), new List<int>());
            }

            var currentNhanVienId = await GetCurrentNhanVienIdAsync();
            if (!currentNhanVienId.HasValue)
            {
                return (false, new List<int>(), new List<int>(), new List<int>());
            }

            var groupIds = new List<int>();
            var departmentIds = new List<int>();

            if (User.IsInRole("TruongNhom"))
            {
                groupIds = await _context.Thanhviennhoms
                    .Where(tv => tv.Manhanvien == currentNhanVienId.Value && tv.Vaitrotrongnhom == "TruongNhom")
                    .Select(tv => tv.Manhom)
                    .Distinct()
                    .ToListAsync();
            }

            if (User.IsInRole("TruongPhong"))
            {
                departmentIds = await _context.Phongbans
                    .Where(pb => pb.Matruongphong == currentNhanVienId.Value)
                    .Select(pb => pb.Maphongban)
                    .Distinct()
                    .ToListAsync();
            }

            var employeeIdsInGroups = groupIds.Count == 0
                ? new List<int>()
                : await _context.Thanhviennhoms
                    .Where(tv => groupIds.Contains(tv.Manhom))
                    .Select(tv => tv.Manhanvien)
                    .Distinct()
                    .ToListAsync();

            var employeeIdsInDepartments = departmentIds.Count == 0
                ? new List<int>()
                : await _context.Nhanviens
                    .Where(nv => nv.Maphongban.HasValue && departmentIds.Contains(nv.Maphongban.Value))
                    .Select(nv => nv.Manhanvien)
                    .Distinct()
                    .ToListAsync();

            var employeeIds = employeeIdsInGroups
                .Concat(employeeIdsInDepartments)
                .Distinct()
                .ToList();

            return (false, employeeIds, groupIds, departmentIds);
        }
    }
}
