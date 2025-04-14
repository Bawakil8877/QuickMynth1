using Google.Apis.Oauth2.v2;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using QuickMynth1.Services;

namespace QuickMynth1.Controllers
{
    public class QuickBooksController : Controller
    {
        private readonly OAuthService _oAuthService;
        private readonly OAuthService _oauthService;


        public QuickBooksController(OAuthService oAuthService)
        {
            _oAuthService = oAuthService;
        }

        // Step 1: Redirect user to QuickBooks authorization URL
        public IActionResult Authorize()
        {
            var authUrl = _oAuthService.GetQuickBooksAuthUrl();
            return Redirect(authUrl);
        }

        public async Task<IActionResult> Callback(string code, string state, string realmId)
        {
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(realmId))
                return BadRequest("Missing code or realmId.");

            var tokenJson = await _oAuthService.ExchangeQuickBooksCodeForTokenAsync(code);
            dynamic tokenData = JsonConvert.DeserializeObject(tokenJson);

            string accessToken = tokenData.access_token;
            TempData["AccessToken"] = accessToken;
            TempData["RealmId"] = realmId;

            return RedirectToAction("EmployeeList");
        }

        public async Task<IActionResult> EmployeeList()
        {
            var accessToken = TempData["AccessToken"] as string;
            var realmId = TempData["RealmId"] as string;

            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(realmId))
            {
                return RedirectToAction("Error");
            }

            var employeesJson = await _oAuthService.GetEmployeesAsync(accessToken, realmId);

            // Optional: Deserialize and pass to view
            dynamic data = JsonConvert.DeserializeObject(employeesJson);
            var employees = data.QueryResponse.Employee;

            return View(employees);
        }


        // Step 3: Fetch QuickBooks data with the access token
        public async Task<IActionResult> QuickBooksData()
        {
            var accessToken = TempData["AccessToken"] as string;
            if (string.IsNullOrEmpty(accessToken))
            {
                return RedirectToAction("Error");
            }

           // await _oAuthService.GetQuickBooksDataAsync(accessToken);
            return View();
        }


        public IActionResult Error()
        {
            return Content("Error occurred during QuickBooks OAuth flow.");
        }
    }

}
