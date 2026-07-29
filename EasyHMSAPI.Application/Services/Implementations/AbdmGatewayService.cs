using EasyHMSAPI.Application.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EasyHMSAPI.Application.Services.Implementations
{
    /// <summary>
    /// Obtains and caches the ABDM gateway session access token (client_credentials grant) used to
    /// authorize every call to the ABHA V3 APIs. Config comes from the "Abdm" appsettings section.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class AbdmGatewayService : IAbdmGatewayService
    {
        private const string CacheKey = "Abdm:AccessToken";
        private static readonly SemaphoreSlim FetchLock = new(1, 1);

        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _gatewayBaseUrl;

        public AbdmGatewayService(HttpClient httpClient, IMemoryCache cache, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _cache = cache;
            _clientId = configuration["Abdm:ABDM_ClientId_Dev"] ?? string.Empty;
            _clientSecret = configuration["Abdm:ABDM_ClientSecret_Dev"] ?? string.Empty;
            _gatewayBaseUrl = (configuration["Abdm:GatewayBaseUrl"] ?? "https://dev.abdm.gov.in/gateway").TrimEnd('/');
        }

        public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
        {
            if (_cache.TryGetValue(CacheKey, out string? cachedToken) && !string.IsNullOrWhiteSpace(cachedToken))
                return cachedToken;

            await FetchLock.WaitAsync(cancellationToken);
            try
            {
                if (_cache.TryGetValue(CacheKey, out string? tokenAfterLock) && !string.IsNullOrWhiteSpace(tokenAfterLock))
                    return tokenAfterLock;

                if (string.IsNullOrWhiteSpace(_clientId) || string.IsNullOrWhiteSpace(_clientSecret))
                    throw new InvalidOperationException("ABDM is not configured (missing Client ID/Secret).");

                var payload = new { clientId = _clientId, clientSecret = _clientSecret, grantType = "client_credentials" };
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{_gatewayBaseUrl}/v0.5/sessions")
                {
                    Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
                };
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await _httpClient.SendAsync(request, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException($"ABDM session token request failed ({(int)response.StatusCode}): {body}");

                using var doc = JsonDocument.Parse(body);
                var accessToken = doc.RootElement.TryGetProperty("accessToken", out var at) ? at.GetString() : null;
                var expiresIn = doc.RootElement.TryGetProperty("expiresIn", out var ei) && ei.ValueKind == JsonValueKind.Number
                    ? ei.GetInt32()
                    : 1800;

                if (string.IsNullOrWhiteSpace(accessToken))
                    throw new InvalidOperationException("ABDM session token response did not contain an accessToken.");

                // Refresh a little before actual expiry so an in-flight request never sees a stale token.
                var ttl = TimeSpan.FromSeconds(Math.Max(60, expiresIn - 300));
                _cache.Set(CacheKey, accessToken, ttl);
                return accessToken;
            }
            finally
            {
                FetchLock.Release();
            }
        }
    }
}
