using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuickMynth1.Data;
using QuickMynth1.Models;

namespace QuickMynth1.Services
{
    public class TimesheetService
    {
        private readonly ApplicationDbContext _db;
        public TimesheetService(ApplicationDbContext db) => _db = db;

        public async Task UpsertEntryAsync(TimesheetEntry entry)
        {
            var existing = await _db.TimesheetEntries
                .FirstOrDefaultAsync(e => e.ExternalId == entry.ExternalId);
            if (existing == null)
                _db.TimesheetEntries.Add(entry);
            else
            {
                existing.Date = entry.Date;
                existing.HoursWorked = entry.HoursWorked;
                existing.Project = entry.Project;
            }
            await _db.SaveChangesAsync();
        }

        public async Task<List<TimesheetEntry>> GetPeriodAsync(DateTime start, DateTime end)
            => await _db.TimesheetEntries
                    .Where(e => e.Date >= start && e.Date <= end)
                    .ToListAsync();
    }
}