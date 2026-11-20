---
description: Survey this machine for what a functional test run needs, for the run type you pick, and install what is missing only on your yes.
argument-hint: '[ft|ui|all] [module, such as m1]'
---

Survey this machine for a functional test run of type `$ARGUMENTS`. `/abdm-build` and `/abdm-test` run the same survey as their first step.

## 1. The run type

With no run type in the arguments, ask one question and wait: `ft`, `ui` or `all`?

| Run type | What it runs | What it needs here |
|---|---|---|
| `ft` | Your system's calls against the sandbox, judged on calls and callbacks | Python with the four libraries |
| `ui` | Your application's screens, driven by an agent | Python, Node, the Playwright tools and a browser, and for a PHR app on Android, adb, a device, the app and Maestro |
| `all` | `ft` then `ui` | Everything `ui` needs |

For `ui` and `all`, ask one more question: is the application under test a PHR app on Android? On a yes, ask its package name.

## 2. The survey

The survey is `scripts/abdm_env.py` in each module's test skill. Load the module skill named in the arguments, else `abdm-m1`, and call its folder `<skill>`.

```bash
mkdir -p abdm-ft
cp <skill>/scripts/abdm_env.py abdm-ft/
python3 -I abdm-ft/abdm_env.py survey --run <ft, ui or all> [--phr-package <package>]
```

It runs in isolated mode, `-I`, and asks each interpreter the same way from an empty folder, so no file in the project runs. On `refused: ... is a symlink`, say so and stop. Paste its output. It lists every Python it found with the libraries each lacks, the one it chose and why, the browser and its `--browser` value, and for a PHR app adb, the device, the app and Maestro. It writes `abdm-ft/env.json`, and a later survey reuses that file while the interpreter it names still imports openpyxl, Pillow, python-docx and ReportLab.

With no `python3` on PATH at all, print "Install Python 3.9 or later from https://www.python.org/downloads" and stop.

## 3. The Playwright tools, for `ui` and `all`

The script cannot see your tools. Read your own tool list for `browser_navigate`, `browser_snapshot`, `browser_click`, `browser_type`, `browser_take_screenshot`, `browser_console_messages`, `browser_network_requests` and `browser_network_request`, under whatever prefix your agent gives the `playwright` server.

| Observed | Do |
|---|---|
| All listed | `browser_navigate` to `about:blank`. A page, not an error naming a missing browser, is `ready` |
| Not listed | This plugin declares the server in its `.mcp.json`: restart Claude Code and check again. Without the plugin: `claude mcp add playwright npx @playwright/mcp@0.0.83` |
| The survey's browser is not Chrome | Start the server with the `--browser` value the survey printed, or set `PLAYWRIGHT_MCP_BROWSER` to it, then restart |

## 4. What is missing

When the survey prints `missing:`, ask the tester one question: may I run the install it printed? It creates `abdm-ft/.venv` and installs the libraries there, and nowhere else.

- On yes: `python3 -I abdm-ft/abdm_env.py install --yes`, and the browser command when the survey printed one. Paste the output; it surveys again at the end.
- On no, or no answer: print the exact commands the survey printed for the tester to run, and stop.

Never install into the system's Python, a Homebrew or conda base, or the project's own environment, and never run an install without that yes. adb, a device, the app and Maestro are never installed for the tester: print what the survey says is missing, with Android Studio from https://developer.android.com/studio, `adb install <path to the APK>` and `curl -fsSL "https://get.maestro.mobile.dev" | bash`.

## Output

One line per need: what was checked, the output observed, and `ready`, `missing` or `not needed`. A need with no pasted output is `missing`. After 3 surveys with the same line still `missing`, stop and ask one question about it.

When every line is `ready` or `not needed`, say so, and suggest the next step: `/abdm-build m1` to build, or `/abdm-test m1` for the final run.
