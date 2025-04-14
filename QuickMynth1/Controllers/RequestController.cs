
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuickMynth1.Services;
using Microsoft.AspNetCore.Identity;
using QuickMynth1.Models;
using System.Security.Claims;
using QuickMynth1.Models.ViewModels;

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

        [HttpPost]
        public IActionResult StartRequest()
        {
            var authorizationUrl = _googleOAuthService.GenerateAuthorizationUrl();
            return Redirect(authorizationUrl);
        }

        public async Task<IActionResult> OAuthCallback(string code)
        {
            if (string.IsNullOrEmpty(code))
                return RedirectToAction("Error");

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var token = await _googleOAuthService.ExchangeCodeForTokenAsync(code, userId);
            var user = await _userManager.FindByIdAsync(userId);

            // Instead of creating Approve/Reject URLs, we now generate the QuickBooks authorize URL.
            string quickBooksAuthorizeUrl = Url.Action("Authorize", "QuickBooks", null, Request.Scheme);

            string subject = "Pay Advance Request - Connect to QuickBooks";
            string body = $@"
        <h2>Pay Advance Request</h2>
        <p>Employee {user.Email} is requesting a pay advance.</p>
        <p>Please click the link below to connect your QuickBooks account and authorize data sharing:</p>
        <p><a href='{quickBooksAuthorizeUrl}'>Connect to QuickBooks</a></p>
        <p>By connecting, you agree that KuickMynth may access your QuickBooks Online data as described in our Terms of Service and Privacy Policy.</p>";

            await _googleOAuthService.SendEmailUsingUserToken(user.Email, user.ManagerEmail, subject, body, userId);

            TempData["EmailResult"] = "Request sent successfully!";
            return RedirectToAction("RequestStatus");
        }


        public IActionResult RequestStatus()
        {
            var model = new RequestStatusViewModel
            {
                Status = "Success",
                Message = TempData["EmailResult"] as string ?? "No message available."
            };

            return View(model);
        }

        [AllowAnonymous]
        public IActionResult ApproveRequest(string userId, string token)
        {
            TempData["EmailResult"] = "✅ You have approved the pay advance request.";
            return View("ApprovalResult");
        }

        [AllowAnonymous]
        public IActionResult RejectRequest(string userId, string token)
        {
            TempData["EmailResult"] = "❌ You have rejected the pay advance request.";
            return View("ApprovalResult");
        }

        public IActionResult Error()
        {
            return Content("Authorization failed.");
        }
    }
}
