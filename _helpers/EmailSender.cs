using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using MimeKit;
using MailKit.Net.Smtp;

namespace PowerGuardCoreApi._Helpers
{
    public interface IEmailSender
    {
        Task SendEmailAsync(string to, string subject, string html, string? from = null);
    }

    public class EmailSender : IEmailSender
    {
        private readonly IConfiguration _config;

        public EmailSender(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendEmailAsync(string to, string subject, string html, string? from = null)
        {
            var emailFrom = from ?? _config["EmailFrom"];
            var smtpSection = _config.GetSection("SmtpOptions");

            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(emailFrom));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = html };

            using var client = new SmtpClient();
            await client.ConnectAsync(
                smtpSection["Host"],
                int.Parse(smtpSection["Port"] ?? "587"),
                bool.Parse(smtpSection["UseSsl"] ?? "false")
            );
            if (!string.IsNullOrEmpty(smtpSection["User"]) && !string.IsNullOrEmpty(smtpSection["Pass"]))
            {
                await client.AuthenticateAsync(smtpSection["User"], smtpSection["Pass"]);
            }
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
    }
}