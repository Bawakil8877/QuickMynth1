namespace QuickMynth1.Models.ViewModels
{
    public class TimesheetEntryViewModel
    {
        public string EmployeeName { get; set; } = null!;
        public DateTime Date { get; set; }
        public decimal HoursWorked { get; set; }
        public string? Project { get; set; }
        public decimal HourlyRate { get; set; }
        public decimal Earned => HoursWorked * HourlyRate;
    }
}