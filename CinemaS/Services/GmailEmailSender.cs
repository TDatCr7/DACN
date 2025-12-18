using CinemaS.Models.Email;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace CinemaS.Services
{
    public class GmailEmailSender : IEmailSender
    {
        private readonly EmailSettings _settings;
        private readonly ILogger<GmailEmailSender> _logger;

        public GmailEmailSender(IOptions<EmailSettings> options, ILogger<GmailEmailSender> logger)
        {
            _settings = options.Value;
            _logger = logger;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            // FIX: app password trong appsettings có thể bị copy kèm khoảng trắng
            var password = (_settings.SenderPassword ?? string.Empty)
                .Replace(" ", string.Empty)
                .Trim();

            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(_settings.SenderEmail));
            message.To.Add(MailboxAddress.Parse(email));
            message.Subject = subject;

            message.Body = new BodyBuilder
            {
                HtmlBody = htmlMessage
            }.ToMessageBody();

            try
            {
                using var client = new SmtpClient();

                // STARTTLS cho smtp.gmail.com:587
                await client.ConnectAsync(_settings.SmtpServer, _settings.Port, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(_settings.SenderEmail, password);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendEmailAsync failed. To={To}, Subject={Subject}", email, subject);
                throw; // để phía caller (RegisterModel) bắt và hiện lỗi hợp lệ
            }
        }
    }
}
