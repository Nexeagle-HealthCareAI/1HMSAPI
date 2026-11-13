# 05 — Billing

> Back to [index](README.md). Source: `features/billing/*`, `printTemplates/*`, API `BillingController`, `ChargeController`, `PaymentController`, `CreditApprovalController`, `ExpensesController`, `InvoiceSettingsController`, `DoctorFeesController`, `ConsultantIncentiveController`.

**Roles:** Admin, AdminDoctor, Receptionist, Nurse, Doctor, Accountant (`billing`). Landing for Accountant.

### Core model (shared by all pages)
- **Encounter** = one visit/stay (OPD, IPD, ER, LAB, PHARMACY). **Charge event** = one posted line (service, bed-day, medicine, lab…), tagged with an origin (e.g. `LAB_PATH`) and a department (OPD/IPD/LAB/PHARMACY/OTHER, via `BillingDepartmentClassifier`). **Invoice** = a bill over an encounter's charges (DRAFT → FINALIZED; may be cancelled); an encounter may have several invoices over time. **Payment** types: `PAYMENT`, `ADVANCE`, `REFUND`; modes: Cash, UPI, Card, Bank Transfer, Insurance.
- **Event-driven engine:** clinical modules post charges (consult, bed, order, pharmacy, lab); Billing Policy decides auto vs manual posting per module. GST (HSN/SAC, CGST/SGST) snapshot comes from the ChargeMaster item.
- **Discount/refund/advance** above thresholds are **held as PENDING credit approvals** for Admin/AdminDoctor.

---

## 5.1 Billing Dashboard — `/billing`

**Purpose.** Management view of revenue, spend, trends and approvals. **Readiness:** ✅ (🟡 items below). Four cards/tabs: **Revenue**, **Expense**, **Analytics**, and (Admin/AdminDoctor) **Approvals**.

### 5.1.1 Revenue tab
| ID | Requirement |
|---|---|
| BIL-1 | Default scope is **today** (IST day) so staff land on the day's billing; filters: day / range / all, status (draft, finalized, paid, partially paid, unpaid, cancelled), visit type, free-text search (patient, invoice no.). |
| BIL-2 | KPIs reflect the *current filtered view* and show a scope chip: **Billed, Collected, Unpaid/Outstanding**, discounts. |
| BIL-3 | Rows are **grouped by patient** (one row per patient, visits expand underneath) with roll-ups: current bill = most recent invoice; past-due = outstanding on the rest; patients with an open bill sort first. |
| BIL-4 | Clicking a patient opens the **Patient Ledger** (5.2) on the current visit. |
| BIL-5 | Bucketing uses IST calendar days; naive timestamps are treated as UTC (known source of midnight bugs — centralise, XC-5). |

### 5.1.2 Expense tab
| ID | Requirement |
|---|---|
| EXP-1 | Record hospital expenses with category (the built-in buckets include Food / canteen / refreshments, Pharmacy purchase (medicine stock), Equipment, Consumables and Other), amount, date, vendor/notes, payment mode. |
| EXP-2 | **Bulk add** (spreadsheet-style dialog) and edit/delete; search debounced 450 ms, custom dates 600 ms; totals by category and period. |
| EXP-3 | Expenses roll into the analytics profit view. |
| Gap (P1) | No attachment (bill photo) on an expense, no approval workflow, no recurring expenses, no link to GRN/PO payments (pharmacy purchase is entered twice). |

### 5.1.3 Analytics tab
| ID | Requirement |
|---|---|
| ANA-1 | Revenue by **department** (OPD / IPD / LAB / PHARMACY / OTHER), by day/week/month, collections vs billed, payment-mode mix. |
| ANA-2 | **Nexeagle AI Predictive Analysis:** deterministic server-side numbers (`BillingTrendCalculator` — a flat trend-continuation daily rate) plus an LLM-written narrative fetched **once per visit to the tab**; forecast cards for Tomorrow / Week with "gap vs recent". |
| ANA-3 | Every number shall be reproducible from ledger data; the AI text may only *describe* those numbers, never invent them. |
| Gap (P1) | The forecast is a naive flat-rate model — label it as such ("trend continuation") or improve (day-of-week/seasonality). Show model/date stamp on the AI insight; disable when the external model is unavailable rather than showing stale text. |
| Gap (P2) | Doctor-wise and referrer-wise revenue, TPA/insurance aging, GST summary (GSTR-1 style) export, daily cash reconciliation. |

### 5.1.4 Approvals tab (Admin/AdminDoctor)
| ID | Requirement |
|---|---|
| APR-1 | Hospital-wide list of **ADVANCE / REFUND / DISCOUNT** credit approvals (the same card used per-visit on a patient's ledger without the encounter filter); default shows PENDING; history by status. |
| APR-2 | Admin **approves/rejects with a reason**; approval posts the held payment/discount; rejection leaves the ledger unchanged; requester is notified. |
| APR-3 | A discount that would reduce NetAmount **below what is already collected**, or a refund/advance that would leave the patient with a **credit**, is held automatically (not applied). |
| Gap (P1) | Approval thresholds (₹/%) and who may approve should be configurable per hospital (Billing Policy), and approvals must be audit-logged with approver identity (XC-4). |

---

## 5.2 Patient Billing Ledger — `/billing/ledger`, `/billing/:appointmentId`

**Purpose.** The cashier's workspace for one patient: all visits, charges, invoices, payments. **Readiness:** ✅

### Functional requirements
| ID | Requirement |
|---|---|
| LED-1 | **Patient picker** (search) → list of the patient's visits (OPD/IPD/ER/LAB/PHARMACY) with cancelled ones dimmed; lands on the **current bill** (most recent open — not finalized, not cancelled — else most recent). A deep link with an appointment/encounter auto-selects. |
| LED-2 | **Add charge** dialog: sources = *Charge Master* item, *Manual* (name, rate, qty, optional GST/HSN overrides, per-charge incentive), *Bed* (pick bed + from/to date → inclusive day count drives quantity); **"+ Add another"** stages several then submits in one call; optimistic row with rollback; queueable offline against an already-synced encounter. |
| LED-3 | **Edit/cancel charge** (before invoicing; cancelled charges are voided, not deleted). Posted-and-invoiced charges are locked. **Cancel entire encounter charges** is a distinct, explicit action (voids every charge on the latest encounter). |
| LED-4 | **Payments:** *Take payment*, *Advance*, *Refund* with mode (reference/UPI id/card last-4 for non-cash). "Balance due — tap to fill". **Refund** is allowed only up to the **raw ledger credit** (billed vs collected), never against a discount; arriving from the credit banner pre-selects Refund with the amount for review. Refund/advance-induced credit is routed to approval (APR-3). Payments/refunds are **online-only**. |
| LED-5 | **Discounts:** per-line and invoice-level; invoice-level discount is shown separately on print; a discount that would breach collected amount is held for approval. |
| LED-6 | **Finalize** locks the invoice: first re-links any posted-but-unlinked charges so none are left off, then finalizes; no charges/payments until **Reopen** (reason mandatory, audit). **Delete invoice** soft-cancels a specific invoice (draft or finalized) and voids all its charges. Invoice **history** lists every draft/finalized/cancelled invoice for the encounter. |
| LED-7 | **Backdated visit date:** an override date makes every charge/invoice on this visit use that service date instead of "now" (the real keyed-in time is kept as audit time). |
| LED-8 | **Day-wise interim billing** (opt-in, anchored to the visit; for IPD also admission-anchored 24-hour windows with locked snapshots): *close day* / *reopen day*, per-day print of the interim bill. |
| LED-9 | **Documents:** Invoice (A4 / 80 mm thermal), Receipt per payment, Bill-cum-Receipt, consolidated Payment Statement, Interim day bill — all with hospital header, GSTIN, HSN/GST breakup, discount lines, bed-day period lines. Print and PDF download. |
| LED-10 | **Credit banner:** if the patient holds real collected credit it is shown at the top with a one-click path to refund. |
| LED-11 | Read/print remain enabled when the subscription is read-only; all writes are disabled with the upsell modal. |

### Business rules
- Net payable = billed − line discounts − invoice discount (+GST per item). `due` nets invoice-level discount; **refundable credit does not** (never refund a discount).
- Incentives: per-service flat ₹ from `ChargeMaster.IncentiveAmount` (editable at bill time) accrues to the referrer/consultant and appears in the Consultant Ledger (6.5).
- Pharmacy and lab post to the same ledger: pharmacy via direct cash *or* "post to admission day-bill"; lab via `LAB_PATH`-origin charges.

### Gaps
| Pri | Gap |
|---|---|
| P1 | **Day-wise interim billing: frontend + print pending** per project notes (backend done in `easyHMSAPI`) — verify the Close/Reopen UI and `interimBillA4` print are shippable. |
| P1 | **Insurance/TPA billing:** payment mode "Insurance" exists, but there is no **pre-auth, claim, co-pay/room-rent-cap split, settlement/short-pay reconciliation** on the ledger. IRDAI discharge clocks and the TPA-split API exist (see 6.6) — wire the ledger to them. |
| P1 | **Package billing** (surgery packages, health checks) via `package-type` master — confirm end-to-end posting and package-exclusion handling. |
| P1 | **Rounding & GST invoice compliance:** sequential numbering per series (configurable pattern), financial-year reset, B2B GSTIN capture, credit notes for cancellations after finalization — verify against GST rules. |
| P1 | **Daily cash close / shift reconciliation** (cash drawer count vs system) is not present. |
| P2 | Payment gateway/UPI QR with auto-reconciliation; part-payment reminders via WhatsApp. |

### Acceptance
- Add 3 charges via "Add another" → one API call, all appear; kill network → they queue and sync once.
- Finalize → add-charge disabled; Reopen with reason → editable; audit shows both.
- Refund > collected credit is rejected; refund within credit is held for approval when policy says so; discount-only balance never refundable.

---

## 5.3 Encounter Billing — `/billing/encounter/:encounterId`

Direct entry from IPD workspace, appointment rows and pathology bills into the **same ledger** keyed by encounter. **Readiness:** 🟡 — the page has a placeholder comment "Backend returns JSON; print template wiring is Phase 1.8. For now we just fetch to confirm endpoint and toast" in its print handler (`EncounterBillingPage.tsx:212`). Requirement: route the print button to the real invoice templates (as `BillingPage` does) or remove the page and redirect to the ledger.

---

## 5.4 Print preview — `/print-preview` (`print_preview`)

Renders a document HTML/template in an iframe/new window for print/PDF; used by cashier, lab (reports), pharmacy (labels), OT/discharge. **Readiness:** 🟡 (`PrintPreviewPage.tsx` contains mock fixtures for preview mode — ensure the production path uses only real payload). Requirements: A4 and 80 mm; page-break control for long item lists; Indic font embedding; hospital branding; no PHI in the URL (payload via session/postMessage, not query string).

---

## 5.5 Billing configuration (managed under Admin → Configuration; see 11.4)

| Config | Notes |
|---|---|
| **Billing Policy** | Per-module auto-billing rules (OPD trigger, IPD, lab path 3-state, pharmacy, etc.) — only rules the backend actually honors are shown (no "promise automation that doesn't exist" toggles); **numbering series** (prefix, pad length 1–10, running value, reset to defaults, next-number preview); discount/credit rules. |
| **Charge Master** | Service/pricing catalog: code (auto-generated per category, e.g. `LAB-001`), name, category, charge type, visit type, rate (≥ 0), qty (≥ 1), GST/HSN-SAC, incentive ₹, **rate card** (payer-type override, **room-class multiplier**). Edit/delete/status; **duplicate is not implemented** (B-14). |
| **Doctor Fees** | Per-doctor OPD consult, IPD visit and Emergency fees (shared `DoctorFees` table, also editable from the doctor tile). |
| **Invoice settings** | Header/footer, GSTIN, logo, thermal/A4 template, terms; template upload. |
