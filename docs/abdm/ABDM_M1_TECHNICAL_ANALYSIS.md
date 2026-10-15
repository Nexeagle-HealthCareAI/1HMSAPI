# ABDM Implementation — Technical Analysis & M1 Requirements

| | |
|---|---|
| **Date** | 2026-10-02 |
| **Scope** | The ABDM (Ayushman Bharat Digital Mission) integration in **easyHMSAPI** (.NET), **easyHMSWeb** (`features/abdm`), and **easyHMSDatabase** (ABDM tables). EasyHMS is the HMS; Doctor Dekho / WhatsApp / NLP have no ABDM code. |
| **Basis** | Full read of the code listed in §3. Nothing was run; the PRD says the flows were live-sandbox verified on 2026-08-04 and I have no way to re-check that. |
| **Caveat on "M1 requirements"** | The ABDM-side expectations in §8 come from my knowledge of the NHA integration programme plus the criteria IDs cited in the code (`CRT_ABHA_102`, `CRT_ABHA_106`). **NHA's current M1 compliance checklist was not available to me.** Treat §8 as a structured checklist to reconcile against NHA's document, not as the authoritative list. |

---

## 1. Executive summary

**Verdict: M1 is functionally built and, per the PRD, worked end-to-end in the ABDM sandbox. It is not production-ready.** The remaining work is mostly *trust and hardening*, not features.

What is there and sound:
- ABHA V3 **enrolment by Aadhaar OTP** (Aadhaar OTP → mobile OTP if the number differs → ABHA-address suggestions → create), **login/verify** (mobile / Aadhaar / ABHA-number OTP), **Find-ABHA** (search by mobile/Aadhaar when the number is forgotten), **profile / QR / ABHA card**, **edit mobile & email**, **deactivate / reactivate**.
- **Scan-and-Share** (facility QR → patient shares profile → ABDM POSTs to our callback → staff list).
- Correct ABDM crypto handshake: gateway session token (client-credentials, cached), RSA-OAEP-SHA1 encryption of PII with ABDM's public key (cached 6 h, auto-refetch), correct headers (`REQUEST-ID`, `TIMESTAMP`, `X-CM-ID` = `sbx`/`abdm`, `Bearer` X-Token).
- Aadhaar numbers/OTPs are never persisted; PII is RSA-encrypted before leaving the server; production URLs/credentials deliberately have **no defaults** (fail loud).
- Tenant guard (`HospitalAccessFilter`) and permission gate (`[RequiresPermission("abdm")]`) on the controller; callback idempotency by `(HospitalId, RequestId)`.

What blocks a production claim (details in §6):

| # | Blocker | Why it matters |
|---|---|---|
| **A-01** | `POST abdm/accounts/link` saves whatever demographics/ABHA number the **client** sends — it never proves the ABHA was OTP-verified | A hospital user (or a tampered client) can record an unverified or someone else's ABHA as "Linked" |
| **A-02** | **Consent is a UI checkbox only.** Nothing is stored server-side and the API doesn't require it; the PRD claims it is stored with timestamp/version | NHA compliance (CRT_ABHA_102) and audit exposure |
| **A-03** | The **callback endpoint** (anonymous, ingests PHI) is protected only by a secret in the URL path; no ABDM-signature/JWT check; the parser *guesses* field locations anywhere in the JSON | Spoofed/poisoned patient profiles; secret leaks via access logs |
| **A-09** | Production is not configured: prod gateway/ABHA/cert URLs, client id/secret, callback secret, public base URL, PHR share URL are all empty; HIP ID is typed in by hand | Expected pre-onboarding state, but it is the go-live critical path |
| **A-10** | The scan-and-share **acknowledgement body is unverified** (the code says so) and returns no OPD token; "handling" a share does not create/link a patient as the PRD says | Scan-and-share may be rejected by ABDM or be incomplete for the M1 assessment |

Then P1 hardening: server-side OTP resend limits (A-07), X-Token session binding and durability (A-04), PHI at rest (A-05), leaked upstream errors (A-06), resilience (A-08), data normalisation (A-11), and almost no automated tests (A-12).

---

## 2. Method

Read, in full: `AbdmController`, `AbdmCallbackController`, `AbdmAbhaService`, `AbdmEncryptionService`, `AbdmGatewayService`, `AbdmProfileShareService`, `AbdmProfileShareParser`, `AbdmModels`, the profile-share and account handlers, `HospitalAccessFilter`, `RequiresPermissionAttribute`, both ABDM SQL scripts, `appsettings*.json`, `Program.cs` registrations, the web `abdmApi.ts`, `AbdmDashboard`, `CreateAbhaWizard`, `LinkExistingAbhaWizard` (first 130 lines), the PRD `13-abdm-hcrm-subscription-profile.md`, and the one ABDM test file. **Not read in full:** `EditAbhaProfileWizard`, `ReactivateAbhaDialog`, `AbdmGuidePanel`, `CounterScanPanel`, the remaining thin handlers (they delegate to the service). Where a conclusion depends on those, it is marked *verify*.

---

## 3. Code map

| Layer | Files |
|---|---|
| **API surface** | `Controllers/AbdmController.cs` (`/abdm/*`, `[Authorize]` + `[RequiresPermission("abdm")]`), `Controllers/AbdmCallbackController.cs` (`/abdm-callback/{secret}/v3/hip/patient/profile/share`, `[AllowAnonymous]`) |
| **Services** | `AbdmGatewayService` (session token), `AbdmEncryptionService` (RSA cert + encrypt), `AbdmAbhaService` (all ABHA V3 calls, 570 lines), `AbdmProfileShareService` (on-share ack + bridge URL registration), `AbdmProfileShareParser` (tolerant callback parser) |
| **Handlers** | ~25 MediatR command/query handlers, each a thin wrapper over a service call (`GenerateAadhaarOtpHandler`, `VerifyAadhaarOtpHandler`, `RequestAbdmLoginOtpHandler`, …); persistence handlers: `SaveLinkedAbhaAccountHandler`, `RemoveAbhaAccountHandler`, `GetAbhaAccountsHandler`, `RecordAbdmProfileShareHandler`, `HandleAbdmProfileShareHandler`, `SaveAbdmFacilityHandler`, `GetAbdmProfileSharesHandler`, `GetAbdmFacilityHandler` |
| **Entities / tables** | `AbhaAccount`, `AbdmProfileShare`, `AbdmFacility` ↔ `AbhaAccount`, `AbdmProfileShare`, `AbdmFacility` (SQL: `create_tables_abdm.sql`, `create_tables_abdm_profile_share.sql`, `…email_column.sql`); `PatientRegistration.AbhaId` (free string) |
| **Web** | `abdmApi.ts`, `AbdmDashboard`, `CreateAbhaWizard`, `LinkExistingAbhaWizard` (also embedded in `PatientForm` / `AdmitPatientSheet` via `onLinked`), `EditAbhaProfileWizard`, `ReactivateAbhaDialog`, `CounterScanPanel`, `AbdmGuidePanel` |
| **Tests** | `AbdmProfileShareTests.cs` only (parser + share handlers). Services, controllers and the ABHA flows are `[ExcludeFromCodeCoverage]` and have no automated tests |

---

## 4. Architecture

```
 Staff browser (easyHMSWeb /abdm, PatientForm, AdmitPatientSheet)
        │  JWT  ·  hospitalId in every call
        ▼
 AbdmController  ──[Authorize]──[RequiresPermission("abdm")]──HospitalAccessFilter (member of hospitalId?)
        │ MediatR
        ▼
 Handlers ──► AbdmAbhaService ──► AbdmGatewayService ──(client_credentials)──► ABDM gateway  /v3/sessions
                    │                    │ cached Bearer (expiresIn − 300 s)
                    │                    ▼
                    ├──► AbdmEncryptionService ──GET cert (Bearer)──► ABDM  /profile/public/certificate
                    │          RSA-OAEP-SHA1(PII) · cert cached 6 h
                    ▼
              ABDM ABHA V3  https://abhasbx.abdm.gov.in/abha/api/v3   (prod URL must be configured)
              X-Token (per ABHA-holder session) cached in IMemoryCache, keyed by txnId, 20 min

 Scan & Share (facility QR)
   Patient's ABHA/PHR app ──► ABDM HIE-CM ──POST──► /abdm-callback/{secret}/v3/hip/patient/profile/share  (anonymous)
                                                      │ RecordAbdmProfileShareHandler
                                                      │   parse (tolerant) → resolve HIP ID → AbdmFacility → hospital
                                                      │   insert AbdmProfileShare (dedupe on hospital+requestId)
                                                      └─► 202 Accepted ──fire-and-forget──► AbdmProfileShareService.AcknowledgeAsync
                                                                                              POST …/v3/hip/patient/profile/on-share
   Staff: CounterScanPanel lists shares → "Handle" → upsert AbhaAccount (Source=FacilityQR)
```

Environment switch is by `IHostEnvironment.IsProduction()`: `X-CM-ID` = `abdm` vs `sbx`, credentials `ABDM_ClientId_Prod/Dev`, base URLs `*Prod` vs sandbox defaults. In Production the *Prod URLs have no fallback and the service **throws at construction** if missing.

---

## 5. Flow-by-flow technical walkthrough

### 5.1 Gateway session and encryption (foundation)
| Step | Implementation | Assessment |
|---|---|---|
| Access token | `POST {gateway}/v3/sessions` `{clientId, clientSecret, grantType: client_credentials}` with `REQUEST-ID`, `TIMESTAMP`, `X-CM-ID`; cached in `IMemoryCache` for `max(60, expiresIn − 300)` s; `SemaphoreSlim` double-checked lock | ✅ correct. ⚠️ no refresh-on-401 (A-08) |
| Certificate | `GET …/profile/public/certificate` (needs the same Bearer, contrary to the PDF); body may be raw PEM, JSON `publicKey`/`certificate`, or bare base64 (wrapped into PEM); handles X.509 and bare RSA keys; cached 6 h; refetch on parse failure | ✅ robust. Rotation is only detected via ABDM rejecting a decrypt (documented in code) |
| Encryption | `RSA.Encrypt(UTF8(plain), OaepSHA1)` → base64 — Aadhaar, mobile, OTP, ABHA number, find-index | ✅ matches ABDM's RSA/OAEP-SHA1 requirement. Note: `mobile` in `enrol/byAadhaar` is deliberately sent **plain** (ABDM rejected the encrypted form) |

### 5.2 Create ABHA (Aadhaar OTP)
`consent (UI)` → `POST abdm/aadhaar/generate-otp {aadhaarNumber}` → ABDM `/enrollment/request/otp` (scope `abha-enrol`, loginHint `aadhaar`, otpSystem `aadhaar`) → `verify-otp {txnId, otp, mobile}` → `/enrollment/enrol/byAadhaar` with `consent {code:"abha-enrollment", version:"1.4"}`.
- Mobile mismatch handling: if the profile returns no `mobile`, `MobileVerified=false` and the wizard sends `/enrollment/request/otp` (scope `abha-enrol`,`mobile-verify`, otpSystem `abdm`) then `/enrollment/auth/byAbdm/verify`, carrying the **new txnId** forward.
- Then `GET /enrollment/enrol/suggestion` (header `Transaction_Id`) and `POST /enrollment/enrol/abha-address {txnId, abhaAddress, preferred:1}`; the handler persists an `AbhaAccount` (`Source = AadhaarEnrol`).
- The enrolment response's `tokens.token` is cached as the X-Token for the txnId (20 min).
- Server enforcement: Aadhaar is 12 digits; nothing else. **Resend limits (2 × 60 s) and consent are UI-only** (A-02, A-07).

### 5.3 Link / verify an existing ABHA
- `login/generate-otp {loginId, loginHint ∈ mobile|aadhaar|abha-number, otpSystem ∈ abdm|aadhaar}` → `/profile/login/request/otp` (scope `abha-login` + `aadhaar-verify`/`mobile-verify`).
- `login/verify-otp` → `/profile/login/verify` with the **same scope** (a mismatch returns "Invalid Scope"); response may contain `accounts[]` — **only `accounts[0]` is used**, so a login ID mapped to several ABHA numbers silently picks the first.
- **Find ABHA**: `find/search` (`/profile/account/abha/search`, scope `search-abha`) → candidate list → `find/generate-otp` (loginHint `index`, RSA-encrypted index) → verify via the same login verify.
- `accounts/link` then upserts `AbhaAccount` (`Source = Login`) **from client-supplied fields** (A-01).
- Alternative path: pick a recent counter scan → skips OTP, because the patient consented in their own app; the client then saves via the same `accounts/link`.

### 5.4 Profile, QR, card, edit, deactivate, reactivate (need a live X-Token)
`RequireXToken(sessionTxnId)` throws "session has expired" if not cached. Endpoints: `GET /profile/account` (profile + base64 photo), `/qrCode`, `/abha-card` (binary passthrough), `request/otp` + `verify` for mobile update (scope `abha-profile`,`mobile-verify`), `request/emailVerificationLink` (email changes only after the holder clicks a link — correctly described in the UI message), deactivate (`de-activate` + reasons), reactivate (cold start, no X-Token, returns a fresh login session).

### 5.5 Facility registration and Scan-and-Share
1. **HIP ID** (`AbdmFacility`): typed by an admin per hospital (`PUT abdm/facility`), unique across hospitals (case-insensitive). No validation against HFR.
2. **Bridge URL**: `POST abdm/bridge/register` → ABDM `PATCH {gateway}/v3/bridge/url {url: "{PublicBaseUrl}/abdm-callback/{CallbackSecret}"}`. One URL **per ABDM client** (platform-wide), not per hospital.
3. **Counter QR**: `GET abdm/facility` returns `qrBaseUrl` (sandbox default `https://phrsbx.abdm.gov.in/share-profile`; **empty in Production until `Abdm:PhrShareBaseUrl` is set**); the web builds the QR for a counter id.
4. **Callback** (`AbdmCallbackController.Share`): secret compared in constant time (unset or `<…>` placeholder ⇒ 404); body parsed by `AbdmProfileShareParser`; `HipId` → `AbdmFacility` → hospital; unknown HIP ⇒ not accepted; duplicate `(HospitalId, RequestId)` ⇒ accepted/duplicate; stores demographics, `LinkToken`, `RawPayload` (≤ 20 000 chars); returns **202** immediately.
5. **Acknowledge** (`on-share`): fire-and-forget on a new DI scope; body `{acknowledgement:{status, abhaAddress}, error?, response:{requestId}}` — **explicitly flagged UNVERIFIED** in the source.
6. **Handle**: staff click → `StatusCode = HANDLED` + upsert `AbhaAccount (Source = FacilityQR)`. It does **not** create or link a `PatientRegistration`.

### 5.6 ABHA as an identity key in the HMS
`PatientRegistration.AbhaId` is a free-text column. `SearchPatientHandler` matches it by substring; `CheckPatientDuplicatesHandler` uses it as a duplicate signal but compares with **exact, case-insensitive string equality** (so `91-1234-5678-9012` ≠ `91123456789012`), whereas `GetAbdmProfileSharesHandler` carefully matches raw/digits/dashed variants (A-11). `MergePatientsHandler` carries it over. `AbhaAccount.LinkedPatientId` ("unenforced pointer") is never written by any handler I read.

### 5.7 Data model
| Table | Key / constraints | PHI held |
|---|---|---|
| `AbhaAccount` | PK `AbhaAccountId`; unique `(HospitalId, AbhaNumber)`; `Source` ∈ AadhaarEnrol/Login/FacilityQR; email column added by migration | ABHA number, address, name, gender, DOB (string), mobile, email |
| `AbdmProfileShare` | PK `ProfileShareId`; unique `(HospitalId, RequestId)`; idx `(HospitalId, StatusCode, ReceivedAt DESC)` | same + address, **LinkToken (NVARCHAR(MAX))**, **RawPayload (NVARCHAR(MAX))** |
| `AbdmFacility` | PK `HospitalId`; unique `HipId` | HFR/HIP id (not PHI) |

---

## 6. Findings (security, compliance, reliability)

Severity: **P0** = block production go-live; **P1** = fix before / immediately after; **P2** = hygiene. *Confirmed* = read in code.

| ID | Pri | Finding | Evidence | Fix |
|---|---|---|---|---|
| **A-01** | P0 | **`accounts/link` trusts the client.** The handler persists `AbhaNumber/Name/Gender/DOB/Mobile` straight from the request body with `Source="Login"`; there is no txnId/session or share-id to prove ABDM verified it. The web posts the profile it received, but the API cannot tell a forged call from a real one. *Confirmed.* | `SaveLinkedAbhaAccountHandler.cs` L21-52; `abdmApi.saveLinkedAccount`; `HandleAbdmProfileShareHandler` does it right for shares (server-held data) | Change the contract to `{ hospitalId, sessionTxnId }` (or `profileShareId`) and **fetch the profile server-side** (`GetProfileAsync`) before saving; record verification method + timestamp on the row |
| **A-02** | P0 | **Consent is not captured or enforced server-side.** `consent = {code:"abha-enrollment", version:"1.4"}` is hard-coded in the ABDM payload; the consent text lives only in `CreateAbhaWizard.tsx`; the API accepts `generate-otp` without any consent flag; no consent row. PRD §13.1 says consent text is stored with timestamp and version — **it isn't**. The code itself says to verify the wording against NHA before go-live. *Confirmed.* | `AbdmAbhaService.cs` L93; `CreateAbhaWizard.tsx` L24-27; `GenerateAadhaarOtpHandler.cs` | Add `consentAccepted` + `consentVersion` to the request, reject if false, persist `{userId, hospitalId, txnId, version, text-hash, timestamp}` (no Aadhaar). Source text/version from config, reviewed against NHA's current declaration |
| **A-03** | P0 | **Callback authenticity.** Anonymous endpoint; the only gate is a secret path segment (comparison is constant-time ✅, but the secret appears in the URL → in proxy/access logs, ABDM's console, and the registered bridge URL). No validation of an ABDM-signed token or source IP. The parser searches the **whole tree depth-first for `hipId`, `token`, `name`, …**, so an unexpected shape (or a crafted body from anyone who learns the URL) can be mis-parsed into a real hospital's inbox. HIP match is case-insensitive string equality. *Confirmed.* | `AbdmCallbackController.cs` L22-24, L68-74; `AbdmProfileShareParser.cs` (header comment, `TryFind`) | Verify ABDM's callback `Authorization` JWT against ABDM's published keys if available (*verify in spec*); add IP allow-list; keep secret out of logs (redact route); replace the tolerant parser with a strict schema once real payloads are captured (the raw body is already stored for this) |
| **A-04** | P1 | **X-Token session handling.** Sessions are `IMemoryCache` entries keyed only by `txnId` (20 min). Not bound to the staff user or hospital; lost on restart; unusable across more than one API instance. `GET abdm/abha-address/suggestions?txnId=` has **no `hospitalId`**, so `HospitalAccessFilter` fails open (it only scopes when a hospitalId is present). TxnIds are ABDM-issued and unguessable, so exploitability is low, but it is a weak binding. *Confirmed.* | `AbdmAbhaService.cs` L22, L439-447, L563-568; `HospitalAccessFilter.cs` L44-45; `AbdmController.GetAbhaAddressSuggestions` | Store sessions in a distributed cache (Redis) keyed by `(userId, hospitalId, txnId)`; require `hospitalId` on every ABDM endpoint |
| **A-05** | P1 | **PHI at rest.** ABHA number, name, DOB, mobile, address in clear; `LinkToken` and the full `RawPayload` (20 K chars) kept indefinitely; no retention or purge job; `Remove` is a hard delete of `AbhaAccount` only. *Confirmed.* | SQL scripts; `RecordAbdmProfileShareHandler` | Column encryption/TDE for `LinkToken`/`RawPayload`; purge raw payloads after parser stabilises (e.g. 30 days); define retention for handled shares |
| **A-06** | P1 | **Raw upstream errors reach the UI.** `SendAsync` throws `"ABDM request to {url} failed ({code}): {body}"` and handlers return `ex.Message` to the client — leaking ABDM URLs/bodies and making messages unfriendly. *Confirmed.* | `AbdmAbhaService.cs` L471-472; handlers' `catch (InvalidOperationException ex)` | Map to a typed `AbdmException {Code, UserMessage}`; log detail, show a safe message |
| **A-07** | P1 | **OTP resend limits (CRT_ABHA_106) are client-side only** (2 resends, 60 s). A direct API caller can hammer `aadhaar/generate-otp`, spamming Aadhaar holders and burning ABDM quota; there is no per-user rate limit or audit of Aadhaar-OTP requests. *Confirmed.* | `CreateAbhaWizard.tsx` L29-31; controller has no throttle | Per-user + per-hospital throttle, per-transaction resend counter server-side, and an audit row (user, hospital, timestamp, txnId, outcome — never the Aadhaar) |
| **A-08** | P1 | **Resilience.** Services use the default `HttpClient` (100 s timeout — the code already records a 100 s hang from a dead cert URL), no retry/circuit breaker; the gateway token is not invalidated on a 401 (up to ~25 min of failures if ABDM revokes early); the on-share ack runs fire-and-forget with failures only logged, so a failed ack is never retried. *Confirmed.* | `AbdmEncryptionService.cs` L42-44 comment; `AbdmGatewayService.cs`; `AbdmCallbackController.cs` L55-56, L76-90 | Named `HttpClient` with 10-15 s timeout + Polly (retry idempotent GETs, breaker); clear cached token on 401 and retry once; outbox + retry for acknowledgements |
| **A-09** | P0 (go-live) | **Production configuration is empty/manual.** `ABDM_ClientId/Secret_Prod`, `GatewayBaseUrlProd`, `AbhaBaseUrlProd`, `CertUrlProd`, `Abdm:PublicBaseUrl`, `Abdm:CallbackSecret`, `Abdm:PhrShareBaseUrl`, `Abdm:HipCallbackBaseUrl` are unset. `AbdmProfileShareService` returns an empty gateway URL in prod instead of failing at startup (the other services throw). Per-hospital HIP ID is typed by hand; no HFR check; bridge registration is a manual click. *Confirmed.* | `appsettings.Production.json`; `AbdmProfileShareService.cs` L35-38 | Single startup validator for the whole `Abdm` section in Production; checklist in §9 |
| **A-10** | P0/P1 | **Scan-and-Share completeness.** (a) the `on-share` body is unverified by the code's own admission, and the base URL differs from the other calls (`…/hiecm/api` vs `…/api/hiecm/gateway`); (b) the ack carries no **OPD token / context** that ABDM's scan-and-share expects the HIP to return (*verify in current spec*); (c) "Handle" only flags the row and upserts `AbhaAccount` — **no patient is created or linked**, contrary to PRD ABDM-5. *Confirmed (a, c).* | `AbdmProfileShareService.cs` L11-18, L49-54; `HandleAbdmProfileShareHandler` | Capture a real sandbox exchange, align the body to the spec, add token generation (reuse the appointment token), and wire "Handle" to open/prefill registration or link to an existing patient |
| **A-11** | P1 | **ABHA identity normalisation.** `PatientRegistration.AbhaId` is free text; duplicate detection compares exact strings (dashes matter), search uses substring, the share list uses dash/digit variants. No format validation (14 digits, `XX-XXXX-XXXX-XXXX`). `LinkedPatientId` is never populated, so ABHA↔patient is implicit. *Confirmed.* | `CheckPatientDuplicatesHandler.cs` L56, L70; `GetAbdmProfileSharesHandler` `Variants()`; `AbhaAccount` | Normalise to digits on write, store formatted for display; one shared helper used by duplicate check, search and shares; populate `LinkedPatientId` |
| **A-12** | P1 | **Almost no tests.** One test file covers the share parser/handlers. Services, controllers and ABHA flows are `[ExcludeFromCodeCoverage]`; there is no contract test harness for ABDM. The code is full of comments recording ABDM behaviour discovered by 400/401/404s, which is exactly what a regression suite should lock in. *Confirmed.* | `UnitTests/…/AbdmProfileShareTests.cs` | WireMock/HTTP-recorder tests for each flow using captured sandbox payloads; crypto round-trip test; callback fuzz tests |
| **A-13** | P2 | **Spec-drift risk.** Fields are read via multi-name fallbacks (`ABHANumber`/`abhaNumber`/`healthIdNumber`, …); only `accounts[0]` is used when several ABHA accounts come back; `TIMESTAMP` is emitted with 7 fractional digits (`"O"`), accepted by the sandbox but not the usual ISO-ms form. | `AbdmAbhaService.cs` L185-189, L455 | Pin the ABDM API version in config; let the user choose among multiple accounts; format timestamps `yyyy-MM-ddTHH:mm:ss.fffZ` |
| **A-14** | P1 | **UI/compliance copy.** Consent wording must be NHA's current text (flagged in code); terminology/logo rules and error copy should be checked against NHA's UX guidelines before the M1 assessment. | `CreateAbhaWizard.tsx` L24-27 | Compliance review of every ABDM screen |

### Corrections to PRD §13.1
| PRD says | Code shows |
|---|---|
| "consent text is stored with timestamp and version for each enrolment" | Not stored (A-02) |
| "staff **handle** a share (creates/links a patient)" | Only marks HANDLED and upserts `AbhaAccount` (A-10) |
| "ABHA verification during registration/admission inline" is a **gap** | **Already built:** `PatientForm` and `AdmitPatientSheet` embed `LinkExistingAbhaWizard` with an `onLinked` callback; remaining gap is verification of an *already-typed* ABHA number |
| "OTP rules: max 2 resends, 60 s cooldown" | UI-only; not enforced server-side (A-07) |
| "Callback URL shared secret — rotate, rate-limit, restrict by source IP/signature" | Rotation is manual; no rate limit; no IP/signature check (A-03) |

---

## 7. Operational view

| Area | Today | Needed for production |
|---|---|---|
| Observability | `ILogger` errors only | Metrics: ABDM call latency/error rate per endpoint, token refresh failures, callback received/accepted/duplicate/unknown-HIP, ack failures; alert on unknown-HIP spikes |
| Capacity | One static token + cert cache per instance | Fine; share the X-Token session store when scaling out |
| Config | Env-specific by `IsProduction()` | One validated options class; secrets in a vault; rotation runbook for client secret and callback secret |
| Subscription interplay | Expired hospitals get `402/403` on ABDM *writes* via `HospitalAccessFilter`; callbacks bypass it (anonymous) | Decide whether an expired hospital still receives shares |
| Audit | `CreatedBy`/`HandledBy` names only | Immutable audit of every ABHA create/link/deactivate/edit (who, when, hospital, txnId) |

---

## 8. M1 requirement matrix

> **Reconcile this table against NHA's current M1 compliance document.** "Required" reflects the standard M1 scope as I understand it; criteria IDs in quotes are the ones cited in the code.

| # | M1 capability | Expected of an HMS integrator | Status in code | Evidence | Gap / action |
|---|---|---|---|---|---|
| M1-1 | **HFR facility registered**, HIP ID mapped to the hospital | Facility has an HFR ID; HMS stores it and uses it to route callbacks | 🟡 manual HIP ID per hospital | `AbdmFacility`, `SaveAbdmFacilityHandler` | Register each hospital in HFR; validate format; admin audit |
| M1-2 | **Gateway authentication** (client id/secret → session token) | Client-credentials, token reuse, `X-CM-ID` | ✅ | `AbdmGatewayService` | 401 refresh (A-08); prod credentials (A-09) |
| M1-3 | **Encrypt PII** (Aadhaar, OTP, mobile) with ABDM's public key | RSA-OAEP, current cert | ✅ | `AbdmEncryptionService` | Cert rotation monitoring |
| M1-4 | **Create ABHA via Aadhaar OTP** (+ mobile verification, ABHA address) | Mandatory consent screen (`CRT_ABHA_102`); OTP resend ≤ 2 with 60 s cooldown (`CRT_ABHA_106`) | ✅ UI / ⚠️ server | `CreateAbhaWizard`, `AbdmAbhaService` | Server-side consent + resend enforcement (A-02, A-07) |
| M1-5 | Create ABHA via **other modes** (Aadhaar biometric/demographic, driving licence, mobile-only) | Depends on the facility's scope in the assessment | ❌ not built | — | Confirm with NHA whether required; DL/document enrolment is the common non-Aadhaar path |
| M1-6 | **Verify / login to ABHA** by mobile OTP, Aadhaar OTP, ABHA number/address | At least the OTP modes | ✅ (mobile / Aadhaar / ABHA-number) | `RequestLoginOtp`, `VerifyLoginOtp` | ABHA-**address** login not exposed; multiple accounts → only first used (A-13) |
| M1-7 | **Find ABHA** (forgotten number) | Search by mobile/Aadhaar | ✅ | `FindAbhaSearch*` | — |
| M1-8 | **View profile, ABHA card, QR code** | Fetch with holder's X-Token | ✅ | `GetProfile`, `GetQrCode`, `GetAbhaCard` | Session binding (A-04) |
| M1-9 | **Update mobile / email**, **deactivate / reactivate** | OTP-gated | ✅ | `Request/VerifyUpdateMobile`, `UpdateEmail`, deactivate/reactivate | Audit trail (§7) |
| M1-10 | **Link ABHA to the HMS patient** and show it on records | ABHA number stored on patient; duplicate detection | 🟡 `PatientRegistration.AbhaId` + `AbhaAccount`, linked only by string | `PatientForm`, `CheckPatientDuplicatesHandler` | Server-verified linking (A-01), normalisation (A-11), populate `LinkedPatientId`; confirm ABHA appears on prescriptions/bills/discharge (*verify*) |
| M1-11 | **Scan-and-Share** (facility QR → profile → token) | Callback endpoint, ack with token, counter handling | 🟡 receive + list built; ack unverified; no token; no patient creation | `AbdmCallbackController`, `AbdmProfileShareService` | A-03, A-10 |
| M1-12 | **Bridge / callback URL registered** with ABDM | Public HTTPS URL | ✅ endpoint to register it; prod unconfigured | `RegisterBridgeUrl` | Configure `PublicBaseUrl`, secret; register in prod |
| M1-13 | **No Aadhaar storage/logging; HTTPS; secrets safe** | Compliance + security audit | ✅ design; ❓ logging config unverified | Aadhaar only transits memory and is encrypted | Confirm request-body logging is off in every sink; secret management |
| M1-14 | **Security assessment** (third-party audit/VAPT) before production credentials | Required by NHA for go-live | ❓ not evidenced | — | Plan and schedule; fix §6 first |
| M1-15 | **UI/UX & terminology compliance** (ABHA wording, consent screen, logo use) | NHA M1 UX checklist | ❓ | `AbdmGuidePanel`, wizards | Compliance review (A-14) |
| M1-16 | **Sandbox test evidence** for each M1 scenario | Test-case execution recorded with NHA | 🟡 "live-sandbox verified 2026-08-04" (PRD), no artefacts in repo | PRD §13.1 | Re-run and archive screenshots/logs per scenario |

(M2 — HIP care-context linking and health-record push as FHIR bundles — and M3 — HIU / consent manager — are **not built**, as the PRD states. They are out of scope for M1 but are prerequisites for any "ABDM-enabled" claim beyond ABHA and for PM-JAY.)

---

## 9. Path to production and work plan

### 9.1 Go-live checklist (in order)
1. **Fix P0 code items:** A-01, A-02, A-03, A-10; add startup config validation (A-09).
2. **Harden P1:** A-04…A-08, A-11 (A-12 tests alongside).
3. **Capture real callback and `on-share` traffic** in the sandbox and replace the tolerant parser/ack body with strict versions.
4. **Security assessment** (M1-14) on the hardened build.
5. **HFR registration** for pilot hospitals; enter HIP IDs.
6. **Production onboarding:** obtain production client id/secret and the confirmed production base URLs (the code deliberately has no guesses); set `Abdm:*Prod`, `PublicBaseUrl`, `CallbackSecret` (long random), `PhrShareBaseUrl`, `HipCallbackBaseUrl` via the secret store; run `bridge/register`.
7. **NHA M1 demo/assessment** with archived evidence (M1-16).
8. **Pilot** at one hospital with monitoring on, then widen.

### 9.2 Work breakdown
| Item | Size | Pri |
|---|---|---|
| Server-side verification for `accounts/link` (session or share-id based) | S-M | P0 |
| Consent capture + enforcement + audit | S-M | P0 |
| Callback hardening (signature/IP, redacted logs, strict parser) | M | P0 |
| `on-share` body/token + patient creation/link from a share | M | P0/P1 |
| Prod config validator + secret store + runbook | S | P0 |
| Redis-backed X-Token sessions bound to user/hospital | M | P1 |
| Typed ABDM errors; safe messages | S | P1 |
| Server-side OTP throttle + audit | S-M | P1 |
| Named HttpClient + Polly; 401 refresh; ack outbox | M | P1 |
| ABHA normalisation helper; populate `LinkedPatientId` | S | P1 |
| PHI retention/encryption for `RawPayload`, `LinkToken` | S | P1 |
| Contract tests with recorded sandbox payloads | M | P1 |
| Metrics/alerts | S | P1 |
| Other enrolment modes, ABHA-address login, multi-account chooser | M | P2 |

### 9.3 Test plan
- **Contract (mocked ABDM):** every endpoint in §5 with recorded request/response pairs, including the failure shapes the code comments describe (wrong scope → "Invalid Scope", missing `mobile` → "Invalid Mobile Number", wrong X-Token prefix).
- **Crypto:** encrypt with a test keypair and decrypt in the test to prove OAEP-SHA1 and base64 handling; cert parse for PEM / X.509 / bare base64.
- **Callback:** valid, duplicate, unknown HIP, malformed JSON, oversized body, wrong secret (must be a plain 404), concurrent identical requests (unique-index race → duplicate, already handled).
- **Security:** `accounts/link` with forged fields (must fail after A-01), cross-hospital `hospitalId`, expired subscription writes, throttling.
- **E2E in sandbox (manual, archived):** create, mobile-mismatch path, link by each hint, find-ABHA with two candidates, edit mobile/email, QR/card, deactivate/reactivate, scan-and-share → handle → patient.

---

## 10. Open questions
1. **Which M1 sub-milestones does NHA require for your assessment** (enrolment modes, scan-and-share token, ABHA on clinical documents)? Please share NHA's current checklist so §8 can be made exact.
2. Is the **ABDM client one per vendor (platform-wide) or per hospital**? The bridge URL and credentials are currently platform-wide with per-hospital HIP IDs; confirm this matches how NHA issued them (and whether calls need an `X-HIP-ID`-style header for any M1 endpoint).
3. Does ABDM sign its callback requests (JWT/JWKS) in the V3 sandbox/prod, so A-03 can use signature verification rather than only a URL secret?
4. Should an **expired-subscription hospital** still receive scan-and-share callbacks?
5. Do you want **ABHA address/PHR-app–based** patient flows (patient-initiated) in M1, or staff-assisted only as built?
6. Who owns the **security assessment** and what is the target date? It gates production credentials.
