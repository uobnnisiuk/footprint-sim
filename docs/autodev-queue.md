# Local AutoDev queue — phase 2 preview

Issue #8 tracks this phase. The first Local AutoDev run completed Issue #3,
created Draft PR #7, passed CI, and was reviewed and merged to main as
`c649183d54047560b675859c2c55a7fc467d45dd`.

`tools/local_autodev_queue.py` removes the need to specify an Issue number
on the command line. It queries open GitHub Issues with the `local-autodev`
label, filters to author `uobnnisiuk` and excludes Issues with the separately
metered `autodev` label, then selects the lowest Issue number.

## Safe preview (no AI call, no GitHub write)

```bash
cd /home/higuchi/personal/me/footprint-sim/footprint-sim-local-autodev-pr6
# Update this clean checkout to the merged branch only after reviewing its PR.
python3 tools/local_autodev_queue.py
```

If no eligible open issue exists, it reports that fact and stops. The queue
runner does not create approval labels. The repository owner must deliberately
apply the `local-autodev` label to a suitable Issue. The label alone **does
not** authorize a model invocation.

## One explicitly authorized run

**Do not run this during the PR review.** Only after the owner checks plan
usage, purchased ChatGPT credits and auto-recharge settings and separately
approves one actual Codex run:

```bash
python3 tools/local_autodev_queue.py \
  --execute --acknowledge-credit-risk --max-issues 1
```

The hard limit is **one Issue per invocation**. The queue delegates to the
existing `tools/local_autodev.py --issue N --execute`, preserving login,
Git remote, issue-authorship, patch-guard, worktree, no-retry and Draft PR rules.
No automatic PR merge or next-Issue execution is performed. After publication,
the owner reviews the PR and the GitHub CI run before considering another batch.

`--execute` without `--acknowledge-credit-risk` is rejected; specifying more
than one Issue is rejected. Tests and dry-run mode never call Codex. Existing
ChatGPT credits may be consumed if the included usage allowance is exhausted:
auto-recharge OFF prevents automatic *purchase*, but is not a guarantee of
zero consumption of purchased credits. There is no trustworthy local
credit-balance API/limit enforced here.

No daemon, cron, systemd timer, unattended runner, auto-retry, API-key fallback,
or automated PR merge is enabled. Do **not** add the paid `autodev` label.
