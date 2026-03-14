using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_HieuSuat.Models;

namespace QL_HieuSuat.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            // Tong so nhan vien
            ViewBag.TotalNhanVien = _context.Nhanviens.Count();

            // Tong KPI thang hien tai
            int thang = DateTime.Now.Month;
            int nam = DateTime.Now.Year;

            ViewBag.TotalKPI = _context.Ketquakpis
                .Where(k => k.Thang == thang && k.Nam == nam)
                .Count();

            // Hieu suat trung binh
            var avgKPI = _context.Ketquakpis
                .Where(k => k.Thang == thang && k.Nam == nam)
                .Average(k => (double?)k.Diemso) ?? 0;

            ViewBag.AvgKPI = Math.Round(avgKPI, 2);

            // Danh sach nhan vien + KPI
            var nhanviens = _context.Nhanviens
                .Include(n => n.MaphongbanNavigation)
                .Take(5)
                .ToList();

            ViewBag.NhanVienGanDay = nhanviens;

            return View();
        }
    }
}