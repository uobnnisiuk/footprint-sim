# Unattended Local AutoDev: one Issue/day

**Owner approval:** On 2026-10-27 the owner approved daily unattended
ChatGPT-plan Local AutoDev execution, with up to one Issue per Japan-local day,
accepting possible consumption of existing purchased ChatGPT credits.
This approval does **not** authorize API-key billing or automatic PR merge.

## Safeguards

`tools/local_autodev_daily.py` is separate from the existing read-only
daily 09:00 monitor, and is **not a background service** on its own.
The Windows task installer explicitly registers a second task at **09:05
Asia/Tokyo** only while the usual Windows account is signed in.

Each run:

1. Requires a clean dedicated checkout on `main`; never touches the research
   worktrees. Does not update or reset the checkout automatically.
2. Checks **all open repository PRs**. If any PR is open (including Draft), it
   stops without spending Codex usage.
3. Queries owner-authored, open `local-autodev` Issues excluding the billed
   `autodev` label; selects only the lowest number.
4. Acquires a nonblocking Linux `flock` and creates exclusive,
   permission-restricted daily and per-Issue attempt markers **before** invoking
   Codex. These markers permanently block the same Issue from being retried
   even on subsequent days, and block more than one attempt per JST day.
5. Delegates **one** call to `tools/local_autodev.py`, which checks
   ChatGPT authentication and the GitHub owner, runs Codex in a restricted
   read-only sandbox, applies the patch guard, and opens a Draft PR.
6. Stops on failures, power loss, timeouts, pending PRs, empty queue, or usage
   limits. No automatic retries, no daemon, no PR merges.

Logs and attempt markers are private WSL files at:

```text
~/.local/state/footprint-local-autodev/daily/
  day-YYYY-MM-DD.json
  issue-<number>.json
  result-YYYY-MM-DD.json
  log-YYYY-MM-DD.txt
```

The directory is restricted to mode 0700; records/logs use 0600.
Model command output stays in local log files (never uploaded to GitHub).
A crash or interruption deliberately consumes the attempt without re-running.
Do not delete markers to reset a failed Issue without manually investigating
the cause and getting a new approval for any retry.

**No hard usage or financial cap exists.** ChatGPT plan usage is consumed;
pre-purchased credits can be consumed when included allowance runs out.
Turning auto-recharge OFF prevents automatic *purchase* but does not guarantee
zero spending of pre-purchased credit balance. If the model hits a limit,
the attempt remains recorded as used; tomorrow does not auto-retry it.
Check remaining account credits/allowance and manually disable the task
whenever needed. There is no automatic credit-state check.

## Install on Windows 11 (after this PR is reviewed, CI passes and it is merged)

First, safely update the **dedicated** Ubuntu checkout, provided it is clean,
not a research checkout:

```bash
cd /home/higuchi/personal/me/footprint-sim/footprint-sim-local-autodev-pr6
test -z "$(git status --porcelain)" || { echo "Dirty checkout; stop"; exit 1; }
git fetch origin main
git merge --ff-only origin/main
python3 -m unittest discover -s .github/scripts -p 'test_local_autodev_daily.py' -v
python3 tools/local_autodev_daily.py
```

Then, in **Windows PowerShell**, as the normal logged-in user:

```powershell
& "\\wsl$\Ubuntu\home\higuchi\personal\me\footprint-sim\footprint-sim-local-autodev-pr6\tools\install_local_autodev_daily.ps1"
```

If local PowerShell execution policy blocks scripts, run the file from a
one-time process with `powershell.exe -NoProfile -ExecutionPolicy Bypass -File`
instead. The installer checks Tokyo timezone, verifies read-only WSL preflight,
then creates/updates task `Footprint Local AutoDev Daily` with:

- WSL distro `Ubuntu` and WSL user `higuchi`;
- daily Windows-local 09:05 schedule;
- interactive Windows user principal at limited privilege;
- ignore concurrent triggers;
- 40-minute execution limit, no retry and no waking the PC;
- separate existing 09:00 monitor remains dry-run only.

**The installer does not start the scheduled task**, and the read-only
preflight does not invoke the model. Registration activates the next scheduled
09:05 execution if the computer is awake and the account is logged on.

To stop unattended AI runs immediately from Windows PowerShell:

```powershell
Disable-ScheduledTask -TaskName "Footprint Local AutoDev Daily"
```

Do not use `Start-ScheduledTask` merely to test connectivity: that triggers a
real, accounted AI attempt when the queue is eligible.

## Limits and next steps

A PR remains review-gated: even CI success does not merge code.
The next Issue (#12, then #13) cannot run until the owner reviews and merges
the earlier PR. That constraint is deliberate and can slow progress, but keeps
candidate code from stacking uncontrolled changes. The run counter uses the
Windows/WSL local machine's clock translated into Asia/Tokyo; clock tampering
or manually deleting ledger files is outside the threat model.
