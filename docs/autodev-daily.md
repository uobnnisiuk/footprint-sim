# Unattended Local AutoDev: up to four JST slots/day

**Owner approval:** On 2026-10-27 the owner approved unattended
ChatGPT-plan Local AutoDev execution and subsequently requested increased
frequency. The enabled schedule offers **09:05, 13:05, 17:05, and 21:05 JST**,
with **at most one Issue attempt per slot (four per JST day)**. It may consume
existing purchased ChatGPT credits.
This approval does **not** authorize API-key billing or automatic PR merge.

## Safeguards

`tools/local_autodev_daily.py` is separate from the existing read-only
daily 09:00 monitor, and is **not a background service** on its own.
The Windows task installer registers a second task at **09:05, 13:05, 17:05,
21:05 Asia/Tokyo** only while the usual Windows account is signed in.
These are opportunities to process another Issue, **not guaranteed runs**:
every open PR, including Draft, blocks further work until reviewed/merged.

Each run:

1. Requires a dedicated checkout on `main` or detached HEAD, with `HEAD` equal
   to the locally recorded `origin/main`. Other named branches are refused.
   Tracked changes and untracked source files stop the run; only untracked
   regular `.pyc` files directly inside `tools/__pycache__/` and
   `.github/scripts/__pycache__/` are tolerated. Preflight creates no bytecode
   and does not fetch, update, reset, or clean the checkout. Research worktrees
   are not changed.
2. Checks **all open repository PRs**. If any PR is open (including Draft), it
   stops without spending Codex usage.
3. Queries owner-authored, open `local-autodev` Issues excluding the billed
   `autodev` label; selects only the lowest number.
4. Acquires a nonblocking Linux `flock` and creates exclusive,
   permission-restricted per-slot and per-Issue attempt markers **before**
   invoking Codex. These markers permanently block the same Issue from being
   retried even on subsequent days, and block more than one attempt in any one
   of the four JST daily slots. A legacy one-per-day marker blocks that day's
   additional slots after an in-place upgrade.
5. Delegates **one** call to `tools/local_autodev.py`, which checks
   ChatGPT authentication and the GitHub owner, runs Codex in a restricted
   read-only sandbox, applies the patch guard, and opens a Draft PR.
6. Stops on failures, power loss, timeouts, pending PRs, empty queue, or usage
   limits. No automatic retries, no daemon, no PR merges.

Logs and attempt markers are private WSL files at:

```text
~/.local/state/footprint-local-autodev/daily/
  day-YYYY-MM-DD-slot-0.json  # 09:05 slot
  day-YYYY-MM-DD-slot-1.json  # 13:05 slot
  day-YYYY-MM-DD-slot-2.json  # 17:05 slot
  day-YYYY-MM-DD-slot-3.json  # 21:05 slot
  issue-<number>.json
  result-YYYY-MM-DD-slot-<slot>.json
  log-YYYY-MM-DD-slot-<slot>.txt
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
the Issue and its slot remain recorded as used; later slots do not auto-retry it.
Check remaining account credits/allowance and manually disable the task
whenever needed. There is no automatic credit-state check.

## Install on Windows 11 (after this PR is reviewed, CI passes and it is merged)

First, run this in **WSL Ubuntu Bash** to update only the dedicated checkout.
The check works with the older script at `ce3aacd`, preserving existing Python
caches while refusing source changes. The parentheses keep a failure from
closing the interactive shell; subsequent steps stop on the first failure.

```bash
(
set -e
cd /home/higuchi/personal/me/footprint-sim/footprint-sim-local-autodev-pr6
python3 -B - <<'PY'
import os
from pathlib import Path
import stat
import subprocess

def git(*args):
    return subprocess.check_output(["git", *args])

if git("branch", "--show-current").strip() not in (b"", b"main"):
    raise SystemExit("Expected main or detached dedicated checkout; stop")
cache_dirs = {Path("tools/__pycache__"), Path(".github/scripts/__pycache__")}
for entry in git("status", "--porcelain=v1", "-z", "--untracked-files=all").split(b"\0"):
    if not entry:
        continue
    path = Path(os.fsdecode(entry[3:]))
    if (entry.startswith(b"?? ") and path.parent in cache_dirs
            and path.suffix == ".pyc" and stat.S_ISREG(path.lstat().st_mode)):
        continue
    raise SystemExit("Source/unexpected file changes present; preserve them and stop")
PY
git fetch origin main
git merge --ff-only origin/main
python3 -B -m unittest discover -s .github/scripts -p 'test_local_autodev_daily.py' -v
python3 -B tools/local_autodev_daily.py
)
```

Detached HEAD is expected and does not need a branch switch. Do not use
`git reset --hard`, `git clean`, or broad cache ignore rules to bypass a stop.
The final command is a dry run: it never invokes Codex or reserves an attempt.
An open PR, empty queue, or recorded attempt is a normal `DRY RUN` result.
Repeat this manual update after future approved merges; daily preflight checks
the local `origin/main` reference and does not check GitHub for a newer commit.

Then, in **Windows PowerShell**, as the normal logged-in user:

```powershell
& "\\wsl$\Ubuntu\home\higuchi\personal\me\footprint-sim\footprint-sim-local-autodev-pr6\tools\install_local_autodev_daily.ps1"
```

If local PowerShell execution policy blocks scripts, run the file from a
one-time process with `powershell.exe -NoProfile -ExecutionPolicy Bypass -File`
instead. The installer checks Tokyo timezone, verifies read-only WSL preflight,
then **replaces** the triggers on the existing task `Footprint Local AutoDev Daily` with four times.
The task name remains the same, and the existing 09:00 read-only monitor is not changed.
Settings:

- WSL distro `Ubuntu` and WSL user `higuchi`;
- daily Windows-local 09:05, 13:05, 17:05, 21:05 schedule;
- interactive Windows user principal at limited privilege;
- ignore concurrent triggers;
- 40-minute execution limit, no retry and no waking the PC;
- separate existing 09:00 monitor remains dry-run only.

**The installer does not start the scheduled task**, and the read-only
preflight does not invoke the model. Registration activates the next scheduled
one of the scheduled executions if the computer is awake and the account is logged on.

Verify registration from **Windows PowerShell**, not WSL Bash:

```powershell
Get-ScheduledTask -TaskName "Footprint Local AutoDev Daily" |
    Select-Object TaskName, State
Get-ScheduledTaskInfo -TaskName "Footprint Local AutoDev Daily" |
    Select-Object LastRunTime, LastTaskResult, NextRunTime
```

To stop unattended AI runs immediately from Windows PowerShell:

```powershell
Disable-ScheduledTask -TaskName "Footprint Local AutoDev Daily"
```

Do not use `Start-ScheduledTask` merely to test connectivity: that triggers a
real, accounted AI attempt when the queue is eligible.

## Limits and next steps

A PR remains review-gated: even CI success does not merge code.
The next Issue (#12, then #13) cannot run until the owner reviews and merges
the earlier PR. If a PR is still open at 13:05, 17:05 or 21:05, those
opportunities are skipped without model usage. No attempt at skipped slots
is made later that day. That constraint is deliberate and can slow progress, but keeps
candidate code from stacking uncontrolled changes. The per-slot counter uses the
Windows/WSL local machine's clock translated into Asia/Tokyo; clock tampering
or manually deleting ledger files is outside the threat model.
