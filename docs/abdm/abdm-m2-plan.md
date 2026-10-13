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
