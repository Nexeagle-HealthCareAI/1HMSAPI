using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.Services.Models
{
    [ExcludeFromCodeCoverage]
    public class HealthWikiImageResult
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; } = 200;
        public string? Message { get; set; }
        public string? Url { get; set; }

        public static HealthWikiImageResult Fail(int status, string message) => new() { Success = false, StatusCode = status, Message = message };
    }
}
