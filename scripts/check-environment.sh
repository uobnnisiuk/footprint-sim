#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repository_root"

if ! command -v dotnet >/dev/null 2>&1 && [[ -x "$HOME/.dotnet/dotnet" ]]; then
  export DOTNET_ROOT="$HOME/.dotnet"
  export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH"
fi

echo "OS:"
if [[ -f /etc/os-release ]]; then
  grep -E '^(PRETTY_NAME|ID|VERSION_ID)=' /etc/os-release
else
  uname -s
fi

echo "Architecture: $(uname -m)"
echo "dotnet path: $(command -v dotnet || echo '<not found>')"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "No dotnet executable was found. Run: source scripts/setup-dotnet.sh" >&2
  exit 1
fi

echo ".NET SDKs:"
dotnet --list-sdks

if ! dotnet --list-sdks | grep -Eq '^8\.'; then
  echo "A .NET 8 SDK is required; a runtime alone is insufficient." >&2
  exit 1
fi

selected_version="$(dotnet --version)"
if [[ "$selected_version" != 8.* ]]; then
  echo "global.json did not select a .NET 8 SDK (selected: $selected_version)." >&2
  exit 1
fi

echo "Selected SDK: $selected_version"
dotnet --info
