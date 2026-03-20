namespace QL_HieuSuat.Services;

public interface IEmailService
{
    Task SendEmailAsync(string toEmail, string subject, string body);
}
