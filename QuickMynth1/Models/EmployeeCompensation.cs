using System.Text.Json;
using System.Text.Json.Serialization;

namespace QuickMynth1.Models
{
    public class EmployeeCompensation
    {
        [JsonPropertyName("employee_id")]
        public string EmployeeId { get; set; } = string.Empty;

        // Accept net_pay as number or string
        [JsonPropertyName("net_pay")]
        public JsonElement NetPayRaw { get; set; }
    }
}