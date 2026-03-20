using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_HieuSuat.Models;
using QL_HieuSuat.Models.ViewModels;
using System.Globalization;
using System.Text;

namespace QL_HieuSuat.Controllers
{
    [Authorize(Roles = "Admin,NhanVien,TruongPhong,TruongNhom,QuanLyNhanSu")]
    public class NhanVienController : Controller
    {
        private sealed class KpiPeriodGroup
        {
            public int? Thang { get; set; }

            public int? Nam { get; set; }

            public List<Ketquakpi> Items { get; set; } = new();
        }

        private readonly ApplicationDbContext _context;

        public NhanVienController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return RedirectToAction(nameof(ThongTinVaXepLoai));
        }

        public async Task<IActionResult> ThongTinVaXepLoai()
        {
            var currentContext = await GetCurrentNhanVienContextAsync();
            if (!currentContext.NhanVienId.HasValue || currentContext.NhanVien == null)
            {
                TempData["UploadError"] = "Không tìm thấy hồ sơ nhân viên cho tài khoản hiện tại.";
                return RedirectToAction("AccessDenied", "Account");
            }

            var nhanVien = await _context.Nhanviens
                .AsNoTracking()
                .Include(nv => nv.MaphongbanNavigation)
                .Include(nv => nv.Thanhviennhoms)
                    .ThenInclude(tv => tv.ManhomNavigation)
                .Include(nv => nv.Kynangnhanviens)
                    .ThenInclude(kn => kn.MakynangNavigation)
                .FirstOrDefaultAsync(nv => nv.Manhanvien == currentContext.NhanVienId.Value);

            if (nhanVien == null)
            {
                TempData["UploadError"] = "Không tìm thấy hồ sơ nhân viên.";
                return RedirectToAction("AccessDenied", "Account");
            }

            var groupedKpis = await GetGroupedKpiByPeriodAsync(currentContext.NhanVienId.Value);
            var latestGroup = groupedKpis
                .OrderByDescending(g => g.Nam ?? 0)
                .ThenByDescending(g => g.Thang ?? 0)
                .FirstOrDefault();

            var latestScore = latestGroup is null ? null : CalculateWeightedScore(latestGroup.Items);

            var model = new NhanVienThongTinXepLoaiViewModel
            {
                HoSoNhanVien = nhanVien,
                KyDanhGiaGanNhat = latestGroup is null ? null : BuildPeriodLabel(latestGroup.Thang, latestGroup.Nam),
                DiemDanhGiaGanNhat = latestScore,
                XepLoaiGanNhat = latestScore.HasValue ? XepLoaiFromScore(latestScore.Value) : "Chưa có dữ liệu",
                ChiTietDanhGiaGanNhat = latestGroup?.Items
                    .OrderByDescending(x => x.MadoanhmucNavigation.Trongso ?? 0)
                    .ThenBy(x => x.MadoanhmucNavigation.Tendoanhmuc)
                    .Select(x => new NhanVienKpiChiTietViewModel
                    {
                        TenDanhMuc = x.MadoanhmucNavigation.Tendoanhmuc,
                        DiemSo = NormalizeScore(x.Diemso),
                        TrongSo = x.MadoanhmucNavigation.Trongso
                    })
                    .ToList() ?? new List<NhanVienKpiChiTietViewModel>()
            };

            return View(model);
        }

        public async Task<IActionResult> DuAnCongViecDangThamGia()
        {
            var currentContext = await GetCurrentNhanVienContextAsync();
            if (!currentContext.NhanVienId.HasValue)
            {
                TempData["UploadError"] = "Không tìm thấy hồ sơ nhân viên cho tài khoản hiện tại.";
                return RedirectToAction("AccessDenied", "Account");
            }

            var baseData = await GetAssignedTaskBaseAsync(currentContext.NhanVienId.Value, currentContext.DepartmentId);
            var activeTasks = baseData.Tasks
                .Where(t => t.Matrangthai != 3 && (t.MaduanNavigation?.Trangthai ?? 0) != 3)
                .ToList();

            var model = new NhanVienDuAnDangThamGiaViewModel
            {
                TenNhanVien = currentContext.NhanVien?.Hoten,
                DuAnDangThamGia = BuildProjectSummaries(activeTasks)
            };

            return View(model);
        }

        public async Task<IActionResult> DuAnDaThamGia()
        {
            var currentContext = await GetCurrentNhanVienContextAsync();
            if (!currentContext.NhanVienId.HasValue)
            {
                TempData["UploadError"] = "Không tìm thấy hồ sơ nhân viên cho tài khoản hiện tại.";
                return RedirectToAction("AccessDenied", "Account");
            }

            var baseData = await GetAssignedTaskBaseAsync(currentContext.NhanVienId.Value, currentContext.DepartmentId);
            var today = DateTime.Today;
            var completedProjects = baseData.Tasks
                .Where(t => t.MaduanNavigation != null &&
                            (t.MaduanNavigation.Trangthai == 3 ||
                             (t.MaduanNavigation.Ngayketthuc.HasValue && t.MaduanNavigation.Ngayketthuc.Value.Date < today)))
                .ToList();

            var model = new NhanVienDuAnDaThamGiaViewModel
            {
                TenNhanVien = currentContext.NhanVien?.Hoten,
                DuAnDaThamGia = BuildProjectSummaries(completedProjects)
            };

            return View(model);
        }

        public async Task<IActionResult> DanhGiaCaNhan(string? ky)
        {
            var currentContext = await GetCurrentNhanVienContextAsync();
            if (!currentContext.NhanVienId.HasValue)
            {
                TempData["UploadError"] = "Không tìm thấy hồ sơ nhân viên cho tài khoản hiện tại.";
                return RedirectToAction("AccessDenied", "Account");
            }

            var groupedKpis = await GetGroupedKpiByPeriodAsync(currentContext.NhanVienId.Value);

            var history = groupedKpis
                .OrderByDescending(g => g.Nam ?? 0)
                .ThenByDescending(g => g.Thang ?? 0)
                .Select(g =>
                {
                    var score = CalculateWeightedScore(g.Items) ?? 0;
                    return new NhanVienDanhGiaKyViewModel
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

            var model = new NhanVienDanhGiaCaNhanViewModel
            {
                TenNhanVien = currentContext.NhanVien?.Hoten,
                LichSuDanhGia = history,
                KyDangXem = selectedGroup is null ? null : BuildPeriodLabel(selectedGroup.Thang, selectedGroup.Nam),
                DiemKyDangXem = selectedScore,
                XepLoaiKyDangXem = selectedScore.HasValue ? XepLoaiFromScore(selectedScore.Value) : "Chưa có dữ liệu",
                ChiTietKyDangXem = selectedGroup?.Items
                    .OrderByDescending(x => x.MadoanhmucNavigation.Trongso ?? 0)
                    .ThenBy(x => x.MadoanhmucNavigation.Tendoanhmuc)
                    .Select(x => new NhanVienKpiChiTietViewModel
                    {
                        TenDanhMuc = x.MadoanhmucNavigation.Tendoanhmuc,
                        DiemSo = NormalizeScore(x.Diemso),
                        TrongSo = x.MadoanhmucNavigation.Trongso
                    })
                    .ToList() ?? new List<NhanVienKpiChiTietViewModel>(),
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

        [Authorize(Roles = "TruongPhong,TruongNhom")]
        public async Task<IActionResult> ThongTinDonViPhuTrach()
        {
            var currentContext = await GetCurrentNhanVienContextAsync();
            if (!currentContext.NhanVienId.HasValue)
            {
                TempData["UploadError"] = "Không tìm thấy hồ sơ nhân viên cho tài khoản hiện tại.";
                return RedirectToAction("AccessDenied", "Account");
            }

            var managedScope = await GetManagedScopeAsync(currentContext.NhanVienId.Value);
            var managerName = currentContext.NhanVien?.Hoten;
            if (string.IsNullOrWhiteSpace(managerName))
            {
                managerName = await _context.Nhanviens
                    .AsNoTracking()
                    .Where(nv => nv.Manhanvien == currentContext.NhanVienId.Value)
                    .Select(nv => nv.Hoten)
                    .FirstOrDefaultAsync();
            }

            var model = new NhanVienThongTinDonViQuanLyViewModel
            {
                TenQuanLy = managerName,
                DonViPhuTrach = managedScope.ManagedUnits,
                TongNhanVienQuanLy = managedScope.ManagedEmployeeIds.Count
            };

            return View(model);
        }

        [Authorize(Roles = "TruongPhong,TruongNhom")]
        public async Task<IActionResult> DanhGiaDonViQuanLy(string? ky)
        {
            var currentContext = await GetCurrentNhanVienContextAsync();
            if (!currentContext.NhanVienId.HasValue)
            {
                TempData["UploadError"] = "Không tìm thấy hồ sơ nhân viên cho tài khoản hiện tại.";
                return RedirectToAction("AccessDenied", "Account");
            }

            var managedScope = await GetManagedScopeAsync(currentContext.NhanVienId.Value);
            var managerName = currentContext.NhanVien?.Hoten;
            if (string.IsNullOrWhiteSpace(managerName))
            {
                managerName = await _context.Nhanviens
                    .AsNoTracking()
                    .Where(nv => nv.Manhanvien == currentContext.NhanVienId.Value)
                    .Select(nv => nv.Hoten)
                    .FirstOrDefaultAsync();
            }

            var emptyModel = new NhanVienDanhGiaDonViQuanLyViewModel
            {
                TenQuanLy = managerName,
                KyDangXem = null,
                DanhSachKy = new List<string>(),
                DanhGiaNhanVien = new List<NhanVienDanhGiaDonViItemViewModel>()
            };

            if (!managedScope.ManagedEmployeeIds.Any())
            {
                return View(emptyModel);
            }

            var managedNhanViens = await _context.Nhanviens
                .AsNoTracking()
                .Include(nv => nv.MaphongbanNavigation)
                .Where(nv => managedScope.ManagedEmployeeIds.Contains(nv.Manhanvien))
                .ToListAsync();

            var managedGroupMemberships = await _context.Thanhviennhoms
                .AsNoTracking()
                .Include(tv => tv.ManhomNavigation)
                .Where(tv => managedScope.ManagedGroupIds.Contains(tv.Manhom) && managedScope.ManagedEmployeeIds.Contains(tv.Manhanvien))
                .ToListAsync();

            var managedGroupNamesByEmployee = managedGroupMemberships
                .GroupBy(tv => tv.Manhanvien)
                .ToDictionary(
                    g => g.Key,
                    g => string.Join(", ", g
                        .Select(x => x.ManhomNavigation?.Tennhom)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct()
                        .OrderBy(x => x)));

            var kpiData = await _context.Ketquakpis
                .Where(k => managedScope.ManagedEmployeeIds.Contains(k.Manhanvien))
                .Include(k => k.MadoanhmucNavigation)
                .AsNoTracking()
                .ToListAsync();

            var periods = kpiData
                .GroupBy(k => BuildPeriodCode(k.Thang, k.Nam))
                .Select(g => g.Key)
                .OrderByDescending(x => x)
                .ToList();

            var selectedPeriod = periods.FirstOrDefault(p => string.Equals(p, ky, StringComparison.OrdinalIgnoreCase))
                ?? periods.FirstOrDefault();

            var kpiByNhanVien = string.IsNullOrWhiteSpace(selectedPeriod)
                ? new Dictionary<int, List<Ketquakpi>>()
                : kpiData
                    .Where(k => BuildPeriodCode(k.Thang, k.Nam) == selectedPeriod)
                    .GroupBy(k => k.Manhanvien)
                    .ToDictionary(g => g.Key, g => g.ToList());

            var danhGiaItems = managedNhanViens
                .OrderBy(nv => nv.Hoten)
                .Select(nv =>
                {
                    kpiByNhanVien.TryGetValue(nv.Manhanvien, out var items);
                    var score = items is null ? null : CalculateWeightedScore(items);
                    var inManagedDepartment = nv.Maphongban.HasValue && managedScope.ManagedDepartmentIds.Contains(nv.Maphongban.Value);
                    managedGroupNamesByEmployee.TryGetValue(nv.Manhanvien, out var managedGroupNames);
                    var inManagedGroup = !string.IsNullOrWhiteSpace(managedGroupNames);

                    return new NhanVienDanhGiaDonViItemViewModel
                    {
                        MaNhanVien = nv.Manhanvien,
                        HoTen = nv.Hoten ?? $"NV {nv.Manhanvien}",
                        TenPhongBan = nv.MaphongbanNavigation?.Tenphongban,
                        CoTrongPhongQuanLy = inManagedDepartment,
                        CoTrongNhomQuanLy = inManagedGroup,
                        DanhSachNhomQuanLy = managedGroupNames ?? string.Empty,
                        DiemTongHop = score,
                        XepLoai = score.HasValue ? XepLoaiFromScore(score.Value) : "Chưa có dữ liệu"
                    };
                })
                .ToList();

            var model = new NhanVienDanhGiaDonViQuanLyViewModel
            {
                TenQuanLy = managerName,
                KyDangXem = selectedPeriod,
                DanhSachKy = periods,
                DanhGiaNhanVien = danhGiaItems
            };

            return View(model);
        }

        public IActionResult ThongTinCaNhan()
        {
            return RedirectToAction(nameof(ThongTinVaXepLoai));
        }

        private async Task<(List<int> ManagedEmployeeIds, List<int> ManagedDepartmentIds, List<int> ManagedGroupIds, List<NhanVienDonViQuanLyItemViewModel> ManagedUnits)> GetManagedScopeAsync(int nhanVienId)
        {
            var managedUnitItems = new List<NhanVienDonViQuanLyItemViewModel>();
            var managedEmployeeIds = new HashSet<int>();
            var managedDepartmentIds = new HashSet<int>();
            var managedGroupIds = new HashSet<int>();

            if (User.IsInRole("TruongPhong"))
            {
                var managedDepartments = await _context.Phongbans
                    .AsNoTracking()
                    .Include(pb => pb.Nhanviens)
                    .Where(pb => pb.Matruongphong == nhanVienId)
                    .ToListAsync();

                foreach (var department in managedDepartments)
                {
                    managedDepartmentIds.Add(department.Maphongban);

                    var departmentMembers = department.Nhanviens
                        .OrderBy(nv => nv.Hoten)
                        .Select(nv => new NhanVienDonViThanhVienViewModel
                        {
                            MaNhanVien = nv.Manhanvien,
                            HoTen = nv.Hoten ?? $"NV {nv.Manhanvien}",
                            Email = nv.Email,
                            SoDienThoai = nv.Sdt,
                            TenPhongBan = department.Tenphongban,
                            VaiTroTrongDonVi = department.Matruongphong == nv.Manhanvien ? "Trưởng phòng" : "Nhân viên phòng"
                        })
                        .ToList();

                    managedUnitItems.Add(new NhanVienDonViQuanLyItemViewModel
                    {
                        LoaiDonVi = "Phòng ban",
                        MaDonVi = department.Maphongban,
                        TenDonVi = department.Tenphongban ?? $"Phòng {department.Maphongban}",
                        SoNhanVien = department.Nhanviens.Count,
                        ThanhVien = departmentMembers
                    });

                    foreach (var employee in department.Nhanviens)
                    {
                        managedEmployeeIds.Add(employee.Manhanvien);
                    }
                }
            }

            if (User.IsInRole("TruongNhom"))
            {
                var ownMemberships = await _context.Thanhviennhoms
                    .AsNoTracking()
                    .Where(tv => tv.Manhanvien == nhanVienId)
                    .ToListAsync();

                var explicitLeadGroupIds = ownMemberships
                    .Where(tv => HasLeadKeyword(tv.Vaitrotrongnhom))
                    .Select(tv => tv.Manhom)
                    .Distinct()
                    .ToList();

                var scopedGroupIds = explicitLeadGroupIds.Any()
                    ? explicitLeadGroupIds
                    : ownMemberships.Select(tv => tv.Manhom).Distinct().ToList();

                if (scopedGroupIds.Any())
                {
                    var managedGroups = await _context.Nhoms
                        .AsNoTracking()
                        .Include(n => n.Thanhviennhoms)
                            .ThenInclude(tv => tv.ManhanvienNavigation)
                                .ThenInclude(nv => nv.MaphongbanNavigation)
                        .Where(n => scopedGroupIds.Contains(n.Manhom))
                        .ToListAsync();

                    foreach (var group in managedGroups)
                    {
                        managedGroupIds.Add(group.Manhom);

                        var groupMembers = group.Thanhviennhoms
                            .Where(tv => tv.ManhanvienNavigation != null)
                            .OrderBy(tv => tv.ManhanvienNavigation!.Hoten)
                            .Select(tv => new NhanVienDonViThanhVienViewModel
                            {
                                MaNhanVien = tv.Manhanvien,
                                HoTen = tv.ManhanvienNavigation?.Hoten ?? $"NV {tv.Manhanvien}",
                                Email = tv.ManhanvienNavigation?.Email,
                                SoDienThoai = tv.ManhanvienNavigation?.Sdt,
                                TenPhongBan = tv.ManhanvienNavigation?.MaphongbanNavigation?.Tenphongban,
                                VaiTroTrongDonVi = string.IsNullOrWhiteSpace(tv.Vaitrotrongnhom) ? "Thành viên nhóm" : tv.Vaitrotrongnhom
                            })
                            .ToList();

                        if (!managedUnitItems.Any(x => x.LoaiDonVi == "Nhóm" && x.MaDonVi == group.Manhom))
                        {
                            managedUnitItems.Add(new NhanVienDonViQuanLyItemViewModel
                            {
                                LoaiDonVi = "Nhóm",
                                MaDonVi = group.Manhom,
                                TenDonVi = group.Tennhom ?? $"Nhóm {group.Manhom}",
                                SoNhanVien = group.Thanhviennhoms.Count,
                                ThanhVien = groupMembers
                            });
                        }

                        foreach (var member in group.Thanhviennhoms)
                        {
                            managedEmployeeIds.Add(member.Manhanvien);
                        }
                    }
                }
            }

            return (
                managedEmployeeIds.ToList(),
                managedDepartmentIds.ToList(),
                managedGroupIds.ToList(),
                managedUnitItems.OrderBy(x => x.LoaiDonVi).ThenBy(x => x.TenDonVi).ToList());
        }

        private async Task<(ApplicationUser? AppUser, Nhanvien? NhanVien, int? NhanVienId, int? DepartmentId)> GetCurrentNhanVienContextAsync()
        {
            var currentUserName = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(currentUserName))
            {
                return (null, null, null, null);
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

            return (appUser, nhanVien, nhanVienId, nhanVien?.Maphongban);
        }

        private async Task<(List<int> AssignedTaskIds, List<Congviec> Tasks)> GetAssignedTaskBaseAsync(int nhanVienId, int? departmentId)
        {
            var groupIds = await _context.Thanhviennhoms
                .Where(tv => tv.Manhanvien == nhanVienId)
                .Select(tv => tv.Manhom)
                .ToListAsync();

            var assignedTaskIds = await _context.Phancongcongviecs
                .Where(pc =>
                    (pc.Manhanvien.HasValue && pc.Manhanvien.Value == nhanVienId) ||
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

            return (assignedTaskIds, tasks);
        }

        private List<NhanVienDuAnTomTatViewModel> BuildProjectSummaries(List<Congviec> tasks)
        {
            return tasks
                .Where(t => t.MaduanNavigation != null)
                .GroupBy(t => new { t.MaduanNavigation.Maduan, t.MaduanNavigation.Tenduan, t.MaduanNavigation.Ngaybatdau, t.MaduanNavigation.Ngayketthuc })
                .Select(g => new NhanVienDuAnTomTatViewModel
                {
                    MaDuAn = g.Key.Maduan,
                    TenDuAn = g.Key.Tenduan,
                    NgayBatDau = g.Key.Ngaybatdau,
                    NgayKetThuc = g.Key.Ngayketthuc,
                    TongCongViec = g.Count(),
                    DangThucHien = g.Count(x => x.Matrangthai == 2),
                    ChoDuyet = g.Count(x => x.Matrangthai == 4),
                    HoanThanh = g.Count(x => x.Matrangthai == 3),
                    QuaHan = g.Count(x => x.Hanhoanthanh.HasValue && x.Hanhoanthanh.Value.Date < DateTime.Today && x.Matrangthai != 3),
                    CongViecs = g
                        .OrderBy(x => x.Hanhoanthanh ?? DateTime.MaxValue)
                        .Select(x => new NhanVienCongViecItemViewModel
                        {
                            MaCongViec = x.Macongviec,
                            TenCongViec = x.Tencongviec,
                            TenDuAn = x.MaduanNavigation?.Tenduan,
                            Matrangthai = x.Matrangthai,
                            HanHoanThanh = x.Hanhoanthanh,
                            LaCongViecCaNhan = x.Phancongcongviecs.Any() &&
                                x.Phancongcongviecs.All(pc => pc.Loaidoituong == "NhanVien") &&
                                x.Phancongcongviecs.Select(pc => pc.Manhanvien).Distinct().Count() == 1,
                            TienDoPhanTram = x.Nhatkycongviecs
                                .OrderByDescending(nk => nk.Ngaycapnhat)
                                .FirstOrDefault()?.Phantramhoanthanh ?? 0
                        })
                        .ToList()
                })
                .OrderBy(x => x.NgayKetThuc ?? DateTime.MaxValue)
                .ThenBy(x => x.MaDuAn)
                .ToList();
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

        private static bool HasLeadKeyword(string? roleInTeam)
        {
            if (string.IsNullOrWhiteSpace(roleInTeam))
            {
                return false;
            }

            var normalized = RemoveDiacritics(roleInTeam).ToUpperInvariant();
            return normalized.Contains("TRUONG NHOM") || normalized.Contains("LEAD");
        }

        private static string RemoveDiacritics(string input)
        {
            var normalized = input.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalized.Length);

            foreach (var c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }

            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
