using Microsoft.AspNetCore.Mvc;
using QuickMynth1.Services;

namespace QuickMynth1.Controllers
{
    public class FinchController : Controller
    {
        private readonly FinchService _finchService;

        public FinchController(FinchService finchService)
        {
            _finchService = finchService;
        }

        public IActionResult Connect()
        {
            var url = _finchService.GetConnectUrl();
            return Redirect(url);
        }

        public async Task<IActionResult> Callback(string code)
        {
            var tokenJson = await _finchService.ExchangeCodeForToken(code);
            // You can parse and save access_token here for API calls later
            return Content("Token received: " + tokenJson);
        }
    }
}
