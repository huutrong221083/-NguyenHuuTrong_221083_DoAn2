using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using QL_HieuSuat.Models;
using QL_HieuSuat.AI;

namespace QL_HieuSuat.Controllers
{
    [Authorize(Roles = "Admin")]
    public class DudoanaisController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private const string NewAIMode = "New";
        private const string LegacyAIMode = "Legacy";
        private const string TaskDelayMarker = "AIv2-TASK|";
        private const string EmployeeClassMarker = "AIv2-EMP|";
        private const string TaskMetaPrefix = "TASKMETA|";

        public DudoanaisController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        private bool IsNewAIModeEnabled()
        {
            var mode = _configuration["AI:ActiveMode"];
            if (string.IsNullOrWhiteSpace(mode))
            {
                return true;
            }

            return string.Equals(mode, NewAIMode, StringComparison.OrdinalIgnoreCase);
        }

        // GET: Dudoanais
        public async Task<IActionResult> Index()
        {
            if (IsNewAIModeEnabled())
            {
                await LoadNewAIDashboardAsync();
                ViewBag.IsNewAIMode = true;

                return View(await _context.Dudoanais
                    .Include(d => d.ManhanvienNavigation)
                    .OrderByDescending(d => d.Nam)
                    .ThenByDescending(d => d.Thang)
                    .ToListAsync());
            }

            ViewBag.IsNewAIMode = false;

            var data = await _context.Dudoanais
                .Include(d => d.ManhanvienNavigation)
                .OrderByDescending(d => d.Nam)
                .ThenByDescending(d => d.Thang)
                .ToListAsync();

            var projectIds = data
                .Where(x => x.Madoituong.HasValue)
                .Select(x => x.Madoituong!.Value)
                .Distinct()
                .ToList();

            var duAnList = await _context.Duans
                .Where(d => projectIds.Contains(d.Maduan))
                .Include(d => d.Congviecs)
                    .ThenInclude(c => c.Phancongcongviecs)
                .Include(d => d.Congviecs)
                    .ThenInclude(c => c.MatrangthaiNavigation)
                .ToListAsync();

            var projectNameMap = duAnList.ToDictionary(d => d.Maduan, d => d.Tenduan ?? $"Du an #{d.Maduan}");

            var latestRiskByProject = data
                .Where(x => x.Madoituong.HasValue)
                .GroupBy(x => x.Madoituong!.Value)
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .OrderByDescending(x => x.Nam ?? 0)
                        .ThenByDescending(x => x.Thang ?? 0)
                        .ThenByDescending(x => x.Thoigiandudoan ?? DateTime.MinValue)
                        .Select(x => x.Xacsuattrehan ?? 0)
                        .FirstOrDefault());

            var estimatedDateMap = duAnList.ToDictionary(
                d => d.Maduan,
                d => EstimateProjectCompletionDate(
                    d,
                    latestRiskByProject.TryGetValue(d.Maduan, out var risk) ? risk : 0));

            var projectDeadlineMap = duAnList.ToDictionary(
                d => d.Maduan,
                d => d.Ngayketthuc);

            var nhanVienProfiles = await _context.Nhanviens
                .Include(n => n.Kynangnhanviens)
                    .ThenInclude(k => k.MakynangNavigation)
                .Include(n => n.Ketquakpis)
                .ToListAsync();

            var projectSkillSuggestionMap = new Dictionary<int, string>();

            foreach (var duAn in duAnList)
            {
                var teamIds = duAn.Congviecs
                    .SelectMany(c => c.Phancongcongviecs)
                    .Where(p => p.Manhanvien.HasValue)
                    .Select(p => p.Manhanvien!.Value)
                    .Distinct()
                    .ToHashSet();

                var requiredSkillIds = nhanVienProfiles
                    .Where(n => teamIds.Contains(n.Manhanvien))
                    .SelectMany(n => n.Kynangnhanviens)
                    .GroupBy(k => k.Makynang)
                    .OrderByDescending(g => g.Average(x => x.Capdo ?? 1))
                    .ThenByDescending(g => g.Count())
                    .Take(3)
                    .Select(g => g.Key)
                    .ToList();

                if (!requiredSkillIds.Any())
                {
                    projectSkillSuggestionMap[duAn.Maduan] = "Chưa đủ dữ liệu kỹ năng để gợi ý nhân sự.";
                    continue;
                }

                var candidates = nhanVienProfiles
                    .Where(n => !teamIds.Contains(n.Manhanvien))
                    .Select(n =>
                    {
                        var matchedSkills = n.Kynangnhanviens
                            .Where(k => requiredSkillIds.Contains(k.Makynang))
                            .OrderByDescending(k => k.Capdo ?? 0)
                            .Take(2)
                            .ToList();

                        var skillScore = matchedSkills.Sum(k => (k.Capdo ?? 1) + (k.Soduandadungkynangnay ?? 0) * 0.1);
                        var latestKpi = n.Ketquakpis
                            .OrderByDescending(k => k.Nam ?? 0)
                            .ThenByDescending(k => k.Thang ?? 0)
                            .Select(k => k.Diemso ?? 0)
                            .FirstOrDefault();

                        return new
                        {
                            NhanVien = n,
                            MatchedSkills = matchedSkills,
                            SkillScore = skillScore,
                            LatestKpi = latestKpi
                        };
                    })
                    .Where(x => x.MatchedSkills.Count > 0)
                    .OrderByDescending(x => x.SkillScore)
                    .ThenByDescending(x => x.LatestKpi)
                    .Take(3)
                    .ToList();

                if (!candidates.Any())
                {
                    projectSkillSuggestionMap[duAn.Maduan] = "Chưa tìm thấy nhân sự ngoài team có kỹ năng phù hợp.";
                    continue;
                }

                var suggestion = string.Join("; ", candidates.Select(c =>
                {
                    var ten = string.IsNullOrWhiteSpace(c.NhanVien.Hoten)
                        ? $"NV #{c.NhanVien.Manhanvien}"
                        : c.NhanVien.Hoten;

                    var skillText = string.Join(", ", c.MatchedSkills
                        .Select(s => s.MakynangNavigation?.Tenkynang)
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Distinct());

                    return string.IsNullOrWhiteSpace(skillText)
                        ? ten
                        : $"{ten} ({skillText})";
                }));

                projectSkillSuggestionMap[duAn.Maduan] = suggestion;
            }

            ViewBag.ProjectNames = projectNameMap;
            ViewBag.ProjectEstimatedDates = estimatedDateMap;
            ViewBag.ProjectDeadlines = projectDeadlineMap;
            ViewBag.ProjectSkillSuggestions = projectSkillSuggestionMap;

            return View(data);
        }

        private static DateTime? EstimateProjectCompletionDate(Duan duAn, double riskProbability)
        {
            var tasks = duAn.Congviecs?.ToList() ?? new List<Congviec>();
            if (tasks.Count == 0)
            {
                return duAn.Ngayketthuc;
            }

            var now = DateTime.Now.Date;
            var total = tasks.Count;
            var completed = tasks.Count(t => t.MatrangthaiNavigation?.Tentrangthai == "Hoàn thành");
            var remaining = Math.Max(0, total - completed);

            if (remaining == 0)
            {
                return now;
            }

            var startDate = duAn.Ngaybatdau?.Date ?? now.AddDays(-30);
            var elapsedDays = Math.Max(1, (now - startDate).TotalDays);
            var velocityPerDay = completed / elapsedDays;

            var overdueCount = tasks.Count(t =>
                t.MatrangthaiNavigation?.Tentrangthai != "Hoàn thành" &&
                t.Hanhoanthanh.HasValue &&
                t.Hanhoanthanh.Value.Date < now);

            var overdueRatio = total == 0 ? 0 : (double)overdueCount / total;
            var avgDifficulty = tasks.Any() ? tasks.Average(t => t.Dokho ?? 0) : 0;
            var staffing = tasks
                .SelectMany(t => t.Phancongcongviecs)
                .Select(p => p.Manhanvien)
                .Distinct()
                .Count();

            double estimatedRemainingDays;

            if (velocityPerDay < 0.03)
            {
                var remainDeadlineDays = tasks
                    .Where(t => t.MatrangthaiNavigation?.Tentrangthai != "Hoàn thành" && t.Hanhoanthanh.HasValue)
                    .Select(t => (t.Hanhoanthanh!.Value.Date - now).TotalDays)
                    .ToList();

                var fallbackByDeadline = remainDeadlineDays.Count > 0
                    ? Math.Max(7, remainDeadlineDays.Average())
                    : 30;

                estimatedRemainingDays = Math.Max(14, fallbackByDeadline);
            }
            else
            {
                estimatedRemainingDays = remaining / velocityPerDay;
            }

            var difficultyFactor = 1 + Math.Max(0, (avgDifficulty - 3) * 0.15);
            var overdueFactor = 1 + overdueRatio * 0.8;
            var riskFactor = 1 + Math.Max(0, riskProbability) * 0.6;
            var staffingFactor = staffing <= 0 ? 1.2 : Math.Clamp(3.0 / staffing, 0.65, 1.25);

            var adjustedDays = Math.Ceiling(estimatedRemainingDays * difficultyFactor * overdueFactor * riskFactor * staffingFactor);
            adjustedDays = Math.Clamp(adjustedDays, 3, 365);

            return now.AddDays(adjustedDays);
        }

        // GET: Dudoanais/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var dudoanai = await _context.Dudoanais
                .Include(d => d.ManhanvienNavigation)
                .FirstOrDefaultAsync(m => m.Madudoan == id);
            if (dudoanai == null)
            {
                return NotFound();
            }

            return View(dudoanai);
        }

        // GET: Dudoanais/Create
        public IActionResult Create()
        {
            ViewData["Manhanvien"] = new SelectList(_context.Nhanviens, "Manhanvien", "Manhanvien");
            return View();
        }

        // POST: Dudoanais/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Madudoan,Manhanvien,Thang,Nam,Diemdudoan,Dexuatcaithien,Thoigiandudoan,Madoituong,Xacsuattrehan,Gioiynguonluc")] Dudoanai dudoanai)
        {
            if (ModelState.IsValid)
            {
                _context.Add(dudoanai);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["Manhanvien"] = new SelectList(_context.Nhanviens, "Manhanvien", "Manhanvien", dudoanai.Manhanvien);
            return View(dudoanai);
        }

        // GET: Dudoanais/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var dudoanai = await _context.Dudoanais.FindAsync(id);
            if (dudoanai == null)
            {
                return NotFound();
            }
            ViewData["Manhanvien"] = new SelectList(_context.Nhanviens, "Manhanvien", "Manhanvien", dudoanai.Manhanvien);
            return View(dudoanai);
        }

        // POST: Dudoanais/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Madudoan,Manhanvien,Thang,Nam,Diemdudoan,Dexuatcaithien,Thoigiandudoan,Madoituong,Xacsuattrehan,Gioiynguonluc")] Dudoanai dudoanai)
        {
            if (id != dudoanai.Madudoan)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(dudoanai);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!DudoanaiExists(dudoanai.Madudoan))
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
            ViewData["Manhanvien"] = new SelectList(_context.Nhanviens, "Manhanvien", "Manhanvien", dudoanai.Manhanvien);
            return View(dudoanai);
        }

        // GET: Dudoanais/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var dudoanai = await _context.Dudoanais
                .Include(d => d.ManhanvienNavigation)
                .FirstOrDefaultAsync(m => m.Madudoan == id);
            if (dudoanai == null)
            {
                return NotFound();
            }

            return View(dudoanai);
        }

        // POST: Dudoanais/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var dudoanai = await _context.Dudoanais.FindAsync(id);
            if (dudoanai != null)
            {
                _context.Dudoanais.Remove(dudoanai);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool DudoanaiExists(int id)
        {
            return _context.Dudoanais.Any(e => e.Madudoan == id);
        }


        /// GET: Dudoanais/ChayDuBaoAI
        public async Task<IActionResult> ChayDuBaoAI()
        {
            var error = IsNewAIModeEnabled()
                ? await RunTaskDelayPredictionAsync()
                : await RunProjectPredictionAsync();

            if (!string.IsNullOrWhiteSpace(error))
            {
                TempData["AI_ERROR"] = error;
            }
            else
            {
                TempData["AI_SUCCESS"] = IsNewAIModeEnabled()
                    ? "Đã chạy Linear Regression cho dự báo trễ hạn công việc."
                    : "Đã chạy dự báo AI cho dự án.";
            }

            return RedirectToAction(nameof(Index));
        }


        /// GET: Dudoanais/ChayDuBaoNhanVienAI
        public async Task<IActionResult> ChayDuBaoNhanVienAI()
        {
            var error = IsNewAIModeEnabled()
                ? await RunEmployeeClassificationAsync()
                : await RunEmployeePredictionAsync();

            if (!string.IsNullOrWhiteSpace(error))
            {
                TempData["AI_ERROR"] = error;
            }
            else
            {
                TempData["AI_SUCCESS"] = IsNewAIModeEnabled()
                    ? "Đã chạy Random Forest cho xếp loại hiệu suất nhân viên."
                    : "Đã chạy dự báo AI cho nhân viên.";
            }

            return RedirectToAction(nameof(Index));
        }

        /// GET: Dudoanais/ChayTatCaDuBaoAI
        public async Task<IActionResult> ChayTatCaDuBaoAI()
        {
            var errors = new List<string>();

            var projectError = IsNewAIModeEnabled()
                ? await RunTaskDelayPredictionAsync()
                : await RunProjectPredictionAsync();

            if (!string.IsNullOrWhiteSpace(projectError))
            {
                errors.Add(projectError);
            }

            var employeeError = IsNewAIModeEnabled()
                ? await RunEmployeeClassificationAsync()
                : await RunEmployeePredictionAsync();

            if (!string.IsNullOrWhiteSpace(employeeError))
            {
                errors.Add(employeeError);
            }

            if (errors.Count > 0)
            {
                TempData["AI_ERROR"] = string.Join(" ", errors.Distinct());
            }
            else
            {
                TempData["AI_SUCCESS"] = IsNewAIModeEnabled()
                    ? "Đã chạy xong 2 mô hình AI mới: Linear Regression và Random Forest."
                    : "Đã chạy xong toàn bộ dự báo AI (dự án và nhân viên).";
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task LoadNewAIDashboardAsync()
        {
            var now = DateTime.Now;
            var month = now.Month;
            var year = now.Year;

            var taskRecords = await _context.Dudoanais
                .AsNoTracking()
                .Where(x =>
                    x.Madoituong.HasValue &&
                    x.Thang == month &&
                    x.Nam == year &&
                    x.Dexuatcaithien != null &&
                    x.Dexuatcaithien.StartsWith(TaskDelayMarker))
                .OrderByDescending(x => x.Xacsuattrehan)
                .ThenByDescending(x => x.Thoigiandudoan)
                .ToListAsync();

            var projectIds = taskRecords
                .Where(x => x.Madoituong.HasValue)
                .Select(x => x.Madoituong!.Value)
                .Distinct()
                .ToList();

            var projects = await _context.Duans
                .Where(d => projectIds.Contains(d.Maduan))
                .Include(d => d.Congviecs)
                    .ThenInclude(c => c.Phancongcongviecs)
                .Include(d => d.Congviecs)
                    .ThenInclude(c => c.MatrangthaiNavigation)
                .AsSplitQuery()
                .ToListAsync();

            var projectMap = projects.ToDictionary(d => d.Maduan, d => d);

            var taskRows = taskRecords.Select(record =>
            {
                var (taskId, taskName) = ParseTaskMeta(record.Gioiynguonluc);
                var projectId = record.Madoituong ?? 0;
                var projectName = projectMap.TryGetValue(projectId, out var duan)
                    ? (string.IsNullOrWhiteSpace(duan.Tenduan) ? $"Du an #{projectId}" : duan.Tenduan)
                    : $"Du an #{projectId}";

                return new NewTaskDelayRow
                {
                    ProjectId = projectId,
                    ProjectName = projectName,
                    TaskId = taskId ?? 0,
                    TaskName = !string.IsNullOrWhiteSpace(taskName)
                        ? taskName
                        : $"Cong viec #{taskId ?? 0}",
                    PredictedLateDays = Math.Round(record.Diemdudoan ?? 0, 1),
                    LateProbabilityPercent = Math.Round((record.Xacsuattrehan ?? 0) * 100, 1),
                    Suggestion = ExtractPayload(record.Dexuatcaithien, TaskDelayMarker),
                    UpdatedAt = record.Thoigiandudoan
                };
            }).ToList();

            var nhanViens = await _context.Nhanviens
                .Include(n => n.Ketquakpis)
                .Include(n => n.Kynangnhanviens)
                    .ThenInclude(k => k.MakynangNavigation)
                .Include(n => n.Phancongcongviecs)
                    .ThenInclude(pc => pc.MacongviecNavigation)
                .AsSplitQuery()
                .ToListAsync();

            var nhoms = await _context.Nhoms
                .Include(n => n.Thanhviennhoms)
                .AsNoTracking()
                .ToListAsync();

            var projectRows = taskRows
                .GroupBy(x => x.ProjectId)
                .Select(g =>
                {
                    var projectId = g.Key;
                    var projectName = g.First().ProjectName;
                    var projectTaskRows = g
                        .OrderByDescending(x => x.LateProbabilityPercent)
                        .ThenByDescending(x => x.PredictedLateDays)
                        .ToList();

                    var highRiskTaskCount = projectTaskRows.Count(x => x.LateProbabilityPercent >= 60);
                    var avgRiskPercent = projectTaskRows.Count == 0 ? 0 : projectTaskRows.Average(x => x.LateProbabilityPercent);
                    var maxRiskPercent = projectTaskRows.Count == 0 ? 0 : projectTaskRows.Max(x => x.LateProbabilityPercent);

                    // Urgency is weighted toward immediate high-risk tasks, then overall risk profile.
                    var urgencyScore = (highRiskTaskCount * 100.0) + (maxRiskPercent * 0.7) + (avgRiskPercent * 0.3);

                    var focusTasks = projectTaskRows
                        .Where(x => x.LateProbabilityPercent >= 40)
                        .Take(3)
                        .ToList();

                    if (focusTasks.Count == 0)
                    {
                        focusTasks = projectTaskRows.Take(2).ToList();
                    }

                    var focusTaskText = focusTasks.Count == 0
                        ? "Chưa xác định được công việc cần tăng cường."
                        : string.Join(", ", focusTasks.Select(x => $"{x.TaskName} ({Math.Round(x.LateProbabilityPercent, 1)}%)"));

                    projectMap.TryGetValue(projectId, out var projectEntity);
                    var teamEmployeeIds = projectEntity?.Congviecs
                        .SelectMany(cv => cv.Phancongcongviecs)
                        .Where(pc => pc.Manhanvien.HasValue)
                        .Select(pc => pc.Manhanvien!.Value)
                        .Distinct()
                        .ToHashSet() ?? new HashSet<int>();

                    var requiredSkillIds = nhanViens
                        .Where(nv => teamEmployeeIds.Contains(nv.Manhanvien))
                        .SelectMany(nv => nv.Kynangnhanviens)
                        .GroupBy(k => k.Makynang)
                        .OrderByDescending(gr => gr.Average(x => x.Capdo ?? 1))
                        .ThenByDescending(gr => gr.Average(x => x.Soduandadungkynangnay ?? 0))
                        .Take(4)
                        .Select(gr => gr.Key)
                        .ToHashSet();

                    bool IsAvailableForSupport(Nhanvien nv)
                    {
                        return !nv.Phancongcongviecs.Any(pc =>
                            pc.MacongviecNavigation != null &&
                            pc.MacongviecNavigation.Matrangthai != 3 &&
                            pc.MacongviecNavigation.Maduan != projectId);
                    }

                    var supportEmployees = nhanViens
                        .Where(nv => !teamEmployeeIds.Contains(nv.Manhanvien))
                        .Where(IsAvailableForSupport)
                        .Select(nv =>
                        {
                            var matchedSkills = nv.Kynangnhanviens
                                .Where(k => requiredSkillIds.Contains(k.Makynang))
                                .OrderByDescending(k => k.Capdo ?? 0)
                                .ThenByDescending(k => k.Soduandadungkynangnay ?? 0)
                                .Take(3)
                                .ToList();

                            var skillScore = matchedSkills.Sum(k =>
                                (k.Capdo ?? 1) + (k.Soduandadungkynangnay ?? 0) * 0.15);

                            var latestKpi = nv.Ketquakpis
                                .OrderByDescending(k => k.Nam ?? 0)
                                .ThenByDescending(k => k.Thang ?? 0)
                                .Select(k => k.Diemso ?? 0)
                                .FirstOrDefault();

                            var experienceScore = nv.Sonamkinhnghiem ?? 0;

                            return new
                            {
                                NhanVien = nv,
                                MatchedSkills = matchedSkills,
                                Score = skillScore + (experienceScore * 0.4) + (latestKpi * 0.03),
                                Experience = experienceScore
                            };
                        })
                        .Where(x => x.MatchedSkills.Count > 0 || requiredSkillIds.Count == 0)
                        .OrderByDescending(x => x.Score)
                        .ThenByDescending(x => x.Experience)
                        .Take(3)
                        .ToList();

                    var employeeSuggestionText = supportEmployees.Count == 0
                        ? "Không có nhân sự rảnh phù hợp ở thời điểm hiện tại."
                        : string.Join("; ", supportEmployees.Select(x =>
                        {
                            var name = string.IsNullOrWhiteSpace(x.NhanVien.Hoten)
                                ? $"NV #{x.NhanVien.Manhanvien}"
                                : x.NhanVien.Hoten;

                            var skills = x.MatchedSkills
                                .Select(k => k.MakynangNavigation?.Tenkynang)
                                .Where(s => !string.IsNullOrWhiteSpace(s))
                                .Distinct()
                                .ToList();

                            var skillText = skills.Count == 0 ? "Kỹ năng tổng quát" : string.Join(", ", skills);
                            return $"{name} ({Math.Round(x.Experience, 1)} năm KN, {skillText})";
                        }));

                    var supportGroups = nhoms
                        .Select(nhom =>
                        {
                            var memberIds = nhom.Thanhviennhoms.Select(tv => tv.Manhanvien).Distinct().ToList();
                            if (memberIds.Count == 0)
                            {
                                return null;
                            }

                            var members = nhanViens.Where(nv => memberIds.Contains(nv.Manhanvien)).ToList();
                            var availableMembers = members.Where(IsAvailableForSupport).ToList();

                            if (availableMembers.Count == 0)
                            {
                                return null;
                            }

                            var matchedMembers = availableMembers
                                .Select(m => m.Kynangnhanviens.Count(k => requiredSkillIds.Contains(k.Makynang)))
                                .Sum();

                            return new
                            {
                                Nhom = nhom,
                                AvailableCount = availableMembers.Count,
                                TotalCount = members.Count,
                                MatchedScore = matchedMembers
                            };
                        })
                        .Where(x => x != null)
                        .OrderByDescending(x => x!.MatchedScore)
                        .ThenByDescending(x => x!.AvailableCount)
                        .Take(2)
                        .Select(x => x!)
                        .ToList();

                    var groupSuggestionText = supportGroups.Count == 0
                        ? "Không có nhóm rảnh phù hợp để hỗ trợ ngay."
                        : string.Join("; ", supportGroups.Select(gx =>
                            $"{(string.IsNullOrWhiteSpace(gx.Nhom.Tennhom) ? $"Nhóm #{gx.Nhom.Manhom}" : gx.Nhom.Tennhom)} ({gx.AvailableCount}/{gx.TotalCount} thành viên rảnh)"));

                    return new NewTaskDelayProjectRow
                    {
                        ProjectId = projectId,
                        ProjectName = projectName,
                        TaskCount = projectTaskRows.Count,
                        HighRiskTaskCount = highRiskTaskCount,
                        MaxRiskPercent = Math.Round(maxRiskPercent, 3),
                        AverageRiskPercent = Math.Round(avgRiskPercent, 1),
                        UrgencyScore = Math.Round(urgencyScore, 3),
                        FocusTasksSuggestion = focusTaskText,
                        SupportEmployeesSuggestion = employeeSuggestionText,
                        SupportGroupsSuggestion = groupSuggestionText,
                        Tasks = projectTaskRows
                    };
                })
                .OrderByDescending(x => x.UrgencyScore)
                .ThenByDescending(x => x.MaxRiskPercent)
                .ThenByDescending(x => x.HighRiskTaskCount)
                .ThenByDescending(x => x.AverageRiskPercent)
                .ToList();

            var employeeRecords = await _context.Dudoanais
                .Include(x => x.ManhanvienNavigation)
                .AsNoTracking()
                .Where(x =>
                    x.Manhanvien.HasValue &&
                    x.Thang == month &&
                    x.Nam == year &&
                    x.Dexuatcaithien != null &&
                    x.Dexuatcaithien.StartsWith(EmployeeClassMarker))
                .ToListAsync();

            employeeRecords = employeeRecords
                .GroupBy(x => x.Manhanvien!.Value)
                .Select(g => g
                    .OrderByDescending(x => x.Thoigiandudoan ?? DateTime.MinValue)
                    .ThenByDescending(x => x.Madudoan)
                    .First())
                .OrderByDescending(x => x.Thoigiandudoan ?? DateTime.MinValue)
                .ToList();

            var employeeIds = employeeRecords
                .Where(x => x.Manhanvien.HasValue)
                .Select(x => x.Manhanvien!.Value)
                .Distinct()
                .ToList();

            var employees = await _context.Nhanviens
                .Where(nv => employeeIds.Contains(nv.Manhanvien))
                .Include(nv => nv.Ketquakpis)
                .Include(nv => nv.Phancongcongviecs)
                    .ThenInclude(pc => pc.MacongviecNavigation)
                .AsSplitQuery()
                .ToListAsync();

            var employeeMap = employees.ToDictionary(x => x.Manhanvien, x => x);

            var employeeRows = employeeRecords.Select(record =>
            {
                var label = ExtractPayload(record.Dexuatcaithien, EmployeeClassMarker);
                var confidence = Math.Round(record.Diemdudoan ?? 0, 1);
                var employeeId = record.Manhanvien ?? 0;
                employeeMap.TryGetValue(employeeId, out var employee);

                var assignments = employee?.Phancongcongviecs
                    .Where(pc => pc.MacongviecNavigation != null)
                    .ToList() ?? new List<Phancongcongviec>();

                var tasks = assignments
                    .Select(pc => pc.MacongviecNavigation!)
                    .Distinct()
                    .ToList();

                var totalTasks = tasks.Count;
                var completedTasks = tasks.Count(cv => cv.Matrangthai == 3);
                var overdueTasks = tasks.Count(cv => cv.Matrangthai != 3 && cv.Hanhoanthanh.HasValue && cv.Hanhoanthanh.Value.Date < now.Date);

                var projectCount = tasks
                    .Select(cv => cv.Maduan)
                    .Distinct()
                    .Count();

                var completionRate = totalTasks == 0 ? 0 : (double)completedTasks / totalTasks;
                var overdueRate = totalTasks == 0 ? 0 : (double)overdueTasks / totalTasks;

                var latestKpi = employee?.Ketquakpis
                    .OrderByDescending(k => k.Nam ?? 0)
                    .ThenByDescending(k => k.Thang ?? 0)
                    .Select(k => k.Diemso ?? 0)
                    .FirstOrDefault() ?? 0;

                var experienceYears = employee?.Sonamkinhnghiem ?? 0;

                var bonusPoints = (completionRate * 40.0) + (latestKpi * 0.3) + (experienceYears * 2.0) + (projectCount * 1.0);
                var penaltyPoints = (overdueRate * 35.0) + (Math.Max(0, 70.0 - latestKpi) * 0.2);
                var evaluationScore = Math.Clamp(bonusPoints - penaltyPoints, 0, 100);

                return new NewEmployeeClassificationRow
                {
                    EmployeeId = employeeId,
                    EmployeeName = record.ManhanvienNavigation?.Hoten ?? $"Nhan vien #{record.Manhanvien}",
                    Classification = string.IsNullOrWhiteSpace(label) ? "Chưa phân loại" : label,
                    ConfidencePercent = confidence,
                    EvaluationScore = Math.Round(evaluationScore, 3),
                    TotalTasks = totalTasks,
                    CompletedTasks = completedTasks,
                    OverdueTasks = overdueTasks,
                    ParticipatedProjectCount = projectCount,
                    CompletionRatePercent = Math.Round(completionRate * 100.0, 3),
                    OverdueRatePercent = Math.Round(overdueRate * 100.0, 3),
                    LatestKpi = Math.Round(latestKpi, 3),
                    ExperienceYears = Math.Round(experienceYears, 3),
                    BonusPoints = Math.Round(bonusPoints, 3),
                    PenaltyPoints = Math.Round(penaltyPoints, 3),
                    FeatureImpact = record.Gioiynguonluc,
                    UpdatedAt = record.Thoigiandudoan
                };
            })
            .OrderByDescending(x => x.EvaluationScore)
            .ThenByDescending(x => x.ConfidencePercent)
            .ToList();

            ViewBag.NewTaskDelayRows = taskRows;
            ViewBag.NewTaskDelayProjects = projectRows;
            ViewBag.NewEmployeeRows = employeeRows;

            var highRiskCount = taskRows.Count(x => x.LateProbabilityPercent >= 60);
            var avgLateDays = taskRows.Count == 0 ? 0 : taskRows.Average(x => x.PredictedLateDays);

            ViewBag.NewAISummary = new NewAISummary
            {
                TaskCount = taskRows.Count,
                HighRiskCount = highRiskCount,
                AverageLateDays = Math.Round(avgLateDays, 1),
                EmployeeClassifiedCount = employeeRows.Count
            };
        }

        private async Task<string?> RunTaskDelayPredictionAsync()
        {
            var allTasks = await _context.Congviecs
                .Include(cv => cv.Phancongcongviecs)
                .Include(cv => cv.Nhatkycongviecs)
                .Include(cv => cv.MaduanNavigation)
                    .ThenInclude(da => da.Congviecs)
                .AsSplitQuery()
                .ToListAsync();

            if (allTasks.Count < 10)
            {
                return "Không đủ dữ liệu công việc để train Linear Regression (cần tối thiểu 10).";
            }

            var training = new List<TaskDelayRegressionInput>();
            var now = DateTime.Now;

            foreach (var task in allTasks)
            {
                if (!task.Hanhoanthanh.HasValue)
                {
                    continue;
                }

                var deadline = task.Hanhoanthanh.Value.Date;

                double actualDelayDays;
                if (task.Matrangthai == 3)
                {
                    var latestActualEnd = task.Phancongcongviecs
                        .Where(pc => pc.Ngayketthucthucte.HasValue)
                        .Select(pc => pc.Ngayketthucthucte!.Value.Date)
                        .DefaultIfEmpty(deadline)
                        .Max();

                    actualDelayDays = Math.Max(0, (latestActualEnd - deadline).TotalDays);
                }
                else
                {
                    actualDelayDays = deadline < now.Date ? (now.Date - deadline).TotalDays : 0;
                }

                var input = BuildTaskDelayFeature(task, now);
                input.SoNgayTreThucTe = (float)Math.Clamp(actualDelayDays, 0, 365);

                training.Add(input);
            }

            if (training.Count < 10)
            {
                return "Không đủ mẫu có hạn hoàn thành để train Linear Regression.";
            }

            var service = new TaskDelayLinearRegressionService();
            service.Train(training);

            var month = now.Month;
            var year = now.Year;
            var targetTasks = allTasks
                .Where(cv => cv.Matrangthai != 3)
                .ToList();

            foreach (var task in targetTasks)
            {
                if (task.Maduan <= 0)
                {
                    continue;
                }

                var input = BuildTaskDelayFeature(task, now);
                var predictedLateDays = service.PredictDaysLate(input);
                var riskProbability = TaskDelayLinearRegressionService.ConvertDaysLateToRiskProbability(predictedLateDays);

                var suggestion = predictedLateDays >= 5
                    ? "Nguy cơ trễ cao: cần giảm tải hoặc bổ sung nhân lực ngay."
                    : predictedLateDays >= 2
                        ? "Nguy cơ trễ trung bình: cần theo dõi sát và kiểm tra tiến độ mỗi ngày."
                        : "Nguy cơ trễ thấp: duy trì tốc độ hiện tại.";

                var markerContent = TaskDelayMarker + suggestion;
                var taskMeta = BuildTaskMeta(task.Macongviec, task.Tencongviec);
                _context.Dudoanais.Add(new Dudoanai
                {
                    Madoituong = task.Maduan,
                    Thang = month,
                    Nam = year,
                    Diemdudoan = Math.Round(predictedLateDays, 2),
                    Xacsuattrehan = riskProbability,
                    Dexuatcaithien = markerContent,
                    Gioiynguonluc = taskMeta,
                    Thoigiandudoan = now
                });
            }

            await _context.SaveChangesAsync();
            return null;
        }

        private async Task<string?> RunEmployeeClassificationAsync()
        {
            var nhanViens = await _context.Nhanviens
                .Include(n => n.Ketquakpis)
                .Include(n => n.Phancongcongviecs)
                    .ThenInclude(pc => pc.MacongviecNavigation)
                .ToListAsync();

            var samples = new List<EmployeePerformanceClassificationInput>();

            foreach (var nv in nhanViens)
            {
                var input = BuildEmployeeFeature(nv);
                input.XepLoai = ClassifyByKpiThreshold(nv.DiemkpiTichluy ?? 50);

                samples.Add(input);
            }

            if (samples.Count < 12)
            {
                return "Không đủ dữ liệu nhân viên để train Random Forest (cần tối thiểu 12).";
            }

            var service = new EmployeePerformanceRandomForestService();
            var trainingResult = service.Train(samples);

            var orderedFeatureImpact = trainingResult.FeatureImportance
                .OrderByDescending(kv => kv.Value)
                .Take(3)
                .Select(kv => $"{MapFeatureName(kv.Key)}: {Math.Round(kv.Value * 100, 2)}")
                .ToList();

            var featureImpactText = orderedFeatureImpact.Count == 0
                ? "Chưa xác định rõ mức ảnh hưởng yếu tố."
                : string.Join(" | ", orderedFeatureImpact);

            var now = DateTime.Now;
            var month = now.Month;
            var year = now.Year;

            foreach (var nv in nhanViens)
            {
                var input = BuildEmployeeFeature(nv);
                var prediction = service.Predict(input);

                var markerContent = EmployeeClassMarker + prediction.PredictedLabel;
                _context.Dudoanais.Add(new Dudoanai
                {
                    Manhanvien = nv.Manhanvien,
                    Thang = month,
                    Nam = year,
                    Diemdudoan = Math.Round(prediction.Confidence * 100, 2),
                    Dexuatcaithien = markerContent,
                    Gioiynguonluc = featureImpactText,
                    Thoigiandudoan = now
                });
            }

            await _context.SaveChangesAsync();
            return null;
        }

        private TaskDelayRegressionInput BuildTaskDelayFeature(Congviec task, DateTime now)
        {
            var assignmentCount = task.Phancongcongviecs
                .Select(pc => pc.Manhanvien)
                .Distinct()
                .Count();

            var projectTasks = task.MaduanNavigation?.Congviecs?.ToList() ?? new List<Congviec>();
            var projectTaskCount = projectTasks.Count;
            var overdueCount = projectTasks.Count(cv => cv.Matrangthai != 3 && cv.Hanhoanthanh.HasValue && cv.Hanhoanthanh.Value.Date < now.Date);
            var projectOverdueRatio = projectTaskCount == 0 ? 0 : (double)overdueCount / projectTaskCount;

            var daysToDeadline = task.Hanhoanthanh.HasValue
                ? (task.Hanhoanthanh.Value.Date - now.Date).TotalDays
                : 0;

            var progress = task.Nhatkycongviecs
                .OrderByDescending(nk => nk.Ngaycapnhat)
                .Select(nk => nk.Phantramhoanthanh)
                .FirstOrDefault() ?? 0;

            return new TaskDelayRegressionInput
            {
                DoKho = (float)(task.Dokho ?? 0),
                DoUuTien = (float)(task.Douutien ?? 0),
                SoNguoiThamGia = assignmentCount,
                SoNgayConLaiDenHan = (float)daysToDeadline,
                TiLeHoanThanh = (float)(progress / 100.0),
                TiLeTreHanDuAn = (float)projectOverdueRatio
            };
        }

        private EmployeePerformanceClassificationInput BuildEmployeeFeature(Nhanvien nv)
        {
            var tasks = nv.Phancongcongviecs
                .Where(pc => pc.MacongviecNavigation != null)
                .Select(pc => pc.MacongviecNavigation)
                .Distinct()
                .ToList();

            var total = tasks.Count;
            var completed = tasks.Count(cv => cv.Matrangthai == 3);
            var overdue = tasks.Count(cv => cv.Matrangthai != 3 && cv.Hanhoanthanh.HasValue && cv.Hanhoanthanh.Value < DateTime.Now);

            var latestKpi = nv.Ketquakpis
                .OrderByDescending(k => k.Nam ?? 0)
                .ThenByDescending(k => k.Thang ?? 0)
                .Select(k => k.Diemso ?? 0)
                .FirstOrDefault();

            return new EmployeePerformanceClassificationInput
            {
                Sonamkinhnghiem = (float)(nv.Sonamkinhnghiem ?? 0),
                SoCongViec = total,
                DoKhoTrungBinh = total == 0 ? 0 : (float)tasks.Average(cv => cv.Dokho ?? 0),
                DoUuTienTrungBinh = total == 0 ? 0 : (float)tasks.Average(cv => cv.Douutien ?? 0),
                TyLeTreHan = total == 0 ? 0 : (float)overdue / total,
                TyLeHoanThanh = total == 0 ? 0 : (float)completed / total,
                DiemKpiGanNhat = (float)latestKpi
            };
        }

        private static string ClassifyByKpiThreshold(double score)
        {
            return score >= 85 ? "Xuất sắc"
                : score >= 70 ? "Tốt"
                : score >= 50 ? "Trung bình"
                : "Yếu";
        }

        private static string MapFeatureName(string featureName)
        {
            return featureName switch
            {
                nameof(EmployeePerformanceClassificationInput.Sonamkinhnghiem) => "Số năm kinh nghiệm",
                nameof(EmployeePerformanceClassificationInput.SoCongViec) => "Số công việc",
                nameof(EmployeePerformanceClassificationInput.DoKhoTrungBinh) => "Độ khó trung bình",
                nameof(EmployeePerformanceClassificationInput.DoUuTienTrungBinh) => "Độ ưu tiên trung bình",
                nameof(EmployeePerformanceClassificationInput.TyLeTreHan) => "Tỷ lệ trễ hạn",
                nameof(EmployeePerformanceClassificationInput.TyLeHoanThanh) => "Tỷ lệ hoàn thành",
                nameof(EmployeePerformanceClassificationInput.DiemKpiGanNhat) => "Điểm KPI gần nhất",
                _ => featureName
            };
        }

        private static string ExtractPayload(string? raw, string marker)
        {
            if (string.IsNullOrWhiteSpace(raw) || !raw.StartsWith(marker))
            {
                return string.Empty;
            }

            return raw[marker.Length..].Trim();
        }

        private static string BuildTaskMeta(int taskId, string? taskName)
        {
            var safeName = string.IsNullOrWhiteSpace(taskName)
                ? $"Cong viec #{taskId}"
                : taskName.Trim().Replace("|", "/");

            return $"{TaskMetaPrefix}{taskId}|{safeName}";
        }

        private static (int? TaskId, string TaskName) ParseTaskMeta(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return (null, string.Empty);
            }

            if (!raw.StartsWith(TaskMetaPrefix))
            {
                return (null, raw);
            }

            var payload = raw[TaskMetaPrefix.Length..];
            var parts = payload.Split('|', 2, StringSplitOptions.TrimEntries);

            if (parts.Length == 0)
            {
                return (null, string.Empty);
            }

            var hasId = int.TryParse(parts[0], out var taskId);
            var name = parts.Length > 1 ? parts[1] : string.Empty;

            return (hasId ? taskId : null, name);
        }

        public sealed class NewTaskDelayRow
        {
            public int ProjectId { get; set; }

            public string ProjectName { get; set; } = string.Empty;

            public int TaskId { get; set; }

            public string TaskName { get; set; } = string.Empty;

            public double PredictedLateDays { get; set; }

            public double LateProbabilityPercent { get; set; }

            public string Suggestion { get; set; } = string.Empty;

            public DateTime? UpdatedAt { get; set; }
        }

        public sealed class NewTaskDelayProjectRow
        {
            public int ProjectId { get; set; }

            public string ProjectName { get; set; } = string.Empty;

            public int TaskCount { get; set; }

            public int HighRiskTaskCount { get; set; }

            public double MaxRiskPercent { get; set; }

            public double AverageRiskPercent { get; set; }

            public double UrgencyScore { get; set; }

            public string FocusTasksSuggestion { get; set; } = string.Empty;

            public string SupportEmployeesSuggestion { get; set; } = string.Empty;

            public string SupportGroupsSuggestion { get; set; } = string.Empty;

            public List<NewTaskDelayRow> Tasks { get; set; } = new();
        }

        public sealed class NewEmployeeClassificationRow
        {
            public int EmployeeId { get; set; }

            public string EmployeeName { get; set; } = string.Empty;

            public string Classification { get; set; } = string.Empty;

            public double ConfidencePercent { get; set; }

            public double EvaluationScore { get; set; }

            public int TotalTasks { get; set; }

            public int CompletedTasks { get; set; }

            public int OverdueTasks { get; set; }

            public int ParticipatedProjectCount { get; set; }

            public double CompletionRatePercent { get; set; }

            public double OverdueRatePercent { get; set; }

            public double LatestKpi { get; set; }

            public double ExperienceYears { get; set; }

            public double BonusPoints { get; set; }

            public double PenaltyPoints { get; set; }

            public string? FeatureImpact { get; set; }

            public DateTime? UpdatedAt { get; set; }
        }

        public sealed class NewAISummary
        {
            public int TaskCount { get; set; }

            public int HighRiskCount { get; set; }

            public double AverageLateDays { get; set; }

            public int EmployeeClassifiedCount { get; set; }
        }

        private async Task<string?> RunProjectPredictionAsync()
        {
            var duans = await _context.Duans
                .Include(d => d.Congviecs)
                    .ThenInclude(c => c.Phancongcongviecs)
                .Include(d => d.Congviecs)
                    .ThenInclude(c => c.MatrangthaiNavigation)
                .ToListAsync();

            var aiService = new ProjectAIService();
            var trainingData = new List<ProjectAIModel>();

            foreach (var da in duans)
            {
                var tasks = da.Congviecs;
                if (tasks.Count == 0) continue;

                float soCongViec = tasks.Count;
                float doKhoTB = (float)tasks.Average(t => t.Dokho ?? 0);
                float doUuTienTB = (float)tasks.Average(t => t.Douutien ?? 0);

                int hoanThanh = tasks.Count(t => t.MatrangthaiNavigation?.Tentrangthai == "Hoàn thành");
                float tyLeHoanThanh = hoanThanh / soCongViec;

                int tre = tasks.Count(t =>
                    t.Hanhoanthanh < DateTime.Now &&
                    t.MatrangthaiNavigation?.Tentrangthai != "Hoàn thành");

                float tyLeTre = soCongViec == 0 ? 0 : tre / soCongViec;

                int nhanVien = tasks
                    .SelectMany(t => t.Phancongcongviecs)
                    .Select(p => p.Manhanvien)
                    .Distinct()
                    .Count();

                bool label = (tyLeTre > 0.2f) || (tyLeHoanThanh < 0.6f);

                trainingData.Add(new ProjectAIModel
                {
                    SoCongViec = soCongViec,
                    TyLeHoanThanh = tyLeHoanThanh,
                    TyLeTreHan = tyLeTre,
                    DoKhoTrungBinh = doKhoTB,
                    DoUuTienTrungBinh = doUuTienTB,
                    SoNhanVien = nhanVien,
                    DuAnTre = label
                });
            }

            int pos = trainingData.Count(x => x.DuAnTre);
            int neg = trainingData.Count(x => !x.DuAnTre);

            if (pos == 0 || neg == 0)
            {
                return "AI dự án cần cả dự án tốt và dự án trễ để học.";
            }

            if (trainingData.Count < 5)
            {
                return "Không đủ dữ liệu để train AI dự án.";
            }

            aiService.Train(trainingData);

            foreach (var da in duans)
            {
                var tasks = da.Congviecs;
                if (tasks.Count == 0) continue;

                float soCongViec = tasks.Count;
                float doKhoTB = (float)tasks.Average(t => t.Dokho ?? 0);
                float doUuTienTB = (float)tasks.Average(t => t.Douutien ?? 0);

                int hoanThanh = tasks.Count(t => t.MatrangthaiNavigation?.Tentrangthai == "Hoàn thành");
                float tyLeHoanThanh = hoanThanh / soCongViec;

                int tre = tasks.Count(t =>
                    t.Hanhoanthanh < DateTime.Now &&
                    t.MatrangthaiNavigation?.Tentrangthai != "Hoàn thành");

                float tyLeTre = tre / soCongViec;

                int nhanVien = tasks
                    .SelectMany(t => t.Phancongcongviecs)
                    .Select(p => p.Manhanvien)
                    .Distinct()
                    .Count();

                var input = new ProjectAIModel
                {
                    SoCongViec = soCongViec,
                    TyLeHoanThanh = tyLeHoanThanh,
                    TyLeTreHan = tyLeTre,
                    DoKhoTrungBinh = doKhoTB,
                    DoUuTienTrungBinh = doUuTienTB,
                    SoNhanVien = nhanVien
                };

                float predicted = aiService.Predict(input);

                var duDoan = new Dudoanai
                {
                    Madoituong = da.Maduan,
                    Thang = DateTime.Now.Month,
                    Nam = DateTime.Now.Year,
                    Diemdudoan = predicted,
                    Xacsuattrehan = predicted,
                    Dexuatcaithien = predicted > 0.6
                        ? "Dự án có nguy cơ trễ hạn."
                        : "Tiến độ dự án đang ổn định.",
                    Gioiynguonluc = predicted > 0.6
                        ? "Cần bổ sung nhân lực hoặc giảm tải công việc."
                        : "Nguồn lực hiện tại phù hợp.",
                    Thoigiandudoan = DateTime.Now
                };

                _context.Dudoanais.Add(duDoan);
            }

            await _context.SaveChangesAsync();
            return null;
        }

        private async Task<string?> RunEmployeePredictionAsync()
        {
            var nhanviens = await _context.Nhanviens
                .Include(n => n.Phancongcongviecs)
                    .ThenInclude(p => p.MacongviecNavigation)
                    .ThenInclude(c => c.MatrangthaiNavigation)
                .ToListAsync();

            var aiService = new KPIService();
            var trainingData = new List<KPIModel>();

            foreach (var nv in nhanviens)
            {
                var tasks = nv.Phancongcongviecs
                    .Select(p => p.MacongviecNavigation)
                    .ToList();

                if (tasks.Count == 0)
                    continue;

                float soCongViec = tasks.Count;
                float doKhoTB = (float)tasks.Average(t => t.Dokho ?? 0);
                float doUuTienTB = (float)tasks.Average(t => t.Douutien ?? 0);

                int tre = tasks.Count(t =>
                    t.Hanhoanthanh < DateTime.Now &&
                    t.MatrangthaiNavigation?.Tentrangthai != "Hoàn thành");

                float tyLeTre = tre / soCongViec;
                float kinhNghiem = (float)(nv.Sonamkinhnghiem ?? 1);
                float label = (float)(nv.DiemkpiTichluy ?? 50);

                trainingData.Add(new KPIModel
                {
                    Sonamkinhnghiem = kinhNghiem,
                    SoCongViec = soCongViec,
                    DoKhoTrungBinh = doKhoTB,
                    DoUuTienTrungBinh = doUuTienTB,
                    TyLeTreHan = tyLeTre,
                    KPIThucTe = label
                });
            }

            if (trainingData.Count < 2)
            {
                return "Không đủ dữ liệu để train AI nhân viên.";
            }

            aiService.Train(trainingData);

            foreach (var nv in nhanviens)
            {
                var tasks = nv.Phancongcongviecs
                    .Select(p => p.MacongviecNavigation)
                    .ToList();

                if (tasks.Count == 0)
                    continue;

                float soCongViec = tasks.Count;
                float doKhoTB = (float)tasks.Average(t => t.Dokho ?? 0);
                float doUuTienTB = (float)tasks.Average(t => t.Douutien ?? 0);

                int tre = tasks.Count(t =>
                    t.Hanhoanthanh < DateTime.Now &&
                    t.MatrangthaiNavigation?.Tentrangthai != "Hoàn thành");

                float tyLeTre = tre / soCongViec;

                var input = new KPIModel
                {
                    Sonamkinhnghiem = (float)(nv.Sonamkinhnghiem ?? 1),
                    SoCongViec = soCongViec,
                    DoKhoTrungBinh = doKhoTB,
                    DoUuTienTrungBinh = doUuTienTB,
                    TyLeTreHan = tyLeTre
                };

                float predicted = aiService.Predict(input);

                string rank = predicted >= 85
                    ? "Xuất sắc"
                    : predicted >= 70
                        ? "Tốt"
                        : predicted >= 50
                            ? "Trung bình"
                            : "Yếu";

                var duDoan = new Dudoanai
                {
                    Manhanvien = nv.Manhanvien,
                    Thang = DateTime.Now.Month,
                    Nam = DateTime.Now.Year,
                    Diemdudoan = predicted,
                    Xacsuattrehan = predicted,
                    Dexuatcaithien = rank == "Yếu" ? "Cần đào tạo thêm." : "Hiệu suất tốt.",
                    Gioiynguonluc = predicted < 60
                        ? "Cần đào tạo thêm hoặc giảm khối lượng công việc."
                        : "Có thể giao thêm nhiệm vụ phù hợp.",
                    Thoigiandudoan = DateTime.Now
                };

                _context.Dudoanais.Add(duDoan);
            }

            await _context.SaveChangesAsync();
            return null;
        }
    }
}
