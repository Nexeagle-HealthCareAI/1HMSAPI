using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace EasyHMSAPI.Api.Mcp
{
    /// <summary>
    /// Server-to-server gate for /mcp. Every caller (router dev, router prod, ...) gets its own key;
    /// keys live in Mcp:ServiceKeys (comma-separated, set via the Mcp__ServiceKeys env var on the VM,
    /// never in git). The caller sends one as X-Service-Key. Fails closed with 503 while no real key
    /// is configured -- same pattern as InternalApiKeyFilter -- so a missing secret can never leave
    /// the endpoint open. Comparison is on SHA-256 hashes in constant time.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class McpServiceKeyMiddleware
    {
        public const string HeaderName = "X-Service-Key";

        private readonly RequestDelegate _next;
        private readonly ILogger<McpServiceKeyMiddleware> _logger;
        private readonly List<byte[]> _keyHashes;

        public McpServiceKeyMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<McpServiceKeyMiddleware> logger)
        {
            _next = next;
            _logger = logger;
            _keyHashes = (configuration["Mcp:ServiceKeys"] ?? string.Empty)
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                // The committed placeholder ("<set-via-env-...>") must never act as a real key.
                .Where(k => !k.StartsWith("<"))
                .Select(k => SHA256.HashData(Encoding.UTF8.GetBytes(k)))
                .ToList();
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (_keyHashes.Count == 0)
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                await context.Response.WriteAsJsonAsync(new { message = "MCP is not configured." });
                return;
            }

            var provided = context.Request.Headers[HeaderName].ToString();
            var providedHash = string.IsNullOrEmpty(provided) ? null : SHA256.HashData(Encoding.UTF8.GetBytes(provided));
            var ok = false;
            if (providedHash != null)
            {
                // Check every key (no early exit) so timing does not reveal which slot matched.
                foreach (var keyHash in _keyHashes)
                    ok |= CryptographicOperations.FixedTimeEquals(providedHash, keyHash);
            }

            if (!ok)
            {
                _logger.LogWarning("MCP request rejected: missing or invalid {Header}", HeaderName);
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { message = "Invalid or missing service key." });
                return;
            }

            await _next(context);
        }
    }
}
