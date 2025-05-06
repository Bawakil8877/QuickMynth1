using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using QuickMynth1.Services;
using QuickMynth1.Models.ViewModels;
using QuickMynth1.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using NuGet.Common;

namespace QuickMynth1.Controllers
{
    [Authorize]
    public class GustoController : Controller
    {
        private readonly GustoService _svc;
        private readonly ApplicationDbContext _db;
        private readonly ILogger<GustoController> _logger;
        private readonly HttpClient _client;
        private readonly GustoService _gustoService;

        public GustoController(GustoService svc, ApplicationDbContext db, ILogger<GustoController> logger, HttpClient client, GustoService gustoService)
        {
            _svc = svc;
            _db = db;
            _logger = logger;
            _client = client;
            _gustoService = gustoService;
        }

        [HttpGet]
        public IActionResult Connect()
        {
            var authorizationUrl = _gustoService.GetAuthorizationUrl();
            return Redirect(authorizationUrl);
        }

        [HttpGet]
        public async Task<IActionResult> Callback(string code, string error)
        {
            if (!string.IsNullOrEmpty(error))
                return Content($"OAuth Error: {error}");

            if (string.IsNullOrEmpty(code))
                return Content("Missing Authorization Code.");

            try
            {
                var tokenJson = await _gustoService.ExchangeCodeForTokenAsync(code);
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                await _gustoService.SaveTokenAsync(userId, tokenJson);

                return Content("Gusto authorization successful! Token saved.");
            }
            catch (Exception ex)
            {
                return Content($"Error during Gusto OAuth: {ex.Message}");
            }
        }



        [HttpGet]
        public async Task<IActionResult> Deduction()
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var token = await _db.GustoTokens
                                 .FirstOrDefaultAsync(t => t.UserId == uid)
                        ?? throw new Exception("Not connected to Gusto.");

            // ensure (or create) the single post-tax benefit and get its UUID
            var benefitUuid = await _svc.EnsurePostTaxBenefitAsync(token.AccessToken, token.CompanyId);

            // fetch its metadata so we can show its name
            var benefits = await _svc.GetCompanyBenefitsAsync(token.AccessToken, token.CompanyId);
            var bene = benefits.First(b => b.Uuid == benefitUuid);

            var vm = new DeductionViewModel
            {
                EmployeeEmail = User.FindFirstValue(ClaimTypes.Email)!,
                SelectedBenefitUuid = benefitUuid,
                BenefitName = bene.Name
            };
            return View(vm);
        }

        // POST: /Gusto/Deduction
        // Controllers/GustoController.cs (only the POST action shown)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deduction(DeductionViewModel m)
        {
            if (!ModelState.IsValid)
                return View(m);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var tok = await _db.GustoTokens.FirstOrDefaultAsync(t => t.UserId == userId)
                         ?? throw new Exception("Not connected to Gusto.");

            var accessToken = tok.AccessToken;
            var companyId = tok.CompanyId;

            // 1) make sure the company has our post-tax benefit
            var benefitUuid = await _svc.EnsurePostTaxBenefitAsync(accessToken, companyId);

            // 2) find the employee
            var employeeId = await _svc.FindEmployeeIdByEmailAsync(
                                 accessToken, companyId, m.EmployeeEmail);

            // 3) and *use* our single, known-good helper
            await _svc.CreateEmployeeBenefitAsync(
                accessToken,
                employeeId,
                benefitUuid,
                m.DeductionAmount!.Value);

            TempData["Success"] = "Deduction created successfully!";
            return RedirectToAction(nameof(Deduction));
        }


    }
}
