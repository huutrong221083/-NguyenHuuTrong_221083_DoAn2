using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public AccountController(SignInManager<ApplicationUser> signInManager,
                             UserManager<ApplicationUser> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
    }

    // GET: Login
    public IActionResult Login()
    {
        return View();
    }

    // GET: ForgotPassword
    public IActionResult ForgotPassword()
    {
        return View();
    }

    // POST: Login
    [HttpPost]
    public async Task<IActionResult> Login(string email, string password)
    {
        var user = await _userManager.FindByEmailAsync(email);

        if (user == null)
        {
            ViewBag.Error = "Sai tài khoản";
            return View();
        }

        var result = await _signInManager.PasswordSignInAsync(user, password, false, false);

        if (!result.Succeeded)
        {
            ViewBag.Error = "Sai mật khẩu";
            return View();
        }

        var roles = await _userManager.GetRolesAsync(user);

        if (roles.Contains("Admin"))
            return RedirectToAction("Index", "Admin");

        if (roles.Contains("QuanLyNhanSu"))
            return RedirectToAction("Index", "QuanLyNhanSu");

        if (roles.Contains("TruongPhong"))
            return RedirectToAction("Index", "QuanLyPhongBan");

        if (roles.Contains("QuanLyPhongBan"))
            return RedirectToAction("Index", "Phongbans");

        return RedirectToAction("Index", "NhanVien");
    }


    // POST: ForgotPassword
    [HttpPost]
    public async Task<IActionResult> ForgotPassword(string email, string newPassword, string confirmPassword)
    {
        if (newPassword != confirmPassword)
        {
            ViewBag.Error = "Mật khẩu xác nhận không khớp!";
            return View();
        }

        var user = await _userManager.FindByEmailAsync(email);

        if (user == null)
        {
            ViewBag.Error = "Email không tồn tại!";
            return View();
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);

        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

        if (result.Succeeded)
        {
            ViewBag.Success = "Đổi mật khẩu thành công!";
        }
        else
        {
            ViewBag.Error = string.Join("<br>", result.Errors.Select(e => e.Description));
        }

        return View();
    }


    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Login");
    }


}