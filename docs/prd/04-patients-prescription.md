# 04 — Patients, Patient Profile & E-Prescription

> Back to [index](README.md). Source: `features/patient/*`, `features/prescription/*`, `features/doctor/services/prescription*`, API `PatientController`, `PatientProfileController`, `EPrescriptionController`, `PrescriptionController`, `PrescriptionSettingsController`, `MedicinesController`, `VisitSummaryPublicController`, `PrescriptionAttachmentPublicController`.

**Roles:** Admin, AdminDoctor, Doctor (`patients`). Receptionist/Nurse do **not** hold `patients` (they work through booking/IPD).

---

## 4.1 Patients — `/patients`

**Purpose.** Hospital-wide patient directory and the entry to a patient's chart. Also embedded as "Patient Management" in the Admin Panel. **Readiness:** 🟡

**Five sections** (side/segment nav): **Current Appointments**, **Upcoming Appointments**, **Past Appointments**, **Patient 360**, **Referrers**.

### Functional requirements
| ID | Requirement |
|---|---|
| PT-1 | **Current/Today:** today's patients with doctor, token, status progress (Vitals → Ready → Consult → Lab → Done), per-doctor counts, search and filters. **Upcoming:** default window tomorrow → +7 days. **Past:** default last 30 days → yesterday. Each has its own date range. |
| PT-2 | **Patient search** is the shared fuzzy search over name / patient ID / mobile / Aadhaar / ABHA (same engine as booking). |
| PT-3 | Opening a patient navigates to the chart `/patient/:id` (4.2) or the prescription workspace `/patient/new?patientId=…&appointmentId=…&date=…`. |
| PT-4 | **Patient 360** (analysis view): per-patient Total Visits, Visit Frequency, Last Visit, Follow-up Status and a chart of visit frequency; list of the patient's doctors and visit counts with status breakdown. |
| PT-5 | **Merge duplicates** (button): admin picks a *source* and a *target* patient (each via search), reviews impact counts (`GET patient/record-counts`) and confirms; the system repoints **13 dependent tables** to the target, marks the source `MergedIntoPatientId` (audit), and hides the source from search. Irreversible without DB intervention. |
| PT-6 | **Referrers panel:** list/search/add/edit referrers (doctor or other; name, phone, address, type); used by booking and IPD admission; referrer incentive reporting lives in billing/IPD ledger. |
| PT-7 | Registration rules (shared with booking): mobile 10-digit (first ≠ 0), duplicate probe before create, ABHA link status, guardian/relative stored on the patient. |

### Business rules
- Patient ID is system-generated and hospital-scoped; a patient may exist without any appointment (IPD pre-registration, walk-in pathology/pharmacy, ABHA creation).
- Merge target wins on conflicts; source's allergies, history and documents move to the target. Merge shall be restricted to Admin/AdminDoctor and fully audit-logged (**VERIFY** restriction — endpoint carries only the `patients` key, which Doctor also holds).

### Gaps
| Pri | Gap |
|---|---|
| **P1** | `PatientsPage.tsx` is 2,100 lines with fields marked "Default/Placeholder values for fields not in API yet but required by UI/Sort" (line ~412) — enumerate which columns show defaults rather than data and remove or populate them. |
| P1 | Merge must be role-restricted to Admin/AdminDoctor and require a typed confirmation; add an **undo window** or at least a downloadable pre-merge snapshot. |
| P1 | A true **patient master list** (all patients, filters by age/sex/blood group/last visit/ABHA/flags, CSV export) does not exist — the page is appointment-centric. Needed for follow-up campaigns and the lapsed-patient feature. |
| P1 | No patient **tags/flags** (VIP, MLC, infectious, DNR) with banner everywhere the patient appears. |
| P2 | Patient merge preview (diff of fields) and bulk duplicate sweep with suggested pairs. |

---

## 4.2 Patient Profile / Prescription workspace — `/patient/:patientId`, `/patient/new`

**Purpose.** A doctor's working chart for one patient during a visit. `/patient/new?patientId=&appointmentId=&date=[&tab=inkrx]` opens the visit for that appointment; `/patient/:id` opens the chart. **Readiness:** ✅

### Layout & requirements
| ID | Requirement |
|---|---|
| PP-1 | **Header:** patient identity, age/sex, blood group, mobile, email, emergency contact; **allergy banner** (new Allergies column) shown prominently; **risk badge** derived from real data — age, recorded allergies, distinct chronic conditions across visits, polypharmacy at the latest visit. |
| PP-2 | **Cross-doctor timeline:** all visits across doctors with doctor name, status, vitals, diagnoses, medicines; a visit's prescription can be previewed with *its own* date (not today's). |
| PP-3 | **Tabs inside the pad:** Timeline, **Lab Tests** (full report-attachment history assembled from the timeline across visits + current appointment, de-duplicated, current wins), **Prescription fields**. |
| PP-4 | Back navigation returns to the originating list (Doctor Board past-visits, Patients). |
| PP-5 | Quick launch of **InkRx** (handwritten, 4.4) and **Voice Rx** (4.5) from the toolbar; **Advise Admission** (4.7). |
| PP-6 | Subscription read-only blocks saving the prescription but allows reading history. |

### Gaps
| Pri | Gap |
|---|---|
| P1 | A parallel older component `PatientProfile.tsx` (vitals "for now we'll just log it") is **not imported anywhere** — delete (B-13). |
| P1 | Problem list / chronic conditions and **immunisation** records as first-class, editable chart sections (today inferred from visits). |
| P2 | Growth charts for paediatrics, pregnancy/ANC tracking. |

---

## 4.3 E-Prescription Pad — inside `/patient/new`

**Purpose.** Structured prescription authoring with autosave, personalised quick-picks, and printable/QR-verifiable output. The most used clinical surface. **Readiness:** 🟡

### Sections (each can be renamed, hidden, reordered per doctor, plus custom fields)
Vitals · Chief complaint · History · Comorbidity · Examination · Diagnosis · Investigations · Procedures · Medications · Advice · Follow-up · Referral · Attachments & drawings.

### Functional requirements
| ID | Requirement |
|---|---|
| RX-1 | **Autosave:** 2 s debounce, only sections that actually changed are sent; each section (and each custom field) shows its own saving/saved/error pill; a failed section keeps its stale reference so the next edit retries it. A draft is created on first save (draft ID from server). |
| RX-2 | **Lookup/quick-pick:** per-section search over hospital lookup data (`lookup/details`, `lookup/search`) plus the doctor's personalised items; free-typed items can be saved to the doctor's personal list. |
| RX-3 | **Medicines:** search the master catalog and the doctor's preferences (`medicines/search`); selecting shows strength/forms and an ingredient-level enrichment (RxNorm/RxNav cross-reference, US-name synonym, available strengths/forms) when NLM has a match; free-typed medicines are stored to the doctor's preference list "Personal" so they appear next time (also from Discharge Medications). Fields per line: dose, frequency, duration, route/instructions. |
| RX-4 | **Vitals** prefilled from the front desk; editable; BP classified (Low / Optimal / Normal / Grade 1–3 hypertension, highest severity first, explicit hypotension check first). |
| RX-5 | **Field layout** is the doctor's personal layout applied globally (rename / show-hide / drag-reorder / custom fields of types text, paragraph, number, date, boolean, select), and the **print** respects the same order and renders custom fields. |
| RX-6 | **Actions:** Save draft, **Submit/Generate prescription** (`details/generate-prescription` → PDF with letterhead and QR to `/verify/:appointmentId`), preview, print, download, attach/upload documents and drawings, upload visit summary, send to patient (WhatsApp/share — **VERIFY**). Submit moves the appointment toward COMPLETED per the board's Complete-Consult flow. |
| RX-7 | **Letterhead:** hospital/doctor letterhead and template designer under Admin → Prescriptions (templates, assets upload, per-doctor field layout). |
| RX-8 | **Translate** helper (`api/v1/translation`) to render advice in the patient's language (Field Translation Tool). |
| RX-9 | **Personalised medicine** and **personalised data** CRUD (`configuration/personalized-*`). |
| RX-10 | The pad shall work on a tablet in portrait with the keyboard open (no obscured save indicator). |

### Gaps — clinical safety (highest priority in this module)
| Pri | Gap |
|---|---|
| **P0** | **No drug–allergy or drug–drug interaction check exists.** Patient allergies are captured and shown in a banner, but nothing cross-checks them against the medication list (no interaction/allergy classes found in the web code or the API when searched for `DrugInteraction`/`DrugAllergy`; `PLAN.md` lists it under Phase 2). Doc AI's interaction answers are canned (3.6). Minimum viable before go-live: allergy-class match against selected medicines with a hard stop + documented override reason, and a curated high-risk combination table; later a licensed interaction database. **VERIFY** nothing exists under other names. |
| P1 | Dose-range and **paediatric/renal/hepatic** dose checks; max daily dose warnings. |
| P1 | A leftover `medicationLookupData: any[] = []` stub ("to fix Cannot find name" error) is dead code — remove; and `console.log('Converted field configs')`-style logs (B-6). |
| P1 | `EPrescriptionPad.tsx` is 5,700 lines — split before adding features; add unit tests around autosave diffing (the riskiest logic: stale-ref retry, per-custom-field status). |
| P1 | **Prescription immutability:** once generated, edits should create an addendum with version history; verification page must show the version that was issued. |
| P2 | e-sign (DSC/Aadhaar eSign) on prescriptions; ABDM health-record push (HIP) of prescriptions. |

### Acceptance
- Typing in any section triggers exactly one save per 2 s idle window; killing the network mid-save shows `error` on that section only and recovers on reconnect without data loss.
- A prescription generated from the pad verifies at `/verify/:appointmentId` with the same content/date.

---

## 4.4 InkRx — handwritten prescription

**Purpose.** Doctors who write by hand can write directly on the hospital letterhead. **Readiness:** ✅
- Full-screen A4 canvas over the doctor's letterhead; draggable floating toolbar (position remembered in localStorage — a UI convenience only); pen/eraser/colours/undo; multiple pages (PDF first-page rasterisation for imported letterheads).
- **Dual save:** PNG → `drawings` and PDF → `attachments` (category "Prescription", via jsPDF), so the handwritten Rx follows the same retrieval/print path as scraped or uploaded prescriptions.
- Requirements: stylus/palm rejection on tablets; autosave of strokes; one-tap "Save & finish" that also completes the consult; handwriting stays readable when printed at 300 dpi.
- Gap (P1): pressure-sensitive strokes and per-stroke undo are only as good as the canvas; test on Android Chrome/iPadOS Safari.

## 4.5 Voice Rx

**Purpose.** Dictate → structured prescription. **Readiness:** ✅ Phase 1; requires server key `OpenAI:ApiKey`.
- Record with the browser's built-in noise suppression/echo cancellation (mono, small file) → upload → **Whisper STT** + **gpt-4o-mini** structuring → **review screen** → doctor *applies* selected parts to the pad. Consult mode with diarization labels ("Speaker 0/1") shown as chips.
- Requirements: explicit mic-permission UX and a visible recording indicator; nothing auto-applied without review; audio deleted after transcription unless the hospital opts in; transcript never auto-saved to the chart; fall back gracefully if the key is missing (feature hidden, not erroring).
- Gap (P1): PHI is sent to a third-party model — needs a hospital-level opt-in and data-processing terms (XC-4); accuracy for Indian-English/Hinglish drug names needs an evaluation set; no offline mode.

## 4.6 Attachments & documents

**Purpose.** Upload/view lab reports, outside prescriptions, scans against an appointment/patient. **Readiness:** ✅
- Reusable `AttachmentsSection`: list + preview pane (desktop split; mobile full-area slide-over), upload (same file twice re-triggers), auto-select the newest upload, secure blob preview via signed URL, delete.
- Uploader may be front desk; the attachment records the **treating doctor** (appointment's doctor), not necessarily the logged-in user.
- Requirements: size limit (proposed ≤ 25 MB) and allowed types (PDF, JPG, PNG, HEIC) enforced server-side; virus scan on upload (**not present**, P1); signed URLs expire; access limited to the hospital; patient-portal (Health Locker) documents remain separate and mobile-scoped.
- The *Lab Tests* tab also pulls pathology-generated reports automatically (see 10) via `PathologyInvestigationLabSync`.

## 4.7 Advise Admission (doctor → IPD)

**Purpose.** A doctor flags that a patient needs admission, optionally attaching an **OT Plan**; it appears on the IPD **Referred Admissions** board (6.3) for front-desk follow-up. **Readiness:** ✅
- If the patient already has a PENDING advice the sheet pre-fills it and **edits in place** (no duplicate referral per reopen).
- Status model: PENDING → CONVERTED (stamped atomically when the patient is actually admitted) / NOT_ADMITTED / FOLLOW_UP; comments thread; badge shown on the doctor's row (DB-5).
- Gap (P1): notify IPD desk (in-app alert + optional WhatsApp) at the time of advice; SLA for follow-up.

## 4.8 Prescription verification — `/verify/:appointmentId` (public)

**Purpose.** Anyone scanning the Rx QR can confirm the prescription is genuine. **Readiness:** 🟡
- Loads the signed visit-summary PDF for the appointment; shows an error "Unable to verify prescription at this time" when unavailable.
- Requirements: show *verification metadata* (hospital, doctor, registration number, date, issued/revoked) before/instead of the full clinical content; use an unguessable verification token rather than the raw `appointmentId` (enumeration risk); rate-limit; audit access; support revoke.
- Public companion endpoints: `public-visit-summary/{appointmentId}`, `public-prescription/{attachmentId}`, `public-discharge/{accessToken}` — all shall be token-based and expire.
