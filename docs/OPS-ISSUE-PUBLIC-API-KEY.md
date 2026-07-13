# Issuing a public API key (platform directory)

`/public/*` (doctor directory + booking, used by the Nexeagle website) is gated by a
platform-wide API key — not scoped to any one hospital. Because a leaked/self-issued key
would reach every hospital that has opted into the public directory (`Hospital.IsPubliclyListed`),
there is **deliberately no HTTP endpoint** for creating or revoking these keys — a hospital
admin self-issuing one would be a privilege escalation. Use the `tools/IssuePublicApiKey`
console tool instead, run directly against the database by whoever already has DB access
(same trust tier as running a manual migration).

This is expected to run rarely — realistically once or twice ever per environment (a prod
key, a staging key for NexEagleWebsite).

## Create a key

```
dotnet run --project tools/IssuePublicApiKey -- --connection-string "<conn>" --client-name "NexEagle-Prod"
```

Prints the raw key **once** — copy it immediately into NexEagleWebsite's `EASYHMS_API_KEY`
env var (`.env.local`, never a committed `.env*` file). It is hashed (SHA-256, via the same
`ApiKeyHasher` the API itself uses to verify incoming keys) before being stored — the raw
value is never persisted and cannot be retrieved again.

## Revoke a key

```
dotnet run --project tools/IssuePublicApiKey -- --connection-string "<conn>" --revoke <apiClientId>
```

Deactivates immediately (`PublicApiKeyFilter` only matches `IsActive` rows) — no delete, the
row stays for audit history. Find the `ApiClientId` via a direct query against
`dbo.PublicApiClient` (there is no list endpoint either, for the same reason there's no create
endpoint).
