using System.IdentityModel.Tokens.Jwt;
using System.Net;
using EasyHMSAPI.Application.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// Authenticity checks for the anonymous ABDM callback, layered on top of the unguessable URL secret. Each layer is switched on by
    /// configuration, because the right values come from ABDM's console / the production network and cannot be guessed:
    ///   Abdm:CallbackAllowedIps   comma-separated IPs; when set, only these remote addresses are served.
    ///   Abdm:CallbackRequireJwt   true = the request must carry a bearer JWT signed by ABDM (checked against the keys at Abdm:CallbackJwksUrl).
    ///   Abdm:CallbackJwksUrl      where ABDM publishes its signing keys (JWKS).
    ///   Abdm:CallbackJwtIssuer    optional expected issuer.
    /// With none of them set the behaviour is unchanged (URL secret only). Turning CallbackRequireJwt on without a JWKS URL fails closed.
    /// NOTE: the JWT layer has not been verified against live ABDM traffic; enable it in the sandbox first.
    /// </summary>
    public interface IAbdmCallbackGuard
    {
        Task<(bool Allowed, string? Reason)> CheckAsync(IPAddress? remoteIp, string? authorizationHeader, CancellationToken cancellationToken);
    }

    public sealed class AbdmCallbackGuard : IAbdmCallbackGuard
    {
        private const string JwksCacheKey = "Abdm:CallbackJwks";
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;
        private readonly IHttpClientFactory _httpClientFactory;

        public AbdmCallbackGuard(IConfiguration configuration, IMemoryCache cache, IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _cache = cache;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<(bool Allowed, string? Reason)> CheckAsync(IPAddress? remoteIp, string? authorizationHeader, CancellationToken cancellationToken)
        {
            if (!IpAllowed(remoteIp)) return (false, "source address not allowed");

            if (bool.TryParse(_configuration["Abdm:CallbackRequireJwt"], out var requireJwt) && requireJwt)
            {
                if (string.IsNullOrWhiteSpace(authorizationHeader) || !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    return (false, "bearer token missing");
                var ok = await ValidateJwtAsync(authorizationHeader["Bearer ".Length..].Trim(), cancellationToken);
                if (!ok) return (false, "bearer token invalid");
            }
            return (true, null);
        }

        private bool IpAllowed(IPAddress? remoteIp)
        {
            var configured = _configuration["Abdm:CallbackAllowedIps"];
            if (string.IsNullOrWhiteSpace(configured)) return true;
            if (remoteIp == null) return false;
            var mapped = remoteIp.IsIPv4MappedToIPv6 ? remoteIp.MapToIPv4() : remoteIp;
            return configured.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(entry => IPAddress.TryParse(entry, out var allowed) && allowed.Equals(mapped));
        }

        private async Task<bool> ValidateJwtAsync(string token, CancellationToken cancellationToken)
        {
            var jwksUrl = _configuration["Abdm:CallbackJwksUrl"];
            if (string.IsNullOrWhiteSpace(jwksUrl)) return false;   // required but not configured: fail closed

            var issuer = _configuration["Abdm:CallbackJwtIssuer"];
            var handler = new JwtSecurityTokenHandler();
            // Try the cached keys first; if the signing key is unknown (ABDM rotated), refresh once and retry.
            foreach (var forceRefresh in new[] { false, true })
            {
                var keys = await GetKeysAsync(jwksUrl, forceRefresh, cancellationToken);
                if (keys == null) return false;
                try
                {
                    handler.ValidateToken(token, new TokenValidationParameters
                    {
                        IssuerSigningKeys = keys,
                        ValidateIssuerSigningKey = true,
                        ValidateIssuer = !string.IsNullOrWhiteSpace(issuer),
                        ValidIssuer = issuer,
                        ValidateAudience = false,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromMinutes(2),
                        RequireSignedTokens = true,
                    }, out _);
                    return true;
                }
                catch (SecurityTokenSignatureKeyNotFoundException) when (!forceRefresh) { /* refresh and retry */ }
                catch (Exception) { return false; }
            }
            return false;
        }

        private async Task<IList<SecurityKey>?> GetKeysAsync(string jwksUrl, bool forceRefresh, CancellationToken cancellationToken)
        {
            if (!forceRefresh && _cache.TryGetValue(JwksCacheKey, out IList<SecurityKey>? cached) && cached != null) return cached;
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                var json = await client.GetStringAsync(jwksUrl, cancellationToken);
                var keys = new JsonWebKeySet(json).GetSigningKeys();
                if (keys.Count == 0) return null;
                _cache.Set(JwksCacheKey, keys, TimeSpan.FromHours(1));
                return keys;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
