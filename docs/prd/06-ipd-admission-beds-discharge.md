# 06 — IPD: Dashboard, Admission, Bed Board, Referrals, KPIs, Ledger, Discharge

> Back to [index](README.md). Source: `features/ipd-redesign/{IpdWorkflowApp,screens,pages,services}`, API `AdmissionController`, `AdmissionReferralController`, `BedController`, `DischargeSummaryController`, `DischargeSettingsController`, `IrdaiDischargeController`, `IpdKpiController`, `ConsultantIncentiveController`, `CssdController`. The IPD client uses `ipdApiClient`, which now targets the single `easyHMSAPI` host (separate IPD backend retired).

**Roles:** Admin, AdminDoctor, Nurse, Doctor, Coordinator (`ipd`). Route `/ipd-workspace` is a single-page switcher between: **Dashboard**, **Bed Board**, **Referred Admissions**, **KPI dashboard**, **Consultant Ledger**, **CSSD board**, and the **Patient Workspace** (07).

### Admission lifecycle
```
Advise admission (OPD doctor) ─► Referred Admissions: PENDING
                                       │ Admit (wizard pre-filled)
Pre-registration (elective) ─► PRE_ADMIT ──confirm arrival──► ADMITTED ──► (transfer bed / change doctor / referrer / coverage)
Emergency quick-admit (unidentified patient) ───────────────► ADMITTED
                                                                  │ discharge summary → sign → discharge
                                                                  ▼
                                                              DISCHARGED   (or LAMA — Left Against Medical Advice, with signed form)
```
Admission types: **Planned (ELECTIVE)**, **Emergency**, **Day Care**, **LAMA**.

---

## 6.1 IPD Dashboard — `/ipd-workspace` (home)

**Purpose.** The admitted-patient list with census KPIs; the launch pad for everything IPD. **Readiness:** ✅

| ID | Requirement |
|---|---|
| IPD-1 | List admissions (`GET admission/active` with status filter **Active / Discharged / All**) showing admission no., patient, age/sex, ward/bed, primary doctor, admission type/date, LOS, status pill (ADMITTED green, PRE_ADMIT, DISCHARGED grey, other rose); rows open the Patient Workspace (07). |
| IPD-2 | KPI tiles reflect the **current active census**, independent of the list filter. |
| IPD-3 | **Admit** button opens the Admit wizard (6.2); header nav to Bed Board, Referred Admissions, KPI, Consultant Ledger, CSSD; Inventory is reached from the main side nav. |
| IPD-4 | **Confirm arrival** action on PRE_ADMIT rows converts a pre-registration into an active admission (bed assignment + deposit collected then). |
| IPD-5 | Date filter in IST; times from the API are naive UTC and shown in IST. |
| IPD-6 | Resilient to auth-store hydration: if `hospitalId` is not ready on first mount, load re-runs when it arrives (no crash). |

### Gaps
| Pri | Gap |
|---|---|
| P1 | Pagination/search server-side for hospitals with large discharged histories; MRD search (year/doctor/department/diagnosis/procedure, `PLAN.md` Phase 4) is not present. |
| P1 | Ward/doctor/payer filters and a "my patients" quick filter for doctors/nurses on this list. |
| P2 | Expected-discharge-date (EDD) column with breach alert; deposit-low alert surfaced here. |

---

## 6.2 Admit Patient wizard (sheet)

**Purpose.** Create an admission fast and safely. **Readiness:** ✅ (4 steps)

| Step | Required | Content |
|---|:-:|---|
| 1. Admission Type | ✔ | Planned / Emergency / Day Care / LAMA (with descriptions). Planned has a **pre-registration** toggle. |
| 2. Personal Information | ✔ | Existing-patient search (name/UHID/mobile/Aadhaar/ABHA, fast known-patient path) **or** new-patient entry with live **duplicate detection** (name/mobile/DOB/Aadhaar/ABHA, same `/patient/check-duplicates` as booking). Phones digits-only, 10 max. |
| 3. Clinical & Referral | – | Primary consultant (picker), diagnosis/chief complaint, ICD-10 pick, **referrer** (`ReferrerPicker`, creates a referrer immediately on confirm), **OT Plan** picker (pre-fills entitled room category; shows ICU hint; frozen at admit), insurance/coverage (payer type, sanctioned amount) for entitlement warning. |
| 4. Advance & Bed | – | Advance/deposit amount + mode; live **bed picker** by ward/room with rates; room-entitlement warning when the chosen bed exceeds entitlement. |

### Functional requirements
| ID | Requirement |
|---|---|
| ADM-1 | **Emergency quick-admit:** an unidentified patient must never be blocked — only **sex + approximate age** are required; name/mobile are backfilled later. |
| ADM-2 | **Draft autosave** (local only, ≤ 24 h) protects against accidental close/refresh; a *restore banner* (never silent) offers restore/discard; a pre-filled-from-referral open always wins over a stale draft. Drafts never touch the server. |
| ADM-3 | **Idempotency key** per admit attempt reused across retries (offline resync cannot create a duplicate admission). |
| ADM-4 | **Referral conversion:** admitting from a Referred Admissions row passes `referralId`; the referral flips to CONVERTED in the *same transaction* as the admission. |
| ADM-5 | **Consent:** if the hospital configured an active `GENERAL_ADMISSION` consent template, a consent checkbox appears (best-effort capture, never blocks or undoes an already-successful admission; skipped for pre-registration). Never blocks on missing config. |
| ADM-6 | Aadhaar is captured only for brand-new patients; existing patients' Aadhaar is untouched. |
| ADM-7 | Success screen shows admission number, bed, and print actions (admission confirmation A4, token receipt). Pre-registration success is labelled "Patient pre-registered". |
| ADM-8 | Entitlement warning: compares chosen room category to the payer's entitlement (`roomEntitlement` util) before submit; warns, does not block. |

### Gaps
| Pri | Gap |
|---|---|
| P1 | **MLC (medico-legal case) handling** with injury map, police intimation, and restricted record access — not present (`PLAN.md` Phase 4). Required for emergency/trauma hospitals. |
| P1 | **Attendant & visitor tracking**, ID proof capture, valuables register. |
| P1 | **ABHA** at admission: link/create inline (see 13.1) rather than leaving the wizard. |
| P1 | PM-JAY / government scheme packages and eligibility; GST handling for ICU/room rent per the IPD gap analysis. |
| P2 | Fast-track triage score at emergency admit. |

---

## 6.3 Referred Admissions — board

**Purpose.** Front-desk work queue of doctor-advised admissions. **Readiness:** ✅

| ID | Requirement |
|---|---|
| REF-1 | List every advised admission (from the prescription pad) with patient, advising doctor, case type, advised on, optional OT plan, status **PENDING / CONVERTED / NOT_ADMITTED / FOLLOW_UP**. |
| REF-2 | **Status and case-type filters are server-side** (required for correct pagination); search is client-side over the current page only (backend has no text search) — requirement: add server text search. |
| REF-3 | Actions: **Admit** (opens the wizard pre-filled), mark Not admitted / Follow-up, **comment thread** (`admission-referral/comment`), edit details. Converting happens only through a real admission. |
| REF-4 | Status ageing: show days since advice and highlight overdue follow-ups. |

---

## 6.4 Bed Board — screen

**Purpose.** Live ward/room/bed occupancy and bed operations. **Readiness:** ✅

| ID | Requirement |
|---|---|
| BED-1 | Group beds by ward (`WardCode`) → room; each bed card shows status (`AVAILABLE` / `OCCUPIED` / `CLEANING` / `RESERVED` / `BLOCKED`), patient, doctor, assigned nurse(s) (bulk-fetched in **one** call, not per bed), LOS, and badges (EWS, alerts). |
| BED-2 | Operations: **Assign** (patient → free bed), **Release**, **Transfer** (target = free beds excluding the current one), with reason; every move is recorded (bed history) and flows to billing (bed-day charges, room-class multiplier). |
| BED-3 | Ward filter and a census footer over the *visible* beds (mobile at-a-glance); silent background refresh (paused while an action dialog is open). |
| BED-4 | Non-admin users see all beds but nurses' own beds are emphasised. |
| BED-5 | Bed master (Admin → Configuration → Bed Master) manages floors → rooms → beds with ward type, rate, bulk create/deactivate/**hard delete only if no assignment history** (blocked bed codes reported in a modal, not a toast). |

### Gaps
| Pri | Gap |
|---|---|
| P1 | **Housekeeping workflow:** a `CLEANING` state exists, but there is no housekeeping task/assignment or "cleaned & ready" confirmation with timestamps; the "bed turnaround" KPI depends on those events — confirm release/ready stamping. |
| P1 | Real-time push (SSE/WebSocket) instead of polling for multi-desk hospitals. |
| P2 | Isolation flag/cohorting and gender-ward rules. |

---

## 6.5 IPD KPI dashboard & Consultant Ledger / Incentives

### KPI dashboard (hospital-wide)
Five metrics with date range: **Bed Occupancy Rate (BOR %)** with 30-day trend, **ALOS (days)** with weekly trend, **average bed turnaround (hours)**, **average discharge TAT (hours, with sample size)**, **readmission rate %** (readmitted / index discharges). Definitions are owned by `GetIpdKpiDashboardHandler`/`IpdKpiCalculator`.
- Requirement: each tile shows its definition on hover and the sample size; zero-data states say "no discharges in range" not 0 %. Mock KPI data exists only for the dev preview hospital id `PREVIEW-HOSPITAL`.
- Gap (P2): department/doctor drill-down, mortality, infection rates (infection events exist, see 07), occupancy forecast.

### Consultant Ledger (`consultant-incentive`)
- Left: per-doctor **accrued / paid / cancelled** totals; right: line-level drill-in per charge with patient/admission; **Settle** marks every currently ACCRUED line PAID.
- Incentive = per-service flat ₹ from `ChargeMaster.IncentiveAmount` (editable at bill time); replaces the old referrer-%.
- Requirements: settle requires confirmation and records settler/date/mode/reference; partial settle; export statement; a settled line cannot be re-accrued; cancelled charges auto-reverse accruals.
- **Incentives screen** (`IncentivesScreen`) — per-beneficiary breakdown by module/department. ⚪ **`IncentivesScreen`, `AutoBillingConfig`, `DischargeFlow`, `NurseDashboard` and `DoctorDashboard` (IPD) are not imported anywhere** — they are unrouted prototypes (the live equivalents are the Consultant Ledger, Admin → Billing Policy, the Discharge panel, and the Nursing Station). Delete or finish (B-13).

---

## 6.6 Discharge (inside Patient Workspace → Discharge; template config under Admin)

**Purpose.** Produce a legally sound, signed discharge summary and close the stay. **Readiness:** 🟡 (B-9)

| ID | Requirement |
|---|---|
| DIS-1 | **Auto-populate** the draft from the chart (diagnosis, course, procedures, investigations, medications, vitals trend, advice) via `GET discharge-summary/draft`; doctor edits; **autosave** (2 s debounce, only on change). |
| DIS-2 | **Per-doctor field layout** and **letterhead/print designer** (Admin → Discharge Letterhead): the layout and letterhead shown are those of the *assigned doctor*, not the logged-in viewer. |
| DIS-3 | **Discharge medications editor** with the same medicine search/preferences as the Rx pad (freehand meds saved to the doctor's Personal list). **Narrative assist** (AI, `discharge-summary/narrate`) drafts hospital-course text for review — never auto-signed. |
| DIS-4 | **Sign / Unsign:** e-signature (SignaturePad) by the assigned doctor; signed summary is locked; *unsign* requires reason and is audited. Signed date/doctor are shown on print. **InkDischargePad** allows handwritten summary on letterhead. |
| DIS-5 | **Outputs:** A4 print (`dischargeSummaryA4`), PDF upload to storage, **QR** to a public tokenised view (`public-discharge/{accessToken}`), **send via WhatsApp**. |
| DIS-6 | **LAMA:** capture signer name, relation (default Self) and signature; LAMA is a discharge type with its own legal form. |
| DIS-7 | **IRDAI/TPA:** show **TPA split** (payable / non-payable / unclassified lines by payer), **coverage utilisation** (sanctioned vs running total, % and "approaching limit"), **enhancement request & approval** (amount, who/when), and **discharge-process clocks** (milestones with durations: e.g. discharge advised → summary ready → bill ready → final approval → patient out) stamped via `irdai-discharge/stamp-milestone`. |
| DIS-8 | Discharge shall be **blocked/warned** when: unsigned summary, pending MAR doses, unreturned CSSD sets, open consent, unpaid/unfinalised bill (policy-configurable hard vs soft). Final bill and bed release are triggered; bed goes to cleaning. |
| DIS-9 | Read-only for everything except the summary once the admission is not Active. |

### Gaps
| Pri | Gap |
|---|---|
| **P1** | **B-9:** custom field values are kept only in `localStorage` (`discharge-custom-fields:<admissionId>`) — they are not part of the saved/printed legal summary and vanish across devices. Persist server-side. |
| P1 | **Discharge bundle PDF** (summary + bill + investigation reports) as one document with WhatsApp send (`PLAN.md` 1.8) — confirm scope shipped. |
| P1 | Discharge **checklist** enforcement (DIS-8) — confirm which checks are hard-coded vs configurable. |
| P1 | **Death summary / DAMA / referral-out** discharge types and death certificate fields (MCCD/Form 4). |
| P2 | NABH-format summary variants and ICD-10 coded diagnoses mandatory-field policy. |

### Acceptance
- A signed summary cannot be edited without Unsign; Unsign leaves an audit row with reason.
- WhatsApp link opens the tokenised public view without login and expires.
- TPA split totals equal the bill total (payable + non-payable + unclassified).

---

## 6.7 CSSD board (inside IPD switcher)

**Purpose.** Track sterile instrument sets and sterilisation cycles. **Readiness:** ✅
- Sets (`cssd/sets`): state (Sterile / In use / Dirty / In cycle), movement log (issue to OT, return), **sterilisation cycles** (autoclave ID, load, start/end, BI/CI results, pass/fail) with history.
- Surgery integration: sets are **auto-returned** when a surgery case completes; OT item usage links to sets.
- Gaps (P1): cycle failure → quarantine and recall of affected sets; expiry of sterile packs; barcode scan; NABH CSSD register export.
