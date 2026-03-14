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
    public class DuansController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DuansController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Duans
        public async Task<IActionResult> Index()
        {
            var duans = await _context.Duans.ToListAsync();

            var today = DateTime.Today;

            foreach (var d in duans)
            {
                if (d.Ngaybatdau.HasValue && d.Ngayketthuc.HasValue)
                {
                    if (today < d.Ngaybatdau.Value)
                        d.Trangthai = 1; // Chưa bắt đầu
                    else if (today >= d.Ngaybatdau.Value && today <= d.Ngayketthuc.Value)
                        d.Trangthai = 2; // Đang thực hiện
                    else
                        d.Trangthai = 3; // Hoàn thành
                }
            }

            await _context.SaveChangesAsync();

            return View(duans);
        }

        // GET: Duans/Details/5
        //public async Task<IActionResult> Details(int? id)
        //{
        //    if (id == null)
        //    {
        //        return NotFound();
        //    }

        //    var duan = await _context.Duans
        //        .Include(d => d.Congviecs.Where(c => c.Macongvieccha == null))
        //        .FirstOrDefaultAsync(m => m.Maduan == id);

        //    if (duan == null)
        //    {
        //        return NotFound();
        //    }

        //    return View(duan);
        //}
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var duan = await _context.Duans
                .Include(d => d.Congviecs)
                    .ThenInclude(c => c.InverseConMacongviecNavigation)
                .FirstOrDefaultAsync(m => m.Maduan == id);

            if (duan == null)
            {
                return NotFound();
            }

            return View(duan);
        }

        // GET: Duans/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Duans/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Tenduan,Mota,Ngaybatdau,Ngayketthuc")] Duan duan)
        {
            if (ModelState.IsValid)
            {
                DateTime today = DateTime.Today;

                if (duan.Ngaybatdau != null && duan.Ngayketthuc != null)
                {
                    if (today < duan.Ngaybatdau)
                        duan.Trangthai = 1;

                    else if (today >= duan.Ngaybatdau && today <= duan.Ngayketthuc)
                        duan.Trangthai = 2;

                    else
                        duan.Trangthai = 3;
                }

                _context.Add(duan);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return View(duan);
        }

        // GET: Duans/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var duan = await _context.Duans.FindAsync(id);
            if (duan == null)
            {
                return NotFound();
            }
            return View(duan);
        }

        // POST: Duans/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Maduan,Tenduan,Mota,Ngaybatdau,Ngayketthuc,Trangthai")] Duan duan)
        {
            if (id != duan.Maduan)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(duan);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!DuanExists(duan.Maduan))
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
            return View(duan);
        }

        // GET: Duans/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var duan = await _context.Duans
                .FirstOrDefaultAsync(m => m.Maduan == id);
            if (duan == null)
            {
                return NotFound();
            }

            return View(duan);
        }

        // POST: Duans/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var duan = await _context.Duans.FindAsync(id);
            if (duan != null)
            {
                _context.Duans.Remove(duan);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool DuanExists(int id)
        {
            return _context.Duans.Any(e => e.Maduan == id);
        }
    }
}
