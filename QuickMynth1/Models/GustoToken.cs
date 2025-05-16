namespace QuickMynth1.Models
{
    public class GustoToken
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public DateTime CreatedAt { get; set; }
        public int ExpiresIn { get; set; } // in seconds
    }

}
