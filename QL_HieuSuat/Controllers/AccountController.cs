using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QL_HieuSuat.Models;
using QL_HieuSuat.Services;
using QL_HieuSuat.Models.ViewModels;
using System.Security.Cryptography;

public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IMemoryCache _memoryCache;
    private readonly IEmailService _emailService;
    private readonly ILogger<AccountController> _logger;
    private readonly QlHieuSuatContext _qlHieuSuatContext;

    public AccountController(SignInManager<ApplicationUser> signInManager,
                             UserManager<ApplicationUser> userManager,
                             IMemoryCache memoryCache,
                             IEmailService emailService,
                             ILogger<AccountController> logger,
                             QlHieuSuatContext qlHieuSuatContext)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _memoryCache = memoryCache;
        _emailService = emailService;
        _logger = logger;
        _qlHieuSuatContext = qlHieuSuatContext;
    }

    // GET: Login
    public IActionResult Login()
    {
        return View(new LoginViewModel());
    }

    // GET: ForgotPassword
    public IActionResult ForgotPassword()
    {
        return View(new ForgotPasswordRequestViewModel());
    }

    // GET: VerifyResetCode
    public IActionResult VerifyResetCode(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            TempData["Error"] = "Thiếu thông tin email.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        return View(new VerifyResetCodeViewModel { Email = email });
    }

    // GET: ResetPassword
    public IActionResult ResetPassword(string? email, string? ticket)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(ticket))
        {
            TempData["Error"] = "Yêu cầu đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        var verifiedKey = GetVerifiedKey(email);
        if (!_memoryCache.TryGetValue(verifiedKey, out string? expectedTicket) ||
            !string.Equals(expectedTicket, ticket, StringComparison.Ordinal))
        {
            TempData["Error"] = "Mã xác nhận không hợp lệ hoặc đã hết hạn.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        return View(new ResetPasswordWithCodeViewModel
        {
            Email = email,
            VerificationTicket = ticket
        });
    }

    // GET: ResetPasswordConfirmation
    public IActionResult ResetPasswordConfirmation()
    {
        return View();
    }

    public IActionResult AccessDenied()
    {
        return View();
    }

    // POST: Login
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);

        if (user == null)
        {
            ModelState.AddModelError(nameof(model.Email), "Sai tài khoản");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(user, model.Password, false, false);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(nameof(model.Password), "Sai mật khẩu");
            return View(model);
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
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequestViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var normalizedEmail = model.Email.Trim();
        var normalizedEmailLower = normalizedEmail.ToLowerInvariant();

        var employeeExists = await _qlHieuSuatContext.Nhanviens
            .AsNoTracking()
            .AnyAsync(nv => nv.Email != null && nv.Email.ToLower() == normalizedEmailLower);

        if (!employeeExists)
        {
            ModelState.AddModelError(nameof(model.Email), "Email không tồn tại trong hồ sơ nhân viên.");
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user == null || string.IsNullOrWhiteSpace(user.Email))
        {
            ModelState.AddModelError(nameof(model.Email), "Email đã có trong hồ sơ nhân viên nhưng chưa có tài khoản đăng nhập.");
            return View(model);
        }

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            var verificationCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            var cacheKey = GetCodeKey(user.Email);
            var payload = new PasswordResetCodePayload
            {
                Code = verificationCode,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10)
            };

            _memoryCache.Set(cacheKey, payload, payload.ExpiresAtUtc);

            try
            {
                var subject = "Mã xác nhận đặt lại mật khẩu";
                var body = $"Mã xác nhận của bạn là: {verificationCode}. Mã có hiệu lực trong 10 phút.";
                await _emailService.SendEmailAsync(user.Email, subject, body);

                _logger.LogInformation("Da gui ma xac minh dat lai mat khau den email {Email}", user.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Loi gui email xac minh dat lai mat khau.");
                var env = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
                var errorMessage = "Không thể gửi mã xác nhận lúc này. Vui lòng thử lại sau.";
                if (env.IsDevelopment())
                {
                    // Fallback de test local khi SMTP bi chan/chua cau hinh duoc.
                    TempData["DevVerificationCode"] = verificationCode;
                    TempData["Warning"] = "Không gửi được email SMTP. Hệ thống hiển thị mã xác nhận để bạn kiểm thử cục bộ.";
                    return RedirectToAction(nameof(VerifyResetCode), new { email = normalizedEmail });
                }

                ModelState.AddModelError(string.Empty, errorMessage);
                return View(model);
            }
        }

        TempData["Info"] = "Mã xác nhận đã được gửi đến email của bạn.";
        return RedirectToAction(nameof(VerifyResetCode), new { email = normalizedEmail });
    }

    // POST: VerifyResetCode
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult VerifyResetCode(VerifyResetCodeViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var cacheKey = GetCodeKey(model.Email);
        if (!_memoryCache.TryGetValue(cacheKey, out PasswordResetCodePayload? payload) || payload == null)
        {
            ModelState.AddModelError(string.Empty, "Mã xác nhận đã hết hạn hoặc không tồn tại. Vui lòng yêu cầu lại.");
            return View(model);
        }

        if (payload.ExpiresAtUtc < DateTime.UtcNow)
        {
            _memoryCache.Remove(cacheKey);
            ModelState.AddModelError(string.Empty, "Mã xác nhận đã hết hạn. Vui lòng yêu cầu lại.");
            return View(model);
        }

        if (!string.Equals(payload.Code, model.VerificationCode, StringComparison.Ordinal))
        {
            ModelState.AddModelError(nameof(model.VerificationCode), "Mã xác nhận không chính xác.");
            return View(model);
        }

        _memoryCache.Remove(cacheKey);

        var ticket = Guid.NewGuid().ToString("N");
        _memoryCache.Set(GetVerifiedKey(model.Email), ticket, DateTimeOffset.UtcNow.AddMinutes(10));

        return RedirectToAction(nameof(ResetPassword), new { email = model.Email, ticket });
    }

    // POST: ResetPassword
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordWithCodeViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var verifiedKey = GetVerifiedKey(model.Email);
        if (!_memoryCache.TryGetValue(verifiedKey, out string? expectedTicket) ||
            !string.Equals(expectedTicket, model.VerificationTicket, StringComparison.Ordinal))
        {
            ModelState.AddModelError(string.Empty, "Phiên đặt lại mật khẩu đã hết hạn. Vui lòng xác minh lại mã.");
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);

        if (user == null)
        {
            return RedirectToAction(nameof(ForgotPassword));
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

        if (result.Succeeded)
        {
            _memoryCache.Remove(verifiedKey);
            return RedirectToAction(nameof(ResetPasswordConfirmation));
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, TranslateIdentityError(error.Description));
        }

        return View(model);
    }


    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Login");
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
            .Replace("Email", "Email", StringComparison.OrdinalIgnoreCase)
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

    private static string GetCodeKey(string email) => $"pwd-reset-code:{email.Trim().ToLowerInvariant()}";
    private static string GetVerifiedKey(string email) => $"pwd-reset-verified:{email.Trim().ToLowerInvariant()}";

    private sealed class PasswordResetCodePayload
    {
        public string Code { get; set; } = string.Empty;
        public DateTime ExpiresAtUtc { get; set; }
    }


}