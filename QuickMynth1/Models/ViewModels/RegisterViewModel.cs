using System.ComponentModel.DataAnnotations;

namespace QuickMynth1.Models.ViewModels
{
    public class RegisterViewModel
    {
        [Required]
        public string Name { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; }

        [Required, DataType(DataType.Password)]
        public string Password { get; set; }

        [Required, DataType(DataType.Password), Compare("Password")]
        public string ConfirmPassword { get; set; }

        // Employee fields
        [Required]
        public string Phone { get; set; }

        [Required]
        public string SSN { get; set; }

        [Required]
        public string HomeAddress { get; set; }

        [Required]
        public string OfficeAddress { get; set; }

        // Employer fields
        [Required]
        public string EmployerName { get; set; }

        [Required]
        public string CompanySize { get; set; }

        [Required, EmailAddress]
        public string ManagerEmail { get; set; }
    }
}