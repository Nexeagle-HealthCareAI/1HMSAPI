using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace EasyHMSAPI.Api.Common
{
    /// <summary>
    /// Server-to-server gate for /internal/health-articles (CMS / EasyHMS writing Health Wiki
    /// articles). Unlike PublicApiKeyFilter the key is MANDATORY: the caller must send
    /// X-Internal-Key matching Internal:HealthArticlesKey. Fails closed (503) when the key is not
    /// configured, so a missing secret can never leave the write endpoints open. Applied
    /// per-controller via [ServiceFilter].
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class InternalApiKeyFilter : IActionFilter
    {
        public const string HeaderName = "X-Internal-Key";

        private readonly string? _expectedKey;

        public InternalApiKeyFilter(IConfiguration configuration)
        {
            var key = configuration["Internal:HealthArticlesKey"];
            // The committed placeholder ("<set-via-env-...>") must never act as a real key.
            _expectedKey = string.IsNullOrWhiteSpace(key) || key.StartsWith("<") ? null : key;
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {
            if (_expectedKey == null)
            {
                context.Result = new ObjectResult(new { message = "Internal API is not configured." })
                {
                    StatusCode = StatusCodes.Status503ServiceUnavailable,
                };
                return;
            }

            var provided = context.HttpContext.Request.Headers[HeaderName].ToString();
            var ok = !string.IsNullOrEmpty(provided) && CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(provided)),
                SHA256.HashData(Encoding.UTF8.GetBytes(_expectedKey)));

            if (!ok)
            {
                context.Result = new ObjectResult(new { message = "Invalid or missing internal key." })
                {
                    StatusCode = StatusCodes.Status401Unauthorized,
                };
            }
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}
