# ABDM M1 integration plan

Survey output required by the `abdm-m1` skill (`.claude/skills/abdm-m1/references/scaffold.md`,
"Before the first journey"). Written 2026-10-13 against the code on `develop`. Builds on
`ABDM_M1_TECHNICAL_ANALYSIS.md` (2026-10-02); A-01, A-02 and A-03 from that document were fixed
on 2026-10-04 and are not repeated here.

## Inventory

| Find | Where it is |
|---|---|
| Languages and runtimes | API: C# / .NET 8 (`EasyHMSAPI.*.csproj`). Web: React + TypeScript + Vite (`easyHMSWeb/package.json`) |
| Build, run, test | API: `dotnet build EasyHMSAPI.sln`, `dotnet test EasyHMSAPI.UnitTests`. Web: `npx tsc -p tsconfig.app.json --noEmit`, `npx vite build`. CI: `.github/workflows/` on the `github` remote, `develop` → dev VM, `main` → prod VM |
| Frontend/backend split | Web `src/features/abdm/services/abdmApi.ts` → API `EasyHMSAPI.Api/Controllers/AbdmController.cs` → MediatR handlers → services. No ABDM call leaves the browser |
| Outbound HTTP client | Default `HttpClient` from `builder.Services.AddHttpClient()` injected into `AbdmGatewayService`, `AbdmEncryptionService`, `AbdmAbhaService`, `AbdmProfileShareService` (registered `AddScoped` in `EasyHMSAPI.Api/Program.cs` L185-190). 100 s default timeout, no named client |
| Secrets and config | `Abdm` section of `appsettings*.json`, overridden by env vars written by the deploy workflow |
| Patient model and identifiers | `PatientRegistration.AbhaId` (free text) plus the standalone `AbhaAccount` table (`LinkedPatientId`, never populated) |
| Visit / encounter model | `Appointments` (OPD) and `Admission` (IPD). Needed for M2 care contexts, not M1 |
| Inbound routing and public URL | `EasyHMSAPI.Api/Controllers/AbdmCallbackController.cs`, gated by `AbdmCallbackGuard`. Prod is fronted by Caddy on .67; `Abdm:PublicBaseUrl` and `Abdm:CallbackSecret` are not set in prod, so callback journeys are blocked there |
| Cryptography | `EasyHMSAPI.Application/Services/Implementations/AbdmEncryptionService.cs` |
| Errors to user / logs | Handlers catch `InvalidOperationException` and return `ex.Message`; logging is `ILogger` |

## Gaps against the skill, in fix order

Each item names the file(s) and the check that proves it done.

| # | Gap | Skill rule | Files | Proof |
|---|---|---|---|---|
| 1 | `TIMESTAMP` sent as `ToString("O")` (7 fractional digits) | ISO-8601 UTC with milliseconds and `Z` | `AbdmGatewayService.cs`, `AbdmEncryptionService.cs`, `AbdmAbhaService.cs`, `AbdmProfileShareService.cs` | Unit test on the shared header builder |
| 2 | `REQUEST-ID` generated but never logged | Log it before sending; it is the only handle on a failed call | same | Request id appears in the log line and in the error returned |
| 3 | Failures throw `"ABDM request to {url} failed ({code}): {body}"` and handlers return that text to the UI (A-06). No ABDM error-code extraction; an empty body becomes a blank message | Read the body; match codes tolerant of a trailing `: `; handle a failure with no body | `AbdmAbhaService.SendAsync`/`SendBinaryAsync`, ABDM handlers | Unit tests: `ABDM-1114`, `ABDM-1114: ` and an empty body all parse to the right code and a safe message |
| 4 | Gateway token never dropped on a 401; no back-off (A-08) | Back off on an auth failure, never loop a credential check | `AbdmGatewayService.cs`, `AbdmAbhaService.SendAsync` | A 401 clears the cached token and retries exactly once |
| 5 | Default 100 s `HttpClient` (A-08) | — | `Program.cs` | Named client with a 15 s timeout |
| 6 | RSA padding hard-coded as OAEP-SHA1; the certificate response's `encryptionAlgorithm` is ignored | Read the algorithm from the certificate response, translate it, refuse one you don't recognise | `AbdmEncryptionService.cs` | Live sandbox: certificate fetch returns an `encryptionAlgorithm` the mapper recognises; unit test refuses an unknown value |
| 7 | Login verify and reactivate keep only `accounts[0]` (A-13) | Read a plural response as plural | `AbdmAbhaService.VerifyLoginOtpAsync`, `VerifyReactivateOtpAsync`, `LinkExistingAbhaWizard.tsx` | Response model carries every account; wizard lets staff pick when there is more than one |
| 8 | ABHA stored as free text, compared as exact strings (A-11) | — | `CheckPatientDuplicatesHandler`, `GetAbdmProfileSharesHandler`, `AbhaAccount` writes | One normaliser used everywhere; `14-digit` and dashed forms match |
| 9 | OTP resend limits are client-side only (A-07) | — | ABDM OTP handlers | Server rejects a third resend inside the window |
| 10 | X-Token sessions in `IMemoryCache` keyed by `txnId` only (A-04) | — | `AbdmAbhaService` | Session bound to user + hospital |
| 11 | No fail-fast config check for production (A-09) | — | `Program.cs` | Production start fails with a list of missing `Abdm:*` keys |
| 12 | Creation does not start on the login path | Every identifier starts on the login path; only `ABDM-1114` means nobody holds an account | `CreateAbhaWizard.tsx`, `abdmApi.ts` | Wizard looks the identifier up first and only offers create on `ABDM-1114` |
| 13 | Scan-and-share ack body unverified, no patient created from a share (A-10) | Log the whole callback body before parsing (already done) | `AbdmProfileShareService.cs`, `HandleAbdmProfileShareHandler` | Blocked on a public URL + a captured sandbox exchange |

Items 1-5 are one change to the shared HTTP plumbing. Items 6-12 are independent and go one at a
time. Item 13 waits on prod config.

## Test evidence

Unit tests go in `EasyHMSAPI.UnitTests` (`dotnet test EasyHMSAPI.UnitTests`). The sandbox can
vouch for the session token and the certificate fetch without any patient data. Journeys that need
a real Aadhaar or mobile OTP (create, login, profile) need a person at the counter and are recorded
against `references/test.md` by hand.
