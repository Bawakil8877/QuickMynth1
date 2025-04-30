// Models/CompanyBenefit.cs
using System.Text.Json.Serialization;

namespace QuickMynth1.Models
{
    public class CompanyBenefit
    {
        [JsonPropertyName("uuid")]
        public string Uuid { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("post_tax")]
        public bool PostTax { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; } = "";
    }
}
