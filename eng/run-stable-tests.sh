#!/usr/bin/env bash
set -euo pipefail

mode="${1:-}"
case "$mode" in
  ordinary|direct) ;;
  *) echo "usage: $0 ordinary|direct" >&2; exit 64 ;;
esac

mkdir -p artifacts/test-results
log="artifacts/test-results/stable-${mode}.log"
common=(--minimum-expected-tests 54 --zero-tests-policy strict --ansi off --progress off --output Normal)

if [[ "$mode" == ordinary ]]; then
  dotnet test --project FsAssay.Tests/FsAssay.Tests.fsproj --configuration Release --no-build -- "${common[@]}" | tee "$log"
else
  dotnet run --project FsAssay.Tests/FsAssay.Tests.fsproj --configuration Release --no-build -- "${common[@]}" | tee "$log"
fi

grep -Eq 'total:[[:space:]]+54' "$log"
grep -Eq 'failed:[[:space:]]+0' "$log"
grep -Eq 'succeeded:[[:space:]]+54' "$log"
