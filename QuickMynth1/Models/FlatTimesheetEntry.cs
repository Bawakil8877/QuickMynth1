namespace QuickMynth1.Models
{
    public class FlatTimesheetEntry
    {
        public string EmployeeName { get; set; } = default!;
        public decimal HoursWorked { get; set; }
        public string PayClassification { get; set; } = default!;
    }
}
