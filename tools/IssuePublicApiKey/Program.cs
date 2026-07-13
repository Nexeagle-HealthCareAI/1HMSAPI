using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

// Ops-only tool for issuing/revoking platform-wide public API keys (used by the Nexeagle
// booking website to reach the public doctor directory). No HTTP endpoint exists for this
// anymore — a key now spans every publicly-listed hospital, so self-service creation by a
// hospital admin would be a privilege escalation. Run this directly, rarely (1-3 times
// ever: prod + staging), by whoever already has DB access. See ../../docs/OPS-ISSUE-PUBLIC-API-KEY.md.
//
// Usage:
//   dotnet run --project tools/IssuePublicApiKey -- --connection-string "<conn>" --client-name "NexEagle-Prod"
//   dotnet run --project tools/IssuePublicApiKey -- --connection-string "<conn>" --revoke <apiClientId>

string? GetArg(string[] a, string name)
{
    var idx = Array.IndexOf(a, name);
    return idx >= 0 && idx + 1 < a.Length ? a[idx + 1] : null;
}

var connectionString = GetArg(args, "--connection-string");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("--connection-string is required.");
    return 1;
}

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlServer(connectionString)
    .Options;

using var db = new AppDbContext(options);

var revokeId = GetArg(args, "--revoke");
if (revokeId != null)
{
    if (!Guid.TryParse(revokeId, out var apiClientId))
    {
        Console.Error.WriteLine("--revoke must be a valid GUID.");
        return 1;
    }

    var existing = await db.PublicApiClient.FirstOrDefaultAsync(c => c.ApiClientId == apiClientId);
    if (existing is null)
    {
        Console.Error.WriteLine($"No PublicApiClient found with id {apiClientId}.");
        return 1;
    }

    existing.IsActive = false;
    existing.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();
    Console.WriteLine($"Revoked {existing.ApiClientId} ({existing.ClientName}).");
    return 0;
}

var clientName = GetArg(args, "--client-name");
if (string.IsNullOrWhiteSpace(clientName))
{
    Console.Error.WriteLine("--client-name is required when creating a key (e.g. \"NexEagle-Prod\").");
    return 1;
}

var rawKey = ApiKeyHasher.GenerateRawKey();
var client = new PublicApiClient
{
    ApiClientId = Guid.NewGuid(),
    HospitalId = null, // platform-wide — never scoped to one hospital
    ClientName = clientName,
    ApiKeyHash = ApiKeyHasher.Hash(rawKey),
    IsActive = true,
    CreatedAt = DateTime.UtcNow,
    UpdatedAt = DateTime.UtcNow,
};

db.PublicApiClient.Add(client);
await db.SaveChangesAsync();

Console.WriteLine($"Created PublicApiClient {client.ApiClientId} ({clientName}).");
Console.WriteLine("=== COPY THIS KEY NOW — IT WILL NEVER BE SHOWN AGAIN ===");
Console.WriteLine(rawKey);
return 0;
