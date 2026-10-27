#!/usr/bin/env bash
# Invoked by Windows Task Scheduler as WSL Ubuntu user higuchi.
set -euo pipefail
repo="/home/higuchi/personal/me/footprint-sim/footprint-sim-local-autodev-pr6"
cd "$repo"
exec python3 tools/local_autodev_daily.py \
  --repo-path "$repo" \
  --execute \
  --acknowledge-credit-risk \
  --max-issues 1
