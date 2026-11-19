#!/usr/bin/env bash
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"
test "$(dotnet --version)" = 11.0.100
output="${1:-artifacts/refresh/qualified-$(date -u +%Y%m%dT%H%M%SZ)}"
dotnet restore FsAssay.Stable.slnx --locked-mode
dotnet build FsAssay.Stable.slnx --no-restore -c Release
dotnet fsi --warnaserror+ --exec eng/qualify-refresh.fsx "$output"
