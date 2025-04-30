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
        public GustoController(GustoService svc, ApplicationDbContext db, ILogger<GustoController> logger )
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
                return Content("Missing code.");

            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var tokenJson = await _svc.ExchangeCodeForTokenAsync(code);
            await _svc.SaveTokenAsync(uid, tokenJson);

            TempData["Success"] = "Connected to Gusto!";
            return RedirectToAction(nameof(Deduction));
        }

        // GET: /Gusto/Deduction
        [HttpGet]
        public async Task<IActionResult> Deduction()
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var token = await _db.GustoTokens
                                 .FirstOrDefaultAsync(t => t.UserId == uid)
                       ?? throw new Exception("Not connected to Gusto.");

            // Ensure one benefit exists, and get its UUID
            var benefitUuid = await _svc.EnsurePostTaxBenefitAsync(token.AccessToken, token.CompanyId);

            // Fetch that benefit’s name
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
            _logger.LogInformation(
       "POST /Gusto/Deduction vm.SelectedBenefitUuid='{Uuid}', vm.BenefitName='{Name}', vm.DeductionAmount={Amount}",
       m.SelectedBenefitUuid, m.BenefitName, m.DeductionAmount);
            if (!ModelState.IsValid)
                return View(m);

            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var token = await _db.GustoTokens
                                 .FirstOrDefaultAsync(t => t.UserId == uid)
                       ?? throw new Exception("Not connected to Gusto.");

            try
            {
                var empId = await _svc.FindEmployeeIdByEmailAsync(
                                token.AccessToken,
                                token.CompanyId,
                                m.EmployeeEmail);

                await _svc.CreateEmployeeBenefitAsync(
                                token.AccessToken,
                                empId,
                                m.SelectedBenefitUuid,
                                m.DeductionAmount!.Value);

                TempData["Success"] = "Deduction created successfully!";
                return RedirectToAction(nameof(Deduction));
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return View(m);
            }
        }



    }
}
