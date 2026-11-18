---
description: Start or resume building a module one functionality at a time, each loop ending in that functionality's own NHA test cases.
argument-hint: '<module, such as m1>'
---

Build `$ARGUMENTS` one functionality at a time, from the tracker in `INTEGRATION-PLAN.md`. With no argument, ask which module and wait.

Every step below ends with its output pasted. A step with no pasted output is not done. Every script runs in isolated mode, `-I`, so nothing in the project is imported by it.

## 1. Load the module

Load the `abdm-$ARGUMENTS` skill and read its `SKILL.md`. Call its folder `<skill>`.

| What `<skill>` holds | Do |
|---|---|
| `references/functionalities/` and `scripts/functionalities.json` | It is built per functionality. Go on to step 2 |
| Anything else | Say in one line: "`abdm-$ARGUMENTS` is not built per functionality yet, so there is no tracker to resume from." Then follow its `SKILL.md`: its plan file rule, and its scaffold, integrate, debug and test sections under `references/`. Stop here |

Read these before step 2, and keep them for the session: in `<skill>/references/run.md`, "Read this first: this run is never hands-off", "The functionality loop", "The tracker in INTEGRATION-PLAN.md" and "Findings after every loop and every run". Read `<skill>/references/foundations.md` at the first Design phase.

## 2. NHA's workbook, before anything else

```bash
mkdir -p abdm-ft
cp <skill>/scripts/abdm_env.py <skill>/scripts/abdm_workbook.py <skill>/scripts/abdm_report.py <skill>/scripts/functionalities.json abdm-ft/
python3 -I abdm-ft/abdm_env.py workbook --skill <skill>
```

On `refused`, exit 4: copy the workbook from `<skill>/workbook/` into `abdm-ft/` under its own name, unless `abdm-ft/` already holds a filled copy from an earlier loop, and run the check again. Never use a workbook from anywhere else. The build does not start until it prints `workbook:`.

## 3. Survey the machine

```bash
python3 -I abdm-ft/abdm_env.py survey --run ui
```

Then read your own tool list for `browser_navigate`, `browser_snapshot`, `browser_click`, `browser_type` and `browser_take_screenshot`, under any prefix.

| Survey says | Do |
|---|---|
| `ready` or `reusing`, and the tools are listed | Go on |
| `missing:` with an install line | Ask one question: may I run the install it printed, into `abdm-ft/.venv`? On yes, run `python3 -I abdm-ft/abdm_env.py install --yes`, and the browser command when one was printed. On no, print the exact commands it printed and stop |
| `refused: ... is a symlink` | Say so and stop |
| The tools are not listed | Say so. The plugin starts the Playwright server: restart Claude Code and run `/abdm-build $ARGUMENTS` again. Stop |
| `missing: Python 3.9 or later` | Print the install line it gave and stop |

From here on, every script runs under the interpreter the survey chose, in isolated mode:

```bash
PY="$(python3 -I abdm-ft/abdm_env.py python)"
"$PY" -I abdm-ft/abdm_workbook.py cases abdm-ft/<workbook>
```

The second line must print the case ids. If it does not, stop and say what it printed.

## 4. Read the tracker

```bash
"$PY" -I abdm-ft/abdm_report.py tracker abdm-ft/<workbook> --run ui
```

- `the tracker holds no entity type`: ask one question, "Is your organisation a government or a private entity?", wait, and run it again with `--entity <answer>`. Never assume one.
- Otherwise say in one line where the work stands, from its last two lines.

## 5. Pick the next functionality

The tracker prints one line per functionality in build order, `<id>: <state>`.

| First match, in build order | Do |
|---|---|
| `failing` | Run its loop, entering at Design or Integrate for the case that failed |
| `building` | Resume its loop where the tracker's Screen or route column and the codebase show it stopped |
| `not started` | Run its loop from Design. An integration that already exists enters at Test |
| Nothing above, and some are `built and waiting for the tester` | Ask for the tester's session, then step 7 for all of them together |
| Every in scope line is `passed` | Say so, and that the next step is `/abdm-test $ARGUMENTS` on one final build. Stop |

`optional, not in scope` and `not applicable` are skipped. Build an optional one only when the person asks: `--set <id>=in-scope` adds it.

## 6. Run its loop

Open `<skill>/references/functionalities/<id>.md`, and record that the loop started:

```bash
"$PY" -I abdm-ft/abdm_report.py tracker abdm-ft/<workbook> --run ui --set <id>=building
```

Run Design, Scaffold, Integrate and Debug as its table sets out. At the Design exit, record what satisfies each mandatory case, one `--screen <case id>=<screen or route>` per case. When it is built, ask whether the tester is on call. On call: step 7 now. Otherwise record `--set <id>=built` and go back to step 5. Batching the tester's cases is the default.

## 7. Test and record

With the tester present, readiness comes first. For M1, its first line, before any other input: "The full ABHA creation journey needs an Aadhaar with no ABHA; delete an existing one on https://abhasbx.abdm.gov.in/abha/v3 to free it." Then follow `run.md` for the functionalities waiting: every input their cases need asked for, then each case to its outcome and written into the workbook. Then, for each functionality tested:

```bash
"$PY" -I abdm-ft/abdm_report.py tracker abdm-ft/<workbook> --run ui --set <id>=tested --build <the build the tester used>
"$PY" -I abdm-ft/abdm_report.py build abdm-ft/<workbook> --run ui --meta abdm-ft/meta.json --evidence abdm-ft/evidence --out abdm-ft/draft-report-$ARGUMENTS.pdf --draft
```

Write `abdm-ft/meta.json` first if it is not there, as "Writing the test report" in `run.md` sets out. The draft writes `abdm-ft/draft-report-$ARGUMENTS-findings.md` beside it. Show the person each Catalogue issue in it; never file one yourself. Before the tester leaves, run `"$PY" -I abdm-ft/abdm_workbook.py open abdm-ft/<workbook> --run ui --entity <entity>` and read the list out.

A failed case sends that functionality back to step 6. Loop limit: 3 passes through Test per functionality. On the fourth, escalate as `run.md` sets out, with one question.

Then go back to step 5.
