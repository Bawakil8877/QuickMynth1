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

        // helper to see if *any* token exists for this user’s company
        private async Task<bool> CompanyIsConnectedAsync(string companyId) =>
            await _db.GustoTokens.AnyAsync(t => t.CompanyId == companyId);

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
            if (tok == null || !await CompanyIsConnectedAsync(tok.CompanyId))
            {
                return View("CompanyNotConnected");
            }

            // refresh if needed…
            if (DateTime.UtcNow >= tok.CreatedAt.AddSeconds(tok.ExpiresIn))
            {
                var refreshJson = await _svc.ExchangeRefreshTokenAsync(tok.RefreshToken);
                await _svc.SaveTokenAsync(uid, refreshJson);
                tok = await _db.GustoTokens.FirstAsync(t => t.UserId == uid);
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
            // 1) Get current user
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                // Not logged in?
                return Challenge();
            }

            // 2) Lookup existing token row
            var tok = await _db.GustoTokens
                .FirstOrDefaultAsync(t => t.UserId == userId);

            // 3) If no token => company not connected yet
            if (tok == null)
            {
                // You could also ModelState.AddModelError + return View(m)
                return View("CompanyNotConnected");
            }

            // 4) Re-check “company connected” by ensuring at least one token exists
            var companyConnected = await _db.GustoTokens
                .AnyAsync(t => t.CompanyId == tok.CompanyId);
            if (!companyConnected)
            {
                return View("CompanyNotConnected");
            }

            // 5) Model validation
            if (!ModelState.IsValid)
                return View(m);

            // 6) Refresh token if expired
            var expiresAt = tok.CreatedAt.AddSeconds(tok.ExpiresIn);
            if (DateTime.UtcNow >= expiresAt)
            {
                try
                {
                    var refreshJson = await _svc.ExchangeRefreshTokenAsync(tok.RefreshToken);
                    await _svc.SaveTokenAsync(userId, refreshJson);
                    tok = await _db.GustoTokens.FirstAsync(t => t.UserId == userId);
                }
                catch
                {
                    ModelState.AddModelError(string.Empty,
                        "Could not refresh your Gusto session. Please ask your admin to reconnect.");
                    return View(m);
                }
            }

            // 7) Find (or create) the Post-Tax benefit for the company
            var benefitUuid = await _svc.EnsurePostTaxBenefitAsync(
                tok.AccessToken,
                tok.CompanyId);

            // 8) Find this employee’s Gusto employee-ID by their email
            string employeeId;
            try
            {
                employeeId = await _svc.FindEmployeeIdByEmailAsync(
                    tok.AccessToken,
                    tok.CompanyId,
                    m.EmployeeEmail!);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty,
                    $"Could not find your employee record in Gusto: {ex.Message}");
                return View(m);
            }

            // 9) Calculate how much net pay is available over past year
            decimal available;
            try
            {
                available = await _svc.GetEmployeeTotalNetPayAsync(
                    tok.AccessToken,
                    tok.CompanyId,
                    employeeId);
            }
            catch (UnauthorizedAccessException)
            {
                ModelState.AddModelError(string.Empty,
                    "Your Gusto session has expired and cannot be refreshed automatically. Please ask your admin to reconnect.");
                return View(m);
            }

            // 10) Compute total to deduct (requested + $5 fee)
            const decimal Fee = 5m;
            var requested = m.DeductionAmount!.Value;
            var totalToDeduct = requested + Fee;

            if (totalToDeduct > available)
            {
                ModelState.AddModelError(nameof(m.DeductionAmount),
                    $"Insufficient funds: you have {available:C} available, " +
                    $"but tried to deduct {totalToDeduct:C} (including {Fee:C} fee).");
                return View(m);
            }

            // 11) Persist the advance locally
            _db.EmployeeAdvances.Add(new EmployeeAdvance
            {
                EmployeeId = employeeId,
                Amount = totalToDeduct,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            // 12) Call Gusto to create the employee benefit deduction
            try
            {
                await _svc.CreateEmployeeBenefitAsync(
                    tok.AccessToken,
                    employeeId,
                    benefitUuid,
                    totalToDeduct);
            }
            catch (Exception ex)
            {
                // Optionally roll back the local advance or log
                ModelState.AddModelError(string.Empty,
                    $"Failed to create deduction in Gusto: {ex.Message}");
                return View(m);
            }

            // 13) All done!
            TempData["Success"] =
                $"Deduction of {totalToDeduct:C} (including {Fee:C} fee) created successfully!";
            return RedirectToAction(nameof(Deduction));
        }

    }
}
