using System.Text.Json.Serialization;

namespace QuickMynth1.Models
{
    public class TimeSheetSummary
    {
        [JsonPropertyName("uuid")]
        public string Uuid { get; set; } = default!;
        // other metadata fields if you need them
    }
}
