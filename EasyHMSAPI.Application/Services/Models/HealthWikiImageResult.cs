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
        // Why storing failed, for the CMS team only (a storage error code, or "address-not-https"). Never shown to a contributor.
        public string? Diagnostic { get; set; }

        public static HealthWikiImageResult Fail(int status, string message, string? diagnostic = null) =>
            new() { Success = false, StatusCode = status, Message = message, Diagnostic = diagnostic };
    }
}
