# 03 — Doctor Board, Analytics, Profile & Calendar

> Back to [index](README.md). Source: `features/doctor/*`, `features/doctor-calendar/*`, `features/ai/*`, API `DoctorDashboardController`, `DoctorsController`, `CalendarServicesController`, `EPrescriptionController` (field layout / preferences).

**Roles:** Doctor, AdminDoctor (`doc_board`); calendar also Admin and Receptionist (`doctor_calendar`).

---

## 3.1 Doctor Board — `/dashboard`

**Purpose.** The doctor's home: their patient queue for the day, history and upcoming; one-click entry into the prescription pad; access to calendar, analytics and prescription settings. Landing for Doctor on desktop. **Readiness:** ✅ (🟡 on items below)

**Shell:** four sections in a nav — **Appointments**, **Calendar**, **Analytics**, **Settings** — with a header showing doctor name, a **Verified** badge driven by profile completion, "View professional profile", and a self-service **Online now / Offline** toggle (controls whether the doctor is bookable on the public site).

### Functional requirements
| ID | Requirement |
|---|---|
| DB-1 | **Profile gate:** if the doctor profile is incomplete (API returns 204/404) the board shall show a restriction screen listing what is required (department, experience, licence number, qualifications) and the benefits unlocked (appointments, calendar, prescriptions) with a *Complete profile* CTA — and shall not show the appointment error state. |
| DB-2 | **Appointments** tabs: Current, Past, Future — search by patient name/ID/token, status chips (All, Vitals Required, Ready, Consulting, Lab Required, Reconsult, Completed, Cancelled), date range for Past/Future; mobile card view and desktop table. |
| DB-3 | **Primary action per status:** READY / AWAITING_RECONSULT / VITALS_REQUIRED → **Start Consult** (opens the prescription pad, 4.3, for that appointment); UNDER_CONSULT → **Complete Consult** (confirmation dialog "mark done"); others → view. Starting a consult from VITALS_REQUIRED is permitted (doctor may proceed without vitals). |
| DB-4 | Row secondary actions: **InkRx** (jump straight to the handwritten pad), **Print Rx** preview (uses the appointment's own date), **Lab attachments** upload/view, **Add Bill** (OPD consult fee, same panel as 2.1/APD-14), **Reschedule**, **Cancel** (dialog with patient/doctor/ID summary), **Advise Admission** (4.7). |
| DB-5 | **Badges per row:** latest *Advise-Admission* referral status (fetched once, not per row); **Lab report ready** (multiple reports per patient, newest first, visible to whichever doctor sees the patient today). |
| DB-6 | Auto-refresh and honest last-updated; sidebar auto-collapses; URL reflects the active section for bookmarking (replace, not push). |
| DB-7 | **Settings → Prescription fields / Personalised data / Layout lab:** per-doctor, hospital-agnostic prescription field layout (rename, show/hide, reorder, custom fields), personal quick-pick data (complaints, diagnoses, investigations, medicines) and layout designer (see 4.3/11.4). |
| DB-8 | When the subscription is read-only, writes (cancel, reschedule, billing) are disabled with the upsell modal. |
| DB-9 | Errors: missing doctorId/hospitalId produce explicit messages with a refresh action, not blank screens. |

### Gaps
| Pri | Gap |
|---|---|
| P1 | The board file is ~2,900 lines mixing data fetch, filtering, three views (table/mobile card/preview) and dialogs; extract to testable hooks/components before further feature work (regression risk on the most used clinical page). |
| P1 | Server-side paging/filter (same as APD gap) for doctors with large histories. |
| P1 | "Complete consult" with an *empty* prescription: define guard (warn if no diagnosis/advice recorded). |
| P1 | In-consult **timer / waiting time** and "next patient" auto-advance are not present. |
| P2 | Dictation (Voice Rx) entry point from the board row. |

### Acceptance
- A doctor with a complete profile sees only their own patients; one with an incomplete profile sees the restriction screen and cannot reach appointments/calendar/prescriptions.
- Start Consult → pad opens with the patient, vitals and prior visits pre-loaded in ≤ 2 s on 10 Mbps.

---

## 3.2 Doctor Analytics — Board → Analytics

**Purpose.** Self-service practice analytics for the doctor. **Readiness:** 🟡 (B-10)

### Functional requirements
| ID | Requirement |
|---|---|
| DA-1 | KPIs with time buckets (today, yesterday, last 7 days, this month, this year, previous year): **total visits, unique patients, new vs returning, no-show, cancelled**. |
| DA-2 | Distributions: age bands, gender; vitals distributions (BP, weight, BMI). |
| DA-3 | **Top-5 lists:** medicines prescribed, chief complaints, diagnoses, investigations, examinations, each with count and share. |
| DA-4 | Source: `GET doctor-dashboard/analysis/hospitalId=…&doctorId=…`, scoped to the logged-in doctor (AdminDoctor may switch). Empty state "No Analytics Data" when none. |
| DA-5 | Each KPI shall show **trend vs the previous equivalent period** (▲/▼ %). |

### Gaps
| Pri | Gap |
|---|---|
| **P1** | **B-10:** the client maps `byBucket` with `trend: 'neutral'` and `percentageChange: 0`; new/returning uses "fallback values"; `noShow`/`cancelled` arrive as a single number and are spread across buckets. Either compute real trends server-side or hide trend arrows. |
| P1 | Revenue per doctor, average consult time, patient wait time, follow-up compliance, referral-source performance — absent. |
| P2 | Export (PDF/CSV) and date-range picker beyond fixed buckets. |

---

## 3.3 Doctor Profile — Board → View professional profile / `/profile?tab=professional`

**Purpose.** Professional identity used on the public directory, prescriptions, and licensing checks. **Readiness:** ✅

| ID | Requirement |
|---|---|
| DP-1 | Fields: primary department, specialisations (multi-select, from `medical-specialities`), qualifications (≥ 1 required), **licence number (required, unique per hospital — conflict toast)**, **medical council (required)**, **registration year**, experience in years (non-negative), professional bio (30–70 words recommended, word counter); profile photo is edited from My Profile (13.4). |
| DP-2 | **Profile completion %** (department, experience, licence, qualifications are the gating four) drives the Verified badge and the board unlock. |
| DP-3 | There is **no public NMC verification API in India**; the CMS offers a "Verify on NMC register" action that copies the number and opens NMC's manual search. The hospital shall record who verified and when (P1). |
| DP-4 | Public-listing flag, online-status (self) and marketing-discount are controlled by hospital admin/CMS, not by the doctor profile page. |

---

## 3.4 Availability Roster (staff landing for the calendar) — in `/calendar` and Appointment Dashboard dialog

**Purpose.** "Who is available today?" at a glance across all doctors, before drilling into one calendar. **Readiness:** ✅

| ID | Requirement |
|---|---|
| AV-1 | List every active doctor with today's availability resolved by the precedence **TimeOff > Date Override > Weekly Template**; unavailable doctors sort first. |
| AV-2 | Staff (Admin/AdminDoctor/Receptionist) can flip a doctor's **Online now** switch (optimistic with rollback) and click through to that doctor's calendar. |
| AV-3 | Same resolver shall feed the dot next to each doctor in the Appointment Dashboard filter so the two screens never disagree. |

---

## 3.5 Doctor Calendar — `/calendar`

**Purpose.** Define when each doctor works and block time off; the output drives bookable slots everywhere. Doctors manage their own; Admin/AdminDoctor/Receptionist pick a doctor via a switcher. **Readiness:** 🟡

### Functional requirements
| ID | Requirement |
|---|---|
| CAL-1 | Views: **Month** (default — answers "which days is this doctor off"), Week, Day (FullCalendar); header navigation; legend; sidebar of upcoming events. |
| CAL-2 | **Weekly templates:** per weekday, per shift (Morning / Afternoon / Evening): start, end, slot duration, active flag; saving creates all candidate templates (inactive) then activates chosen ones. |
| CAL-3 | **Date overrides:** change a single day's shift hours; cancelling an override restores the template; override edit/delete via dialogs. |
| CAL-4 | **Time-off / blocks:** types Annual Leave, Sick Leave, Personal, Conference, Training, Meeting, Emergency, Other; single day or range (date-range popup); partial-day blocks; start must be ≥ now − 10 min buffer; delete time-off with confirmation. |
| CAL-5 | **Conflict handling:** creating a block/override over existing booked appointments shall list affected appointments and require the user to reschedule/cancel them or abort. |
| CAL-6 | Mark-unavailable shortcut for today/tomorrow. |
| CAL-7 | Staff must arrive with an explicit doctor (`?doctorId=`) or pick from the switcher/roster — never auto-select "first doctor". |
| CAL-8 | Changes shall take effect immediately in booking, confirm-pre-appointment and public availability, and notify the doctor when changed by staff (**VERIFY**). |
| CAL-9 | Embeddable (inline from the Appointment Dashboard and Doctor Board) with the same behaviour. |

### Gaps
| Pri | Gap |
|---|---|
| P1 | Five `TODO: Refetch calendar events here if needed` markers in `DoctorCalendarPage.tsx` after create/delete flows — confirm the calendar always reflects the server after each mutation (stale-calendar risk). |
| P1 | Conflict list (CAL-5) behaviour must be tested; it is the highest patient-impact path (patients booked into leave days). |
| P1 | Recurring blocks (e.g. every Tuesday OT day) and holiday calendar at hospital level. |
| P2 | **B-15** gamified header (XP/level/streak in `localStorage`): either back it with real server metrics or remove; it currently rewards clicks, not outcomes. |

---

## 3.6 Doc AI — `/doc-ai` 🔴

**Intended purpose.** A clinical-reference assistant for doctors (symptoms, ICD-10, guidelines, drug interactions, emergency protocols), with chat, suggested questions, copy/download, voice input and file upload.

**Current state — NOT production (B-3).** `DocAI.tsx` simulates a response with `setTimeout` and a hard-coded keyword switch (hypertension, diabetes/ICD, pneumonia) and a generic fallback. There is no model, no citation, no safety layer; the mic and file-upload buttons are stubs. It is routed under `doc_board` but not in the sidebar, so it is reachable only by URL.

**Resolved 2026-10-02 (B-3): the route, nav rule and component were removed.** Rebuild only if the requirements below are funded.

### Requirements if built
| ID | Requirement |
|---|---|
| AI-1 | Answers shall come from a governed model/RAG over approved sources (guidelines, formulary, ICD-10/11) with **citations** and a visible "clinical decision support — verify before acting" disclaimer. |
| AI-2 | No patient identifiers are sent to a third-party model without hospital opt-in and a data-processing agreement; a de-identification step is mandatory. |
| AI-3 | Conversations are not stored with PHI unless the doctor saves them to the chart; retention configurable. |
| AI-4 | Drug-interaction queries shall use a deterministic interaction database, not free-form generation. |
| AI-5 | Observability: log prompts/responses (de-identified), rate-limit per doctor, kill-switch per hospital. |
