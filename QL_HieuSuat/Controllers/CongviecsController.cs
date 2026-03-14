using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using QL_HieuSuat.Hubs;
using QL_HieuSuat.Models;
using QL_HieuSuat.Models.ViewModels;

namespace QL_HieuSuat.Controllers
{
    [Authorize]
    public class CongviecsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CongviecsController> _logger;
        private readonly IHubContext<TaskProgressHub> _hubContext;
        private const long MaxUploadSizeBytes = 10 * 1024 * 1024; // 10 MB
        private static readonly HashSet<string> AllowedUploadExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".png", ".jpg", ".jpeg", ".gif", ".txt"
        };

        public CongviecsController(
            ApplicationDbContext context,
            ILogger<CongviecsController> logger,
            IHubContext<TaskProgressHub> hubContext)
        {
            _context = context;
            _logger = logger;
            _hubContext = hubContext;
        }

        // GET: Congviecs
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.Congviecs
                .Include(c => c.MaduanNavigation)
                .Include(c => c.MatrangthaiNavigation);
            return View(await applicationDbContext.ToListAsync());
        }

        // GET: Congviecs/Details/5
        [Authorize(Roles = "Admin,NhanVien,TruongPhong,TruongNhom,QuanLyNhanSu,QuanLyDuAn")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            if (!User.IsInRole("Admin") && !await IsTaskAssignedToCurrentEmployeeAsync(id.Value))
            {
                return Forbid();
            }

            var congViec = await _context.Congviecs
                .Include(c => c.Phancongcongviecs)
                    .ThenInclude(pc => pc.ManhanvienNavigation)
                .Include(c => c.Phancongcongviecs)
                    .ThenInclude(pc => pc.ManhomNavigation)
                .Include(c => c.Phancongcongviecs)
                    .ThenInclude(pc => pc.MaphongbanNavigation)
                .Include(c => c.Nhatkycongviecs)
                .Include(c => c.Matailieus)
                .FirstOrDefaultAsync(m => m.Macongviec == id);

            if (congViec == null) return NotFound();

            // ⭐ LẤY BÌNH LUẬN
            var comments = await _context.Binhluans
    .Where(b => b.Macongviec == id)
    .Include(b => b.ManhanvienNavigation)
    .OrderByDescending(b => b.Ngaytao)
    .ToListAsync();

            ViewBag.Comments = comments;

            // Lấy công việc con
            var congViecCon = await _context.Congviecs
                .Where(c => c.Macongvieccha == id)
                .Include(c => c.Phancongcongviecs)
                    .ThenInclude(pc => pc.ManhanvienNavigation)
                .Include(c => c.Phancongcongviecs)
                    .ThenInclude(pc => pc.ManhomNavigation)
                .Include(c => c.Phancongcongviecs)
                    .ThenInclude(pc => pc.MaphongbanNavigation)
                .Include(c => c.Nhatkycongviecs)
                .ToListAsync();

            congViec.InverseConMacongviecNavigation = congViecCon;

            ViewBag.CurrentNhanVienId = await GetCurrentNhanVienIdAsync();

            return View(congViec);
        }

        [Authorize(Roles = "TruongPhong,TruongNhom")]
        public async Task<IActionResult> XacNhanTienDo()
        {
            var currentUserId = await GetCurrentNhanVienIdAsync();
            if (currentUserId == null)
            {
                TempData["UploadError"] = "Không xác định được tài khoản nhân viên hiện tại.";
                return View(new List<XacNhanTienDoItemViewModel>());
            }

            var leadGroupIds = await _context.Thanhviennhoms
                .Where(tv => tv.Manhanvien == currentUserId && tv.Vaitrotrongnhom == "TruongNhom")
                .Select(tv => tv.Manhom)
                .Distinct()
                .ToListAsync();

            var leadDepartmentIds = await _context.Phongbans
                .Where(pb => pb.Matruongphong == currentUserId)
                .Select(pb => pb.Maphongban)
                .Distinct()
                .ToListAsync();

            if (leadGroupIds.Count == 0 && leadDepartmentIds.Count == 0)
            {
                return View(new List<XacNhanTienDoItemViewModel>());
            }

            var employeeIdsInLeadGroups = await _context.Thanhviennhoms
                .Where(tv => leadGroupIds.Contains(tv.Manhom))
                .Select(tv => tv.Manhanvien)
                .Distinct()
                .ToListAsync();

            var employeeIdsInLeadDepartments = await _context.Nhanviens
                .Where(nv => nv.Maphongban != null && leadDepartmentIds.Contains(nv.Maphongban.Value))
                .Select(nv => nv.Manhanvien)
                .Distinct()
                .ToListAsync();

            var tasks = await _context.Congviecs
                .Where(cv => cv.Matrangthai == 4)
                .Include(cv => cv.MaduanNavigation)
                .Include(cv => cv.Nhatkycongviecs)
                .Include(cv => cv.Phancongcongviecs)
                    .ThenInclude(pc => pc.ManhanvienNavigation)
                .Include(cv => cv.Phancongcongviecs)
                    .ThenInclude(pc => pc.ManhomNavigation)
                .Include(cv => cv.Phancongcongviecs)
                    .ThenInclude(pc => pc.MaphongbanNavigation)
                .AsSplitQuery()
                .ToListAsync();

            var filteredTasks = tasks
                .Where(cv => cv.Phancongcongviecs.Any(pc =>
                    (pc.Loaidoituong == "Nhom" && pc.Manhom.HasValue && leadGroupIds.Contains(pc.Manhom.Value)) ||
                    (pc.Loaidoituong == "PhongBan" && pc.Maphongban.HasValue && leadDepartmentIds.Contains(pc.Maphongban.Value)) ||
                    (pc.Loaidoituong == "NhanVien" && pc.Manhanvien.HasValue &&
                        (employeeIdsInLeadGroups.Contains(pc.Manhanvien.Value) || employeeIdsInLeadDepartments.Contains(pc.Manhanvien.Value)))
                ))
                .OrderBy(cv => cv.Hanhoanthanh ?? DateTime.MaxValue)
                .ThenBy(cv => cv.Macongviec)
                .ToList();

            var viewModel = filteredTasks.Select(cv =>
            {
                var latestProgress = cv.Nhatkycongviecs
                    .OrderByDescending(nk => nk.Ngaycapnhat)
                    .FirstOrDefault()?.Phantramhoanthanh ?? 0;

                var assignees = cv.Phancongcongviecs
                    .Select(pc =>
                    {
                        if (pc.Loaidoituong == "NhanVien" && !string.IsNullOrWhiteSpace(pc.ManhanvienNavigation?.Hoten))
                        {
                            return pc.ManhanvienNavigation.Hoten;
                        }

                        if (pc.Loaidoituong == "Nhom" && !string.IsNullOrWhiteSpace(pc.ManhomNavigation?.Tennhom))
                        {
                            return $"Nhóm: {pc.ManhomNavigation.Tennhom}";
                        }

                        if (pc.Loaidoituong == "PhongBan" && !string.IsNullOrWhiteSpace(pc.MaphongbanNavigation?.Tenphongban))
                        {
                            return $"Phòng: {pc.MaphongbanNavigation.Tenphongban}";
                        }

                        return null;
                    })
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x!)
                    .Distinct()
                    .ToList();

                return new XacNhanTienDoItemViewModel
                {
                    MaCongViec = cv.Macongviec,
                    TenCongViec = cv.Tencongviec,
                    TenDuAn = cv.MaduanNavigation?.Tenduan,
                    HanHoanThanh = cv.Hanhoanthanh,
                    TienDoPhanTram = latestProgress,
                    NguoiNhan = assignees,
                    MucUuTien = cv.Douutien
                };
            }).ToList();

            return View(viewModel);
        }


        // GET: Congviecs/Create
        [Authorize(Roles = "Admin")]
        public IActionResult Create(int? maduan, int? macongvieccha)
        {
            ViewBag.Maduan = maduan;
            ViewBag.Macongvieccha = macongvieccha;

            if (macongvieccha != null)
            {
                var congViecCha = _context.Congviecs
                    .FirstOrDefault(c => c.Macongviec == macongvieccha);

                if (congViecCha != null)
                {
                    ViewBag.TenCongViecCha = congViecCha.Tencongviec;

                    // ⭐ QUAN TRỌNG
                    ViewBag.Maduan = congViecCha.Maduan;
                }
            }

            ViewData["Matrangthai"] = new SelectList(
                _context.Trangthaicongviecs,
                "Matrangthai",
                "Tentrangthai"
            );

            return View();
        }

        private void PopulateCreateViewData(int? maduan, int? macongvieccha)
        {
            ViewBag.Maduan = maduan;
            ViewBag.Macongvieccha = macongvieccha;

            if (macongvieccha != null)
            {
                var congViecCha = _context.Congviecs
                    .FirstOrDefault(c => c.Macongviec == macongvieccha);

                if (congViecCha != null)
                {
                    ViewBag.TenCongViecCha = congViecCha.Tencongviec;
                    ViewBag.Maduan = congViecCha.Maduan;
                }
            }

            ViewData["Matrangthai"] = new SelectList(
                _context.Trangthaicongviecs,
                "Matrangthai",
                "Tentrangthai",
                1
            );
        }

        private bool ValidateUploadFile(IFormFile file, out string message)
        {
            message = string.Empty;

            if (file.Length > MaxUploadSizeBytes)
            {
                message = "File vượt quá giới hạn 10 MB.";
                return false;
            }

            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(extension) || !AllowedUploadExtensions.Contains(extension))
            {
                message = "Định dạng file không được hỗ trợ.";
                return false;
            }

            return true;
        }

        private async Task<(string savedFileName, string savedFilePath)> SaveUploadFileAsync(IFormFile file)
        {
            var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var savedFileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
            var savedFilePath = Path.Combine(folder, savedFileName);

            using (var stream = new FileStream(savedFilePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return (savedFileName, savedFilePath);
        }

        // POST: Congviecs/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(Congviec congviec, IFormFile? TaiLieuFile)
        {
            // bỏ validate navigation
            ModelState.Remove("MaduanNavigation");
            ModelState.Remove("MatrangthaiNavigation");
            ModelState.Remove("ConMacongviecNavigation");
            ModelState.Remove("InverseConMacongviecNavigation");

            // DEBUG: xem lỗi ModelState trong Console
            foreach (var state in ModelState)
            {
                foreach (var error in state.Value.Errors)
                {
                    Console.WriteLine($"Field: {state.Key} - Error: {error.ErrorMessage}");
                }
            }

            if (!ModelState.IsValid)
            {
                PopulateCreateViewData(congviec.Maduan, congviec.Macongvieccha);
                ViewData["Matrangthai"] = new SelectList(
                    _context.Trangthaicongviecs,
                    "Matrangthai",
                    "Tentrangthai",
                    congviec.Matrangthai > 0 ? congviec.Matrangthai : 1
                );

                return View(congviec);
            }

            if (congviec.Maduan <= 0)
            {
                ModelState.AddModelError("Maduan", "Thiếu mã dự án. Vui lòng vào lại từ màn hình chi tiết dự án.");
                PopulateCreateViewData(congviec.Maduan, congviec.Macongvieccha);
                return View(congviec);
            }

            // công việc CHA
            if (congviec.Macongvieccha == 0)
            {
                congviec.Macongvieccha = null;
            }

            // trạng thái mặc định
            if (congviec.Matrangthai <= 0)
            {
                congviec.Matrangthai = 1;
            }
            congviec.Trangthai = congviec.Matrangthai;

            try
            {
                _context.Congviecs.Add(congviec);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Create task failed. Maduan={Maduan}, Tencongviec={Tencongviec}", congviec.Maduan, congviec.Tencongviec);
                ModelState.AddModelError(string.Empty, $"Không thể lưu công việc. Chi tiết: {ex.GetBaseException().Message}");
                PopulateCreateViewData(congviec.Maduan, congviec.Macongvieccha);
                return View(congviec);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Create task unexpected error. Maduan={Maduan}, Tencongviec={Tencongviec}", congviec.Maduan, congviec.Tencongviec);
                ModelState.AddModelError(string.Empty, $"Lỗi hệ thống khi lưu công việc: {ex.Message}");
                PopulateCreateViewData(congviec.Maduan, congviec.Macongvieccha);
                return View(congviec);
            }

            if (TaiLieuFile != null && TaiLieuFile.Length > 0)
            {
                if (!ValidateUploadFile(TaiLieuFile, out var uploadMessage))
                {
                    TempData["UploadError"] = $"Công việc đã lưu, nhưng tài liệu không hợp lệ: {uploadMessage}";
                    return RedirectToAction("Details", "Duans", new { id = congviec.Maduan });
                }

                string? createdPhysicalFilePath = null;
                try
                {
                    var (savedFileName, savedFilePath) = await SaveUploadFileAsync(TaiLieuFile);
                    createdPhysicalFilePath = savedFilePath;

                    var taskToAttach = await _context.Congviecs
                        .Include(c => c.Matailieus)
                        .FirstOrDefaultAsync(c => c.Macongviec == congviec.Macongviec);

                    if (taskToAttach != null)
                    {
                        var tailieu = new Tailieu
                        {
                            Tentailieu = Path.GetFileName(TaiLieuFile.FileName),
                            Huongdan = savedFileName
                        };

                        taskToAttach.Matailieus.Add(tailieu);
                        await _context.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    if (!string.IsNullOrWhiteSpace(createdPhysicalFilePath) && System.IO.File.Exists(createdPhysicalFilePath))
                    {
                        System.IO.File.Delete(createdPhysicalFilePath);
                    }

                    _logger.LogError(ex, "Task {TaskId} saved but attaching document failed", congviec.Macongviec);
                    TempData["UploadError"] = "Công việc đã lưu, nhưng tải tài liệu thất bại.";
                }
            }

            return RedirectToAction("Details", "Duans", new { id = congviec.Maduan });

}



        // GET: Congviecs/Edit/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var congviec = await _context.Congviecs.FindAsync(id);
            if (congviec == null)
                return NotFound();

            // Lấy công việc cha
            if (congviec.Macongvieccha != null)
            {
                var cha = _context.Congviecs
                    .FirstOrDefault(c => c.Macongviec == congviec.Macongvieccha);

                if (cha != null)
                    ViewBag.CongViecCha = cha.Tencongviec;
            }

            // Lấy tên dự án để hiển thị (không cho sửa)
            var duan = _context.Duans
                .FirstOrDefault(d => d.Maduan == congviec.Maduan);

            if (duan != null)
                ViewBag.TenDuAn = duan.Tenduan;


            
            return View(congviec);
        }

        // POST: Congviecs/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, Congviec model)
        {
            if (id != model.Macongviec)
                return NotFound();

            // ❗ remove validation navigation
            ModelState.Remove("MaduanNavigation");
            ModelState.Remove("MatrangthaiNavigation");
            ModelState.Remove("ConMacongviecNavigation");
            ModelState.Remove("InverseConMacongviecNavigation");

            var cv = await _context.Congviecs.FindAsync(id);

            if (cv == null)
                return NotFound();

            if (ModelState.IsValid)
            {
                cv.Maduan = model.Maduan;
                cv.Matrangthai = model.Matrangthai;
                cv.Tencongviec = model.Tencongviec;
                cv.Mota = model.Mota;
                cv.Hanhoanthanh = model.Hanhoanthanh;
                cv.Trangthai = model.Matrangthai;
                cv.Douutien = model.Douutien;
                cv.Dokho = model.Dokho;
                cv.Diemcongviec = model.Diemcongviec;

                await _context.SaveChangesAsync();

                return RedirectToAction("Details", new { id = cv.Macongviec });
            }

            // debug lỗi
            foreach (var state in ModelState)
            {
                foreach (var error in state.Value.Errors)
                {
                    Console.WriteLine($"Field: {state.Key} - Error: {error.ErrorMessage}");
                }
            }

            ViewData["Maduan"] = new SelectList(_context.Duans, "Maduan", "Maduan", model.Maduan);
            ViewData["Matrangthai"] = new SelectList(_context.Trangthaicongviecs, "Matrangthai", "Matrangthai", model.Matrangthai);

            return View(model);
        }

        // GET: Congviecs/Delete/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var congviec = await _context.Congviecs
                .Include(c => c.ConMacongviecNavigation)
                .Include(c => c.MaduanNavigation)
                .Include(c => c.MatrangthaiNavigation)
                .FirstOrDefaultAsync(m => m.Macongviec == id);
            if (congviec == null) return NotFound();

            return View(congviec);
        }

        // POST: Congviecs/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var congviec = await _context.Congviecs.FindAsync(id);
            if (congviec != null)
            {
                _context.Congviecs.Remove(congviec);
            }
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // API/Action phụ
     
        [HttpPost]
        [Authorize(Roles = "NhanVien,TruongPhong,TruongNhom")]
        public async Task<IActionResult> RequestComplete(int id, bool status)
        {
            var cv = _context.Congviecs.Find(id);

            if (cv == null)
                return Json(new { success = false });

            var currentUserName = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(currentUserName))
            {
                return Json(new { success = false });
            }

            var currentNhanVienId = _context.Users
                .Where(u => u.UserName == currentUserName || u.Email == currentUserName)
                .Select(u => u.MaNhanVien)
                .FirstOrDefault();

            if (!currentNhanVienId.HasValue)
            {
                return Json(new { success = false });
            }

            var isAssigned = _context.Phancongcongviecs.Any(pc =>
                pc.Macongviec == id && pc.Manhanvien.HasValue && pc.Manhanvien.Value == currentNhanVienId.Value);

            if (!isAssigned)
            {
                return Json(new { success = false });
            }

            if (status)
            {
                // TruongPhong can directly mark completion without approval.
                cv.Matrangthai = User.IsInRole("TruongPhong") ? 3 : 4;
            }
            else
            {
                cv.Matrangthai = 2; // đang làm
            }

            cv.Trangthai = cv.Matrangthai;

            await _context.SaveChangesAsync();

            CapNhatTienDoCongViecCha(cv.Macongvieccha);
            CapNhatTienDoDuAn(cv.Maduan);

            await BroadcastProgressChangedAsync(cv);

            return Json(new { success = true });
        }

        

        [Authorize(Roles = "TruongPhong,TruongNhom")]
        public async Task<IActionResult> Approve(int id)
        {
            var cv = await _context.Congviecs.FindAsync(id);

            if (cv == null)
                return NotFound();

            var currentUserId = await GetCurrentNhanVienIdAsync();
            if (currentUserId == null)
            {
                return Content("Không tìm thấy nhân viên.");
            }

            if (!await CanApproveTaskAsync(id, currentUserId.Value))
            {
                return Content("Bạn không có quyền duyệt công việc này.");
            }

            // ===== DUYỆT =====
            cv.Matrangthai = 3;
            cv.Trangthai = cv.Matrangthai;

            await _context.SaveChangesAsync();

            CapNhatTienDoCongViecCha(cv.Macongvieccha);
            CapNhatTienDoDuAn(cv.Maduan);

            await BroadcastProgressChangedAsync(cv);

            if (cv.Macongvieccha != null)
                return RedirectToAction("Details", new { id = cv.Macongvieccha });

            return RedirectToAction("Details", new { id = cv.Macongviec });
        }

        private async Task BroadcastProgressChangedAsync(Congviec congviec)
        {
            await _hubContext.Clients.All.SendAsync("ProgressChanged", new
            {
                taskId = congviec.Macongviec,
                parentTaskId = congviec.Macongvieccha,
                projectId = congviec.Maduan,
                status = congviec.Matrangthai,
                updatedAt = DateTime.UtcNow
            });
        }

        private async Task<int?> GetCurrentNhanVienIdAsync()
        {
            var userName = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(userName))
            {
                return null;
            }

            var currentUser = await _context.Nhanviens
                .AsNoTracking()
                .FirstOrDefaultAsync(nv => nv.Email == userName);

            return currentUser?.Manhanvien;
        }

        private async Task<bool> CanApproveTaskAsync(int taskId, int currentUserId)
        {
            var assignments = await _context.Phancongcongviecs
                .Where(pc => pc.Macongviec == taskId)
                .ToListAsync();

            if (assignments.Count == 0)
            {
                return false;
            }

            var leadGroupIds = await _context.Thanhviennhoms
                .Where(tv => tv.Manhanvien == currentUserId && tv.Vaitrotrongnhom == "TruongNhom")
                .Select(tv => tv.Manhom)
                .Distinct()
                .ToListAsync();

            var leadDepartmentIds = await _context.Phongbans
                .Where(pb => pb.Matruongphong == currentUserId)
                .Select(pb => pb.Maphongban)
                .Distinct()
                .ToListAsync();

            var employeeIdsInLeadGroups = await _context.Thanhviennhoms
                .Where(tv => leadGroupIds.Contains(tv.Manhom))
                .Select(tv => tv.Manhanvien)
                .Distinct()
                .ToListAsync();

            var employeeIdsInLeadDepartments = await _context.Nhanviens
                .Where(nv => nv.Maphongban != null && leadDepartmentIds.Contains(nv.Maphongban.Value))
                .Select(nv => nv.Manhanvien)
                .Distinct()
                .ToListAsync();

            return assignments.Any(pc =>
                (pc.Loaidoituong == "Nhom" && pc.Manhom.HasValue && leadGroupIds.Contains(pc.Manhom.Value)) ||
                (pc.Loaidoituong == "PhongBan" && pc.Maphongban.HasValue && leadDepartmentIds.Contains(pc.Maphongban.Value)) ||
                (pc.Loaidoituong == "NhanVien" && pc.Manhanvien.HasValue &&
                    (employeeIdsInLeadGroups.Contains(pc.Manhanvien.Value) || employeeIdsInLeadDepartments.Contains(pc.Manhanvien.Value))));
        }

        // Helper/Private methods
        private void CapNhatTienDoCongViecCha(int? macongvieccha)
        {
            if (macongvieccha == null) return;

            var cha = _context.Congviecs
                .Include(c => c.InverseConMacongviecNavigation)
                .FirstOrDefault(c => c.Macongviec == macongvieccha);

            if (cha == null) return;

            var conList = cha.InverseConMacongviecNavigation.ToList();

            if (conList.Count == 0) return;

            int total = conList.Count;
            int done = conList.Count(c => c.Matrangthai == 3);

            int percent = (done * 100) / total;

            cha.Tongcongvieccon = percent;

            // ⭐ nếu tất cả công việc con hoàn thành
            if (done == total)
            {
                cha.Matrangthai = 3; // hoàn thành
            }
            else if (done > 0)
            {
                cha.Matrangthai = 2; // đang thực hiện
            }
            else
            {
                cha.Matrangthai = 1; // chưa bắt đầu
            }

            cha.Trangthai = cha.Matrangthai;

            _context.SaveChanges();

            // ⭐ cập nhật tiếp lên công việc cha tiếp theo (đệ quy)
            CapNhatTienDoCongViecCha(cha.Macongvieccha);
        }


        private void CapNhatTienDoDuAn(int maduan)
        {
            var duan = _context.Duans
                .Include(d => d.Congviecs)
                .FirstOrDefault(d => d.Maduan == maduan);

            if (duan == null) return;

            int total = duan.Congviecs.Count;
            int done = duan.Congviecs.Count(c => c.Matrangthai == 3);

            if (total == 0) return;

            int percent = (done * 100) / total;

            if (percent == 100)
                duan.Trangthai = 3; // hoàn thành
            else if (percent > 0)
                duan.Trangthai = 2; // đang thực hiện
            else
                duan.Trangthai = 1; // chưa bắt đầu

            _context.SaveChanges();
        }


        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UploadTaiLieu(int macongviec, IFormFile file)
        {
            if (file == null || file.Length == 0)
                return RedirectToAction("Details", new { id = macongviec });

            if (!ValidateUploadFile(file, out var uploadMessage))
            {
                TempData["UploadError"] = uploadMessage;
                return RedirectToAction("Details", new { id = macongviec });
            }

            string? createdPhysicalFilePath = null;
            try
            {
                var (savedFileName, savedFilePath) = await SaveUploadFileAsync(file);
                createdPhysicalFilePath = savedFilePath;

                var tailieu = new Tailieu
                {
                    Tentailieu = Path.GetFileName(file.FileName),
                    Huongdan = savedFileName
                };

                var congviec = _context.Congviecs
                    .Include(c => c.Matailieus)
                    .FirstOrDefault(c => c.Macongviec == macongviec);

                if (congviec == null)
                {
                    if (System.IO.File.Exists(savedFilePath))
                    {
                        System.IO.File.Delete(savedFilePath);
                    }

                    return NotFound();
                }

                congviec.Matailieus.Add(tailieu);
                await _context.SaveChangesAsync();

                TempData["UploadSuccess"] = "Tải tài liệu lên thành công.";
                return RedirectToAction("Details", new { id = macongviec });
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(createdPhysicalFilePath) && System.IO.File.Exists(createdPhysicalFilePath))
                {
                    System.IO.File.Delete(createdPhysicalFilePath);
                }

                TempData["UploadError"] = $"Không thể tải tài liệu: {ex.Message}";
                return RedirectToAction("Details", new { id = macongviec });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteFile(int id)
        {
            var tailieu = await _context.Tailieus
                .Include(t => t.Macongviecs)
                .FirstOrDefaultAsync(t => t.Matailieu == id);

            if (tailieu == null)
                return NotFound();

            var macongviec = tailieu.Macongviecs.FirstOrDefault()?.Macongviec;

            // ❗ XÓA LIÊN KẾT TRONG BẢNG TRUNG GIAN
            tailieu.Macongviecs.Clear();

            // xóa file vật lý
            if (!string.IsNullOrWhiteSpace(tailieu.Huongdan))
            {
                var filePath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot/uploads",
                    tailieu.Huongdan
                );

                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }

            // xóa tài liệu
            _context.Tailieus.Remove(tailieu);

            await _context.SaveChangesAsync();

            return RedirectToAction("Details", new { id = macongviec });
        }

        [HttpPost]
        [Authorize(Roles = "Admin,NhanVien,TruongPhong,TruongNhom,QuanLyNhanSu,QuanLyDuAn")]
        public IActionResult AddComment(int macongviec, string noidung)
        {
            var username = User.Identity?.Name;

            var user = _context.Users
                .Include(u => u.Nhanvien)
                .FirstOrDefault(u => u.UserName == username);

            if (user == null || user.Nhanvien == null)
                return RedirectToAction("Details", new { id = macongviec });

            var comment = new Binhluan
            {
                Macongviec = macongviec,
                Manhanvien = user.Nhanvien.Manhanvien,
                Noidung = noidung,
                Ngaytao = DateTime.Now
            };

            _context.Binhluans.Add(comment);
            _context.SaveChanges();

            return RedirectToAction("Details", new { id = macongviec });
        }

        [Authorize(Roles = "Admin,NhanVien,TruongPhong,TruongNhom,QuanLyNhanSu,QuanLyDuAn")]
        public async Task<IActionResult> TroChuyen(int id)
        {
            var congviec = await _context.Congviecs
                .Include(c => c.Phancongcongviecs)
                .FirstOrDefaultAsync(c => c.Macongviec == id);

            if (congviec == null)
            {
                return NotFound();
            }

            if (!User.IsInRole("Admin") && !await IsTaskAssignedToCurrentEmployeeAsync(id))
            {
                return Forbid();
            }

            if (await IsPersonalTaskAsync(id))
            {
                TempData["UploadError"] = "Công việc cá nhân không có khu vực trò chuyện nhóm/công việc.";
                return RedirectToAction("Details", new { id });
            }

            var comments = await _context.Binhluans
                .Where(b => b.Macongviec == id)
                .Include(b => b.ManhanvienNavigation)
                .OrderBy(b => b.Ngaytao)
                .ToListAsync();

            ViewBag.CongViec = congviec;
            return View(comments);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,NhanVien,TruongPhong,TruongNhom,QuanLyNhanSu,QuanLyDuAn")]
        public async Task<IActionResult> AddChatMessage(int macongviec, string noidung)
        {
            if (string.IsNullOrWhiteSpace(noidung))
            {
                return RedirectToAction("TroChuyen", new { id = macongviec });
            }

            if (!User.IsInRole("Admin") && !await IsTaskAssignedToCurrentEmployeeAsync(macongviec))
            {
                return Forbid();
            }

            if (await IsPersonalTaskAsync(macongviec))
            {
                TempData["UploadError"] = "Công việc cá nhân không có khu vực trò chuyện nhóm/công việc.";
                return RedirectToAction("Details", new { id = macongviec });
            }

            var username = User.Identity?.Name;

            var user = await _context.Users
                .Include(u => u.Nhanvien)
                .FirstOrDefaultAsync(u => u.UserName == username || u.Email == username);

            if (user?.Nhanvien == null)
            {
                return RedirectToAction("TroChuyen", new { id = macongviec });
            }

            var comment = new Binhluan
            {
                Macongviec = macongviec,
                Manhanvien = user.Nhanvien.Manhanvien,
                Noidung = noidung.Trim(),
                Ngaytao = DateTime.Now
            };

            _context.Binhluans.Add(comment);
            await _context.SaveChangesAsync();

            return RedirectToAction("TroChuyen", new { id = macongviec });
        }

        private bool CongviecExists(int id)
        {
            return _context.Congviecs.Any(e => e.Macongviec == id);
        }

        private async Task<bool> IsTaskAssignedToCurrentEmployeeAsync(int taskId)
        {
            if (User.IsInRole("Admin"))
            {
                return true;
            }

            var currentUserName = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(currentUserName))
            {
                return false;
            }

            var currentNhanVienId = await _context.Users
                .Where(u => u.UserName == currentUserName || u.Email == currentUserName)
                .Select(u => u.MaNhanVien)
                .FirstOrDefaultAsync();

            if (!currentNhanVienId.HasValue)
            {
                return false;
            }

            var groupIds = await _context.Thanhviennhoms
                .Where(tv => tv.Manhanvien == currentNhanVienId.Value)
                .Select(tv => tv.Manhom)
                .Distinct()
                .ToListAsync();

            var departmentId = await _context.Nhanviens
                .Where(nv => nv.Manhanvien == currentNhanVienId.Value)
                .Select(nv => nv.Maphongban)
                .FirstOrDefaultAsync();

            return await _context.Phancongcongviecs.AnyAsync(pc =>
                pc.Macongviec == taskId &&
                ((pc.Manhanvien.HasValue && pc.Manhanvien.Value == currentNhanVienId.Value) ||
                 (pc.Manhom.HasValue && groupIds.Contains(pc.Manhom.Value)) ||
                 (departmentId.HasValue && pc.Maphongban.HasValue && pc.Maphongban.Value == departmentId.Value)));
        }

        private async Task<bool> IsPersonalTaskAsync(int taskId)
        {
            var assignments = await _context.Phancongcongviecs
                .Where(pc => pc.Macongviec == taskId)
                .ToListAsync();

            if (assignments.Count == 0)
            {
                return true;
            }

            return assignments.All(pc => pc.Loaidoituong == "NhanVien")
                && assignments.Select(pc => pc.Manhanvien).Distinct().Count() == 1;
        }
    }
}



