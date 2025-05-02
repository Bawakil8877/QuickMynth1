using System;
using System.ComponentModel.DataAnnotations;

namespace QuickMynth1.Models
{
    public class TimesheetEntry
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string ExternalId { get; set; } = default!;  // from Gusto webhook

        [Required]
        public string EmployeeId { get; set; } = default!;

        [Required]
        public string EmployeeName { get; set; } = default!;

        [Required]
        [DataType(DataType.Date)]
        public DateTime Date { get; set; }

        [Required]
        [Range(0, 24)]
        public decimal HoursWorked { get; set; }

        public string? Project { get; set; }
    }
}
