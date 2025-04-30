// Data/GustoUserToken.cs
using System;

namespace QuickMynth1.Data
{
    public class GustoUserToken
    {
        public int Id { get; set; }
        public string UserId { get; set; } = null!;
        public string AccessToken { get; set; } = null!;
        public string RefreshToken { get; set; } = null!;
        public int ExpiresIn { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CompanyId { get; set; } = null!;
    }
}

