---
description: Turn an ABDM response into the code, the cause and the named fix.
argument-hint: '<pasted response body, or an ABDM-nnnn code>'
---

Decode `$ARGUMENTS` into the error it names and the fix that clears it.

## Read the shape before the code

ABDM returns four error shapes and a parser written for one will miss three.

| Shape | Where the code is |
|---|---|
| `{"error": {"code": "ABDM-1204", ...}}` | `error.code`, from the service's business logic |
| `{"code": "ABDM-1094", "timestamp": ...}` | top level `code`, same family, no wrapper |
| `{"txnId": "Invalid Transaction Id", ...}` | no code. Every key but `timestamp` names a field you got wrong |
| `{"code": "900901", "description": ...}` | the API gateway in front of the service, before your request reached it |

Read `error.code` first and fall back to a top level `code`. A numeric code with a `description` field is an authorisation problem, not a business one.

## Then match it

Load the module's skill by its plugin name, `abdm-integrators-assistant:abdm-m1` for example. Each module skill's `references/debug.md`, or `references/errors.md` in a module built per functionality such as `abdm-m1`, lists the codes its specification's examples return, with message, status and operation, and no fix per code. The module skills are `abdm-gateway`, `abdm-m1` through `abdm-m4`, `abdm-p1` through `abdm-p4`, `abdm-scan-and-register`, `abdm-scan-and-pay` and `abdm-record-share`. Where the file says the specification returns no code for the module, use the error codes reference on the portal at `/docs/hiecm/v3/reference/error-codes` and say so.

## What a field validation body does not tell you

A body naming a field is reporting the field, not the cause. When the field carried an encrypted value, the encryption is the first thing to check and the value is the last. A call that returns the same body for every input tells you nothing about which input was right.

## Output

1. The shape, and where the code was found
2. The code, its message, and the operation that returns it
3. The most likely fix, and the second hypothesis it has to be separated from
4. The exit condition: the original call succeeding, not the fix being applied

Where no listed code matches, say so and name the two most likely causes rather than picking one. Do not invent a code.
