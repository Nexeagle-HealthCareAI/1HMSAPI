namespace EasyHMSAPI.Application.Services.Interfaces
{
    // IsHosting: the address belongs to a hosting provider / data centre (crawlers, cloud servers),
    // not a home or mobile connection. Only ip-api reports it; GeoLite2-City does not (always false).
    public record GeoIpResult(string? Country, string? Region, string? City, bool IsHosting = false);

    // Best-effort IP -> region resolution for the CMS "Site Visits" report. Implementations must
    // never throw and must never meaningfully delay the caller — return null on any failure,
    // timeout, or unresolvable (private/local) address instead.
    public interface IGeoIpLookupService
    {
        Task<GeoIpResult?> LookupAsync(string? ipAddress, CancellationToken cancellationToken);
    }
}
