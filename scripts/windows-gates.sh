#!/bin/bash
# the windows app's gates, runnable on the mac: format check, build (warnings are errors), tests.
# ci runs the same three steps on a windows runner (.github/workflows/windows.yml).
set -euo pipefail
cd "$(dirname "$0")/../windows"

# the sdk installs user-locally on the mac (~/.dotnet) and isn't on PATH there
DOTNET="$(command -v dotnet || echo "$HOME/.dotnet/dotnet")"

echo "==> dotnet format"
"$DOTNET" format Murmur.slnx --verify-no-changes

echo "==> build"
"$DOTNET" build Murmur.slnx -c Release -nologo -v q

echo "==> tests"
"$DOTNET" test --solution Murmur.slnx -c Release --no-build

echo "==> all windows gates passed"
