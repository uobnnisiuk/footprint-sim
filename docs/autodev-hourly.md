# Hourly polling with a separate eight-attempt daily limit

This is an opt-in scheduling change, not permission for 24 daily model calls.
The existing `Footprint Local AutoDev Daily` task checks at 00:05 through 23:05
JST. Only approved unattempted work may call the existing ChatGPT-authenticated
one-shot runner. The hard limit is **eight reserved attempts per JST calendar
day**, at most one per hour, including failures, timeouts and uncertain calls.
Open PRs, one concurrent runner and per-Issue no-retry protections remain.
An empty queue or open PR does not consume an attempt.

The original three-hour controller, launcher and installer remain unchanged.
Merging this change alone does **not** change Windows's existing triggers or
action. After independent code review and CI, Work must update only the dedicated
checkout and run `tools/install_local_autodev_hourly.ps1` in normal Windows
PowerShell. It stages the replacement Daily definition **disabled** (no duplicate
task), performs a read-only preflight, verifies all 24 daily triggers, action,
principal and safety settings, and never starts a model or manually starts the
task. Monitor is untouched. Normal interactive logon, IgnoreNew, no automatic
retry, no wake, no missed-run catch-up, battery behavior and the 40-minute task
limit remain. Windows must use Tokyo Standard Time.

Before changing the checkout, save the original Daily XML and enabled state
outside the repository. Work away from a trigger; confirm the task is idle,
disable it, then inspect actual WSL processes and acquire the existing
`daily/daily.lock`, `runner.lock` and `controller.lock` without blocking. A
scheduler state check alone is not an atomic deployment barrier. Do not kill
an in-flight model or continue when a lock is busy. Hold the locks across the
fast-forward, source verification, dry-run and installer invocation. The
installer rejects an enabled task and an elevated PowerShell process. Its own
backup is the already disabled definition, so keep the original backup too.

After the staged definition and unchanged ledgers/Monitor are verified, release
the deployment locks while Daily remains disabled, restore the saved enabled
state and read the actual
`Get-ScheduledTaskInfo` NextRunTime. If verification fails, leave Daily disabled,
retain all records and report the incomplete step; do not enable or roll back
by guessing. No `Start-ScheduledTask`, production `--execute` or model invocation
is part of deployment validation.

## Reservation and migration

The new controller uses the SAME `daily.lock`, `issue-N.json` and daily state
directory. New files are `day-YYYY-MM-DD-hour-H.json`, `log-...-hour-H.txt` and
`result-...-hour-H.json`. Reservations are written before model invocation.
No reservation or Issue marker is removed, reset, overwritten or discounted
because of failure. Both old `-slot-N` and `-3h-slot-N` reservations count against
the same eight-attempt total; their current three-hour bucket remains blocked
conservatively. An original one-per-day marker blocks the entire date. Every
historical and current reservation must match exactly one per-Issue marker;
missing or mismatched counterparts, duplicate Issue reservations, orphaned
results/logs, and malformed or unknown entries stop execution rather than
guessing unused credit. This also prevents a partial reservation write from
making an Issue eligible again on a later hour or date. Missing result/log
files can follow a crash and do not restore an allowance. Result/log files
are not extra attempts. Validation never repairs or removes a ledger entry.

The JST date/hour is sampled again after network preflight, under the existing
lock. Midnight resets only the DATE allowance, never Issue eligibility. This is
not an account-wide monetary cap or rolling-24-hour credit budget. Other manually
started models/Work sessions are outside this controller and remain unapproved
by this scheduling change. No API fallback, purchase or recharge is introduced.

Do NOT restore an old runner after hourly attempts have occurred that day: the
old binary cannot understand the new ledger. If reverting is necessary, disable
Daily for the remainder of that JST day, retain every marker, and restore the
reviewed old definition for a later date after checking no in-flight process.
Changing the task action while a run is in progress is forbidden. The installer
requires a disabled task and leaves the replacement disabled; the operator must
also establish process quiescence and hold the shared WSL locks as described
above. Disabled scheduler state alone does not prove a previous process ended.

The new process defers if its imported runtime changes during automatic checkout
refresh. This avoids using old loaded policy with newly written source. No slot
is reserved; the next scheduled poll loads the reviewed revision.

## Evidence before claiming deployment

Run the full existing Python suite plus `test_local_autodev_hourly.py` and normal
repository CI. Locally mocked controller tests are not a Windows installation
or live Codex test. On Windows verify the actual action, 24 enabled triggers,
ordinary user/Ubuntu/higuchi, unchanged Monitor/attempt files and the actual
NextRunTime. Perform only a dry run. Record deployment SHA and definition in
GitHub; the cloud monitor must not infer hourly activation from a merge alone.
