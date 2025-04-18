using Newtonsoft.Json;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Net.Http.Headers;

namespace QuickMynth1.Services
{
    public class OAuthService
    {
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _clientFactory;

        public OAuthService(IConfiguration config, IHttpClientFactory clientFactory)
        {
            _config = config;
            _clientFactory = clientFactory;
        }

        public string GetQuickBooksAuthUrl()
        {
            var clientId = _config["QuickBooks:ClientId"];
            var redirectUri = _config["QuickBooks:RedirectUri"];
            var scopes = "com.intuit.quickbooks.accounting";

            var authUrl = $"https://appcenter.intuit.com/connect/oauth2" +
                          $"?client_id={clientId}" +
                          $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                          $"&response_type=code" +
                          $"&scope={Uri.EscapeDataString(scopes)}" +
                          $"&state=xyz";

            return authUrl;
        }

        public async Task<string> ExchangeQuickBooksCodeForTokenAsync(string code)
        {
            var clientId = _config["QuickBooks:ClientId"];
            var clientSecret = _config["QuickBooks:ClientSecret"];
            var redirectUri = _config["QuickBooks:RedirectUri"];

            var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer");
            var client = _clientFactory.CreateClient();

            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));

            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "authorization_code" },
            { "code", code },
            { "redirect_uri", redirectUri }
        });

            var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            return content; // contains access_token, refresh_token, etc.
        }

        public async Task<string> GetEmployeesAsync(string accessToken, string realmId)
        {
            var client = _clientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            string baseUrl = _config["QuickBooks:BaseUrl"];
            string queryUrl = $"{baseUrl}/{realmId}/query?query=SELECT%20*%20FROM%20Employee&minorversion=65";

            var response = await client.GetAsync(queryUrl);
            var content = await response.Content.ReadAsStringAsync();

            return content; // You can deserialize this JSON later
        }


        public async Task<string> GetContractorsAsync(string accessToken, string realmId)
        {
            var client = _clientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // NOTE: “Vendor” is the table for contractors
            string baseUrl = _config["QuickBooks:BaseUrl"];
            string queryUrl = $"{baseUrl}/{realmId}/query?query=SELECT%20*%20FROM%20Vendor&minorversion=65";

            var response = await client.GetAsync(queryUrl);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }


    }

}

