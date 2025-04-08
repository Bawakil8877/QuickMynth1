using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace QuickMynth1.Models.ViewModels
{
    public class RegisterViewModel
    {
        [Required]
        public string Name { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [StringLength(100, ErrorMessage = "The password must be at least 6 characters long.")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("Password", ErrorMessage = "The passwords don't match.")]
        public string ConfirmPassword { get; set; }

        [Required]
        [DisplayName("Role Name")]
        public string RoleName { get; set; }

        // EMPLOYEE-specific fields
        public string Phone { get; set; }
        public string SSN { get; set; }
        public string HomeAddress { get; set; }
        public string OfficeAddress { get; set; }

        // EMPLOYER-specific fields
        public string EmployerName { get; set; }
        public string CompanySize { get; set; }
        public string ManagerEmail { get; set; }
    }
}

