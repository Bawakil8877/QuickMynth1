using System.Text.Json.Serialization;

public class TokenInfoResponse
{
    public class Resource
    {
        [JsonPropertyName("uuid")]
        public string Uuid { get; set; } = "";
    }

    [JsonPropertyName("resource")]
    public Resource ResourceInfo { get; set; } = new();
}



