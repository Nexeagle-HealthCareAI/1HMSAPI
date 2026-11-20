# ABDM skills

Copied from the ABDM Developer Portal plugin `abdm-integrators-assistant` 0.11.1
(github.com/nha-in/docs, commit ba576a6, 16 Nov 2026), as published on
https://docs.abdm.gov.in/docs/hiecm/v3/getting-started/build-with-ai

- `skills/`   13 skills: abdm-gateway, abdm-m1 to m4, abdm-p1 to p4, abdm-fhir,
              abdm-record-share, abdm-scan-and-pay, abdm-scan-and-register
- `commands/` /abdm-test, /abdm-test-setup, /abdm-build, /abdm-preflight,
              /abdm-prove-encryption, /abdm-decode-error, /abdm-start
- `agents/`   abdm-integration-agent, abdm-call-debugger, fhir-compliance-agent

Not copied: the plugin's MCP servers (abdm-docs, playwright). Add the docs server with
`claude mcp add --transport http abdm-docs https://docs.abdm.gov.in/mcp -s user`.

To refresh, install the plugin (`claude plugin marketplace add nha-in/docs` then
`claude plugin install abdm-integrators-assistant@abdm-portal`) or copy the folders again.
A test run works in `abdm-ft/`; its `.venv` is ignored by git.
