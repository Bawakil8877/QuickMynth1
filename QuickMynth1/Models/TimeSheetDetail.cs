using System.Text.Json.Serialization;

namespace QuickMynth1.Models
{
    public class TimeSheetDetail
    {
        [JsonPropertyName("uuid")]
        public string Uuid { get; set; } = default!;

        [JsonPropertyName("employee_uuid")]
        public string EmployeeUuid { get; set; } = default!;

        [JsonPropertyName("employee_name")]
        public string EmployeeName { get; set; } = default!;

        [JsonPropertyName("entries")]
        public List<TimeSheetEntryRaw> Entries { get; set; } = new();
    }
}
