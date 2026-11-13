# 10 — Pathology Lab

> Back to [index](README.md). Source: `features/pathology/*`, API `PathologyOrderController`, `PathologyCatalogController`, `PathologyConfigController` (all `api/v1/pathology…`, `[RequiresPermission("pathology")]`).

**Roles:** Admin, AdminDoctor, Doctor, **Lab Technician** (`pathology`). The Lab Technician role cannot sign in to the web app today (**B-1, P0**) — the primary persona for this module is blocked until fixed.

**Scope:** in-house clinical-pathology workflow (order → sample → result → verified report → billing) with optional outsourcing to external labs and an optional public (Doctor Dekho) lab listing. Radiology/imaging is out of scope.

### Order lifecycle
```
PLACED ──collect sample──► IN_PROGRESS (per test line: PENDING → resulted → report generated)
   │                              │  ├─ send to external lab → receive external result
   │                              ▼
   └──────── cancel ───────► COMPLETED (all lines reported)           CANCELLED
```
Each **test line has its own independent report** (a multi-test order can have several reports at different times).

---

## 10.1 Pathology Dashboard — `/pathology`

**Shell:** six tabs — **Workspace · Billing · Test Catalog · Keywords · Lab Settings · Letterhead**. **Readiness:** ✅ (🟡 items below)

### 10.1.1 Workspace (worklist)
| ID | Requirement |
|---|---|
| PATH-1 | Worklist of orders with filter chips **All · OPD · IPD · STAT · Completed**, date filter (**default today**; *All dates / Single day / Range*), search; KPI counts **Pending (PLACED) · In progress · Completed**; a failed fetch is distinguished from "no orders" (explicit error + retry). |
| PATH-2 | **Quick row actions without opening the order:** *Mark sample collected*, *Edit order*, *Cancel order*; cancel is disabled for already-cancelled orders and **edit/cancel is blocked once reports exist** (reports-ready count > 0). |
| PATH-3 | **New Lab Order** is **disabled until the lab has a Technician name configured** (Lab Settings → Identity & Sign-off) — the one field that identifies who is accountable for reports; banner "Complete Lab Setup" jumps to that tab. |
| PATH-4 | **Reports ready** list (`reports/ready`) powers the *Lab report ready* badge on the Doctor Board (3.1) — recent reports per patient, newest first, visible to whichever doctor sees the patient. |
| PATH-5 | Bulk preview/print of all reports of an order using the same preview drawer as the order page. |
| PATH-6 | Token print for the patient (`PathologyTokenPrintModal`). |

### 10.1.2 New / Edit Order (full-page flow)
| ID | Requirement |
|---|---|
| ORD-1 | **Patient:** search existing (appointment / IPD / WhatsApp patients are all just patient rows) or register a **walk-in** with name, age/sex, mobile, guardian (same relation-prefix convention as OPD registration). |
| ORD-2 | **Context:** `OPD · EMERGENCY · WALK_IN · IPD`. OPD attaches to the patient's **open OPD visit/invoice** so the lab charge lands on the same bill as the consultation; IPD lists the patient's active admission(s) and bills through the admission; priority STAT/Routine. |
| ORD-3 | **Tests:** checklist from the Test Catalog (and panels); selected tests create order lines and **auto-billing charge events** (via the catalog's linked `ChargeId`); optional referring doctor, clinical notes. |
| ORD-4 | **Edit mode** reuses every step; patient reassignment and test add/remove remain possible whatever the progress, but **unchecking a test that already has a generated report needs a second confirming click** (it deletes that report). |
| ORD-5 | Offline: order creation is not queued (lab money/legal artefact) — **VERIFY** and show a clear message. |

### 10.1.3 Order Detail / Result entry — `/pathology/orders/:orderId`
Deep-linkable page; header with patient/order info and a **single page-level save/status readout** that reflects whichever test tab is active; one tab per **test line**.
| ID | Requirement |
|---|---|
| RES-1 | **Collect sample** per line (time, collector); lines move PENDING → in progress. |
| RES-2 | **Parameter grid** per test with units and **reference ranges** (male, female and child min/max per `PathologyParameterRange`, default value, critical low/high), **automatic abnormal flagging** (Low/High) and **CRITICAL_LOW / CRITICAL_HIGH** flags computed server-side by `PathologyResultFlagCalculator`. A **critical beep** (Web Audio) plays only on the *first* appearance of a critical value (edge-triggered), never on re-render. |
| RES-3 | **Autosave:** results debounce-save silently after the last keystroke; explicit Save does a visible save + refetch; a background flush occurs before switching tabs. |
| RES-4 | **Interpretation / Notes** and hospital-defined **custom fields** per test line, and **Report details** fields once per report, driven by the configured **Field Layout** (editable in a dialog from this page, so a pathologist can add e.g. "Method used" without leaving). |
| RES-5 | **Generate report** (per line): creates the report with technician/pathologist sign-off identity from Lab Settings, renders on the **letterhead**, produces **PDF** (`report/{id}/pdf`); preview, print, download; reports then appear in the patient's Lab Tests tab and on the doctor's badge. |
| RES-6 | **Outsourced tests:** *Send to external lab* (choose lab from External Labs), then *Receive external result* (enter/upload) — the report shows the performing lab. |
| RES-7 | **Keywords:** insert reusable formatted paragraphs (rich text) into interpretation/notes. |
| RES-8 | **Report amend/re-issue:** a generated report is not silently overwritten; an amendment produces a new version marked "amended" (**not evident — gap**). |
| RES-9 | A **critical result must trigger a call-out record** (who was informed, when, read-back) and an in-app alert to the ordering doctor (**gap**). |

### 10.1.4 Billing tab
| ID | Requirement |
|---|---|
| PB-1 | One row per **bill/encounter** (not per charge) with date, patient, order(s), net, discount, paid, balance; IST day buckets; search; paged (10/page) — only the visible page's particulars/discount are fetched, merged into a cache so paging back does not flash "Loading…". |
| PB-2 | **Edit Bill** drill-in lists the bill's charge lines with per-line Edit (shared `EditChargeDialog`) and Remove (void with optional reason) — distinct from whole-invoice cancel (reason required). |
| PB-3 | **New Lab Bill** drawer: identify/register the patient, a fresh LAB encounter is created, then the shared *Add Charges* picker is used to select tests. Collect payment through the shared billing payment dialog (online only). Overview cards mirror the workspace date selector. |
| Gap (P1) | Lab revenue by test/doctor/referrer, referral incentive for lab, home-collection charges. |

### 10.1.5 Test Catalog
Test master per hospital (`pathology/{hospitalId}` create/update): **test code, name, category, sample type, container type, linked charge (`ChargeId`, auto-billing; picker scoped to LAB/ANY charge items) and rate, cost price, outsourced flag with default external lab, sort order, active state**, and the **parameter definitions** (name, unit, default value, male/female/child min–max, critical low/high). **Report templates** (`templates`, with file upload) cover narrative tests.
| ID | Requirement |
|---|---|
| CAT-1 | Validation: unique code per hospital; each range min ≤ max; critical limits outside the normal range; unit required for numeric parameters. |
| CAT-2 | Bulk import/export of the catalog (**gap P1** — manual entry/templates only today). |
| CAT-3 | The rate prefills from the linked ChargeMaster item — one source of truth for price. |

### 10.1.6 Keywords · Lab Settings · Letterhead
- **Keywords:** reusable formatted report paragraphs (`report-keywords`) with rich-text editor.
- **Lab Settings:** lab identity (name, address, registration number, city/state/pincode, public description/phone/email), **Technician name (required — gates new orders)** and **Pathologist name** for sign-off, test categories, report footer, **Doctor Dekho public-listing opt-in** with GPS pin (device location or manual) — every save re-reads the current `LabConfiguration` row and spreads it into the write so fields the screen does not own are never blanked.
- **Letterhead designer:** report letterhead/print layout (template, page margins, font family/size, header/footer, logo); same designer family as prescription/discharge; **Field Layout editor** for report/test fields; **external labs** manager.

### Gaps (module level)
| Pri | Gap |
|---|---|
| **P0** | **B-1:** Lab Technician cannot use the web app. |
| P1 | **Sample accessioning**: barcode label print/scan per tube, sample rejection with reason, sample tracking (collected → received → processed). Only "collected" exists. |
| P1 | **Critical value policy** (RES-9), **delta check** vs previous result, **two-step verify** (technician enters → pathologist authorises) — a single technician identity signs today. |
| P1 | **QC/Westgard** records, reagent lot tracking linked to Inventory, instrument interface (LIS/analyser HL7/ASTM) — fully manual entry today. |
| P1 | **NABL-style** report fields (specimen collected/received/reported times, method, accreditation mark). |
| P1 | Patient-facing delivery of reports (WhatsApp/SMS link with tokenised public view) and doctor notification. |
| P1 | `ReportTemplateManager`, `ReportKeywordsManager`, `RichTextField` must sanitise rich text (stored HTML → printed/PDF) against script injection. |
| P2 | Home sample collection scheduling; package/health-check profiles; TAT dashboards. |

### Acceptance
- Order with 3 tests → 3 independent reports; editing one result autosaves without disturbing the others; critical value triggers one beep and a visible critical banner.
- Cancelling an order with a generated report is blocked; unchecking such a test needs the second confirm.
- OPD lab order appears on the same invoice as the consult; IPD order bills through the admission.
