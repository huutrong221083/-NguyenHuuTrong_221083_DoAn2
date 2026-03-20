using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QL_HieuSuat.Models;
using QL_HieuSuat.Models.ViewModels;
using System.Collections.Generic;
using System.Linq;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private static readonly string[] AllowedAssignableRoles =
    {
        "NhanVien",
        "TruongPhong",
        "QuanLyPhongBan",
        "TruongNhom",
        "QuanLyNhanSu",
        "Admin"
    };

    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public AdminController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public IActionResult Index()
    {
        ViewBag.TotalNhanVien = _context.Nhanviens.Count();
        ViewBag.TotalKPI = _context.Ketquakpis.Count();

        var avg = _context.Ketquakpis
            .Where(x => x.Diemso.HasValue)
            .Average(x => x.Diemso.Value);

        ViewBag.AvgKPI = Math.Round(avg, 2);

        // TOP 5 nhân viên KPI cao nhất
        var topNhanVien = _context.Nhanviens
            .Select(nv => new
            {
                Ten = nv.Hoten,
                Diem = nv.Ketquakpis.Average(k => k.Diemso) ?? 0
            })
            .OrderByDescending(x => x.Diem)
            .Take(5)
            .ToList();

        ViewBag.TopNhanVien = topNhanVien;

        // Công việc đã trễ
        var congViecTre = _context.Congviecs
            .Where(c => c.Hanhoanthanh < DateTime.Now && c.Matrangthai != 3)
            .Select(c => new
            {
                Ten = c.Tencongviec,
                Han = c.Hanhoanthanh
            })
            .ToList();

        // Công việc sắp trễ (3 ngày)
        var congViecSapTre = _context.Congviecs
            .Where(c => c.Hanhoanthanh >= DateTime.Now &&
                        c.Hanhoanthanh <= DateTime.Now.AddDays(3) &&
                        c.Matrangthai != 3)
            .Select(c => new
            {
                Ten = c.Tencongviec,
                Han = c.Hanhoanthanh
            })
            .ToList();

        ViewBag.CongViecTre = congViecTre;
        ViewBag.CongViecSapTre = congViecSapTre;

        // Thông báo mới nhất
        var thongBaoMoi = _context.Thongbaos
            .OrderByDescending(tb => tb.Thoigian)
            .Take(5)
            .Select(tb => new
            {
                NoiDung = tb.Noidung,
                ThoiGian = tb.Thoigian
            })
            .ToList();

        ViewBag.ThongBaoMoi = thongBaoMoi;

        // KPI theo tháng
        var kpiByMonth = _context.Ketquakpis
            .Where(x => x.Thang != null)
            .GroupBy(x => x.Thang)
            .Select(g => new
            {
                Thang = g.Key,
                Diem = g.Average(x => x.Diemso)
            })
            .OrderBy(x => x.Thang)
            .ToList();

        ViewBag.KpiThang = kpiByMonth;

        // KPI theo phòng ban
        var kpiPhongBan = _context.Phongbans
            .Select(pb => new
            {
                PhongBan = pb.Tenphongban,
                Diem = pb.Nhanviens
                    .SelectMany(nv => nv.Ketquakpis)
                    .Where(k => k.Diemso.HasValue)
                    .Average(k => k.Diemso) ?? 0
            })
            .ToList();

        ViewBag.KpiPhongBan = kpiPhongBan;

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> TaoTaiKhoan()
    {
        await PopulateNhanVienSelectListAsync();
        PopulateRoleSelectList();

        return View(new TaoTaiKhoanViewModel
        {
            Role = "NhanVien"
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TaoTaiKhoan(TaoTaiKhoanViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateNhanVienSelectListAsync(model.MaNhanVien);
            PopulateRoleSelectList(model.Role);
            return View(model);
        }

        if (await _userManager.FindByEmailAsync(model.Email) != null)
        {
            ModelState.AddModelError(nameof(model.Email), "Email đã tồn tại.");
            await PopulateNhanVienSelectListAsync(model.MaNhanVien);
            PopulateRoleSelectList(model.Role);
            return View(model);
        }

        if (model.MaNhanVien.HasValue)
        {
            var nhanVienExists = await _context.Nhanviens
                .AsNoTracking()
                .AnyAsync(nv => nv.Manhanvien == model.MaNhanVien.Value);

            if (!nhanVienExists)
            {
                ModelState.AddModelError(nameof(model.MaNhanVien), "Mã nhân viên không tồn tại.");
                await PopulateNhanVienSelectListAsync(model.MaNhanVien);
                PopulateRoleSelectList(model.Role);
                return View(model);
            }

            var daCoTaiKhoan = await _context.Users
                .AsNoTracking()
                .AnyAsync(u => u.MaNhanVien == model.MaNhanVien.Value);

            if (daCoTaiKhoan)
            {
                ModelState.AddModelError(nameof(model.MaNhanVien), "Nhân viên này đã có tài khoản.");
                await PopulateNhanVienSelectListAsync(model.MaNhanVien);
                PopulateRoleSelectList(model.Role);
                return View(model);
            }
        }

        var selectedRole = AllowedAssignableRoles.Contains(model.Role) ? model.Role : string.Empty;
        if (string.IsNullOrWhiteSpace(selectedRole) || !await _roleManager.RoleExistsAsync(selectedRole))
        {
            ModelState.AddModelError(nameof(model.Role), "Vai trò không hợp lệ.");
            await PopulateNhanVienSelectListAsync(model.MaNhanVien);
            PopulateRoleSelectList(model.Role);
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            MaNhanVien = model.MaNhanVien
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, TranslateIdentityError(error.Description));
            }

            await PopulateNhanVienSelectListAsync(model.MaNhanVien);
            PopulateRoleSelectList(model.Role);
            return View(model);
        }

        var rolesToAdd = new List<string> { selectedRole };

        foreach (var role in rolesToAdd.Distinct())
        {
            var roleResult = await _userManager.AddToRoleAsync(user, role);
            if (roleResult.Succeeded)
            {
                continue;
            }

            foreach (var error in roleResult.Errors)
            {
                ModelState.AddModelError(string.Empty, TranslateIdentityError(error.Description));
            }

            await _userManager.DeleteAsync(user);
            await PopulateNhanVienSelectListAsync(model.MaNhanVien);
            PopulateRoleSelectList(model.Role);
            return View(model);
        }

        TempData["UploadSuccess"] = "Tạo tài khoản thành công.";
        return RedirectToAction(nameof(TaoTaiKhoan));
    }

    [HttpGet]
    public async Task<IActionResult> DanhSachTaiKhoan()
    {
        var users = await _context.Users
            .Include(u => u.Nhanvien)
            .OrderBy(u => u.Email)
            .ToListAsync();

        var viewModel = new List<TaiKhoanItemViewModel>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);

            viewModel.Add(new TaiKhoanItemViewModel
            {
                UserId = user.Id,
                Email = user.Email,
                UserName = user.UserName,
                VaiTro = string.Join(", ", roles),
                MaNhanVien = user.MaNhanVien,
                TenNhanVien = user.Nhanvien?.Hoten
            });
        }

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> SuaTaiKhoan(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return NotFound();
        }

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            return NotFound();
        }

        var roles = await _userManager.GetRolesAsync(user);
        var selectedRole = roles.FirstOrDefault(r => r != "NhanVien")
            ?? (roles.Contains("NhanVien") ? "NhanVien" : "NhanVien");

        await PopulateNhanVienSelectListAsync(user.MaNhanVien);
        PopulateRoleSelectList(selectedRole);

        return View(new SuaTaiKhoanViewModel
        {
            UserId = user.Id,
            Email = user.Email ?? string.Empty,
            MaNhanVien = user.MaNhanVien,
            Role = selectedRole
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SuaTaiKhoan(SuaTaiKhoanViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateNhanVienSelectListAsync(model.MaNhanVien);
            PopulateRoleSelectList(model.Role);
            return View(model);
        }

        var user = await _userManager.FindByIdAsync(model.UserId);
        if (user == null)
        {
            return NotFound();
        }

        var emailOwner = await _userManager.FindByEmailAsync(model.Email);
        if (emailOwner != null && emailOwner.Id != user.Id)
        {
            ModelState.AddModelError(nameof(model.Email), "Email đã thuộc tài khoản khác.");
            await PopulateNhanVienSelectListAsync(model.MaNhanVien);
            PopulateRoleSelectList(model.Role);
            return View(model);
        }

        if (model.MaNhanVien.HasValue)
        {
            var nhanVienExists = await _context.Nhanviens
                .AsNoTracking()
                .AnyAsync(nv => nv.Manhanvien == model.MaNhanVien.Value);

            if (!nhanVienExists)
            {
                ModelState.AddModelError(nameof(model.MaNhanVien), "Mã nhân viên không tồn tại.");
                await PopulateNhanVienSelectListAsync(model.MaNhanVien);
                PopulateRoleSelectList(model.Role);
                return View(model);
            }

            var daGanTaiKhoanKhac = await _context.Users
                .AsNoTracking()
                .AnyAsync(u => u.MaNhanVien == model.MaNhanVien.Value && u.Id != user.Id);

            if (daGanTaiKhoanKhac)
            {
                ModelState.AddModelError(nameof(model.MaNhanVien), "Nhân viên này đã gắn với tài khoản khác.");
                await PopulateNhanVienSelectListAsync(model.MaNhanVien);
                PopulateRoleSelectList(model.Role);
                return View(model);
            }
        }

        var selectedRole = AllowedAssignableRoles.Contains(model.Role) ? model.Role : string.Empty;
        if (string.IsNullOrWhiteSpace(selectedRole) || !await _roleManager.RoleExistsAsync(selectedRole))
        {
            ModelState.AddModelError(nameof(model.Role), "Vai trò không hợp lệ.");
            await PopulateNhanVienSelectListAsync(model.MaNhanVien);
            PopulateRoleSelectList(model.Role);
            return View(model);
        }

        user.Email = model.Email;
        user.UserName = model.Email;
        user.MaNhanVien = model.MaNhanVien;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            foreach (var error in updateResult.Errors)
            {
                ModelState.AddModelError(string.Empty, TranslateIdentityError(error.Description));
            }

            await PopulateNhanVienSelectListAsync(model.MaNhanVien);
            PopulateRoleSelectList(model.Role);
            return View(model);
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Any())
        {
            var removeRolesResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeRolesResult.Succeeded)
            {
                foreach (var error in removeRolesResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, TranslateIdentityError(error.Description));
                }

                await PopulateNhanVienSelectListAsync(model.MaNhanVien);
                PopulateRoleSelectList(model.Role);
                return View(model);
            }
        }

        var rolesToAdd = new List<string> { selectedRole };

        var addRolesResult = await _userManager.AddToRolesAsync(user, rolesToAdd.Distinct());
        if (!addRolesResult.Succeeded)
        {
            foreach (var error in addRolesResult.Errors)
            {
                ModelState.AddModelError(string.Empty, TranslateIdentityError(error.Description));
            }

            await PopulateNhanVienSelectListAsync(model.MaNhanVien);
            PopulateRoleSelectList(model.Role);
            return View(model);
        }

        TempData["UploadSuccess"] = "Cập nhật tài khoản thành công.";
        return RedirectToAction(nameof(DanhSachTaiKhoan));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> XoaTaiKhoan(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return RedirectToAction(nameof(DanhSachTaiKhoan));
        }

        var currentUserId = _userManager.GetUserId(User);
        if (currentUserId == id)
        {
            TempData["UploadError"] = "Không thể xóa chính tài khoản đang đăng nhập.";
            return RedirectToAction(nameof(DanhSachTaiKhoan));
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            TempData["UploadError"] = "Không tìm thấy tài khoản cần xóa.";
            return RedirectToAction(nameof(DanhSachTaiKhoan));
        }

        var deleteResult = await _userManager.DeleteAsync(user);
        if (!deleteResult.Succeeded)
        {
            TempData["UploadError"] = string.Join("; ", deleteResult.Errors.Select(e => TranslateIdentityError(e.Description)));
            return RedirectToAction(nameof(DanhSachTaiKhoan));
        }

        TempData["UploadSuccess"] = "Đã xóa tài khoản.";
        return RedirectToAction(nameof(DanhSachTaiKhoan));
    }

    private async Task PopulateNhanVienSelectListAsync(int? selectedId = null)
    {
        var linkedNhanVienIds = await _context.Users
            .Where(u => u.MaNhanVien.HasValue)
            .Select(u => u.MaNhanVien!.Value)
            .ToListAsync();

        var nhanVienItems = await _context.Nhanviens
            .AsNoTracking()
            .Where(nv => !linkedNhanVienIds.Contains(nv.Manhanvien) || (selectedId.HasValue && nv.Manhanvien == selectedId.Value))
            .OrderBy(nv => nv.Hoten)
            .Select(nv => new
            {
                nv.Manhanvien,
                nv.Hoten
            })
            .ToListAsync();

        var options = nhanVienItems
            .Select(nv => new SelectListItem
            {
                Value = nv.Manhanvien.ToString(),
                Text = $"{nv.Hoten} (#{nv.Manhanvien})"
            })
            .ToList();

        options.Insert(0, new SelectListItem
        {
            Value = string.Empty,
            Text = "-- Không liên kết nhân viên --"
        });

        ViewBag.NhanVienOptions = options;
    }

    private void PopulateRoleSelectList(string? selectedRole = null)
    {
        var roles = new List<SelectListItem>
        {
            new() { Value = "NhanVien", Text = "Nhân viên" },
            new() { Value = "TruongPhong", Text = "Trưởng phòng" },
            new() { Value = "QuanLyPhongBan", Text = "Quản lý phòng ban" },
            new() { Value = "TruongNhom", Text = "Trưởng nhóm" },
            new() { Value = "QuanLyNhanSu", Text = "Quản lý nhân sự" },
            new() { Value = "Admin", Text = "Admin" }
        };

        foreach (var role in roles)
        {
            role.Selected = role.Value == (selectedRole ?? "NhanVien");
        }

        ViewBag.RoleOptions = roles;
    }

    private static string TranslateIdentityError(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Có lỗi xác thực xảy ra.";
        }

        var text = message;
        return text
            .Replace("Username", "Tên đăng nhập", StringComparison.OrdinalIgnoreCase)
            .Replace("User name", "Tên đăng nhập", StringComparison.OrdinalIgnoreCase)
            .Replace("is already taken", "đã được sử dụng", StringComparison.OrdinalIgnoreCase)
            .Replace("is already in use", "đã được sử dụng", StringComparison.OrdinalIgnoreCase)
            .Replace("Passwords must be at least", "Mật khẩu phải có ít nhất", StringComparison.OrdinalIgnoreCase)
            .Replace("characters.", "ký tự.", StringComparison.OrdinalIgnoreCase)
            .Replace("Passwords must have at least one non alphanumeric character.", "Mật khẩu phải có ít nhất một ký tự đặc biệt.", StringComparison.OrdinalIgnoreCase)
            .Replace("Passwords must have at least one lowercase ('a'-'z').", "Mật khẩu phải có ít nhất một chữ thường (a-z).", StringComparison.OrdinalIgnoreCase)
            .Replace("Passwords must have at least one uppercase ('A'-'Z').", "Mật khẩu phải có ít nhất một chữ hoa (A-Z).", StringComparison.OrdinalIgnoreCase)
            .Replace("Passwords must have at least one digit ('0'-'9').", "Mật khẩu phải có ít nhất một chữ số (0-9).", StringComparison.OrdinalIgnoreCase)
            .Replace("Invalid token.", "Mã xác thực không hợp lệ.", StringComparison.OrdinalIgnoreCase)
            .Replace("is invalid", "không hợp lệ", StringComparison.OrdinalIgnoreCase);
    }
}