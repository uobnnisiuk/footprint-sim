# Local AutoDev MVP — ChatGPT-plan runner

This is an **opt-in local execution alternative** to the existing GitHub Actions `autodev` workflow. The existing workflow uses `OPENAI_API_KEY` (separately billed API tokens). For the local prototype **do not register `OPENAI_API_KEY` and never add the `autodev` label to an Issue**.

The local runner is `tools/local_autodev.py`; the smoke-test Issue is [#3](https://github.com/uobnnisiuk/footprint-sim/issues/3). Tracking: [#4](https://github.com/uobnnisiuk/footprint-sim/issues/4).

## Cost and usage safeguards

- `codex login status` must explicitly report **ChatGPT** authentication. If it shows API-key login, stop and use `codex login` to sign in with ChatGPT.
- The ChatGPT Plus plan includes a Codex usage allowance; signing in with ChatGPT does not make separately billed OpenAI API calls. **Purchased ChatGPT credits may nevertheless be consumed when included usage is exhausted.** Check remaining usage/credits in ChatGPT, turn **automatic credit reload off**, and do not buy credits if you want to avoid incremental spending. The local script cannot query or enforce account credit balances.
- The runner explicitly removes API-key environment variables before invoking Codex. There is no API-key fallback, retry loop, background schedule, or automatic model upgrade. `--execute` runs a *single* task.
- Public-repository standard GitHub-hosted Actions runners ordinarily have no billed minutes. Verify billing policy if the repo becomes private or self-hosted/premium runners are introduced.

**Do not run `--execute` until you've reviewed the account's credit settings.** It starts actual Codex usage against your plan.

## Setup on WSL Ubuntu

Existing checkout: `/home/higuchi/personal/me/footprint-sim`.

After reviewing and merging the installation PR, run:

```bash
cd /home/higuchi/personal/me/footprint-sim
git fetch origin main
# Integrate changes using your normal workflow; do not discard local edits.
python3 -m unittest discover -s .github/scripts -p 'test_*.py' -v
codex login status
gh auth status
gh label create local-autodev --repo uobnnisiuk/footprint-sim \
  --color 0366d6 --description 'Approved local ChatGPT-plan task'
```

Sign in with ChatGPT via `codex login` if needed. The GitHub CLI must be signed in as `uobnnisiuk`. A preexisting `local-autodev` label may make label creation return an error; if it already exists, continue.

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
2. The runner checks ChatGPT login, GitHub owner login, owner-authored open Issue, and approval via `local-autodev` label. It refuses to overwrite an existing branch or PR for that Issue.
3. It fetches `origin/main` and creates an isolated temporary Git worktree pinned to that commit. Existing local branches and uncommitted changes stay intact.
4. A single `codex exec --sandbox read-only --ignore-user-config --ephemeral` call inspects sources and outputs only a raw unified patch into a file outside the working tree; neither candidate code nor its tests are executed in the local credential-bearing environment.
5. The existing trusted `.github/scripts/patch_guard.py` applies only allowlisted UTF-8 changes, rejecting protected configuration, unsafe paths, invalid patch structures, special file modes, and symlinks.
6. If patch validation succeeds, a new branch and commit are pushed, and GitHub CLI creates a **Draft PR**. GitHub's normal read-only `pull_request` CI validates the code in a separate runner. The user manually reviews and merges.

The read-only Codex sandbox and environment-variable filtering **do not completely isolate local credentials or the user's home directory**. For stronger isolation, use a dedicated restricted OS account and stronger credential boundaries. Source files and Issue text are untrusted. Do not use this first prototype for sensitive repositories.

Generated code is not run locally; however, it **will execute during GitHub CI** inside its isolated, read-only job. Inspect CI permissions and repository secrets before expanding this to other repositories.

## Failure modes

- Incorrect login, missing label, issue not owner-authored, existing branch/PR → stop.
- API-key login → stop (no fallback).
- No patch, `NO_CHANGES`, malformed patch, invalid paths → stop without pushing.
- Usage limit or Codex timeout → stop without retrying.
- Remote push succeeded but PR creation failed → remote branch remains for manual recovery. The runner refuses automatic overwrites.
- A later change to `main` can make the PR stale; review GitHub's merge checks before merging.

## Status

Python syntax/unit tests and offline patch-protection logic have been inspected. End-to-end execution on the owner's WSL machine with ChatGPT authentication remains **unverified**. This MVP does not yet poll Issues or run unattended. Expand only after the initial smoke test succeeds.
