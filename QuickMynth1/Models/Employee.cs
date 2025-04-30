using System;
using System.Text.Json.Serialization;

namespace QuickMynth1.Models
{
    public class Employee
    {
        [JsonPropertyName("uuid")]
        public string Id { get; set; } = null!;

        [JsonPropertyName("email")]
        public string Email { get; set; } = null!;

        [JsonPropertyName("work_email")]
        public string WorkEmail { get; set; } = null!;

        [JsonPropertyName("first_name")]
        public string FirstName { get; set; } = null!;

        [JsonPropertyName("last_name")]
        public string LastName { get; set; } = null!;

        public string PrimaryEmail =>
            !string.IsNullOrEmpty(WorkEmail)
            ? WorkEmail
            : Email;
    }
}

