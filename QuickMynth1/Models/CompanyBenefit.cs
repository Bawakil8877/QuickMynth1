// Models/CompanyBenefit.cs
using System.Text.Json.Serialization;

namespace QuickMynth1.Models
{
    public class CompanyBenefit
    {
        public string Uuid { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }  // "pre_tax", "post_tax", etc.
        [JsonPropertyName("deduction_type")]
        public string? DeductionType { get; set; }

    }

}
