using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Options;
using QuickMynth1.Models;
using System.Text;
using System.Text.Json;

namespace QuickMynth1.Services
{
    public class GoogleOAuthService
    {
        private readonly GoogleOAuthSettings _googleOAuthSettings;
        private readonly string _tokenDirectory;

        public GoogleOAuthService(IOptions<GoogleOAuthSettings> googleOAuthSettings)
        {
            _googleOAuthSettings = googleOAuthSettings.Value;
            _tokenDirectory = Path.Combine(Directory.GetCurrentDirectory(), "tokens");
            Directory.CreateDirectory(_tokenDirectory);
        }

        public string GenerateAuthorizationUrl()
        {
            var clientSecrets = new ClientSecrets
            {
                ClientId = _googleOAuthSettings.ClientId,
                ClientSecret = _googleOAuthSettings.ClientSecret
            };

            var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = clientSecrets,
                Scopes = new[] { GmailService.Scope.GmailSend },
                DataStore = new FileDataStore(_tokenDirectory, true)
            });

            var redirectUri = _googleOAuthSettings.RedirectUri;
            var authorizationUrl = flow.CreateAuthorizationCodeRequest(redirectUri).Build().ToString();


            return authorizationUrl;
        }

        public async Task<UserCredential> ExchangeCodeForTokenAsync(string code, string userId)
        {
            var clientSecrets = new ClientSecrets
            {
                ClientId = _googleOAuthSettings.ClientId,
                ClientSecret = _googleOAuthSettings.ClientSecret
            };

            var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = clientSecrets,
                Scopes = new[] { GmailService.Scope.GmailSend },
                DataStore = new FileDataStore(_tokenDirectory, true)
            });

            var token = await flow.ExchangeCodeForTokenAsync(userId, code, _googleOAuthSettings.RedirectUri, CancellationToken.None);

            return new UserCredential(flow, userId, token);
        }

        public async Task SendEmailUsingUserToken(string fromEmail, string toEmail, string subject, string htmlBody, string userId)
        {
            var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new ClientSecrets
                {
                    ClientId = _googleOAuthSettings.ClientId,
                    ClientSecret = _googleOAuthSettings.ClientSecret
                },
                Scopes = new[] { GmailService.Scope.GmailSend },
                DataStore = new FileDataStore(_tokenDirectory, true)
            });

            var credential = new UserCredential(flow, userId, await flow.LoadTokenAsync(userId, CancellationToken.None));

            var service = new GmailService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "QuickMynth"
            });

            var message = new Message
            {
                Raw = EncodeEmail(fromEmail, toEmail, subject, htmlBody)
            };

            await service.Users.Messages.Send(message, "me").ExecuteAsync();
        }

        private string EncodeEmail(string from, string to, string subject, string bodyHtml)
        {
            var rawMessage = $"From: {from}\r\nTo: {to}\r\nSubject: {subject}\r\nContent-Type: text/html; charset=utf-8\r\n\r\n{bodyHtml}";
            var bytes = Encoding.UTF8.GetBytes(rawMessage);
            return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').Replace("=", "");
        }
    }
}

