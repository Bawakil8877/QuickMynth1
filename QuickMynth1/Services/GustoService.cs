using Microsoft.EntityFrameworkCore;
using QuickMynth1.Data;
using QuickMynth1.Models;
using System.Net.Http.Headers;
using System.Text.Json;

namespace QuickMynth1.Services
{
    public class GustoService
    {
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ApplicationDbContext _context;
        private readonly string _baseUrl;

        public GustoService(IConfiguration config, IHttpClientFactory httpClientFactory, ApplicationDbContext context)
        {
            _config = config;
            _httpClientFactory = httpClientFactory;
            _context = context;
            _baseUrl = "https://api.gusto-demo.com"; // ✅ Correct Gusto sandbox endpoint
        }

        public string GetAuthorizationUrl()
        {
            var clientId = _config["GustoOAuth:ClientId"];
            var redirectUri = Uri.EscapeDataString(_config["GustoOAuth:RedirectUri"]); // ✅ Encode properly
            var scopes = Uri.EscapeDataString(_config["GustoOAuth:Scopes"]);

            return $"{_baseUrl}/oauth/authorize?client_id={clientId}&redirect_uri={redirectUri}&response_type=code&scope={scopes}";
        }


        public async Task<string> ExchangeCodeForTokenAsync(string code)
        {
            var clientId = _config["GustoOAuth:ClientId"];
            var clientSecret = _config["GustoOAuth:ClientSecret"];
            var redirectUri = _config["GustoOAuth:RedirectUri"];

            var client = _httpClientFactory.CreateClient();

            // ✅ Set Basic Authentication Header
            var basicAuth = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);

            var body = new Dictionary<string, string>
            {
                { "grant_type", "authorization_code" },
                { "code", code },
                { "redirect_uri", redirectUri }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/oauth/token")
            {
                Content = new FormUrlEncodedContent(body)
            };

            var response = await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Token Exchange Failed: {error}");
            }

            return await response.Content.ReadAsStringAsync();
        }

        public async Task SaveTokenAsync(string userId, string tokenJson)
        {
            var tokenData = JsonSerializer.Deserialize<GustoTokenResponse>(tokenJson);

            var token = new GustoUserToken
            {
                UserId = userId,
                AccessToken = tokenData.access_token,
                RefreshToken = tokenData.refresh_token,
                ExpiresIn = tokenData.expires_in,
                CreatedAt = DateTime.UtcNow
            };

            _context.GustoTokens.Add(token);
            await _context.SaveChangesAsync();
        }

        public async Task<GustoUserToken> GetTokenByUserIdAsync(string userId)
        {
            return await _context.GustoTokens.FirstOrDefaultAsync(x => x.UserId == userId);
        }

        public async Task<string> GetEmployeesAsync(string accessToken)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await client.GetAsync($"{_baseUrl}/v1/me"); // Adjust if needed later
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }
    }
}

