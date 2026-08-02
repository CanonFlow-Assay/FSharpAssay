#!/usr/bin/env bash
set -euo pipefail

mkdir -p artifacts/restore
log="$(pwd)/artifacts/restore/locked-negative.log"
tmp="$(mktemp -d)"
trap 'rm -rf -- "$tmp"' EXIT

mkdir -p "$tmp/FsAssay.Analyzers"
cp Directory.Build.props Directory.Packages.props global.json "$tmp/"
cp FsAssay.Analyzers/FsAssay.Analyzers.fsproj FsAssay.Analyzers/packages.lock.json "$tmp/FsAssay.Analyzers/"
sed -i 's/PackageVersion Include="FSharp.Core" Version="10.1.201"/PackageVersion Include="FSharp.Core" Version="10.1.202"/' "$tmp/Directory.Packages.props"

set +e
(cd "$tmp" && dotnet restore FsAssay.Analyzers/FsAssay.Analyzers.fsproj --locked-mode) >"$log" 2>&1
status=$?
set -e

cat "$log"
if [[ "$status" -eq 0 ]]; then
  echo "locked restore unexpectedly accepted package drift" >&2
  exit 1
fi
grep -q 'NU1004' "$log"
echo "locked restore rejected package drift as required (exit $status)"
