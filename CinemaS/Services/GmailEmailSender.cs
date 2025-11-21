using CinemaS.Models;
using CinemaS.Models.Email;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace CinemaS.Services
{
    public class GmailEmailSender : IEmailSender
    {
        private readonly EmailSettings _settings;

        public GmailEmailSender(IOptions<EmailSettings> settings)
        {
            _settings = settings.Value;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            using var client = new SmtpClient(_settings.SmtpServer, _settings.Port)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(_settings.SenderEmail, _settings.SenderPassword)
            };

            using var message = new MailMessage
            {
                From = new MailAddress(_settings.SenderEmail, "CinemaS"),
                Subject = subject,
                Body = htmlMessage,
                IsBodyHtml = true
            };
            message.To.Add(new MailAddress(email));

            await client.SendMailAsync(message);
        }
    }
}
