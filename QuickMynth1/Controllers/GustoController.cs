using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using QuickMynth1.Services;
using QuickMynth1.Models.ViewModels;
using QuickMynth1.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;
using NuGet.Common;
using QuickMynth1.Models;

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
            var tok = await _db.GustoTokens.FirstOrDefaultAsync(t => t.UserId == uid);
            if (tok == null)
            {
                TempData["Error"] = "Your Gusto account is not connected. Please ask admin to connect once.";
                return View(new DeductionViewModel());
            }

            try
            {
                if (DateTime.UtcNow >= tok.CreatedAt.AddSeconds(tok.ExpiresIn))
                {
                    var refreshJson = await _svc.ExchangeRefreshTokenAsync(tok.RefreshToken);
                    await _svc.SaveTokenAsync(uid, refreshJson);
                    tok = await _db.GustoTokens.FirstAsync(t => t.UserId == uid);
                }
            }
            catch
            {
                TempData["Error"] = "Could not refresh Gusto session. Please contact support.";
                return View(new DeductionViewModel());
            }

            var benefitUuid = await _svc.EnsurePostTaxBenefitAsync(tok.AccessToken, tok.CompanyId);
            var benefits = await _svc.GetCompanyBenefitsAsync(tok.AccessToken, tok.CompanyId);
            var bene = benefits.First(b => b.Uuid == benefitUuid);

            var vm = new DeductionViewModel
            {
                EmployeeEmail = User.FindFirstValue(ClaimTypes.Email)!,
                SelectedBenefitUuid = benefitUuid,
                BenefitName = bene.Name
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deduction(DeductionViewModel m)
        {
            if (!ModelState.IsValid)
                return View(m);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var tok = await _db.GustoTokens.FirstOrDefaultAsync(t => t.UserId == userId);
            if (tok == null)
            {
                ModelState.AddModelError(string.Empty, "Your Gusto account is not connected. Please ask admin to connect once.");
                return View(m);
            }

            try
            {
                if (DateTime.UtcNow >= tok.CreatedAt.AddSeconds(tok.ExpiresIn))
                {
                    var refreshJson = await _svc.ExchangeRefreshTokenAsync(tok.RefreshToken);
                    await _svc.SaveTokenAsync(userId, refreshJson);
                    tok = await _db.GustoTokens.FirstAsync(t => t.UserId == userId);
                }
            }
            catch
            {
                ModelState.AddModelError(string.Empty, "Could not refresh Gusto session. Please contact support.");
                return View(m);
            }

            var access = tok.AccessToken;
            var companyId = tok.CompanyId;
            var benefitUuid = await _svc.EnsurePostTaxBenefitAsync(access, companyId);
            var employeeId = await _svc.FindEmployeeIdByEmailAsync(access, companyId, m.EmployeeEmail!);
            var requested = m.DeductionAmount!.Value;

            decimal available;
            try
            {
                available = await _svc.GetEmployeeTotalNetPayAsync(access, companyId, employeeId);
            }
            catch (UnauthorizedAccessException)
            {
                ModelState.AddModelError(string.Empty, "Your Gusto session has expired and cannot be refreshed automatically. Please contact support.");
                return View(m);
            }

            var totalToDeduct = requested + 5m;
            if (totalToDeduct > available)
            {
                ModelState.AddModelError(nameof(m.DeductionAmount),
                    $"Insufficient funds: you have {available:C} available, but tried to deduct {totalToDeduct:C}.");
                return View(m);
            }

            _db.EmployeeAdvances.Add(new EmployeeAdvance
            {
                EmployeeId = employeeId,
                Amount = totalToDeduct,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
            await _svc.CreateEmployeeBenefitAsync(access, employeeId, benefitUuid, totalToDeduct);

            TempData["Success"] = $"Deduction of {totalToDeduct:C} (including $5 fee) created successfully!";
            return RedirectToAction(nameof(Deduction));
        }

    }
}
