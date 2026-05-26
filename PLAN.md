# EasyHMS Implementation Plan

> Living document. Updated as phases complete. Anchors UX principles and the SRS gap analysis.

## Scope Decisions
- **Radiology Modules 2 & 3 of the SRS are OUT of scope for EasyHMS.** Radiology lives in a separate system; EasyHMS exposes REST hooks to receive order-completion events and reports. Radiology UI workflows (worklist, dictation, DICOM, AERB dose tracking, critical-value escalation) belong to that other product.
- All other SRS modules (Admission, Clinical Documentation, Pharmacy, Billing, Alerts, Discharge & Records) ARE in scope.

---

## UX Principles (non-negotiable)
1. Role-driven home screens. Nurse → her ward's vitals-due list. Cashier → day's pending bills.
2. Single context, zero re-login. Reports and bills open *inside* the patient chart.
3. Mobile-first for ward staff. Big tap targets, native numeric keypad, voice for free-text.
4. Status as visual primary key. Colored pill + priority sort. No "click to see status".
5. Time is first-class. Human-friendly timestamps. SLA breaches get red badges.
6. Confirm only the irreversible (finalize, discharge, sign). Everything else inline + undo toast.
7. Skeleton loaders, not spinners.
8. Empty states sell the next action.
9. Print formats baked in from day one (Indian GST invoice, NABH discharge summary).
10. Offline tolerant for bedside flows (vitals, MAR, I-O). Service Worker + IndexedDB queue.
11. Audit invisible to user, visible to admin.

---

## Gap Summary
🟢 Done · 🟡 Partial · ❌ Not started

| SRS Module | Status | Notes |
|---|---|---|
| 1. Admission & Registration | 🟡 | Bed/admission backend ✅; consent/MLC/fast-track ❌; bed grid UI ❌ |
| 2. Radiology Order Mgmt | OUT | Separate system |
| 3. Imaging & Reporting | OUT | Separate system |
| 4. Clinical Documentation | 🟡 | Single-shot vitals ✅; round notes / continuous vitals / I-O / nursing scores ❌ |
| 5. Pharmacy & Consumables | 🟡 | Prescription doctor-side ✅; MAR nurse-side ❌; consumables/blood ❌ |
| 6. Billing | 🟢🟡 | Event-driven engine ✅; GST/HSN ❌; producer integrations partial |
| 7. Alerts & Notifications | 🟡 | SMS/WhatsApp infra ✅; in-app push + alert engine ❌ |
| 8. Discharge & Records | 🟡 | Status + LOS ✅; auto-populate summary + bundle PDF ❌; MRD search ❌ |

Rough roll-up: **~8% done / ~22% partial / ~70% not started.**

---

## Phased Roadmap

### Phase 1 — Make IPD usable end-to-end (4–6 weeks) — *active*
| # | Slice | Backend | Frontend |
|---|---|---|---|
| 1.1 | Replace IPD mocks with real APIs | — | `ipdService.ts` rewrite, ward-by-WardCode grouping |
| 1.2 | Real admission flow | (admit/transfer/discharge handlers already exist) | `NewAdmissionSheet` → `/admission/admit`; bed availability grid component |
| 1.3 | Patient workspace reads real admission | — | `IPDPatientWorkspace`, `AdmissionDetailSheet` updates |
| 1.4 | Billing flow UI | — | `BillingPage` add-event → invoice → finalize → payment |
| 1.5 | Doctor round notes | New `RoundNote` entity, multi-doctor/day, 24h lock | SOAP editor in IPD workspace |
| 1.6 | Continuous vitals chart | New `VitalReading` entity (1/2/4-hourly per ward), trend API | Vitals tab with table + trend graph |
| 1.7 | Auto discharge summary | Extend discharge handler to compose summary from chart | Summary review screen pre-sign |
| 1.8 | Discharge bundle PDF | PDF compose service (summary + bill, no DICOM) | "Download Bundle" + WhatsApp send |
| 1.9 | IPD admission/discharge SMS+WhatsApp | Wire existing services into admit/discharge handlers | — |

### Phase 2 — Patient Safety & NABH foundations (4–6 weeks)
- Consent management (general + procedure + IV contrast; digital sign; PDF; gate orders)
- MAR (nurse administration view, missed-dose alerts, high-alert second-nurse verify)
- Drug-allergy + drug-drug interaction check
- Fluid balance I/O + glucose/insulin charts
- Nursing scores (Morse Fall, Braden, MUST)
- Generic audit trail across modules
- PCPNDT USG form (legal)

### Phase 3 — Billing completeness + Pharmacy/Inventory (4–6 weeks)
- GST/tax engine (HSN/SAC, CGST/SGST, GST invoice template)
- Discount approval workflow (HOD log)
- Consumable items + inventory deduction at point of use
- Pharmacy dispensing producer → `BillingChargeEvent`
- Blood products workflow + transfusion record
- Payment receipt format + daily reconciliation
- Revenue analytics

### Phase 4 — Alerts, Operations, MRD (3–4 weeks)
- Alert engine (in-app push channel + SMS/WhatsApp dispatch)
- Operational alerts (EDD breach, deposit low, consent pending)
- MRD search (year/doctor/dept/diagnosis/procedure)
- Equipment master + PM reminders
- MLC handling + injury map
- Fast-track / triage admission
- Attendant & visitor tracking
- IPD analytics dashboard (occupancy, ALOS, readmission %)

### Phase 5 — Radiology integration (separate system) (2–3 weeks)
- REST endpoints in EasyHMS to receive: order-completed, report-ready, critical-finding-flagged
- `BillingChargeEvent` producer when radiology system signals completion
- Patient chart link to external report URL
- WhatsApp delivery of radiology PDF via EasyHMS messaging
- No EasyHMS worklist/dictation/DICOM/dose code

---

## Non-Functional (carried from SRS)
- Page load < 2s on 10 Mbps
- 500-item worklist < 3s render
- 200 concurrent users / hospital
- JWT 8h, AES-256 at rest, TLS 1.3 in transit, 30-min idle logout
- RBAC: Admin · Doctor · Radiologist · Nurse · Billing · Receptionist · Biomedical · Management
- Offline mode for worklist + vitals
- DB backups every 6h
- Audit log immutable
