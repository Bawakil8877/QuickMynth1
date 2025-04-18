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

            // exchange code for token
            var tokenJson = await _oAuthService.ExchangeQuickBooksCodeForTokenAsync(code);
            dynamic tokenObj = JsonConvert.DeserializeObject(tokenJson);
            string accessToken = tokenObj.access_token;

            // ←–– save into Session
            HttpContext.Session.SetString("QbAccessToken", accessToken);
            HttpContext.Session.SetString("QbRealmId", realmId);

            return RedirectToAction("EmployeeList");
        }


        public async Task<IActionResult> EmployeeList()
        {
            var accessToken = HttpContext.Session.GetString("QbAccessToken");
            var realmId = HttpContext.Session.GetString("QbRealmId");
            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(realmId))
                return RedirectToAction("Error");

            var employeesJson = await _oAuthService.GetEmployeesAsync(accessToken, realmId);
            dynamic data = JsonConvert.DeserializeObject(employeesJson);
            var employees = data.QueryResponse.Employee;

            return View(employees);
        }

        public async Task<IActionResult> ContractorList()
        {
            var accessToken = HttpContext.Session.GetString("QbAccessToken");
            var realmId = HttpContext.Session.GetString("QbRealmId");
            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(realmId))
                return RedirectToAction("Error");

            var contractorsJson = await _oAuthService.GetContractorsAsync(accessToken, realmId);
            dynamic data = JsonConvert.DeserializeObject(contractorsJson);
            var contractors = data.QueryResponse.Vendor;

            return View("ContractorList", contractors);
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
