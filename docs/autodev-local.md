# Local AutoDev MVP — ChatGPT-plan runner

This is an **opt-in local execution alternative** to the existing GitHub Actions `autodev` workflow. The existing workflow uses `OPENAI_API_KEY` (separately billed API tokens). For the local prototype **do not register `OPENAI_API_KEY` and never add the `autodev` label to an Issue**.

The local runner is `tools/local_autodev.py`; the smoke-test Issue is [#3](https://github.com/uobnnisiuk/footprint-sim/issues/3). Tracking: [#4](https://github.com/uobnnisiuk/footprint-sim/issues/4).

## Cost and usage safeguards

- `codex login status` must explicitly report **ChatGPT** authentication. If it shows API-key login, stop and use `codex login` to sign in with ChatGPT.
- The ChatGPT Plus plan includes a Codex usage allowance; signing in with ChatGPT does not make separately billed OpenAI API calls. **Purchased ChatGPT credits may nevertheless be consumed when included usage is exhausted.** Check remaining usage/credits in ChatGPT, turn **automatic credit reload off**, and do not buy credits if you want to avoid incremental spending. The local script cannot query or enforce account credit balances.
- The runner passes only a small allowlist of OS environment settings to Codex, ignores user configuration and execution rules for the agent call, and pins the OpenAI provider with ChatGPT-only authentication. There is no API-key fallback, retry loop, background schedule, or automatic model upgrade. `--execute` runs a *single* task.
- Public-repository standard GitHub-hosted Actions runners ordinarily have no billed minutes. Verify billing policy if the repo becomes private or self-hosted/premium runners are introduced.

**Do not run `--execute` until you've reviewed the account's credit settings.** It starts actual Codex usage against your plan.

## Setup on WSL Ubuntu

The parent folder is `/home/higuchi/personal/me/footprint-sim`; the existing research checkout is its `footprint-sim/` subdirectory. Use a separate clean worktree at `/home/higuchi/personal/me/footprint-sim/footprint-sim-local-autodev` to preserve the research branch and uncommitted changes. The runner defaults to the checkout containing the script; `--repo-path` can select another trusted checkout.

Install Codex CLI and GitHub CLI on WSL first. Verified CLI versions: Codex 0.164.0 and gh 2.103.2. After reviewing and merging the installation PR and updating the separate worktree, run:

```bash
cd /home/higuchi/personal/me/footprint-sim/footprint-sim-local-autodev
# Use the merged main revision in this clean worktree; never reset the research checkout.
python3 -m unittest discover -s .github/scripts -p 'test_*.py' -v
codex login status
gh auth status
gh label create local-autodev --repo uobnnisiuk/footprint-sim \
  --color 0366d6 --description 'Approved local ChatGPT-plan task'
```

Sign in with ChatGPT via `codex login` if needed. The GitHub CLI must be signed in as `uobnnisiuk` (`gh auth login --hostname github.com --git-protocol https --web`, then `gh auth setup-git`). Authentication status is checked without printing token values. Existing Git credentials may lack the scopes required by GitHub CLI. A preexisting `local-autodev` label may make label creation return an error; if it already exists, continue.

Only when ready to approve the *local* task, add the separate label:

```bash
gh issue edit 3 --repo uobnnisiuk/footprint-sim --add-label local-autodev
```

Preflight (no model invocation, no write to GitHub):

```bash
python3 tools/local_autodev.py --issue 3
```

Run a single local agent task only after cost/credit confirmation:

```bash
python3 tools/local_autodev.py --issue 3 --execute
```

On success the runner creates a `local-autodev/issue-3` branch and a **Draft PR**. Review source and GitHub CI before manually merging.

## Architecture and security boundaries

1. The user is the only trigger: a one-time `--execute`. No daemon, timer, self-scheduling, or GitHub webhook starts the local Codex job.
2. The runner checks ChatGPT login (including its stderr status), GitHub owner login, exact fetch and push remote URLs, an owner-authored open Issue, and approval via `local-autodev` label. The API-workflow `autodev` label is rejected. Existing local/remote candidate branches and PRs stop the run before model usage.
3. It fetches `origin/main` and creates an isolated temporary Git worktree pinned to that commit. Existing local branches and uncommitted changes stay intact.
4. A single `codex exec --sandbox read-only --ignore-user-config --ignore-rules --ephemeral` call with `model_provider="openai"`, `forced_login_method="chatgpt"`, `approval_policy="never"`, and `web_search="disabled"` inspects sources and writes a structured JSON proposal outside the working tree. `--output-schema` points to `.github/scripts/local_autodev_response.schema.json`; preflight checks support using `codex exec --help`, without model usage or automatic upgrades. Project `.codex` configuration is rejected. The prompt prohibits executing project scripts/tests; the runner never executes candidate code or tests locally. The read-only sandbox is a write restriction, not a guarantee that source commands cannot be run.
5. The trusted `.github/scripts/structured_edits.py` validates the response, resolves exact replacements against the fixed base SHA, and generates a diff using isolated Git objects/index. The existing `.github/scripts/patch_guard.py` must then accept the generated patch before applying anything. Its path, protected-file, UTF-8, size, mode, symlink and applicability restrictions remain in force.
6. All runner Git commands disable hooks and fsmonitor. Timeouts terminate the command process group before worktree cleanup. If patch validation succeeds, a new branch and commit are pushed, and GitHub CLI creates a **Draft PR**. GitHub's normal read-only `pull_request` CI validates the code in a separate runner. The user manually reviews and merges.

The read-only Codex sandbox and environment-variable filtering **do not completely isolate local credentials or the user's home directory**. For stronger isolation, use a dedicated restricted OS account and stronger credential boundaries. Source files and Issue text are untrusted. Do not use this first prototype for sensitive repositories.

Generated code is not run locally; however, it **will execute during GitHub CI** inside its isolated, read-only job. Inspect CI permissions and repository secrets before expanding this to other repositories.

## Structured replacement protocol

The model returns this shape, with no extra fields:

```json
{"version":1,"status":"changes","edits":[{"path":"README.md","before":"exact original text","after":"replacement text"}]}
```

Only existing ordinary `100644` UTF-8 files are supported in this version. New
files, deletions and renames are intentionally unsupported. `before` must be
nonempty and match exactly once, counting overlapping occurrences, in the
original file from the trusted base. Every edit is checked against that original
content. Duplicate or overlapping ranges are rejected; inserted text is never
used as another edit's search target. There is no whitespace repair, newline
normalization, fuzzy matching, partial application or repair model call.
UTF-8, Japanese, CRLF and final-newline state are preserved byte for byte except
where the approved replacement explicitly changes them.

`{"version":1,"status":"no_changes","edits":[]}` stops without applying a patch
or publishing a PR. The runtime validates semantics independently of the output
schema: duplicate JSON keys, unknown fields, wrong types, NUL, invalid UTF-8 or
Unicode surrogates, oversized responses and unsafe paths are rejected.
Accepted responses are limited to 2,000,000 bytes and 100 edits, with the guard's
existing 30-file, 180,000-byte file and 400,000-byte patch limits. Rejected input
can be fingerprinted up to a hard 8,000,000-byte read limit; larger files are
rejected using their size without reading their contents, so their full SHA256
is explicitly unavailable (`null`). Missing or unreadable input likewise has no
invented fingerprint.

All replacements are resolved before output publication. A fresh bare Git
repository with an isolated index generates the headers and hunk counts from raw
blobs; the real worktree/index remains untouched during conversion. Hooks,
external diff, textconv and filters are disabled or bypassed. The generated diff
still passes the unchanged patch guard. Candidate source and scripts are never
executed as part of this conversion or local validation.

Deploy the runner, converter, schema and guard from the same reviewed revision.
The daily schedule, one-Issue limit, locks, open-PR stop and immutable attempt
records are unchanged. A failed Issue must not be retried or copied into a new
Issue to bypass its record.

## Failure diagnostics (no model call)

On a nonzero exit or timeout from `codex exec`, response conversion or patch validation, the runner saves a small JSON
summary under `~/.local/state/footprint-local-autodev/diagnostics/`. The directory
is restricted to 0700 and each uniquely named file to 0600. Symlinked diagnostic
paths are refused. The error message includes the local file path; no diagnostic
is automatically uploaded to GitHub.

Codex summaries (`codex-failure-*.json`) contain only fixed fields: schema version,
command, `stage: "model_execution"`, exit or timeout, exit code, a known error
category/code, and an HTTP error status when recognized. Executed tasks also
include the Issue number, base SHA and prompt byte count/SHA256; no prompt text
is retained.
It never saves arbitrary stderr lines, stdout, prompts, credential fields, URLs,
or response bodies. Unknown errors stay `unknown`; endpoint and unrecognized
error details are intentionally unavailable. Classification is a hint from CLI
output, not proof of a particular authentication failure such as token expiry.

Patch validation summaries (`patch-guard-failure-*.json`) contain the schema version,
`command: "patch_guard"`, `stage: "patch_validation"`, Issue number, trusted base SHA,
exit/timeout status, exit code, and a fixed `reason`. The guard's optional
`--diagnostic-json` mode emits this reason without a traceback, patch contents,
filenames from the patch, or raw Git output. The runner accepts only the exact
protocol and known reasons; malformed or unrecognized output becomes `unknown`.
The runner also records the generated patch's SHA256 and byte count.

Structured response summaries (`structured-edit-failure-*.json`) record one of
`response_validation`, `edit_resolution`, or `diff_generation`, the Issue/base,
original response SHA256/byte count when available, a one-based operation number
and match count when relevant, and a fixed reason. No paths or source fragments
are saved. Examples include `invalid_json`, `invalid_schema`, `match_not_found`,
`ambiguous_match`, `overlapping_edits`, `baseline_mismatch`, and `dirty_checkout`.
Unknown or malformed diagnostic protocols become `diagnostic_unavailable`.

| Reason | Next inspection |
| --- | --- |
| `invalid_patch`, `malformed_diff`, `malformed_hunk`, `invalid_encoding` | Patch transport, headers, hunk line counts, or UTF-8 encoding |
| `patch_not_applicable` | Patch context against the trusted base commit |
| `disallowed_path`, `disallowed_file`, `size_limit` | Allowed paths, file modes, file types, and size limits |
| `baseline_mismatch`, `dirty_checkout` | Temporary checkout baseline and cleanliness |
| `git_failure`, `io_error`, `internal_error`, `unknown` | Local execution or diagnostic protocol failure |

The progress log is flushed at model completion, response validation, and
Git-generated patch validation. A patch-guard failure
also prints its fixed reason and diagnostic path. The daily result's `exitCode`
is the single-Issue runner's status, not necessarily the Codex process's status.

Only Codex, structured conversion and patch validation opt into this logging. A later Git cleanup failure
cannot overwrite their unique records. A diagnostic write failure still stops the
command safely without exposing raw output. The runner does not retry failed
invocations or start further model work.

Temporary response JSON and candidate patches are deleted on exit; raw response
retention is not enabled. `--ephemeral` does not
retain a Codex session transcript. This change cannot recover detailed reasons for
older failures whose diagnostics were discarded; do not remove attempt markers
or rerun an Issue solely to reconstruct them.

To inspect locally after a failure:

```bash
ls -lt ~/.local/state/footprint-local-autodev/diagnostics/
# Open the exact JSON path reported by the failed run in your editor.
```

`codex login status` confirms the stored login method; it does not guarantee the
next server request will accept those credentials. A minimal connection test succeeded with the runner's ChatGPT authentication
options, and a separately approved full Issue #3 run also succeeded. A dry-run
checks prerequisites without model inference. Never run `--execute` solely to
diagnose a failure unless the owner has approved a new one-off execution.

## Failure modes

- Incorrect login, missing label, issue not owner-authored, existing branch/PR → stop.
- API-key login → stop (no fallback).
- Missing/invalid JSON, structured `no_changes`, ambiguous edits, guard rejection → stop without pushing.
- Usage limit or Codex timeout → stop without retrying; on timeout, child processes are terminated too.
- Remote push succeeded but PR creation failed → remote branch remains for manual recovery. The runner refuses automatic overwrites.
- A later change to `main` can make the PR stale; review GitHub's merge checks before merging.

## Status

Python tests cover authentication output, remote allowlisting, environment filtering, duplicate detection, timeout cleanup, dry-run behavior, and the existing patch guard. A successful dry-run validates prerequisites without starting a model call. The owner-approved one-off Issue #3 run succeeded with ChatGPT login: patch guard passed, Draft PR #7 was created, CI passed, and the reviewed PR was merged. The single-Issue runner still does not poll Issues or run unattended; see [autodev-queue.md](autodev-queue.md) for the separately opt-in one-Issue queue preview.

CLI authentication and billing references: [OpenAI authentication](https://learn.chatgpt.com/docs/auth), [CLI reference](https://learn.chatgpt.com/docs/cli/reference), [Codex pricing](https://learn.chatgpt.com/docs/pricing).
