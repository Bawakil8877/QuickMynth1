// Models/GustoTokenResponse.cs
using System.Text.Json.Serialization;

namespace QuickMynth1.Models
{
    public class GustoTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = "";

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = "";

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }

   
}
