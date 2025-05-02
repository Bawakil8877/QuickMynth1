// Services/GustoService.cs
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.Blazor;
using QuickMynth1.Data;
using QuickMynth1.Models;

namespace QuickMynth1.Services
{
    public class GustoService
    {
       
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _http;
        private readonly ApplicationDbContext _db;
        private readonly string _base = "https://api.gusto-demo.com";
        private readonly string _version;
        private readonly HttpClient _client;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GustoService> _logger;
        public GustoService(
            IConfiguration config,
            IHttpClientFactory httpClientFactory,
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<GustoService> logger,
            ApplicationDbContext context)
        {
            _config = config;
            _http = httpClientFactory;
            _httpClient = httpClient;
            _configuration = configuration;
            _db = context;
            _version = _config["GustoOAuth:ApiVersion"] ?? "2024-04-01";
            _logger = logger;
        }

        public string GetAuthorizationUrl()
        {
            var client = _config["GustoOAuth:ClientId"];
            var redirect = Uri.EscapeDataString(_config["GustoOAuth:RedirectUri"]);
            var scopes = Uri.EscapeDataString(_config["GustoOAuth:Scopes"]);
            return $"{_base}/oauth/authorize?client_id={client}&redirect_uri={redirect}&response_type=code&scope={scopes}";
        }
      
        private HttpClient Client(string token)
        {
            var c = _http.CreateClient();
            c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            c.DefaultRequestHeaders.Add("X-Gusto-API-Version", _version);
            return c;
        }

        public async Task<string> ExchangeCodeForTokenAsync(string code)
        {
            var client = _http.CreateClient();
            var cred = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{_config["GustoOAuth:ClientId"]}:{_config["GustoOAuth:ClientSecret"]}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", cred);

            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string,string>("grant_type","authorization_code"),
                new KeyValuePair<string,string>("code", code),
                new KeyValuePair<string,string>("redirect_uri", _config["GustoOAuth:RedirectUri"])
            });

            var resp = await client.PostAsync($"{_base}/oauth/token", form);
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                throw new Exception($"Token Exchange Failed: {body}");
            return body;
        }

        public async Task<string> GetCompanyIdFromTokenInfoAsync(string token)
        {
            var client = _http.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Add("X-Gusto-API-Version", _version);

            var resp = await client.GetAsync($"{_base}/v1/token_info");
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                throw new Exception($"token_info failed: {body}");

            var info = JsonSerializer.Deserialize<TokenInfoResponse>(body)
                       ?? throw new Exception("Invalid token_info JSON");
            return info.ResourceInfo.Uuid;
        }

        public async Task SaveTokenAsync(string userId, string tokenJson)
        {
            var tok = JsonSerializer.Deserialize<GustoTokenResponse>(tokenJson)
                      ?? throw new Exception("Invalid token JSON");
            var cid = await GetCompanyIdFromTokenInfoAsync(tok.AccessToken);

            // Get the first (or null) existing token
            var existing = await _db.GustoTokens
                                    .FirstOrDefaultAsync(t => t.UserId == userId);

            if (existing != null)
            {
                existing.AccessToken = tok.AccessToken;
                existing.RefreshToken = tok.RefreshToken;
                existing.ExpiresIn = tok.ExpiresIn;
                existing.CreatedAt = DateTime.UtcNow;
                existing.CompanyId = cid;
            }
            else
            {
                _db.GustoTokens.Add(new GustoUserToken
                {
                    UserId = userId,
                    AccessToken = tok.AccessToken,
                    RefreshToken = tok.RefreshToken,
                    ExpiresIn = tok.ExpiresIn,
                    CreatedAt = DateTime.UtcNow,
                    CompanyId = cid
                });
            }

            await _db.SaveChangesAsync();
        }


        public async Task<List<CompanyBenefit>> GetCompanyBenefitsAsync(string token, string companyId)
        {
            var client = Client(token);
            var resp = await client.GetAsync($"{_base}/v1/companies/{companyId}/company_benefits");
            var body = await resp.Content.ReadAsStringAsync();

            Console.WriteLine($">>> RAW company_benefits response: {body}");   // ← log it

            if (resp.StatusCode == HttpStatusCode.NotFound || resp.StatusCode == HttpStatusCode.Unauthorized)
                return new List<CompanyBenefit>();

            resp.EnsureSuccessStatusCode();

            var list = JsonSerializer.Deserialize<List<CompanyBenefit>>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return list ?? new List<CompanyBenefit>();
        }


        public async Task<string> CreateDefaultCompanyBenefitAsync(string token, string companyId)
        {
            const int PostTaxBenefitTypeId = 998;  // from your logs

            // flat payload as required by the demo endpoint
            var payload = new
            {
                active = true,
                benefit_type = PostTaxBenefitTypeId,
                description = "Post-tax deduction for QuickMynt earned-wage advances",
                pretax = false,
                posttax = true
            };
            var body = new StringContent(
                            JsonSerializer.Serialize(payload),
                            Encoding.UTF8,
                            "application/json"
                         );
            var client = Client(token);
            var url = $"{_base}/v1/companies/{companyId}/company_benefits";

            var resp = await client.PostAsync(url, body);
            var text = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                throw new Exception($"create company_benefit failed: {text}");

            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;

            // if the API wrapped it under "company_benefit", drill in; otherwise stay at root
            if (root.TryGetProperty("company_benefit", out var wrapped))
                root = wrapped;

            // now we expect "uuid" to live here
            if (root.TryGetProperty("uuid", out var u))
                return u.GetString()!;

            // otherwise, include the raw text in the exception for debugging
            throw new Exception($"Couldn’t find uuid in response: {text}");
        }


        public async Task<string> FindEmployeeIdByEmailAsync(string token, string companyId, string email)
        {
            var client = _http.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Add("X-Gusto-API-Version", _version);

            var resp = await client.GetAsync($"{_base}/v1/companies/{companyId}/employees");
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                throw new Exception($"employees failed: {body}");

            var emps = JsonSerializer.Deserialize<List<Employee>>(body)
                       ?? new List<Employee>();
            var emp = emps.FirstOrDefault(e =>
                e.PrimaryEmail.Equals(email, StringComparison.OrdinalIgnoreCase));
            return emp?.Id
                   ?? throw new Exception($"Employee '{email}' not found.");
        }

        public async Task CreateEmployeeBenefitAsync(
     string token,
     string employeeId,
     string benefitUuid,
     decimal amount)
        {
            var client = Client(token);

            // ── NESTED WRAPPER with the *contribution* field ────────────────
            var payload = new
            {
                employee_benefit = new
                {
                    company_benefit_uuid = benefitUuid,
                    company_contribution_amount = 0m,
                    employee_contribution_amount = amount,
                    active = true
                }
            };
            // ────────────────────────────────────────────────────────────────

            var json = JsonSerializer.Serialize(payload);
            Console.WriteLine($">>> POST /employee_benefits payload: {json}");

            var resp = await client.PostAsync(
                $"{_base}/v1/employees/{employeeId}/employee_benefits",
                new StringContent(json, Encoding.UTF8, "application/json")
            );
            var text = await resp.Content.ReadAsStringAsync();
            Console.WriteLine($">>> Response {resp.StatusCode}: {text}");

            if (!resp.IsSuccessStatusCode)
                throw new Exception($"employee_benefits failed: {text}");
        }


        public async Task<string> EnsurePostTaxBenefitAsync(string token, string companyId)
        {
            var all = await GetCompanyBenefitsAsync(token, companyId);
            var existing = all.FirstOrDefault(b =>
                b.Type == "post_tax" && b.Name == "QuickMynt Deduction");
            return existing != null
                ? existing.Uuid
                : await CreateDefaultCompanyBenefitAsync(token, companyId);
        }





        // 2) Find the employee ID by email
        public async Task CreateEmployeeDeductionAsync(
     string token,
     string employeeId,
     string companyBenefitUuid,
     decimal amount)
        {
            var client = Client(token);

            var payload = new
            {
                employee_benefit = new
                {
                    company_benefit_uuid = companyBenefitUuid,
                    pretax = false,
                    posttax = true,
                    employee_deduction_amount = amount,
                    active = true
                }
            };

            var json = JsonSerializer.Serialize(payload);
            Console.WriteLine($">>> POST /employee_benefits payload: {json}");

            var resp = await client.PostAsync(
                $"{_base}/v1/employees/{employeeId}/employee_benefits",
                new StringContent(json, Encoding.UTF8, "application/json")
            );

            var text = await resp.Content.ReadAsStringAsync();
            Console.WriteLine($">>> Response {resp.StatusCode}: {text}");

            if (!resp.IsSuccessStatusCode)
                throw new Exception($"employee_benefits failed: {text}");
        }


        public async Task<int> FindDeductionBenefitTypeAsync(string token)
        {
            var client = Client(token);
            var resp = await client.GetAsync($"{_base}/v1/benefits");
            resp.EnsureSuccessStatusCode();

            var list = JsonSerializer.Deserialize<List<SupportedBenefit>>(
                await resp.Content.ReadAsStringAsync(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            )!;

            // 1) try to pick a true post-tax benefit
            var postTax = list.FirstOrDefault(b =>
                string.Equals(b.DeductionType, "post_tax", StringComparison.OrdinalIgnoreCase)
            );
            if (postTax != null)
                return postTax.Id;

            // 2) else pick the “Other” benefit if it exists
            var other = list.FirstOrDefault(b =>
                string.Equals(b.Name, "Other", StringComparison.OrdinalIgnoreCase)
            );
            if (other != null)
                return other.Id;

            // 3) final fallback: just take the first ID in the list
            return list[0].Id;
        }

        public async Task LogSupportedBenefitsAsync(string token)
        {
            var client = Client(token);
            var resp = await client.GetAsync($"{_base}/v1/benefits");
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            _logger.LogInformation("=== Gusto Supported Benefits ===\n{json}", json);
        }

        public async Task<decimal> GetHourlyRateAsync(string token, string companyId, string employeeId)
        {
            var client = Client(token);
            var url = $"{_base}/v1/companies/{companyId}/employees/{employeeId}/compensation";
            var resp = await client.GetAsync(url);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            var comp = JsonSerializer.Deserialize<CompensationResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })!;
            return comp.HourlyRate;
        }

        public async Task RegisterWebhookAsync(string token, string companyId, string callbackUrl)
        {
            var client = Client(token);
            var payload = new
            {
                event_type = "TimeEntryCreated",
                company_uuid = companyId,
                target_url = callbackUrl
            };
            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            var resp = await client.PostAsync($"{_base}/v1/webhooks", content);
            resp.EnsureSuccessStatusCode();
        }


    }
}
