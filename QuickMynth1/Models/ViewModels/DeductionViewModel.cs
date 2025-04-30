using System.ComponentModel.DataAnnotations;

namespace QuickMynth1.Models.ViewModels
{
    public class DeductionViewModel
    {
        [Required, EmailAddress]
        public string EmployeeEmail { get; set; } = "";

        [Required]
        public string SelectedBenefitUuid { get; set; } = "";

        // just to show the name on the form
        public string BenefitName { get; set; } = "";

        [Required]
        [Display(Name = "Deduction Amount")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Please enter a positive amount")]
        public decimal? DeductionAmount { get; set; }
    }
}
