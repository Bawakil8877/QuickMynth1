using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace QuickMynth1.Models.ViewModels
{
    public class DeductionViewModel
    {
        [HiddenInput(DisplayValue = false)]
        public string EmployeeEmail { get; set; } = "";

        [Required(ErrorMessage = "Please select a benefit.")]
        [Display(Name = "Benefit")]
        public string? SelectedBenefitUuid { get; set; }

        [Required(ErrorMessage = "Please enter an amount.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero.")]
        [Display(Name = "Amount")]
        public decimal? DeductionAmount { get; set; }

        // Populated in controller
        public List<SelectListItem> AvailableBenefits { get; set; } = new();
    }
}
