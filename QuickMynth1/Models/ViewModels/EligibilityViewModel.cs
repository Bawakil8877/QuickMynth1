namespace QuickMynth1.Models.ViewModels
{
    public class EligibilityViewModel
    {
        public string EmployeeId { get; set; } = default!;
        public decimal TotalHours { get; set; }
        public decimal HourlyRate { get; set; }
        public decimal EarnedToDate => TotalHours * HourlyRate;
    }
}


