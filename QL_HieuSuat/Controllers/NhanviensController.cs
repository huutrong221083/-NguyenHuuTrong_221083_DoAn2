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
    [Authorize(Roles = "Admin,QuanLyNhanSu,QuanLyPhongBan")]
    public class NhanviensController : Controller
    {
        private readonly ApplicationDbContext _context;

        public NhanviensController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Nhanviens
        [Authorize(Roles = "Admin,QuanLyNhanSu")]
        public async Task<IActionResult> Index(string searchString)
        {
            var nhanviens = _context.Nhanviens
                                    .Include(n => n.MaphongbanNavigation)
                                    .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                searchString = searchString.ToLower();

                nhanviens = nhanviens.Where(n =>
                    (n.Hoten != null && n.Hoten.ToLower().Contains(searchString)) ||
                    (n.Email != null && n.Email.ToLower().Contains(searchString)) ||
                    (n.Sdt != null && n.Sdt.Contains(searchString)));
            }

            return View(await nhanviens.ToListAsync());
        }

        // GET: Nhanviens/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var nhanvien = await _context.Nhanviens
                .Include(n => n.MaphongbanNavigation)
                .FirstOrDefaultAsync(m => m.Manhanvien == id);
            if (nhanvien == null)
            {
                return NotFound();
            }

            return View(nhanvien);
        }

        // GET: Nhanviens/Create
        [Authorize(Roles = "Admin,QuanLyNhanSu")]
        public IActionResult Create()
        {
            //ViewData["Maphongban"] = new SelectList(_context.Set<Phongban>(), "Maphongban", "Maphongban");
            ViewData["Maphongban"] = new SelectList(_context.Phongbans, "Maphongban", "Tenphongban");
            return View();
        }

        // POST: Nhanviens/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> Create([Bind("Manhanvien,Maphongban,Hoten,Ngaysinh,Cccd,Diachi,Gioitinh,Email,Sdt,Ngayvaolam,Trangthai,Sonamkinhnghiem,DiemkpiTichluy")] Nhanvien nhanvien)
        //{
        //    if (ModelState.IsValid)
        //    {
        //        _context.Add(nhanvien);
        //        await _context.SaveChangesAsync();
        //        return RedirectToAction(nameof(Index));
        //    }
        //    ViewData["Maphongban"] = new SelectList(_context.Set<Phongban>(), "Maphongban", "Maphongban", nhanvien.Maphongban);
        //    return View(nhanvien);
        //}
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,QuanLyNhanSu")]
        public async Task<IActionResult> Create([Bind("Maphongban,Hoten,Ngaysinh,Cccd,Diachi,Gioitinh,Email,Sdt,Ngayvaolam,Trangthai,Sonamkinhnghiem,DiemkpiTichluy")] Nhanvien nhanvien)
        {

            if (ModelState.IsValid)
            {
                _context.Nhanviens.Add(nhanvien);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }


            
            ViewData["Maphongban"] = new SelectList(_context.Phongbans, "Maphongban", "Tenphongban", nhanvien.Maphongban);
            return View(nhanvien);
        }


        // GET: Nhanviens/Edit/5
        [Authorize(Roles = "Admin,QuanLyNhanSu")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var nhanvien = await _context.Nhanviens.FindAsync(id);
            if (nhanvien == null)
            {
                return NotFound();
            }
            //ViewData["Maphongban"] = new SelectList(_context.Set<Phongban>(), "Maphongban", "Maphongban", nhanvien.Maphongban);
            ViewData["Maphongban"] = new SelectList(_context.Phongbans, "Maphongban", "Tenphongban", nhanvien.Maphongban);
            return View(nhanvien);
        }

        // POST: Nhanviens/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,QuanLyNhanSu")]
        public async Task<IActionResult> Edit(int id, [Bind("Manhanvien,Maphongban,Hoten,Ngaysinh,Cccd,Diachi,Gioitinh,Email,Sdt,Ngayvaolam,Trangthai,Sonamkinhnghiem,DiemkpiTichluy")] Nhanvien nhanvien)
        {
            if (id != nhanvien.Manhanvien)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(nhanvien);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!NhanvienExists(nhanvien.Manhanvien))
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
            //ViewData["Maphongban"] = new SelectList(_context.Set<Phongban>(), "Maphongban", "Maphongban", nhanvien.Maphongban);
            ViewData["Maphongban"] = new SelectList(_context.Phongbans, "Maphongban", "Tenphongban", nhanvien.Maphongban);
            return View(nhanvien);
        }

        // GET: Nhanviens/Delete/5
        [Authorize(Roles = "Admin,QuanLyNhanSu")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var nhanvien = await _context.Nhanviens
                .Include(n => n.MaphongbanNavigation)
                .FirstOrDefaultAsync(m => m.Manhanvien == id);
            if (nhanvien == null)
            {
                return NotFound();
            }

            return View(nhanvien);
        }

        // POST: Nhanviens/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,QuanLyNhanSu")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var nhanvien = await _context.Nhanviens.FindAsync(id);
            if (nhanvien != null)
            {
                _context.Nhanviens.Remove(nhanvien);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool NhanvienExists(int id)
        {
            return _context.Nhanviens.Any(e => e.Manhanvien == id);
        }



        [Authorize(Roles = "Admin,QuanLyNhanSu")]
        public async Task<IActionResult> Search(string searchString)
        {
            var nhanviens = _context.Nhanviens
                                    .Include(n => n.MaphongbanNavigation)
                                    .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                searchString = searchString.ToLower();

                nhanviens = nhanviens.Where(n =>
                    (n.Hoten != null && n.Hoten.ToLower().Contains(searchString)) ||
                    (n.Email != null && n.Email.ToLower().Contains(searchString)) ||
                    (n.Sdt != null && n.Sdt.Contains(searchString)));
            }

            return PartialView("_NhanVienTable", await nhanviens.ToListAsync());
        }


        // POST: Nhanviens/RemoveFromPhong
        [HttpPost]
        public async Task<IActionResult> RemoveFromPhong(int manhanvien, int maphongban)
        {
            var nhanvien = await _context.Nhanviens
                .FirstOrDefaultAsync(n => n.Manhanvien == manhanvien);

            if (nhanvien == null)
                return NotFound();

            // kiểm tra nếu nhân viên là trưởng phòng
            var phongban = await _context.Phongbans
                .FirstOrDefaultAsync(p => p.Matruongphong == manhanvien);

            if (phongban != null)
            {
                // bỏ trưởng phòng
                phongban.Matruongphong = null;
            }

            // bỏ nhân viên khỏi phòng
            nhanvien.Maphongban = null;

            await _context.SaveChangesAsync();

            return RedirectToAction("Details", "Phongbans", new { id = maphongban });
        }

        // GET: Nhanviens/AddToPhong/5
        public async Task<IActionResult> AddToPhong(int maphongban)
        {
            var phong = await _context.Phongbans.FindAsync(maphongban);

            if (phong == null)
                return NotFound();

            // lấy nhân viên chưa có phòng
            var nhanviens = await _context.Nhanviens
                .Where(n => n.Maphongban == null)
                .ToListAsync();

            ViewBag.Phong = phong;
            ViewBag.Nhanviens = nhanviens;

            return View();
        }


        // POST: Nhanviens/AddToPhong
        [HttpPost]
        public async Task<IActionResult> AddNhanVienToPhong(int manhanvien, int maphongban)
        {
            var nhanvien = await _context.Nhanviens.FindAsync(manhanvien);

            if (nhanvien == null)
                return NotFound();

            nhanvien.Maphongban = maphongban;

            await _context.SaveChangesAsync();

            return RedirectToAction("Details", "Phongbans", new { id = maphongban });
        }
    }
}
