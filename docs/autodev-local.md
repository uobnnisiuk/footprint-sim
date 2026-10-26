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
4. A single `codex exec --sandbox read-only --ignore-user-config --ignore-rules --ephemeral` call with `model_provider="openai"`, `forced_login_method="chatgpt"`, `approval_policy="never"`, and `web_search="disabled"` inspects sources and outputs only a raw unified patch into a file outside the working tree. Project `.codex` configuration is rejected for this MVP. The prompt prohibits executing project scripts/tests; the runner never executes candidate code or tests locally. The read-only sandbox is a write restriction, not a guarantee that source commands cannot be run.
5. The existing trusted `.github/scripts/patch_guard.py` applies only allowlisted UTF-8 changes, rejecting protected configuration, unsafe paths, invalid patch structures, special file modes, and symlinks.
6. All runner Git commands disable hooks and fsmonitor. Timeouts terminate the command process group before worktree cleanup. If patch validation succeeds, a new branch and commit are pushed, and GitHub CLI creates a **Draft PR**. GitHub's normal read-only `pull_request` CI validates the code in a separate runner. The user manually reviews and merges.

The read-only Codex sandbox and environment-variable filtering **do not completely isolate local credentials or the user's home directory**. For stronger isolation, use a dedicated restricted OS account and stronger credential boundaries. Source files and Issue text are untrusted. Do not use this first prototype for sensitive repositories.

Generated code is not run locally; however, it **will execute during GitHub CI** inside its isolated, read-only job. Inspect CI permissions and repository secrets before expanding this to other repositories.

## Failure diagnostics (no model call)

On a nonzero exit or timeout from \`codex exec\`, the runner now writes a small, filtered
stderr excerpt under \`~/.local/state/footprint-local-autodev/diagnostics/\` using
0700 directory and 0600 file permissions. The path appears in the error message.
It records the exit code or timeout and up to 25 bounded *error-like* stderr lines,
after credential-like tokens and URLs are redacted. It intentionally discards
stdout, full prompts and normal CLI transcripts.

This is **best-effort sanitization**, not a formal guarantee: review the local log
before sharing any excerpt. If the CLI writes no recognized diagnostic lines,
the log says so rather than inventing a cause. No automatic retry takes place.

To inspect locally after a failure:

\`\`\`bash
ls -lt ~/.local/state/footprint-local-autodev/diagnostics/
# Open the exact path reported by the failed run in your editor.
\`\`\`

Do not paste raw local logs into issues or chat without checking for personal
information and credentials.

## Failure modes

- Incorrect login, missing label, issue not owner-authored, existing branch/PR → stop.
- API-key login → stop (no fallback).
- No patch, `NO_CHANGES`, malformed patch, invalid paths → stop without pushing.
- Usage limit or Codex timeout → stop without retrying; on timeout, child processes are terminated too.
- Remote push succeeded but PR creation failed → remote branch remains for manual recovery. The runner refuses automatic overwrites.
- A later change to `main` can make the PR stale; review GitHub's merge checks before merging.

## Status

Python tests cover authentication output, remote allowlisting, environment filtering, duplicate detection, timeout cleanup, dry-run behavior, and the existing patch guard. A successful dry-run validates prerequisites without starting a model call. End-to-end patch generation and Draft PR publication remain **unverified** until an explicitly approved `--execute` smoke test. This MVP does not poll Issues or run unattended.

CLI authentication and billing references: [OpenAI authentication](https://learn.chatgpt.com/docs/auth), [CLI reference](https://learn.chatgpt.com/docs/cli/reference), [Codex pricing](https://learn.chatgpt.com/docs/pricing).
