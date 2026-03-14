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
    [Authorize(Roles = "Admin")]
    public class NhomsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public NhomsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Nhoms
        public async Task<IActionResult> Index()
        {
            var nhoms = await _context.Nhoms
                .Include(n => n.Thanhviennhoms)
                .ThenInclude(tv => tv.ManhanvienNavigation)
                .ToListAsync();

            return View(nhoms);
        }


        // Danh sach thanh vien trong nhom
        public async Task<IActionResult> ThanhVien(int id)
        {
            var nhom = await _context.Nhoms
                .Include(n => n.Thanhviennhoms)
                    .ThenInclude(tv => tv.ManhanvienNavigation)
                        .ThenInclude(nv => nv.Kynangnhanviens)
                            .ThenInclude(kn => kn.MakynangNavigation)
                .FirstOrDefaultAsync(n => n.Manhom == id);

            if (nhom == null)
                return NotFound();

            return View(nhom);
        }

        // GET: Nhoms/ThemThanhVien/5
        public IActionResult ThemThanhVien(int id)
        {
            ViewBag.NhomId = id;

            ViewBag.Nhanviens = new SelectList(
                _context.Nhanviens,
                "Manhanvien",
                "Hoten"
            );

            return View();
        }

        // POST: Nhoms/ThemThanhVien
        [HttpPost]
        public async Task<IActionResult> ThemThanhVien(int Manhom, int Manhanvien)
        {
            var tonTai = await _context.Thanhviennhoms
                .AnyAsync(x => x.Manhanvien == Manhanvien && x.Manhom == Manhom);

            if (tonTai)
            {
                return Json(new
                {
                    success = false,
                    message = "Nhân viên đã có trong nhóm này"
                });
            }

            var tv = new Thanhviennhom
            {
                Manhom = Manhom,
                Manhanvien = Manhanvien,
                Vaitrotrongnhom = "Thành viên"
            };

            _context.Thanhviennhoms.Add(tv);

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                manhanvien = Manhanvien
            });
        }

        // GET: Nhoms/XoaThanhVien/5?manhanvien=10
        public async Task<IActionResult> XoaThanhVien(int manhom, int manhanvien)
        {
            var tv = await _context.Thanhviennhoms
                .FirstOrDefaultAsync(x => x.Manhom == manhom && x.Manhanvien == manhanvien);

            if (tv == null)
            {
                return Json(new { success = false });
            }

            // nếu là trưởng nhóm thì không cho xóa
            if (tv.Vaitrotrongnhom == "Trưởng nhóm")
            {
                return Json(new
                {
                    success = false,
                    message = "Không thể xóa trưởng nhóm. Hãy đổi trưởng nhóm trước."
                });
            }

            _context.Thanhviennhoms.Remove(tv);

            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }



        // GET: Nhoms/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var nhom = await _context.Nhoms
                .Include(n => n.Thanhviennhoms)
                    .ThenInclude(tv => tv.ManhanvienNavigation)
                .FirstOrDefaultAsync(m => m.Manhom == id);

            if (nhom == null)
            {
                return NotFound();
            }

            return View(nhom);
        }

        // GET: Nhoms/Create
        public IActionResult Create()
        {
            var nhanviens = _context.Nhanviens
                .Include(n => n.Thanhviennhoms)
                .ThenInclude(tv => tv.ManhomNavigation)
                .ToList();

            var list = new List<SelectListItem>();

            foreach (var nv in nhanviens)
            {
                var truongNhom = nv.Thanhviennhoms
                    .FirstOrDefault(tv => tv.Vaitrotrongnhom == "Trưởng nhóm");

                string text = nv.Hoten ?? "";

                if (truongNhom != null)
                {
                    text += $" (Trưởng nhóm {truongNhom.ManhomNavigation.Tennhom})";
                }

                list.Add(new SelectListItem
                {
                    Value = nv.Manhanvien.ToString(),
                    Text = text
                });
            }

            ViewBag.Nhanviens = list;

            return View();
        }

        // POST: Nhoms/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Nhom nhom, int TruongNhomId, List<int> ThanhVienIds)
        {
            if (ModelState.IsValid)
            {
                nhom.Ngaytao = DateTime.Now;

                _context.Nhoms.Add(nhom);
                await _context.SaveChangesAsync();

                int manhom = nhom.Manhom;

                // Thêm trưởng nhóm
                var truongNhom = new Thanhviennhom
                {
                    Manhom = manhom,
                    Manhanvien = TruongNhomId,
                    Ngaygianhap = DateTime.Now,
                    Vaitrotrongnhom = "Trưởng nhóm"
                };

                _context.Thanhviennhoms.Add(truongNhom);

                // Thêm thành viên
                foreach (var id in ThanhVienIds)
                {
                    if (id != TruongNhomId)
                    {
                        _context.Thanhviennhoms.Add(new Thanhviennhom
                        {
                            Manhom = manhom,
                            Manhanvien = id,
                            Ngaygianhap = DateTime.Now,
                            Vaitrotrongnhom = "Thành viên"
                        });
                    }
                }

                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            ViewBag.Nhanviens = new SelectList(_context.Nhanviens, "Manhanvien", "Hoten");
            return View(nhom);
        }

        // GET: Nhoms/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var nhom = await _context.Nhoms
                .Include(n => n.Thanhviennhoms)
                .ThenInclude(tv => tv.ManhanvienNavigation)
                .FirstOrDefaultAsync(n => n.Manhom == id);

            if (nhom == null)
            {
                return NotFound();
            }

            var nhanviens = _context.Nhanviens
                .Select(n => new SelectListItem
                {
                    Value = n.Manhanvien.ToString(),
                    Text = n.Hoten
                }).ToList();

            ViewBag.Nhanviens = nhanviens;

            return View(nhom);
        }

        // POST: Nhoms/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Manhom,Tennhom,Ngaytao")] Nhom nhom)
        {
            if (id != nhom.Manhom)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(nhom);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!NhomExists(nhom.Manhom))
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
            return View(nhom);
        }

        // GET: Nhoms/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var nhom = await _context.Nhoms
                .Include(n => n.Thanhviennhoms)
                .Include(n => n.Phancongcongviecs)
                    .ThenInclude(pc => pc.MacongviecNavigation)
                        .ThenInclude(cv => cv.MaduanNavigation)
                .FirstOrDefaultAsync(m => m.Manhom == id);

            if (nhom == null)
                return NotFound();

            return View(nhom);
        }

        // POST: Nhoms/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var nhom = await _context.Nhoms
                .Include(n => n.Thanhviennhoms)
                .Include(n => n.Phancongcongviecs)
                    .ThenInclude(pc => pc.MacongviecNavigation)
                        .ThenInclude(cv => cv.MaduanNavigation)
                .FirstOrDefaultAsync(n => n.Manhom == id);

            if (nhom == null)
                return NotFound();

            // 1️⃣ Kiểm tra còn thành viên
            if (nhom.Thanhviennhoms.Any())
            {
                TempData["Error"] = "❌ Không thể xóa nhóm vì vẫn còn thành viên trong nhóm.";
                return RedirectToAction("Delete", new { id = id });
            }

            // 2️⃣ Kiểm tra đang có công việc / dự án
            var duans = nhom.Phancongcongviecs
                .Where(pc => pc.MacongviecNavigation?.MaduanNavigation != null)
                .Select(pc => pc.MacongviecNavigation.MaduanNavigation.Tenduan)
                .Distinct()
                .ToList();

            if (duans.Any())
            {
                TempData["Error"] = "❌ Không thể xóa nhóm vì đang tham gia dự án: " + string.Join(", ", duans);
                return RedirectToAction("Delete", new { id = id });
            }

            _context.Nhoms.Remove(nhom);
            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ Xóa nhóm thành công";

            return RedirectToAction(nameof(Index));
        }



        // POST: Nhoms/DoiTruongNhom
        [HttpPost]
        public async Task<IActionResult> DoiTruongNhom(int manhom, int manhanvien)
        {
            var thanhviens = await _context.Thanhviennhoms
                .Where(x => x.Manhom == manhom)
                .ToListAsync();

            // Đưa trưởng nhóm cũ thành thành viên
            foreach (var tv in thanhviens)
            {
                tv.Vaitrotrongnhom = "Thành viên";
            }

            // Kiểm tra nhân viên có trong nhóm chưa
            var tvMoi = thanhviens.FirstOrDefault(x => x.Manhanvien == manhanvien);

            if (tvMoi != null)
            {
                // Nếu đã ở trong nhóm
                tvMoi.Vaitrotrongnhom = "Trưởng nhóm";
            }
            else
            {
                // Nếu chưa trong nhóm -> thêm mới
                var thanhvien = new Thanhviennhom
                {
                    Manhom = manhom,
                    Manhanvien = manhanvien,
                    Vaitrotrongnhom = "Trưởng nhóm"
                };

                _context.Thanhviennhoms.Add(thanhvien);
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }


        private bool NhomExists(int id)
        {
            return _context.Nhoms.Any(e => e.Manhom == id);
        }
    }
}
