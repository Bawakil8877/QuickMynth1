// Services/GustoService.cs
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
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

        public GustoService(
            IConfiguration config,
            IHttpClientFactory httpClientFactory,
            HttpClient httpClient,
            IConfiguration configuration,
            ApplicationDbContext context)
        {
            _config = config;
            _http = httpClientFactory;
            _httpClient = httpClient;
            _configuration = configuration;
            _db = context;
            _version = _config["GustoOAuth:ApiVersion"] ?? "2024-04-01";
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

            if (resp.StatusCode == HttpStatusCode.NotFound || resp.StatusCode == HttpStatusCode.Unauthorized)
                return new List<CompanyBenefit>();

            resp.EnsureSuccessStatusCode();

            // Parse plain JSON array directly
            var list = JsonSerializer.Deserialize<List<CompanyBenefit>>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return list ?? new List<CompanyBenefit>();
        }


        public async Task<string> CreateDefaultCompanyBenefitAsync(string token, string companyId)
        {
            var client = Client(token);

            var wrapper = new
            {
                company_benefit = new
                {
                    benefit_type = 1,
                    name = "QuickMynt Advance Fee",
                    description = "Earned Wage Access Service Fee",
                    pretax = false,
                    posttax = true
                }
            };
            var payload = JsonSerializer.Serialize(wrapper);
            var resp = await client.PostAsync(
                $"{_base}/v1/companies/{companyId}/company_benefits",
                new StringContent(payload, Encoding.UTF8, "application/json")
            );

            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync();
                throw new Exception($"create company_benefit failed: {err}");
            }

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var cb = doc.RootElement.GetProperty("company_benefit");
            return cb.GetProperty("uuid").GetString()!;
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
    string token, string employeeId, string benefitUuid, decimal amount)
        {
            var client = Client(token);

            var payload = new
            {
                employee_benefit = new
                {
                    company_benefit_uuid = benefitUuid,
                    company_contribution_amount = 0m,
                    employee_deduction_amount = amount
                }
            };
            var json = JsonSerializer.Serialize(payload);
            var resp = await client.PostAsync(
                $"{_base}/v1/employees/{employeeId}/employee_benefits",
                new StringContent(json, Encoding.UTF8, "application/json")
            );

            if (!resp.IsSuccessStatusCode)
            {
                var error = await resp.Content.ReadAsStringAsync();
                throw new Exception($"employee_benefits failed: {error}");
            }
        }


        public async Task<string> EnsurePostTaxBenefitAsync(string token, string companyId)
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var all = await GetCompanyBenefitsAsync(token, companyId);

            var existing = all.FirstOrDefault(b =>
                b.Type == "post_tax" && b.Name == "QuickMynt Deduction");

            if (existing != null)
                return existing.Uuid;

            var content = new
            {
                name = "QuickMynt Deduction",
                deduction_type = "post_tax",
                benefit_type = "other", // required
                company_id = companyId
            };

            var json = JsonSerializer.Serialize(content);
            var body = new StringContent(json, Encoding.UTF8, "application/json");

            var create = await _client.PostAsync("https://api.gusto.com/v1/company_benefits", body);

            if (!create.IsSuccessStatusCode)
            {
                var err = await create.Content.ReadAsStringAsync();
                throw new Exception($"Gusto POST failed: {create.StatusCode} – {err}");
            }

            using var doc = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
            var cb = doc.RootElement.GetProperty("company_benefit");
            return cb.GetProperty("uuid").GetString()!;
        }


        // 2) Find the employee ID by email
        public async Task CreateEmployeeDeductionAsync(
    string token,
    string employeeId,
    string benefitUuid,
    decimal amount)
        {
            var client = Client(token);
            var body = new
            {
                employee_benefit = new
                {
                    company_benefit_uuid = benefitUuid,
                    company_contribution_amount = 0m,
                    employee_deduction_amount = amount
                }
            };

            var resp = await client.PostAsync(
                $"{_base}/v1/employees/{employeeId}/employee_benefits",
                new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            );

            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync();
                throw new Exception($"employee_benefits failed: {err}");
            }
        }
    }
}
