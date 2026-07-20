using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using EasyHMSAPI.Application.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace EasyHMSAPI.Application.Services.Implementations
{
    // Free, no-key IP geolocation via ip-api.com (45 req/min on the free tier — fine for this
    // self-hosted stack's current traffic; swap in a self-hosted MaxMind GeoLite2 database behind
    // this same interface later if that ceiling is ever actually hit). Every failure mode —
    // timeout, non-2xx, malformed JSON, private/local IP — resolves to null rather than throwing,
    // since a visit must never fail to record just because region lookup had a bad moment.
    [ExcludeFromCodeCoverage]
    public class IpApiGeoLookupService : IGeoIpLookupService
    {
        private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(6);
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(2);
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _cache;
        private readonly ILogger<IpApiGeoLookupService> _logger;

        public IpApiGeoLookupService(IHttpClientFactory httpClientFactory, IMemoryCache cache, ILogger<IpApiGeoLookupService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _cache = cache;
            _logger = logger;
        }

        public async Task<GeoIpResult?> LookupAsync(string? ipAddress, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(ipAddress) || ipAddress == "unknown" || IsPrivateOrLocal(ipAddress))
                return null;

            var cacheKey = $"geoip:{ipAddress}";
            if (_cache.TryGetValue<GeoIpResult?>(cacheKey, out var cached))
                return cached;

            try
            {
                using var client = _httpClientFactory.CreateClient();
                client.Timeout = RequestTimeout;

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(RequestTimeout);

                var response = await client.GetAsync(
                    $"http://ip-api.com/json/{ipAddress}?fields=status,country,regionName,city",
                    cts.Token);

                if (!response.IsSuccessStatusCode) return CacheAndReturn(cacheKey, null);

                await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
                var payload = await JsonSerializer.DeserializeAsync<IpApiResponse>(stream, JsonOptions, cts.Token);

                if (payload == null || !string.Equals(payload.Status, "success", StringComparison.OrdinalIgnoreCase))
                    return CacheAndReturn(cacheKey, null);

                return CacheAndReturn(cacheKey, new GeoIpResult(payload.Country, payload.RegionName, payload.City));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GeoIP lookup failed for {IpAddress}", ipAddress);
                return CacheAndReturn(cacheKey, null);
            }
        }

        private GeoIpResult? CacheAndReturn(string cacheKey, GeoIpResult? result)
        {
            _cache.Set(cacheKey, result, CacheDuration);
            return result;
        }

        private static bool IsPrivateOrLocal(string ipAddress)
        {
            if (!IPAddress.TryParse(ipAddress, out var ip)) return true;
            if (IPAddress.IsLoopback(ip)) return true;

            var bytes = ip.GetAddressBytes();
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                return bytes[0] == 10
                    || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                    || (bytes[0] == 192 && bytes[1] == 168);
            }
            return false;
        }

        private class IpApiResponse
        {
            public string? Status { get; set; }
            public string? Country { get; set; }
            public string? RegionName { get; set; }
            public string? City { get; set; }
        }
    }
}
