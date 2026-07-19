using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Api.Common
{
    /// <summary>
    /// Resolves the IP to partition rate-limit policies by. Plain <c>HttpContext.Connection.
    /// RemoteIpAddress</c> is meaningless for traffic proxied through NexEagleWebsite's Next.js
    /// server: that app calls this API server-to-server (see easyhmsFetch), and both apps run in
    /// separate Docker containers on the same VM (easyHMSAPI with --network host) — every visitor's
    /// request arrives here from the SAME Docker bridge address regardless of which real person
    /// triggered it, turning a "20/min per IP" policy into an accidental "20/min for the entire
    /// site" policy. There's no reverse-proxy layer here to lean on ASP.NET Core's standard
    /// UseForwardedHeaders/KnownProxies feature for — this is two independent app servers, not a
    /// gateway-in-front-of-one-app topology — so trust is established with an explicit shared
    /// secret instead of an IP allowlist.
    ///
    /// Falls back to the raw connection IP whenever the secret is unset or doesn't match, which
    /// includes: direct callers hitting this API without going through NexEagleWebsite at all, and
    /// (deliberately) anyone trying to spoof the forwarded-IP header to dodge their own rate limit
    /// — without the correct secret, the header is simply ignored.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class TrustedProxyIpResolver
    {
        public const string ForwardedIpHeader = "X-Forwarded-Client-Ip";
        public const string ProxySecretHeader = "X-Internal-Proxy-Secret";

        public static string Resolve(HttpContext context, string? trustedSecret)
        {
            if (!string.IsNullOrEmpty(trustedSecret)
                && context.Request.Headers.TryGetValue(ProxySecretHeader, out var providedSecret)
                && string.Equals(providedSecret.ToString(), trustedSecret, StringComparison.Ordinal)
                && context.Request.Headers.TryGetValue(ForwardedIpHeader, out var forwardedIp))
            {
                // Only the leftmost address if a chain was somehow forwarded — that's the original
                // client; anything after it was appended by an intermediate hop.
                var ip = forwardedIp.ToString().Split(',')[0].Trim();
                if (!string.IsNullOrEmpty(ip)) return ip;
            }

            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }
    }
}
