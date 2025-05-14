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
                return View("CompanyNotConnected");

            // refresh if needed
            if (DateTime.UtcNow >= tok.CreatedAt.AddSeconds(tok.ExpiresIn))
            {
                var refreshJson = await _svc.ExchangeRefreshTokenAsync(tok.RefreshToken);
                await _svc.SaveTokenAsync(uid, refreshJson);
                tok = await _db.GustoTokens.FirstAsync(t => t.UserId == uid);
            }

            // ensure benefit exists
            var benefitUuid = await _svc.EnsurePostTaxBenefitAsync(tok.AccessToken, tok.CompanyId);
            var benefits = await _svc.GetCompanyBenefitsAsync(tok.AccessToken, tok.CompanyId);
            var bene = benefits.First(b => b.Uuid == benefitUuid);

            // *** new: lookup the employee and their available funds ***
            var employeeId = await _svc.FindEmployeeIdByEmailAsync(tok.AccessToken, tok.CompanyId, User.FindFirstValue(ClaimTypes.Email)!);
            var available = await _svc.GetEmployeeCurrentNetPayAsync(tok.AccessToken, tok.CompanyId, employeeId);

            var vm = new DeductionViewModel
            {
                EmployeeEmail = User.FindFirstValue(ClaimTypes.Email)!,
                SelectedBenefitUuid = benefitUuid,
                BenefitName = bene.Name,
                AvailableFunds = available    // ← populated here
            };

            return View(vm);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deduction(DeductionViewModel m)
        {
            // 1) Auth & Model validation
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Challenge();
            if (!ModelState.IsValid) return View(m);

            // 2) Load & refresh Gusto token
            var tok = await _db.GustoTokens.FirstOrDefaultAsync(t => t.UserId == userId);
            if (tok == null) return View("CompanyNotConnected");

            if (DateTime.UtcNow >= tok.CreatedAt.AddSeconds(tok.ExpiresIn))
            {
                try
                {
                    var json = await _svc.ExchangeRefreshTokenAsync(tok.RefreshToken);
                    await _svc.SaveTokenAsync(userId, json);
                    tok = await _db.GustoTokens.FirstAsync(t => t.UserId == userId);
                }
                catch
                {
                    ModelState.AddModelError("", "Could not refresh Gusto session. Please reconnect.");
                    return View(m);
                }
            }

            // 3) Lookup employee’s Gusto ID
            string employeeId;
            try
            {
                employeeId = await _svc.FindEmployeeIdByEmailAsync(
                    tok.AccessToken, tok.CompanyId, m.EmployeeEmail!);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Employee lookup failed: {ex.Message}");
                return View(m);
            }

            // 4) Compute *current* net pay
            decimal available;
            try
            {
                available = await _svc.GetEmployeeCurrentNetPayAsync(
                    tok.AccessToken, tok.CompanyId, employeeId);
            }
            catch (UnauthorizedAccessException)
            {
                ModelState.AddModelError("",
                    "Gusto session expired and cannot be refreshed automatically. Please reconnect.");
                return View(m);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Could not determine net pay: {ex.Message}");
                return View(m);
            }

            // 5) Enforce the affordability check (including $5 fee)
            const decimal Fee = 5m;
            var requested = m.DeductionAmount!.Value;
            var totalToDeduct = requested + Fee;

            if (totalToDeduct > available)
            {
                ModelState.AddModelError(nameof(m.DeductionAmount),
                    $"Insufficient funds: you have {available:C} available this pay period, " +
                    $"but tried to deduct {totalToDeduct:C} (including {Fee:C} fee).");
                return View(m);
            }

            // 6) Ensure post-tax benefit exists
            string benefitUuid;
            try
            {
                benefitUuid = await _svc.EnsurePostTaxBenefitAsync(
                    tok.AccessToken, tok.CompanyId);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Benefit setup failed: {ex.Message}");
                return View(m);
            }

            // 7) Persist local advance
            _db.EmployeeAdvances.Add(new EmployeeAdvance
            {
                EmployeeId = employeeId,
                Amount = totalToDeduct,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            // 8) Create the deduction in Gusto
            try
            {
                await _svc.CreateEmployeeBenefitAsync(
                    tok.AccessToken, employeeId, benefitUuid, totalToDeduct);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Gusto deduction failed: {ex.Message}");
                return View(m);
            }

            // 9) Success
            TempData["Success"] =
                $"Deduction of {totalToDeduct:C} (including {Fee:C} fee) created successfully!";
            return RedirectToAction(nameof(Deduction));
        }


    }
}
