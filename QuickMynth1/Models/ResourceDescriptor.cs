using System.Text.Json.Serialization;

namespace QuickMynth1.Models
{
    public class ResourceDescriptor
    {
        [JsonPropertyName("uuid")]
        public string Uuid { get; set; } = null!;   // your company UUID
    }
}
