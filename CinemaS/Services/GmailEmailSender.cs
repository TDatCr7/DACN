using CinemaS.Models.Email;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Utils;

namespace CinemaS.Services
{
    public interface IEmailSenderWithAttachment : IEmailSender
    {
        Task SendEmailWithAttachmentAsync(string email, string subject, string htmlMessage, byte[] attachmentData, string attachmentName, string attachmentMimeType);
    }

    public class GmailEmailSender : IEmailSenderWithAttachment
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

        public async Task SendEmailWithAttachmentAsync(string email, string subject, string htmlMessage, byte[] attachmentData, string attachmentName, string attachmentMimeType)
        {
            var password = (_settings.SenderPassword ?? string.Empty)
                .Replace(" ", string.Empty)
                .Trim();

            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(_settings.SenderEmail));
            message.To.Add(MailboxAddress.Parse(email));
            message.Subject = subject;

            var builder = new BodyBuilder
            {
                HtmlBody = htmlMessage
            };

            // Add attachment as linked resource for images so it can be embedded via cid
            if (attachmentData != null && attachmentData.Length > 0 && !string.IsNullOrWhiteSpace(attachmentName))
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(attachmentMimeType) && attachmentMimeType.StartsWith("image/"))
                    {
                        // Add as linked resource (inline) with Content-Id for cid reference
                        var contentId = attachmentName.Replace(" ", "_");
                        var linked = builder.LinkedResources.Add(contentId, attachmentData, ContentType.Parse(attachmentMimeType ?? "image/png"));
                    }
                    else
                    {
                        // Non-image attachments: regular attachment
                        builder.Attachments.Add(attachmentName, attachmentData, ContentType.Parse(attachmentMimeType ?? "application/octet-stream"));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to add attachment as linked resource, falling back to regular attachment.");
                    try
                    {
                        builder.Attachments.Add(attachmentName, attachmentData);
                    }
                    catch { }
                }
            }

            message.Body = builder.ToMessageBody();

            try
            {
                using var client = new SmtpClient();
                await client.ConnectAsync(_settings.SmtpServer, _settings.Port, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(_settings.SenderEmail, password);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation("Email with attachment sent successfully. To={To}, Subject={Subject}, Attachment={Attachment}", email, subject, attachmentName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendEmailWithAttachmentAsync failed. To={To}, Subject={Subject}", email, subject);
                throw;
            }
        }
    }
}
