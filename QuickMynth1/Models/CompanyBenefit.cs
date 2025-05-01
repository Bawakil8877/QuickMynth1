using static System.Net.Mime.MediaTypeNames;
using System.Text.Json.Serialization;

namespace QuickMynth1.Models
{
    public class CompanyBenefit
    {
        [JsonPropertyName("uuid")]
        public string Uuid { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; }

        [JsonPropertyName("company_uuid")]
        public string CompanyUuid { get; set; }

        [JsonPropertyName("benefit_type")]
        public int BenefitType { get; set; }     // <— new

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("active")]
        public bool Active { get; set; }

        public string Name { get; set; }
        public string Type { get; set; }  // "pre_tax", "post_tax", etc.
        [JsonPropertyName("deduction_type")]
        public string? DeductionType
        {
            get; set;
        }
    }

}