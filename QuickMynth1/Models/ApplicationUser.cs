using Microsoft.AspNetCore.Identity;

namespace QuickMynth1.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string Name { get; set; }

        // EMPLOYEE fields
        public string? SSN { get; set; }
        public string? HomeAddress { get; set; }
        public string? OfficeAddress { get; set; }

        // EMPLOYER fields
        public string? EmployerName { get; set; }
        public string? CompanySize { get; set; }
        public string? ManagerEmail { get; set; }

        // You can explicitly add the Phone property here
        public new string Phone { get; set; }
    }
}


