using QuickMynth1.Data;
using QuickMynth1.DbInitializer;
using QuickMynth1.Models;
using QuickMynth1.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Services;


var builder = WebApplication.CreateBuilder(args);
// Configure Google OAuth service
builder.Services.AddSingleton<GmailService>(sp =>
{
    // Retrieve OAuth details from appsettings.json
    var config = builder.Configuration.GetSection("GoogleOAuth");
    var clientId = config["ClientId"];
    var clientSecret = config["ClientSecret"];
    var redirectUri = config["RedirectUri"];
    var applicationName = config["ApplicationName"];



    // Set up OAuth2 credentials
    var credentials = GoogleCredential.FromFile("path-to-your-credentials-file.json")
        .CreateScoped(GmailService.Scope.GmailSend);

    // Create and return GmailService instance
    return new GmailService(new BaseClientService.Initializer()
    {
        HttpClientInitializer = credentials,
        ApplicationName = applicationName
    });
});

// Add services to the container.
builder.Services.AddControllersWithViews();

// Configure DbContext with SQL Server
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser,  IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddTransient<IQuickMynthervice, QuickMynthervice>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddTransient<EmailService>();
builder.Services.AddSession();
builder.Services.AddScoped<IDbInitializer, DbInitializer>();
// In Program.cs or Startup.cs
builder.Services.Configure<GoogleOAuthSettings>(builder.Configuration.GetSection("GoogleOAuth"));
builder.Services.AddSingleton<GoogleOAuthService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<OAuthService>();
builder.Services.AddHttpContextAccessor(); // if needed
builder.Services.AddScoped<GustoService>();
builder.Services.AddScoped<FinchService>();
builder.Services.AddHttpClient<GustoService>();
builder.Services
    .AddHttpClient("Gusto")
    .AddHttpMessageHandler(() => new LoggingHandler(logger: builder.Services
    .BuildServiceProvider()
    .GetRequiredService<ILogger<LoggingHandler>>()));




builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = new Microsoft.AspNetCore.Http.PathString("/Account/Login");
    options.AccessDeniedPath = new Microsoft.AspNetCore.Http.PathString("/Home/AccessDenied");
});

var app = builder.Build();

// Ensure the database is initialized when the application starts
using (var scope = app.Services.CreateScope())
{
    var dbInitializer = scope.ServiceProvider.GetRequiredService<IDbInitializer>();
    dbInitializer.Initialize();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
