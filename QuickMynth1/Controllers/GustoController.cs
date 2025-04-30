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
        public GustoController(GustoService svc, ApplicationDbContext db)
        {
            _svc = svc;
            _db = db;
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

            // Ensure we have exactly one post-tax benefit created
            await _svc.EnsurePostTaxBenefitAsync(token.AccessToken, token.CompanyId);

            // Fetch and dedupe benefits
            var all = await _svc.GetCompanyBenefitsAsync(token.AccessToken, token.CompanyId);
            var postTaxItems = all
              .Where(b => b.Type == "post_tax")
              .GroupBy(b => b.Name)
              .Select(g => g.First())
              .Select(b => new SelectListItem { Text = b.Name, Value = b.Uuid })
              .ToList();

            var vm = new DeductionViewModel
            {
                EmployeeEmail = User.FindFirstValue(ClaimTypes.Email)!,
                AvailableBenefits = postTaxItems
            };

            // Pre-select the only available benefit
            if (postTaxItems.Count == 1)
                vm.SelectedBenefitUuid = postTaxItems[0].Value;

            return View(vm);
        }


        // POST: /Gusto/Deduction
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deduction(DeductionViewModel m)
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var token = await _db.GustoTokens
                                 .FirstOrDefaultAsync(t => t.UserId == uid)
                       ?? throw new Exception("Not connected to Gusto.");

            // Re‐populate WITHOUT calling EnsurePostTaxBenefitAsync here
            var all = await _svc.GetCompanyBenefitsAsync(token.AccessToken, token.CompanyId);
            m.AvailableBenefits = all
              .Where(b => b.Type == "post_tax")
              .GroupBy(b => b.Name)
              .Select(g => g.First())
              .Select(b => new SelectListItem { Text = b.Name, Value = b.Uuid })
              .ToList();

            // If only one benefit, bind it and clear its ModelState entry
            if (m.AvailableBenefits.Count == 1)
            {
                m.SelectedBenefitUuid = m.AvailableBenefits[0].Value;
                ModelState.Remove(nameof(DeductionViewModel.SelectedBenefitUuid));
            }

            if (!ModelState.IsValid)
                return View(m);

            try
            {
                var empId = await _svc.FindEmployeeIdByEmailAsync(
                                token.AccessToken, token.CompanyId, m.EmployeeEmail);

                await _svc.CreateEmployeeBenefitAsync(
                                token.AccessToken, empId,
                                m.SelectedBenefitUuid!, m.DeductionAmount!.Value);

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
