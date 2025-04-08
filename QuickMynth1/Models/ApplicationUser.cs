using Microsoft.AspNetCore.Identity;

namespace QuickMynth1.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string Name { get; set; }

        // Employee fields
        public string Phone { get; set; }
        public string SSN { get; set; }
        public string HomeAddress { get; set; }
        public string OfficeAddress { get; set; }

        // Employer fields
        public string EmployerName { get; set; }
        public string CompanySize { get; set; }
        public string ManagerEmail { get; set; }
    }
}


