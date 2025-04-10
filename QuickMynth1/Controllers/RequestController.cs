
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuickMynth1.Services;
using Microsoft.AspNetCore.Identity;
using QuickMynth1.Models;
using System.Security.Claims;
using Google.Apis.Auth.OAuth2.Responses;
using QuickMynth1.Models.ViewModels;  // Add the namespace for the ViewModel

namespace QuickMynth1.Controllers
{
    [Authorize]
    public class RequestController : Controller
    {
        private readonly GoogleOAuthService _googleOAuthService;
        private readonly UserManager<ApplicationUser> _userManager;

        public RequestController(GoogleOAuthService googleOAuthService, UserManager<ApplicationUser> userManager)
        {
            _googleOAuthService = googleOAuthService;
            _userManager = userManager;
        }

        // Step 1: After login, user clicks button to start request
        public IActionResult StartRequest()
        {
            var authorizationUrl = _googleOAuthService.GenerateAuthorizationUrl();
            return Redirect(authorizationUrl);
        }

        // Step 2: Google redirects back here with the auth code
        public async Task<IActionResult> OAuthCallback(string code)
        {
            if (string.IsNullOrEmpty(code))
                return RedirectToAction("Error");

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var token = await _googleOAuthService.ExchangeCodeForTokenAsync(code, userId);

            // Now send the email using this user's token
            var user = await _userManager.FindByIdAsync(userId);

            string subject = "Pay Advance Request";
            string body = $@"
        <h2>Pay Advance Request</h2>
        <p>Employee {user.Email} is requesting a pay advance.</p>
        <p>Please approve or reject the request.</p>
        <p><a href='#'>Approve</a> | <a href='#'>Reject</a></p>";

            await _googleOAuthService.SendEmailUsingUserToken(user.Email, user.ManagerEmail, subject, body, userId);

            TempData["EmailResult"] = "Request sent successfully!";
            return RedirectToAction("RequestStatus");
        }


        public IActionResult RequestStatus()
        {
            // Create a ViewModel with some example status and message
            var model = new RequestStatusViewModel
            {
                Status = "Success",  // Set status dynamically if needed
                Message = TempData["EmailResult"] as string ?? "No message available."
            };

            return View(model);
        }


        public IActionResult Error()
        {
            return Content("Authorization failed.");
        }
    }
}
