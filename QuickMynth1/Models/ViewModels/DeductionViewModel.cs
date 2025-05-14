using System.ComponentModel.DataAnnotations;

namespace QuickMynth1.Models.ViewModels
{
    public class DeductionViewModel
    {
        [Required, EmailAddress]
        public string EmployeeEmail { get; set; } = "";

        [Required]
        public string SelectedBenefitUuid { get; set; } = "";

        public string BenefitName { get; set; } = "";

        [Display(Name = "Advance Amount")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Must be at least $0.01")]
        public decimal? DeductionAmount { get; set; }

        // ← new
        public decimal AvailableFunds { get; set; }
    }

}
