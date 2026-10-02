# 02 — Appointments & OPD Front Desk

> Back to [index](README.md). Source: `features/appointment/*`, API `AppointmentsController`, `QueueController`, `CalendarServicesController`, `ReferrersController`, `PublicController` (online bookings, tokens).

**Roles with access:** Admin, AdminDoctor, Receptionist, Nurse (`appointment_scheduler`, `appointment_booking`).

### OPD visit lifecycle (shared by all pages in this file)

```
PRE_APPOINTMENT (online booking, no slot yet)
        │ Confirm (auto-assign / pick slot, token allocated)
        ▼
   SCHEDULED ──► VITALS_REQUIRED ──► READY ──► UNDER_CONSULT ──► COMPLETED
        │                                  │         │  ▲
        │                                  │         ▼  │
        └──────────────► CANCELLED   LAB_REQUIRED ─► AWAITING_RECONSULT
```
`finalStatusCode` is computed server-side; badges for **Online Appointment**, **Vitals**, **Ready** are clickable shortcuts (confirm / record vitals).

---

## 2.1 Appointment Dashboard — `/appointment-dashboard`

**Purpose.** The front-desk board: today's queue and history/upcoming, with the actions a receptionist/nurse performs around a visit — register, confirm online bookings, take vitals, reschedule, cancel, print token, bill the consult fee, and see lab/documents. Landing page for Receptionist (and Doctor on mobile). **Readiness:** 🟡

### Functional requirements
| ID | Requirement |
|---|---|
| APD-1 | The page shall have three views — **Current** (today / active queue), **Past** (history), **Future** (upcoming) — each with its own date-range filter (Past: up to yesterday; Future: from tomorrow). |
| APD-2 | Each appointment row shall show: patient ID & name, doctor, token number, appointment date/time, status badge, case type (New / fee follow-up / free follow-up) with the applicable fee, referrer, lab-report indicator, and row actions. |
| APD-3 | Filters: free-text search (patient ID, name, token no.), doctor (with a today's-availability dot per doctor), department, status chips (All, Vitals Required, Ready, Under Consult, Lab Required, Awaiting Reconsult, Completed, Cancelled, Online Appointment). Filters shall combine (AND). |
| APD-4 | KPI strip shall summarise the current view: Total, Vitals, Ready, Consulting, Lab tests, Follow-ups, Completed; for Past: Total Past, Unique Dates; for Future: Total Future; plus per-doctor no-show counts. KPIs reflect the *whole loaded view*, not just the page. |
| APD-5 | Sorting by patient ID, name, doctor, token, date, status, case; default **token number, descending**. |
| APD-6 | **Online booking live feed:** new Doctor Dekho / NexEagle bookings shall appear within a few seconds without a manual refresh: a "Live" chip, a NEW pill and highlight on the row, a ringing animation, an optional chime (user preference persisted), and a stacked alert with a **View** action that jumps to the booking's date. The first poll is a baseline (never announces pre-existing bookings); duplicates are announced once; polling pauses when the tab is hidden and catches up on focus. |
| APD-7 | **Confirm pre-appointment:** the Online Appointment badge/button opens a drawer showing patient details; the system shall *auto-assign* the patient's preferred time if free, else the earliest open slot that day, else search forward day-by-day (max 14 days) before asking staff to pick manually; "Change" switches to manual date/slot picking. On a "slot just taken" conflict it shall refetch booked slots and re-run the auto-search, never leave staff stuck. The real per-shift slot duration shall be sent. |
| APD-8 | **Vitals:** Vitals/Ready badge or action opens the vitals form (see 2.4); status moves VITALS_REQUIRED → READY on save. |
| APD-9 | **Reschedule:** pick department → specialist → new date → slot; strictly future dates only; the previous slot is released. |
| APD-10 | **Edit:** edit patient details of an existing appointment in place (no re-booking), including demographics not carried on the appointment (loaded from the patient profile). |
| APD-11 | **Cancel:** reason required text ("Why is this appointment being cancelled?" optional per UI, **required by policy — see gaps**); on success the slot is freed and the list refreshes; Cancel, reschedule and confirm are disabled for COMPLETED and CANCELLED rows. |
| APD-12 | **Print token:** thermal/A4 token with token no., patient, ID, age/sex, doctor, date and a "Scan to track" QR to `/token-view`. **Print Rx:** reprints the generated prescription of a completed visit. |
| APD-13 | **Documents:** upload/view patient documents (lab reports, outside records) against the appointment via the attachments panel; a lab-report-ready badge shall appear on rows with new reports. |
| APD-14 | **Add Bill (OPD consult fee):** a per-row panel shows the doctor's consult fee, whether this visit is chargeable (new / fee follow-up / free follow-up within the follow-up window), payment status (Paid / Unpaid / Not charged), a payment timeline, **Mark Paid / Mark Unpaid** with payment mode (Cash, Card, UPI, …), and — once paid — Invoice / Receipt / Bill+Receipt with Print and PDF. Paying shall be idempotent (never double-charge; already-paid shows the success popup with the existing receipt). **Go to Billing** opens the encounter ledger. Collecting money requires connectivity (never queued). |
| APD-15 | **Book Appointment** button (and a floating button on mobile) opens the booking flow (2.2) without leaving the page; on success the list refreshes and the new row is highlighted. |
| APD-16 | **Manage Availability** opens the doctor availability roster/calendar dialog (see 3.4) so staff can check who is in/out. |
| APD-17 | Auto-refresh every 30 s plus manual "Refresh now", with an honest "last updated" timestamp that changes only when data actually changed. |
| APD-18 | When the subscription is Expired/Blocked all write actions (cancel, reschedule, edit, confirm, vitals, billing) shall be disabled with the upsell modal; viewing remains. |
| APD-19 | Sidebar auto-collapses on entry to maximise space; compact mode is the default; all strings are i18n (EN/HI). |

### Business rules
- **Consult fee policy:** auto-charge on same-day booking only when Billing Policy → OPD trigger = AUTO and the doctor has a consult fee; future bookings are billed from the dashboard when the patient arrives. A paid consult grants a **free follow-up window** returned by the consult-timeline API (`GET appointments/consult-timeline`: last paid date, free follow-ups, next-visit preview); outside it the visit is chargeable again. **VERIFY** where the window length is configured (Billing Policy vs Doctor Fees).
- **Duplicate-visit guard:** booking a patient who already has a non-cancelled appointment with the same doctor the same day shows a warning but allows staff to proceed.
- Same-slot concurrency is resolved server-side; the UI treats a conflict as a normal path (APD-7).

### Gaps
| Pri | Gap |
|---|---|
| P1 | **Page size is fixed at 5** (`itemsPerPage = 5`) and all filtering/sorting/KPIs are computed client-side over the full fetched set. With ≥ 200 appointments/day this fails the "500-row worklist < 3 s" NFR and makes the page tedious. Move to server-side pagination/filters/sort with configurable page size (10/25/50), keep KPIs as a server aggregate. |
| P1 | **Queue endpoints are not used by any UI.** `QueueController` (`mark-arrived`, `call`, `skip` per doctor) exists with `appointment_scheduler` permission; the dashboard has no "arrived", "call next" or "skip" control, and no public/TV token display driven by it. Decide: build token calling (waiting-hall display + SMS/WhatsApp "your turn") or drop the endpoints. |
| P1 | Cancel reason is optional in the UI; require it (and capture who cancelled) for audit and refund logic; trigger refund/credit workflow if the consult fee was already collected. |
| P1 | No explicit **no-show** action/status on this page (KPIs count no-shows per doctor, but staff cannot mark one). Add "Mark no-show" with automatic slot release and follow-up reminder. |
| P1 | Online-booking alerts depend on polling every few seconds per open tab; define SLA, back-off, and consider SSE/WebSocket. Alert audio needs a user gesture on first load — define behaviour when blocked. |
| P2 | Cross-doctor "walk-in/emergency" override (book beyond slot capacity) with reason. |
| P2 | Bulk actions (cancel all for a doctor on leave → notify patients). |

### Acceptance
- A booking made on the public site appears on a signed-in receptionist's board in ≤ 10 s with chime + highlight, once only.
- Confirming 20 online bookings in a row assigns distinct slots with no double-booking, even with two receptionists confirming simultaneously.
- Marking the consult Paid twice yields one invoice, one receipt, and the second click shows the existing receipt.

---

## 2.2 Appointment Booking — `/appointment-booking` (and in-dashboard sheet)

**Purpose.** Book a new OPD appointment for a new or existing patient. **Readiness:** 🟡

### Flow
1. **Department** (sidebar with icons; hospital departments) →
2. **Doctor** (doctors in that department; availability aware) →
3. **Date** (date selector; past dates disabled) →
4. **Shift tab** (Morning/Afternoon/Evening generated from the doctor's configured shifts) →
5. **Time slot** grid (free / booked / blocked; booked slots greyed; doctor time-off blocks the day) →
6. **Patient form** (search/register) → optional **Vitals** → **Booking success** (token, print, share).

### Functional requirements
| ID | Requirement |
|---|---|
| BK-1 | Slots shall come from `GET calendar/doctor/slots` (weekly template + date overrides + time-off) and `GET appointments/patient-booked-slots`; a slot booked by anyone shall be marked booked after a refresh triggered on every doctor/date change and after every successful booking. |
| BK-2 | If the doctor is on time-off for the date, the UI shall say so and offer no slots. |
| BK-3 | **Patient search** shall be one fuzzy search over name / patient ID / mobile / Aadhaar / ABHA; a mostly-digit query is treated as a phone number and stripped to digits; arrow-key navigation and Enter selection are scoped to the search field. |
| BK-4 | Selecting an existing patient auto-fills demographics (handling missing age safely) and ABHA link status; "Discard selected patient" returns to new-patient entry. |
| BK-5 | **New patient** mandatory fields: name, age (+unit), sex, 10-digit mobile (first digit ≠ 0). Optional: alternate mobile, email, blood group, guardian/relative (name + relation, stored on the patient), full address (pincode format validated only if given), emergency contact (name, relation, phone). Mandatory-empty fields highlight amber, invalid red after submit. |
| BK-6 | **Duplicate detection** (Jaro-Winkler on name + age/DOB/mobile/ABHA, only while entering a *new* patient) shall list "Matching patients" with **Use this** / **Dismiss**; an exact ABHA match is deterministic and triggers without a name. |
| BK-7 | **Referrer:** "Self" by default; search doctor/referrer, or create one inline (name, phone, address). A typed-but-not-selected referrer shall be reused if an exact case-insensitive match exists else created — never silently dropped. The referrer's incentive is recorded for billing. |
| BK-8 | **Same-day OPD fee:** when Billing Policy OPD = AUTO and the doctor has a fee, the form shows the fee and "Mark paid" with payment mode; the encounter and consult charge are auto-created (idempotent, non-blocking — the appointment is already booked). Offline: the booking is queued and the backend creates the charge on replay; **payment must be collected after reconnect** (never queued). |
| BK-9 | **Patient ID** is generated automatically; booking returns `appointmentId` and `tokenNumber`; the UI shows the success screen with Print Token and share. |
| BK-10 | **Offline:** booking is queueable (Saved offline toast); on replay, a slot conflict shall surface a clear "that slot was taken — choose another" resolution path. |
| BK-11 | Same-day duplicate booking warns but allows. Booking into the past shall be blocked server-side (the slot picker offers only future/current slots). |
| BK-12 | ABHA: when the patient has a linked ABHA it shows "Linked"; otherwise "Not linked yet" with a deep link to `/abdm` (see 13.1). |

### Gaps
| Pri | Gap |
|---|---|
| P1 | The booking component renders its own "authentication required"/"hospital data unavailable" states and uses a `useHospitalUser` chain (user → hospital → departments); failure of the first call shows a dead-end. Add retry and a clear error. |
| P1 | No **walk-in/emergency override** or "next available" quick-book. |
| P1 | Slot capacity is 1 patient/slot; clinics running multi-patient (wave) slots cannot model it. |
| P2 | WhatsApp/SMS confirmation to the patient at booking (public flow does; staff flow — verify). |

---

## 2.3 Reschedule / Cancel / Edit dialogs

Covered by APD-9…11. Additional rules: reschedule keeps the same appointment ID, releases the old slot, re-issues a token if the day changes, notifies the patient (**VERIFY** notification), and re-evaluates the consult fee (a rescheduled same-day → future visit moves billing to arrival). Cancel dialog shows Appointment ID, patient, doctor and requires explicit confirm; failures show "Could not cancel appointment" without losing the row.

---

## 2.4 Vitals form (modal from dashboard / booking)

**Purpose.** Capture triage vitals so the doctor sees them in the prescription pad and the status moves to READY. **Readiness:** ✅

| ID | Requirement |
|---|---|
| VIT-1 | Capture systolic/diastolic BP, heart rate, respiratory rate, temperature, SpO₂, height and weight (BMI derived downstream); each with unit and plausible-range validation (reject impossible values, warn on out-of-range). |
| VIT-2 | Prefill with existing vitals for the appointment; show a patient info banner (name, age/sex, allergies if known). |
| VIT-3 | Save to `POST appointments/patient-vitals`; success invalidates dashboard queries so the status flips without refresh; offline-safe. |
| VIT-4 | Vitals recorded here shall appear on the e-prescription pad (4.3) with hypertension grade classification (Low / Optimal / Grade 1–3) derived on the client. |

---

## 2.5 Token print & digital token — `/token-view` (public)

- **TokenPrintModal:** printable token (see APD-12). 
- **TokenDetailsPage (public):** opened by scanning the QR; shows token number, status "Active", date, patient (ID, age/sex), doctor, and "Please wait for your turn." **Requirements:** (a) shall expose the *minimum* patient data needed (mask name, no mobile) since the URL is shareable; (b) shall show live position/ETA once the queue endpoints (2.1 gap) are used; (c) shall rate-limit by token/IP; (d) shall not accept enumerable IDs (use signed/opaque tokens). **Readiness:** 🟡 (privacy hardening needed).

---

## 2.6 Appointment Oversight — `/appointment-oversight` 🔴

**Intended purpose.** A manager/admin view over appointments: filter by department/doctor/status/date, see no-shows and required follow-ups, edit/cancel/reschedule, message patients, export.

**Current state — NOT production.** The component initialises state from a hard-coded `sampleAppointments` array (three fictional appointments dated Jan 2024; five fictional doctors "Dr. Sarah Johnson", "Dr. Michael Chen", …) and keeps all edits in memory. No API calls. It is routed under `appointment_scheduler`, so Admin/AdminDoctor/Receptionist/Nurse can open it and would see fake patient rows. (The corresponding Admin Panel entry is already commented out.)

**Resolved 2026-10-02 (B-2): the route, nav rule and component were removed.** If the capability is wanted, build it as below.

### Requirements if built
| ID | Requirement |
|---|---|
| OVR-1 | Read real appointments through the same paged server API as the dashboard, with filters: date range, department, doctor, status, appointment type (consultation/follow-up/emergency/surgery), priority. |
| OVR-2 | Show no-show and cancelled analytics per doctor/department, plus "needs follow-up" with due dates. |
| OVR-3 | Actions: reschedule, cancel (with reason), mark no-show, send reminder (WhatsApp/SMS), export CSV/PDF. Every action audit-logged. |
| OVR-4 | Restrict to Admin/AdminDoctor (not Nurse/Receptionist) via a dedicated permission key (e.g. `appointment_oversight`). |

---

## 2.7 Referrer management (used here; managed in Patients → Referrers, see 4.5)
Referrer picker is shared by appointment booking and IPD admission; types: doctor/other; fields name, phone, address; incentive per service is applied at bill time (see 05).
