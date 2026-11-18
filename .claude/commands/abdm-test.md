---
description: Run every mandatory NHA test case of a module on one final build, and write the workbook, the report or the draft, and its findings.
argument-hint: '<module, such as m1> [ft|ui|all]'
---

Run the final test of the module in `$ARGUMENTS`: every mandatory case for the entity type, on one build, with the tester present. With no module, ask which and wait.

Results gathered while building came from different builds on different days. This run starts from a fresh copy of NHA's workbook, so the report carries only this run's results. Every step ends with its output pasted. A step with no pasted output is not done. Every script runs in isolated mode, `-I`.

## 1. Load the module and the run type

Load the `abdm-<module>` skill and read its `SKILL.md`. Call its folder `<skill>`.

The run type is the second argument, else ask one question and wait: `ft`, `ui` or `all`? Call it `<run>`. Every script call below takes `--run <run>`. For `all`, run each workbook and report call twice, `--run ft` first, then `--run ui`; the survey takes `--run all`.

| What `<skill>` holds | Do |
|---|---|
| `references/run.md` and `scripts/functionalities.json` | Read `run.md` in full now: it is the run. Go on to step 2 |
| `references/test.md` | It is not built per functionality yet. Say so in one line, then run steps 2 and 3 below and follow `references/test.md` from its readiness check to its report. There is no tracker and no findings file for it. Stop here |

## 2. A fresh workbook, before anything else

```bash
mkdir -p abdm-ft/final
cp <skill>/scripts/abdm_*.py abdm-ft/
cp <skill>/scripts/functionalities.json abdm-ft/ 2>/dev/null
cp <skill>/workbook/*.xlsx abdm-ft/final/
python3 -I abdm-ft/abdm_env.py workbook --skill <skill> --in abdm-ft/final
```

The run is refused until it prints `workbook:`. Never use a workbook from anywhere else, and never the one the build loops wrote into.

## 3. Survey the machine

```bash
python3 -I abdm-ft/abdm_env.py survey --run <run, or all>
```

Add `--phr-package <package>` for a PHR app on Android. For `ui` and `all`, read your tool list for the Playwright tools. On `missing:`, ask once whether to run the install it printed into `abdm-ft/.venv`; on yes run `python3 -I abdm-ft/abdm_env.py install --yes`, on no print the exact commands and stop. On `refused`, say so and stop. Then:

```bash
PY="$(python3 -I abdm-ft/abdm_env.py python)"
"$PY" -I abdm-ft/abdm_workbook.py cases abdm-ft/final/<workbook>
```

## 4. Readiness, all of it up front

Follow the readiness check in `run.md`, before any case runs:

1. For M1, the first line, before any other input: "The full ABHA creation journey needs an Aadhaar with no ABHA; delete an existing one on https://abhasbx.abdm.gov.in/abha/v3 to free it." Record whether the tester has one, or keeps their existing ABHA.
2. The entity type: read `entity` from `abdm-ft/tracker.json`, else ask, government or private. Call it `<entity>`.
3. The build: ask which build this run tests, and write it in the run log's header. Every case runs on it.
4. Every input the mandatory cases need, from their functionality files and "What each need means" in `run.md`: ask for each one now, one at a time, and record `ready` or `missing`, never the value.

## 5. The case plan

```bash
"$PY" -I abdm-ft/abdm_report.py plan abdm-ft/final/<workbook> --run <run> --entity <entity>
```

Make the run log's rows from every line it prints. No case may be dropped.

## 6. Every mandatory case

Run them functionality by functionality, in the order of the table in `SKILL.md`, each to its outcome as `run.md` sets out, and write each into `abdm-ft/final/<workbook>` with `--run <run>`. Ask again for a `missing` input when its case is reached. Run an optional case only when the integrator claims it.

Before the tester leaves:

```bash
"$PY" -I abdm-ft/abdm_workbook.py open abdm-ft/final/<workbook> --run <run> --entity <entity>
```

Read every line out, and finish or ask about each while the tester is there.

## 7. The report, or the draft

```bash
"$PY" -I abdm-ft/abdm_report.py check abdm-ft/final/<workbook> --run <run> --entity <entity> --meta abdm-ft/meta.json
```

- Exit 0: build `abdm-ft/final/report-<module>.pdf` without `--draft`.
- Exit 1: build `abdm-ft/final/draft-report-<module>.pdf` with `--draft`, and say how many mandatory cases are still open.

The `build` command is in `run.md`, "Writing the test report"; run it as `"$PY" -I abdm-ft/abdm_report.py build ... --run <run> --entity <entity>`. It writes `<report>-findings.md` beside the PDF. Show the person each Catalogue issue in it; never file one yourself. Then bring the tracker up to date from this run:

```bash
"$PY" -I abdm-ft/abdm_report.py tracker abdm-ft/final/<workbook> --run <run> --entity <entity> --meta abdm-ft/meta.json --state abdm-ft/tracker.json
```

Say in three lines: the report or the draft and its path, the counts from its summary, and the findings file's path.
