using Microsoft.AspNetCore.Identity.UI.Services;
using System.Net.Mail;
using System.Net;
using System.Threading.Tasks;

namespace Backend.Services
{
    public class EmailSender : IEmailSender
    {
        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            // Here you can integrate your email sending logic
            // For example, using SMTP, SendGrid, etc.
            // Below is a simple example using System.Net.Mail

            var client = new SmtpClient("your-smtp-server")
            {
                Port = 587,
                Credentials = new NetworkCredential("your-email@example.com", "your-email-password"),
                EnableSsl = true,
            };

            return client.SendMailAsync(
                new MailMessage("your-email@example.com", email, subject, htmlMessage) { IsBodyHtml = true }
            );
        }
    }
}
