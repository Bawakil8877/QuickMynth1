using System.Text.Json.Serialization;

namespace QuickMynth1.Models
{
    public class PayrollDetail
    {
        [JsonPropertyName("employee_compensations")]
        public List<EmployeeCompensation> EmployeeCompensations { get; set; } = new();

        [JsonPropertyName("totals")]
        public PayrollTotals Totals { get; set; } = new();
    }
}
