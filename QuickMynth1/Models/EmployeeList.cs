// Models/EmployeeList.cs
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace QuickMynth1.Models
{
    public class EmployeeList
    {
        [JsonPropertyName("employees")]
        public List<Employee> Employees { get; set; } = new();
    }
}

