# Unattended Local AutoDev: one bounded attempt per 3-hour JST window

**Owner approval:** On 2026-10-27 the owner approved unattended
ChatGPT-plan Local AutoDev execution and subsequently requested increased
frequency. The enabled schedule offers **00:05, 03:05, 06:05, 09:05, 12:05, 15:05, 18:05, and 21:05 JST**,
with **at most one Issue attempt per slot (eight per JST day)**. It may consume
existing purchased ChatGPT credits.
This approval does **not** authorize API-key billing or automatic PR merge.

## Safeguards

`tools/local_autodev_daily.py` is separate from the existing read-only
daily 09:00 monitor, and is **not a background service** on its own.
The Windows task installer registers a second task at **00:05, 03:05, 06:05, 09:05, 12:05,
15:05, 18:05 and 21:05 Asia/Tokyo** only while the usual Windows account is signed in.
These are opportunities to process another Issue, **not guaranteed runs**:
every open PR, including Draft, blocks further work until reviewed/merged.

Each run:

1. Requires a dedicated checkout on `main` or detached HEAD, with `HEAD`
   equal to or an ancestor of the locally recorded `origin/main`. Other
   named branches or diverged histories are refused.
   Tracked changes and untracked source files stop the run; only untracked
   regular `.pyc` files directly inside `tools/__pycache__/` and
   `.github/scripts/__pycache__/` are tolerated. Preview does not fetch or
   change any files. **Real scheduled executions** verify the trusted GitHub
   origin, fetch remote main and fast-forward only this clean, dedicated
   checkout under the execution lock before any Codex use. If the local HEAD
   diverged or any user files changed, the refresh fails closed. No reset or
   clean is ever performed, and research worktrees are not changed.
2. Checks **all open repository PRs**. If any PR is open (including Draft), it
   stops without spending Codex usage.
3. Queries owner-authored, open `local-autodev` Issues excluding the billed
   `autodev` label; selects the lowest-numbered **never-attempted** Issue.
   Previously attempted Issues are skipped without altering their immutable
   attempt markers or retriggering the failed model call. This prevents a
   blocked Issue from starving separately approved later work. If none remains,
   the run stops without reserving the slot or calling Codex.
4. Acquires a nonblocking Linux `flock` and creates exclusive,
   permission-restricted per-slot and per-Issue attempt markers **before**
   invoking Codex. These markers permanently block the same Issue from being
   retried even on subsequent days, and block more than one attempt in any one
   of the eight three-hour JST daily slots. A legacy one-per-day marker blocks that day's
   additional slots after an in-place upgrade.
5. Delegates **one** call to `tools/local_autodev.py`, which checks
   ChatGPT authentication and the GitHub owner, runs Codex in a restricted
   read-only sandbox, applies the patch guard, and opens a Draft PR.
6. Stops on failures, power loss, timeouts, pending PRs, empty queue, or usage
   limits. No automatic retries, no daemon, no PR merges.

Logs and attempt markers are private WSL files at:

```text
~/.local/state/footprint-local-autodev/daily/
  day-YYYY-MM-DD-3h-slot-0.json  # 00:05
  day-YYYY-MM-DD-3h-slot-1.json  # 03:05
  day-YYYY-MM-DD-3h-slot-2.json  # 06:05
  day-YYYY-MM-DD-3h-slot-3.json  # 09:05
  day-YYYY-MM-DD-3h-slot-4.json  # 12:05
  day-YYYY-MM-DD-3h-slot-5.json  # 15:05
  day-YYYY-MM-DD-3h-slot-6.json  # 18:05
  day-YYYY-MM-DD-3h-slot-7.json  # 21:05
  issue-<number>.json
  result-YYYY-MM-DD-3h-slot-<slot>.json
  log-YYYY-MM-DD-3h-slot-<slot>.txt
```

The directory is restricted to mode 0700; records/logs use 0600.
Model command output stays in local log files (never uploaded to GitHub).
A crash or interruption deliberately consumes the attempt without re-running.
Do not delete markers to reset a failed Issue without manually investigating
the cause and getting a new approval for any retry.

The daily runner invokes `tools/local_autodev.py` directly. It does not update the
older controller's `attempts/` directory or `last-failure.json`. The `started`
day marker records the reservation; use `result-...json` for the final outcome.
Its `exitCode` is the single-Issue runner's exit code, not Codex's exit code.

Codex, structured-response conversion, and patch-validation failures have separate private
summaries under `~/.local/state/footprint-local-autodev/diagnostics/`. For a guard
failure, the daily log includes a fixed reason and a `patch-guard-failure-*.json`
path. That JSON identifies the Issue and trusted base SHA. See
[failure diagnostics](autodev-local.md#failure-diagnostics-no-model-call) for
reason codes and what is deliberately not retained. Logs made before this
diagnostic support cannot be reconstructed by reading the older controller files.

Deploy the runner, structured-edit converter, output schema and guard together
through the reviewed main branch: the runner uses these files from a temporary
checkout of `origin/main`. Replacing only the local runner while main still has
older conversion/validation files is not a supported
upgrade. An upgrade does not authorize or clear a recorded Issue attempt.

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
**Only the initial update after merging this maintenance PR is manual.**
After installation, each authorized scheduled execution safely refreshes this
dedicated checkout from the GitHub `main` branch, so future reviewed PR merges
do not require you to run these commands again. Read-only previews still do
not fetch or modify any files.

Then, in **Windows PowerShell**, as the normal logged-in user:

```powershell
& "\\wsl$\Ubuntu\home\higuchi\personal\me\footprint-sim\footprint-sim-local-autodev-pr6\tools\install_local_autodev_daily.ps1"
```

If local PowerShell execution policy blocks scripts, run the file from a
one-time process with `powershell.exe -NoProfile -ExecutionPolicy Bypass -File`
instead. The installer checks Tokyo timezone, verifies read-only WSL preflight,
then **replaces** the triggers on the existing task `Footprint Local AutoDev Daily` with eight 3-hour times.
The task name remains the same, and the existing 09:00 read-only monitor is not changed.
Settings:

- WSL distro `Ubuntu` and WSL user `higuchi`;
- daily Windows-local 00:05, 03:05, 06:05, 09:05, 12:05, 15:05, 18:05, 21:05 schedule;
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

Local AutoDev PRs are separately reviewed by the configured hourly ChatGPT
PR monitor. The user's 2026-10-27 opt-in permits that reviewer to merge an
owner-authored local-autodev/issue-N PR only after verifying every diff,
Issue acceptance criterion, patch guard scope, CI success and head/main SHA.
Ambiguous, infrastructure, security-sensitive and other non-Local-AutoDev PRs
are never auto-merged. CI alone is not sufficient. Branch protections remain
in force; this program itself never performs a merge.

A PR remains review-gated: even CI success alone does not merge code.
The next Issue (#12, then #13) cannot run until the owner reviews and merges
the earlier PR. If a PR is still open at the next three-hour scheduled time, those
opportunities are skipped without model usage. No attempt at skipped slots
is made later that day. That constraint is deliberate and can slow progress, but keeps
candidate code from stacking uncontrolled changes. The per-slot counter uses the
Windows/WSL local machine's clock translated into Asia/Tokyo; clock tampering
or manually deleting ledger files is outside the threat model.

## Migration from four slots to 3-hour cadence

The earlier four-slot ledger used `day-YYYY-MM-DD-slot-N.json`, with N
associated with 09:05, 13:05, 17:05, 21:05. The newer eight-slot ledger uses
a distinct `-3h-slot-N.json` namespace. Existing four-slot attempts remain
authoritative inside their corresponding three-hour windows. The original
one-per-day legacy marker also remains authoritative. Per-Issue
`issue-N.json` markers are **never reset** or reused.

A new checkout update alone does not change Windows Task Scheduler. After
the reviewed PR is merged and the dedicated WSL checkout is updated, rerun
`install_local_autodev_daily.ps1`. It replaces the four triggers with eight
and performs a read-only dry run; it does **not** start Codex. The Windows
account must still be logged on and PC awake at each trigger time.

Moving from four to eight potential Codex calls daily increases plan/credit
consumption risk. There is still no automatic credit-balance spending cap.
An approved unattempted Issue is needed, and any open PR blocks the run.
