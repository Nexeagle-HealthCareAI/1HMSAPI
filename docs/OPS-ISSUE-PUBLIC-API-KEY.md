# Issuing a public API key (platform directory)

`/public/*` (doctor directory, booking, reviews — used by NexEagleWebsite and, generically,
any site wanting to list/book/review publicly-listed doctors) does **not require** an API
key. Anonymous callers with no `X-Api-Key` header are let through untracked (see
`PublicApiKeyFilter`) — this is deliberately zero-configuration, since the data behind it
is only ever what a hospital has already opted into showing publicly
(`Hospital.IsPubliclyListed`). Abuse protection is a separate, always-on IP rate limit
(`PublicBookingPolicy` in `Program.cs`), unaffected by whether a key is sent.

A key is only useful if you want one specific consumer's traffic identified and
individually revocable later (e.g. a distinct partner integration) — most deployments,
including NexEagleWebsite today, don't need one. Because a leaked/self-issued key would
reach every hospital that has opted into the public directory, there is **deliberately no
HTTP endpoint** for creating or revoking these keys — a hospital admin self-issuing one
would be a privilege escalation. Use the `tools/IssuePublicApiKey` console tool instead,
run directly against the database by whoever already has DB access (same trust tier as
running a manual migration).

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
