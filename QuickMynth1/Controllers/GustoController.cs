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

        public GustoController(GustoService svc, ApplicationDbContext db, ILogger<GustoController> logger, HttpClient client)
        {
            _svc = svc;
            _db = db;
            _logger = logger;
            _client = client;
        }

        [HttpGet]
        public IActionResult Connect() => Redirect(_svc.GetAuthorizationUrl());

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

            var tokEntity = await _db.GustoTokens
                                     .FirstOrDefaultAsync(t => t.UserId == uid);
            if (tokEntity == null)
                throw new Exception("Saved Gusto token not found.");

            // Register webhook once
            var callbackUrl = "https://your-app.com/api/gusto/webhook";
            await _svc.RegisterWebhookAsync(tokEntity.AccessToken, tokEntity.CompanyId, callbackUrl);

            TempData["Success"] = "Connected to Gusto and webhook registered.";
            return RedirectToAction("Index", "Home");
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

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var tok = await _db.GustoTokens
                                  .FirstOrDefaultAsync(t => t.UserId == userId)
                       ?? throw new Exception("Not connected to Gusto.");

            var accessToken = tok.AccessToken;
            var companyId = tok.CompanyId;

            // 1) Resolve the Gusto employee ID
            var employeeId = await _svc.FindEmployeeIdByEmailAsync(
                                 accessToken,
                                 companyId,
                                 m.EmployeeEmail);

            // 2) Fetch ALL company benefits
            var benefits = await _svc.GetCompanyBenefitsAsync(accessToken, companyId);

            // Debug: log the set of UUIDs we got back
            _logger.LogDebug("Company benefits returned: {UUIDs}",
                benefits.Select(b => b.Uuid).ToArray());

            // 3) Find the one the form posted
            var benefit = benefits
                            .FirstOrDefault(b => b.Uuid == m.SelectedBenefitUuid);

            if (benefit is null)
                throw new Exception(
                    $"Selected benefit '{m.SelectedBenefitUuid}' not found. " +
                    $"Returned UUIDs: {string.Join(", ", benefits.Select(b => b.Uuid))}");

            // 4) Call the service with the exact payload shape the demo API accepts
            await _svc.CreateEmployeeDeductionAsync(
                accessToken,
                employeeId,
                benefit.Uuid,           // company_benefit_uuid
                m.DeductionAmount!.Value);

            TempData["Success"] = "Deduction created successfully!";
            return RedirectToAction(nameof(Deduction));
        }


    }
}
