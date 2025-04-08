using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

public class RegisterViewModel : IValidatableObject
{
    [Required]
    public string Name { get; set; }

    [Required, EmailAddress]
    public string Email { get; set; }

    [Required, DataType(DataType.Password)]
    public string Password { get; set; }

    [Required, DataType(DataType.Password), Compare("Password")]
    public string ConfirmPassword { get; set; }

    [Required]
    public string RoleName { get; set; }

    // Employee fields
    public string Phone { get; set; }
    public string SSN { get; set; }
    public string HomeAddress { get; set; }
    public string OfficeAddress { get; set; }

    // Employer fields
    public string EmployerName { get; set; }
    public string CompanySize { get; set; }
    public string ManagerEmail { get; set; }

    // Conditional validation
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (RoleName == "Employee")
        {
            if (string.IsNullOrWhiteSpace(Phone))
                yield return new ValidationResult("Phone is required for Employee", new[] { "Phone" });
            if (string.IsNullOrWhiteSpace(SSN))
                yield return new ValidationResult("SSN is required for Employee", new[] { "SSN" });
            if (string.IsNullOrWhiteSpace(HomeAddress))
                yield return new ValidationResult("Home Address is required for Employee", new[] { "HomeAddress" });
            if (string.IsNullOrWhiteSpace(OfficeAddress))
                yield return new ValidationResult("Office Address is required for Employee", new[] { "OfficeAddress" });
        }
        else if (RoleName == "Employer")
        {
            if (string.IsNullOrWhiteSpace(EmployerName))
                yield return new ValidationResult("Employer Name is required for Employer", new[] { "EmployerName" });
            if (string.IsNullOrWhiteSpace(CompanySize))
                yield return new ValidationResult("Company Size is required for Employer", new[] { "CompanySize" });
            if (string.IsNullOrWhiteSpace(ManagerEmail))
                yield return new ValidationResult("Manager Email is required for Employer", new[] { "ManagerEmail" });
        }
    }
}