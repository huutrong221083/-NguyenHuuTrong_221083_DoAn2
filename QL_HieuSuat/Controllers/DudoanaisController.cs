using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QL_HieuSuat.Models;
using QL_HieuSuat.AI;

namespace QL_HieuSuat.Controllers
{
    [Authorize(Roles = "Admin")]
    public class DudoanaisController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DudoanaisController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Dudoanais
        public async Task<IActionResult> Index()
        {
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
            var error = await RunProjectPredictionAsync();
            if (!string.IsNullOrWhiteSpace(error))
            {
                TempData["AI_ERROR"] = error;
            }
            else
            {
                TempData["AI_SUCCESS"] = "Đã chạy dự báo AI cho dự án.";
            }

            return RedirectToAction(nameof(Index));
        }


        /// GET: Dudoanais/ChayDuBaoNhanVienAI
        public async Task<IActionResult> ChayDuBaoNhanVienAI()
        {
            var error = await RunEmployeePredictionAsync();
            if (!string.IsNullOrWhiteSpace(error))
            {
                TempData["AI_ERROR"] = error;
            }
            else
            {
                TempData["AI_SUCCESS"] = "Đã chạy dự báo AI cho nhân viên.";
            }

            return RedirectToAction(nameof(Index));
        }

        /// GET: Dudoanais/ChayTatCaDuBaoAI
        public async Task<IActionResult> ChayTatCaDuBaoAI()
        {
            var errors = new List<string>();

            var projectError = await RunProjectPredictionAsync();
            if (!string.IsNullOrWhiteSpace(projectError))
            {
                errors.Add(projectError);
            }

            var employeeError = await RunEmployeePredictionAsync();
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
                TempData["AI_SUCCESS"] = "Đã chạy xong toàn bộ dự báo AI (dự án và nhân viên).";
            }

            return RedirectToAction(nameof(Index));
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

                var exist = await _context.Dudoanais.FirstOrDefaultAsync(x =>
                    x.Madoituong == da.Maduan &&
                    x.Thang == DateTime.Now.Month &&
                    x.Nam == DateTime.Now.Year);

                if (exist != null)
                {
                    exist.Diemdudoan = predicted;
                    exist.Xacsuattrehan = predicted;
                    exist.Dexuatcaithien = duDoan.Dexuatcaithien;
                    exist.Gioiynguonluc = duDoan.Gioiynguonluc;
                    exist.Thoigiandudoan = duDoan.Thoigiandudoan;
                }
                else
                {
                    _context.Dudoanais.Add(duDoan);
                }
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

                var exist = await _context.Dudoanais.FirstOrDefaultAsync(x =>
                    x.Manhanvien == nv.Manhanvien &&
                    x.Thang == DateTime.Now.Month &&
                    x.Nam == DateTime.Now.Year);

                if (exist != null)
                {
                    exist.Diemdudoan = predicted;
                    exist.Xacsuattrehan = predicted;
                    exist.Dexuatcaithien = duDoan.Dexuatcaithien;
                    exist.Gioiynguonluc = duDoan.Gioiynguonluc;
                    exist.Thoigiandudoan = duDoan.Thoigiandudoan;
                }
                else
                {
                    _context.Dudoanais.Add(duDoan);
                }
            }

            await _context.SaveChangesAsync();
            return null;
        }
    }
}
