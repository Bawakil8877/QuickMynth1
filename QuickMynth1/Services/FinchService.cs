using Microsoft.Extensions.Configuration;

namespace QuickMynth1.Services
{
    public class FinchService
    {
        private readonly IConfiguration _config;

        public FinchService(IConfiguration config)
        {
            _config = config;
        }

        public string GetConnectUrl()
        {
            var clientId = _config["Finch:ClientId"];
            var redirectUri = _config["Finch:RedirectUri"];

            // ✅ Only using the most basic product available for all: `company`
            var url = $"https://connect.tryfinch.com/authorize?client_id={clientId}&redirect_uri={redirectUri}&products=company&mode=redirect&sandbox=true";

            return url;
        }



        public async Task<string> ExchangeCodeForToken(string code)
        {
            using var client = new HttpClient();

            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue(
                    "Basic",
                    Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
                        $"{_config["Finch:ClientId"]}:{_config["Finch:ClientSecret"]}")));

            var response = await client.PostAsync("https://api.tryfinch.com/oauth/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["code"] = code,
                    ["redirect_uri"] = _config["Finch:RedirectUri"],
                    ["grant_type"] = "authorization_code"
                }));

            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Token exchange failed: {content}");
            }

            return content;
        }

    }
}
