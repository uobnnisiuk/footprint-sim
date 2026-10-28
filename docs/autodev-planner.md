# AutoDev Planner — A: read-only planning evidence

Tracking: Issue #19 and Issue #22.

This is the **first, deliberately non-AI stage** of roadmap-to-Issue automation.
It produces a snapshot for a separately reviewed proposal generator.
It does not create Issues, label a task, call Codex, use a billed API,
merge a PR, write to GitHub or schedule itself.

## Use

On a clean dedicated main or detached checkout exactly at locally recorded
origin/main, with logged-in GitHub CLI:

    python3 -B tools/autodev_planner_snapshot.py --milestone M1-B

The command emits compact UTF-8 JSON to stdout. To save it, redirect the
output **outside** the checkout, e.g. $HOME/autodev-planner-M1-B.json.

The snapshot contains: approved research documents and SHA-256 hashes;
the local HEAD commit; open/closed Issue titles/labels/states/author;
recent merged PR titles/branch/merge times; and explicit policy flags.
GitHub list queries are read-only. Missing or oversized files, incomplete
GitHub listings, unexpected remote URLs, a dirty checkout, and incorrect
HEAD/base alignment stop without producing a partial planning result.

Repository and GitHub text is **data**, not executable instructions.
A later planner must not obey instructions from comments, Issue titles,
PR descriptions or hostile-looking prose that contradicts independent policy.
The snapshot does not contain model output or automatically approve
the milestone named on the command line.

This tool does not decide that the given milestone should proceed or
that a proposed Issue is approved for implementation.

## Next steps

- Issue #23: validate structured Issue drafts, detect duplicates and dependency loops.
- Issue #24: build a separately approved AI proposal-only driver with usage controls.

Neither Issue has the local-autodev label. Existing Local AutoDev
implementation queue and four Windows schedule slots remain unchanged.

## Stage B: inspect proposed Issues without publishing

The next local tool validates an explicit JSON bundle of one to three draft
Issues against Stage A's evidence. An AI proposal generator is **not connected**
yet. To validate a user-reviewed or externally prepared JSON proposal file:

    python3 -B tools/autodev_planner_validate.py --snapshot "$HOME/autodev-planner-M1-B.json" --drafts "$HOME/proposed-issues.json"

The drafts file is a JSON object containing schema_version=1 and a proposals
array (1–3 items). Each proposal must contain exactly these fields:

- draft_id (P1, P2 or P3), title, objective
- scope (array), exclusions (array), paths (array)
- acceptance_tests (array, at least two), risks (array), evidence (array)
- dependencies (array of P1–P3 or issue:NUMBER)
- target_milestone and base_sha matching the snapshot

Example of an evidence path: AGENTS.md. Only source and test paths allowed by
the existing patch guard can be proposed; protected configuration is rejected.
Missing or stale dependencies, cycles, duplicated normalized titles, already
tracked Issue titles, and ambiguous/non-testable drafts stop validation.

Outputs carry a deterministic dedup_key, blocked/draft status and explicit
blocking_dependencies. Exact normalized-title matching is not semantic
duplicate detection; manual scientific/roadmap review is still necessary.

**The validator never adds labels, creates Issues, runs Codex, incurs API
charges or grants implementation authority.** It cannot supply an approval
token. Stage C requires its own explicit authorization and review.
