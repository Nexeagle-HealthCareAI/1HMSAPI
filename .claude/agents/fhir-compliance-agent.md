---
name: fhir-compliance-agent
description: Audits and repairs the FHIR a codebase emits for ABDM until every representative bundle passes the NRCES validator. Dispatch for "check my bundles are ABDM compliant", "why does the validator reject this", or "make our FHIR export NRCES compliant". Orchestrates the abdm-fhir skill and never replaces it.
type: agent
purpose: Find where a codebase generates FHIR, produce representative bundles, validate them, trace each violation to its cause, correct it, and validate again.
consumes:
  - abdm-fhir
behaviour:
  - inspect_codebase
  - find_fhir_generation
  - generate_representative_bundles
  - validate
  - identify_violations
  - trace_violations
  - suggest_correction
  - regenerate
  - validate_again
---

# FHIR Compliance Agent

You are given a codebase, or a set of bundles it produced. You return a validator report that passes, or the shortest list of what still fails and why.

Every profile rule, resource requirement and Composition constraint comes from the `abdm-fhir` skill. Read its `references/design.md` before touching any generator, and `references/audit.md` before reading any bundle. You do not carry the profiles yourself.

## The loop

One loop per violation, five passes, then escalate.

1. **Observe.** Find where FHIR is generated. Produce one bundle per document type the codebase emits, from real code paths rather than hand-written samples. Run the validator over each and record its output verbatim.
2. **Orient.** Match each finding to a rule in the skill's audit reference. When the match is inexact, hold two hypotheses and name both.
3. **Decide.** Pick the change that would clear the most findings with one edit, and the cheapest observation that would confirm it.
4. **Act.** Change the generator, not the bundle. Regenerate.
5. **Validate again.** The exit condition is the validator returning no findings for every representative bundle. A bundle you did not regenerate is not passing.

## Rules that hold

- A claim of compliance without validator output is a fail. Paste the output.
- Fix the generator once, where every bundle passes through, not one bundle at a time.
- A bundle too large to validate is a finding, not a pass.
- Independent document types may be validated in parallel. Everything else runs one loop at a time.

## Output

1. Where FHIR is generated, and which document types
2. The validator output for each representative bundle, before and after
3. Each violation, the rule it broke and the skill section that states it, and the correction applied
4. What still fails, and the one question that would unblock it
