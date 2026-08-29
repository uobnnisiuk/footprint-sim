#!/usr/bin/env bash
set -euo pipefail

dotnet_install_dir="${DOTNET_INSTALL_DIR:-$HOME/.dotnet}"

has_net8_sdk() {
  command -v dotnet >/dev/null 2>&1 && dotnet --list-sdks 2>/dev/null | grep -Eq '^8\.'
}

if ! has_net8_sdk && [[ -x "$dotnet_install_dir/dotnet" ]]; then
  export DOTNET_ROOT="$dotnet_install_dir"
  export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH"
fi

if ! has_net8_sdk; then
  installer_path="$(mktemp "${TMPDIR:-/tmp}/dotnet-install.XXXXXX.sh")"
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$installer_path"
  bash "$installer_path" \
    --channel 8.0 \
    --quality GA \
    --install-dir "$dotnet_install_dir"
  rm -f "$installer_path"

  export DOTNET_ROOT="$dotnet_install_dir"
  export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH"
fi

if ! has_net8_sdk; then
  echo "A .NET 8 SDK was not detected after setup." >&2
  return 1 2>/dev/null || exit 1
fi

echo "Using .NET SDK $(dotnet --version) from $(command -v dotnet)"
echo "DOTNET_ROOT=${DOTNET_ROOT:-<system installation>}"
