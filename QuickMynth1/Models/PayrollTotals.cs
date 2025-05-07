using System.Text.Json;
using System.Text.Json.Serialization;

namespace QuickMynth1.Models
{
    public class PayrollTotals
    {
        // Accept net_pay as number or string
        [JsonPropertyName("net_pay")]
        public JsonElement NetPayRaw { get; set; }
    }
}
