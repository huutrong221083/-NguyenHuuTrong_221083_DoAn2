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
    [Authorize(Roles = "Admin,QuanLyPhongBan")]
    public class PhongbansController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PhongbansController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Phongbans

        public async Task<IActionResult> Index(string searchString)
        {
            var phongbans = _context.Phongbans
                                    .Include(p => p.Nhanviens)
                                    .Include(p => p.MatruongphongNavigation)
                                    .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                phongbans = phongbans.Where(p =>
                    p.Tenphongban.Contains(searchString));
            }

            return View(await phongbans.ToListAsync());
        }

        // GET: Phongbans/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var phongban = await _context.Phongbans
                .Include(p => p.Nhanviens)  
                .Include(p => p.MatruongphongNavigation)
                .FirstOrDefaultAsync(m => m.Maphongban == id);

            if (phongban == null)
            {
                return NotFound();
            }

            return View(phongban);
        }

        // GET: Phongbans/Create
        public IActionResult Create()
        {
            var nhanvienList = _context.Nhanviens
                .Where(n => !_context.Phongbans
                    .Any(p => p.Matruongphong == n.Manhanvien))
                .ToList();

            ViewBag.Matruongphong = new SelectList(
                nhanvienList,
                "Manhanvien",
                "Hoten"
            );

            return View();
        }

        // POST: Phongbans/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Maphongban,Tenphongban,Mota,Matruongphong")] Phongban phongban)
        {
            // kiểm tra trùng tên phòng ban
            if (!string.IsNullOrEmpty(phongban.Tenphongban))
            {
                bool exists = await _context.Phongbans
                    .AnyAsync(p => p.Tenphongban.ToLower() == phongban.Tenphongban.ToLower());

                if (exists)
                {
                    ModelState.AddModelError("Tenphongban", "Tên phòng ban đã tồn tại!");
                }
            }

            if (ModelState.IsValid)
            {
                _context.Add(phongban);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            // load lại dropdown
            var nhanvienList = _context.Nhanviens
                .Where(n => !_context.Phongbans
                    .Any(p => p.Matruongphong == n.Manhanvien))
                .ToList();

            ViewBag.Matruongphong = new SelectList(
                nhanvienList,
                "Manhanvien",
                "Hoten",
                phongban.Matruongphong
            );

            return View(phongban);
        }

        // GET: Phongbans/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var phongban = await _context.Phongbans.FindAsync(id);

            if (phongban == null)
            {
                return NotFound();
            }

            var nhanvienList = _context.Nhanviens
                .Where(n => !_context.Phongbans
                    .Any(p => p.Matruongphong == n.Manhanvien)
                    || n.Manhanvien == phongban.Matruongphong)
                .ToList();

            ViewBag.Matruongphong = new SelectList(
                nhanvienList,
                "Manhanvien",
                "Hoten",
                phongban.Matruongphong
            );

            return View(phongban);
        }

        // POST: Phongbans/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Maphongban,Tenphongban,Mota,Matruongphong")] Phongban phongban)
        {
            if (id != phongban.Maphongban)
            {
                return NotFound();
            }

            // kiểm tra nhân viên đã là trưởng phòng phòng khác chưa
            if (phongban.Matruongphong != null)
            {
                bool daLaTruongPhong = await _context.Phongbans
                    .AnyAsync(p => p.Matruongphong == phongban.Matruongphong
                                && p.Maphongban != phongban.Maphongban);

                if (daLaTruongPhong)
                {
                    TempData["Error"] = "❌ Nhân viên này đã là trưởng phòng của phòng khác!";
                    return RedirectToAction(nameof(Index));
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // lấy phòng ban cũ trong database
                    var phongbanDb = await _context.Phongbans
                        .FirstOrDefaultAsync(p => p.Maphongban == id);

                    if (phongbanDb == null)
                    {
                        return NotFound();
                    }

                    // cập nhật thông tin
                    phongbanDb.Tenphongban = phongban.Tenphongban;
                    phongbanDb.Mota = phongban.Mota;
                    phongbanDb.Matruongphong = phongban.Matruongphong;

                    // nếu có trưởng phòng
                    if (phongban.Matruongphong != null)
                    {
                        var truongPhong = await _context.Nhanviens
                            .FirstOrDefaultAsync(nv => nv.Manhanvien == phongban.Matruongphong);

                        if (truongPhong != null)
                        {
                            // gán trưởng phòng vào phòng ban
                            truongPhong.Maphongban = phongban.Maphongban;
                        }
                    }

                    await _context.SaveChangesAsync();

                    TempData["Success"] = "✔ Cập nhật phòng ban thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PhongbanExists(phongban.Maphongban))
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

            var nhanvienList = _context.Nhanviens
                .Where(n => !_context.Phongbans
                    .Any(p => p.Matruongphong == n.Manhanvien)
                    || n.Manhanvien == phongban.Matruongphong)
                .ToList();

            ViewBag.Matruongphong = new SelectList(
                nhanvienList,
                "Manhanvien",
                "Hoten",
                phongban.Matruongphong
            );

            return View(phongban);
        }

        // GET: Phongbans/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var phongban = await _context.Phongbans
                .Include(p => p.MatruongphongNavigation)
                .Include(p => p.Nhanviens)
                .FirstOrDefaultAsync(m => m.Maphongban == id);

            if (phongban == null)
            {
                return NotFound();
            }

            return View(phongban);
        }

        // POST: Phongbans/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var phongban = await _context.Phongbans
                .Include(p => p.Nhanviens)
                .FirstOrDefaultAsync(p => p.Maphongban == id);

            if (phongban == null)
            {
                return NotFound();
            }

            // ❌ Không cho xóa nếu còn nhân viên
            if (phongban.Nhanviens != null && phongban.Nhanviens.Any())
            {
                TempData["Error"] = "Không thể xóa phòng ban khi còn nhân viên!";
                return RedirectToAction(nameof(Index));
            }

            _context.Phongbans.Remove(phongban);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Xóa phòng ban thành công!";
            return RedirectToAction(nameof(Index));
        }

        private bool PhongbanExists(int id)
        {
            return _context.Phongbans.Any(e => e.Maphongban == id);
        }
    }
}
