using System;
using System.ComponentModel.DataAnnotations;

namespace QuickMynth1.Models
{
    public class EmployeeAdvance
    {
        [Key]
        public int Id { get; set; }
        public string EmployeeId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
