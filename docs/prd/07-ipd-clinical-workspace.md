# 07 — IPD Patient Workspace (per-admission clinical record)

> Back to [index](README.md). Route: `/ipd-workspace/patient/:id` (also embedded from `/ipd-workspace` rows; OT/ICU boards deep-link with `?tab=surgery` / `?tab=criticalCare`). Source: `screens/PatientWorkspace.tsx` and `components/*Panel.tsx`, API controllers listed per section.

**Roles:** Admin, AdminDoctor, Doctor, Nurse, Coordinator (`ipd`). Several clinical controllers behind this page carry **no** `[RequiresPermission]` (B-4) — closing that is a precondition for production use.

**Purpose.** One place for everything about one patient's stay: bed, orders, medication administration, nursing documentation, notes, consents, documents, blood, surgery, critical care, and discharge. Mobile-first for ward staff.

**Shell.** Two-level side nav (desktop) / section-picker sheet (mobile) — per the IA rule: *new top-level clinical sections get their own sidebar item; only genuinely-same-concept things nest under an existing parent.*

```
Overview · Admission Details · CPOE ▸ (Medication, Lab, Procedure, Diet, Nursing) · MAR
· Nursing ▸ (Vitals, I/O & Glucose, Assessment, Care Plan, Restraint)
· Round Notes · SBAR Handover · Consent · Documents · Blood Bank · Surgery · Critical Care · Discharge
```
The whole workspace is **read-only for bed/order/nursing actions once the admission is no longer in an Active status.**

**Readiness (whole page):** 🟡 — feature-complete across sections; security (B-4), localisation (B-11), tests (B-12) and the items below are open.

---

## 7.1 Overview
Patient/admission header (name, age/sex, UHID, admission no., type, bed/ward, doctor, LOS, payer/entitlement), **deterioration banner** (shows when the latest NEWS2-style Early Warning Score is Medium/High, or a Rapid Response is open — renders nothing otherwise), allergy banner, active alerts, quick actions (transfer bed, change doctor, print admission, request pharmacy items), billing summary link (`/billing/encounter/:id`).
| ID | Requirement |
|---|---|
| WS-1 | The banner and header shall render within 2 s from cached data and refresh in the background. |
| WS-2 | Opening from the OT/ICU boards lands on the relevant section directly. |
| WS-3 | Mobile: compact one-line metadata rows (icon + label left, value right, truncating). |

## 7.2 Admission Details
Edit admission after the fact: **primary doctor change** (history kept — `admission/doctor/history`), **referrer change** (history), **coverage** (`PUT admission/coverage`: payer type, sanctioned amount, policy), diagnosis/notes, details edit sheet. Requirements: every change is versioned (who/when/old→new), room entitlement re-evaluated, billing consultant attribution handled for the period (consultant ledger).

## 7.3 CPOE — Clinical Orders (`clinical-order`)
One reusable `ClinicalOrderPanel` for **Medication, Lab, Procedure, Diet, Nursing** orders (Radiology is out of scope — hand-off only).
| ID | Requirement |
|---|---|
| CPOE-1 | New order picks an item from the charge master category for that type (`itemPickerCategoryCodes`); Medication orders use a **fixed frequency dropdown** (drives the MAR's computed schedule); other types keep free-text frequency. |
| CPOE-2 | Order lines have start/stop, dose/route/instructions, priority (routine/STAT), **discontinue line** with reason (never delete). |
| CPOE-3 | Ongoing services (oxygen therapy, continuous monitoring) **accrue daily** instead of once, posting a charge per day to the encounter. |
| CPOE-4 | **Order sets** (`order-set`) and **post-op order sets** place several orders in one go by calling the same place-order API once per order type. |
| CPOE-5 | Placing a Medication order notifies the MAR; a Lab order creates a pathology order/bill line (see 10) and results flow back to the chart. |
| Gap (P0) | **No drug–allergy/interaction/duplicate-therapy check at order entry** (same root cause as 4.3). |
| Gap (P1) | Verbal/telephone order read-back workflow with co-sign; order expiry (antibiotic stop dates); pharmacist verification step before dispensing. |

## 7.4 MAR — Medication Administration Record (`mar`)
| ID | Requirement |
|---|---|
| MAR-1 | One IST calendar day at a time, grid of **computed dose slots** per active Medication order; status computed purely server-side on view (`PENDING` / `DUE` / `OVERDUE` / `MISSED` / `ADMINISTERED` / `HELD` / `REFUSED` / `PATIENT_NOT_AVAILABLE`) — nothing is persisted until a nurse acts. |
| MAR-2 | A nurse may act on a slot that is due/overdue/missed, or a pending one ahead of time (slightly early); **not** re-act on a resolved slot. Record: administered / held / refused / patient-not-available, actual time, site, notes. |
| MAR-3 | **High-alert / narcotic drugs require a witness name** (second-nurse verification) before the record is accepted; narcotic dispenses also write the **narcotic register**. |
| MAR-4 | Missed-dose and overdue counts feed the Nursing Station tiles and alerts. |
| MAR-5 | Offline: recording a dose may queue (bedside flow) with the *actual administered time*; syncing must not alter it. **VERIFY**. |
| Gap (P1) | Barcode scan of patient wristband + medicine (5 Rights), PRN with effect-check reminder, IV infusion rate/volume tracking, pharmacy-to-MAR dispensing trail. |

## 7.5 Nursing sub-sections
- **Vitals** (`vitals`): record readings (BP, pulse, resp. rate, SpO₂, temperature, GCS, pain 0–10, weight/height/BMI); table + **trend chart**; per-ward frequency (1/2/4-hourly) due list; **NEWS2-style Early Warning Score** (`vitals/ews`, autofills from latest vitals; history) with escalation guidance and a link to Rapid Response. Not ICU-gated — a deteriorating ward patient must be flagged *before* ICU.
- **I/O & Glucose** (`fluid-entry`, `glucose-reading`): intake/output entries with volume, route, type; **net balance**; capillary glucose chart with insulin notes.
- **Assessment** (`nursing-assessment`): **Morse Fall**, **Braden**, **MUST** scores computed from the entered fields, each with a risk badge; history list.
- **Care Plan** (`nursing-care-plan`): problems, goals, interventions; **resolve** action; evaluation notes.
- **Restraint** (`restraint`): order, type, site, reason, monitoring, **release** with time — regulatory register.
| ID | Requirement |
|---|---|
| NUR-1 | Every entry stores author, timestamp (IST) and is append-only; corrections are addenda. |
| NUR-2 | Out-of-range vitals are highlighted and, per policy, raise an Alert (alerts engine). |
| NUR-3 | Mobile numeric keypad inputs; ≥ 44 px targets; works offline with sync status. |
| Gap (P1) | Pressure-injury turning schedule, fluid-restriction targets, wound care, central-line/Foley care bundles in the ward (ICU has bundles), nurse-to-nurse task list. |

## 7.6 Round Notes (`round-note`)
SOAP-style notes, **multiple per doctor per day**, **editable for 24 hours** (`EDIT_LOCK_HOURS = 24`); after that only **addenda** can be added. Requirements: lock enforced server-side (client check is advisory); co-sign for residents; template snippets; voice dictation (Voice Rx engine) — gap P2.

## 7.7 SBAR Handover (`shift-handover`)
Structured Situation / Background / Assessment / Recommendation handover per shift; **acknowledge** by the incoming nurse; history. Requirement: unacknowledged handovers at shift change raise an alert; the Nursing Station shows handover status per patient. Shift definitions: see 8.3 (currently browser-local — B-8).

## 7.8 Consent (`consent-template`, `consent-record`)
Capture patient/relative consent against hospital templates (Admission → `GENERAL_ADMISSION`; procedure; anaesthesia; blood transfusion; high-risk) with name/relation/witness and a **signature pad**; PDF/print. Templates managed in Admin → Consent Templates. Requirements: consent is a **gate** for the corresponding order/procedure (surgery cannot move to IN_THEATRE without a procedure consent — policy); revocation; language variants. Gap (P1): enforcement gating is not evident; MLC/PCPNDT forms.

## 7.9 Documents (`admission/document/*`)
Upload/list/delete admission documents (ID proofs, insurance, outside reports) via `AdmissionDocumentsPanel` in the `admissiondocuments` container. Requirements: type tagging, size/type limits, signed URL access, audit of delete. (Container naming: `{entityId}_admissiondocuments`.)

## 7.10 Blood Bank (`blood-bank`)
| ID | Requirement |
|---|---|
| BB-1 | Hospital **blood-bag pool** (component, group, volume, expiry) with search; bags **reserved** for an admission, **discarded** (reason), **transfused**. |
| BB-2 | **Transfusion record** captures volume given, start/stop, **witness name**, **vitals before and after**, and **reaction** (type/severity incl. anaphylaxis) — mandatory fields before saving. |
| BB-3 | The pool query already excludes expired bags; the UI flags the soonest-expiring unit so staff prefer it (FEFO). |
| BB-4 | Ledger (`blood-bank/ledger`) and per-admission history; billing posts the blood-product charge. |
| Gap (P1) | ABO/Rh **cross-match / compatibility check** against the patient's group (hard stop on mismatch), cold-chain temperature log linkage, haemovigilance report. |

## 7.11 Surgery (`surgery-case`)
| ID | Requirement |
|---|---|
| SUR-1 | **Request** a case (procedure name — prefilled from the admission's OT Plan —, urgency Routine/Urgent/Emergency, surgeon, surgical team, anaesthetist, ASA grade, anaesthesia type). |
| SUR-2 | Forward-only status machine **REQUESTED → SCHEDULED → PRE_OP → IN_THEATRE → POST_OP → COMPLETED**, with **CANCELLED** from any non-terminal state (reason mandatory). |
| SUR-3 | **Pre-op assessment**, then the WHO checklist: **Sign-in**, **Time-out**, **Sign-out**, each stamped with user/time. |
| SUR-4 | **Intra-op:** incision and closure times, procedure performed (actual), findings, complications, **estimated blood loss**; **item usage** (consumables/implants) with *deducted from inventory* and *billed* flags; **implant log** (batch/serial) searchable (`implant-log/search`). |
| SUR-5 | **CSSD:** issue a sterile instrument set to the case; sets are **auto-returned** on completion. |
| SUR-6 | Print the **OT record** (`surgeryCaseA4`); post-op order set dialog after COMPLETED/POST_OP. |
| Gap (P1) | Anaesthesia record (drugs, airway, monitoring), swab/instrument count, specimen tracking, OT utilisation/turnover analytics, PCPNDT/MTP forms. |

## 7.12 Critical Care (`icu`, `early-warning`, `rapid-response`, `infection-events`, `devices`)
| ID | Requirement |
|---|---|
| ICU-1 | **Level of care** (Level 1 Ward+monitoring / 2 High dependency / 3 Intensive) with history and rationale; drives the ICU Board column. |
| ICU-2 | **APACHE II** and **SOFA** scores: autofill from labs/vitals where available (ABG/PaO₂/FiO₂ always start blank), history list, and a **trend flag** (rising/falling) beside the chart. |
| ICU-3 | **Ventilator** settings (mode incl. AC and PSV, FiO₂, PEEP, rate, volumes) with history; **weaning** assessments (readiness, SBT) with history. |
| ICU-4 | **Devices** (central line, urinary catheter, ventilator, etc.) with insertion site/date and **care-bundle compliance check** (`devices/bundle-check`) — e.g. VAP/CLABSI/CAUTI bundles. |
| ICU-5 | **Infection events** register with rate summary (`infection-events/rate-summary`); **Rapid Response**: raise → arrive → resolve with times; open responses surface on the Overview banner and ICU Board. |
| ICU-6 | **Vasopressor support**, GCS, pain scoring captured and trended. |
| Gap (P1 — ICU redesign Phase 3 remainder) | Pain/RASS/CAM-ICU sedation & delirium scoring, early mobility, **family conference** log. |
| Gap (P1) | Hourly flowsheet integration with bedside monitors (device feed) — manual only today. |

---

## 7.13 Cross-section requirements
| ID | Requirement |
|---|---|
| WS-X1 | **Authorization:** every endpoint above shall enforce `ipd` (and where relevant `nursing_station`/`icu_board`/`ot_board`) server-side (B-4). |
| WS-X2 | **Audit:** all clinical writes append-only with author; unsign/discontinue/cancel carry reasons. |
| WS-X3 | **Alerts:** missed meds, EWS Medium/High, critical lab, rapid response, bundle breach raise `Alert` rows (alerts engine) with acknowledge/snooze/dismiss. |
| WS-X4 | **Performance:** workspace opens < 2 s on 10 Mbps; each section lazy-loads its data; background polling pauses when tab hidden. |
| WS-X5 | **Localization:** none of these panels are i18n'd (B-11) — Hindi coverage is expected for ward staff. |
| WS-X6 | **Testing:** golden-path e2e (admit → order → MAR dose → vitals/EWS → discharge) and API tests for state machines (surgery, restraint, blood bank). |
