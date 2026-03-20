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
using System.Security.Claims;
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
        private const string AssigneeStatusNotePrefix = "ASSIGNEE_STATUS";
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
        [Authorize(Roles = "Admin,TruongPhong,TruongNhom,QuanLyPhongBan")]
        public async Task<IActionResult> Index()
        {
            IQueryable<Congviec> query = _context.Congviecs
                .Include(c => c.MaduanNavigation)
                .Include(c => c.MatrangthaiNavigation);

            if (User.IsInRole("Admin") || User.IsInRole("QuanLyPhongBan"))
            {
                return View(await query.ToListAsync());
            }

            var currentUserId = await GetCurrentNhanVienIdAsync();
            if (!currentUserId.HasValue)
            {
                return Forbid();
            }

            var leadGroupIds = User.IsInRole("TruongNhom")
                ? await _context.Thanhviennhoms
                    .Where(tv => tv.Manhanvien == currentUserId.Value &&
                        (tv.Vaitrotrongnhom == "TruongNhom" || tv.Vaitrotrongnhom == "Trưởng nhóm" || tv.Vaitrotrongnhom == "Trưởng Nhóm"))
                    .Select(tv => tv.Manhom)
                    .Distinct()
                    .ToListAsync()
                : new List<int>();

            var leadDepartmentIds = User.IsInRole("TruongPhong")
                ? await _context.Phongbans
                    .Where(pb => pb.Matruongphong == currentUserId.Value)
                    .Select(pb => pb.Maphongban)
                    .Distinct()
                    .ToListAsync()
                : new List<int>();

            if (User.IsInRole("TruongPhong") && !leadDepartmentIds.Any())
            {
                var ownDepartmentId = await _context.Nhanviens
                    .Where(nv => nv.Manhanvien == currentUserId.Value)
                    .Select(nv => nv.Maphongban)
                    .FirstOrDefaultAsync();

                if (ownDepartmentId.HasValue)
                {
                    leadDepartmentIds.Add(ownDepartmentId.Value);
                }
            }

            var employeeIdsInLeadGroups = leadGroupIds.Count == 0
                ? new List<int>()
                : await _context.Thanhviennhoms
                    .Where(tv => leadGroupIds.Contains(tv.Manhom))
                    .Select(tv => tv.Manhanvien)
                    .Distinct()
                    .ToListAsync();

            var employeeIdsInLeadDepartments = leadDepartmentIds.Count == 0
                ? new List<int>()
                : await _context.Nhanviens
                    .Where(nv => nv.Maphongban != null && leadDepartmentIds.Contains(nv.Maphongban.Value))
                    .Select(nv => nv.Manhanvien)
                    .Distinct()
                    .ToListAsync();

            query = query.Where(cv => cv.Phancongcongviecs.Any(pc =>
                (pc.Loaidoituong == "Nhom" && pc.Manhom.HasValue && leadGroupIds.Contains(pc.Manhom.Value)) ||
                (pc.Loaidoituong == "PhongBan" && pc.Maphongban.HasValue && leadDepartmentIds.Contains(pc.Maphongban.Value)) ||
                (pc.Loaidoituong == "NhanVien" && pc.Manhanvien.HasValue &&
                    (employeeIdsInLeadGroups.Contains(pc.Manhanvien.Value) || employeeIdsInLeadDepartments.Contains(pc.Manhanvien.Value)))));

            return View(await query.ToListAsync());
        }

        // GET: Congviecs/Details/5
        [Authorize(Roles = "Admin,NhanVien,TruongPhong,TruongNhom,QuanLyNhanSu")]
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
                .Where(tv => tv.Manhanvien == currentUserId &&
                    (tv.Vaitrotrongnhom == "TruongNhom" || tv.Vaitrotrongnhom == "Trưởng nhóm" || tv.Vaitrotrongnhom == "Trưởng Nhóm"))
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
                .Where(cv => cv.Matrangthai == 4 || cv.Matrangthai == 5)
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
            var cv = await _context.Congviecs.FindAsync(id);

            if (cv == null)
            {
                return Json(new { success = false, message = "Không tìm thấy công việc." });
            }

            if (!await IsTaskAssignedToCurrentEmployeeAsync(id))
            {
                return Json(new { success = false, message = "Bạn không thuộc đối tượng được giao công việc này." });
            }

            var currentNhanVienId = await GetCurrentNhanVienIdAsync();
            if (!currentNhanVienId.HasValue)
            {
                return Json(new { success = false, message = "Không xác định được tài khoản nhân viên hiện tại." });
            }

            if (cv.Matrangthai == 3)
            {
                return Json(new { success = false, message = "Công việc đã được duyệt hoàn thành. Không thể cập nhật lại." });
            }

            var assigneeIds = await GetAssignedEmployeeIdsAsync(cv.Macongviec);
            var hasApprover = await TaskHasApproverAsync(cv.Macongviec);

            if (assigneeIds.Count > 0 && !assigneeIds.Contains(currentNhanVienId.Value))
            {
                return Json(new { success = false, message = "Bạn không thuộc danh sách nhân sự thực hiện công việc này." });
            }

            if (assigneeIds.Count == 0)
            {
                if (status)
                {
                    var approvedDirectly = !hasApprover || User.IsInRole("TruongPhong") || User.IsInRole("TruongNhom");
                    cv.Matrangthai = approvedDirectly ? 3 : 4;

                    if (approvedDirectly)
                    {
                        await NotifyTaskAssigneesAsync(cv, $"Công việc '{cv.Tencongviec}' đã được duyệt hoàn thành.");
                        await ApplyTaskOutcomeKpiAsync(cv, approved: true);
                    }
                }
                else
                {
                    cv.Matrangthai = 2;
                }

                cv.Trangthai = cv.Matrangthai;

                await AddTaskProgressLogsAsync(cv.Macongviec, new List<(double percent, string note)>
                {
                    (MapTaskStatusToPercent(cv.Matrangthai), "Cập nhật tiến độ theo trạng thái công việc.")
                });

                await _context.SaveChangesAsync();

                await CapNhatTienDoCongViecChaAsync(cv.Macongvieccha);
                await CapNhatTienDoDuAnAsync(cv.Maduan);

                await BroadcastProgressChangedAsync(cv);
                return Json(new { success = true });
            }

            var completionStates = await GetLatestAssigneeCompletionStatesAsync(cv.Macongviec, assigneeIds);
            completionStates[currentNhanVienId.Value] = status;

            var completedCount = completionStates.Count(x => x.Value);
            var totalCount = assigneeIds.Count;
            var progressPercent = Math.Round((double)completedCount * 100.0 / totalCount, 0);

            var allCompleted = completedCount == totalCount;
            var approvedDirectlyByLeader = allCompleted && (!hasApprover || User.IsInRole("TruongPhong") || User.IsInRole("TruongNhom"));

            if (status)
            {
                cv.Matrangthai = allCompleted
                    ? (approvedDirectlyByLeader ? 3 : 4)
                    : 2;

                if (approvedDirectlyByLeader)
                {
                    await NotifyTaskAssigneesAsync(cv, $"Công việc '{cv.Tencongviec}' đã được duyệt hoàn thành.");
                    await ApplyTaskOutcomeKpiAsync(cv, approved: true);
                }
            }
            else
            {
                cv.Matrangthai = 2; // đang làm
            }

            cv.Trangthai = cv.Matrangthai;

            var assigneeStatusNote = BuildAssigneeStatusNote(currentNhanVienId.Value, status);
            var summaryNote = $"Tiến độ theo nhân sự tham gia: {completedCount}/{totalCount} hoàn thành.";

            await AddTaskProgressLogsAsync(cv.Macongviec, new List<(double percent, string note)>
            {
                (progressPercent, assigneeStatusNote),
                (progressPercent, summaryNote)
            });

            await _context.SaveChangesAsync();

            await CapNhatTienDoCongViecChaAsync(cv.Macongvieccha);
            await CapNhatTienDoDuAnAsync(cv.Maduan);

            await BroadcastProgressChangedAsync(cv);

            return Json(new
            {
                success = true,
                message = $"Đã ghi nhận tiến độ {completedCount}/{totalCount} người hoàn thành.",
                progress = progressPercent,
                completedCount,
                totalCount
            });
        }

        private async Task<bool> TaskHasApproverAsync(int taskId)
        {
            var assignments = await _context.Phancongcongviecs
                .AsNoTracking()
                .Where(pc => pc.Macongviec == taskId)
                .Select(pc => new { pc.Manhom, pc.Maphongban, pc.Manhanvien })
                .ToListAsync();

            if (assignments.Count == 0)
            {
                return false;
            }

            var explicitGroupIds = assignments
                .Where(a => a.Manhom.HasValue)
                .Select(a => a.Manhom!.Value)
                .Distinct()
                .ToList();

            var explicitDepartmentIds = assignments
                .Where(a => a.Maphongban.HasValue)
                .Select(a => a.Maphongban!.Value)
                .Distinct()
                .ToList();

            var directEmployeeIds = assignments
                .Where(a => a.Manhanvien.HasValue)
                .Select(a => a.Manhanvien!.Value)
                .Distinct()
                .ToList();

            var employeeGroupIds = new List<int>();
            var employeeDepartmentIds = new List<int>();

            if (directEmployeeIds.Count > 0)
            {
                employeeGroupIds = await _context.Thanhviennhoms
                    .AsNoTracking()
                    .Where(tv => directEmployeeIds.Contains(tv.Manhanvien))
                    .Select(tv => tv.Manhom)
                    .Distinct()
                    .ToListAsync();

                employeeDepartmentIds = await _context.Nhanviens
                    .AsNoTracking()
                    .Where(nv => directEmployeeIds.Contains(nv.Manhanvien) && nv.Maphongban.HasValue)
                    .Select(nv => nv.Maphongban!.Value)
                    .Distinct()
                    .ToListAsync();
            }

            var allGroupIds = explicitGroupIds
                .Concat(employeeGroupIds)
                .Distinct()
                .ToList();

            if (allGroupIds.Count > 0)
            {
                var hasTeamLead = await _context.Thanhviennhoms
                    .AsNoTracking()
                    .AnyAsync(tv => allGroupIds.Contains(tv.Manhom)
                        && (tv.Vaitrotrongnhom == "TruongNhom"
                            || tv.Vaitrotrongnhom == "Trưởng nhóm"
                            || tv.Vaitrotrongnhom == "Trưởng Nhóm"));

                if (hasTeamLead)
                {
                    return true;
                }
            }

            var allDepartmentIds = explicitDepartmentIds
                .Concat(employeeDepartmentIds)
                .Distinct()
                .ToList();

            if (allDepartmentIds.Count > 0)
            {
                var hasDepartmentHead = await _context.Phongbans
                    .AsNoTracking()
                    .AnyAsync(pb => allDepartmentIds.Contains(pb.Maphongban) && pb.Matruongphong.HasValue);

                if (hasDepartmentHead)
                {
                    return true;
                }
            }

            return false;
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

            var wasApproved = cv.Matrangthai == 3;

            // ===== DUYỆT =====
            cv.Matrangthai = 3;
            cv.Trangthai = cv.Matrangthai;

            await AddTaskProgressLogsAsync(cv.Macongviec, new List<(double percent, string note)>
            {
                (100, "Đã được duyệt hoàn thành.")
            });

            await _context.SaveChangesAsync();

            if (!wasApproved)
            {
                await ApplyTaskOutcomeKpiAsync(cv, approved: true);
            }

            await NotifyTaskAssigneesAsync(cv, $"Công việc '{cv.Tencongviec}' đã được duyệt hoàn thành.");

            await CapNhatTienDoCongViecChaAsync(cv.Macongvieccha);
            await CapNhatTienDoDuAnAsync(cv.Maduan);

            await BroadcastProgressChangedAsync(cv);

            if (cv.Macongvieccha != null)
                return RedirectToAction("Details", new { id = cv.Macongvieccha });

            return RedirectToAction("Details", new { id = cv.Macongviec });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "TruongPhong,TruongNhom")]
        public async Task<IActionResult> Reject(int id, string? reason)
        {
            var cv = await _context.Congviecs.FindAsync(id);
            if (cv == null)
            {
                TempData["UploadError"] = "Không tìm thấy công việc.";
                return RedirectToAction(nameof(XacNhanTienDo));
            }

            var currentUserId = await GetCurrentNhanVienIdAsync();
            if (currentUserId == null || !await CanApproveTaskAsync(id, currentUserId.Value))
            {
                TempData["UploadError"] = "Bạn không có quyền không duyệt công việc này.";
                return RedirectToAction(nameof(XacNhanTienDo));
            }

            if (cv.Matrangthai != 4 && cv.Matrangthai != 5)
            {
                TempData["UploadError"] = "Chỉ có thể không duyệt công việc đang ở trạng thái chờ duyệt hoặc trễ.";
                return RedirectToAction(nameof(XacNhanTienDo));
            }

            var rejectStatusId = await GetRejectStatusIdAsync();
            if (!rejectStatusId.HasValue)
            {
                TempData["UploadError"] = "Chưa cấu hình trạng thái 'Không duyệt'.";
                return RedirectToAction(nameof(XacNhanTienDo));
            }

            var normalizedReason = string.IsNullOrWhiteSpace(reason)
                ? "Không đạt yêu cầu duyệt."
                : reason.Trim();

            cv.Matrangthai = rejectStatusId.Value;
            cv.Trangthai = cv.Matrangthai;

            var assigneeIds = await GetAssignedEmployeeIdsAsync(cv.Macongviec);
            var rejectLogs = assigneeIds
                .Select(employeeId => (percent: 0d, note: BuildAssigneeStatusNote(employeeId, false)))
                .ToList();

            rejectLogs.Add((0d, $"Không duyệt: {normalizedReason}"));
            await AddTaskProgressLogsAsync(cv.Macongviec, rejectLogs);

            await _context.SaveChangesAsync();

            await ApplyTaskOutcomeKpiAsync(cv, approved: false);

            await NotifyTaskAssigneesAsync(
                cv,
                $"Công việc '{cv.Tencongviec}' không được duyệt. Lý do: {normalizedReason}");

            await CapNhatTienDoCongViecChaAsync(cv.Macongvieccha);
            await CapNhatTienDoDuAnAsync(cv.Maduan);

            await BroadcastProgressChangedAsync(cv);

            TempData["UploadSuccess"] = "Đã chuyển công việc sang trạng thái không duyệt.";
            return RedirectToAction(nameof(XacNhanTienDo));
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
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrWhiteSpace(userId))
            {
                var nhanVienIdFromUserId = await _context.Users
                    .AsNoTracking()
                    .Where(u => u.Id == userId)
                    .Select(u => u.MaNhanVien)
                    .FirstOrDefaultAsync();

                if (nhanVienIdFromUserId.HasValue)
                {
                    return nhanVienIdFromUserId.Value;
                }
            }

            var userName = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(userName))
            {
                return null;
            }

            var nhanVienIdFromUserName = await _context.Users
                .AsNoTracking()
                .Where(u => u.UserName == userName || u.Email == userName)
                .Select(u => u.MaNhanVien)
                .FirstOrDefaultAsync();

            if (nhanVienIdFromUserName.HasValue)
            {
                return nhanVienIdFromUserName.Value;
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
                .Where(tv => tv.Manhanvien == currentUserId &&
                    (tv.Vaitrotrongnhom == "TruongNhom" || tv.Vaitrotrongnhom == "Trưởng nhóm" || tv.Vaitrotrongnhom == "Trưởng Nhóm"))
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
        private async Task CapNhatTienDoCongViecChaAsync(int? macongvieccha)
        {
            if (macongvieccha == null) return;

            var cha = await _context.Congviecs
                .Include(c => c.InverseConMacongviecNavigation)
                    .ThenInclude(con => con.Nhatkycongviecs)
                .FirstOrDefaultAsync(c => c.Macongviec == macongvieccha);

            if (cha == null) return;

            var conList = cha.InverseConMacongviecNavigation.ToList();

            if (conList.Count == 0) return;

            var progressValues = conList
                .Select(GetLatestTaskProgressPercent)
                .ToList();

            var percent = (int)Math.Round(progressValues.Average(), 0);

            cha.Tongcongvieccon = percent;

            if (percent >= 100)
            {
                cha.Matrangthai = 3;
            }
            else if (percent > 0)
            {
                cha.Matrangthai = 2;
            }
            else
            {
                cha.Matrangthai = 1;
            }

            cha.Trangthai = cha.Matrangthai;

            await AddTaskProgressLogsAsync(cha.Macongviec, new List<(double percent, string note)>
            {
                (percent, "Tiến độ tự động tổng hợp từ công việc con.")
            });

            await _context.SaveChangesAsync();

            await CapNhatTienDoCongViecChaAsync(cha.Macongvieccha);
        }


        private async Task CapNhatTienDoDuAnAsync(int maduan)
        {
            var duan = await _context.Duans
                .Include(d => d.Congviecs)
                    .ThenInclude(cv => cv.Nhatkycongviecs)
                .FirstOrDefaultAsync(d => d.Maduan == maduan);

            if (duan == null) return;

            int total = duan.Congviecs.Count;

            if (total == 0) return;

            int percent = (int)Math.Round(duan.Congviecs
                .Select(GetLatestTaskProgressPercent)
                .Average(), 0);

            if (percent == 100)
                duan.Trangthai = 3; // hoàn thành
            else if (percent > 0)
                duan.Trangthai = 2; // đang thực hiện
            else
                duan.Trangthai = 1; // chưa bắt đầu

            await _context.SaveChangesAsync();
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
        [Authorize(Roles = "Admin,NhanVien,TruongPhong,TruongNhom,QuanLyNhanSu")]
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

        [Authorize(Roles = "Admin,NhanVien,TruongPhong,TruongNhom,QuanLyNhanSu")]
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
        [Authorize(Roles = "Admin,NhanVien,TruongPhong,TruongNhom,QuanLyNhanSu")]
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

            int? currentNhanVienId = null;

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrWhiteSpace(currentUserId))
            {
                currentNhanVienId = await _context.Users
                    .Where(u => u.Id == currentUserId)
                    .Select(u => u.MaNhanVien)
                    .FirstOrDefaultAsync();
            }

            var currentUserName = User.Identity?.Name;

            if (!currentNhanVienId.HasValue && !string.IsNullOrWhiteSpace(currentUserName))
            {
                currentNhanVienId = await _context.Users
                    .Where(u => u.UserName == currentUserName || u.Email == currentUserName)
                    .Select(u => u.MaNhanVien)
                    .FirstOrDefaultAsync();
            }

            if (!currentNhanVienId.HasValue && !string.IsNullOrWhiteSpace(currentUserName))
            {
                currentNhanVienId = await _context.Nhanviens
                    .Where(nv => nv.Email == currentUserName)
                    .Select(nv => (int?)nv.Manhanvien)
                    .FirstOrDefaultAsync();
            }

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

        private async Task<int?> GetRejectStatusIdAsync()
        {
            var statusById = await _context.Trangthaicongviecs
                .AsNoTracking()
                .Where(t => t.Matrangthai == 6)
                .Select(t => (int?)t.Matrangthai)
                .FirstOrDefaultAsync();

            if (statusById.HasValue)
            {
                return statusById.Value;
            }

            var rejectedStatus = await _context.Trangthaicongviecs
                .AsNoTracking()
                .Where(t => t.Tentrangthai != null &&
                    (t.Tentrangthai == "Không duyệt" ||
                     t.Tentrangthai == "Khong duyet" ||
                     t.Tentrangthai == "Từ chối" ||
                     t.Tentrangthai == "Tu choi"))
                .Select(t => (int?)t.Matrangthai)
                .FirstOrDefaultAsync();

            if (rejectedStatus.HasValue)
            {
                return rejectedStatus.Value;
            }

            var fallbackStatus = await _context.Trangthaicongviecs
                .AsNoTracking()
                .Where(t => t.Matrangthai == 6)
                .Select(t => (int?)t.Matrangthai)
                .FirstOrDefaultAsync();

            return fallbackStatus;
        }

        private async Task<List<int>> GetAssignedEmployeeIdsAsync(int taskId)
        {
            var assignments = await _context.Phancongcongviecs
                .Where(pc => pc.Macongviec == taskId)
                .Select(pc => new { pc.Manhanvien, pc.Manhom, pc.Maphongban })
                .ToListAsync();

            var employeeIds = assignments
                .Where(a => a.Manhanvien.HasValue)
                .Select(a => a.Manhanvien!.Value)
                .Distinct()
                .ToList();

            var groupIds = assignments
                .Where(a => a.Manhom.HasValue)
                .Select(a => a.Manhom!.Value)
                .Distinct()
                .ToList();

            if (groupIds.Count > 0)
            {
                var groupEmployeeIds = await _context.Thanhviennhoms
                    .Where(tv => groupIds.Contains(tv.Manhom))
                    .Select(tv => tv.Manhanvien)
                    .Distinct()
                    .ToListAsync();

                employeeIds.AddRange(groupEmployeeIds);
            }

            var departmentIds = assignments
                .Where(a => a.Maphongban.HasValue)
                .Select(a => a.Maphongban!.Value)
                .Distinct()
                .ToList();

            if (departmentIds.Count > 0)
            {
                var departmentEmployeeIds = await _context.Nhanviens
                    .Where(nv => nv.Maphongban.HasValue && departmentIds.Contains(nv.Maphongban.Value))
                    .Select(nv => nv.Manhanvien)
                    .Distinct()
                    .ToListAsync();

                employeeIds.AddRange(departmentEmployeeIds);
            }

            return employeeIds.Distinct().ToList();
        }

        private async Task NotifyTaskAssigneesAsync(Congviec task, string content)
        {
            var employeeIds = await GetAssignedEmployeeIdsAsync(task.Macongviec);
            if (employeeIds.Count == 0)
            {
                return;
            }

            var employees = await _context.Nhanviens
                .Where(nv => employeeIds.Contains(nv.Manhanvien))
                .ToListAsync();

            if (employees.Count == 0)
            {
                return;
            }

            var notification = new Thongbao
            {
                Noidung = content,
                Dadoc = false,
                Thoigian = DateTime.Now,
                Manhanviens = employees
            };

            _context.Thongbaos.Add(notification);
            await _context.SaveChangesAsync();
        }

        private static string BuildAssigneeStatusNote(int employeeId, bool completed)
        {
            return $"{AssigneeStatusNotePrefix}|employeeId={employeeId}|completed={(completed ? 1 : 0)}";
        }

        private bool TryParseAssigneeStatusNote(string? note, out int employeeId, out bool completed)
        {
            employeeId = 0;
            completed = false;

            if (string.IsNullOrWhiteSpace(note) || !note.StartsWith(AssigneeStatusNotePrefix + "|", StringComparison.Ordinal))
            {
                return false;
            }

            var segments = note.Split('|', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 3)
            {
                return false;
            }

            var employeeSegment = segments.FirstOrDefault(s => s.StartsWith("employeeId=", StringComparison.Ordinal));
            var completedSegment = segments.FirstOrDefault(s => s.StartsWith("completed=", StringComparison.Ordinal));

            if (employeeSegment == null || completedSegment == null)
            {
                return false;
            }

            if (!int.TryParse(employeeSegment["employeeId=".Length..], out employeeId))
            {
                return false;
            }

            completed = completedSegment["completed=".Length..] == "1";
            return true;
        }

        private async Task<Dictionary<int, bool>> GetLatestAssigneeCompletionStatesAsync(int taskId, List<int> assigneeIds)
        {
            var states = assigneeIds.Distinct().ToDictionary(id => id, _ => false);
            if (states.Count == 0)
            {
                return states;
            }

            var logs = await _context.Nhatkycongviecs
                .AsNoTracking()
                .Where(nk => nk.Macongviec == taskId && nk.Ghichu != null)
                .OrderByDescending(nk => nk.Ngaycapnhat)
                .ThenByDescending(nk => nk.Manhatkycongviec)
                .ToListAsync();

            var seen = new HashSet<int>();
            foreach (var log in logs)
            {
                if (!TryParseAssigneeStatusNote(log.Ghichu, out var employeeId, out var completed))
                {
                    continue;
                }

                if (!states.ContainsKey(employeeId) || seen.Contains(employeeId))
                {
                    continue;
                }

                states[employeeId] = completed;
                seen.Add(employeeId);

                if (seen.Count == states.Count)
                {
                    break;
                }
            }

            return states;
        }

        private async Task AddTaskProgressLogsAsync(int taskId, List<(double percent, string note)> entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return;
            }

            var now = DateTime.Now;

            foreach (var entry in entries)
            {
                _context.Nhatkycongviecs.Add(new Nhatkycongviec
                {
                    Macongviec = taskId,
                    Phantramhoanthanh = Math.Max(0, Math.Min(100, entry.percent)),
                    Ghichu = entry.note,
                    Ngaycapnhat = now
                });
            }
        }

        private int GetLatestTaskProgressPercent(Congviec task)
        {
            var latestFromLog = task.Nhatkycongviecs
                .OrderByDescending(nk => nk.Ngaycapnhat)
                .ThenByDescending(nk => nk.Manhatkycongviec)
                .Select(nk => nk.Phantramhoanthanh)
                .FirstOrDefault();

            if (latestFromLog.HasValue)
            {
                return (int)Math.Round(latestFromLog.Value, 0);
            }

            return MapTaskStatusToPercent(task.Matrangthai);
        }

        private static int MapTaskStatusToPercent(int statusId)
        {
            return statusId switch
            {
                3 => 100,
                4 => 90,
                2 => 50,
                _ => 0
            };
        }

        private async Task ApplyTaskOutcomeKpiAsync(Congviec task, bool approved)
        {
            var assigneeIds = await GetAssignedEmployeeIdsAsync(task.Macongviec);
            if (assigneeIds.Count == 0)
            {
                return;
            }

            await EnsureKpiCategoryWeightsAsync();

            var now = DateTime.Now;
            var month = now.Month;
            var year = now.Year;

            var deadline = task.Hanhoanthanh?.Date;
            var today = now.Date;
            var daysEarly = 0;
            var daysLate = 0;

            if (deadline.HasValue)
            {
                var diff = (deadline.Value - today).Days;
                if (diff >= 0)
                {
                    daysEarly = diff;
                }
                else
                {
                    daysLate = -diff;
                }
            }

            var progressBase = approved ? 100d : 40d;
            var qualityBase = approved ? 90d : 50d;
            var disciplineBase = approved ? 90d : 45d;

            if (daysEarly > 0)
            {
                var bonus = Math.Min(10d, daysEarly * 1.5d);
                progressBase += bonus;
                qualityBase += bonus * 0.6d;
                disciplineBase += bonus * 0.5d;
            }

            if (daysLate > 0)
            {
                var penalty = Math.Min(25d, daysLate * 2d);
                progressBase -= penalty;
                qualityBase -= penalty * 0.7d;
                disciplineBase -= penalty * 0.8d;
            }

            var progressScore = Math.Max(0, Math.Min(100, progressBase));
            var qualityScore = Math.Max(0, Math.Min(100, qualityBase));
            var disciplineScore = Math.Max(0, Math.Min(100, disciplineBase));

            var employees = await _context.Nhanviens
                .Where(nv => assigneeIds.Contains(nv.Manhanvien))
                .ToListAsync();

            if (employees.Count == 0)
            {
                return;
            }

            var targetCategoryScores = new Dictionary<int, double>
            {
                [1] = progressScore,
                [2] = qualityScore,
                [3] = disciplineScore,
                [901] = progressScore,
                [902] = qualityScore,
                [903] = disciplineScore
            };

            var categories = await _context.Danhmuckpis
                .Where(dm => targetCategoryScores.Keys.Contains(dm.Madoanhmuc))
                .ToListAsync();

            if (categories.Count == 0)
            {
                return;
            }

            var nextResultId = (await _context.Ketquakpis.MaxAsync(k => (int?)k.Maketqua) ?? 0) + 1;

            foreach (var employee in employees)
            {
                foreach (var category in categories)
                {
                    var existing = await _context.Ketquakpis
                        .FirstOrDefaultAsync(k =>
                            k.Manhanvien == employee.Manhanvien &&
                            k.Madoanhmuc == category.Madoanhmuc &&
                            k.Thang == month &&
                            k.Nam == year);

                    var score = targetCategoryScores[category.Madoanhmuc];

                    if (existing == null)
                    {
                        _context.Ketquakpis.Add(new Ketquakpi
                        {
                            Maketqua = nextResultId++,
                            Manhanvien = employee.Manhanvien,
                            Madoanhmuc = category.Madoanhmuc,
                            Diemso = score,
                            Thang = month,
                            Nam = year
                        });
                    }
                    else
                    {
                        existing.Diemso = Math.Round(((existing.Diemso ?? score) + score) / 2, 2);
                    }
                }

                var monthlyMainKpi = await _context.Ketquakpis
                    .Where(k =>
                        k.Manhanvien == employee.Manhanvien &&
                        k.Thang == month &&
                        k.Nam == year &&
                        (k.Madoanhmuc == 1 || k.Madoanhmuc == 2 || k.Madoanhmuc == 3))
                    .Join(_context.Danhmuckpis,
                        k => k.Madoanhmuc,
                        dm => dm.Madoanhmuc,
                        (k, dm) => new { k.Diemso, dm.Trongso })
                    .ToListAsync();

                var totalWeight = monthlyMainKpi.Sum(x => x.Trongso ?? 0);
                var weightedAverage = totalWeight > 0
                    ? monthlyMainKpi.Sum(x => (x.Diemso ?? 0) * (x.Trongso ?? 0)) / totalWeight
                    : monthlyMainKpi.Select(x => x.Diemso ?? 0).DefaultIfEmpty(0).Average();

                var scoreDelta = 0d;
                if (approved)
                {
                    scoreDelta += daysLate > 0 ? -2d : 2d;
                    scoreDelta += Math.Min(3d, daysEarly / 3d);
                    scoreDelta -= Math.Min(3d, daysLate / 2d);
                }
                else
                {
                    scoreDelta = -3d - Math.Min(2d, daysLate / 3d);
                }

                var baselineAdjustment = (weightedAverage - 70d) / 50d;
                scoreDelta += baselineAdjustment;

                employee.DiemkpiTichluy = Math.Round((employee.DiemkpiTichluy ?? 50d) + scoreDelta, 2);
            }

            await _context.SaveChangesAsync();
        }

        private async Task EnsureKpiCategoryWeightsAsync()
        {
            var targetWeights = new Dictionary<int, double>
            {
                [1] = 0.4,
                [2] = 0.4,
                [3] = 0.2,
                [901] = 0.4,
                [902] = 0.35,
                [903] = 0.25
            };

            var categories = await _context.Danhmuckpis
                .Where(dm => targetWeights.Keys.Contains(dm.Madoanhmuc))
                .ToListAsync();

            var hasChanges = false;
            foreach (var category in categories)
            {
                if (Math.Abs((category.Trongso ?? 0d) - targetWeights[category.Madoanhmuc]) > 0.0001d)
                {
                    category.Trongso = targetWeights[category.Madoanhmuc];
                    hasChanges = true;
                }
            }

            if (hasChanges)
            {
                await _context.SaveChangesAsync();
            }
        }
    }
}



