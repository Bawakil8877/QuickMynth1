// Controllers/GustoController.cs

using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuickMynth1.Data;
using QuickMynth1.Models;
using QuickMynth1.Models.ViewModels;
using QuickMynth1.Services;

namespace QuickMynth1.Controllers
{
    [Authorize]
    public class GustoController : Controller
    {
        private readonly GustoService _svc;
        private readonly ApplicationDbContext _db;
        private readonly ILogger<GustoController> _logger;

        public GustoController(
            GustoService svc,
            ApplicationDbContext db,
            ILogger<GustoController> logger)
        {
            _svc = svc;
            _db = db;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Connect()
            => Redirect(_svc.GetAuthorizationUrl());

        [HttpGet]
        public async Task<IActionResult> Callback(string code, string error)
        {
            if (!string.IsNullOrEmpty(error))
                return Content($"OAuth Error: {error}");
            if (string.IsNullOrEmpty(code))
                return Content("Missing authorization code.");

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var tokenJs = await _svc.ExchangeCodeForTokenAsync(code);
            await _svc.SaveTokenAsync(userId, tokenJs);
            TempData["Success"] = "Connected to Gusto!";
            return RedirectToAction(nameof(Deduction));
        }

        // helper to see if *any* token exists for this user’s company
        private async Task<bool> CompanyIsConnectedAsync(string companyId) =>
            await _db.GustoTokens.AnyAsync(t => t.CompanyId == companyId);

        [HttpGet]
        public async Task<IActionResult> Deduction()
        {
            var tok = await GetValidTokenAsync();
            if (tok == null) return View("CompanyNotConnected");

            var benefitUuid = await _svc.EnsurePostTaxBenefitAsync(tok.AccessToken, tok.CompanyId);
            var benefits = await _svc.GetCompanyBenefitsAsync(tok.AccessToken, tok.CompanyId);
            var bene = benefits.First(b => b.Uuid == benefitUuid);

            var email = User.FindFirstValue(ClaimTypes.Email)!;
            var empId = await _svc.FindEmployeeIdByEmailAsync(tok.AccessToken, tok.CompanyId, email);
            var available = await _svc.GetEmployeeCurrentNetPayAsync(tok.AccessToken, tok.CompanyId, empId);

            var vm = new DeductionViewModel
            {
                EmployeeEmail = email,
                SelectedBenefitUuid = benefitUuid,
                BenefitName = bene.Name,
                AvailableFunds = available
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deduction(DeductionViewModel m)
        {
            var tok = await GetValidTokenAsync();
            if (tok == null) return View("CompanyNotConnected");
            if (!ModelState.IsValid) return View(m);

            var empId = await _svc.FindEmployeeIdByEmailAsync(tok.AccessToken, tok.CompanyId, m.EmployeeEmail!);
            var available = await _svc.GetEmployeeCurrentNetPayAsync(tok.AccessToken, tok.CompanyId, empId);
            const decimal Fee = 5m;
            var total = m.DeductionAmount!.Value + Fee;

            if (total > available)
            {
                ModelState.AddModelError(nameof(m.DeductionAmount),
                    $"Insufficient funds: {available:C} available, need {total:C}.");
                return View(m);
            }

            _db.EmployeeAdvances.Add(new EmployeeAdvance
            {
                EmployeeId = empId,
                Amount = total,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            await _svc.CreateEmployeeBenefitAsync(tok.AccessToken, empId, m.SelectedBenefitUuid!, total);

            TempData["Success"] = $"Deduction of {total:C} created!";
            return RedirectToAction(nameof(Deduction));
        }

        [HttpGet]
        public async Task<IActionResult> TimesheetsRaw(DateTime? start, DateTime? end)
        {
            var tok = await GetValidTokenAsync();
            if (tok == null) return Unauthorized();

            var ps = start ?? DateTime.UtcNow.AddDays(-7);
            var pe = end ?? DateTime.UtcNow;
            var json = await _svc.GetRawTimeSheetsJsonAsync(tok.AccessToken, tok.CompanyId, ps, pe);
            return Content(json, "application/json");
        }

        [HttpGet]
        public async Task<IActionResult> Timesheets(DateTime? start, DateTime? end)
        {
            var tok = await GetValidTokenAsync();
            if (tok == null) return Unauthorized();

            var ps = start ?? DateTime.UtcNow.AddDays(-7);
            var pe = end ?? DateTime.UtcNow;

            var entries = await _svc.GetTimesheetEntriesAsync(tok.AccessToken, tok.CompanyId, ps, pe);

            var rates = await Task.WhenAll(
                entries.Select(e => e.EmployeeUuid).Distinct()
                       .Select(async emp =>
                       {
                           var r = await _svc.GetHourlyRateAsync(tok.AccessToken, tok.CompanyId, emp);
                           return (emp, r);
                       })
            );
            var dict = rates.ToDictionary(x => x.emp, x => x.r);

            var vm = entries.Select(e => new TimesheetEntryViewModel
            {
                EmployeeName = e.EmployeeName,
                Date = e.StartDate,
                HoursWorked = e.Hours,
                Project = e.Project,
                HourlyRate = dict[e.EmployeeUuid]
            })
            .OrderBy(x => x.EmployeeName)
            .ThenBy(x => x.Date)
            .ToList();

            return View(vm);
        }

        private async Task<GustoUserToken?> GetValidTokenAsync()
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var tok = await _db.GustoTokens.FirstOrDefaultAsync(t => t.UserId == uid!);
            if (tok == null) return null;

            var expiresAt = tok.CreatedAt.AddSeconds(tok.ExpiresIn);
            if (DateTime.UtcNow >= expiresAt)
            {
                var json = await _svc.ExchangeRefreshTokenAsync(tok.RefreshToken);
                await _svc.SaveTokenAsync(uid!, json);
                tok = await _db.GustoTokens.FirstAsync(t => t.UserId == uid!);
            }
            return tok;
        }
    }
}
