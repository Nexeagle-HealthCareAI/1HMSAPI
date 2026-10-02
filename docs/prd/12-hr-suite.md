# 12 — 1HR Suite (Workforce Management) — `/hr`

> Back to [index](README.md). Source: `features/hr/*`, API `HrController`, `HrBiometricController`, `BiometricIngestController`, `ZktecoPushController`, `RunMonthlyPayrollHandler`, `SalariedPayrollStrategy`, `ConsultantPayrollStrategy`. Project context: connect a ZKTeco K40 Pro and complete the HR page (audit 2026-09-26; phases shipped: tenant-leak fix + biometric device registry/ingestion `278b362`, attendance ON_LEAVE/override/ABSENT `aafa159`).

**Roles:** none are mapped today. The route has **no board-access rule** and the API uses keys `hr.manage_employees` / `hr.view_dashboard` that are **not in the seed** (B-7). Intended: Admin, AdminDoctor (full), plus an `hr` manager role. **Readiness:** 🟡 — substantial feature set; **authorization wiring (B-7) and payroll correctness (below, B-16) are P0.**

Eight tabs: **Overview · Staff Directory · 24/7 Roster · Leave Console · Attendance · Devices · Payroll · License Alerts**.

---

## 12.1 Overview — KPIs & alerts
| ID | Requirement |
|---|---|
| HR-1 | KPI matrix from `hr/kpi-summary`: headcount by status/department, present today, on leave, absent, late, open leave requests, expiring licences. |
| HR-2 | Alerts panel links to the owning tab (leave pending → Leave Console; expiring licences → License Alerts; attendance exceptions → Attendance). |

## 12.2 Staff Directory ("Employee Vault")
| ID | Requirement |
|---|---|
| HR-3 | **Add employee** (`POST hr/employees`): code (auto, e.g. `EMP-2026-0042`), name, gender, DOB, blood group, contact, email, photo, **employment type**, department, designation, reporting manager, joining date, probation end, **PAN**, UAN, ESI, bank name/account/IFSC, status, **payroll track** (salaried vs consultant), **salary structure** (basic, HRA, DA, special/medical/uniform allowances, PF/ESI eligibility, professional tax, night-shift rate, monthly gross CTC). |
| HR-4 | **Employee drawer:** profile, **credentials** (council name, registration number, degree & completion year, **licence valid until**, document scan, verified-by/at, **BLS/ACLS/PALS expiry**), **vaccination records** (vaccine, dose, date, next due, batch), **needle-stick/PEP incidents** (date, source status, PEP started), salary structure, and for **consultants** the **fee configuration** (`consultant-fee-config`: retainer, OPD share, IPD visit amount, surgery share). |
| HR-5 | Search/filter by department/role/status; deactivate (never delete — payroll/attendance history). Documents (scans) stored in the object store with signed URLs. |
| HR-6 | **PII protection:** PAN, bank account, UAN/ESI are sensitive — masked in lists, full value only to HR/Admin, access audit-logged, never in logs. |
| Gap (P1) | Aadhaar capture policy, emergency contact, document checklist (appointment letter, ID, degree), exit/relieving workflow, link to the `User` login account (an employee ≠ a login user today). |

## 12.3 24/7 Roster ("Duty Planner")
| ID | Requirement |
|---|---|
| ROS-1 | Server-side **shift definitions** (`hr/shifts`: code, name, start/end, **grace minutes, handover buffer, night allowance, applicable roles, colour**) and **duty roster** (`hr/rosters`): employee × date × shift, ward, **on-call** flag, status. |
| ROS-2 | **Rest-period violation** is computed and shown per assignment with a message (minimum rest between consecutive shifts — threshold to be confirmed with HR policy); violating assignments require override reason. |
| ROS-3 | Roster is the basis for attendance expectation, night-shift allowance and (target) nurse ward assignment. |
| Gap (P1) | **Two shift systems exist:** HR's server-side `HrHospitalShift` and the IPD Nursing Station's browser-only shift list (B-8). Unify on the server-side HR shifts. The 2026-09-26 audit found the roster and leave screens largely read-only — **VERIFY** what the planner can now write; auto-generate, copy-week, swap/shift-trade and publish workflow are needed; payroll hard-codes night shift as code `SFT_N`. |

## 12.4 Leave Console
| ID | Requirement |
|---|---|
| LV-1 | Inbox of leave requests (`hr/leave-requests`) with type, dates, days, reason, balance; **approve/reject with reason** (`PUT …/status`). **On approval the days are marked `ON_LEAVE` in attendance** (shipped in `aafa159`). |
| LV-2 | **Leave balances** per employee/type (`hr/leave-balances`); accrual rules; comp-off. |
| LV-3 | Conflicts: warn if approving leaves a ward/department below minimum roster cover. |
| Gap (P1) | **Employee self-service** to apply for leave (the console is the approver side — **VERIFY** how requests are created today), leave policy configuration (types, entitlements, carry-forward), holiday calendar, sandwich-leave rules. |

## 12.5 Attendance
| ID | Requirement |
|---|---|
| ATT-1 | **Today** board (`attendance-today`): present/late/absent/on-leave per employee with in/out times. |
| ATT-2 | **Exceptions** (`attendance/exceptions`): missing punch, late, early-out, **ABSENT** (shipped), no roster; resolve via **manual override** (`PUT attendance/{employeeId}/override`) with mandatory reason and audit. |
| ATT-3 | Punches come from biometric devices (12.6) and/or manual override; attendance status set: `PRESENT`, `LATE`, `HALF_DAY`, `ABSENT`, `ON_LEAVE`. |
| Gap (P1) | Geo-fenced mobile punch, shift-aware late/half-day rules from `gracePeriodMinutes`, overtime approval, regularisation request flow. |

## 12.6 Devices (ZKTeco K40 Pro and compatible)
| ID | Requirement |
|---|---|
| DEV-1 | **Device registry:** register a terminal (serial, name, location), enable/disable, **rotate token**; the **plaintext token is shown once** after register/rotate and then never stored client-side. |
| DEV-2 | **Ingestion pipeline:** devices push over the ZKTeco ADMS/iclock protocol (`/iclock/cdata`, `getrequest`, `devicecmd`, `ping`) and/or a token-authenticated JSON endpoint (`POST hr/biometric/punches`); punches de-duplicated, mapped device-user-id → employee, stored raw and then reconciled into attendance. |
| DEV-3 | **Unmapped punches** list and **device users** list (from device); map/unmap an employee ↔ device user id (an id already mapped cannot be offered again). |
| DEV-4 | Security: the former hard-coded punch key is **disabled** (`f9dbf78`); all device calls carry a per-device token; device endpoints are rate-limited and tenant-scoped; the ADMS endpoints (necessarily unauthenticated by user) shall validate serial+token and reject unknown devices. |
| DEV-5 | Timestamps from devices are naive local time — define and store the device time zone; display in IST (known timestamp pitfall in this panel). |
| Gap (P1) | Device health (last seen, queue depth), command queue (enroll user, sync users), template/fingerprint **biometric data is never stored by the platform** (only IDs) — document this for DPDP, firmware/time-drift monitoring, face/palm devices. |

## 12.7 Payroll ("1-click")
**Wizard (4 steps):** select month/year & preview → review per-employee computation → run → payslips (view, **PDF**, **dispatch** via WhatsApp/email) and **bank export** (`payroll/export-bank`). A run is created as `DRAFT`; payslips are then dispatched (`payroll/{id}/dispatch`). One run per hospital per month (duplicate run rejected).

| ID | Requirement |
|---|---|
| PAY-1 | **Two tracks:** *Salaried* — basic/HRA/allowances pro-rated by payable days, night allowance (flat × completed night shifts), overtime (hourly = basic ÷ (days × 8 h) × 1.5), PF (12 % of basic capped at ₹15,000 wage, employer share), ESI, professional tax (applies when payable ≥ 50 % of days), TDS (simplified Section 192 slabs on annualised CTC), loan instalment. *Consultant* — retainer + OPD share + IPD visit + surgery share from billing-linked fee configuration. |
| PAY-2 | A payslip shows days in month, payable days, overtime, night count, every earning/deduction, employer PF/ESI; payslip number `PAY-<year>-<mm>-<code>`; totals on the run (gross, net, PF, ESI, TDS). |
| PAY-3 | Approval before disbursement, lock after approval, and an audit trail of who ran/approved. Bank-transfer file in the bank's format. |
| PAY-4 | Reprocessing a locked month requires an explicit reversal/adjustment, not a silent re-run. |

### Payroll defects found in code review (B-16 — P0 for any hospital that will pay salaries from this)
| # | Finding |
|---|---|
| 1 | **(Fixed 2026-10-02 — see README §3a) ** Payable days used to count only `PRESENT`, `LATE`, `HALF_DAY` attendance. `ON_LEAVE` (paid leave — now written on leave approval), weekly offs, public holidays and comp-offs are **not payable**, so every salaried employee is under-paid by their leave/off days. Define the policy (paid vs unpaid leave, weekly-off treatment) and compute payable days from attendance + leave ledger + calendar. |
| 2 | **(Fixed 2026-10-02: skipped employees are now reported)** Per-employee failures were swallowed (`catch` → `Console.WriteLine`, employee skipped): an employee with no active salary structure silently receives **no payslip** and the run still reports success. Return a per-employee exception list in the response and block run finalisation until resolved. |
| 3 | **TDS is a simplified single-regime slab**; no regime selection, declarations, previous-employer income, standard deduction, cess, surcharge, or Form 16/24Q outputs — do not market as statutory-compliant payroll until done. |
| 4 | **Loan instalment is a `TODO`** (always 0); loans/advances ledger absent. |
| 5 | Night shift is detected by the hard-coded code `SFT_N` rather than the shift's `nightAllowanceAmount`/night flag. |
| 6 | **PF/ESI/PT parameters are hard-coded constants** (PF cap, rates, PT); make them configurable per state/effective-date; add ESI wage ceiling and LWF. |
| 7 | One run per month with no supplementary/arrears run; no F&F (full & final) on exit. |

## 12.8 License Alerts
`hr/license-alerts`: expiring/expired **medical-council registration, BLS/ACLS/PALS, vaccination due** with severity bands (e.g. 90/60/30 days); drill to employee; notify employee/manager. Requirements: configurable thresholds, daily digest, and a hard flag on the doctor/nurse when a licence is expired (**gap** — consider blocking prescribing for an expired registration).

---

## 12.9 Page-level requirements
| ID | Requirement |
|---|---|
| HRX-1 | **Authorization:** add `hr` to `BOARD_ACCESS` and seed `hr.manage_employees`, `hr.view_dashboard` (and finer keys: `hr.run_payroll`, `hr.approve_leave`) to Admin/AdminDoctor; hide the nav item otherwise (**B-7**). |
| HRX-2 | **Tenant isolation:** every ID-only endpoint (employee, payroll run, payslip, leave, device) verifies hospital ownership (fixed once in `f9dbf78`; add regression tests). |
| HRX-3 | **Audit** every salary/bank/credential change and payroll action; export for labour inspections. |
| HRX-4 | **i18n** — none present (B-11). |
| HRX-5 | **Tests:** unit tests for payroll strategies with golden fixtures (leave month, half-day, night shifts, PF cap, ESI threshold, TDS bands) before any payroll goes live. |
