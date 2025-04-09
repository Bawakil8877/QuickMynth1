using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using QuickMynth1.Models;
using QuickMynth1.Models.ViewModels;
using QuickMynth1.Services;

namespace QuickMynth1.Controllers
{
    public class RequestController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly EmailService _emailService;

        public RequestController(UserManager<ApplicationUser> userManager, EmailService emailService)
        {
            _userManager = userManager;
            _emailService = emailService;
        }

        [HttpGet]
        public async Task<IActionResult> StartRequestForm()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var model = new RegisterViewModel
            {
                Email = user.Email,
                ManagerEmail = user.ManagerEmail
            };

            return View(model);
        }


        // Handle the POST request
        [HttpPost]
        public async Task<IActionResult> StartRequest(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                string subject = "Get up to $500 for each advance request — $5 flat fee. No interest. No credit checks.";

                string message = @"
                <h3>To get started, we’ll need one-time approval from your employer.</h3>
                <p>This helps us confirm what you’ve already earned each time you want to access pay advance.</p>
                <p><strong>Why employer approval?</strong> <a href='https://yourwebsite.com/learn-more'>Learn more</a></p>
                <p>Request sent by: <strong>" + model.Email + @"</strong></p>";

                await _emailService.SendEmailAsync(model.ManagerEmail, subject, message);

                TempData["Success"] = "Request sent successfully!";
                return RedirectToAction("Confirmation"); // Create a confirmation view if you want
            }

            return View("StartRequestForm", model);
        }

        // Optional confirmation view
        public IActionResult Confirmation()
        {
            return View();
        }
    }

}
