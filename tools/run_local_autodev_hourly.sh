#!/usr/bin/env bash
# Opt-in replacement action for the EXISTING Windows Daily task, not a second task.
set -euo pipefail
repo="/home/higuchi/personal/me/footprint-sim/footprint-sim-local-autodev-pr6"
cd "$repo"
exec python3 -B tools/local_autodev_hourly.py \
  --repo-path "$repo" --execute --acknowledge-credit-risk --max-issues 1
