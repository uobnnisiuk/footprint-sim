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


## R5 repair handoff: no scheduling changes at merge time

R5 is an **offline handoff and rollout policy**, not a new hourly task or an
additional daily model allowance. The existing 24 hourly *checks* remain
read-only unless the independently authorized single shared manager books a
work operation under the same existing `daily.lock`. The eight JST daily
reservation cap, one start per hour, first Issue lifetime one-shot, minor
lifetime one, substantive lifetime maximum two and combined repair lifetime
maximum two all remain strict. Automatic CI following a published fix is not
another active rerun; separately authorized active CI reruns do consume a
shared slot and can run once per exact head.

Old minor and new substantive worker must not both update the same PR/head.
A production transition requires the old minor direct-ref writer to use the
verified shared publisher *before* substantive can be enabled, or the latter
stays disabled. Do not trust a task's friendly name: read all saved task IDs,
exact prompts, enabled flags and schedules. If only some configuration
updates persist, stop; do not compensate by adding a new automation.

On Windows/WSL, independently archive exact old XML/enabled states, code SHA,
old/new reservation file hashes and modes, process state and all live locks.
Read the deployed runtime, not merely GitHub main. Observe shadow polling
without reservations; verify a safe, specifically authorized target before
any real operation. Preserve all unknown/timeout histories and the old
ledgers on rollback. A controller that cannot count R3 repair reservations
must never be restored while repair reservations exist.

`tools/pr_repair_handoff.py` and
`.github/scripts/test_pr_repair_handoff.py` provide deterministic offline
state-transition, pending-to-final CI, delayed-formal-approval recheck and
notification tests only. Their simulated history is
not a saved task update, a Windows deployment, or a successful unattended
production repair. The live R4 GitHub adapter and its independent trust source
are still unverified, so production remains disabled. See
`docs/autodev-pr-repair.md` for the full handoff, safety and activation gates.

## K8 runtime status returned through Issue #88

Issue #88 adds a fixed operational observer to the existing hourly controller.
It does not use the `local-autodev` code-edit queue or make a model call. An
ordinary reviewed infrastructure PR installs the observer through the existing
fast-forward update: the process which notices a runtime change stops without a
model reservation, and the next scheduled process loads the reviewed modules.
There is no new Windows task, action change or manual prompt-transfer step.

On an opted-in production invocation, after `run_hourly` has released its daily
lock, `tools/local_autodev_status.py` records the controller outcome and attempts
to update the dedicated result comment
https://github.com/uobnnisiuk/footprint-sim/issues/88#issuecomment-6700340146 .
The issue must remain open and contain the exact line
`K8-Status-Enabled: runtime-status-v1`. The destination comment must retain the
managed-result marker. These remote values enable only this fixed observer;
neither issue text nor comment text is executed or used as a command, path, or
destination. Closing the issue or removing its marker stops remote reporting.

The observer is limited to the existing dedicated K8 checkout and default
Local AutoDev state location. Other checkouts, CI and mock tests cannot publish
through this production path. The actual loaded hourly and observer files must
belong to that checkout, and a source revision is recorded only when the source
checkout is clean; otherwise the revision remains unknown. A dry run does not
call the observer. The new
module participates in the runtime fingerprint, and a process that has just
refreshed its runtime does not report with a mixture of old and new code.

### What the result proves

The fixed-schema report separates:

- The current Python hourly controller's terminal outcome and intended exit
  code, observed times and the revision loaded before preflight.
- The preceding locally saved controller outcome, when available and valid.
- A read-only snapshot of the existing Windows Daily task's state,
  LastRunTime, LastTaskResult and NextRunTime. A value observed while the same
  task is running is not proof that the whole Windows task completed.
- A bounded, redacted observation of the existing F22 last-poll file and its
  own modification time. A recognized `already-published` marker is a
  historical marker, not proof that the current poll re-ran the research.
- Availability of existing locks, tested without creating, deleting or
  retaining those locks.

Missing or malformed observational inputs are unknown individually; the observer
does not invent successful stages. Invalid or unsafe private reporter state is
preserved and stops that report, rather than being reset automatically. Empty
queues and ordinary PR waits are useful
controller outcomes, not evidence that the hourly poll failed to run. The
observer cannot prove the Task Scheduler trigger cause, the later return of
the outer F22 wrapper, long-term reliability, or the old Monitor's repair.
These limitations must not erase the controller execution that was observed.

### Isolation and handoff

Observer state is stored in a private `runtime-status` directory adjacent to
`daily`, never inside the immutable model-attempt ledger. It contains only
validated status records. The observer does not read raw model logs, trajectory
data or arbitrary files, and never emits or stores credentials or environment
values. Diagnostic failures
are isolated: they cannot alter the original controller exit code, model
reservation count, Issue retry policy or PR repair activation.

The reporter uses the existing GitHub authentication to GET the fixed issue
and comment and PATCH that same result comment. It creates no new heartbeat
comments. Writes are read back; an ambiguous response remains unconfirmed
unless the fetched content matches. A later poll may update that same slot,
without duplicating a post or re-running any research.

Code review, CI and merging are only the code-delivery stage. The cloud monitor
must wait for a real K8 report, validate its schema, timestamps and loaded
revision, then record the verified fields in UOB-10. The initial verification is
complete when that real report has been accepted. Issue #88 stays open as the
operational control for subsequent reports; closing it deliberately disables
reporting, rather than merely acknowledging the first result. The monitor must
not replace the result slot with cloud fixture output.

If no report arrives, check the actual update prerequisites: the existing F22
wrapper must reach hourly, the daily lock and budget ledger must permit refresh,
the dedicated checkout must be clean and fast-forwardable, and existing GitHub
authentication must work. Absence of a report alone proves none of those
conditions false. Only an identified failure that cannot be handled through
the established path warrants a specific local recovery action.
