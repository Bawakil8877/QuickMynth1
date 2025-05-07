// Services/GustoService.cs
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.Blazor;
using QuickMynth1.Data;
using QuickMynth1.Models;
using QuickMynth1.Models.ViewModels;

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
        private readonly IHttpClientFactory _httpFactory;
        public GustoService(
            IConfiguration config,
            IHttpClientFactory httpClientFactory,
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<GustoService> logger,
            ApplicationDbContext context,
            IHttpClientFactory httpFactory)
        {
            _config = config;
            _http = httpClientFactory;
            _httpClient = httpClient;
            _configuration = configuration;
            _db = context;
            _version = _config["GustoOAuth:ApiVersion"] ?? "2024-04-01";
            _logger = logger;
            _httpFactory = httpFactory;
        }

        private HttpClient Client(string token)
        {
            var c = _httpFactory.CreateClient();
            c.BaseAddress = new Uri(_base);
            c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            c.DefaultRequestHeaders.Add("X-Gusto-API-Version", _version);
            return c;
        }

        public string GetAuthorizationUrl()
        {
            var clientId = _config["GustoOAuth:ClientId"];
            var redirect = Uri.EscapeDataString(_config["GustoOAuth:RedirectUri"]);
            var scopes = Uri.EscapeDataString(_config["GustoOAuth:Scopes"]);
            return $"{_base}/oauth/authorize?client_id={clientId}&redirect_uri={redirect}&response_type=code&scope={scopes}";
        }

        public async Task<string> ExchangeCodeForTokenAsync(string code)
        {
            var client = _httpFactory.CreateClient();
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

        public async Task<string> ExchangeRefreshTokenAsync(string refreshToken)
        {
            var client = _httpFactory.CreateClient();
            var cred = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{_config["GustoOAuth:ClientId"]}:{_config["GustoOAuth:ClientSecret"]}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", cred);

            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string,string>("grant_type","refresh_token"),
                new KeyValuePair<string,string>("refresh_token", refreshToken)
            });

            var resp = await client.PostAsync($"{_base}/oauth/token", form);
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                throw new Exception($"Refresh Token Failed: {body}");
            return body;
        }

        public async Task SaveTokenAsync(string userId, string tokenJson)
        {
            var tok = JsonSerializer.Deserialize<GustoTokenResponse>(tokenJson)
                      ?? throw new Exception("Invalid token JSON");
            var cid = await GetCompanyIdFromTokenInfoAsync(tok.AccessToken);

            var existing = await _db.GustoTokens.FirstOrDefaultAsync(t => t.UserId == userId);
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

        private async Task<string> GetCompanyIdFromTokenInfoAsync(string token)
        {
            var client = Client(token);
            var resp = await client.GetAsync($"/v1/token_info");
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                throw new Exception($"token_info failed: {body}");
            var info = JsonSerializer.Deserialize<TokenInfoResponse>(body)
                       ?? throw new Exception("Invalid token_info JSON");
            return info.ResourceInfo.Uuid;
        }



        public async Task<List<CompanyBenefit>> GetCompanyBenefitsAsync(string token, string companyId)
        {
            var client = Client(token);
            var resp = await client.GetAsync($"/v1/companies/{companyId}/company_benefits");
            var body = await resp.Content.ReadAsStringAsync();
            _logger.LogDebug("GET /company_benefits → {code}: {body}", resp.StatusCode, body);

            resp.EnsureSuccessStatusCode();
            return JsonSerializer.Deserialize<List<CompanyBenefit>>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                   ?? new List<CompanyBenefit>();
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

        /// <summary>
        /// The one payload shape that the demo API accepts for any employee_benefit add.
        /// </summary>
        /// <summary>
        /// Creates (or deducts) an employee benefit enrollment in Gusto.
        /// </summary>
        public async Task CreateEmployeeBenefitAsync(
            string token,
            string employeeId,
            string benefitUuid,
            decimal amount)
        {
            var client = Client(token);

            // build the flat payload shape that Gusto expects
            var payload = new
            {
                company_benefit_uuid = benefitUuid,                // which company benefit
                active = true,                      // enroll/activate it
                employee_deduction = amount.ToString("F2"),     // how much to deduct
                deduct_as_percentage = false,                     // false => absolute amount
                                                                  // optional: annual max, but you can omit if you don't need it
                                                                  // employee_deduction_annual_maximum = "1200.00",
                contribution = new
                {
                    type = "amount",
                    value = amount.ToString("F2")                 // company “contribution” field, same as deduction here
                }
            };

            var json = JsonSerializer.Serialize(payload);
            _logger.LogDebug(
                "POST /v1/employees/{Emp}/employee_benefits payload: {payload}",
                employeeId,
                json);

            // because BaseAddress is set in Client(), this relative path works
            var resp = await client.PostAsync(
                $"/v1/employees/{employeeId}/employee_benefits",
                new StringContent(json, Encoding.UTF8, "application/json")
            );

            var body = await resp.Content.ReadAsStringAsync();
            _logger.LogDebug("Response {StatusCode}: {body}", resp.StatusCode, body);

            if (!resp.IsSuccessStatusCode)
                throw new Exception($"employee_benefits failed: {body}");
        }


        public async Task<string> EnsurePostTaxBenefitAsync(string token, string companyId)
        {
            var all = await GetCompanyBenefitsAsync(token, companyId);
            _logger.LogDebug("All benefits: {@all}", all.Select(b => new { b.Uuid, b.Name, b.Type }));

            var found = all.FirstOrDefault(b => b.Type == "post_tax" && b.Name.Contains("QuickMynt"));
            if (found != null)
                return found.Uuid;

            // create it
            var payload = new
            {
                active = true,
                benefit_type = 998,
                description = "Post-tax deduction for QuickMynt",
                pretax = false,
                posttax = true
            };
            var json = JsonSerializer.Serialize(payload);
            _logger.LogDebug("POST /company_benefits payload: {json}", json);

            var resp = await Client(token)
                         .PostAsync($"/v1/companies/{companyId}/company_benefits",
                                    new StringContent(json, Encoding.UTF8, "application/json"));
            var text = await resp.Content.ReadAsStringAsync();
            _logger.LogDebug("Response {code}: {body}", resp.StatusCode, text);
            resp.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement.TryGetProperty("company_benefit", out var w) ? w : doc.RootElement;
            return root.GetProperty("uuid").GetString()!;
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


        public async Task<decimal> GetEmployeeTotalNetPayAsync(string token, string companyId, string employeeId)
        {
            var client = Client(token);
            var since = DateTime.UtcNow.AddYears(-1).ToString("yyyy-MM-dd");
            var respList = await client.GetAsync($"/v1/companies/{companyId}/payrolls?processing_status=processed&start_date={since}");
            if (respList.StatusCode == HttpStatusCode.Unauthorized)
                throw new UnauthorizedAccessException("Access token expired or invalid.");
            respList.EnsureSuccessStatusCode();

            var runs = JsonSerializer.Deserialize<List<PayrollSummary>>(await respList.Content.ReadAsStringAsync(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

            decimal totalNet = 0m;
            foreach (var run in runs)
            {
                var runId = run.Uuid;
                if (string.IsNullOrWhiteSpace(runId))
                    continue;

                var resp = await client.GetAsync($"/v1/companies/{companyId}/payrolls/{runId}");
                if (resp.StatusCode == HttpStatusCode.Unauthorized)
                    throw new UnauthorizedAccessException("Access token expired or invalid.");
                resp.EnsureSuccessStatusCode();

                var detail = JsonSerializer.Deserialize<PayrollDetail>(await resp.Content.ReadAsStringAsync(),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

                // Attempt to get employee-specific net pay
                decimal netPay = 0m;
                var empComp = detail.EmployeeCompensations.FirstOrDefault(e => e.EmployeeId == employeeId);
                if (empComp != null)
                {
                    if (empComp.NetPayRaw.ValueKind == JsonValueKind.Number)
                        netPay = empComp.NetPayRaw.GetDecimal();
                    else if (empComp.NetPayRaw.ValueKind == JsonValueKind.String)
                        Decimal.TryParse(empComp.NetPayRaw.GetString(), out netPay);
                }
                else
                {
                    // Fallback to total run net pay
                    var tot = detail.Totals.NetPayRaw;
                    if (tot.ValueKind == JsonValueKind.Number)
                        netPay = tot.GetDecimal();
                    else if (tot.ValueKind == JsonValueKind.String)
                        Decimal.TryParse(tot.GetString(), out netPay);
                }

                totalNet += netPay;
            }

            // Subtract any advances already recorded
            var alreadyAdvanced = await _db.EmployeeAdvances
                                              .Where(a => a.EmployeeId == employeeId)
                                              .SumAsync(a => a.Amount);
            return totalNet - alreadyAdvanced;
        }

    }

}

