using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace QuickMynth1.Models
{
    public class CompanyBenefitsList
    {
        [JsonPropertyName("company_benefits")]
        public List<CompanyBenefit> CompanyBenefits { get; set; } = new();
    }
}

