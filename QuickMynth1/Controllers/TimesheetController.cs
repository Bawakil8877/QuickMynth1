using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using QuickMynth1.Data;
using QuickMynth1.Models.ViewModels;
using QuickMynth1.Services;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace QuickMynth1.Controllers
{
    [Authorize]
    public class TimesheetController : Controller
    {
        private readonly TimesheetService _tsSvc;
        private readonly GustoService _gusto;
        private readonly ApplicationDbContext _db;

        public TimesheetController(TimesheetService tsSvc, GustoService gusto, ApplicationDbContext db)
        {
            _tsSvc = tsSvc;
            _gusto = gusto;
            _db = db;
        }

        // GET: /Timesheet/Eligibility
        public async Task<IActionResult> Eligibility(DateTime start, DateTime end)
        {
            var entries = await _tsSvc.GetPeriodAsync(start, end);

            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var tok = await _db.GustoTokens
                        .FirstOrDefaultAsync(t => t.UserId == uid)
                      ?? throw new Exception("Not connected to Gusto.");
            var token = tok.AccessToken;
            var cid = tok.CompanyId;

            var vmList = new List<EligibilityViewModel>();
            foreach (var group in entries.GroupBy(e => e.EmployeeId))
            {
                var rate = await _gusto.GetHourlyRateAsync(token, cid, group.Key);
                vmList.Add(new EligibilityViewModel
                {
                    EmployeeId = group.Key,
                    TotalHours = group.Sum(x => x.HoursWorked),
                    HourlyRate = rate
                });
            }

            return View(vmList);
        }
    }
}
