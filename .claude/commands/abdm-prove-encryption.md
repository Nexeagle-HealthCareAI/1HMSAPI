---
description: Prove your RSA padding against the ABDM sandbox before you build a flow on it.
argument-hint: '[m1|m4] [--mobile <10 digits>]'
---

Prove the encryption path for `$ARGUMENTS` before any flow is built on it. With no argument, prove M1.

Every ABDM journey opens with a call carrying an encrypted value, so a wrong padding stops you on your first request. A refusal can name the business field rather than the encryption, which sends you to check your test data instead of your crypto.

## What this runs

**M1.** `POST /v3/profile/login/request/otp` on `https://abhasbx.abdm.gov.in/abha/api/v3`, with `loginHint: "mobile"` and `scope: ["abha-login", "mobile-verify"]`, carrying a mobile number you control encrypted under the certificate from `/v3/profile/public/certificate`.

Load the `abdm-m1` skill for the headers and the full body. Read `encryptionAlgorithm` from the certificate response and use the padding it names.

**M4.** The certificate at `/api/v1/auth/cert` in the M4 specification. Load the `abdm-m4` skill. Prove each module's path separately and never reuse one module's path for the other.

## Pick the endpoint that can disagree with you

Before you read a refusal as evidence, check that the call answers differently for a right and a wrong input. A call that returns the same refusal for every row of a padding matrix rules out the correct answer along with the wrong ones.

## Passing

HTTP 200 and a `txnId`. An OTP arrives on that phone. Report the padding, the key source and the key size that produced it, then stop.

## Failing

- A refusal naming the mobile for a number you know is correct. Check the padding against `encryptionAlgorithm`, then the key you encrypted under, then the key format your library loaded.
- Your library will not load the key. Check the format the certificate arrives in and convert it to the one your library expects.
- A refusal on a path you can see is correct. Check `TIMESTAMP` is UTC, ISO-8601 with a trailing `Z`.

Report the failing padding and what you changed. Do not move on to a flow until this passes.
