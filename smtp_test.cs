using System;
using System.Net;
using System.Net.Mail;

var host = Environment.GetEnvironmentVariable("SMTP_HOST");
var port = int.Parse(Environment.GetEnvironmentVariable("SMTP_PORT") ?? "587");
var user = Environment.GetEnvironmentVariable("SMTP_USER");
var pass = (Environment.GetEnvironmentVariable("SMTP_PASS") ?? "").Replace(" ", "");
var from = Environment.GetEnvironmentVariable("SMTP_FROM");

try
{
    using var smtp = new SmtpClient(host, port)
    {
        EnableSsl = true,
        UseDefaultCredentials = false,
        Credentials = new NetworkCredential(user, pass),
        DeliveryMethod = SmtpDeliveryMethod.Network,
        Timeout = 15000
    };

    using var msg = new MailMessage(from, user, "SMTP TEST", "SMTP test from QL_HieuSuat");
    smtp.Send(msg);
    Console.WriteLine("SMTP_TEST_OK");
}
catch (Exception ex)
{
    Console.WriteLine("SMTP_TEST_FAIL");
    Console.WriteLine(ex.GetType().FullName);
    Console.WriteLine(ex.Message);
    if (ex.InnerException != null)
    {
        Console.WriteLine("INNER:" + ex.InnerException.GetType().FullName);
        Console.WriteLine("INNER_MSG:" + ex.InnerException.Message);
    }
}
