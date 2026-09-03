# Pharmacy, POS & Inventory — PRD (EasyHMS-scoped)

> Living document. Trimmed from the vendor-style full PRD to what's actually buildable against the current schema. Anchors `PLAN.md` Phase 3 (Pharmacy/Inventory). Update status marks as phases complete.

## 1. Baseline (what exists today — verified against code, not assumed)

| Piece | State |
|---|---|
| `InventoryItem` / `Batch` / `Store` / `StockLevel` | 🟢 Built — hierarchical stores, per-item-per-store stock |
| `FefoBatchAllocationService` | 🟢 Built — auto-picks earliest-expiry batch, splits across batches when qty exceeds one batch |
| `PharmacyRetailController` + `PharmacyRetailDashboard.tsx` | 🟡 Checkout works end-to-end but FEFO/batch/expiry is invisible in the cart UI |
| `MedicineMaster` | 🟡 Exists but disconnected from `InventoryItem` — separate catalog used only for prescriptions |
| `GoodsReceiptNoteController`, `PurchaseOrderController`, bulk batch JSON endpoint | 🟢 Built — structured intake, not file upload |
| `AdmissionDayBill` / `AdmissionDayBillLine` | 🟢 Built (used by IPD billing) but **not wired** to pharmacy checkout |
| `InventoryItem.MinStockLevel/MaxStockLevel/ReorderQty` | 🟢 Built, flat values — no weekly/monthly windowing |
| Batch MRP field | ❌ Missing — `Batch` has `UnitCost` only |
| Barcode field / scan decode | ❌ Missing anywhere in schema or UI |
| Expiry buckets / alerts / near-expiry report | ❌ Missing |
| Generic/salt substitution | ❌ Missing — `GenericName` is a flat string, no cross-brand lookup |
| Walk-in patient record on pharmacy checkout | ❌ Missing — free-text name/contact only, no record created |
| Returns / restock | ❌ Missing entirely |
| RTV (return-to-vendor) | ❌ Missing entirely |
| Pharmacy-specific print template (DL numbers, GSTIN, Schedule H1) | ❌ Missing — only generic `InvoicePrintSettings` |
| Analytics (ABC, GST, expiry-loss) | ❌ Missing |

## 2. Design decisions locked before build

1. **Catalog merge.** `MedicineMaster` and `InventoryItem` will be reconciled by linking `MedicineMaster` to `InventoryItem` via a foreign key (not merged into one table — prescription search needs doctor-preference metadata `InventoryItem` doesn't carry). POS search reads `InventoryItem` joined to `MedicineMaster` for display fields.
2. **Batch MRP.** Add `MRP` (decimal) to `Batch`. Migration via guarded `ALTER TABLE` per [[feedback_db_schema_never_assume_undeployed]] — `Batch` is an already-committed table.
3. **Barcode.** Add nullable `BarcodeValue` to `Batch` (batch-level, since MRP/expiry are batch-level, not item-level). Hardware: USB HID keyboard-wedge scanners (types into the focused input like a keyboard) — no camera-decode SDK needed for Phase 3a. Camera-based scanning (tablet/mobile) deferred to a later phase.
4. **Min/max threshold algorithm.** Weekly/monthly auto-threshold = trailing consumption average over the selected window (last 4 weeks or last 3 months) × configurable buffer multiplier, computed from `InventoryMovement` (dispense) rows. Stored as a computed suggestion the store manager can accept or override — not a fully automatic silent threshold change.
5. **IPD posting.** Pharmacy checkout gets a `SettlementMode` enum: `DirectCash`/`PostToAdmissionDayBill`. The latter calls the existing `AdmissionDayBill` write path instead of `BillingInvoice` directly — reuses, doesn't duplicate.
6. **Expiry buckets.** Computed server-side from `Batch.ExpiryDate - Today` at read time (no stored "bucket" field to avoid staleness): Green >180d, Yellow 90–180d, Orange 30–90d, Red <30d/expired (locked from POS, matching existing FEFO cutoff).

## 3. Phased Roadmap

### Phase 3a — Foundation + wiring existing pieces (highest leverage, lowest new-build)
- Batch MRP + BarcodeValue schema additions
- Catalog link: `MedicineMaster` → `InventoryItem`
- POS cart UI: show allocated batch(es), expiry, MRP per line (FEFO already computes this server-side — just surface it)
- Real walk-in patient quick-add (name + mobile, 10-digit validation, creates a lightweight patient record via existing patient search/create path)
- IPD `SettlementMode` — wire pharmacy checkout to `AdmissionDayBill`
- Barcode input handling in POS (keyboard-wedge scan → batch lookup by `BarcodeValue`)

### Phase 3b — Compliance-critical — 🟢 done
- Expiry watchdog: `ExpiryBucketCalculator` (Green >180d / Yellow 90-180d / Orange 30-90d / Red <30d) + `GetNearExpiryReportHandler`, filterable by store/supplier — `GET inventory/expiry/near-expiry-report`
- Notifications: `EvaluateExpiryAlertsHandler` raises `Alert` rows + one digest SMS at the 90/60/30-day thresholds (dedup per batch+code, mirrors the existing admission-alert evaluator); a new daily `ExpiryAlertBackgroundService` fires it automatically — the only scheduled job in the API, since none existed to reuse
- Schedule H1 register: new `DrugScheduleRegisterEntry` table, auto-logged inside `InventoryCommandHandlers`' movement handler on every H1 dispense (mirrors `NarcoticRegisterEntry` without the witness requirement) — `GET inventory/schedule-register`
- Pharmacy print settings: new `PharmacyPrintSettings` table (DL 20B/21B, FSSAI, pharmacist name/reg no, return-policy text) at `pharmacy-settings/print`, plus an 80mm thermal receipt template (`pharmacyReceiptThermal80.ts`) that auto-prints on direct-cash checkout with per-line batch/expiry/HSN

### Phase 3c — Efficiency features
- Bulk Excel/CSV stock intake with fuzzy header matching + pre-commit validation grid
- Rapid keyboard-only GRN grid (numpad tab-flow, auto trade-scheme calc for free-qty schemes)
- 1-click generic/salt substitution: requires a `Molecule`/`SaltComposition` lookup table + cross-`InventoryItem` matching by molecule+strength+form
- Weekly/monthly min/max auto-threshold suggestion engine

### Phase 3d — Back-office — 🟡 backend done, frontend pending
- Patient return/restock workflow (bill-scan → line selection → qty validation → stock reversal → refund slip) — backend live-verified end-to-end (`pharmacy-returns/invoice-lines`, `pharmacy-returns`); dedicated `PharmacyReturn`/`PharmacyReturnLine` ledger, `BillingInvoice`/`BillingChargeEvent` are never touched. Refund slip UI/print not yet built.
- RTV: supplier-grouped near-expiry batch compile → debit note → stock deduction — backend live-verified (`pharmacy-returns/rtv/eligible-batches`, `pharmacy-returns/rtv`); stock deducted via the shared movement handler with a narrow `IsVendorReturnContext` bypass on the expired-batch guard. Debit note PDF not yet built.
- Pharmacy analytics (sales trend, ABC analysis, GST liability by HSN/rate, expiry-loss-prevented) — backend live-verified (`pharmacy-returns/analytics/*`), pure aggregations over existing `BillingChargeEvent`/`Batch` data. Dashboard UI not yet built.

## 4. Explicitly deferred / cut from the original PRD
- Camera-based 2D DataMatrix scanning (tablet/mobile) — keyboard-wedge scanners cover the counter use case; revisit if field feedback demands it.
- Full offline LAN-only POS continuity (NFR-2.1/2.2) — the existing PWA offline architecture ([[offline_architecture]]) covers general read/queue patterns; POS-specific offline billing with local FEFO computation is a distinct, larger effort and is **not** in Phase 3 — flag separately if needed before rollout to low-connectivity sites.
- Inter-store ward transfer 1-click UX polish — the underlying transfer capability already exists via `BoardInventoryPanel`; only cosmetic/1-click wrapping remains, low priority.

## 5. Acceptance criteria (per phase, trimmed to what's actually being built)

**3a:** Cart shows batch+expiry+MRP per line without pharmacist input · walk-in created with name+mobile in one form · IPD checkout posts to AdmissionDayBill, not BillingInvoice · barcode scan resolves batch and adds to cart.

**3b:** Batches auto-bucket into 4 color states on any inventory read · 90/60/30-day digest fires via existing alert channel · every Schedule H1 dispense produces an immutable register row · pharmacy invoice print includes DL/GSTIN/FSSAI/pharmacist reg fields.

**3c:** CSV import of 100+ rows completes with per-row validation feedback · GRN grid supports full tab-flow entry with free-qty auto-calc · out-of-stock item surfaces same-molecule in-stock alternatives in one click · threshold suggestions computed from trailing consumption, manager can accept/override.

**3d:** Return flow validates against original dispensed qty per batch and reverses stock correctly · RTV produces a supplier debit note PDF and deducts stock · dashboard renders sales/ABC/GST/expiry-loss for a selected date range.

---
*See PLAN.md Phase 3 for how this sits alongside GST/tax engine and consumables work already scoped there — this document supersedes the "Pharmacy dispensing producer → BillingChargeEvent" line item with the fuller plan above.*
