# 09 — Inventory Management & Pharmacy Retail

> Back to [index](README.md). Detailed pharmacy phase history lives in [`PHARMACY_PRD.md`](../../PHARMACY_PRD.md) (3a–3d); this file states the production requirements for the **pages**. Source: `ipd-redesign/screens/InventoryBoardScreen.tsx`, `pages/InventoryManagementPage.tsx`, `components/{BoardInventoryPanel,ProcurementPanel,InternalRequestsPanel,TransferStockPanel,ReceiveStockDialog,UseStockPopover,BulkStockUpload,EquipmentMaintenancePanel,BloodBankManagementPanel,NarcoticCompliancePanel}`, `features/pharmacy/*`, `hospital/components/masters/{Store,Item,Vendor,Equipment}Master`, API `InventoryController`, `StoreController`, `VendorController`, `PurchaseOrderController`, `GoodsReceiptNoteController`, `IndentController`, `PharmacyRetailController`, `PharmacyBillingController`, `PharmacyCatalogController`, `PharmacyReturnController`, `PharmacySettingsController`, `NarcoticComplianceController`, `EquipmentController`, `BloodBankController`, `CssdController`.

### One shared stock model
**Store → Item → Batch → StockLevel** is the single model for OT, ICU, ward stores, central store and pharmacy — **one** `BoardInventoryPanel`, not duplicated per board. Batches carry expiry, **MRP**, unit cost, optional **barcode**; allocation is **FEFO** (earliest-expiry first, auto-split across batches); expired stock (< 0 days) is locked from dispensing. Expiry buckets are computed at read time (Green > 180 d, Yellow 90–180, Orange 30–90, Red < 30/expired).

---

## 9.1 Inventory Management — `/inventory`

**Roles:** Admin, AdminDoctor, Nurse, Doctor, Coordinator (`inventory`). **Readiness:** ✅ (🟡 items below)

Sidebar groups and sections:

| Group | Section | Requirement summary |
|---|---|---|
| Overview | **Dashboard** | Hospital-wide overview of stock by store, reorder alerts and expiry alerts (90/60/30-day tiers) from `inventory/board`. **VERIFY** exact tiles; add stock value and pending-request counts if absent. |
| Overview | **Current Stock** | Stock-by-store table with search, store filter, sortable columns, batch drill-down (`inventory/unified-stock`, `items/{id}/batches`). |
| Operations | **Stock Moves & Requests** | Tabs *Requests* (internal indents from wards/OT/ICU to the pharmacy/central store: submit → approve/decide → issue) and *Manual* (transfer between stores, receive, adjust). |
| Operations | **Purchasing & Vendors** | **Indent → PO → GRN**: indents (`inventory/indents`: submit, decide, **convert-to-PO**, issue), purchase orders (create, **approve**, mark-sent, cancel), goods-receipt notes (batch entry, rapid keyboard grid with free-qty scheme maths), vendor master. |
| Operations | **Catalog & Setup** | *Item master* (drugs, consumables, implants with **schedule, LASA, high-alert** flags, HSN/GST, min/max/reorder) and *Bulk upload* (Excel/CSV with fuzzy header matching + validation grid before commit). |
| Alerts | **Alerts & Warnings** | *Low stock* and *Expiring* lists (90/60/30-day tiers); badge count on the nav. Raises `Alert` rows and a digest SMS via the daily `ExpiryAlertBackgroundService`. |
| Alerts | **Equipment Maintenance** | Asset register with AMC and **preventive-maintenance due** list, maintenance log per asset (`equipment/{id}/maintenance-log`); due badge on nav. |
| Specialised | **Blood Bank** | Pool management (add bag, reserve, discard), ledger, expiry (`BloodBankManagementPanel`); bedside transfusion in 7.10. |
| Specialised | **Sterile Instruments (CSSD)** | See 6.7. |
| Specialised | **Narcotics Log** | Narcotic dispense register (witnessed), **cold-chain temperature readings** (`inventory/cold-chain/readings`). |

### Functional requirements
| ID | Requirement |
|---|---|
| INV-1 | **Use & bill at point of use:** `inventory/use-and-bill` deducts stock (FEFO) and posts a charge to the patient's encounter in one transaction (board **Use** popover: item, qty, patient). Failure of either rolls both back. |
| INV-2 | **Transfer** between stores (`inventory/transfer`) with source-availability check, reason, and receiving acknowledgement; **Receive** (`inventory/receive`) creates batches with expiry/MRP/cost/barcode. |
| INV-3 | **Movement ledger** for every change (receive, use, transfer, adjust, return-to-vendor, expiry write-off) with user, reason and reference; immutable. |
| INV-4 | **Reorder threshold suggestions:** weekly/monthly trailing-consumption average × buffer multiplier computed from dispense movements, shown as a *suggestion* the store manager **accepts or overrides** (never silently changes thresholds). |
| INV-5 | **Near-expiry report** filterable by store/supplier; RTV (return-to-vendor) path in pharmacy (9.2). |
| INV-6 | **Narcotic & Schedule H1 registers** are immutable ledgers (narcotic requires witness name; H1 auto-logged on every H1 dispense) with printable/exportable inspection views. |
| INV-7 | **Equipment:** AMC expiry and PM reminders raise alerts; maintenance log has date, type, vendor, cost, next-due. |
| INV-8 | The **page is a hub**: board panels in OT/ICU use the same component with `boardType` and patient context so staff never re-learn stock UI. |

### Gaps
| Pri | Gap |
|---|---|
| P0 | **Controllers `NarcoticComplianceController`, `EquipmentController`, `BloodBankController`, `CssdController` have no `[RequiresPermission]`** (B-4) although they back the Inventory board — a Nurse/Doctor-only restriction is not enforceable server-side. |
| P1 | `ARCHITECTURE.md` for inventory is stale — do not use as spec; this document supersedes. |
| P1 | **Stock-take / cycle count** with variance posting and approval — not present. |
| P1 | **Purchase returns & vendor payment tracking** (accounts payable) and GRN ↔ Expense link (pharmacy purchases are entered twice — see 5.1.2). |
| P1 | **Consumption analytics** (by ward/item/doctor, cost per patient-day) and **dead-stock** report. |
| P1 | Barcode/QR printing for batches and scanning on every stock action (keyboard-wedge exists in pharmacy only). |
| P2 | Auto-PO generation from approved reorder suggestions; vendor price comparison; multi-warehouse landed cost. |

---

## 9.2 Pharmacy Retail — `/pharmacy-retail`

**Roles:** Admin, AdminDoctor, Doctor, Pharmacist (`pharmacy`). **Readiness:** 🟡 — backend of 3a–3d live-verified; **frontend of Phase 3d (Returns/RTV/Analytics) built but not browser-tested** per `PHARMACY_PRD.md`; Pharmacist cannot sign in to the web today (**B-1, P0**).

Top-level strip: **Retail POS · Inventory · Billing History · Compliance · Returns · Analytics**. Inventory has sub-tabs *Medicine Catalog, Stock/Batches, Near Expiry, Reorder, Requests*; Returns has *Patient Returns, RTV*.

### 9.2.1 Retail POS
| ID | Requirement |
|---|---|
| POS-1 | **Customer identification is mandatory before checkout** (no anonymous cash sales): search existing patient or one-form **walk-in quick-add** (name + 10-digit mobile) that creates a lightweight patient record via the normal patient path. |
| POS-2 | **Search/scan:** name/code search; **Enter** first tries the text as a scanned barcode (keyboard-wedge scanners type code + Enter) and resolves a batch via `inventory/batches/by-barcode`, falling back to name search. |
| POS-3 | **Cart lines show allocated batch, expiry and MRP** (FEFO preview fetched at add time; the final allocation, possibly split across batches, is shown after checkout). Expired/Red batches cannot be added. |
| POS-4 | **Generic/salt substitution:** for an out-of-stock or unaffordable item, one click lists in-stock alternatives with the same molecule + strength + form (`pharmacy-catalog/substitutes`). |
| POS-5 | **Load e-prescription** into the cart from a patient's prescription (`LoadEPrescriptionModal`). |
| POS-6 | **Settlement mode:** *Direct cash* (pays now; prints 80 mm thermal receipt with per-line batch/expiry/HSN, DL 20B/21B, FSSAI, pharmacist name/reg no.) **or** *Post to admission day-bill* for admitted patients (writes to the existing `AdmissionDayBill` path, never a separate invoice). |
| POS-7 | **Schedule H1** items require recipient details and auto-log to the H1 register; **narcotics** require witness. |
| POS-8 | **Store resolution:** uses the store typed PHARMACY; if none, falls back to the first active store and shows a **banner** that POS/stock/requests are transacting against a non-pharmacy store. |
| POS-9 | **Incoming requests badge** (polled) for ward indents awaiting issue. |
| POS-10 | Subscription read-only blocks checkout; usage-based free-tier quota counts pharmacy bills. |

### 9.2.2 Other tabs
- **Medicine Catalog / Stock-Batches / Near Expiry / Reorder / Requests:** catalog search with `MedicineMaster ↔ InventoryItem` link; batch list with expiry-bucket colours and MRP/barcode; near-expiry report; reorder suggestions (accept/override); incoming ward requests view (issue against FEFO).
- **Billing History:** all pharmacy bills (day/range/all) with reprint and return entry.
- **Compliance:** Schedule H1 register view and export.
- **Returns → Patient Returns:** scan/enter bill → select lines → quantity validated against dispensed qty per batch → stock reversed → refund slip / **A4 credit note**. Uses a dedicated `PharmacyReturn`/`PharmacyReturnLine` ledger (does not mutate the original invoice/charge events).
- **Returns → RTV:** supplier-grouped near-expiry/expired batches → **A4 debit note** → stock deduction (shared movement handler with a narrow vendor-return bypass of the expired-batch guard).
- **Analytics:** sales trend chart, **ABC analysis**, **GST liability by HSN/rate**, **expiry-loss prevented** (recovered vs at-risk) with date range.
- **Print settings dialog:** DL numbers, GSTIN, FSSAI, registered pharmacist name/reg no., return-policy text (`pharmacy-settings/print`).

### Gaps
| Pri | Gap |
|---|---|
| **P0** | **B-1:** Pharmacist role cannot reach this page after login. |
| P1 | **Browser-test the Phase 3d UI** (Returns, RTV, Analytics, credit/debit-note print) and add Playwright coverage for scan → FEFO → checkout → return. |
| P1 | **Offline POS:** deliberately deferred (LAN-only continuity not in scope). Define the supported failure mode: if the API is unreachable the counter must show a clear "billing unavailable — use manual receipt book" screen and a recovery process, since the general PWA queue never queues payments. |
| P1 | **Drug-interaction/allergy warning at dispense** (see 4.3) and **prescription-required** enforcement for scheduled drugs (link H1 sale to a prescription/doctor record). |
| P1 | **Discount/margin controls**, price override with reason, customer-credit/udhaar ledger, split payments (cash+UPI). |
| P1 | **GST invoice compliance** for retail (sequence, B2B GSTIN customer, HSN-wise tax summary on print) — check against the GST rules; consider e-way/IRN thresholds. |
| P2 | Camera barcode scanning (tablet/phone), loyalty, home delivery, e-commerce/online orders. |

### Acceptance (from `PHARMACY_PRD.md`, trimmed)
- Cart shows batch + expiry + MRP per line without pharmacist input; IPD checkout posts to `AdmissionDayBill`; barcode scan resolves a batch and adds to the cart.
- Every Schedule H1 dispense produces an immutable register row; pharmacy print contains DL/GSTIN/FSSAI/pharmacist fields.
- Return validates against the original dispensed qty per batch and restores stock correctly; RTV produces a debit note PDF and deducts stock.
