# 11 — Admin Panel, User Management, Hospital Configuration & Chains

> Back to [index](README.md). Source: `features/dashboard/*`, `features/user-management/*`, `features/hospital/*`, `features/settings/*`, API `AdminController`, `HospitalsController`, `ChainsController`, `DepartmentsController`, `DoctorsController`, `DoctorFeesController`, `InvoiceSettingsController`, `ChargeController`, `BedController`, `StoreController`, `VendorController`, `EquipmentController`, `ConsentTemplateController`, `OtPlanController`, `OrderSetController`, `PackageTypeController`, `PrescriptionSettingsController`, `DischargeSettingsController`.

**Roles:** Admin, AdminDoctor (`admin_panel`). Landing for Admin. If the hospital profile is incomplete, the Admin is held on `/admin` with other nav disabled until registration is completed (NAV-4).

---

## 11.1 Admin Panel — `/admin`

**Left nav (desktop sidebar / mobile list-then-detail):** **Dashboard · AI Predictive Analysis · User Management · Patient Management · Settings**. (Appointment Oversight, Billing & Insurance, Bulk Messaging and Audit & Security entries are **commented out** — see gaps.) **Readiness:** 🟡

### 11.1.1 Dashboard (hospital analytics)
| ID | Requirement |
|---|---|
| ADM-D1 | **Registration-progress card** (`Hospital registration progress`, complete %), "Admin features locked / basic features unlocked" states, CTA to complete Hospital Info, and a one-time **regulatory update required** banner (dismissal stored per hospital) — shown while hospital data is incomplete. |
| ADM-D2 | **KPIs** from `hospitals/analysis/hospitalId=` with buckets (today, yesterday, last 7 days, this month, this year, previous year): total visits, unique patients, new vs returning (count + %), no-show. |
| ADM-D3 | **Breakdowns:** per **doctor** (visits, unique, new patients by day/week/month/year, returning, first visits, no-shows, share %), per **specialty** (visits, unique, share %, trend vs previous period), **age distribution**, **gender** stats, **city map** of patient origin. |
| ADM-D4 | Copy hospital ID/code (copy success/fail feedback); hospital access fallback text when the hospital cannot be resolved. |
| ADM-D5 | Hospital **QR posters** (hospital QR, booking QR, doctor QR) are printable from the admin tools (`hospitalQrPosterA4`, `hospitalBookingQrPosterA4`, `doctorQrPosterA4`). |

### 11.1.2 AI Predictive Analysis
- **Patient volume forecast** (`hospitals/analytics/patient-volume-forecast`): day-by-day projection with horizon selector — **Tomorrow / Week (default) / Month** — tiles and a per-doctor list that read sub-sums of the same projection (nothing recomputed client-side) plus **AI insights** fetched once per mount.
- **Lapsed patients panel** (`analytics/lapsed-patients`): patients who have not returned within the follow-up horizon, for recall campaigns.
- Requirements: show model basis and last-computed time; allow export of the lapsed list; "Send reminder" action via WhatsApp/SMS with consent (**gap**).

### 11.1.3 User Management, Patient Management, Settings
Embedded modules: **User Management** (11.2), **Patient Management** (= Patients page, 4.1), **Settings** (system config → Hospital Info, 11.3).

### Gaps
| Pri | Gap |
|---|---|
| P1 | **Audit & Security module not built** (commented out): viewer for login history, failed logins, permission changes, discount approvals, discharge unsign, patient merges, deletions — filter by user/date/entity, export; immutable. |
| P1 | **Bulk messaging** (campaigns, templates, consent/opt-out, delivery status) not built; WhatsApp is used transactionally only. |
| P1 | **Billing & Insurance** admin summary not built (Billing page covers revenue; TPA ledger missing). |
| P1 | Dashboard KPIs are visit-centric: add revenue, bed occupancy, ALOS, lab/pharmacy volumes, pending approvals, subscription usage, alerts, and day-over-day deltas. |
| P1 | `useAdminDashboard.ts` and `ProfilePage.tsx` carry `TODO: Move setup data to Zustand store` (setup data in localStorage) — settle. |
| P2 | Multi-hospital consolidated dashboard for chain owners. |
| ⚪ | `DashboardOverview.tsx` (mock KPIs with `TODO: Replace with actual API data`) is not used — delete (B-13). |

---

## 11.2 User Management

**Entry:** Admin Panel → User Management, and Hospital Info → Users (mobile). **Readiness:** ✅

| ID | Requirement |
|---|---|
| UM-1 | **Quick Add** (primary flow): one form — full name, 10-digit mobile, optional email, **password set by the admin**, role, and for doctors: licence number, department, specialisations (cascading from department), experience, qualification, OPD consult fee. The new member can **log in immediately** with mobile + password. Replaces the invitation-link flow, which remains as a legacy API. |
| UM-2 | Role list shows each role type **once** (system role + per-hospital copies are de-duplicated). While the subscription is Expired/Blocked, only **Doctor/AdminDoctor** can be onboarded. |
| UM-3 | After creation a **Share login** screen offers credentials via WhatsApp/copy; later, **reset + re-share credentials** (`users/reset-credentials`, `users/share-credentials`) without the admin ever seeing a stored password. |
| UM-4 | **Onboarded users list**: search, role/status filter, sorted active-first then alphabetical, edit (`users/update`), **Deactivate** (confirmation dialog, soft — keeps history) and **Reactivate** (no confirmation — lower risk). A user cannot deactivate themselves or the last Admin. |
| UM-5 | Plan-based user/seat limits are enforced (team size is a pricing axis) with a clear upsell message when exceeded. |

### Gaps
| Pri | Gap |
|---|---|
| P1 | **Custom roles / permission editor** — permissions exist as `RolePermissions` rows but there is no UI to create a hospital-specific role or toggle board access; Doctor's broad access is hard-coded by seed. |
| P1 | Admin-set passwords: force **change on first login**, minimum strength, and log credential shares (XC-4). |
| P1 | Last-admin protection and self-deactivation guard — verify server-side. |
| P2 | SSO/Google login, bulk CSV user import, per-user login history. |

---

## 11.3 Hospital Info — `/settings` (and "Configuration" in nav)

**Purpose.** The hospital's identity, contact, compliance data and public (online) presence. Three areas: **Hospital Info (branding)**, **Online Listing (public directory)**, and **Users** (mobile only). **Readiness:** ✅

### Hospital Info (branding)
Four sections — **Core details** (name ≤ 150 chars, hospital type: Clinic / Polyclinic / Nursing Home / General / Multispeciality / Super Speciality / Medical Center / Diagnostic Center / Dental / Eye / Other), **Contact** (email, primary and alternate phone, website), **Address** (street, city, state, country, pincode) with **GPS pin** (device geolocation or manual; HTTPS-only, with specific messages for denied/unavailable/timeout), and **Configuration & regulatory** (time zone, registration number, **GSTIN**, **PAN**, **NABH/NABL no.**) — plus languages spoken (`LanguagesSelector`). GSTIN/PAN/phone/pincode/website/email have format validation. Completion % drives the registration lock and profile badges; save status is inline (not modal) on mobile; hospital-profile edits are offline-queueable via the outbox.
- Requirements: changes propagate to every print-template header (name/address/GSTIN); all edits audit-logged; **time zone** is selectable from several non-Indian zones although the whole product renders in IST (XC-5) — restrict to `Asia/Kolkata` or make timezone handling truly per-hospital; logo/branding assets (stored for letterheads) need size/type limits.

### Online Listing (Doctor Dekho public directory)
Admin screen to **opt the hospital — and individual doctors — into the platform-wide public directory** (replaces per-hospital API-key management). Every toggle (hospital or doctor) goes through a **single confirm gate**; per-doctor curation is meaningful only after the hospital has opted in. Each tile edit (**EditDoctorTileDialog**) manages: displayed fields (languages, experience, fees, availability notes), **OPD fee** (writes the shared `DoctorFees` table), marketing-discount schedule (datetime-local interpreted as the admin's local time and stored as UTC, mirroring the CMS editor), and **review moderation** (`DoctorReviewsDialog`, hide/approve patient reviews).
- Requirements: show a preview of the public tile; changing visibility is effective on the public site within a minute; reviews moderation is audit-logged; online-booking-only discounts do **not** affect in-hospital billing.

### Gaps
| Pri | Gap |
|---|---|
| P1 | `/settings` and `/configuration` are **two routes both under `admin_panel` with different content** (Settings → Hospital Info; Configuration → masters) while the sidebar labels `/configuration` as "Hospital Info" — rename and consolidate to avoid support confusion. |
| P1 | An i18n block `systemConfiguration.subscription.cards` contains hard-coded demo values ("45 days", "₹12,000", "Credit Card") — remove so they can never render (cf. commit `acf8246` removing a fabricated trial countdown). |
| P2 | Multiple branches/sites inside one hospital entity; holiday calendar; SMS/WhatsApp sender configuration. |

---

## 11.4 Configuration masters — `/configuration`

Left nav (list-then-detail on mobile). All are write-gated by the subscription overlay (`SubscriptionReadOnlyOverlay`). **Readiness:** ✅/🟡

| Master | Purpose & key requirements |
|---|---|
| **Billing Policy** | Per-module auto-billing triggers (only those the backend honors), numbering series (prefix, pad 1–10, running value, reset to defaults, next-number preview), OPD trigger/fee rules (see 05.5). |
| **Prescriptions** | Prescription layout, fields and templates; letterhead; per-doctor field layout lab (see 4.3). |
| **Discharge Letterhead** | Discharge summary letterhead/print designer with template upload (`discharge-settings`). |
| **Pathology Report Letterhead** | Lab report letterhead/print designer (see 10.1.6). |
| **Charge Master** | Service & pricing catalog with auto-generated codes per category (`LAB-001`…), GST/HSN-SAC, incentive ₹, rate card (payer-type override, room-class multiplier); status toggle; soft-delete. **Duplicate item is a no-op** (B-14). |
| **Bed Master** | Floors → rooms → beds; default ward code/name per ward type; room-linked beds inherit ward/rate; legacy standalone beds still need their own ward/bed code/rate; **bulk add/deactivate**, **hard delete only without assignment history** (blocked codes reported in a modal), deactivated beds hidden unless toggled; left-panel nav `ALL / floor / room / UNASSIGNED`. |
| **OT Plans** | Reusable procedure templates by department (default entitled room category, ICU hint, consumables, duration, linked package). |
| **Doctor Fees** | Per-doctor OPD consult, IPD visit, Emergency fees (shared table; also editable from the doctor tile). e2e test exists (`doctor-fees.spec.ts`). |
| **Store Master** | Store hierarchy (central, pharmacy, wards, OT, ICU, CSSD…) with store type; used by every stock operation. |
| **Item Master** | Drugs/consumables/implants with schedule, LASA, high-alert flags, HSN/GST, min/max/reorder, links to MedicineMaster. |
| **Equipment Master** | Biomedical/ICT/facility asset register with AMC and PM scheduling. |
| **Vendor Master** | Suppliers/distributors with GSTIN, terms, contact. |
| **Consent Templates** | Legal forms by type (`GENERAL_ADMISSION`, `PROCEDURE`, …) with versioning and language. |
| **Also configured elsewhere** | Order sets (OT page), Package types (`package-type`), Nurse ward roster & shift settings (Nursing Station), Referrer master (Patients), Invoice print settings, Pharmacy print settings. |

### Cross-master requirements
| ID | Requirement |
|---|---|
| CFG-1 | Masters are hospital-scoped; a **starter pack** (default departments, charge categories, consent templates, shifts) is seeded at registration so a new hospital is usable on day one (**VERIFY** extent). |
| CFG-2 | Editing a master never rewrites history: billing/charge snapshots keep the rate/GST at time of posting. |
| CFG-3 | Deactivate before delete; delete only when unreferenced; blocked deletes explain why. |
| CFG-4 | Import/export (CSV/Excel) for Charge, Item, Vendor and Test catalogs (only Item bulk-stock upload exists). **P1** |
| CFG-5 | Change history for pricing masters (who/when/old→new). **P1** |

---

## 11.5 Chain management — `/chain`

**Purpose.** Group several hospitals under one owner. **Readiness:** ✅ Phase 1 (doctor-multi-hospital and per-hospital roles are later phases).
- **Create chain** (name; links the current hospital); **Onboard a new hospital** into the chain (name, contact, city…); **Deactivate hospital**; **Add doctor to a chain hospital** (pick hospital + department; "Already there" idempotency) via `chains/{id}/doctors`, `chains/mine/doctors`.
- Header **Hospital Switcher** lets multi-hospital users act in one hospital at a time; offline cache is partitioned per hospital.
- Gaps (P1): one doctor in multiple hospitals with a single identity and **per-hospital roles**; chain-level consolidated reports; central master sync (charge master/templates) across hospitals; chain-owner role.
