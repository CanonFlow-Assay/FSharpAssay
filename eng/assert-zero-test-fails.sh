#!/usr/bin/env bash
set -euo pipefail

mkdir -p artifacts/test-results
log="artifacts/test-results/zero-test-negative.log"
set +e
dotnet test --project FsAssay.Tests/FsAssay.Tests.fsproj --configuration Release --no-build -- \
  --filter this-test-does-not-exist \
  --minimum-expected-tests 1 \
  --zero-tests-policy strict \
  --ansi off --progress off --output Normal >"$log" 2>&1
status=$?
set -e

cat "$log"
if [[ "$status" -eq 0 ]]; then
  echo "zero-test selection unexpectedly passed" >&2
  exit 1
fi
grep -Eq 'total:[[:space:]]+0' "$log"
echo "zero-test selection failed as required (exit $status)"
