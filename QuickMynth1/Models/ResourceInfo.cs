using System.Text.Json.Serialization;

namespace QuickMynth1.Models
{

    public class ResourceInfo
    {
        [JsonPropertyName("resource_type")]
        public string ResourceType { get; set; } = default!;

        [JsonPropertyName("uuid")]
        public string Uuid { get; set; } = default!;
    }
}
