# ABDM M2 plan (HIP: link records, answer discovery, push records on consent)

Survey output required by the `abdm-m2` skill (`.claude/skills/abdm-m2/references/scaffold.md`,
"Before the first journey"). Written 2026-10-13 against `develop`. Read alongside
`abdm-integration-plan.md` (M1) — the transport rules fixed there (`AbdmHttp`: ms timestamps,
logged REQUEST-ID, safe errors, one 401 retry) are reused by every M2 call.

## Deployment answers (2026-10-13)

| Question | Answer | Consequence |
|---|---|---|
| Facility in HFR, HIP ID linked to the sandbox client ID | Not sure | A missing HIP ID is a local blocker: refuse before sending and name the setting. Readiness check shows it. |
| Public callback URL for dev | Exists, not registered as bridge URL | Build everything; registering is one click on the existing Setup > Register bridge action once `Abdm:PublicBaseUrl` + `Abdm:CallbackSecret` are set on dev. Until then calls are accepted and never answered — the exchange log names that cause. |
| Scope | Full M2 | All four journeys plus FHIR and encryption, built in the phases below. |

## Inventory (M2 rows only; M1 rows are in `abdm-integration-plan.md`)

| Find | Where it is |
|---|---|
| HIP ID per hospital | `AbdmFacility` (`EasyHMSAPI.Domain/Entities/AbdmProfileShare.cs`), saved by `SaveAbdmFacilityHandler` |
| Inbound routing | `EasyHMSAPI.Api/Controllers/AbdmCallbackController.cs`, route `abdm-callback/{secret}/v3/hip/patient/profile/share` only. Guard: `AbdmCallbackGuard` (URL secret, optional IP allow-list, optional JWT) |
| Visit = care context | OPD: `Appointment` (+ `Prescription`, `AppointmentVitals`, `PathologyReport`). IPD: `Admission` (+ `DischargeSummary`, `PathologyReport`). One care context per OPD visit and per IPD admission, as the skill recommends |
| Records → HI types | `Prescription` → Prescription / OPConsultation; `PathologyReport` → DiagnosticReport; `DischargeSummary` → DischargeSummary; `AppointmentVitals` → WellnessRecord; `BillingInvoice` → Invoice |
| Patient ↔ ABHA | `PatientRegistration.AbhaId` (+ `AbhaAccount`) |
| FHIR | none, add: `Hl7.Fhir.R4` NuGet, new `EasyHMSAPI.Application/Services/Fhir/` |
| Curve25519 / HKDF / AES-GCM | none, add: `BouncyCastle.Cryptography` NuGet, new `EasyHMSAPI.Application/Services/AbdmFidelius.cs`. Interop proved against the Fidelius CLI (Java 17 is installed) |
| Patient OTP for user-initiated linking | existing WhatsApp/SMS services (to confirm which in phase 4) |

## Phases (each one commit, tested, then pushed)

1. **Exchange log + catch-all callback receiver.** New tables `AbdmExchange` (one row per outbound call: REQUEST-ID, kind, hospital, status sent → accepted / refused / answered / no-answer, error code+message, timings, callback body) and `AbdmInbound` (every inbound delivery: path, header names + lengths only, raw body capped, facility, verdict). Row is written *before* sending so a fast callback can't win the race. Catch-all route `abdm-callback/{secret}/{**path}` records first, checks (bearer present; `X-HIP-ID` names one of our facilities; an answer's `response.requestId` matches a sent, unanswered exchange), acks 202 at once, then dispatches. Proof: unit tests for each check, including a valid body with `Authorization` removed being rejected and logged.
2. **Link token + HIP-initiated linking.** `AbdmCareContext` table (per OPD visit / IPD admission: reference number, patient-facing display with no diagnosis, HI types, link status + exchange ids). `AbdmLinkToken` (per HIP + ABHA address, from `on-generate-token`). Calls: `generate-token`, `link/carecontext` (with `X-LINK-TOKEN`), `link/context/notify`, `sms/notify2`. Never blocks: the button starts the work and returns; status lives on the record. Duplicate refusal (`ABDM-1092`) shown as such and points back at the first refusal. Proof: tests on the state machine; sandbox evidence once the bridge is registered.
3. **Web: link status + integrator panel.** Per-visit "ABDM" chip and Link button; readiness check (HIP ID missing = blocker, callback URL missing = warning); exchange list grouped by REQUEST-ID with header names/lengths, never values.
4. **User-initiated linking.** Inbound `discover` → match on verified identifiers → `on-discover`; `init` → OTP to the patient's mobile → `on-init`; `confirm` → verify OTP → `on-confirm`.
5. **Encryption (Fidelius).** X25519/Curve25519 key pair + 32-byte nonce per transfer, XOR nonces (20-byte salt, 12-byte IV), HKDF-SHA256 over the shared key's x coordinate with empty info, AES-256-GCM, tag appended, base64. Proof: round trip against the Fidelius CLI, both directions.
6. **FHIR bundles.** NRCES document bundles per HI type from the records above, validated before storing (references resolve inside the bundle). Follows `.claude/skills/abdm-fhir`.
7. **Consent + data flow.** `consent/request/hip/notify` → store the artefact → `on-notify`; `health-information/request` → check consent (granted, in date, care contexts ours) → `on-request` → build + encrypt + POST pages to `dataPushUrl` → `health-information/notify` with per-care-context status.

## Test commands

`dotnet test EasyHMSAPI.UnitTests --filter "FullyQualifiedName~Abdm"`; web `npx tsc -p tsconfig.app.json --noEmit` and `npx vite build`. Sandbox evidence for every journey needs the bridge URL registered and a HIP ID linked to the client ID.

## Phase 4 as built (2026-11-17, not yet compiled or run: written without a .NET SDK)

- `AbdmUserLinkingService` answers `discover`, `init` and `confirm`; `AbdmCallbackProcessor` routes to it. Rules are in `AbdmDiscoveryRules` (pure, unit tested in `AbdmUserLinkingTests`).
- Matching: a verified ABHA number/address equal to the ABHA on our record is enough; otherwise a verified mobile plus agreeing gender, year of birth and name. Two candidates is no match unless the typed patient ID (MR) picks one. A typed ID alone never matches.
- Offered: the 25 most recent visits and 25 most recent admissions that hold records and are not linked or being linked (`IAbdmLinkingService.UnlinkedForPatientAsync`, which creates the care context rows).
- OTP: six digits to the mobile on our record via `IWhatsAppMessagingService.SendOtpAsync`, 10 minutes, 3 attempts, stored as a salted SHA-256 only. New table `AbdmLinkRequest` (1HMSDatabase `create_tables_abdm_link_request.sql`).
- To confirm against the sandbox, because NHA's references do not state them: the bodies ABDM sends for `init` (care contexts are read from any `careContexts[].referenceNumber`) and `confirm` (`linkRefNumber`, `token`), and the error codes we answer with (no patient ABDM-1010, no unlinked records ABDM-1012, unknown transaction ABDM-1017, records not offered ABDM-1057, bad link reference ABDM-1056, OTP wrong or expired ABDM-1036, OTP not sent ABDM-1004).

## Phases 5 to 7 and M3 as built (2026-11-17, not yet compiled or run: written without a .NET SDK)

- **Phase 5** `AbdmFidelius`: curve25519 (Weierstrass, BouncyCastle via MailKit's dependency), nonce XOR, HKDF-SHA256 over x, AES-256-GCM. `AbdmFideliusTests` pins known answers computed independently. **Confirmed 2026-11-17** against Fidelius CLI 1.2.0 in both directions: our known-answer ciphertext decrypts in the CLI, the CLI's encryption of the same input is byte-identical, and fresh CLI keys (raw and X.509 public keys) round-trip both ways. BouncyCastle C# 2.x has no named `curve25519`, so the curve is built from its parameters.
- **Phase 6** `Services/Fhir/AbdmFhir` (plain JSON, no `Hl7.Fhir.R4`) and `AbdmRecordSource`: Prescription, DiagnosticReport, DischargeSummary, WellnessRecord. **Tier 2 done 2026-11-17**: all four record types validate with 0 errors against the HL7 validator 7.0.6 with `ndhm.in#6.5.0` (warnings: no per-resource narrative). Tier 1 (`validate`) not run: it needs ABDM's docs tool. Lab results are sent as the report with its conclusion, not as coded Observations.
- **Phase 7** `AbdmDataFlowService`: consent notify → on-notify; health-information request → on-request → push to `dataPushUrl` → notify. Tables `AbdmConsent`, `AbdmDataTransfer`. To confirm in the sandbox: the request and transfer body shapes, the checksum (MD5 of the plain bundle), and whether the push needs the gateway bearer token (not sent unless `Abdm:DataPushSendsGatewayToken` is `true`: the push goes to another facility). The push client follows no redirect and connects only to a public address, checked after DNS.
- **M3** `AbdmHiuService`, `AbdmHiuController` (`abdm/hiu/*`), `AbdmHiuDataController` (`abdm-hiu/data-push/{token}`): consent init → on-init → notify → on-notify + fetch → on-fetch → health-information request → on-request → data push → notify. Received bundles are stored in `AbdmReceivedRecord` until the consent's `dataEraseAt`, and removed on revocation; `AbdmHiuSweepBackgroundService` erases past-date records hourly for every hospital and closes transfers that received nothing within 24 hours (key and push address discarded). The data push address is per transfer and does not contain the bridge secret.
- These were not built on 2026-11-17 and are now (see the last section): the two status calls, `link/context/notify`, `sms/notify2`, paging when sending, and entries delivered by `link`.

## Gaps closed for the licence review (2026-11-18, compiled and unit tested; not yet run on the sandbox)

Built against ABDM's own M1 case list (`.claude/skills/abdm-m1/references/test.md`) and the M2 / M3 operation lists. 237 ABDM unit tests pass.

**M1**
- **Verify by ABHA address** (VRFY_ABHA_102 Aadhaar OTP, VRFY_ABHA_202 mobile OTP): `/phr/web/login/abha/search` → `request/otp` → `verify` → `profile/abha-profile`, scope `abha-address-login`. `AbdmLoginRules` holds the scope and parsing; the desk picks where the OTP goes. An address with no ABHA number behind it verifies but cannot be saved, and says so.
- **ABHA number by Aadhaar OTP** (VRFY_ABHA_101): the request scope now follows where the OTP goes (it was always `mobile-verify` for an ABHA number), and the screen offers the choice.
- **Aadhaar check digit** (CRT_ABHA_104, VRFY_ABHA_401): Verhoeff, in the browser and again on the server, before any call.
- **Resend OTP on the verify screen** (VRFY_ABHA_305): at most 2, 60 seconds apart, as on the create screen.
- **"Just scanned at the counter" on the verify screen** now saves: `accounts/link` accepts `profileShareId` and reads the profile from our own record of the share (hospital-scoped), since a scan has no OTP proof.

**M2**
- **SMS after linking**: `link/patient/links/sms/notify2` once a hospital-initiated link is confirmed, with the patient's mobile and the facility name. `Abdm:SendLinkSms=false` turns it off. A refusal never undoes the link.
- **A record added after linking**: `link/context/notify` with only the new record types. Checked hourly for care contexts linked in the last 90 days (`NotifyChangedSweepAsync`), and `NotifyChangedAsync` for one.
- **Pages**: a transfer is sent as numbered pages of at most 20 records or about 4 MB, each with the same key material; a page the HIU refuses fails the transfer and ABDM is told.

**M3**
- **Where a request stands**: `consent/v3/request/status` (answer on `hiu/consent/request/on-status`) and `GET health-information/request/status/{transactionId}`, behind `POST abdm/hiu/consents/{id}/check` and a "Check with ABDM" button. An approval whose notice was lost is picked up, and its records are asked for once.
- **A record sent as a link** is fetched (public addresses only, no redirects, 25 MB limit) and decrypted like an inline one.

**Checked against ABDM's operation pages on 2026-11-18** (docs.abdm.gov.in, each operation's `.md` page)
- ABHA address login: the verify answer carries `authResult`, `users[]` (`abhaAddress`, `fullName`, `abhaNumber`, `status`, `kycStatus`) and `tokens.token`; an expired OTP is also a 200, told by `authResult`. `abha-profile` returns `abhaAddress`, `fullName`, `abhaNumber`, `dateOfBirth` (and day / month / year), `gender`, `mobile`, `email`. The code reads exactly these.
- `link/context/notify`: `careContext.patientReference` is "a patient identifier with which patient is registered in the facility", so our patient id is right (the example's ABHA address is a placeholder).
- `sms/notify2` takes no `X-HIP-ID`, as sent.
- `on-status`: `consentRequest.id` and `consentRequest.status`, as read. **Its statuses are REQUESTED, DENIED, EXPIRED and REVOKED only: no approval and no artefact ids.** So a status check recovers a refusal, expiry or withdrawal whose notice was lost, but not an approval; a GRANTED with no artefact is ignored.
- From the portal's "Build with AI" rules: an ABHA number is encrypted with its dashes (the login, deactivate and reactivate calls now add them when typed without); error codes are compared with or without the trailing ": " (`AbdmHttp.NormaliseCode`, already so); the callback URL and credentials are one set for the bridge, not per hospital (already so); no number that passes the Aadhaar check digit is written in a test (the tests work one out).

**Patient-initiated linking and the data transfer, checked against ABDM's pages on 2026-11-18** (compiled and unit tested)
- **Bug fixed: the confirm OTP.** ABDM documents `confirmation.token` as an integer. We read it only as text, so every OTP would have been refused. It is now read either way and padded to six digits; our OTP never starts with 0, because a number cannot carry a leading zero.
- `communicationHint` is now `XXXXXX3210`: ABDM's pattern for the field has no asterisk.
- "No patient here" is answered with `ABDM-1001` (No data found), not `ABDM-1010`, which ABDM publishes as "Validation failed". The other codes match their published messages: 1012, 1017, 1057, 1056, 1036, 1004.
- `on-discover` is the one HIP answer whose page names `X-HIU-ID` as the facility header. It now carries the facility id as both `X-HIP-ID` and `X-HIU-ID`.
- An ABHA address is `^[a-zA-Z0-9][a-zA-Z0-9_.]+[a-zA-Z0-9]@(abdm|sbx)$`; the desk and the server now use exactly this.
- The data push sends the gateway token by default (`Abdm:DataPushSendsGatewayToken`, set to `false` to stop): the transfer page says the token is mandatory.
- As HIU, a transfer ends at page n of n, or at its only page whichever way that is numbered (ABDM's example is 0 of 1). n-1 of n is not taken as the end: from a sender counting from 1, ours included, it is the page before the last. A sender counting from 0 over several pages leaves the transfer open; the hourly sweep closes it after 24 hours as Transferred, keeping its records (ABDM is not sent the data-received notice in that case).
- Confirmed as built, no change: the discover, init, health-information request, on-request, transfer and notify bodies; the checksum is MD5 of the bundle before encryption; curve and parameters strings.

**Still to confirm on the sandbox**
- Whether the transfer's `pageNumber` starts at 0 or 1. We send from 1; ABDM's only example is `0` of `1`.
- `sms/notify2`: whether `phoneNo` wants ten digits (sent) or a +91 form. The page says only "the mobile number".
- Everything end to end: none of this has been run against the sandbox.

**Still not built**: OPD consultation note and immunisation record as their own record types (prescriptions, lab reports, discharge summaries and vitals are).
