# HIE-CM p2 test

## Read this first: this run is never hands-off

A tester sits with this run from the first case to the last. Every Aadhaar number, mobile number, OTP, ABHA number or address, password, document number, biometric and approval is entered by that person, at the moment the case needs it. You never type one, never make one up, and never mock a response to get past one. This rule outranks every other rule in this file.

### In plain words

A functional test run is done with a tester at the keyboard. The agent drives
the steps; the tester enters everything that belongs to a real person. When a
step needs one of the values below, the agent stops, says exactly what is
needed and where, and waits for the tester.

| Value | Who enters it |
|---|---|
| Aadhaar number | The tester |
| Mobile number, including the communication mobile | The tester |
| Any OTP: Aadhaar OTP, mobile OTP, linking OTP | The tester |
| ABHA number, ABHA address, and the password or PIN of either | The tester |
| Name, date of birth, gender, email or address being enrolled or verified | The tester |
| Driving licence or any document number | The tester |
| A fingerprint, iris or face capture | The tester, on the device |
| A consent, link or subscription approval in a PHR app | The tester, in the app |
| A captcha | The tester |

The agent never types any of these, in any run type. A value the agent did
not get from the tester at that moment does not exist.

This holds for a deliberately wrong value too. A case that checks your
screen's own format rule, such as an 11 digit Aadhaar number or letters in a
mobile field, still has the tester type it: the agent says what to type ("an
11 digit number") and the tester types it.

### Before you start

- The tester is present for the whole run, with the phone that receives the Aadhaar and mobile OTPs.
- For a `ui` run the browser is visible on the tester's screen, never headless, so the tester can type into it. The Playwright MCP server opens a visible browser unless it was started with `--headless`; if it was, stop and ask the tester to restart it without that flag.
- For a PHR app, the emulator window or the phone is in front of the tester.

### What happens

Before every field, ask one question: does this field take a person's identity, a document number, a code sent to a person, or an approval? If yes, it is a tester's step.

**A field that already holds a value.** Look at the field before you ask. If it already holds something when the case reaches it, because the app kept it from an earlier case or screen, or the browser filled it from its saved form data, do not go on with that value. Ask the tester to clear the field and type the value this case needs, which may be the same one, and write `field was pre-filled: <field>, tester cleared and typed` in the run record. Within one case, a value the app carries from one step to the next, such as the Aadhaar number from the OTP request to the OTP check, is the app not asking twice, and stays as it is.

**In a `ui` run (browser or emulator):**

1. Bring the field into view and click it, so the cursor is in it.
2. Stop and say, in one line, what to enter and where:

   ```text
   CRT_ABHA_104: type your Aadhaar number into the "Aadhaar Number" field in the browser window, then reply done.
   CRT_ABHA_105: an OTP has been sent to the Aadhaar linked mobile. Type it into the "Enter OTP" field in the browser, then reply done.
   ```

3. Wait for the tester's reply. Do not ask them to tell you the value; they type it into the screen, so it never passes through you.
4. After `done`, take the snapshot, with that field's value replaced by `[redacted]`, and go on with the next step yourself.
5. No reply, `skip`, or the tester not present: ask once more, naming the field. If the tester says they cannot provide it in this run, record the case `needs-human` with `Needs tester: <the field>, asked at <the step>`, and go to the next case. If they stay silent after the second ask, record the same `needs-human`, with `Needs tester: <the field>, asked at <the step>, no answer after two asks`.

**In an `ft` run (terminal):** prompt in the terminal with input hidden, name what is needed and where it came from ("the OTP sent to the mobile ending 1234"), send it once, and hold it only for that call. Never write it to the run log, the results file or the workbook.

**Never repeat an identity.** Reading a page, a snapshot or a network response puts a person's values in front of you. Never repeat an Aadhaar number, mobile number, ABHA number, ABHA address or a person's name read from a page or a response: not in chat, the run log, Remarks, a command or a file.

| Instead of | Write |
|---|---|
| The tester's own identity | Its last four characters: "ABHA ending 8219", "the mobile ending 1234", "the ABHA address ending r7k2@sbx" |
| Another patient's data, such as a row in the app's patient list | Nothing from it at all. Say "another patient's row" and go on |
| Asking the tester to enter a value | The field's name, never the value: "type the ABHA address into the ABHA Address field" |

**Ask before a case is left untested.** Every input a case needs is asked for in the readiness check, and asked again when the run reaches that case: a second mobile, a mobile that holds no ABHA, an Aadhaar that holds no ABHA, a second ABHA address, a fingerprint or face device, the ABHA app, a counter QR code, a public callback URL. A case is recorded `needs-human` only after the tester has said they cannot provide it in this run, and its Remarks name what was asked, such as `Needs tester: a second ABHA address, asked at readiness and at the case`. Never record such a case `not-run`, and never leave it blank. `not-run` is only for a case that does not apply: the entity type, an optional capability you do not offer, or no published operation for it.

**Never, in any run type:**

| Never | Why |
|---|---|
| Type a made-up, sample or placeholder value, such as `1234 5678 9012`, `999999999999`, `9999999999`, `123456` or `000000` | The sandbox checks Aadhaar and mobile against UIDAI and sends a real OTP. A made-up value fails every case for a reason that is not the integrator's |
| Use an example value from the workbook, a test case page or this skill | Examples show the format, not a person |
| Reuse a value from an earlier case or an earlier run | An OTP is valid once; an identity is the tester's to give each time |
| Generate an OTP, guess one, or read one from a log or an SMS tool | Only the tester enters an OTP |
| Skip the step and carry on | Every later step then tests nothing |
| Mock, stub, intercept or replay a sandbox response, or route the browser's network to a fake | The run then tests the mock, not the integration |
| Run the browser headless | The tester cannot type into it |

### How you know it worked

- At every identity, code or approval step, the run log shows the prompt that was given to the tester and their `done`, or the case is `needs-human`.
- No value from the "Never" table appears in any field the agent typed into: `browser_type` was never called on an identity, document, code or password field.
- No case is `failed` because of a value the agent supplied.
- No Aadhaar number, mobile, ABHA number, ABHA address or name read from a page or a response appears in chat, the run log, Remarks or a command: only last four characters, such as "ABHA ending 8219".
- Every `needs-human` case names in Remarks what the tester was asked for, and no case waiting on a person, device or value is `not-run` or blank.

### When it goes wrong

| Observed | Do |
|---|---|
| You are about to type an Aadhaar number, mobile number, OTP or other identity value yourself | Stop. Click the field and ask the tester to type it, as above |
| The tester asks you to make up a value so the run can go faster | Refuse, and say why in one line: the sandbox needs a real person's values, and a made-up one fails the case |
| You find that a value was typed by the agent, made up or reused, earlier in this run | Stop the run. Every case from that point is void: do not write those results to the workbook, mark them `pending` in the run log with `void: agent entered <value type>`, tell the tester, and run those cases again with the tester entering the value |
| The OTP never arrives | Ask the tester to use the screen's resend option once, then wait. Still nothing: `needs-human` with `Needs tester: OTP not received` |
| The browser is headless or not on the tester's screen | Stop before the first case and ask the tester to restart the Playwright server without `--headless` |
| You are about to repeat an Aadhaar number, mobile, ABHA number, ABHA address or name you read on a page or in a response, in chat, the run log, Remarks, a command or a file | Stop. Write the last four characters only, such as "ABHA ending 8219". If it is another patient's data, write nothing from it |
| A case needs an input the run does not have, such as a second ABHA address or a mobile with no ABHA | Ask the tester for it now, even if it was asked at readiness. Only after they say they cannot provide it in this run, record `needs-human` naming what was asked |
| An identity field already holds a value at the start of a case | The app kept it from an earlier case, or the browser autofilled it. Ask the tester to clear it and type the value for this case; note it in the run record. Never run a case on a value the tester did not enter for it |

From `shared.concept.test-human-entry`.

Walks the ABDM P2 functional test cases against the PHR app you built and the sandbox: linking records, sharing a profile, the consents tabs and the profile. Each case ends in one of five recorded outcomes, never in an opinion.

NHA's sheet lists no APIs for these cases. Before building, read `references/test-matrix.md` in this skill: build each mandatory case's Expected Result for your entity type into the app screen, and use this module's journeys for the calls. The test run uses it to know what each case exercises.

## What this skill does

- Runs every P2 case in the PHR app set against your app and the sandbox, one case at a time
- Records, for each case, the sandbox response or callback and the REQUEST-ID that prove it, or the screen that has to be attested, or the human step it stopped at
- Writes a manifest of case names to request ids at the end, which is the evidence a reviewer checks against the gateway rather than a folder of screenshots
- Checks that every credential, person and device the chosen cases need is ready before the first call, and stops when a required one is missing
- Logs every case to a run log file in your repository as it runs, and asks for every OTP, password and approval in the terminal, never skipping one

## What it needs from you

| Need | Where it comes from |
|---|---|
| Sandbox client id and secret | The ABDM sandbox portal, under your registered application |
| Your PHR app running against the sandbox hosts, with its bridge callback URL registered | Your build and your bridge. Linking and sharing answer on your callback, so a callback that never arrives fails those cases |
| A person logged in to your app with an ABHA address | The P1 login. The skill uses that person's user token |
| A sandbox HIP with a record for that person, and a sandbox HIU that raises consent requests | A partner or your own test facility. Without one, the linking and consent cases are `needs-human`, after it was asked for at readiness and again at the case, and the tester said they cannot provide it in this run |
| The person present with their phone | Every OTP, every consent tap and every QR scan is a human step and the skill stops for it |
| A folder for the run log | Your repository. `abdm-ft/` unless you name another |
| NHA's PHR test case workbook, required: the results and the report are built from it | It ships with this skill, in its `workbook/` folder (on the docs site, `/skills/abdm-p2/workbook/`): copy it into `abdm-ft/` before the run, and never use another copy, so the sheet matches the cases below. It has one tab per PHR milestone and no case ids; the script adds a Test Case ID column with the ids this skill uses. Writing into it needs Python 3.9 or later with `openpyxl` and `Pillow` |
| For a `ui` run: your PHR app's APK, installed on an Android emulator or a device over adb | Your build. Android Studio's emulator, or a phone with USB debugging on |
| For a `ui` run: Maestro, to drive the app | https://maestro.mobile.dev, Apache 2.0. `/abdm-test-setup` lists what is missing and prints the install command |
| For a `ui` run: a test ABHA address and its login | A sandbox ABHA address the tester holds, typed at the app's own login when the run reaches it, never stored |

## Say one of these

- "Walk the P2 test cases against my PHR app and log each one."
- "Run only the linking cases, PHR_APP_101 to PHR_APP_111."
- "Re-run PHR_APP_1505, linking the ABHA number failed yesterday."
- "Write a terminal script that runs the P2 test cases and asks me for each OTP and each consent tap."

## What happens first

The first question is the run type. `ft` is the API functional test: your PHR app's backend against the sandbox, judged on calls and callbacks. `ui` is the UI test: an agent drives your PHR app on an Android emulator or device with Maestro, as the section "Running a case as a UI test" below sets out. `all` runs `ft` for the chosen cases, then `ui`. The second question: which cases. All of them, one group, or a list of case ids. Then the preconditions for the first case are checked before anything is sent. Then the readiness check below. After it, nothing else is asked until a case needs a human or an authentication step.

**Readiness check.** The run does not start until every row of "What it needs from you" that the chosen cases use is confirmed. Ask for each one in the terminal, one at a time. Record each in the run log's header as `ready`, `missing` or `not needed`, never the value itself.

| Row | Ready when |
|---|---|
| Credentials | They are set in the environment or typed at a hidden prompt, and the first session call the cases use returns its token |
| Your system | It answers on the address you gave, against the sandbox hosts |
| People and devices | Each is named for this run: who receives the OTPs, who approves in the PHR app, which device scans |
| Entity type and claimed capabilities | Answered, because they decide which cases are mandatory |
| The run log | Its folder exists and can be written: `abdm-ft/` in your repository unless you name another |
| The workbook | `python3 -I abdm-ft/abdm_workbook.py cases <workbook>` lists the case ids, and every case chosen is among them. Required: without it there are no results in the sheet and no report, so ask for it before the first case |
| UI readiness, for a `ui` or `all` run | Every row of the readiness check in "Running a case as a UI test" is `ready`: the emulator or device, the APK installed, Maestro, and the tester present. `not needed` for an `ft` run |

A `missing` row that a mandatory case needs stops the run before the first call. Say what is missing and ask the one question that would fix it.

**Ask for every input the cases need.** In the readiness check, ask for each input a chosen case needs beyond the rows above, such as a second mobile, a mobile that holds no ABHA, an Aadhaar that holds no ABHA, a second ABHA address, a fingerprint or face device, the ABHA app, a counter QR code or a public callback URL. Record each as `ready` or `missing`. Ask again for a `missing` one when the run reaches the case that needs it. Only when the tester says they cannot provide it in this run is that case `needs-human`, with what was asked in Remarks.

**The case plan.** Before the first case, run `python3 -I abdm-ft/abdm_report.py plan <workbook> --run <ft or ui> --entity <government or private>`, as "Writing the test report" sets out. It lists every case in the workbook, in sheet order, with its group, what it is for your entity type and its status so far. The run log's rows are made from that list. No case may be dropped: a case that cannot run is `needs-human` with its need named, after asking, or `not-run` with the rule that makes it not apply. Before the tester leaves, run `python3 -I abdm-ft/abdm_workbook.py open <workbook> --run <ft or ui> --entity <government or private>` and read the list out, including every `NA on a mandatory case` line, so every case still waiting for them is done, or asked about, while they are there.

## How this skill runs

Every case is an observe, orient, decide, act loop. Observe the live state: the last response, the last callback, the last REQUEST-ID, what your screen shows. Orient against the case below. Decide the cheapest action that produces a new observation. Act, paste the raw result, return to observe. A case is done only when its exit condition is observed, never because the step should have worked.

Loop limit: 3 passes per test case. Hitting the limit is an escalation: state what was observed with request ids, what was tried, name one section of this skill or one operation page to read, and ask one question.

### The five outcomes

| Outcome | Record it when | Never when |
|---|---|---|
| `passed` | The exit condition is observed and the observation is pasted verbatim | The response or the callback was not pasted. A pass with no output is a fail |
| `conditional-pass` | The Expected Result is met and observed as for `passed`, with a gap beside it, such as a point in the sheet's Suggestions for the Functional Tester column not met. Remarks name the condition | The Expected Result is not met. That is `failed`. A Suggestions gap alone never makes a FAIL |
| `failed` | Three passes and the exit condition was not observed, or the sandbox returned the refusal the case expects you to handle and your app did not handle it | A human step was missing. That is `needs-human` |
| `needs-human` | The case reached an OTP, a consent tap, a QR scan, a facility partner or a person who was not there | Ever as a substitute for a fail you can see, or before the tester was asked for what the case needs and said they cannot provide it in this run |
| `not-run` | The case is optional and you do not claim it, or no P specification publishes the operation it needs | To skip a mandatory case that applies to you, or for a case waiting on a person, a device or test data. That is `needs-human` |

### Judging a case

- The sheet's Expected Result column decides the outcome. Its Steps To Be Executed say what to do. Its Suggestions for the Functional Tester column is guidance: a gap against it, with the Expected Result met, is a `conditional-pass`, never on its own a FAIL.
- A message that means the same as the one the sheet quotes is a PASS. Quote both in the run record.
- A case with several steps records a result for each step, in its result's `steps`. The case is `failed` only when a step's expected result fails. When every step run passes and a step could not be run, the case is `needs-human`, naming that step and what it needs.
- A case waiting on a person, a device or test data is `needs-human` only after the tester was asked for it, at the readiness check and again at the case, and said they cannot provide it in this run. Remarks name what was asked. Such a case is never `not-run` and never blank. `not-run` is only for a case that does not apply: the entity type, an optional capability you do not offer, or no published operation for it.

### The three kinds of evidence

Every case names one of these. It decides what the manifest carries.

- **sandbox.** A call or a callback the gateway sees. Evidence is the HTTP status, the body and the REQUEST-ID, pasted. For an asynchronous call it is the 202 and then the callback carrying the named field. A reviewer can check this against the gateway's own record, which is why it outranks the other two.
- **screen.** A rule about what your app shows or stores, which no call proves. Evidence is one screenshot or a short recording. A screen case is attested, not observed, and the manifest marks it so. In a `ui` run the agent observes the screen itself instead, as "Running a case as a UI test" sets out.
- **human.** A step only a person can do. Evidence is the observation immediately before it and the outcome `needs-human` when nobody was there after two asks.

### Rules that hold for every case

- Tokens as the specification requires. The gateway access token goes in `Authorization` on every call. Profile calls send the person's user token in `X-token`. Gateway calls about the person send their login token in `X-AUTH-TOKEN` and the consent manager in `X-CM-ID`, `sbx` in sandbox.
- A 202 is a receipt, not a result. The answer arrives on your callback. Log the whole callback body before you parse it. `hiecm.concept.asynchronous-callbacks`
- Fresh `REQUEST-ID` on every call, logged before sending. A reused id is refused, and after a failure the id is the only handle on the call.
- Paste the response. A step with no pasted output is not done.
- Sensitive values travel encrypted: mobile, ABHA number, OTP. Read the key and the algorithm from the certificate call rather than hard coding them. `hiecm.concept.input-encryption`
- Read plural as plural. Discovery returns many care contexts, and one consent request yields one artefact per HIP. `hiecm.concept.m3-plural-artefacts-and-codes`
- One question when you escalate. The person is being interrupted.

### The run record

Append one block per loop pass. It is the raw material for the manifest.

```text
case: PHR_APP_105   pass: 1 of 3   at: 2026-10-01T10:14:02Z
observed: POST /api/hiecm/user-initiated-linking/v3/patient/care-context/discover  REQUEST-ID 6f1c...  HTTP 202
callback: POST /api/v3/hiu/patient/care-context/on-discover  response.requestId 6f1c...
body: {"transactionId":"...","patient":[{"referenceNumber":"...","careContexts":[...],"hiType":"Prescription","count":1}],...}
matched: exit condition for PHR_APP_105
next: PHR_APP_106
```

### The run log

The run is logged to a file as it happens, so the person can follow it, stop and resume it, and hand it to a reviewer. Create it before the first case, at `abdm-ft/p2-<YYYYMMDD-HHMM>.md`.

- The header names this skill, the set, the entity type, the time started, and the readiness check row by row.
- Every case `plan` lists gets a row before the first call, each `pending`, in the order `plan` prints them. A case outside the ones chosen for this run stays `pending`, so the log always shows what is still open.
- A case reads `running` while its loop runs. When it ends, update its row at once: outcome, request ids, the literal that matched or the reason, and the evidence file for a `screen` case. Then append its run record block under the table.
- A stopped run resumes from the log: the first row that is still `pending` or `running` is the next case.
- Never write a secret to it: no token, OTP, password, Aadhaar number, full mobile, client secret or private key. A request id, a txnId and a masked mobile are fine.

```markdown
# P2 functional test run, 2026-10-13 10:12
Readiness: credentials ready; system ready; OTP person ready; biometric device not needed.

| Case | Outcome | Request ids | Observed |
|---|---|---|---|
| PHR_APP_101 | passed | 6f1c..., a02d... | exit condition matched, pasted below |
| PHR_APP_102 | needs-human | b7a1... | OTP asked for twice, no answer |
| PHR_APP_103 | running | | |
| PHR_APP_104 | pending | | |
```

### Authentication is never skipped

Every step that needs someone to authenticate stops and asks in the terminal. That covers the session token, every OTP, a password or PIN, a captcha, a biometric scan, a hosted login page, and a consent or subscription the person approves in their PHR app.

| Do | Never |
|---|---|
| Prompt at the moment the case needs it, naming what is needed and where it went, such as "the OTP sent to the mobile ending 1234" | Skip the step and go on to the next call |
| Read OTPs, passwords and secrets with input hidden, and hold them only for that call | Use a fixed, test or default OTP, or one from an earlier run |
| Fetch a fresh session token at the start of the run from the credentials in the readiness check | Hard code a token, or reuse one from an earlier run |
| Wait while the person approves on their phone, then observe the callback or status that proves it | Mock, stub or replay a response so the case can carry on |
| Record `needs-human` only after the prompt was shown, asked again naming the field, and the tester said they cannot provide it in this run or stayed silent after the second ask. Remarks name what was asked | Record any outcome for an authentication step that was never asked |

A runner that reaches an authenticated call without asking is broken, not finished. Stop, fix the runner, and run the case again.

### When the run is a script

Asked for a test script, a test command or automated tests, build one interactive terminal runner that walks the cases below. It does the typing. Every rule above still holds.

| Rule | Why |
|---|---|
| Before the first case, run the readiness check and create the run log | A run that starts without its credentials, people or devices fails cases for the wrong reason |
| Every step in "Authentication is never skipped" is a terminal prompt, read with input hidden | A skipped login proves nothing, and a runner that never asks reaches no success path |
| Update the run log after every case, then write the manifest beside it at the end | The person follows the run as it happens, and a stopped run resumes from the log |
| Name every check by its case name from this skill, such as `PHR_APP_105`. Never invent a numbering such as `TC01` | The functional testing report quotes these names. A check with no case name answers nothing a reviewer asks |
| At each `human` step, stop and prompt in the terminal: the OTP, "approve the request on the phone, then press enter", "scan the QR, then press enter". Read any value from stdin, encrypt it, send it | Linking, sharing and consent all pass through a person. A runner that never asks never reaches one |
| Never write a typed value to disk, a log or the manifest, and never hard code one | An OTP and a mobile number belong to the person, not the run |
| For an asynchronous call, wait on your callback with a timeout, and match its `response.requestId` to the REQUEST-ID you sent | The 202 alone proves only that the gateway took the request |
| Run each `sandbox` case's success path and the refusal its exit condition names, as separate checks | The case passes only when both are observed |
| Compare the status and the body literal the exit condition names, such as `202` and then `status` `GRANTED`, or `200` with `authResult` `failed` | A status alone never passes a check |
| A `4xx` passes only where the case's exit condition names that refusal | A `400` on a success path step is `failed` |
| A blank answer at a prompt asks once more, naming the field. A second blank, or the tester saying they cannot provide it in this run, records the case `needs-human` with what was asked in Remarks, and moves to the next case | Nobody present is neither a pass nor a fail |
| A `screen` case prints its check, then asks `passed? y/n` and the evidence file name | No call proves a screen, so the case is attested |
| At the end, print one line per case name with its outcome and request ids, then write the manifest in the last section | The person reads outcomes by case name, not by script step |

A run reads like this. The person's values are typed at the prompts and never echoed back.

```text
PHR_APP_1503  POST /abha/api/v3/phr/app/login/profile/request/otp  REQUEST-ID 3b9e...  HTTP 200  txnId present  matched
  Enter the OTP sent to the mobile ending 2425, or leave blank if nobody is here:
PHR_APP_1503  wrong OTP  REQUEST-ID 7c02...  HTTP 200  authResult failed  matched
PHR_APP_1503  right OTP  REQUEST-ID 9a41...  HTTP 200  authResult success  matched
PHR_APP_1505  POST /abha/api/v3/phr/app/login/profile/link  REQUEST-ID 4d10...  HTTP 200  message ABHA number is securely linked to ABHA address  matched
PHR_APP_1505  passed
```

Nudge: calls with a bad `scope`, a missing `X-AUTH-TOKEN` or a made up transaction id test your client, not a case. Run them first if you want them, label them `preflight`, and keep them out of the case counts.

## Test cases

NHA's PHR sheet numbers its rows but gives them no ids, so each case here has an id built the way the HMIS ids are, from the PHR milestone and the row: row P1 3.2 is `PHR_ADDR_302`, and P2, P3 and P4 rows are `PHR_APP_`, `PHR_HIU_` and `PHR_LOCKER_`. The test case page shows each id with its row. Use the id in the run log, the manifest and the workbook.

The marks and the wording of each check are ABDM's, from the PHR mobile app test cases, PHR App Functionality tab. The sections are grouped as the sheet groups them. Every operation id below is in a P specification, which carries the host, the headers and a full request.

Fetching health data is the health information request, `m3_post_data_flow_v3_health_information_request`. No P specification publishes it, so every case whose result is records fetched is `not-run` here with that reason.

### PHR_APP_101 to PHR_APP_111: link and view records from a health facility, user initiated

Mandatory unless marked. The programme group repeats this one, so it is written in full.

Loop limit: 3 passes per test case.

**Preconditions.** The person logged in. A sandbox HIP holding a record whose name, year of birth, gender and mobile match the person's profile. Your callback reachable.

| Case | Evidence | Check | Exit condition |
|---|---|---|---|
| PHR_APP_101 | `screen` | "Link my Health Records" in My Records, and "+" in Linked Facility, open the facility search | The search screen after each tap |
| PHR_APP_102 | `sandbox` | The person types part of a facility name and the facility is found | `p2_get_gateway_v3_providers` with `name` returns 200 with entries carrying `identifier.name`, `identifier.id` and `isHIP` `true`, and the screen lists them with the address. A name with no match returns 204 with `error.code`, and the screen says nothing was found |
| PHR_APP_103 | `screen` | The person's verified mobile, ABHA address, ABHA number, patient id field, full name, year of birth and gender are shown after choosing the facility | The screen |
| PHR_APP_104 | `sandbox` | An entered patient id narrows the match | `p2_post_user_initiated_linking_v3_patient_care_context_discover` with an `unverifiedIdentifiers` entry of `type` `MR` returns 202, and the `p2_post_v3_hiu_patient_care_context_on_discover` callback carries `patient` with that record |
| PHR_APP_105 | `sandbox` | Fetch Records discovers by name, year of birth, gender and mobile, and leaves out what is already linked | The discover call returns 202 with `hip.id` set. The on-discover callback carries `transactionId` and `patient[].careContexts`. No care context in it appears in `p2_get_hip_v3_link_patient_links` `Patient.links[].careContexts` |
| PHR_APP_106 | `sandbox`, `human` | The discovered records are shown with their type, the person selects some, and only those link | `p2_post_user_initiated_linking_v3_link_care_context_init` with the `transactionId` and the selected care contexts returns 202. The `p2_post_v3_hiu_patient_care_context_on_init` callback carries `link.referenceNumber`. After PHR_APP_107, the on-confirm `patient.careContexts` holds the selected ones and no other |
| PHR_APP_107 | `sandbox`, `human`, optional | The OTP the HIP sends completes the link, and "Records are successfully linked" is shown | The person enters the OTP. `p2_post_user_initiated_linking_v3_link_care_context_confirm` with `linkRefNumber` and `token` returns 202, and the `p2_post_v3_hiu_patient_care_context_on_confirm` callback carries `patient.careContexts`. A wrong OTP brings the callback with `error.code`, and the screen shows the error. Record `not-run` if you do not claim it |
| PHR_APP_108 | not-run | Pull Records sends a data request within 5 minutes and records arrive | `not-run`: needs `m3_post_data_flow_v3_health_information_request`, which no P specification publishes. The screen check that the facility shows in Linked Facility is PHR_APP_1201 |
| PHR_APP_109 | `screen`, optional | An "i" control in My Records says fetching can take time, and a status shows while it does | The message and the status. Record `not-run` if you do not claim it |
| PHR_APP_110 | `screen`, optional | A record shows facility name, visit type, prescriber, date and its attachment | The record screen. Record `not-run` if you do not claim it |
| PHR_APP_111 | `screen`, optional | The attachment opens on the device | The opened record. Record `not-run` if you do not claim it |

### PHR_APP_201 to PHR_APP_211: link and view records from a health programme, user initiated

Same as PHR_APP_101 to PHR_APP_111, with these differences. Run each as its counterpart and record the counterpart's evidence.

Loop limit: 3 passes per test case.

| Case | Evidence | Check | Exit condition |
|---|---|---|---|
| PHR_APP_201 | `screen` | As PHR_APP_101 | As PHR_APP_101 |
| PHR_APP_202 | `sandbox` | The person selects an integrated government programme | `p2_get_gateway_v3_govt_programs` returns 200 with entries carrying `identifier.name` and `identifier.id`, and the screen lists them |
| PHR_APP_203 | `screen` | As PHR_APP_103, with the programme's own label in place of patient id | The screen |
| PHR_APP_204 | `sandbox` | An entered programme id, such as a PM-JAY id, narrows the match | As PHR_APP_104 |
| PHR_APP_205 | `sandbox` | As PHR_APP_105 | As PHR_APP_105, with the programme's id in `hip.id` |
| PHR_APP_206 | `sandbox`, `human` | As PHR_APP_106. The records show as the programme names them, such as each vaccine dose | As PHR_APP_106 |
| PHR_APP_207 | `sandbox`, `human`, optional | As PHR_APP_107, with the OTP sent by the programme | As PHR_APP_107 |
| PHR_APP_208 | not-run | As PHR_APP_108 | `not-run`: as PHR_APP_108 |
| PHR_APP_209 | `screen`, optional | As PHR_APP_109 | As PHR_APP_109 |
| PHR_APP_210 | `screen`, optional | As PHR_APP_110 | As PHR_APP_110 |
| PHR_APP_211 | `screen`, optional | As PHR_APP_111 | As PHR_APP_111 |

### PHR_APP_300: records linked by the facility, HIP initiated

Mandatory. The facility links the record with `m2_post_hip_v3_link_carecontext`, an M2 call your app does not make.

Loop limit: 3 passes per test case.

#### PHR_APP_300, linked records appear in the app. `sandbox` and `screen`

A facility links a record to the person's address. Exit condition: `p2_get_hip_v3_link_patient_links` returns 200 with `Patient.links[]` holding that HIP's `hip` and the linked `careContexts`, and Linked Facility shows the facility. Viewing the record needs the fetch in PHR_APP_108, which is `not-run` here.

### PHR_APP_400: an HIU views data under an approved consent

Mandatory. `not-run`: the HIU's fetch is `m3_post_data_flow_v3_health_information_request`, which no P specification publishes. The approval half is PHR_APP_1001.

Loop limit: 3 passes per test case.

### PHR_APP_50101 to PHR_APP_50103: share the profile by QR from the app

Mandatory.

Loop limit: 3 passes per test case.

| Case | Evidence | Check | Exit condition |
|---|---|---|---|
| PHR_APP_50101 | `sandbox`, `human` | The app shows the person's QR code in Profile, and the facility scans it | `p2_get_v3_phr_app_login_profile_qrcode` returns 202, as the specification states, and the screen shows the QR. The facility's scan is a human step. Screen check: after the scan the person sees the profile, the consent text, and Share and Cancel |
| PHR_APP_50102 | `sandbox`, `human` | Share sends the profile to the facility's system and a notification confirms it. Cancel sends nothing | Share: `p2_post_patient_share_v3_share` with `intent` `PROFILE_SHARE` and `metaData.hipId` returns 202, and the `p2_post_v3_hiu_patient_on_share` callback carries `acknowledgement.status` and `acknowledgement.abhaAddress`. Cancel: no share call in the log |
| PHR_APP_50103 | `sandbox`, `human` | The person scans the facility's counter QR with Scan in Profile, confirms, and shares | The person scans. As PHR_APP_50102, with `metaData.context` from the QR, and the callback carries `acknowledgement.profile.tokenNumber`, which the screen shows |

### PHR_APP_50201 to PHR_APP_50211: share the profile when the QR is scanned with the phone camera

Mandatory unless the sheet leaves a case unmarked. The camera, the app chooser and the store are the phone's, so each step is a person's.

Loop limit: 3 passes per test case.

| Case | Evidence | Check | Exit condition |
|---|---|---|---|
| PHR_APP_50201 | `screen`, `human` | App installed, person logged out: the scan offers the installed PHR apps and yours launches when chosen | A recording from scan to launch |
| PHR_APP_50202 | `screen` | Logged out, the app opens on the login page | The login page after launch |
| PHR_APP_50203 | `screen` | After login, the app opens the share profile page | The share profile page |
| PHR_APP_50204 | `sandbox`, `human` | Share sends the profile, Cancel does not | As PHR_APP_50102 |
| PHR_APP_50205 | `screen`, `human` | App installed, person logged in: the scan launches your app | A recording from scan to launch |
| PHR_APP_50206 | `screen` | The app opens the share profile page | The share profile page |
| PHR_APP_50207 | `sandbox`, `human` | As PHR_APP_50204 | As PHR_APP_50102 |
| PHR_APP_50208 | `screen`, `human` | App not installed: the scan opens a browser on the deep link page | A recording from scan to the page |
| PHR_APP_50209 | `screen`, `human` | The deep link page lists the ABDM compliant PHR apps, yours among them, and choosing one opens its store page | The page and the store page |
| PHR_APP_50210 | `sandbox`, `human` | After install, the person registers or logs in and lands on share profile | A P1 registration or login 200, such as `p1_post_v3_phr_app_enrollment_enrol` with `message` `ABHA Address Created Successfully`, then the share profile page |
| PHR_APP_50211 | `sandbox`, `human` | As PHR_APP_50204 | As PHR_APP_50102 |

### PHR_APP_600: profile details with KYC verified or self declared

Mandatory. `sandbox` and `screen`.

Loop limit: 3 passes per test case.

#### PHR_APP_600, the profile shows its status

`p2_get_v3_phr_app_login_profile` with `X-token` returns 200 with `abhaAddress`, `fullName`, `gender`, `mobile` and `kycStatus`. Screen check: every filled field is shown, with "KYC Verified" and a green tick when `kycStatus` is `VERIFIED`, and "Self-Declared" otherwise.

### PHR_APP_700: download the ABHA address card

Mandatory. `sandbox` and `screen`.

Loop limit: 3 passes per test case.

#### PHR_APP_700, the card shares and downloads

`p2_get_v3_phr_app_login_profile_phrcard` with `X-token` returns 202, as the specification states. Screen check: the card carries the photo, full name, ABHA address, mobile, and the ABHA number when one is linked, and it can be shared and saved.

### PHR_APP_901 to PHR_APP_903: the Requests section of the Consents tab

Mandatory. Each list covers consent, subscription and locker requests. `p4_get_subscription_requests_v3_patients_requests` with `X-AUTH-TOKEN` returns both consents and subscriptions, filtered by `status`.

Loop limit: 3 passes per test case.

**Preconditions.** A sandbox HIU has raised at least one consent request to the person's address with `m3_post_consent_v3_request_init`, an M3 call your app does not make.

| Case | Evidence | Check | Exit condition |
|---|---|---|---|
| PHR_APP_901 | `sandbox`, `screen` | Requests not yet acted on show under Requested | `p2_get_consent_v3_request` returns 200 with `requests[].status` `REQUESTED` for the request, and the screen lists it under Requested |
| PHR_APP_902 | `sandbox`, `human` | A denied request shows under Denied | The person taps Deny. `p2_post_consent_v3_request_request_id_deny` returns 202, then `p2_get_consent_v3_request_request_id` returns 200 with `status` `DENIED`, and the screen lists it under Denied |
| PHR_APP_903 | `sandbox`, `screen` | A request left past its expiry shows under Expired, never under Denied | `p2_get_consent_v3_request_request_id` returns 200 with `status` `EXPIRED`, and the screen lists it under Expired. `hiecm.concept.consent-in-a-phr-app` |

### PHR_APP_1001 and 10.2: the Approved section of the Consents tab

Mandatory.

Loop limit: 3 passes per test case.

| Case | Evidence | Check | Exit condition |
|---|---|---|---|
| PHR_APP_1001 | `sandbox`, `human` | A granted request shows under Granted | The person taps Approve. `p2_post_consent_v3_request_request_id_approve` returns 202 with `consentIds[].id`, then `p2_get_consent_v3_request_request_id` returns 200 with `status` `GRANTED`, and the screen lists it under Granted |
| PHR_APP_1002 | `sandbox`, `human` | A revoked consent shows under Revoked | The person taps Revoke. `p2_post_consent_v3_revoke` returns 202 with `message`, then `p2_get_consent_v3_artefact_artefact_id` returns 200 with `status` `REVOKED`, and the screen lists it under Revoked |

### PHR_APP_1101 to PHR_APP_1103: the My Records tab

Mandatory. Each needs a fetched record, which PHR_APP_108 cannot produce here. Attest on a record your app already holds.

Loop limit: 3 passes per test case.

| Case | Evidence | Check | Exit condition |
|---|---|---|---|
| PHR_APP_1101 | `screen` | Tapping a fetched record opens its details | The details screen |
| PHR_APP_1102 | `screen` | The details show HI type, date, time and the doctor or facility, with the attachment | The details screen |
| PHR_APP_1103 | `screen` | The attachment opens on the device | The opened report |

### PHR_APP_1201: the Linked Facility tab

Mandatory.

Loop limit: 3 passes per test case.

| Case | Evidence | Check | Exit condition |
|---|---|---|---|
| PHR_APP_1201 | `sandbox`, `screen` | Every linked provider is listed with Pull Records beside it | `p2_get_hip_v3_link_patient_links` returns 200 with one `Patient.links[]` entry per linked facility or programme, and `p4_get_subscription_requests_v3_patients_lockers` returns 200 with each locker. The screen lists every one, each with Pull Records |

### PHR_APP_1300 and PHR_APP_1400: edit a subscription, disable auto approval

Mandatory. These repeat PHR_HIU_10105 and PHR_HIU_10203. One run can serve both; quote both names.

Loop limit: 3 passes per test case.

| Case | Evidence | Check | Exit condition |
|---|---|---|---|
| PHR_APP_1300 | `sandbox`, `human` | A granted subscription's HI types, visit types and period can be edited and saved | The person edits and taps Save Changes. `p3_put_subscription_requests_v3_patients_subscription_id` returns 202 with `subscriptionId`, then `p3_get_subscription_requests_v3_subscription_id` returns 200 with `includedSources[].hiTypes` and `includedSources[].period` as saved |
| PHR_APP_1400 | `sandbox`, `human` | A granted auto approval policy can be disabled | The person taps Disable. `p2_post_consent_v3_auto_approve_auto_approval_id_disable` returns 202 with `message`, then the next request from that HIU shows `status` `REQUESTED` until the person acts |

### PHR_APP_1501 to PHR_APP_1506: link an ABHA number to the ABHA address

Mandatory.

Loop limit: 3 passes per test case.

| Case | Evidence | Check | Exit condition |
|---|---|---|---|
| PHR_APP_1501 | `screen` | A self declared profile shows "Link ABHA Number" beside "Self Declared" on home | The home screen |
| PHR_APP_1502 | `screen` | The 14 digit number is entered, and a malformed one is refused in the form | The entry screen and the refusal |
| PHR_APP_1503 | `sandbox`, `human` | The number is verified by Aadhaar OTP and by mobile OTP | `p2_post_v3_phr_app_login_profile_request_otp` with `loginHint` `abha-number` and `scope` `["abha-login","mobile-verify"]` with `otpSystem` `abdm`, or `["abha-login","aadhaar-verify"]` with `otpSystem` `aadhaar`, returns 200 with `txnId`. `p2_post_v3_phr_app_login_profile_verify` then returns 200 with `authResult` `success`. A wrong mobile OTP returns 200 with `authResult` `failed` and `message` `Please enter a valid OTP. Entered OTP is either expired or incorrect.` Run both OTP systems |
| PHR_APP_1504 | `screen`, `sandbox` | Resend is offered after 60 seconds for both OTPs | A second request OTP 200 at least 60 seconds after the first, and PHR_APP_1503 then passes |
| PHR_APP_1505 | `sandbox`, `screen` | The link succeeds, the message says so, and the profile takes the ABHA number's details | `p2_post_v3_phr_app_login_profile_link` with `action` `LINK` and the `transactionId` returns 200 with `message` `ABHA number is securely linked to ABHA address` and `authResult` `success`. Then `p2_get_v3_phr_app_login_profile` returns 200 with `abhaNumber` present. Screen check: the congratulations message, the number on profile and home, and "Self-Declared" gone |
| PHR_APP_1506 | `screen` | "Go back to home screen" returns home | The home screen |

### PHR_APP_1701 and 17.3: edit a KYC verified profile

Mandatory. The sheet has no 17.2.

Loop limit: 3 passes per test case.

| Case | Evidence | Check | Exit condition |
|---|---|---|---|
| PHR_APP_1701 | `sandbox`, `human` | The mobile is updated after an OTP on the new number | `p2_post_v3_phr_app_login_profile_request_otp` with `scope` `["abha-address-profile","mobile-verify"]` and `loginHint` `mobile-number` returns 200 with `txnId`. `p2_post_v3_phr_app_login_profile_verify` returns 200 with `message` `Mobile Number linked successfully` and `authResult` `success`. A wrong OTP returns 200 with `authResult` `failed` and `message` `Entered OTP is incorrect. Kindly re-enter valid OTP.` |
| PHR_APP_1703 | `sandbox` | Address line, district, state and pin code are updated | `p2_post_v3_phr_app_login_profile_updateprofile` returns 200 with `address`, `districtName`, `stateName` and `pinCode` equal to the values sent |

### PHR_APP_1801 to PHR_APP_1807: edit a self declared profile

Mandatory. The sheet has no 18.6.

Loop limit: 3 passes per test case.

| Case | Evidence | Check | Exit condition |
|---|---|---|---|
| PHR_APP_1801 | `sandbox`, `screen` | The photo is updated from the camera or the gallery | `p2_post_v3_phr_app_login_profile_updateprofile` with `profilePhoto` returns 200. The response carries no photo field, so the screen check is the new photo on the profile |
| PHR_APP_1802 | `sandbox` | First, middle and last name are updated | The same call returns 200 with `firstName`, `middleName` and `lastName` as sent |
| PHR_APP_1803 | `sandbox` | Gender is updated | 200 with `gender` as sent |
| PHR_APP_1804 | `sandbox` | Day, month and year of birth are updated | 200 with `dayOfBirth`, `monthOfBirth` and `yearOfBirth` as sent |
| PHR_APP_1805 | `sandbox`, `human` | As PHR_APP_1701 | As PHR_APP_1701 |
| PHR_APP_1807 | `sandbox` | As PHR_APP_1703 | As PHR_APP_1703 |

### PHR_APP_1901 to PHR_APP_1908: the deep link flow

Mandatory. The facility's notice and the SMS are not your app's calls.

Loop limit: 3 passes per test case.

| Case | Evidence | Check | Exit condition |
|---|---|---|---|
| PHR_APP_1901 | `human` | The person is registered at a facility that holds a record but no ABHA address for them | A facility partner confirms it. Without one, `needs-human`, after it was asked for at readiness and again at the case, and the tester said they cannot provide it in this run |
| PHR_APP_1902 | not-run | The facility notifies ABDM of the new record with the mobile and its HIP code | `not-run`: the notice is `m2_post_hip_v3_link_patient_links_sms_notify2`, an M2 call no P specification publishes |
| PHR_APP_1903 | `human` | ABDM sends the SMS with the deep link | The SMS on the person's phone, read out or photographed |
| PHR_APP_1904 | `screen`, `human` | The link opens the list of ABDM compliant PHR apps, yours among them | The list |
| PHR_APP_1905 | `screen`, `human` | App not installed: choosing it opens the store to install it | A recording from the list to the installed app |
| PHR_APP_1906 | `sandbox`, `human` | After install, a new person registers an address and password | As PHR_APP_50210. The sheet also names registration by email, which the P1 specification does not publish |
| PHR_APP_1907 | `sandbox`, `screen` | App installed: it launches into user initiated linking for that facility | The app opens on discovery, and `p2_post_user_initiated_linking_v3_patient_care_context_discover` returns 202 with `hip.id` equal to the HIP code in the link, followed by the on-discover callback as PHR_APP_105 |
| PHR_APP_1908 | `sandbox`, `human` | Scanning the facility QR gets a token number, and the records link as in PHR_APP_101 to PHR_APP_111 | The person scans. As PHR_APP_50103, with `acknowledgement.profile.tokenNumber` shown. Then PHR_APP_105 and PHR_APP_106 on that facility |

### PHR_APP_2001: scan and share with no PHR app installed

Mandatory. `sandbox` and `human`.

Loop limit: 3 passes per test case.

#### PHR_APP_2001, the token is shown after sharing

The person scans the facility QR with the phone camera, installs the app and logs in. Exit condition: `p2_post_patient_share_v3_share` returns 202 and the `p2_post_v3_hiu_patient_on_share` callback carries `acknowledgement.profile.tokenNumber`. Screen check: the consent statement before Share, the notification that the profile went to the hospital, and the token number after OK. `p2_get_patient_share_v3_profile_gettokendetails` then returns 200 with an entry holding that `tokenNumber` and the facility's `hipId`.

## The environment survey

Run this before the readiness check, and before any other script in this section. `/abdm-test-setup`, `/abdm-build` and `/abdm-test` run it first.

### In plain words

A run stalls when the interpreter on PATH lacks a library the workbook writer
needs, or when a browser is installed that nobody told the Playwright server
about. The survey finds what the machine already has before asking for
anything, and writes it to `abdm-ft/env.json`. Every later script runs under
the interpreter that file names.

| What | How it is found | Chosen |
|---|---|---|
| Python | `python3` and `python3.x` on PATH, Homebrew's prefixes, every pyenv version, every conda environment, `uv python find` and uv's own builds, any `.venv` or `venv` in the project, and `abdm-ft/.venv`. Each is asked for its version and whether `openpyxl`, `PIL`, `docx` and `reportlab` import, in isolated mode (`-I`, or `-E -s` where `-I` is refused), from a new empty folder, with only `PATH`, `HOME` and `LANG` set, so no file in the project and no `PYTHONPATH` or `PYTHONSTARTUP` runs | The newest Python 3.9 or later with all four; between two of the same version, the one found first, in that order with `abdm-ft/.venv` first. Else the newest 3.9 or later, with the missing libraries named |
| A browser, for a `ui` run | An installed Google Chrome, an installed Microsoft Edge, and Playwright's own Chromium in its cache | Chrome first, because the Playwright server launches it with no option. Edge needs `--browser msedge`, Playwright's Chromium `--browser chromium`, or the same value in `PLAYWRIGHT_MCP_BROWSER` |
| A PHR app on Android, with `--phr-package` | `adb` on PATH or in the Android SDK, a line ending in `device` from `adb devices`, the package on that device, and `maestro` | Each is `true` or a path when present |

Whether the Playwright MCP tools are loaded is a question about your agent,
not the machine, so the script cannot answer it. Read your tool list for
`browser_navigate`, `browser_snapshot`, `browser_click`, `browser_type` and
`browser_take_screenshot`, under whatever prefix your agent gives the
`playwright` server, and record the answer beside the survey.

### Before you start

- Python 3, any version, on PATH as `python3`, to run the survey itself. It
  imports nothing outside the standard library, and runs itself again in
  isolated mode when started without `-I`, before it imports anything.
- NHA's workbook copied into `abdm-ft/` from the skill's `workbook/` folder.
  The first command refuses, exit 4, until it is there:

  ```bash
  python3 -I abdm-ft/abdm_env.py workbook --skill <this skill folder>
  ```
- The script, `scripts/abdm_env.py` in the test skill's folder, copied to
  `abdm-ft/abdm_env.py`.

### What happens

1. Run the survey. Add `--run ft` for an API run with no browser, and
   `--phr-package <package>` for a PHR app on Android.

   ```bash
   python3 -I abdm-ft/abdm_env.py survey --run ui
   ```

   It prints one line per interpreter with what it lacks, the one chosen and
   why, the browser and its `--browser` value, and `ready`, or `missing:` and
   what is missing. It exits 0 when ready, 1 otherwise.
2. A later survey reuses `abdm-ft/env.json` only for the same run type and
   PHR package, while nothing was missing, the interpreter it names still
   imports all four libraries, and for a `ui` run Node and the browser still
   answer. It prints `reusing`. `--refresh` surveys again.
3. When a library is missing, the survey prints the one install that fixes
   it: create `abdm-ft/.venv` from the newest Python 3.9 or later that is not
   itself a virtual environment and does not live in the project, then install
   `openpyxl`, `pillow`, `python-docx` and `reportlab` into it, `reportlab<5`
   on Python 3.9, with pip's fixed arguments and no index of anyone's
   choosing. Ask the tester once whether to run it. On a yes:

   ```bash
   python3 -I abdm-ft/abdm_env.py install --yes
   ```

   It surveys again and builds the two commands from what it just found,
   never from `env.json`, runs them with a timeout each, then surveys again.
   Without `--yes` it installs nothing, prints the exact commands for the
   tester to run, and exits 3. It refuses, exit 5, when `abdm-ft`,
   `abdm-ft/.venv` or `env.json` is a symlink. Never install into the system's
   Python, a Homebrew or conda base, or the project's own environment.
4. When no browser is reachable, the survey prints
   `npx playwright install chromium`. Offer it in the same question as the
   Python install, run it only on the tester's yes, and then start the
   Playwright server with `--browser chromium`.
5. Every script after this runs under the interpreter `env.json` names.
   Wherever a section says `python3 -I abdm-ft/abdm_workbook.py` or
   `python3 -I abdm-ft/abdm_report.py`, run it under that interpreter, always
   with `-I`:

   ```bash
   "$(python3 -I abdm-ft/abdm_env.py python)" -I abdm-ft/abdm_report.py plan <workbook> --run ui --entity private
   ```

### How you know it worked

The survey prints `ready` and exits 0, `abdm-ft/env.json` names an
interpreter whose four libraries are listed with their versions, and for a
`ui` run it names a browser. The file holds paths, versions and a timestamp,
never a credential or an identity value.

### When it goes wrong

| What you see | Why | Do this |
|---|---|---|
| `missing: Python 3.9 or later` and no install command | No interpreter on the machine is 3.9 or later | Install Python 3.9 or later from https://www.python.org/downloads, then survey again |
| `missing: python library ...` | The newest Python lacks a library | Ask the tester, then `install --yes`. Without a yes, print the command |
| `missing: a browser` | No Chrome, Edge or Playwright Chromium was found | Ask the tester, then `npx playwright install chromium`, and start the Playwright server with `--browser chromium` |
| `browser_navigate` names a missing browser although the survey found one | The server was started for a different browser | Start it with the `--browser` value the survey printed |
| `missing: Node 18 or later` | The Playwright server runs on `npx` | Install the LTS release from https://nodejs.org |
| `ModuleNotFoundError` from a script after a `ready` survey | The script was run with another `python3` | Run it under `"$(python3 -I abdm-ft/abdm_env.py python)" -I` |
| A library the tester installed with `pip install --user` is reported missing | Isolated mode ignores the user's site folder, as every script here runs | Install into `abdm-ft/.venv` on the tester's yes |
| `refused: ... is a symlink` | `abdm-ft`, `abdm-ft/.venv` or `env.json` points somewhere else | Make it a plain folder or file in the project, then run it again |
| `refused: NHA's workbook ... is not in abdm-ft` | The workbook was not copied, or was renamed | Copy it from the skill's `workbook/` folder under its own name |

### The script

The script ships with this skill as `scripts/abdm_env.py`. Copy `abdm_env.py` from there; never retype it or rebuild it from this text:

```bash
mkdir -p abdm-ft && cp <this skill folder>/scripts/abdm_env.py abdm-ft/abdm_env.py
```

From `shared.concept.test-environment`.

## Running a case as a UI test

Read this section when the run type is `ui` or `all`. Its readiness check joins the one above, and every rule above still holds.

### In plain words

A test section runs each case in one of three run types. The tester picks one at the start.

| Run type | What it drives | Judged on |
|---|---|---|
| `ft` | Your system's calls against the sandbox. No browser | Calls, responses and callbacks |
| `ui` | Your application's screens: a web application through the Playwright MCP server, a PHR app on Android through Maestro | What the screen shows at each step of the sheet |
| `all` | `ft` for every chosen case first, then `ui` for the same cases | Each type on its own, in its own columns |

`all` runs `ft` first so a backend fault shows before the slower `ui` run starts. A case that has no meaning in a run type, such as a "Create ABHA option is visible" case in an `ft` run, is recorded `not-run` with the run type as the reason.

One rule above changes in a `ui` run, and only this one. In an `ft` run a `screen` case is attested: the tester answers `passed? y/n` and names the screenshot. In a `ui` run the agent observes the screen itself, and its screenshot plus the literal on-screen text it matched replace the tester's attestation. Every other rule above still holds in a `ui` run.

### Before you start

The `ui` readiness check runs before the first case. Check every row the chosen cases need, in order. Record each as `ready`, `missing` or `not needed`, never the value itself.

| Need | Ready when | Install command to print when missing |
|---|---|---|
| Node 18 or later | `node --version` prints `v18` or higher | Install the LTS release from https://nodejs.org |
| The Playwright MCP server, connected | Your tool list holds `browser_navigate`, `browser_snapshot`, `browser_click`, `browser_type`, `browser_take_screenshot`, `browser_console_messages`, `browser_network_requests` and `browser_network_request`. These are the names the server publishes. Read your own tool list for the exact names, which some agents prefix with the server name | Claude Code with the integrators plugin: restart Claude Code. The plugin starts `@playwright/mcp@0.0.83`, and the first start downloads that version with `npx`. Claude Code without it: `claude mcp add playwright npx @playwright/mcp@0.0.83`. Any other agent: an MCP server with command `npx` and argument `@playwright/mcp@0.0.83` |
| A browser | `browser_navigate` to the application URL returns a page, not an error naming a missing browser | Install the browser the server's error names, and only that one: `npx playwright install chrome` when it names Chrome, `npx playwright install chromium` when it names Chromium |
| The application URL | `browser_navigate` to it and `browser_snapshot` shows your application's first screen | Ask the tester for it |
| A tester login | Read from an environment variable the tester names, or typed at a hidden prompt, and the login screen accepts it | Ask the tester for it |
| The server log command, optional | The tester gave a command that prints your server's log for a time window, such as `docker logs --since <start> --until <end> <container>` | `not needed` when none is given |
| For a PHR app on Android: a device | `adb devices` lists one line ending in `device` | Android Studio's emulator from https://developer.android.com/studio, or a phone with USB debugging on |
| For a PHR app on Android: the app | `adb shell pm list packages <package>` prints `package:<package>` | `adb install <path to the APK>` |
| For a PHR app on Android: Maestro | `maestro --version` prints a version | `curl -fsSL "https://get.maestro.mobile.dev" \| bash` |

When a row is `missing`: stop, print its install command exactly as above, and wait for the tester to say it is done. Then check the row again. The one exception is an install `shared.concept.test-environment` prints, which you run only after the tester says yes. Never install anything without that yes. After 3 checks of the same row still `missing`, stop the run and ask one question about that row.

### What happens

For each chosen case, at most 3 passes. Record the number of passes the case took, whether it ended passed or failed.

1. Read the case's Steps and Expected Result from its workbook row, and its exit condition from the skill's case.
2. Follow the Steps in order, on the surface the step names: the web application, or the PHR app.
   - Web: `browser_navigate`, `browser_click`, `browser_type`, reading `browser_snapshot` before each action to find the element. `browser_type` is for fields that hold no person's value, such as a search term or a free text note. Every identity, document, code or password field is the tester's, as the rule at the top of this skill sets out.
   - PHR app: write the steps as Maestro flows at `abdm-ft/maestro/<case id>-<n>.yaml` and run each with `maestro test`. A flow ends at the field before any identity, code or approval step, the tester types it on the emulator or phone, and the next flow starts after it.
3. After step `n`, save one screenshot and the screen's text:

   | Surface | Screenshot | Screen text |
   |---|---|---|
   | Web | `browser_take_screenshot` with `filename` `abdm-ft/evidence/<case id>/<n>.png`. The server resolves that name against its workspace root, which need not be your repository, so save or move the file to `abdm-ft/evidence/<case id>/<n>.png` and check that the file exists there | `browser_snapshot` output saved to `abdm-ft/evidence/<case id>/<n>.txt` |
   | PHR app | `- takeScreenshot: abdm-ft/evidence/<case id>/<n>` in the flow | `maestro hierarchy` output saved to `abdm-ft/evidence/<case id>/<n>.txt` |

   The screen text shows what was typed into each field. Before saving it, replace the value of every field the tester typed an identity value, an OTP or a password into with `[redacted]`. For an OTP step, take the screenshot only after the field is submitted or cleared. Never save a snapshot or screenshot that shows an OTP, password or full Aadhaar number.

4. An Aadhaar number, a mobile number, an OTP, an ABHA number or address, a password, a captcha, a document number, a biometric or an approval stops the case. Click the field, then ask the tester to type the value into the browser or the app themselves and reply `done`, as the rule at the top of this skill sets out. Never type it yourself, never ask the tester to tell you the value, and never put in a made-up, sample or earlier value. In the run log write `<Aadhaar entered by tester>`, `<OTP entered by tester>` and so on in its place.
5. Judge the case, by the rules in "Judging a case" below. The case's exit condition and screen check in this skill are how the sheet's Expected Result is observed. The case is `passed` when the screen holds both. Paste into the run record the exact on-screen text or element you matched for each. When one holds and the other does not, the pass failed: say which one is missing. When the skill's case gives no literal for the screen, quote the on-screen text you believe satisfies the sheet's Expected Result, and record the case `needs-human` with `tester to confirm` in Remarks. Never record such a case `passed`.
6. Write the result into the workbook, as `shared.concept.test-workbook-write-back` sets out, with `--run ui`. A `ui` run fills NHA's own columns:

   ```bash
   python3 -I abdm-ft/abdm_workbook.py write <workbook> --run ui --results abdm-ft/<workbook name>-ui-results.json --executed-by "<tester>" --application "<your application>"
   ```

#### Judging a case

The sheet has three columns that describe a case. Each has one job.

| Column | What it does |
|---|---|
| Expected Result | Decides the outcome. Met is a pass, not met is a FAIL |
| Steps To Be Executed | Says what to do. Follow them in order |
| Suggestions for the Functional Tester | Guidance. A gap against it, with the Expected Result met, is a `conditional-pass`, never on its own a FAIL |

| Situation | Outcome |
|---|---|
| The Expected Result is met, and nothing beside it falls short | `passed` |
| The Expected Result is met, with a gap beside it, such as a Suggestions point not met | `conditional-pass`, with the condition in Remarks: what was met, then the gap |
| The screen shows a message that means the same as the one the sheet quotes, in other words | `passed`. Quote both in the run record |
| The Expected Result is not met | `failed`, with the reason |
| A case with several steps: every step run passes, and a step could not be run | `needs-human`, naming that step and what it needs, after the tester was asked for it |
| A case with several steps: a step's expected result fails | `failed`, naming that step. Only a failing step makes the case FAIL |

A case with several steps records a result for each step in its result's `steps`, as `shared.concept.test-workbook-write-back` sets out, so the report shows which step held and which did not.

#### The PHR proof rule

When the run is for an HMIS, an EMR or another HIP, or for an HIU, and a step happens in the PHR app (logging in, approving or denying a consent, pulling records), do not drive the PHR app.

1. Ask the tester for one PHR app screenshot that shows the consent granted or the record visible.
2. Save it as `abdm-ft/evidence/<case id>/phr.png`.
3. Check your application's side of the same step on screen, as in step 3 above.

The case passes only when the PHR screenshot shows the step and your application's screen holds the exit condition. No screenshot from the tester is `needs-human`, with `Needs tester: PHR screenshot` in Remarks.

#### Finding the reason for a failure

Collect the reason in this order, and stop at the first one that names it. The Remarks value carries the reason and the code or exception.

| Order | Look at | How | What it gives |
|---|---|---|---|
| 1 | The error on screen | `browser_snapshot` | The message your application shows |
| 2 | Failed network responses | `browser_network_requests` lists the requests. For each with a status of 400 or above, `browser_network_request` with its number shows the body. When it shows no body, take the error code from the on-screen message or from the `ft` replay at order 4 | The HTTP status and an ABDM error code such as `ABDM-1204` |
| 3 | Browser console errors | `browser_console_messages` | A script error that stopped the screen |
| 4 | One replay of the case's `ft` calls | The same skill's case, run once as `ft`, when the sandbox credentials are ready | The ABDM error code. Diagnosis only: the `ui` outcome stays as observed |
| 5 | Your server log for the case's time window | The command from the readiness check, with the case's start and end times | A backend exception such as `NullPointerException`, which usually appears nowhere else |

```json
{"id": "CRT_ABHA_105", "outcome": "failed", "response": "Screen: Invalid OTP. POST /v3/enrollment/request/otp HTTP 400 ABDM-1204", "iterations": 3, "remarks": "OTP field sent empty; server log: NullPointerException in OtpService.verify"}
```

### How you know it worked

- `abdm-ft/evidence/<case id>/` holds one `<n>.png` and one `<n>.txt` for every step run, and `phr.png` for a case under the PHR proof rule.
- The run log pastes, for every `passed` case, the exact on-screen text or element that matched the exit condition and the screen check.
- A case whose screen check has no literal in the skill is `needs-human` with `tester to confirm`, never `passed`.
- Every `conditional-pass` names its condition in Remarks, and every multi step case lists its steps there.
- `write` exits `0`, and the workbook row shows `PASS`, `CONDITIONAL PASS`, `FAIL`, `NA` or blank in `Status(PASS/FAIL)`, a number from 1 to 3 in `Iterations`, and the reason in `Remarks`.
- No OTP, password, token or full Aadhaar number appears in the run log, a screenshot, the evidence text, the results file or the workbook. Every typed OTP, password or Aadhaar field in a `.txt` file reads `[redacted]`.

### When it goes wrong

| Observed | Cause | Do |
|---|---|---|
| No `browser_navigate` in the tool list | The Playwright MCP server is not connected | Print the install command from the readiness table, wait, check again |
| `browser_navigate` fails naming a browser that is not installed | No browser installed | Print the install command from the readiness table, wait, check again |
| The screenshot shows the login screen mid case | The tester's session expired | Log in again, count it as a new pass |
| `adb devices` lists nothing, or `unauthorized` | No emulator running, or the phone has not accepted the computer | Start the emulator, or accept the prompt on the phone |
| `maestro: command not found` | Maestro is not installed or not on the path | Print its install command, wait, check again |
| A step's element is not in the snapshot | The screen differs from the sheet's Steps | Record the pass as failed with the snapshot line nearest to it. Never invent a step |
| You are about to record FAIL because a Suggestions for the Functional Tester point is not met | The Suggestions column is guidance, not the Expected Result | Check the Expected Result. Met: `conditional-pass` with the gap as the condition. Not met: `failed` |
| The screen's message reads differently from the one the sheet quotes | The wording differs, the meaning may not | Same meaning: `passed`, with both quoted. Different meaning: `failed` |
| A multi step case has a step that could not be run | A person, device or value the step needs is missing | Ask the tester for it. If they cannot provide it in this run, the case is `needs-human` naming the step; never collapse it to FAIL |
| The reason is still unknown after all five sources | The fault leaves no trace a `ui` run can see | Record `failed` with what each source showed, and ask the tester for the server log command |

From `shared.concept.test-ui-run`.

## Writing results into NHA's workbook

Ask for the workbook in the readiness check, and copy `scripts/abdm_workbook.py` from this skill to `abdm-ft/abdm_workbook.py` before that check, because the check runs it. Every case ends by writing its result into the sheet as below, after the run log row is updated. The workbook is required: the report is built from it.

### In plain words

NHA publishes each module's functional test cases as a workbook, one row per
case id. A test run writes its result into that same workbook: the status, the
response or error code that decided it, how many passes the case took, and a
remark. The run log and the manifest stay as they are. The workbook is what a
reviewer reads.

A run is one of two types, and each writes to its own columns, so one never
overwrites the other:

| Run type | What it tests | Columns it fills |
|---|---|---|
| `ft`, API functional test | Your system against the sandbox, judged on calls and callbacks | `FT Status`, `FT Response with Code`, `FT Iterations`, `FT Remarks`, added after the last column |
| `ui`, UI test | Your screens, driven by an agent | NHA's own `Status(PASS/FAIL)`, `Response of Request with Code`, `Iterations`, `Remarks`, the first of each where a sheet has two. A sheet without one gets it added. Where NHA already wrote in that cell, such as the consent language in HPR's Remarks, NHA's text stays and the value goes in an added `<column> (run)` column |

| Outcome | Status written | Remarks written |
|---|---|---|
| `passed` | `PASS` | what matched |
| `conditional-pass` | `CONDITIONAL PASS` | the condition: the Expected Result was met, and what beside it was not, such as a gap against the Suggestions for the Functional Tester column. Required: the writer refuses a conditional pass with empty remarks |
| `failed` | `FAIL` | the reason: the HTTP status and ABDM error code, the missing callback, or the exception from your server log |
| `needs-human` | blank | `Needs tester:`, the step that was left, and what the tester was asked for |
| `not-run` | `NA` | why it does not apply |

A case with more than one step can carry its step results. Each step is `passed`, `failed`, `needs-human` or `not-run`, with a short note, and the writer appends them to Remarks in one line, such as `Steps: 1 PASS; 2 needs tester (a second ABHA address); 3 PASS`. A step waiting on a person, a device or test data is `needs-human`, never `not-run`. The report shows the steps as a table in the case's evidence. The writer refuses steps that disagree with the case's outcome:

| Outcome | Its steps |
|---|---|
| `passed` | Every step `passed` |
| `conditional-pass` | No step `failed` |
| `failed` | Any steps; a case with a `failed` step is always `failed` |
| `needs-human` | At least one step `needs-human`, and no step `failed` |

The writer masks the Response and Remarks values before it writes them, with the same masking block as the report: a 12 digit Aadhaar number, with or without spaces or hyphens, reads `XXXX XXXX 1234`, a 14 digit ABHA number, with or without hyphens, `XX-XXXX-XXXX-1234`, a 10 digit mobile, with or without `0`, `91` or `+91` in front and spaces or hyphens inside, `XXXXXX1234`, an ABHA address ending `@sbx` or `@abdm` keeps the last four characters before the `@` with the rest as `x` (four or fewer are all `x`), a JWT reads `[token]`, a value after a password, secret, token or OTP label, and a `Bearer` credential, read `[redacted]`, and the value of a profile field (`firstName`, `middleName`, `lastName`, `fullName`, `name`, `dob`, `dayOfBirth`, `monthOfBirth`, `yearOfBirth`, `address`, `pinCode`, `photo`, `profilePhoto`, `kycPhoto`, `email`), in JSON or as `key: value` in any case, reads `[masked]` to the end of its clause, so `Name: asha verma, then` reads `Name: [masked], then`. A date of birth after `dob`, `DOB` or `date of birth` reads `[masked]`, and any date written dd/mm/yyyy reads `[date]`. Masking is the backstop, not the rule: never put an identity value in a result in the first place. A name written with no label, such as `asha verma saw the screen`, cannot be told from other words and is not masked: the agent never writes a name.

### Before you start

- The workbook for the module, copied from the test skill's own `workbook/` folder into your repository beside the run log, such as `abdm-ft/M1_ABHA_CREATION_VERIFICATION_SCAN_AND_SHARE.xlsx`. The skill ships the sheet its cases come from, so use that copy rather than another. M4 has two, HFR and HPR: each is its own workbook with its own results file. The PHR workbook has no case ids: the script builds them from each tab and row, `PHR_ADDR_302` for row 3.2 of the ABHA address creation tab, exactly as the test case page shows them, and its first write adds a `Test Case ID` column holding them.
- Python 3.9 or later with `openpyxl` and `Pillow`. Pillow is what keeps the sheet's logos when it is saved. The environment survey, `shared.concept.test-environment`, finds an interpreter that has them, or installs them into `abdm-ft/.venv` on the tester's yes. Run the script under the interpreter it names.
- The script `scripts/abdm_workbook.py` from this skill, copied to `abdm-ft/abdm_workbook.py` before the readiness check, which runs it.

The script ships with this skill as `scripts/abdm_workbook.py`. Copy `abdm_workbook.py` from there; never retype it or rebuild it from this text:

```bash
mkdir -p abdm-ft && cp <this skill folder>/scripts/abdm_workbook.py abdm-ft/abdm_workbook.py
```

### What happens

1. Before the first case, list the case ids the workbook holds:

   ```bash
   python3 -I abdm-ft/abdm_workbook.py cases <workbook>
   ```

   Run the cases in that list. A case id the skill carries that the workbook lacks is a different version of the set: say so before the run starts.

2. After every case, append its result to `abdm-ft/<workbook name>-<run type>-results.json`, one file per workbook, one object per case:

   ```json
   {"id": "CRT_ABHA_105", "outcome": "failed", "response": "POST /v3/enrollment/request/otp HTTP 400 ABDM-1204", "iterations": 3, "remarks": "Invalid OTP value; OTP read from the wrong field"}
   ```

   A case with steps adds them, and a conditional pass names its condition in `remarks`:

   ```json
   [{"id": "SHARE_PATIENT_PROFILE_702", "outcome": "needs-human", "iterations": 1, "remarks": "a second ABHA address, asked at readiness and at the case; the tester has none for this run", "steps": [{"step": 1, "outcome": "passed"}, {"step": 2, "outcome": "needs-human", "note": "a second ABHA address"}, {"step": 3, "outcome": "passed"}, {"step": 4, "outcome": "passed"}]},
    {"id": "CRT_ABHA_113", "outcome": "conditional-pass", "iterations": 1, "remarks": "Expected Result met: ABHA number and address shown with the profile. Condition: <the Suggestions for the Functional Tester point that was not met>"}]
   ```

   `id` is the id `cases` printed. A sheet that repeats an id lists the repeat as `<id>#2`, such as HFR's second bridge linkage, `HFR-118#2` to `HFR-123#2`, and the result for that row uses that id. `iterations` is the number of loop passes the case took, whether it ended passed or failed. Never put a token, OTP, Aadhaar number or full mobile in any field.

3. Then write it into the workbook, so a stopped run loses nothing:

   ```bash
   python3 -I abdm-ft/abdm_workbook.py write <workbook> --run ft --results abdm-ft/<workbook name>-ft-results.json --executed-by "<tester>" --application "<your system>"
   ```

   The first write copies the workbook to `<name>.before-abdm-run.xlsx` beside it, and every later write reads that copy to tell NHA's text from the run's. Every write rewrites the result cells of every case in the file, so writing again is safe. The header block above the table gets the tester, the date and the application, and a later write updates them. When NHA sends a new version of the workbook, delete the old `.before-abdm-run.xlsx` before the first write to it.

4. To resume, list the cases whose status for the run type is not `PASS`, `CONDITIONAL PASS` or `NA`, and run only those. With `--entity`, the list also holds every `NA` on a case mandatory for that entity type, marked `(NA on a mandatory case)`, because the report's gate blocks it unless NHA agreed it is not run. Read the list out before the tester leaves, so every case still waiting for them is done or asked about while they are there:

   ```bash
   python3 -I abdm-ft/abdm_workbook.py open <workbook> --run ft --entity private
   ```

### How you know it worked

- `write` exits `0` and prints nothing. It exits `1` and prints `not in the workbook: <id>` for any case id the workbook does not hold.
- Opened again, each case's row shows the status, response, iterations and remark you wrote. Every cell NHA filled holds what NHA put there, with its formatting, and the logos, merged cells, column widths and frozen panes are as NHA sent them. Two parts of the file are not kept: printer settings and SharePoint metadata, neither of which a reader sees.
- `open` lists exactly the cases whose status is not `PASS`, `CONDITIONAL PASS` or `NA`, in any case and spacing, and with `--entity` each `NA` on a mandatory case too.
- Every `CONDITIONAL PASS` row carries its condition in Remarks, and a case with steps reads `Steps: ...` at the end of its Remarks.
- No Aadhaar number, ABHA number, ABHA address, mobile, token or OTP appears in the Response or Remarks columns: each reads in its masked form.

### When it goes wrong

| Observed | Cause | Do |
|---|---|---|
| `ModuleNotFoundError: No module named 'openpyxl'` | The script ran under an interpreter without it | Run it under `"$(python3 -I abdm-ft/abdm_env.py python)" -I`. If the survey found none with it, install into `abdm-ft/.venv` on the tester's yes |
| The logos are gone after a write | Pillow is missing, so the images were not read | Install Pillow, copy `<name>.before-abdm-run.xlsx` back over the workbook, and write again |
| `case id on more than one tab` | Two tabs carry the same id | Ask NHA which one counts. The script writes neither until it is resolved |
| `not in the workbook: <id>` | The workbook is another version of the set, or the id was typed with a space | Check the id against `cases`. Never rename a case to fit |
| `outcome must be one of` | An outcome outside the five | Use `passed`, `conditional-pass`, `failed`, `needs-human` or `not-run` |
| `a conditional-pass needs remarks naming the condition` | A conditional pass with empty remarks | Write the condition in `remarks`: what was met, and the gap beside it |
| `a step's outcome must be one of` | A step outcome outside the four a step can have | Use `passed`, `failed`, `needs-human` or `not-run` for each step |
| `steps disagree with the outcome` | The case's outcome does not follow from its steps, such as `passed` with a step not passed | Set the outcome by the table above: a `failed` step makes the case `failed`; a step waiting on the tester makes it `needs-human` |
| `open` lists `<id> (NA on a mandatory case)` | A case mandatory for your entity type is recorded `NA` | Run it, or record why with NHA's agreement in `agreed_not_run` for the report |
| An identity value shows in clear in Remarks | It was written by an earlier version of the writer, or in a form the masking does not know | Correct the results file so it names the value by its last four characters, and write again |
| The workbook will not open in Excel while the run writes | Excel holds a lock on it | Close it in Excel; the next write catches up |

From `shared.concept.test-workbook-write-back`.

## Writing the test report

Copy `scripts/abdm_report.py` from this skill to `abdm-ft/abdm_report.py` before the readiness check, and make the run log from its `plan`. When the last case is recorded, run `check`. Run `build` only when `check` exits 0.

Pass `--tab "PHR App Functionality"` to `plan`, `check` and `build`. NHA's PHR workbook holds one tab per PHR milestone and NHA asks for one report per milestone, so without it this run is blocked by the other three tabs' cases and the report counts them as not tested.

The report prints each case as a numbered step under the test case it belongs to, the way NHA's own reports read, so the run has two things to collect that no script can work out.

| Collect | When | Where it goes |
|---|---|---|
| One line per case saying what was done and what the screen showed, in the past tense | As the case runs, from the step and the screen in front of you. Never written later from the sheet alone | `narration` in `meta.json`, keyed by case id |
| One screen recording per test case | Once that test case is finished | `recording_links` in `meta.json`, keyed by the name of the test case as the sheet heads it |

Where a case has no narration the report prints the sheet's own line for it, which says what the case checks but not what happened. Collect the narration: it is the difference NHA asked for.

### In plain words

A functional test run ends with NHA's workbook filled in, one row per case.
The test report is the document a reviewer signs off: who tested what, when,
with which tools, the status of every case, and the evidence for each one. It
is built from the workbook, not written by hand, so the report and the sheet
always agree.

The report is written only when the run has passed. Its gate is every
mandatory case that applies to your entity type, government or private, with
status `PASS` or `CONDITIONAL PASS` in the run type's column. A mandatory case
that is `FAIL` or blank stops it, and so does one that is `NA` unless NHA
agreed it is not run.

A conditional pass is a case whose Expected Result was met with a gap beside
it, such as a point in the sheet's Suggestions for the Functional Tester column
that was not met. It passes the gate only with its condition written in
Remarks, and the report counts it on its own and lists every one with its
condition, so a reviewer sees each gap.

The same script also plans the run. `plan` lists every case in the workbook,
in sheet order, with its group, what it is for your entity type and its status
so far. The run log is made from that list, so no case is ever dropped.

| Run type | Report | Evidence per case |
|---|---|---|
| `ui` | The exit report NHA asks for | The screenshots under `abdm-ft/evidence/<case id>/` |
| `ft` | The same structure | The `FT Response with Code` value in place of screenshots |

The report is a PDF, the file you submit, with a Word copy beside it that you
can edit. Both hold the same text. The PDF is drawn in the ABDM Sandbox design
system: a navy cover with the key facts in a ruled grid, a contents list with
page numbers, each section opened by a rule and a numbered label, and hairline
tables. Every page after the cover carries the report title in its header and
`Confidential` with `Page N of M` in its footer. Cases flow on from one another;
a case's heading and fields stay on the page with its first screenshot, and no
heading ends a page. Its parts come in
this order, each from a fixed source, so two reports for the same milestone
read the same way.

| Part | What it holds | Comes from |
|---|---|---|
| Cover | Title, product, organisation, milestone, run type, document id, version, release date, a confidentiality line | `meta.json`, `--run` |
| Contents | Every section, and each test case in the test execution details, with its page number. The Word copy holds a contents field that Word fills in on opening | The headings below |
| 1. Document control | Document title, id, version, prepared, reviewed, approved and released by, release date; change history; distribution list by name, organisation and role | `meta.json` `document` |
| 2. Executive summary | The counts table, for all cases and for mandatory cases, each term defined in one line under it, then `Ready for sandbox exit.` or `<n> mandatory cases are still open.` | The workbook, `plan`'s applicability, the gate |
| 3. Scope of work | One line naming the application, then every test case this report covers, in sheet order, with its scenario beside it | The workbook, `meta.json` `applications` |
| 4. ABDM details | Entity, product, category, role, entity type, milestone, API version, run type, dates, days, most passes any case took, executed by, recordings | `meta.json`, the workbook |
| 5. Execution summary | The identifiers the run used, such as the ABHA address created and the mobile number that returned several profiles, then Yes or No against each test case and whether recordings exist. The identifiers are the tester's; the Yes or No is counted from the workbook, so it cannot claim more than the run did | `meta.json` `execution_summary`, the workbook |
| 6. Results summary | The counts by function group; every conditional pass with its condition; any mandatory case NHA agreed is not run, with the agreement; then every case not tested, blocked or not applicable, once, with what it needs or why it does not apply | The workbook, `agreed_not_run` |
| 7. Scope and limitations | Applications and URLs; workbook and tabs; the limitation that results hold for the build tested on those dates, and any `limitations` | `meta.json`, the workbook |
| 8. Flow details | For linking and consent flows: the ABHA address used and the HI types created or tested | `meta.json` `flows` |
| 9. Test environment and tools | Browser, Playwright, PHR app, emulator, the model that ran the agent, with versions | `meta.json` `environment` |
| 10. Test team | Name, role and qualifications of each tester, reviewer and approver | `meta.json` `team` |
| 11. Approach and methodology | One line each: who ran the cases, the three pass limit, how OTPs and consents were given, the PHR app proof, the run type, REQUEST-IDs | Fixed text, `--run` |
| 12. Test data | The ABHA numbers and addresses used, masked | `meta.json` `test_data` |
| 13. Test execution details | Tested cases only, passed, conditional pass or failed, under the test case they belong to, in sheet order. Each test case opens with a block naming its scenario, its number and its recording link. Each of its cases then prints as a numbered step: the sheet's number for it and the line it prints, then the result line (status, passes, marking, any condition or error code); the expected result; what was observed; its REQUEST-IDs; its case id; a table of its sub steps if it has them; the response for an `ft` run; then its screenshots | The workbook, `narration`, `recording_links`, `findings`, `--manifest`, the evidence folder |
| 14. Failures and iterations log | Failed, blocked and conditional pass cases only, with the reason, the condition or what is needed, and any error code | The workbook |
| 15. Conclusion and sign-off | The same ready line as the summary, then a sign-off table for preparer, reviewer and approver with date and signature | The gate, `meta.json` `document` |
| 16. Annexures | Recording links, the completed workbook, the REQUEST-ID manifest, the full size screenshots in the evidence folder and the file listing them | `meta.json`, the arguments |
| 17. Contact | Who to ask about the report: name, role, organisation and email | `meta.json` `contact` |

| Count | What it means |
|---|---|
| Cases in NHA's sheet | Every case in the workbook |
| Applicable to this integrator | The cases less those not applicable, as `plan` classifies them for your entity type |
| Tested | Passed, conditional pass and failed |
| Passed, conditional pass, failed | `PASS`, `CONDITIONAL PASS` and `FAIL` in the run type's status column |
| Blocked | A blank status with Remarks starting `Needs tester:`: waiting for a tester, a device or test data |
| Not tested | Applicable and blank but not blocked; `NA` on a mandatory case NHA has not agreed to, which is still open; or a status the script does not know, shown as `Unrecognised status: <value>` |
| Not applicable | In a group the sheet marks for the other entity type only, whether or not it was run (a run one is listed as `Run, not applicable` with its result); or `NA` on an optional case or with NHA's agreement |

The counts must add up: applicable is tested, blocked and not tested; tested is
passed, conditional pass and failed. The script stops with `the summary counts
do not add up` if they do not. The mandatory column counts each case that is
mandatory for your entity type, and each either pair once: by the case that met
it, else by one that was tried. So the mandatory failed, blocked and not tested
add up to the number in `<n> mandatory cases are still open.` When one case of
an either pair passes, the other's result carries `Either rule: <id> or <id>
satisfies it.` and is not a mandatory failure.

Screenshots print to be read, at their own resolution. Two rows fit a page with a
case heading: each row is at most about 98 mm tall. A landscape (desktop)
screenshot spans the text width up to that height, one to a row, never under
140 mm wide. Portrait (phone) ones sit up to three to a row at that height,
never under 45 mm wide. A capture
more than three times as tall as it is wide prints 60 mm wide, cut into parts
that each fit a page. Landscape and portrait ones never share a row. Each is
embedded with every pixel it has, unless that is more than prints at 300 dots
per inch at its size, when it is scaled to that. A PNG stays lossless. A file
Pillow cannot open, an empty one included, is named in the case as `Screenshot
could not be read: <file>`, and the report is still written. The full size
files stay in the evidence folder, and `build` writes `<report>-screenshots.json`
beside the report, listing each figure's case, step and file, for the annexure
bundle.

The only prose you write is the one line finding per case, in `meta.json`.
The observed value is that finding, else the Remarks value. A failure reason is
the Remarks value, else the response. The result line names any `ABDM-` or
`MIS-` code, HTTP 4xx or 5xx status or exception in them. Request
ids come from the run's manifest JSON, the file the test skill writes beside
the run log, given with `--manifest`. A request id that is a UUID is printed as
it is, so a reviewer can look it up in the gateway's record; anything else in
that field is masked like the rest of the report.

What the report never holds: a login, password, client secret, token or OTP,
an unmasked Aadhaar number, ABHA number, ABHA address or mobile, and anyone's
email or phone number. The team and distribution tables take a name, a role and an
organisation only.

How the marking column is read. It is the first column whose name holds both
`Mandatory` and `Optional`, in any case and spacing: `Mandatory/ Optional` on
the HMIS and HPR sheets, `Optional / Mandatory` on the PHR sheet.

| Marking in the sheet | Government entity | Private entity |
|---|---|---|
| `Mandatory`, `Yes` | Mandatory | Mandatory |
| `Mandatory` then a list of mandatory and optional fields, as on PHR's profile cases | Mandatory | Mandatory |
| `No`, `Optional`, `Non-Mandatory` | Optional | Optional |
| `Mandatory for Private` | Optional | Mandatory |
| `Mandatory for Government` | Mandatory | Optional |
| `Mandatory for Private /Government (Optional only if ...)` | Mandatory, flagged conditional | Mandatory, flagged conditional |
| `Either of the test cases X or Y is mandatory for Government` | X or Y must pass; one passing satisfies both | The row carrying it is optional; the other case keeps its own marking |
| `Yes (if ...)`, `Yes, if ...`, `Yes(only if ...)` | Mandatory, flagged conditional | Mandatory, flagged conditional |
| Blank | Mandatory, flagged not marked | Mandatory, flagged not marked |
| Anything else, such as a note in the marking cell | Mandatory, flagged unrecognised | Mandatory, flagged unrecognised |

A group's Applicable To comes first. When the row that heads a group names one entity type only, such as M1's demographic authentication group, `Government`, every case in it is not for the other entity type, whatever its own marking says: for a private entity, CRT_ABHA_301 to 309 are not mandatory.

A mandatory case can have no meaning in a run type, such as a screen only case
in an `ft` run, or be a conditional case your system does not offer. Record it
`not-run` only after NHA agrees it does not apply, and say so in its remarks.
Then add it to `agreed_not_run` in `meta.json`, with who at NHA agreed and
when: `"agreed_not_run": {"<case id>": "<who at NHA agreed, and when>"}`. The gate passes a mandatory `NA` case only when it is listed there, and
the report lists every such case with its agreement in the results summary and
in the failures and iterations log. An unlisted mandatory `NA` still blocks.
The check also prints every case it treated as conditional, so the person
signing off sees them.

### Before you start

- The workbook the run wrote into, with its results, as `shared.concept.test-workbook-write-back` leaves it.
- For a `ui` report, the screenshots under `abdm-ft/evidence/<case id>/`, one folder per case id as `cases` prints it. They are placed in name order, so name them `01-consent.png`, `02-otp.png`. PNG, JPEG, GIF, BMP, WebP and TIFF are read; HEIC and PDF files are read only where Pillow can open them, so export them to PNG. Blur any Aadhaar number, OTP or full mobile on a screenshot before it goes in the folder: the script masks text, not images.
- Python 3.9 or later with `openpyxl`, `Pillow`, `python-docx` and `reportlab`, `reportlab<5` on Python 3.9, which ReportLab 5 does not support. The environment survey, `shared.concept.test-environment`, finds an interpreter that has all four, or installs them into `abdm-ft/.venv` on the tester's yes. Run the script under the interpreter it names. All four are open source (MIT, the HPND licence, MIT and BSD). ReportLab draws the PDF itself, so no office suite is needed.
- Optionally, the design system's fonts as `.ttf` files: Newsreader for headings, Public Sans for text and JetBrains Mono for labels and case ids, all under the SIL Open Font License. The script looks in the folder given with `--fonts`, then `abdm-ft/fonts/`, then `~/Library/Fonts`, `/Library/Fonts` and `/usr/share/fonts`, and prints which it used. A font it does not find is replaced by the PDF's own Times-Roman, Helvetica or Courier, which are the design system's fallbacks, so the report is complete either way.
- The script `scripts/abdm_report.py` from this skill, copied to `abdm-ft/abdm_report.py`.
- A `meta.json` for the report, written by you, with placeholder values replaced. It carries no credential: the script refuses any field named like `password`, `secret`, `token` or `credentials`, and any value that holds a token or a credential's label with its value, such as `client secret: ...`. `agreed_not_run` is only for mandatory cases NHA agreed are not run; leave it `{}` otherwise. `flows` is for M2 and M3 linking and consent flows; leave it `[]` for M1. `team` and `distribution` carry no email or phone number.
- For request ids in the report, the run's manifest JSON, which the test skill writes beside the run log at the end of the run. It is optional: without it each tested case reads `No manifest given`. The script refuses a manifest that holds a credential, as it does `meta.json`.

```json
{
  "organisation": "<YOUR ORGANISATION'S REGISTERED NAME>",
  "product": "<YOUR PRODUCT NAME>",
  "milestone": "M1",
  "api_version": "V3",
  "category": "HMIS",
  "entity_type": "private",
  "role": "HIP",
  "executed_by": "<TESTER NAME>",
  "test_dates": {"start": "2026-10-20", "end": "2026-10-23"},
  "applications": [{"name": "<YOUR PRODUCT NAME> web", "url": "https://<YOUR TEST HOST>/"}],
  "environment": {
    "browser": "Chromium",
    "browser_version": "<VERSION PLAYWRIGHT REPORTS>",
    "playwright": "<PLAYWRIGHT VERSION>",
    "phr_app": "ABHA app",
    "phr_app_version": "<VERSION ON THE TEST DEVICE>",
    "emulator": "<EMULATOR NAME AND ANDROID VERSION>",
    "model": "<MODEL THAT RAN THE AGENT>"
  },
  "test_data": [
    {"item": "ABHA number used", "value": "XX-XXXX-XXXX-1234"},
    {"item": "ABHA address used", "value": "<TEST ABHA ADDRESS>@sbx"}
  ],
  "document": {
    "id": "FT-M1",
    "version": "1.0",
    "release_date": "2026-10-24",
    "prepared_by": "<NAME, ROLE>",
    "reviewed_by": "<NAME, ROLE>",
    "approved_by": "<NAME, ROLE>",
    "released_by": "<NAME, ROLE>",
    "change_history": [{"version": "1.0", "date": "2026-10-24", "by": "<NAME>", "change": "First issue"}],
    "distribution": [{"name": "<NAME>", "organisation": "<ORGANISATION>", "role": "<ROLE>"}]
  },
  "team": [{"name": "<TESTER NAME>", "role": "Tester", "qualifications": "<QUALIFICATIONS, OR LEAVE EMPTY>"}],
  "flows": [{"name": "<FLOW, SUCH AS HIP INITIATED LINKING>", "abha_addresses": ["<TEST ABHA ADDRESS>@sbx"], "hi_types": ["<HI TYPE, SUCH AS PRESCRIPTION>"]}],
  "limitations": [],
  "findings": {"CRT_ABHA_101": "The Create ABHA option opens the Aadhaar consent screen."},
  "agreed_not_run": {},
  "recordings": [{"title": "M1 ui run, full trace", "link": "<LINK TO THE RECORDING>"}],
  "execution_summary": [
    {"item": "ABHA address created with the mobile number flow", "value": "<TEST ABHA ADDRESS>@sbx"},
    {"item": "Mobile number that returned several profiles", "value": "<TEST MOBILE NUMBER>"}
  ],
  "recording_links": {"<NAME OF TEST CASE, AS THE SHEET HEADS IT>": "<LINK TO THAT SCENARIO'S RECORDING>"},
  "narration": {"CRT_ABHA_101": "<WHAT THE TESTER DID AND WHAT THE SCREEN SHOWED, IN THE PAST TENSE>"},
  "contact": [{"name": "<NAME>", "role": "<ROLE>", "organisation": "<ORGANISATION>", "email": "<EMAIL>"}]
}
```

The script ships with this skill as `scripts/abdm_report.py`. Copy `abdm_report.py` from there; never retype it or rebuild it from this text:

```bash
mkdir -p abdm-ft && cp <this skill folder>/scripts/abdm_report.py abdm-ft/abdm_report.py
```

### What happens

**Before the first case, plan the run from the workbook:**

```bash
python3 -I abdm-ft/abdm_report.py plan <workbook> --run ui --entity private
```

It prints one line per case, in sheet order: the case id, its group (the sheet's Function, else its section heading), what it is for your entity type (`mandatory`, `mandatory, either <id> or <id>`, `conditional`, `optional`, or `not applicable` with the group and its Applicable To; an Applicable To that names neither entity type, such as `PHR App`, adds `applies: PHR App (read as all)`), and its status so far in the run type's column, `open` when blank. Then one `total` line. NHA's PHR workbook carries no case ids until the writer's first write adds them, so for P1 to P4 write an empty results file, `[]`, with `abdm_workbook.py write` first, then run `plan`. Create the run log's rows from this list, one per case. No case may be dropped: a case that cannot run is `needs-human` with what it needs named, after the tester was asked for it, or `not-run` with the rule that makes it not apply. Before the tester leaves, run `python3 -I abdm-ft/abdm_workbook.py open <workbook> --run ui --entity private` and read the list out, so every case still waiting for them is done or asked about while they are there.

1. When the last case is recorded and written into the workbook, run the gate for the run type and your entity type:

   ```bash
   python3 -I abdm-ft/abdm_report.py check <workbook> --run ui --entity private --meta abdm-ft/meta.json
   ```

   `--meta` is optional for `check`: it is read for `agreed_not_run`. The script prints one `marking` line for every case whose marking is entity specific, conditional, an either rule, a field list, blank or unrecognised, with how it was treated, and one `agreed not run <id>: <agreement>` line per mandatory `NA` case `agreed_not_run` lets through. Then one `not passed <id>: <status>` line per mandatory case in the way, an either pair as `<id> or <id>: <status>, <status>`, and a count.

2. If it exits `1`, the run is not finished, but the tester still gets a PDF of where it stands. Build the draft:

   ```bash
   python3 -I abdm-ft/abdm_report.py build <workbook> --run ui --meta abdm-ft/meta.json --evidence abdm-ft/evidence --manifest abdm-ft/m1-<YYYYMMDD-HHMM>.json --out abdm-ft/draft-report-m1.pdf --draft
   ```

   The draft is the same report, marked Draft on the cover and in the header of every page. Its executive summary ends on `<n> mandatory cases are still open.`, and each of them is in the failures log or the table of cases not tested. It is never the report to submit. Then rerun the cases `check` listed, as `open` in `shared.concept.test-workbook-write-back` does, and run `check` again. Every run ends with a PDF: the report when `check` exits `0`, the draft when it does not.

3. Only when `check` exits `0`, write the one line findings into `meta.json` under `findings`, keyed by case id. A case without one gets its Remarks value. Then build:

   ```bash
   python3 -I abdm-ft/abdm_report.py build <workbook> --run ui --meta abdm-ft/meta.json --evidence abdm-ft/evidence --manifest abdm-ft/m1-<YYYYMMDD-HHMM>.json --out abdm-ft/report-m1.pdf
   ```

   `entity_type` in `meta.json` is the entity type, unless `--entity` is given. `build` runs the gate again and refuses with the same list if it fails. `--manifest` is optional. It writes `abdm-ft/report-m1.pdf`, the report to submit, and `abdm-ft/report-m1.docx` beside it, the editable copy, whichever of the two suffixes `--out` names. `--format pdf` writes the PDF alone and `--format docx` the Word copy alone; the default is `both`. `--fonts <folder>` points it at the design system's fonts if they are somewhere else.

4. Submit the PDF as it is: its contents list and page numbers are already filled in. If you edit the Word copy instead, open it in Word or LibreOffice and accept the prompt to update fields, which fills in its contents and page count, then export it to PDF. A screenshot that needs a highlight box gets it before it goes in the evidence folder.

5. For an `ft` report, use `--run ft` in both commands. The evidence for each case is its `FT Response with Code` value, and `--evidence` is not read.

6. A skill built per functionality, M1 today, ships `scripts/functionalities.json` beside this script: each functionality's cases, and which of them a screen requirement in its file cites. Copy it into `abdm-ft/` with the script. Then `build` also writes `<report>-findings.md` beside the report, the draft included. To write the findings without a report, after a functionality's test:

   ```bash
   python3 -I abdm-ft/abdm_report.py findings <workbook> --run ui --entity private --out abdm-ft/draft-report-m1.pdf
   ```

   It lists each FAIL and CONDITIONAL PASS with its functionality and the screen requirement that states the check for it. A row that only hands the case to another functionality, such as "follow create-abha-aadhaar-otp", does not count by itself: the finding follows it to that functionality's rule for the case of the same title and names it. A case with no such rule gets a Catalogue issue, written out ready to paste and never filed by the script.

7. The same file feeds the tracker in `INTEGRATION-PLAN.md`. After every change of state, and after every write into the workbook:

   ```bash
   python3 -I abdm-ft/abdm_report.py tracker <workbook> --run ui --entity private --set create-abha-aadhaar-otp=tested --build <build id>
   ```

   `--set <id>=building`, `built` or `tested` records where one functionality stands, `--set <id>=in-scope` adds an optional one, and `--screen <case id>=<screen or route>` records what satisfies a case. Each can repeat. The tracker writes its rows between the two `abdm-tracker` markers in `INTEGRATION-PLAN.md`, creating the file or the section when missing, and keeps its state in `abdm-ft/tracker.json`. `--entity` is needed once; later calls read it from the state. A case counts as passed exactly as `check` counts it: a CONDITIONAL PASS needs its condition in Remarks, and an `NA` closes a mandatory case only when `agreed_not_run` in the `--meta` file names it. A held `building` or `built` state gives way once a later write into the workbook makes the functionality passed or failing. It prints one line per functionality, its id and state with its failing case ids, then the counts.

### How you know it worked

- `plan` prints one line per case in the workbook, 100 for M1, then a `total` line, and the run log has a row for every one of them.
- `check` exits `0` and reads `0 of <n> mandatory case(s) for a <entity> entity not PASS in the <run> run`, followed by one `conditional pass <id>: <condition>` line per conditional pass.
- The executive summary's counts table has nine rows, from `Cases in NHA's sheet` to `Not applicable`, with an `All cases` and a `Mandatory` column. In each column, applicable equals tested plus blocked plus not tested, and tested equals passed plus conditional pass plus failed. Under it, one line defines each term, then `Ready for sandbox exit.` or `<n> mandatory cases are still open.`, the same number `check` lists. The words `No result` appear nowhere.
- The results summary counts each function group in sheet order, lists every conditional pass with its condition, and lists every case not tested, blocked or not applicable once, with what it needs or why it does not apply. A tested case whose Remarks end in `Steps: ...` shows a Step, Result and Note table in its evidence.
- `build` prints `wrote abdm-ft/report-m1-screenshots.json` for a `ui` run, a `fonts:` line naming each font it used or replaced, then `wrote abdm-ft/report-m1.docx` and `wrote abdm-ft/report-m1.pdf`, and exits `0`.
- The PDF opens on a navy cover with the milestone in orange, the key facts in a ruled grid and the confidentiality note. Page 2 is the contents, with every section, each function group in the evidence, and its page. Every later page reads the report title at the top left and `Confidential` and `Page N of M` at the bottom, and every page outside the evidence holds at least four lines of text besides them. No section opener, group heading or case heading ends a page.
- Both files open with the cover and the contents, then the seventeen headings from Document control to Contact, in order. Every page of the Word copy has the report title at the top and `Confidential, page N of M` at the bottom.
- The evidence holds every tested case and no other, under its function group. Each starts with its id and title, then its result line, expected result, observed result and REQUEST-IDs, then its screenshots, two rows to a page: a landscape one 140 mm wide or more, portrait ones up to three to a row, each captioned with its figure number and step: `Fig <n>, step <k>` in the PDF, `Figure <n>, step <k>` in the Word copy. `<report>-screenshots.json` names each figure's full size file.
- The failures and iterations log lists only failed, blocked and conditional pass cases.
- With `--manifest`, each tested case's Request IDs line lists the REQUEST-IDs the manifest gives it, or reads `None in the manifest`. A value that is not a UUID reads `[invalid id]`.
- No 12 digit number appears anywhere in either file, the PDF's header and footer included: an Aadhaar number reads `XXXX XXXX 1234` and an ABHA number `XX-XXXX-XXXX-1234`, with the last four digits kept. A 10 digit mobile, with or without `0`, `91` or `+91` and spaces or hyphens inside, reads `XXXXXX1234`, and an ABHA address ending `@sbx` or `@abdm` keeps only the last four characters before the `@`, the rest as `x`, or is all `x` when four or fewer. A profile field's value, `firstName`, `lastName`, `name`, `dob`, `address`, `pinCode`, `photo`, `email` and the like, reads `[masked]`, in any case and to the end of its clause, so `Name: asha verma` reads `Name: [masked]`. Dates of birth and dd/mm/yyyy dates are masked as the writer masks them, because the writer uses the same masking block, so the sheet and the report agree.
- With `functionalities.json` beside the script, `build` and `findings` print `findings: <n> FAIL or CONDITIONAL PASS, <m> without a screen requirement`, a `catalogue issue <id> in <functionality>` line for each of the `m`, and `wrote <report>-findings.md`. `tracker` prints `wrote INTEGRATION-PLAN.md and abdm-ft/tracker.json`. With no optional functionality added, its failing and open mandatory cases are the ones `check` lists.
- No token or secret appears in it: a JWT reads `[token]`, and a value after a label naming a password, secret, token, OTP, credential, API key or private key reads `[redacted]`, as does a `Bearer` credential. This covers the workbook's Response and Remarks values and the findings, as well as `meta.json`.

### When it goes wrong

| Observed | Cause | Do |
|---|---|---|
| `ModuleNotFoundError: No module named 'docx'` or `'reportlab'` | The script ran under an interpreter without them | Run it under `"$(python3 -I abdm-ft/abdm_env.py python)" -I`. If the survey found none with them, install into `abdm-ft/.venv` on the tester's yes |
| `ImportError: reportlab requires 3.10+` | A ReportLab release newer than your Python was installed, on Python 3.9 | Survey again with `--refresh`: it skips that interpreter, and its install puts `reportlab<5` into `abdm-ft/.venv` on Python 3.9 |
| `fonts: Public Sans not found, Helvetica in its place` | That font is not in any folder the script looks in | Nothing, the report is complete with the fallback. For the design system's own face, put its `.ttf` files in `abdm-ft/fonts/` and build again |
| `not passed CRT_ABHA_105: FAIL` | A mandatory case failed | Fix the cause the Remarks name, rerun the case, write it back, run `check` again |
| `not passed <id>: NA` | A mandatory case was recorded not run, and `agreed_not_run` does not list it | Run it, or agree with NHA that it does not apply, keep the reason in its remarks, and add it to `agreed_not_run` with who agreed and when. Pass `--meta` to `check` |
| `agreed_not_run must map each case id` | An entry has no agreement text | Write who at NHA agreed and when for every case listed |
| `not passed <id>: blank` | The case needs a tester step, or never ran | Finish it with the tester, then write it back |
| `not passed <id>: CONDITIONAL PASS with no condition in Remarks` | The Remarks cell for a conditional pass is empty, which the writer refuses, so it was typed into the sheet by hand. The gate blocks it | Write the result again through `abdm_workbook.py write` with the condition in `remarks` |
| A case is missing from the run log | The run log was written by hand, not from `plan` | Run `plan` again and add a row for every case it lists. Never drop a case to finish sooner |
| `plan` prints `not applicable` for a case you expected to run | The sheet's group Applicable To names the other entity type only | Check `--entity`. A group for the other entity type is `not-run` with that rule in Remarks |
| `marking <id> "...": unrecognised marking: treated as mandatory`, or `no marking` | NHA used a marking the script does not know, such as PHR's note on row P1 3.6, or left it blank | Ask NHA what it means for your entity type. Never edit NHA's marking to pass the gate |
| `meta.json carries a credential` | `meta.json` has a password, secret, token or credentials field, or a value holding a JWT or a credential's label with its value | Delete the field or the value. A report never carries a credential |
| `the workbook has a (run) column but no .before-abdm-run copy` | The writer's copy of NHA's workbook was moved or deleted | Put it back beside the workbook. Without it the script cannot tell NHA's text from the run's |
| `case id on more than one tab` | Two tabs carry the same id | Ask NHA which one counts |
| `no Test Case ID column with ids` | The workbook has no case ids, as NHA's PHR workbook does not, and no write has added them yet | Write the run's results with `shared.concept.test-workbook-write-back` first: its first write adds the `Test Case ID` column |
| `the manifest carries a credential` | The manifest JSON holds a token, secret or password field or value | Write the manifest as the test skill does, case ids to request ids and nothing else |
| The Word copy's contents page reads `Open in Word and update fields` | Its fields have not been updated since the script wrote them. The PDF is not affected | Open the file in Word or LibreOffice and update fields, then save |
| `no functionalities.json beside this script` | The module's skill is not built per functionality yet, or the file was not copied into `abdm-ft/` | Copy `scripts/functionalities.json` from the skill. A module without one has no tracker and no findings; use its plan file steps |
| `the tracker holds no entity type` | The first tracker call for this workbook, with no `--entity` | Ask the tester, government or private, and pass `--entity` |
| A screenshot is missing from the report | Its file is not under `<evidence>/<case id>/`, or its extension is not png, jpg, jpeg, gif, bmp, webp, tif, tiff, heic, heif or pdf. Or the case was not tested: only passed, conditional pass and failed cases have evidence | Move it into the case's folder, named as `cases` prints the id, and build again. For a case not tested, finish it first |
| A case you ran is under Cases not tested as `Blocked` | Its status is blank and its Remarks start `Needs tester:`, as the writer leaves a `needs-human` case | Finish the step with the tester, write the result again, and build again |
| A case is counted `Not applicable` though you ran it | Its status is `NA`, as the writer writes a `not-run` case; or its group is for the other entity type only, listed as `Run, not applicable` | Write the result again with the outcome it had. For the group, check `--entity` |
| A mandatory `NA` is counted `Not tested` | NHA has not agreed it does not apply | Run it, or add the agreement to `agreed_not_run` |
| `Unrecognised status: Passed` in Cases not tested | The status was typed by hand, not written by the writer | Write the result again through `abdm_workbook.py write` |
| `Screenshot could not be read: <file>` | The file is empty, damaged, or in a format Pillow cannot open, such as HEIC on most systems | Export it to PNG in the same folder, delete the unreadable one, and build again |
| A person's name shows in the report | It was written with no label, such as `asha verma saw the screen`. Masking finds a name only after a label such as `Name:`, which it masks to the end of the clause | Write the result again without the name. The agent never writes a name; masking is the backstop, not the rule |
| `the summary counts do not add up` | A status the script does not sort into the counts, which should not happen | Report it with the workbook; never edit the counts by hand |

From `shared.concept.test-report`.

## When the last case is recorded

Mark the run log finished, with the time and the count of each outcome. Then write the manifest beside it, with the same name and `.json`. It maps case names to request ids, nothing else. Those REQUEST-IDs are the evidence a reviewer can check against the gateway's own record. Secrets never appear in it: no tokens, no OTPs, no mobile or ABHA numbers, no client secret. The run log and the manifest are written on every run. The manifest is not the report. Every run ends with a PDF, written as the section "Writing the test report" sets out: run its `check`; when it exits 0, build the report; when it does not, build the draft with `--draft`. Never end a run without one, and say where it is in the three lines below.

```json
{
  "skill": "abdm-p2",
  "set": "PHR mobile app test cases, NHA, PHR App Functionality tab",
  "run_started": "2026-10-01T10:12:40Z",
  "run_finished": "2026-10-01T11:48:03Z",
  "client_id": "<your sandbox client id>",
  "cases": [
    {"id": "PHR_APP_105", "outcome": "passed", "evidence": "sandbox",
     "request_ids": ["6f1c..."],
     "observed": ["discover 202", "on-discover patient[].careContexts 2 entries, none already linked"]},
    {"id": "PHR_APP_110", "outcome": "passed", "evidence": "screen",
     "attachment": "record-details-2026-10-01.png"},
    {"id": "PHR_APP_1001", "outcome": "needs-human", "evidence": "human",
     "request_ids": ["b7a1..."], "reason": "approval is the person's tap; nobody present"},
    {"id": "PHR_APP_108", "outcome": "not-run", "reason": "health information request is M3; no P specification publishes it"}
  ]
}
```

Then say, in three lines: how many cases passed, passed with a condition, failed, need a human and were not run; which mandatory cases are not `passed`; and the one case to fix first.
