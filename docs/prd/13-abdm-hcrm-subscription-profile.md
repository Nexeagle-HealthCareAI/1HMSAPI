# 13 — ABHA/ABDM, HCRM, Subscription, My Profile & Alerts

> Back to [index](README.md). Source: `features/abdm/*`, `features/leads/*`, `features/subscription/*`, `features/profile/*`, `features/alerts/*`; API `AbdmController`, `AbdmCallbackController`, `LeadsController`, `SubscriptionController`, `UserController`, `AlertsController`, `PublicController`.

---

## 13.1 ABHA / ABDM — `/abdm`

**Roles:** Admin, AdminDoctor, Receptionist (`abdm`). **Scope:** ABDM **Milestone 1 (M1)** — ABHA creation/linking/management and facility-QR scan-and-share. Live-sandbox verified 2026-08-04. M2 (HIP) / M3 (HIU, consent manager, health-record exchange) are **not built**. **Readiness:** ✅ for M1.

### Page layout
Header action cards — **Create New ABHA** (Aadhaar + mobile enrolment), **Link Existing ABHA**, **Reactivate ABHA**; an **ABDM guide** panel; a **Counter scan panel**; and the **ABHA accounts table** (Patient details · ABHA details · Contact · Source · Added on) with edit and **remove from hospital records**.

### Functional requirements
| ID | Requirement |
|---|---|
| ABDM-1 | **Create ABHA wizard:** show the **NHA-published Aadhaar consent declaration** (CRT_ABHA_102, mandatory) with explicit acceptance → Aadhaar OTP generate/verify → mobile verification (if the Aadhaar-linked number differs, a separate OTP with a *new* transaction id carried forward) → **ABHA address** suggestions + create → profile. |
| ABDM-2 | **OTP rules:** max **2 resends** per OTP step, each gated by a **60 s cooldown** (CRT_ABHA_106, mandatory). The OTP input uses the completed value passed by `onComplete` (not stale state). |
| ABDM-3 | **Link existing ABHA** by ABHA number/address with login OTP (`login/generate-otp`, `verify-otp`) and `accounts/link` to the hospital record/patient. |
| ABDM-4 | **Edit profile** (mobile and email with OTP), **view QR / ABHA card** (`profile/qr-code`, `profile/abha-card`), **deactivate/reactivate** with OTP. |
| ABDM-5 | **Counter scan (facility QR scan & share):** each counter has a QR; a patient scans with the ABHA app and shares their profile; the callback (`abdm-callback/{secret}/v3/hip/patient/profile/share`) lands in a **profile-shares** list for that counter (counter id persisted in localStorage); staff **handle** a share (creates/links a patient) and the account list refreshes. |
| ABDM-6 | **ABHA as an identity key:** the patient search matches ABHA; an exact ABHA match is the **top-confidence duplicate signal** at registration (appointment and admission). |
| ABDM-7 | Removing an account removes it from *hospital records only* (not from ABDM) and states so in the confirm dialog. |
| ABDM-8 | Facility registration (`abdm/facility`, `bridge/register`) is configured per hospital by Admin. |

### Compliance / security requirements
- Aadhaar numbers/OTP are **never stored or logged**; transaction ids are short-lived; consent text is stored with timestamp and version for each enrolment (**verify against NHA's current wording before go-live** — flagged in code).
- The callback URL contains a **shared secret** — rotate, rate-limit, restrict by source IP/signature if NHA provides one.
- ABDM credentials (client id/secret) are server-side only; the web app calls only `abdm/*` endpoints with the user's JWT.
- Dates from the API are naive UTC (append `Z`).

### Gaps
| Pri | Gap |
|---|---|
| P1 | **ABHA verification during registration/admission** inline (verify an ABHA number by OTP/QR without leaving the form) — today the booking form shows Linked/Not-linked with a link here. |
| P1 | **HIP linking & health-record push** (M2): prescriptions, discharge summaries, lab reports as FHIR bundles; care-context linking; **HIU/consent manager** (M3). Required for any "ABDM-enabled" claim beyond M1 and for PM-JAY. |
| P1 | Sandbox → **production ABDM** onboarding and go-live checklist (facility registration in HFR, certificate/credentials rotation). |
| P2 | Bulk ABHA creation at camps; ABHA-based **scan-and-share token** at OPD kiosk. |

---

## 13.2 HCRM (Lead generation) — `/leads`

**Roles:** Admin, AdminDoctor (`leads`). **Purpose.** "Turn every doctor search, WhatsApp enquiry and social visit into a followed-up patient." **Readiness:** 🟡 (visibility only; no follow-up workflow)

| ID | Requirement |
|---|---|
| HC-1 | Table of hospital leads (`GET leads?hospitalId=`), paged, newest first: time (IST), **source** (Doctor Dekho / WhatsApp), **lead type** (doctor-name search, hospital-name search, doctor-profile view, hospital-page view), doctor (if any), search query, patient name, mobile, city/region/country. |
| HC-2 | Filters: source, lead type, date window; **count tiles** by source/type (respect the date window but not the source/type filters — those *are* the breakdown); Total / WhatsApp / Doctor Dekho tiles; refresh. |
| HC-3 | Loading, empty and error states with retry. |
| HC-4 | PII: mobile numbers visible only to Admin roles; export logs. |

### Gaps (the product copy itself lists these as roadmap)
| Pri | Gap |
|---|---|
| P1 | **Follow-up workflow:** lead status (new/contacted/booked/lost), owner, notes, next-action date, click-to-call/WhatsApp, **convert lead → appointment/patient**, and attribution of bookings back to the lead source (so "what actually drives bookings" is measurable). |
| P1 | Duplicate/merge leads by mobile; consent for outreach (TRAI/DND, WhatsApp opt-in). |
| P2 | Meta/Instagram campaign connection, AI lead scoring, campaign ROI. |

---

## 13.3 Subscription — `/subscription`

**Roles:** Admin, AdminDoctor (`admin_panel`). **Readiness:** ✅ (manual-payment flow); 🟡 for online payment.

| ID | Requirement |
|---|---|
| SUB-1 | Show current plan, **status** (`Trial` / `Active` / `Expired` / `Blocked` / `Rejected` / `Pending` / `PendingApproval`), validity and **usage** (`subscription/{hospitalId}/usage`) against the pooled monthly free-tier quota (IPD/OPD/pathology/pharmacy actions). A real countdown only — the fabricated trial days-remaining was removed. |
| SUB-2 | **Plan grid** priced on two axes — **team size** and **bed capacity** — grouped by team size; **Monthly/Quarterly/Yearly** toggle shown only when a second cycle exists in the catalog (sorted by real duration); shared features hoisted into one line, cards list only extras; the middle-priced tier is highlighted. |
| SUB-3 | Selecting a plan opens a **review drawer** and writes nothing until payment is submitted. Fresh/trial: amount = plan price. **Mid-cycle switch:** amount = new price − credit for unused days of the current plan (pro-rated quote); a full credit requires no bank reference. |
| SUB-4 | **Manual payment submission** (`submit-payment`): payment mode + bank reference (proof-of-payment upload is a gap); status **PendingApproval** until the platform CMS approves; history list (`payment-history`) shows each payment's status (PendingApproval / Approved / Rejected). |
| SUB-5 | **Referral codes:** entered at registration/plan purchase; reward (%-off or extra months) lands only on **Yearly** plan approval; single-use globally; `ExtraMonths` is informational until approval. |
| SUB-6 | **Enforcement:** Expired/Blocked → API rejects every non-GET; UI shows read-only banner, disables writes and opens the upsell modal; seats/roles constrained (only Doctor/AdminDoctor can still be onboarded). Approaching expiry shows a banner; **free-tier usage badge** and per-feature usage-limit badges show remaining quota. |
| SUB-7 | Invoices/receipts for subscription payments with GST. |

### Gaps
| Pri | Gap |
|---|---|
| P1 | **Online payment gateway** (UPI/card/netbanking) with automatic approval and failed-payment handling; payment proof upload and approval SLA notification to the hospital. |
| P1 | **Usage-based Phase 2** (masking beyond quota) not built per project notes — define behaviour exactly at/after quota exhaustion (block vs mask vs allow-and-bill). |
| P1 | Grace period and dunning (reminders at 15/7/1 days, WhatsApp/email) before Expired; downgrade with data-retention rules; GST invoice PDF download. |
| P1 | A recent defect (`Forbid(string)` misusing an auth scheme) caused 500s on select-plan/submit-payment — add API tests for the 4xx paths. |

---

## 13.4 My Profile — `/profile`

**Roles:** any authenticated user. **Readiness:** ✅ (🟡 minor)
| ID | Requirement |
|---|---|
| PRF-1 | View/edit personal details (name, contact, email, address incl. city/state — **VERIFY** the exact field set), **profile picture** upload/remove (`user/profile-picture/*`; image preview, revert on cancel, update on save), with success dialog "Profile updated". |
| PRF-2 | **Change password** is independent of the edit toggle; new password strength rules; confirm match; success message; other sessions invalidated (**VERIFY**). |
| PRF-3 | For doctors: **Professional** section auto-expands with `?tab=professional` / `?focus=doctor` (department, specialisations, qualifications, licence, council, registration year, experience, bio — see 3.3) and shows **profile completion**. |
| PRF-4 | Profile completion % and banner drive the Verified badge/lock rules (`ProfileCompletionBanner`). |
| Gap (P1) | Two `TODO: Move setup data to Zustand store / Save to Zustand store instead of localStorage` markers — settle where profile completion/setup state lives; avoid stale localStorage driving locks. |
| Gap (P1) | Notification preferences (WhatsApp/email/in-app per category), language preference persisted server-side, active sessions/devices list with sign-out-all. |

---

## 13.5 Alerts bell (header) — all roles

**Purpose.** In-app notification centre fed by the **alert engine** (`alerts`): admission alerts (deposit low, EDD breach, consent pending), expiry alerts (90/60/30 days), missed doses, critical values, rapid response, etc. **Readiness:** 🟡

| ID | Requirement |
|---|---|
| ALR-1 | Bell with unread count (`alerts/counts`), list sorted by severity (**CRITICAL** > WARNING > INFO) then time; severity icon/tone; critical alerts visually distinct. |
| ALR-2 | Actions per alert: **Acknowledge**, **Dismiss**, **Snooze (1 h)**; toasts confirm; failures are **silent** by design (the bell never pops errors). |
| ALR-3 | Alerts are hospital-scoped and role-targeted (a nurse sees ward alerts, not billing alerts); de-duplicated per entity+code (e.g. expiry per batch+code). |
| ALR-4 | Evaluators run on schedule (`alerts/evaluate`, `evaluate-expiry`; daily `ExpiryAlertBackgroundService`) and the admission evaluator on demand. |
| Gap (P1) | **Delivery channels:** in-app only on this surface; push (web push/FCM), SMS/WhatsApp for CRITICAL, escalation if unacknowledged within N minutes. |
| Gap (P1) | No alert **history/settings page** (per-category on/off, quiet hours, recipients). |
| Gap (P1) | `AlertsController` carries no `[RequiresPermission]` (B-4) — a user can read/dismiss any alert in the hospital by ID. |
