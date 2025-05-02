using Microsoft.AspNetCore.Mvc;
using QuickMynth1.Models;
using QuickMynth1.Services;
using System.Text.Json;

namespace QuickMynth1.Controllers
{
    [ApiController]
    [Route("api/gusto/webhook")]
    public class GustoWebhookController : ControllerBase
    {
        private readonly TimesheetService _tsSvc;
        public GustoWebhookController(TimesheetService tsSvc) => _tsSvc = tsSvc;

        [HttpPost]
        public async Task<IActionResult> Receive()
        {
            // read raw body
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            var payload = JsonDocument.Parse(body).RootElement;

            var type = payload.GetProperty("event_type").GetString();
            if (type == "TimeEntryCreated" || type == "TimeEntryUpdated")
            {
                var res = payload.GetProperty("resource");
                var entry = new TimesheetEntry
                {
                    ExternalId = res.GetProperty("id").GetString()!,
                    EmployeeId = res.GetProperty("employee_uuid").GetString()!,
                    EmployeeName = res.GetProperty("employee_name").GetString()!,
                    Date = res.GetProperty("date").GetDateTime(),
                    HoursWorked = res.GetProperty("hours").GetDecimal(),
                    Project = res.GetProperty("project").GetString()!
                };
                await _tsSvc.UpsertEntryAsync(entry);
            }
            return Ok();
        }
    }
}