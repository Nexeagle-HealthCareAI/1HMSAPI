namespace EasyHMSAPI.Application.Services.Interfaces
{
    public record GeoIpResult(string? Country, string? Region, string? City);

    // Best-effort IP -> region resolution for the CMS "Site Visits" report. Implementations must
    // never throw and must never meaningfully delay the caller — return null on any failure,
    // timeout, or unresolvable (private/local) address instead.
    public interface IGeoIpLookupService
    {
        Task<GeoIpResult?> LookupAsync(string? ipAddress, CancellationToken cancellationToken);
    }
}
