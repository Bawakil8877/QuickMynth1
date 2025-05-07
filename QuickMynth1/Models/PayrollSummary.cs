using System.Text.Json.Serialization;

namespace QuickMynth1.Models
{
    public class PayrollSummary
    {
        [JsonPropertyName("uuid")]
        public string Uuid { get; set; } = string.Empty;
    }
}
