# 08 — OT Board, ICU Board & Nursing Station

> Back to [index](README.md). Source: `pages/OtBoardPage.tsx`, `screens/OtBoardScreen.tsx`, `pages/IcuBoardPage.tsx`, `screens/IcuBoardScreen.tsx`, `pages/NursingStationPage.tsx`, `components/UnifiedWardBoard.tsx`, `hospital/components/masters/{OtMaster,OtPlanMaster,OrderSetMaster,NurseWardRoster,VisualNurseRosterBoard}`, API `OTBookingController`, `OtPlanController`, `SurgeryCaseController`, `IcuController`, `NursingStationController`, `OrderSetController`.

Each board is a **hospital-wide operational view**; per-patient detail opens the IPD Patient Workspace (07) at the right section.

---

## 8.1 OT Board — `/ot-board`

**Roles:** Admin, AdminDoctor, Nurse, Doctor, Coordinator (`ot_board`). **Readiness:** ✅

**Four tabs:** **Plan Board** (kanban), **Master Config** (theatres), **Inventory** (OT store, shared `BoardInventoryPanel`), **Order Sets** (post-op order-set master).

### Plan Board
| ID | Requirement |
|---|---|
| OT-1 | Kanban columns: **Requested · Scheduled · Pre-Op · In Theatre · Post-Op · Completed · Cancelled**. Card: patient, urgency badge (Routine/Urgent/Emergency), procedure, surgeon, theatre, scheduled start, and four progress dots — **Pre-Op, Sign-In, Time-Out, Sign-Out**. |
| OT-2 | **Drag-and-drop** moves a card only along the legal forward path (`REQUESTED→SCHEDULED→PRE_OP→IN_THEATRE→POST_OP→COMPLETED`, cancel from any non-terminal); illegal drops are rejected visually and never sent. Terminal columns do not accept cards back. The server enforces the same state machine. |
| OT-3 | A drop whose transition needs data or is gated opens the **transition dialog**: *Scheduled* → theatre booking (`ot-booking/book`, with clash detection **VERIFY** server-side); *Pre-Op → In Theatre* requires the Pre-Op assessment + WHO **Sign-In** checklist; *In Theatre → Post-Op* requires **Time-Out** and **Sign-Out** (+ optional intra-op notes); *Completed* asks confirmation; *Cancelled* requires a reason. Ungated hops (Scheduled → Pre-Op) move directly. **Reschedule** and **Cancel** are also available (`ot-booking/reschedule|cancel`). |
| OT-4 | Clicking a card deep-links to the admission's **Surgery** tab (`?tab=surgery`). |
| OT-5 | Schedule view by theatre/day (`ot-booking/schedule`) shows utilisation and gaps. |
| OT-6 | Refresh by silent polling; cases update without losing drag state. |

### Master Config
Theatres (`ot-booking/theatre`): name, code, type, equipment, active; **OT Plans** (reusable procedure templates by department e.g. PCNL, Hysterectomy — default room category, ICU hint, consumables, estimated duration, package linkage) in Admin → Configuration → OT Plans (11.4). **Order Sets:** post-op and procedural order bundles.

### Gaps
| Pri | Gap |
|---|---|
| P1 | **Pre-anaesthetic check (PAC) clearance** and **procedure consent present** as additional gates for Pre-Op → In-Theatre (today the gates are the Pre-Op assessment and WHO checklist). |
| P1 | Surgeon/anaesthetist **availability check** against the Doctor Calendar when scheduling. |
| P1 | OT utilisation analytics (first-case on-time start, turnover time, cancellation reasons), emergency-case pre-emption of schedule. |
| P1 | Consumable/implant billing completeness check at COMPLETED (items used but not billed). |
| P2 | Day-care surgery pathway with discharge-the-same-day shortcut. |

---

## 8.2 ICU Board — `/icu-board`

**Roles:** Admin, AdminDoctor, Nurse, Doctor, Coordinator (`icu_board`). **Readiness:** ✅ (tabs: **Patients**, **Inventory**)

| ID | Requirement |
|---|---|
| ICU-B1 | Columns by **level of care**: **Level 3 (Intensive)**, **Level 2 (High Dependency)**, **Level 1 (Ward + Monitoring)**, and a **Pending/Unassigned** column shown only when non-empty. |
| ICU-B2 | Card: patient, bed, level and an **EWS badge** (score with risk band HIGH / MEDIUM / LOW_MEDIUM colouring); click → workspace **Critical Care** tab (`?tab=criticalCare`). |
| ICU-B3 | **Polls more frequently than the Nursing Station** (higher-acuity); silent refresh with retry; clear error state with Retry. |
| ICU-B4 | **Inventory tab** — ICU store (shared stock model with quick **Use / Receive / Transfer** actions per patient context). |
| ICU-B5 | Board patients feed the "use stock for patient" popover (encounter/patient prefilled). |

### Gaps
| Pri | Gap |
|---|---|
| P1 | Remaining ICU redesign items: Pain/RASS/CAM-ICU, early mobility, family conference (see 7.12). |
| P1 | Sort/priority within a column by acuity (SOFA/EWS) and a **hand-off summary view** for shift change. |
| P1 | Alert sound/visual escalation when a patient crosses EWS High while the board is open. |
| P2 | Bed-level monitor feed integration; ICU-specific KPIs (ventilator days, CLABSI/VAP rates, mortality). |

---

## 8.3 Nursing Station — `/nursing-station`

**Roles:** Admin, AdminDoctor, Nurse, Doctor (`nursing_station`). Landing page for Nurse. **Readiness:** 🟡 (B-8)

**Purpose.** An interactive **ward whiteboard**: every bed with its patient, due/overdue medication counts, and the nurses responsible; nurses are assigned to patients by drag-and-drop.

| ID | Requirement |
|---|---|
| NS-1 | **View modes:** by **Ward** (selector) or by **Floor**; **shift filter** (Morning/Evening/Night by default, configurable). |
| NS-2 | **Census tiles:** Patients, **Meds due**, **Meds overdue**, from `nursing-station/summary` (MAR-computed). Auto-refresh every ~60 s. |
| NS-3 | **Bed cards** show patient, doctor, status chips and the **assigned nurse team**; empty beds are greyed. |
| NS-4 | **Assign a nurse to a patient** by dragging a nurse from the roster onto a bed ("Drop to assign") or via the assign popover; remove with the × on a chip; multiple nurses per patient (**team model**); independent of the ward-level roster; the **Nurse** role can assign. Assignments are fetched in **one bulk call** for all admitted beds. |
| NS-5 | **Ward roster** (`nursing-station/assignment`, `roster`): Admin/AdminDoctor assign nurses to a **ward + shift** (visual roster board; ward×shift); release assignment. Highlights beds of the logged-in nurse. |
| NS-6 | **Quick-action dialog** from a bed: Vitals, Notes, MAR, I/O etc. for that patient without leaving the board (tab state resets per patient). |
| NS-7 | **Shift settings sheet** defines shift codes/labels/times; used by roster, handover and board filters. |

### Gaps
| Pri | Gap |
|---|---|
| **P1** | **B-8:** shift definitions live in `localStorage` (`easyhms_shifts_<hospitalId>`) behind a fake-latency promise — not server-side, so one browser's shifts differ from another's and roster assignments reference codes that may not exist elsewhere. Create a `ShiftDefinition` table + API and migrate; until then, hide the "Shift settings" editor or fix to defaults. |
| P1 | A parallel `NursingStationScreen.tsx` + `NurseDashboard.tsx` are not imported anywhere (⚪) — remove (B-13). |
| P1 | Nurse **task list** (due vitals, due doses, pending handover, new orders) as the nurse's home, sorted by urgency; **new-order alert** to the responsible nurse. |
| P1 | Roster: leave integration with HR (12.4) so a nurse on leave cannot be assigned; workload (patients-per-nurse) warning. |
| P2 | Staffing ratio analytics by ward/shift. |

### Acceptance
- Dragging a nurse onto a bed persists after refresh on another device; two nurses can be assigned; removing one leaves the other.
- Overdue count equals the MAR computation for the same shift/ward.

---

## 8.4 Shared board requirements
| ID | Requirement |
|---|---|
| BRD-1 | All three boards are usable on a 10" tablet and a phone (horizontal scroll columns on mobile, 44 px targets), in dark mode, with reduced-motion honoured (cards use motion; provide a no-animation fallback). |
| BRD-2 | Polling pauses on hidden tabs and respects low-bandwidth mode; consider push (SSE) once > 10 concurrent board viewers per hospital. |
| BRD-3 | Every board action is permission-checked server-side, audit-logged, and idempotent. |
| BRD-4 | Boards load < 3 s with 200 cases/beds; empty states sell the next action (e.g. "No cases today — Book a surgery"). |
