using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuickMynth1.Services;
using System.Security.Claims;
using System.Text.Json;

namespace QuickMynth1.Controllers
{
    [Authorize] // ✅ Only logged-in users can authorize Gusto
    public class GustoController : Controller
    {
        private readonly GustoService _gustoService;

        public GustoController(GustoService gustoService)
        {
            _gustoService = gustoService;
        }

        [HttpGet]
        public IActionResult Connect()
        {
            var authorizationUrl = _gustoService.GetAuthorizationUrl();
            return Redirect(authorizationUrl); // ✅ Redirect user to Gusto Consent screen
        }

        [HttpGet]
        public async Task<IActionResult> Callback(string code, string error)
        {
            if (!string.IsNullOrEmpty(error))
            {
                // ✅ Gusto returned an error
                return Content($"OAuth Error: {error}");
            }

            if (string.IsNullOrEmpty(code))
            {
                return Content("Missing Authorization Code.");
            }

            try
            {
                // ✅ Exchange authorization code for access token
                var tokenJson = await _gustoService.ExchangeCodeForTokenAsync(code);

                // ✅ Get current User ID (logged-in user)
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                // ✅ Save token to database
                await _gustoService.SaveTokenAsync(userId, tokenJson);

                return Content("Gusto authorization successful! Token saved.");
            }
            catch (Exception ex)
            {
                return Content($"Error during Gusto OAuth: {ex.Message}");
            }
        }

        [HttpGet]
        public async Task<IActionResult> Employees()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var token = await _gustoService.GetTokenByUserIdAsync(userId);

            if (token == null)
            {
                return Content("No Gusto token found. Please connect Gusto first.");
            }

            try
            {
                var employeesJson = await _gustoService.GetEmployeesAsync(token.AccessToken);

                var employeesPretty = JsonSerializer.Serialize(
                    JsonSerializer.Deserialize<object>(employeesJson),
                    new JsonSerializerOptions { WriteIndented = true });

                return Content(employeesPretty, "application/json");
            }
            catch (Exception ex)
            {
                return Content($"Failed to fetch employees: {ex.Message}");
            }
        }
    }
}
