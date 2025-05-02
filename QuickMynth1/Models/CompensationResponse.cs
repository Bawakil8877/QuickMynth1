using System.Text.Json.Serialization;

namespace QuickMynth1.Models
{
    public class CompensationResponse
    {
        [JsonPropertyName("hourly_rate")]
        public decimal HourlyRate { get; set; }
    }
}
