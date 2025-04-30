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

        public GustoService(
            IConfiguration config,
            IHttpClientFactory httpClientFactory,
            ApplicationDbContext context)
        {
            _config = config;
            _http = httpClientFactory;
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
            var client = _http.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Add("X-Gusto-API-Version", _version);

            var resp = await client.GetAsync($"{_base}/v1/companies/{companyId}/company_benefits");
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.StatusCode == HttpStatusCode.NotFound ||
                resp.StatusCode == HttpStatusCode.Unauthorized)
            {
                return new List<CompanyBenefit>();
            }
            if (!resp.IsSuccessStatusCode)
                throw new Exception($"company_benefits failed: {body}");

            // **directly parse the JSON array**:
            return JsonSerializer.Deserialize<List<CompanyBenefit>>(body)
                   ?? new List<CompanyBenefit>();
        }

        public async Task<string> CreateDefaultCompanyBenefitAsync(string token, string companyId)
        {
            var client = Client(token);

            // Build the payload for a post-tax deduction benefit
            var payload = new
            {
                benefit_type = 1,   // fixed‐dollar deduction
                name = "QuickMynt Advance Fee",
                description = "Earned Wage Access Service Fee",
                pretax = false,
                posttax = true
            };
            var jsonPayload = JsonSerializer.Serialize(payload);

            // Send the create request
            var response = await client.PostAsync(
                $"{_base}/v1/companies/{companyId}/company_benefits",
                new StringContent(jsonPayload, Encoding.UTF8, "application/json")
            );
            response.EnsureSuccessStatusCode();

            // Read raw body for inspection/logging
            var bodyText = await response.Content.ReadAsStringAsync();

            // Parse JSON and handle both wrapped and unwrapped formats
            using var doc = JsonDocument.Parse(bodyText);
            var root = doc.RootElement;

            JsonElement benefitElement;
            if (root.TryGetProperty("company_benefit", out var wrapped))
            {
                benefitElement = wrapped;
            }
            else
            {
                benefitElement = root;
            }

            if (!benefitElement.TryGetProperty("uuid", out var uuidProp))
            {
                throw new Exception($"Unexpected JSON creating company benefit: {bodyText}");
            }

            return uuidProp.GetString()!;
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
            var client = Client(token);

            // 1) Fetch all benefits
            var resp = await client.GetAsync($"{_base}/v1/companies/{companyId}/company_benefits");
            resp.EnsureSuccessStatusCode();
            var list = JsonSerializer.Deserialize<List<CompanyBenefit>>(
                           await resp.Content.ReadAsStringAsync())
                       ?? new List<CompanyBenefit>();

            // 2) Look for an existing post_tax benefit *with our exact name*
            var existing = list
                .FirstOrDefault(b =>
                    b.Type == "post_tax" &&
                    b.Name == "QuickMynt Advance Fee"       // match on name
                );
            if (existing != null)
                return existing.Uuid;

            // 3) Create one (only if truly missing)
            var payload = new
            {
                benefit_type = 1,
                name = "QuickMynt Advance Fee",
                description = "EWA Service Fee",
                pretax = false,
                posttax = true
            };
            var json = JsonSerializer.Serialize(payload);

            var create = await client.PostAsync(
                $"{_base}/v1/companies/{companyId}/company_benefits",
                new StringContent(json, Encoding.UTF8, "application/json")
            );
            create.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            var benefitEl = root.TryGetProperty("company_benefit", out var wrapped)
                ? wrapped
                : root;

            if (!benefitEl.TryGetProperty("uuid", out var uuidProp))
                throw new Exception($"Unexpected JSON: {await create.Content.ReadAsStringAsync()}");

            return uuidProp.GetString()!;
        }



        // 2) Find the employee ID by email
        public async Task CreateEmployeeDeductionAsync(string token, string employeeId, string benefitUuid, decimal amount)
        {
            var body = JsonSerializer.Serialize(new
            {
                employee_benefit = new
                {
                    company_benefit_uuid = benefitUuid,
                    company_contribution_amount = 0m,
                    employee_deduction_amount = amount
                }
            });

            var resp = await Client(token)
                .PostAsync($"{_base}/v1/employees/{employeeId}/employee_benefits",
                           new StringContent(body, Encoding.UTF8, "application/json"));

            if ((int)resp.StatusCode >= 400)
            {
                var err = await resp.Content.ReadAsStringAsync();
                throw new Exception($"employee_benefits failed: {err}");
            }
        }
    }
}
