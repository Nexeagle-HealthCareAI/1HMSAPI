# 1HMS (EasyHMS) — Production Requirements Document (PRD)

> **Status:** Draft v1.0 · 2026-10-02 · Based on a code review of `easyHMSWeb`, `easyHMSAPI`, `easyHMSDatabase` as of branch `develop` (HEAD `aafa159`).
> **Audience:** Product, engineering, QA, hospital-onboarding/support.
> **What this is:** For every page the web app currently routes, what it must do in production, what the code does today, and what is still missing. It is a *requirements* document — each page lists testable requirements and a gap list, not a design.
> **What this is not:** A runtime test report. Everything marked "built" was verified by reading code and routes; nothing here was exercised against a live environment. Items needing runtime confirmation are labelled **VERIFY**.

---

## 1. How to read this PRD

| File | Modules / pages covered |
|---|---|
| [01-auth-access-shell.md](01-auth-access-shell.md) | Login (password/OTP/magic-link), registration, forgot password, role routing, app shell/navigation, 404, locked-account, language |
| [02-appointments-opd.md](02-appointments-opd.md) | Appointment Dashboard, Booking, Confirm online booking, Reschedule/Cancel, Vitals, Token print, Token view, **Appointment Oversight** |
| [03-doctor-board-calendar.md](03-doctor-board-calendar.md) | Doctor Board, Doctor Analytics, Doctor Profile, Availability Roster, Doctor Calendar, Doc AI |
| [04-patients-prescription.md](04-patients-prescription.md) | Patients (Today/Upcoming/Past/360/Referrers), Patient Profile, E-Prescription Pad, InkRx, Voice Rx, Attachments, Merge, Prescription verify |
| [05-billing.md](05-billing.md) | Billing Dashboard (Revenue/Expense/Analytics/Approvals), Patient Ledger, Encounter Billing, Day-wise bills, Print preview |
| [06-ipd-admission-beds-discharge.md](06-ipd-admission-beds-discharge.md) | IPD Dashboard, Admit wizard, Referred Admissions, Bed Board, KPI dashboard, Consultant Ledger/Incentives, Discharge |
| [07-ipd-clinical-workspace.md](07-ipd-clinical-workspace.md) | Patient Workspace: CPOE, MAR, Vitals/EWS, I/O & Glucose, Nursing, Round notes, SBAR, Consent, Documents, Blood bank, Surgery, Critical care |
| [08-ot-icu-nursing-boards.md](08-ot-icu-nursing-boards.md) | OT Board, ICU Board, Nursing Station |
| [09-inventory-pharmacy.md](09-inventory-pharmacy.md) | Inventory board (stock, procurement, CSSD, blood bank, narcotics, equipment), Pharmacy Retail (POS, returns, RTV, analytics, compliance) |
| [10-pathology.md](10-pathology.md) | Pathology Lab: Workspace, New Order, Order Detail/Results, Billing, Catalog, Keywords, Lab Settings, Letterhead |
| [11-admin-configuration.md](11-admin-configuration.md) | Admin Panel, User Management, Hospital Info, Configuration masters, Chain management, Settings |
| [12-hr-suite.md](12-hr-suite.md) | 1HR Suite: Overview, Staff, Roster, Leave, Attendance, Devices (ZKTeco), Payroll, Licenses |
| [13-abdm-hcrm-subscription-profile.md](13-abdm-hcrm-subscription-profile.md) | ABHA/ABDM, HCRM (Leads), Subscription, My Profile, Alerts bell |

Each page section uses the same structure:

1. **Purpose & route / access**
2. **Readiness** (legend below)
3. **Functional requirements** — numbered `PREFIX-n`, written as testable statements ("The system shall…")
4. **Business rules & data**
5. **Gaps to close before production** — each tagged P0/P1/P2

### Readiness legend

| Mark | Meaning |
|---|---|
| ✅ | Implemented and API-backed per code review |
| 🟡 | Implemented, with known gaps that must be closed (listed) |
| 🔴 | Prototype / mock / dead — **must not ship as-is** |
| ⚪ | Present in the repo but not reachable (unrouted) — decide: finish or delete |

### Priority legend

| Tag | Meaning |
|---|---|
| **P0** | Release blocker — go-live must not happen with this open |
| **P1** | Required within the first release cycle; has a workaround |
| **P2** | Post-launch improvement |

---

## 2. Platform summary

1HMS is a multi-tenant hospital management platform for Indian hospitals/clinics: a React/Vite PWA (`easyHMSWeb`) over a .NET API (`easyHMSAPI`, CQRS/MediatR, EF Core) and SQL Server (`easyHMSDatabase`). Companion systems (CMS admin, Doctor Dekho public site, WhatsApp bot, NLP symptom router, Vita voice assistant, 1Rad) integrate through the public API and are **out of scope** of this PRD except where a page depends on them.

**Scope decision (carried from `PLAN.md`):** Radiology is out of scope for EasyHMS; it is a separate product with a REST hand-off.

### 2.1 Routed pages inventory (authoritative — from `AppRoutes.tsx` + `boardAccess.ts`)

| Route | Page | Permission key | PRD file |
|---|---|---|---|
| `/login`, `/magic-login` | Login / one-tap link | public | 01 |
| `/404`, `*` | Not found | public | 01 |
| `/token-view` | Digital token view | public | 02 |
| `/verify/:appointmentId` | Prescription verification | public | 04 |
| `/` | Role-based redirect | authenticated | 01 |
| `/appointment-dashboard` | Appointment Dashboard | `appointment_scheduler` | 02 |
| `/appointment-booking` | Appointment Booking | `appointment_booking` | 02 |
| `/appointment-oversight` | Appointment Oversight | `appointment_scheduler` | 02 |
| `/dashboard` | Doctor Board | `doc_board` | 03 |
| `/calendar` | Doctor Calendar | `doctor_calendar` | 03 |
| `/doc-ai` | Doc AI | `doc_board` | 03 |
| `/patients` | Patients | `patients` | 04 |
| `/patient/:patientId`, `/patient/new` | Patient Profile / Prescription workspace | `patients` | 04 |
| `/billing`, `/billing/ledger`, `/billing/:appointmentId`, `/billing/encounter/:encounterId` | Billing | `billing` | 05 |
| `/print-preview` | Print preview | `print_preview` | 05 |
| `/ipd-workspace` | IPD (dashboard, bed board, referrals, KPI, ledger, CSSD) | `ipd` | 06 |
| `/ipd-workspace/patient/:id` | IPD Patient Workspace | `ipd` | 07 |
| `/ot-board` | OT Board | `ot_board` | 08 |
| `/icu-board` | ICU Board | `icu_board` | 08 |
| `/nursing-station` | Nursing Station | `nursing_station` | 08 |
| `/inventory` | Inventory Management | `inventory` | 09 |
| `/pharmacy-retail` | Pharmacy Retail | `pharmacy` | 09 |
| `/pathology`, `/pathology/orders/:orderId` | Pathology Lab / Order detail | `pathology` | 10 |
| `/admin` | Admin Panel (dashboard, AI forecast, users, patients, settings) | `admin_panel` | 11 |
| `/configuration` | Hospital Info / Configuration masters | `admin_panel` | 11 |
| `/settings` | Hospital Info (settings page) | `admin_panel` | 11 |
| `/chain` | Chain management | `admin_panel` | 11 |
| `/hr` | 1HR Suite | **none defined** (see §3 B-7) | 12 |
| `/abdm` | ABHA / ABDM | `abdm` | 13 |
| `/leads` | HCRM | `leads` | 13 |
| `/subscription` | Subscription | `admin_panel` | 13 |
| `/profile` | My Profile | any authenticated | 13 |

Dev-only preview routes (`/ipd-preview`, `/bedboard-preview`, `/kpi-preview`, `/admit-preview`, `/workspace-preview`, `/ledger-preview`, `/referrals-preview`, `/billing-preview`, `/billing-ledger-preview`, `/admin-preview`, `/docboard-preview`, `/rx-preview`, `/ipd-mobile-review`) are gated by `import.meta.env.DEV` and are not part of the production surface (see XC-12).

### 2.2 Role → board matrix (from `seed_global_min.sql`)

| Permission key | Admin | AdminDoctor | Receptionist | Nurse | Doctor | Accountant | Lab Technician | Pharmacist | Coordinator |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| admin_panel | ✔ | ✔ | | | | | | | |
| appointment_scheduler | ✔ | ✔ | ✔ | ✔ | | | | | |
| appointment_booking | ✔ | ✔ | ✔ | ✔ | | | | | |
| billing | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | | | |
| doc_board | | ✔ | | | ✔ | | | | |
| ipd | ✔ | ✔ | | ✔ | ✔ | | | | ✔ |
| nursing_station | ✔ | ✔ | | ✔ | ✔ | | | | |
| ot_board | ✔ | ✔ | | ✔ | ✔ | | | | ✔ |
| icu_board | ✔ | ✔ | | ✔ | ✔ | | | | ✔ |
| inventory | ✔ | ✔ | | ✔ | ✔ | | | | ✔ |
| pathology | ✔ | ✔ | | | ✔ | | ✔ | | |
| pharmacy | ✔ | ✔ | | | ✔ | | | ✔ | |
| patients | ✔ | ✔ | | | ✔ | | | | |
| doctor_calendar | ✔ | ✔ | ✔ | | ✔ | | | | |
| abdm | ✔ | ✔ | ✔ | | | | | | |
| leads | ✔ | ✔ | | | | | | | |
| print_preview | ✔ | ✔ | ✔ | ✔ | | ✔ | ✔ | ✔ | ✔ |

> Observations: Receptionist has no `patients` permission yet registers patients through booking; Nurse holds `appointment_*` and `billing` but not `patients`; Doctor holds `billing` but **not** `print_preview`; the Doctor role was deliberately kept broad ("explicit product decision" in the seed). These are product decisions to re-confirm in XC-3, not defects.

---

## 3. Release-blocker register

Findings from this review that are verified in code and must be resolved or explicitly accepted before go-live. Each is cross-referenced in the owning page file.

| ID | Pri | Finding | Evidence | Owner file |
|---|---|---|---|---|
| **B-1** | P0 | **Pharmacist, Lab Technician and Coordinator cannot use the web app.** After login they fall into the "default" branch (→ `/appointment-dashboard`, which they have no permission for) and the `/` route's `RoleBasedRedirect` calls `logout()` for any role other than Admin/Doctor/AdminDoctor/Receptionist/Nurse/Accountant. The roles exist in the seed with boards assigned. **VERIFY** at runtime. | `AppRoutes.tsx:17-36`, `LoginPage.tsx:60-96`, `seed_global_min.sql:120-250` | 01 |
| **B-2** | P0 | **`/appointment-oversight` runs on hard-coded sample data** (fictional doctors, 2024 dates, in-memory state) yet is routed under `appointment_scheduler`. Any user with the permission sees fake appointments. | `AppointmentOversight.tsx:63-120` | 02 |
| **B-3** | P0 | **`/doc-ai` returns canned strings** (`generateAIResponse` keyword switch behind a `setTimeout`). Presented to doctors as a "medical assistant" — a patient-safety/liability risk. | `DocAI.tsx:70-105` | 03 |
| **B-4** | P0 | **Server-side authorization is fail-open on most clinical controllers.** About 50 controllers carry no `[RequiresPermission]` (a handful — public, ZKTeco push, ABDM callback, health — are legitimately anonymous). `PermissionAuthorizationFilter` only enforces where the attribute is present, so any authenticated user of the hospital can call the rest regardless of role. Affected clinical controllers (MAR, vitals, round notes, restraint, nursing assessment/care plan, shift handover, blood bank, CSSD, equipment, devices, discharge summary, consent, IRDAI, narcotics, fluid, glucose, EWS, rapid response, infection, KPI, consultant incentive, admission referral, alerts, e-prescription, prescription settings, doctor fees, referrers, hospitals, departments, doctors, user, subscription) are among them. | `endpoints.txt` extract; `PermissionAuthorizationFilter.cs` doc comment | XC-3 |
| **B-5** | P0 | **Lockout is client-side only** (`localStorage.accountLockout`, client `checkRateLimit`). Trivially bypassed by clearing storage or calling the API directly. Needs server-side throttling/lockout + audit. **VERIFY** whether the API already throttles. | `SecureLogin.tsx:158-205, 693`, `LockedAccountScreen.tsx` | 01 |
| **B-6** | P0 | **PHI/auth-state debug logging in production bundle.** 171 `console.log` calls in `src`, including `[LOGIN-DEBUG]` lines in `RouteGuard` and `LoginPage` (role, path, auth state) and OTP-send responses in `SecureLogin`. | `grep console.log` | XC-9 |
| **B-7** | P0 | **`/hr` has no board-access rule and its API permission keys are not seeded.** `getRequiredPermissions('/hr')` returns `undefined`, so the route/nav is unrestricted client-side; the API requires `hr.manage_employees` / `hr.view_dashboard`, which do not appear in `seed_global_min.sql` or any `easyHMSDatabase` seed searched. Net effect: either everyone sees a page that always 403s, or HR is accessible only if keys were inserted by hand. | `boardAccess.ts`, `HrController.cs`, `HrBiometricController.cs` | 12 |
| **B-16** | P0 | **Payroll under-pays and fails silently.** Salaried payable days count only `PRESENT/LATE/HALF_DAY` — paid leave, weekly offs and holidays are not payable; employees without a salary structure are skipped with a `Console.WriteLine` and no payslip while the run reports success; TDS is a simplified slab; loan deduction is a TODO; night shift keyed on hard-coded `SFT_N`. | `SalariedPayrollStrategy.cs`, `RunMonthlyPayrollHandler.cs` | 12 |
| **B-8** | P1 | **Nursing shift definitions are stored in browser `localStorage`** (`easyhms_shifts_<hospitalId>`), with a fake 200–300 ms delay. Shifts differ per browser/device and vanish on cache clear, while nurse-roster assignments are server-side and reference shift codes. | `ipd-redesign/services/shiftApi.ts` | 08 |
| **B-9** | P1 | **Discharge-summary custom field values are stored only in `localStorage`** (`discharge-custom-fields:<admissionId>`) — not in the legal discharge document. | `DischargeSummaryPanel.tsx:115-124` | 06 |
| **B-10** | P1 | **Doctor Analytics trend data is placeholder:** every bucket returns `trend: 'neutral'`, `percentageChange: 0`; new/returning uses "fallback values". | `doctorAnalyticsApi.ts:150-200` | 03 |
| **B-11** | P1 | **Localization covers OPD/auth/admin only** (EN+HI). IPD, billing (9 keys), pathology, pharmacy, OT, ICU, HR, ABDM have no i18n keys; `navigation` and `analytics` sections exist in EN with 0 HI counterparts. | `i18n/locales/*.json` | XC-8 |
| **B-12** | P1 | **Automated test coverage on the web client is minimal:** 3 unit-test files and 2 Playwright specs (`doctor-fees`, `default-letterhead`) for ~160k lines. API has 261 test files. No e2e for login, booking, billing, admit, discharge, pharmacy checkout. | `find *.test.*`, `e2e/` | XC-10 |
| **B-13** | P2 | Dead/unrouted code with mock data: `DashboardOverview.tsx` (TODO mock stats), `PatientFlow.tsx` (empty mock arrays), `PatientProfile.tsx` ("for now we'll just log it" vitals save), `DischargeFlow/NurseDashboard/DoctorDashboard` screens, `PatientSimulator.tsx`, mock timeline/patient list utils. No import found for several. Delete or finish. | greps | per file |
| **B-14** | P2 | Billing Configuration "Duplicate Item" is a no-op (`TODO: Call create API`). | `BillingConfiguration.tsx:214` | 05 |
| **B-15** | P2 | Doctor gamification XP/streak is stored in `localStorage` (`doctor_xp`) — per-browser, not authoritative, not tied to any real metric. Decide whether the feature stays. | `useGamification.ts` | 03 |

---

### 3a. Remediation log — 2026-10-02

Work done against the register above. **Code changes are uncommitted in the working trees** (web: `easyHMSWeb`; API: `easyHMSAPI`; seed: `easyHMSDatabase`); nothing is deployed. Items marked *needs DB/product* were deliberately not changed.

| ID | Status | What changed |
|---|---|---|
| B-1 | **Fixed** | New `config/landing.ts` resolves the landing page from granted permissions (all 9 seeded roles + custom roles); `LoginPage`, `RoleBasedRedirect` and `RoleService.getRedirectPath` use it. A user with no permitted board now sees a "No access assigned" screen (`NoAccessPage`) instead of being silently logged out. Unit test added (`landing.test.ts`). |
| B-2 | **Fixed** | `/appointment-oversight` route, nav rule and `AppointmentOversight.tsx` removed. |
| B-3 | **Fixed** | `/doc-ai` route, nav rule and `DocAI.tsx` removed. |
| B-4 | **Mostly fixed** | `[RequiresPermission]` added to 26 clinical/inventory controllers (MAR, vitals, round notes, restraint, nursing assessment/care plan, handover, fluid, glucose, EWS, rapid response, infection, devices, device bundles, consent record/template, implant log, order sets, IPD KPI, admission referral, IRDAI, consultant incentive, blood bank, CSSD, equipment, narcotics) using OR-sets of the boards that legitimately use them. **Still unannotated (shared by many roles — need a per-endpoint review):** e-prescription (now has one annotated action), alerts, doctor fees, referrers, hospitals, departments, doctors, user, subscription, discharge-settings, prescription settings, translation, patient-profile. |
| B-5 | **Fixed (server side)** | Password login now locks the account after `Auth:MaxFailedLoginAttempts` (default 5) failures — the `FailedLoginAttempts`/`IsLocked` columns existed but were never enforced for password sign-in; unlocking is via Forgot-Password OTP. New per-IP `StaffAuthPolicy` rate limit (30/min) on `user/login`, `otp/send`, `otp/verify`. |
| B-6 | **Fixed** | All `[LOGIN-DEBUG]` statements removed; production build now strips `console.log/info/debug` (`terser pure_funcs`) and `debugger`. The hard-coded `aquib@gmail.com` offline-mock login is now compiled out of production (`import.meta.env.DEV`). |
| B-7 | **Fixed (needs seed run)** | `hr` added to `BOARD_ACCESS` with `hr.manage_employees` + `hr.view_dashboard`; `seed_global_min.sql` now grants both to Admin and AdminDoctor. **The seed must be applied before the HR page is usable.** |
| B-10 | **Fixed** | Doctor Analytics no longer fabricates `trend: 'neutral'` / `+0%`; the badge is hidden until the API returns a real comparison. (The no-show/cancelled "same count in every bucket" mapping still needs an API change.) |
| B-13 | **Fixed** | 13 unreferenced files deleted (`PatientProfile`, `PatientFlow`, `PatientSimulator`, `DashboardOverview`, IPD `IncentivesScreen`/`AutoBillingConfig`/`DischargeFlow`/`NurseDashboard`/`DoctorDashboard`/`NursingStationScreen`, mock patient/timeline/complaint data). |
| B-16 | **Fixed (policy now configurable)** | Approved paid leave (`ON_LEAVE`) is payable; **weekly offs and declared holidays are payable by default and configurable per hospital** (`hr/payroll-settings`: weekly-off days + two payable switches; `hr/holidays` calendar; tables `HrPayrollSettings`, `HrHolidays`); nothing is paid before the joining date; employees skipped in a run are returned in `SkippedEmployees` and named in the message. Pure calculator `PayableDayCalculator` with 7 unit tests. Configured from a new **Weekly offs & holidays** card under HR → Payroll (weekday toggles, two "paid" switches, holiday calendar). Still open: statutory TDS, loan ledger, configurable PF/ESI/PT rates, `SFT_N` night-shift keying. |
| Interaction/allergy check | **Built (advisory, optional acknowledgement)** | *Correction to the earlier finding:* the database already had `PatientAllergy` and `DrugInteraction` tables plus a starter seed; only the API/UI side was missing. Added entities, `POST e-prescription/safety-check` (structured + free-text profile allergies + pair interactions; fails safe with `checked=false`), a warning banner in the prescription pad and IPD medication-order dialog, and tests. **Per product decision the override reason is optional:** the clinician can *Acknowledge & continue* with or without a reason; the acknowledgement is stored as a private note on the prescription (or in the order notes for IPD). Nothing is hard-blocked. The seed is a starter set, not a licensed database. |
| Also found & fixed | — | `EPrescriptionPad` fell back to a hard-coded hospital id when none was resolved; removed (a missing hospital now skips the fetch). |
| B-8 | **Fixed (needs DB script)** | Nursing shifts now live on the server: table `NursingShift`, `GET/PUT nursing-station/shifts` (PUT is admin-only — the permission filter now honours an action-level attribute over the class-level one). `shiftApi` reads/writes the server and **migrates an existing browser-local list once**; offline falls back to defaults. 6 unit tests. |
| B-9 | **Fixed (needs DB script)** | Custom discharge-summary fields persist server-side (table `DischargeSummaryCustomField`, `GET/PUT discharge-summary/custom-fields`), are frozen once the summary is signed, are printed from the saved values, and any local copy is migrated once. A separate table was used deliberately so existing discharge queries are untouched. |
| B-11 | **Partly fixed** | Hindi now has **parity with every existing English key** (537 strings added, mostly Doctor Calendar, Analytics, Admin, Common), plus new `mainNav`, `noAccess`, `safety` sections wired into the sidebar, the no-access screen and the safety banner. A parity test fails the build if a Hindi key goes missing. **Still English-only:** the page bodies of IPD, Billing, Pathology, Pharmacy, OT, ICU, HR, ABDM, Subscription — those screens have no i18n keys at all and need their strings extracted first. |
| B-12 | **Partly fixed** | Added tests: web — landing-page routing, Hindi parity, nursing shifts; API — password hashing (5), login migration + lockout (2), safety check (5), payable-day calculator (7), updated password-handler tests. **No new end-to-end (Playwright) specs** — they need a running stack. The web unit runner cannot start in this sandbox, so the three new web test files were type-checked and traced but not executed. |
| Password storage | **Fixed** | Passwords are now PBKDF2-HMAC-SHA256 (600,000 iterations, per-user random salt, `PasswordHasher`). Legacy unsalted SHA-256 hashes still verify and are **upgraded on the user's next successful sign-in**; all write paths (set/reset/change password, quick-add, reset credentials) store the new format. Remaining: `Login`/OTP endpoints still echo `ex.Message` on 500s. |

**Deployment checklist for these changes** (database scripts are idempotent / guarded; apply **before** the API/web that use them):

| Script (easyHMSDatabase) | Needed by |
|---|---|
| `db/data/seed/seed_global_min.sql` | `/hr` access (grants `hr.manage_employees`, `hr.view_dashboard`) |
| `db/schema/tables/create_tables_medication_safety.sql` + `db/data/seed/seed_drug_interactions.sql` | Allergy / interaction check (it reports "not screened" until applied) |
| `db/schema/tables/create_tables_hr__payroll_settings.sql` | Payroll weekly-off / holiday settings (payroll falls back to the defaults if absent) |
| `db/schema/tables/create_tables_nursing_shift.sql` | Server-side nursing shifts (UI falls back to defaults / local copy if absent) |
| `db/schema/tables/create_tables_discharge_summary_custom_fields.sql` | Persisted discharge custom fields (only that endpoint errors if absent) |

Password hashes need no script — they migrate lazily on login. Optional config: `Auth:MaxFailedLoginAttempts` (default 5).

## 4. Cross-cutting requirements (apply to every page)

These are stated once here; page files reference them as `XC-n` and only list page-specific deviations.

### XC-1 Multi-tenancy & data isolation
- Every read/write shall be scoped to the caller's `HospitalId` server-side (`HospitalAccessFilter`); the client-supplied `hospitalId` is advisory only.
- A user belonging to several hospitals (chain) shall act in exactly one at a time via the header Hospital Switcher; switching shall clear page state and partition the offline cache (already implemented).
- No endpoint addressed by an ID alone (appointment, admission, order, invoice, employee…) may return or mutate another hospital's row. Commit `f9dbf78` fixed this for HR; **all** ID-only endpoints shall be audited (P0 before go-live).

### XC-2 Authentication & session
- Methods: mobile+password, mobile+OTP (WhatsApp, email fallback), one-tap magic link from notifications, demo QR login (demo hospital only).
- JWT lifetime and idle logout per `PLAN.md` NFR: token ≤ 8 h, **30-minute idle logout** (an `inactivity` i18n section exists — **VERIFY** it is wired).
- Server-side brute-force protection with lockout and audit (see B-5). OTP: 6 digits, bounded lifetime, bounded resends with cooldown, single use.
- Password change/reset flows shall invalidate other sessions (**VERIFY**).
- Subscription state shall be enforced server-side: when a hospital is `Expired`/`Blocked`, GETs succeed and all writes are rejected (`HospitalAccessFilter`); the UI mirrors this with `useSubscriptionReadOnly` (disables writes, shows upsell modal, read-only banner).

### XC-3 Authorization (RBAC)
- Boards are gated by `PermissionKey` via `boardAccess.ts` (single source of truth for UI) and `[RequiresPermission]` on the API.
- **Requirement:** every controller that exposes hospital data shall carry `[RequiresPermission(...)]` mapped to the owning board (closes B-4). Fail-open on unannotated endpoints shall be removed after annotation (or replaced by a deny-by-default policy with an explicit `[AllowAnonymous]`/`[Authorize]`-only allow-list).
- Public endpoints (`/public/*`, `/public-*`, `/iclock/*`, `/abdm-callback/*`) shall be rate-limited and shall not expose cross-tenant data; secrets in URLs (ABDM callback secret) shall be rotatable.
- Role changes shall take effect within the permission-cache TTL (60 s today).
- Sensitive operations shall require elevated role *and* be audit-logged: finalize/reopen invoice, discount > threshold, refund, credit approval, discharge sign/unsign, narcotic dispense, user create/deactivate, role change, patient merge, payroll run.

### XC-4 Audit & compliance
- An immutable audit trail (who/what/when/before→after) for all clinical and financial writes (`PLAN.md`: "Audit invisible to user, visible to admin"). A generic audit viewer for Admin is **not built** (the Admin "Audit & Security" nav entry is commented out) — P1.
- Regulatory artefacts already in the product: Schedule H1 register, narcotic register, cold-chain log, consent records with templates, NMC registration capture, ABDM M1 consent text (CRT_ABHA_102) and resend limits (CRT_ABHA_106), GST (HSN/SAC, CGST/SGST) on invoices, pharmacy DL/FSSAI/pharmacist on receipts. The hospital shall be able to export each register for inspection.
- Data protection: PHI encrypted in transit (TLS 1.2+/1.3) and at rest; attachments in MinIO/S3 served via short-lived signed URLs (`RefreshUrlAsync`); no PHI in logs, URLs, or analytics events (see B-6). DPDP-Act consent and retention/erasure policy are **not defined** — P1 product decision.

### XC-5 Time, locale, money
- All user-visible times shall render in IST (`Asia/Kolkata`). Backend timestamps are naive UTC; every screen shall append `Z`/convert (already done page by page — requirement is to centralise in one util to stop the repeated bug class: appointments, billing, ABDM, biometric devices each re-implement it).
- Currency ₹ with `en-IN` grouping; GST-inclusive/exclusive handling per ChargeMaster; rounding rules per Billing Policy.
- Dates sent as `YYYY-MM-DD` for date-only fields; no browser-local date parsing for business dates.

### XC-6 Offline & resilience (PWA)
- Service worker (vite-plugin-pwa, autoUpdate), encrypted IndexedDB query cache (partitioned per hospital), outbox write-queue with FIFO sync (last-write-wins), connectivity heartbeat against `/health`, `OfflineBanner`.
- **Queueable offline:** patient registration/appointment booking (OPD), charge posting to an existing encounter, hospital profile edits (as wired). **Never queued:** payments, refunds, finalize, discharge, narcotic dispense (money/legal actions are online-only).
- Requirement: every queued item shall have a visible pending/failed state with retry and a conflict message; storage-full shall block, never silently drop (implemented in `enqueue`).
- Requirement (P1): define and test the offline matrix per page (what reads from cache, what writes queue) — today it is implicit.

### XC-7 Printing & documents
- Print templates exist for: invoice (A4, 80 mm thermal), receipt (A4, thermal), bill-cum-receipt, interim day-bill, payment statement, admission confirmation, admission token receipt, discharge summary, surgery case, pharmacy receipt (80 mm), pharmacy credit/debit notes, QR posters (hospital, booking, doctor), prescription (letterhead designer), pathology report (letterhead designer), payslip.
- Requirement: print output shall be tested on A4 and 80 mm for long item lists, Indic scripts, and missing optional fields; every document shall carry hospital name/address/GSTIN/registration numbers from Hospital Info.

### XC-8 Localization & accessibility
- English and Hindi are supported (`languageSelector`); Hinglish handled by the NLP router. **Requirement:** all user-visible strings on pages in the production surface shall be i18n keys with EN+HI parity (closes B-11). Print documents remain English unless the letterhead configuration says otherwise.
- Design system: Inter Variable (self-hosted), 44 px base control height, 11 px minimum text, ≥ 44 px tap targets on ward/mobile screens, colour never the only status carrier, dark mode, reduced-motion respected. Open debt: arbitrary font sizes, raw colours vs tokens, spinners-not-skeletons, jargon labels.
- Mobile-first for ward staff (IPD, MAR, vitals, nursing station); desktop-first for billing/admin/HR.

### XC-9 Observability & hygiene
- Strip `console.log/debug` from production builds (esbuild `drop`), keep `console.error` routed to a client error reporter. No tokens/PHI in logs.
- Client error boundary per route; API request-id propagated and shown on error toasts.
- Server: structured logs, health (`/health` exists), metrics for p95 latency per endpoint, background-job heartbeat (expiry alert service, night jobs).

### XC-10 Quality gates
- Performance (from `PLAN.md`): page load < 2 s on 10 Mbps; 500-row worklist renders < 3 s; 200 concurrent users/hospital; DB backups every 6 h; restore drill documented.
- Test gates before release: (a) API unit/integration suite green; (b) Playwright smoke per role covering login → primary task → logout for each of the 9 roles; (c) golden-path e2e for OPD (book→vitals→consult→Rx→bill→pay), IPD (admit→order→MAR→discharge→bill), Pharmacy (scan→FEFO→checkout→return), Pathology (order→result→report).
- Type safety: `npx tsc -p tsconfig.app.json --noEmit` must be run (root `tsc --noEmit` checks nothing); there were 60 pre-existing type errors — reduce to 0 or ratchet.

### XC-11 Deployment & operations
- Self-hosted VMs, Docker/GHCR, GitHub Actions: `develop` → dev (.77), `main` → prod (.67), DB VM (.68); MinIO (S3-compatible) for files; Caddy reverse proxy. Prod deploy SSH/SCP secret problem is a known infra issue for some repos.
- Requirements: DB migrations apply before the API that needs them (guarded `ALTER`, never edit committed `CREATE TABLE`); documented rollback; zero-downtime or announced maintenance window; backup/restore tested; secrets rotated and held in GitHub secrets only.

### XC-12 Release hygiene for non-production code
- Preview/mock routes stay behind `import.meta.env.DEV` and the `PREVIEW-HOSPITAL` mock short-circuits in services shall be tree-shaken from prod (assert in CI that no production bundle contains `PREVIEW-HOSPITAL` mock data or the strings "Sarah Johnson"/"Michael Chen").
- Routed pages marked 🔴 shall be removed from routing/nav or finished (B-2, B-3).

---

## 5. Open product decisions (need an owner)

1. **Pharmacist / Lab Technician / Coordinator landing pages** (B-1): `/pharmacy-retail`, `/pathology`, `/ot-board` suggested.
2. **Appointment Oversight:** build (admin/manager view over real appointments with no-show, follow-up, reassign) or remove.
3. **Doc AI:** integrate a real clinical-assistant backend with guardrails/disclaimer, or remove from routing.
4. **DPDP-Act posture:** consent capture, retention periods, patient data-export/erasure workflow.
5. **Audit viewer** scope and retention (XC-4).
6. **Insurance/TPA depth:** IRDAI discharge clocks and enhancement requests exist; PM-JAY/scheme packages and GST for ICU gap-analysis remain (see IPD Phase 1 notes). Decide go-live scope.
7. **Radiology hand-off** (Phase 5 of `PLAN.md`): not built; confirm out of go-live scope.
8. **Online-booking SLA:** target time from public booking to receptionist alert (polling today, ~seconds) and what happens when no one is on the board.
