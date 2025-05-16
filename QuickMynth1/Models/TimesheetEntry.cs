// Models/TimesheetEntry.cs
using System;
using System.Text.Json.Serialization;

namespace QuickMynth1.Models
{
    public class TimesheetEntry
    {
        [JsonPropertyName("uuid")]
        public string Uuid { get; set; } = null!;

        [JsonPropertyName("employee_uuid")]
        public string EmployeeUuid { get; set; } = null!;

        [JsonPropertyName("employee_name")]
        public string EmployeeName { get; set; } = null!;

        [JsonPropertyName("start_date")]
        public DateTime StartDate { get; set; }

        [JsonPropertyName("hours")]
        public decimal Hours { get; set; }

        [JsonPropertyName("project")]
        public string? Project { get; set; }
    }
}




