using System.Text.Json.Serialization;

namespace QuickMynth1.Models
{
    public class TimeSheetEntryRaw
    {
        [JsonPropertyName("uuid")]
        public string Uuid { get; set; } = default!;

        [JsonPropertyName("hours_worked")]
        public string HoursWorked { get; set; } = default!; // e.g. "1.500"

        [JsonPropertyName("pay_classification")]
        public string PayClassification { get; set; } = default!;
    }

}
