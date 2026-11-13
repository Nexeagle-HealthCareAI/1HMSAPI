# 01 — Authentication, Access Control & App Shell

> Back to [index](README.md) · Cross-cutting rules XC-1…XC-12 apply to every page.

Source: `features/auth/*`, `app/AppRoutes.tsx`, `config/boardAccess.ts`, `components/layout/MainLayout.tsx`, `components/guards/RouteGuard.tsx`, `store/authStore.ts`, API `AuthServicesController`, `UserController`.

---

## 1.1 Login page — `/login`

**Purpose.** Let any staff user sign in and land on the board that matches their role. **Access:** public; an already-authenticated visitor is redirected to their landing page.
**Readiness:** 🟡 (B-1, B-5, B-6)

### Functional requirements
| ID | Requirement |
|---|---|
| AUTH-1 | The system shall offer **mobile + password** and **mobile + OTP** sign-in on one screen, with a clear switch between them. |
| AUTH-2 | Mobile numbers shall be sanitised and validated as 10-digit Indian mobiles (first digit 6–9) before any request is sent. |
| AUTH-3 | OTP sign-in shall: send a 6-digit OTP via WhatsApp (email fallback); advance to the OTP-entry step **only** if an OTP was actually generated; show "couldn't send via WhatsApp or Email" when generation succeeded but delivery failed; reject non-6-digit input client-side. |
| AUTH-4 | The login response shall provide a token and user ID; the client shall then fetch roles, permissions, hospital(s), employee/doctor IDs and only then navigate. If permissions cannot be loaded the sign-in shall fail with a message — not proceed with an empty permission set. |
| AUTH-5 | After sign-in the user shall go to: the originally requested page (`location.state.from`) if any; else the role landing page (table 1.4). |
| AUTH-6 | **Forgot password:** enter mobile → OTP → set new password (strength rules shown live) → success screen → sign in. Rate-limited (client: 2 requests/min; server: required). |
| AUTH-7 | **Account lockout:** after N consecutive failures the account/IP shall be locked for a cool-down with a visible countdown (`LockedAccountScreen`). Lockout shall be **enforced server-side** and logged. |
| AUTH-8 | The login screen shall show the product branding, language selector (EN/HI), and a mobile layout that does not clip the banner. |
| AUTH-9 | A **demo QR** deep link signs into the demo hospital without credentials; it shall be impossible to reach any real hospital through it. |

### Business rules & data
- Roles are returned by the API; the client stores `userRole` (first role) and `userRoles[]`. Multi-role users are possible; **landing page uses the first role only** — requirement: use highest-privilege/preferred role deterministically.
- OTP delivery: the API generates and stores the OTP before delivery and returns `success=true` even when both channels fail; the client compensates by showing a warning. Requirement (P1): API shall return a distinct `deliveryStatus` so the client does not guess.
- `strict: false` in `tsconfig.app.json` — null-safety is not enforced by the compiler on this codebase; auth code relies on manual guards.

### Gaps
| Pri | Gap |
|---|---|
| **P0** | **B-1:** `LoginPage.handleLogin` and `AppRoutes.RoleBasedRedirect` know only Admin/AdminDoctor/Receptionist/Nurse/Doctor/Accountant. Pharmacist, Lab Technician, Coordinator fall to `/appointment-dashboard` (forbidden) and then `/` → `logout()`. Add landing routes (Pharmacist→`/pharmacy-retail`, Lab Technician→`/pathology`, Coordinator→`/ipd-workspace` or `/ot-board`), and change the unknown-role fallback from silent logout to an "no access assigned — contact admin" screen. |
| **P0** | **B-5:** lockout in `localStorage`; add server throttling (per account + per IP), generic error messages (no user enumeration), audit entries. |
| **P0** | **B-6:** remove `[LOGIN-DEBUG]`, OTP response and role/path `console.log`s. |
| P1 | Registration flow (`handleRegister`) has a different role→landing mapping than login (no Accountant) — unify into one `getLandingPath(roles)` helper used by login, register, magic-link and `/`. |
| P1 | OTP bypass/"support can retrieve OTP" comment indicates stored OTPs are retrievable — define who/how, ensure OTPs are hashed at rest and never logged. |
| P2 | WebAuthn/passkey or TOTP second factor for Admin/AdminDoctor. |

### Acceptance
- Each of the 9 seeded roles can sign in by password and by OTP and lands on a page they are permitted to open; none is logged out.
- 5 consecutive wrong passwords lock the account for the configured period even after clearing site data and from another browser.
- Production bundle contains no `LOGIN-DEBUG` string.

---

## 1.2 Registration (hospital onboarding) — `LoginPage` register mode

**Purpose.** A new hospital/clinic owner creates their account and hospital shell. **Access:** public. **Readiness:** 🟡

### Functional requirements
| ID | Requirement |
|---|---|
| REG-1 | Step 1 — choose account type: **Doctor & Admin** (full access incl. clinical) or **Admin Only** (administrative). |
| REG-2 | Step 2 — mobile number entry and OTP verification; the number is locked after OTP is sent and can be changed only via an explicit "change" that re-enables Send OTP. |
| REG-3 | Step 3 — optional email + password setup with live strength feedback and confirm-password match; email, if given, must be valid. |
| REG-4 | Final step creates user + hospital + role mapping, optionally a doctor profile, loads permissions, populates the multi-hospital switcher, and signs the user in. |
| REG-5 | Referral codes (from the platform referral program) shall be accepted at registration where the flow exposes it; a code is single-use globally and its reward applies only on Yearly-plan approval. |
| REG-6 | A newly registered hospital starts in the usage-based **Free tier** (pooled monthly IPD/OPD/pathology/pharmacy quota) and is directed to complete Hospital Info (see 11.3) — the Admin board locks other features until hospital registration is complete. |

### Gaps
| Pri | Gap |
|---|---|
| P1 | `HospitalRegisterHandler` has a known role-row mutation issue (comment in seed: a user's role row is re-pointed to the registering hospital, "hijacking" the shared global role). Fix the handler to clone role rows; the seed currently fans out to every same-named role as a workaround. |
| P1 | Duplicate-hospital detection (same name + pincode / GSTIN / registration no.) at registration. |
| P1 | `console.log` in step handlers (B-6). |

---

## 1.3 Magic-link sign-in — `/magic-login`

**Purpose.** One-tap sign-in from a WhatsApp/email notification (e.g. online-booking alert, credentials share). **Access:** public; establishes the session. **Readiness:** ✅

| ID | Requirement |
|---|---|
| ML-1 | The link carries a single-use, expiring token **in the URL fragment** (never query string, never a password); client validates it against `^[A-Za-z0-9_-]{32,128}$` before exchanging. |
| ML-2 | `POST /auth/magic-link/exchange` returns token, user, hospital and an optional `targetPath`; the client then performs the standard session bootstrap and navigates to `targetPath` (default `/appointment-dashboard`). |
| ML-3 | Expired/used/invalid tokens show a friendly failure with a path to normal login. The fragment is removed from history after use. |
| ML-4 | `targetPath` shall be allow-listed to in-app paths the user's role may open (open-redirect protection). **VERIFY**. |

---

## 1.4 Role landing & redirect — `/`

| Role | Landing | Notes |
|---|---|---|
| Admin, AdminDoctor | `/admin` | If hospital registration incomplete → forced to `/admin` (nav locked until complete). |
| Doctor | `/dashboard` (desktop) / `/appointment-dashboard` (viewport < 1024 px) | |
| Receptionist | `/appointment-dashboard` | |
| Nurse | `/nursing-station` | |
| Accountant | `/billing` | |
| **Pharmacist** | **undefined → logout (B-1)** → required: `/pharmacy-retail` | |
| **Lab Technician** | **undefined → logout (B-1)** → required: `/pathology` | |
| **Coordinator** | **undefined → logout (B-1)** → required: `/ipd-workspace` | |

**Requirement AUTH-10:** the mapping shall live in one function, derived from the user's permissions (first permitted board in a fixed priority) rather than from role names, so a custom hospital-defined role also lands correctly.

---

## 1.5 App shell & navigation (`MainLayout`)

**Purpose.** Persistent frame: desktop left rail, mobile bottom nav/tile menu, header with hospital switcher, notifications bell, language, profile menu, banners. **Readiness:** ✅/🟡

| ID | Requirement |
|---|---|
| NAV-1 | Sidebar items shall be filtered by the user's granted `PermissionKey`s via `BOARD_ACCESS`; an item without a rule is unrestricted (today only `hr`, see B-7). **Requirement:** every nav item shall have a rule; default-deny. |
| NAV-2 | Mobile bottom nav shows only boards flagged `showInMobileNav` **and** permitted: Settings, IPD, Appointments, Billing. |
| NAV-3 | Header shall show: current hospital (with switcher when the user belongs to >1), notification bell with unread count, language selector, profile completion indicator (doctors), low-bandwidth toggle, profile/logout. |
| NAV-4 | Admin/AdminDoctor with an incomplete hospital profile shall be redirected to `/admin` and every other nav item disabled; other roles shall **not** be locked (they cannot fix it). |
| NAV-5 | Banners: `SubscriptionExpiryBanner` (approaching expiry), `SubscriptionReadOnlyBanner` (expired/blocked), free-tier usage badge, offline banner, profile-completion banner. |
| NAV-6 | `SubscriptionReadOnlyOverlay`/`useSubscriptionReadOnly` shall disable every write action on gated pages and open the shared upsell modal ("renew to unlock") instead of a toast. |
| NAV-7 | Low-bandwidth mode shall reduce polling/animation/image weight (`isLowBandwidthMode`). |
| NAV-8 | Route guard: unauthenticated → `/login` with `from` preserved; authenticated but unpermitted → redirect (see AUTH-10), never a blank page. |

### Gaps
| Pri | Gap |
|---|---|
| **P0** | B-7: add `hr` to `BOARD_ACCESS` with the HR permission keys; ensure seeds create them. |
| P1 | The `Configuration` entry exists in the sidebar list labelled "Hospital Info" and also `/settings`; two routes render overlapping content (see 11.3) — consolidate. |
| P1 | `/profile` is guarded only by authentication; confirm that is intended. |
| P2 | Remove `console.log` in `RouteGuard` (B-6). |

---

## 1.6 Not-found page — `/404`, `*`

Friendly 404 with navigation back to the user's landing page. ✅ Requirement: unknown route when unauthenticated should go to login, not 404 (today the catch-all renders 404 for both).

---

## 1.7 Language selector

EN/HI toggle persisted per user; applies to pages with i18n keys. See XC-8 / B-11 for coverage gaps. Requirement: language choice shall also drive date/number formatting and be sent to the API for localized SMS/WhatsApp templates when the patient-facing channel supports it.
