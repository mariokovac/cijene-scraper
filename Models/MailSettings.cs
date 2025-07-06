using System.Net.Mail;

namespace CijeneScraper.Models
{
    public class MailSettings
    {
        public bool Enabled { get; set; }
        public string SmtpServer { get; set; }
        public int Port { get; set; }
        public bool EnableSsl { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string From { get; set; }
        public string ToAddress { get; set; }
    }
}
