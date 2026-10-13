using EasyHMSAPI.Application.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EasyHMSAPI.Application.Services
{
    public sealed class AbdmResponse
    {
        public byte[] Body { get; init; } = Array.Empty<byte>();
        public string? ContentType { get; init; }
        public string RequestId { get; init; } = string.Empty;
        public string BodyText => Encoding.UTF8.GetString(Body);
    }

    /// <summary>A refused ABDM call. Derives from InvalidOperationException so the ABDM handlers'
    /// existing catch blocks still apply; <see cref="Exception.Message"/> is safe to show a user —
    /// it never carries the ABDM URL or the raw response body, which go to the log instead.</summary>
    public sealed class AbdmException : InvalidOperationException
    {
        public AbdmException(string message, string? code, int statusCode, string requestId) : base(message)
        {
            Code = code;
            StatusCode = statusCode;
            RequestId = requestId;
        }

        public string? Code { get; }
        public int StatusCode { get; }
        public string RequestId { get; }

        public bool HasCode(string code) =>
            Code != null && string.Equals(Code, AbdmHttp.NormaliseCode(code), StringComparison.OrdinalIgnoreCase);
    }

    public static class AbdmHttp
    {
        private static readonly Regex CodePattern = new(@"ABDM-\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly HashSet<string> NonMessageFields = new(StringComparer.OrdinalIgnoreCase)
        {
            "code", "errorCode", "timestamp", "path", "status", "requestId", "txnId"
        };
        private const int MaxMessageLength = 300;
        private const int MaxLoggedBodyLength = 2000;

        public static string FormatTimestamp(DateTime utc) =>
            utc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

        /// <summary>Adds the three headers every ABDM call carries and returns the REQUEST-ID — a
        /// fresh one per call, so a failed call can be traced on both sides.</summary>
        public static string AddStandardHeaders(HttpRequestMessage request, string cmId)
        {
            var requestId = Guid.NewGuid().ToString();
            request.Headers.Add("REQUEST-ID", requestId);
            request.Headers.Add("TIMESTAMP", FormatTimestamp(DateTime.UtcNow));
            request.Headers.Add("X-CM-ID", cmId);
            return requestId;
        }

        /// <summary>ABDM returns the same code bare ("ABDM-1114") or with a trailing separator
        /// ("ABDM-1114: "), so codes are compared after this, never with raw string equality.</summary>
        public static string? NormaliseCode(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;
            var match = CodePattern.Match(raw);
            if (match.Success)
                return match.Value.ToUpperInvariant();
            var trimmed = raw.Trim().TrimEnd(':', ' ');
            return trimmed.Length == 0 ? null : trimmed;
        }

        /// <summary>Pulls an error code and a human-readable message out of a refusal body. Handles
        /// an empty body, a non-JSON body, {code, message}, {error: {code, message}}, a top-level
        /// array, and a field map such as {"loginId": "Invalid LoginId"} where the refusal only
        /// names the field.</summary>
        public static (string? Code, string? Message) ParseError(string? body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return (null, null);

            string? code = null;
            string? message = null;
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                    return ParseError(root[0].GetRawText());

                if (root.ValueKind == JsonValueKind.Object)
                {
                    var error = root.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.Object ? e : root;
                    code = ReadScalar(error, "code", "errorCode") ?? ReadScalar(root, "code", "errorCode");
                    message = ReadText(error, "message", "errorMessage", "details")
                        ?? ReadText(root, "message", "errorMessage")
                        ?? (root.TryGetProperty("error", out var es) && es.ValueKind == JsonValueKind.String ? es.GetString() : null)
                        ?? ReadFieldMap(root);
                }
            }
            catch (JsonException)
            {
                // Plain text is usable as a message; an HTML error page from a proxy is not.
                var trimmed = body.Trim();
                if (!trimmed.StartsWith('<'))
                    message = trimmed;
            }

            if (code == null)
            {
                var match = CodePattern.Match(body);
                if (match.Success)
                    code = match.Value;
            }
            code = NormaliseCode(code);
            return (code, CleanMessage(message, code));
        }

        public static string BuildUserMessage(string? code, string? message, int statusCode, string requestId)
        {
            var text = !string.IsNullOrWhiteSpace(message)
                ? message
                : statusCode >= 500
                    ? $"ABDM is temporarily unavailable (HTTP {statusCode}). Please try again shortly."
                    : $"ABDM could not complete the request (HTTP {statusCode}).";
            if (code != null && !text.Contains(code, StringComparison.OrdinalIgnoreCase))
                text = $"{text.TrimEnd('.')} ({code}).";
            return $"{text} Reference: {requestId}";
        }

        /// <summary>Sends an ABDM call built by <paramref name="buildRequest"/>. On a 401 the cached
        /// gateway token is dropped and the call is rebuilt and sent exactly once more — never looped.
        /// Any other refusal becomes an <see cref="AbdmException"/>.</summary>
        public static async Task<AbdmResponse> SendAsync(
            HttpClient client,
            IAbdmGatewayService gateway,
            ILogger logger,
            Func<Task<HttpRequestMessage>> buildRequest,
            CancellationToken cancellationToken)
        {
            for (var attempt = 1; ; attempt++)
            {
                using var request = await buildRequest();
                var requestId = ReadRequestId(request);
                logger.LogInformation("ABDM {Method} {Path} REQUEST-ID {RequestId}", request.Method, request.RequestUri?.AbsolutePath, requestId);

                using var response = await client.SendAsync(request, cancellationToken);
                var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return new AbdmResponse
                    {
                        Body = bytes,
                        ContentType = response.Content.Headers.ContentType?.MediaType,
                        RequestId = requestId
                    };
                }

                if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 1)
                {
                    logger.LogWarning("ABDM returned 401 for REQUEST-ID {RequestId}; dropping the cached gateway token and retrying once.", requestId);
                    gateway.InvalidateAccessToken();
                    continue;
                }

                throw ToException(logger, request.Method.Method, request.RequestUri?.AbsolutePath, (int)response.StatusCode, Encoding.UTF8.GetString(bytes), requestId);
            }
        }

        public static AbdmException ToException(ILogger logger, string method, string? path, int statusCode, string body, string requestId)
        {
            var (code, message) = ParseError(body);
            logger.LogWarning("ABDM {Method} {Path} failed: HTTP {Status}, code {Code}, REQUEST-ID {RequestId}, body {Body}",
                method, path, statusCode, code ?? "none", requestId, Truncate(body, MaxLoggedBodyLength));
            return new AbdmException(BuildUserMessage(code, message, statusCode, requestId), code, statusCode, requestId);
        }

        private static string ReadRequestId(HttpRequestMessage request) =>
            request.Headers.TryGetValues("REQUEST-ID", out var values) ? values.FirstOrDefault() ?? string.Empty : string.Empty;

        private static string? ReadScalar(JsonElement element, params string[] names)
        {
            foreach (var name in names)
            {
                if (!element.TryGetProperty(name, out var value))
                    continue;
                if (value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
                    return value.GetString();
                if (value.ValueKind == JsonValueKind.Number)
                    return value.GetRawText();
            }
            return null;
        }

        private static string? ReadText(JsonElement element, params string[] names)
        {
            foreach (var name in names)
            {
                if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
                    return value.GetString();
            }
            return null;
        }

        private static string? ReadFieldMap(JsonElement root)
        {
            var parts = root.EnumerateObject()
                .Where(p => p.Value.ValueKind == JsonValueKind.String && !NonMessageFields.Contains(p.Name) && !string.IsNullOrWhiteSpace(p.Value.GetString()))
                .Select(p => $"{p.Name}: {p.Value.GetString()}")
                .ToList();
            return parts.Count == 0 ? null : string.Join("; ", parts);
        }

        private static string? CleanMessage(string? message, string? code)
        {
            if (string.IsNullOrWhiteSpace(message))
                return null;
            var text = message.Trim();
            if (code != null && text.StartsWith(code, StringComparison.OrdinalIgnoreCase))
                text = text[code.Length..].TrimStart(':', ' ', '-');
            if (text.Length == 0)
                return null;
            return text.Length > MaxMessageLength ? text[..MaxMessageLength] + "…" : text;
        }

        private static string Truncate(string value, int max) => value.Length > max ? value[..max] + "…" : value;
    }
}
