using System.Diagnostics.CodeAnalysis;
using System.Net;
using EasyHMSAPI.Application.Services.Interfaces;
using MaxMind.Db;
using MaxMind.GeoIP2;
using Microsoft.Extensions.Logging;

namespace EasyHMSAPI.Application.Services.Implementations
{
    // Self-hosted IP -> region lookup from a MaxMind GeoLite2-City .mmdb file (Tracking:GeoLite2CityDbPath).
    // Replaces ip-api.com: visitor IPs no longer leave our servers, there is no rate limit and no
    // network call on the request path. The file needs a free MaxMind account and a periodic refresh
    // (MaxMind publishes updates twice a week); MaxMind's licence asks for attribution where the
    // data is shown. Program.cs falls back to IpApiGeoLookupService while no file is configured.
    [ExcludeFromCodeCoverage]
    public sealed class GeoLite2LookupService : IGeoIpLookupService, IDisposable
    {
        private readonly DatabaseReader _reader;
        private readonly ILogger<GeoLite2LookupService> _logger;

        public GeoLite2LookupService(string databasePath, ILogger<GeoLite2LookupService> logger)
        {
            // Memory mode: the City database is ~70 MB and lookups become pure in-memory reads.
            _reader = new DatabaseReader(databasePath, FileAccessMode.Memory);
            _logger = logger;
        }

        public Task<GeoIpResult?> LookupAsync(string? ipAddress, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(ipAddress) || !IPAddress.TryParse(ipAddress, out var ip))
                return Task.FromResult<GeoIpResult?>(null);
            try
            {
                if (!_reader.TryCity(ip, out var city) || city == null)
                    return Task.FromResult<GeoIpResult?>(null);   // private, reserved or unknown address
                return Task.FromResult<GeoIpResult?>(new GeoIpResult(
                    city.Country.Name, city.MostSpecificSubdivision.Name, city.City.Name));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GeoLite2 lookup failed");
                return Task.FromResult<GeoIpResult?>(null);
            }
        }

        public void Dispose() => _reader.Dispose();
    }
}
