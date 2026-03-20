using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace QL_HieuSuat.Services;

public class SmtpEmailService : IEmailService
{
    private readonly SmtpSettings _settings;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IOptions<SmtpSettings> settings, ILogger<SmtpEmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string body)
    {
        var normalizedUserName = (_settings.UserName ?? string.Empty).Trim();
        var normalizedFromEmail = (_settings.FromEmail ?? string.Empty).Trim();
        var normalizedPassword = (_settings.Password ?? string.Empty).Replace(" ", string.Empty).Trim();

        var isPlaceholderConfig =
            normalizedUserName.Contains("your-email", StringComparison.OrdinalIgnoreCase) ||
            normalizedPassword.Contains("your-app-password", StringComparison.OrdinalIgnoreCase) ||
            normalizedFromEmail.Contains("your-email", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(_settings.Host) ||
            string.IsNullOrWhiteSpace(normalizedUserName) ||
            string.IsNullOrWhiteSpace(normalizedPassword) ||
            string.IsNullOrWhiteSpace(normalizedFromEmail) ||
            isPlaceholderConfig)
        {
            throw new InvalidOperationException("Cấu hình SMTP chưa hợp lệ. Hãy thay thông tin mẫu trong EmailSettings bằng tài khoản SMTP thật.");
        }

        using var message = new MailMessage
        {
            From = new MailAddress(normalizedFromEmail, _settings.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };

        message.To.Add(new MailAddress(toEmail));

        using var smtp = new SmtpClient(_settings.Host, _settings.Port)
        {
            EnableSsl = _settings.EnableSsl,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(normalizedUserName, normalizedPassword)
        };

        try
        {
            await smtp.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể gửi email tới {ToEmail}", toEmail);
            throw;
        }
    }
}
