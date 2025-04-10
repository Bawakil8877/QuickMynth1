using QuickMynth1.Models;
using Microsoft.Extensions.Configuration;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System.Threading.Tasks;
using System.Net.Mail;
using System.Net;
using Microsoft.AspNetCore.Mvc;

namespace QuickMynth1.Services
{


    public class EmailService
    {


        public bool SendEmailToEmployer(Controller controller, string employeeEmail, string employerEmail)
        {
            try
            {
                var smtpClient = new System.Net.Mail.SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential("yourgmail@gmail.com", "yourAppPassword"), // Replace this
                    EnableSsl = true,
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress("yourgmail@gmail.com"),
                    Subject = "Advance Request",
                    Body = $"An advance request was made by employee: {employeeEmail}. Please approve it.",
                    IsBodyHtml = false,
                };

                mailMessage.To.Add(employerEmail);
                smtpClient.Send(mailMessage);

                return true;
            }
            catch (Exception ex)
            {
                controller.TempData["EmailResult"] = "Failed to send the email: " + ex.Message;
                return false;
            }
        }


    }


}

