using CijeneScraper.Models;
using Microsoft.Extensions.Options;
using System.Net.Mail;

namespace CijeneScraper.Services.Notification
{
    public interface IEmailNotificationService
    {
        Task SendAsync(string subject, string body);
        Task SendAsync(string to, string subject, string body);
    }

    public class EmailNotificationService : IEmailNotificationService
    {
        private readonly MailSettings _settings;

        public EmailNotificationService(IOptions<MailSettings> settings)
        {
            _settings = settings.Value;
        }

        public async Task SendAsync(string subject, string body)
        {
            await SendAsync(_settings.ToAddress, subject, body);
        }

        public async Task SendAsync(string to, string subject, string body)
        {
            if (!_settings.Enabled)
                return;

            var mail = new MailMessage(_settings.From, to)
            {
                Subject = subject,
                Body = body
            };

            using var smtp = new SmtpClient(_settings.SmtpServer)
            {
                Port = _settings.Port,
                EnableSsl = _settings.EnableSsl,
                Credentials = new System.Net.NetworkCredential(_settings.Username, _settings.Password)
            };

            await smtp.SendMailAsync(mail);
        }
    }
}
