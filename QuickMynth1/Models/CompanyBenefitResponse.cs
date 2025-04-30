// Models/CompanyBenefitResponse.cs
using System.Text.Json.Serialization;

namespace QuickMynth1.Models
{
    public class CompanyBenefitResponse
    {
        // Covers responses that come back as { "company_benefit": { ... } }
        [JsonPropertyName("company_benefit")]
        public CompanyBenefit? Wrapped { get; set; }

        // Covers responses that come back as { "uuid": "...", "name": "...", ... }
        // (i.e. directly the benefit object)
        [JsonPropertyName("uuid")]
        public string? Uuid { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("post_tax")]
        public bool? PostTax { get; set; }
    }
}




