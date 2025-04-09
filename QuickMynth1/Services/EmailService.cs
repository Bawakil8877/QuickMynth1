using QuickMynth1.Models;
using Microsoft.Extensions.Configuration;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System.Threading.Tasks;
namespace QuickMynth1.Services
{
 

    public class EmailService
    {
        private readonly string _smtpServer = "smtp.gmail.com";  // Or any SMTP service
        private readonly int _smtpPort = 587;
        private readonly string _emailFrom = "07026981622mustapha@gmail.com";
        private readonly string _password = "fdar djcl paia bhtu";  // You should use App passwords or a service key for better security

        public async Task SendEmailAsync(string emailTo, string subject, string message)
        {
            var emailMessage = new MimeMessage();
            emailMessage.From.Add(new MailboxAddress("QuickMynth", _emailFrom));
            emailMessage.To.Add(new MailboxAddress("", emailTo));
            emailMessage.Subject = subject;

            var bodyBuilder = new BodyBuilder { HtmlBody = message };
            emailMessage.Body = bodyBuilder.ToMessageBody();

            using (var client = new SmtpClient())
            {
                await client.ConnectAsync(_smtpServer, _smtpPort, false);
                await client.AuthenticateAsync(_emailFrom, _password);
                await client.SendAsync(emailMessage);
                await client.DisconnectAsync(true);
            }
        }
    }

}

