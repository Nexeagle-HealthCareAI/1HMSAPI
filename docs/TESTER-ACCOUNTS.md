# Tester accounts: sign up and onboard again and again (Dev only)

For testing staff registration and hospital onboarding without a phone, and with the same number every time.

## What a tester number is

A mobile listed in `StaffAuth:TesterMobiles` (comma-separated 10-digit numbers). It only counts when the API runs with
`ASPNETCORE_ENVIRONMENT=Development`; on any other server the list is ignored and nothing below exists.

| For a tester number on Dev | |
|---|---|
| Sign-up / login / forgot-password code | Returned to the page (`devCode` in `POST /auth/otp/send`) and shown there. Nothing goes to WhatsApp or email. Same 6 digits, same 3-minute life, same verification as a real number. |
| WhatsApp messages of any kind | Not sent (dropped in `WhatsAppMessagingService`), so a made-up number never reaches a stranger. The flow carries on as if it was accepted. |
| Reset | `POST /internal/tester-accounts/reset` wipes the account so the number can register again. |

Everything else is the normal product: password rules, hospital set-up, profile completion, plan selection and CMS approval.

## Setting it up (once)

In the 1HMSAPI GitHub repository → Settings → Secrets and variables → Actions:

- Variable `STAFF_TESTER_MOBILES`: e.g. `9000000001,9000000002,9000000003`
- Secret `DEV_TESTER_RESET_KEY`: any long random string (`openssl rand -hex 32`)

Then redeploy Dev (push to `develop`). Without the key the reset route answers 404; without the variable there are no tester numbers.

Pick numbers nobody uses for real on Dev. A made-up number is fine: nothing is ever sent to it.

## Resetting a tester number

```bash
TESTER_RESET_KEY=<the key> scripts/reset-tester.sh 9000000001            # shows what would go, asks, deletes
TESTER_RESET_KEY=<the key> scripts/reset-tester.sh 9000000001 --dry-run  # only shows
TESTER_RESET_KEY=<the key> scripts/reset-tester.sh 9000000001 --yes      # no question
```

or directly:

```bash
curl -X POST https://1hms-dev-api.nexeagle.com/internal/tester-accounts/reset \
  -H 'Content-Type: application/json' -H 'X-Tester-Key: <the key>' \
  -d '{"mobileNumber":"9000000001","dryRun":true}'
```

The answer lists the hospitals, the number of accounts and the rows per table (`dryRun: true` changes nothing).

### What is deleted

- The tester's user (login, profile, roles, memberships).
- Every hospital that user **registered**, and everything stored under it: tables with a `HospitalId` column are cleared
  for those hospitals, and before any row goes, the rows that point at it through a foreign key go first. The links are
  read from the database catalogue, so a new table needs no change here.
- Chains the user owns.
- Staff accounts that belonged **only** to those hospitals (so their numbers are free again). Staff who also belong to
  another hospital keep their account.

### What stops a reset

| | Answer |
|---|---|
| Not a Development server, or no key configured | 404 |
| Wrong or missing `X-Tester-Key` | 401 |
| The number is not in `StaffAuth:TesterMobiles` | 400 |
| The tester was also added to a hospital somebody else registered | 409, nothing changed |
| One table would lose more than `StaffAuth:TesterResetMaxRows` rows (default 20000) | 409, rolled back |
| Any database error | 500, rolled back |

It runs in one transaction: it either all happens or nothing does.

## The automated run

`1HMSWeb/e2e/registration-onboarding.spec.ts` resets a tester number and walks sign-up to the end of onboarding in a
browser. See `1HMSWeb/e2e/README.md`.

## Code

`TesterAccounts` (the rules), `OtpSendHandler` (code on the page), `WhatsAppMessagingService` (nothing sent),
`ResetTesterAccountHandler` + `TesterResetSql` (the reset), `TesterAccountsInternalController` (the route).
Tests: `TesterAccountsTests`, `ResetTesterAccountHandlerTests`.
