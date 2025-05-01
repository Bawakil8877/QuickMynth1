using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using QuickMynth1.Services;
using QuickMynth1.Models.ViewModels;
using QuickMynth1.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace QuickMynth1.Controllers
{
    [Authorize]
    public class GustoController : Controller
    {
        private readonly GustoService _svc;
        private readonly ApplicationDbContext _db;
        private readonly ILogger<GustoController> _logger;
        private readonly HttpClient _client;

        public GustoController(GustoService svc, ApplicationDbContext db, ILogger<GustoController> logger, HttpClient client)
        {
            _svc = svc;
            _db = db;
            _logger = logger;
            _client = client;
        }

        [HttpGet]
        public IActionResult Connect()
            => Redirect(_svc.GetAuthorizationUrl());

        [HttpGet]
        [HttpGet]
        public async Task<IActionResult> Callback(string code, string error)
        {
            if (!string.IsNullOrEmpty(error))
                return Content($"OAuth Error: {error}");
            if (string.IsNullOrEmpty(code))
                return Content("Missing code.");

            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var tokenJson = await _svc.ExchangeCodeForTokenAsync(code);
            await _svc.SaveTokenAsync(uid, tokenJson);

            // — Fetch the saved token entity from the database
            var tokEntity = await _db.GustoTokens
                                     .FirstOrDefaultAsync(t => t.UserId == uid);
            if (tokEntity == null)
                throw new Exception("Saved Gusto token not found in database.");

            // — Use its AccessToken to log supported benefits
            await _svc.LogSupportedBenefitsAsync(tokEntity.AccessToken);

            TempData["Success"] = "Connected to Gusto!  Check your console/output for the supported-benefits JSON.";
            return RedirectToAction(nameof(Deduction));
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
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deduction(DeductionViewModel m)
        {
            if (!ModelState.IsValid)
                return View(m);

            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var token = await _db.GustoTokens
                                 .FirstOrDefaultAsync(t => t.UserId == uid)
                        ?? throw new Exception("Not connected to Gusto.");

            // find the employee record
            var empId = await _svc.FindEmployeeIdByEmailAsync(
                            token.AccessToken,
                            token.CompanyId,
                            m.EmployeeEmail);

            // create the deduction
            await _svc.CreateEmployeeBenefitAsync(
                            token.AccessToken,
                            empId,
                            m.SelectedBenefitUuid,
                            m.DeductionAmount!.Value);

            TempData["Success"] = "Deduction created successfully!";
            return RedirectToAction(nameof(Deduction));
        }

    }
}
