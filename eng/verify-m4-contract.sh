#!/usr/bin/env bash
set -euo pipefail

base="36c1b9264618344878cbf9dcca11363f5ea3d59b"

test "$(git rev-parse "$base^{tree}")" = "1a0a29a706779baa3a746ecea78d016decb135c1"
test "$(grep -c '^# M4 consumer and package qualification$' docs/evidence/m4-consumer-package-qualification.md)" -eq 1
grep -q 'common=(--minimum-expected-tests 93 ' eng/run-stable-tests.sh
grep -q '<PackageId>FsAssay.Cli</PackageId>' FsAssay.Runner/FsAssay.Runner.fsproj
grep -q '<Version>$(FsAssayBaselineVersion)</Version>' FsAssay.Runner/FsAssay.Runner.fsproj
grep -q '<FsAssayBaselineVersion>1.0.4</FsAssayBaselineVersion>' Directory.Build.props
grep -q 'zero blocking or advisory rules' FsAssay.Runner/FsAssay.Runner.fsproj
grep -q '<PackageReadmeFile>README.md</PackageReadmeFile>' FsAssay.Runner/FsAssay.Runner.fsproj
grep -q '<PackageLicenseFile>LICENSE</PackageLicenseFile>' FsAssay.Runner/FsAssay.Runner.fsproj
grep -q '<clear />' eng/m4-offline-nuget.config
grep -q 'actions/attest-build-provenance@v2' .github/workflows/ci.yml
grep -q 'attestations: write' .github/workflows/ci.yml
grep -q 'id-token: write' .github/workflows/ci.yml
grep -q 'FSASSAY_CANDIDATE_SHA: ${{ github.sha }}' .github/workflows/ci.yml
grep -q 'if: always()' .github/workflows/ci.yml
grep -q 'name: stable-qualification-${{ github.sha }}' .github/workflows/ci.yml
if grep -q 'pull_request.head.sha' .github/workflows/ci.yml; then
  echo "PR qualification must retain the synthetic merge checkout" >&2
  exit 1
fi
test "$(grep -c '563' .github/workflows/fsassay.yml)" -eq 0
test "$(grep -c '607' .github/workflows/fsassay.yml)" -eq 3
test -x eng/fsassay-authority-gate.sh
test -x eng/test-fsassay-authority-gate.sh
grep -q '^# FsAssay two-lane CI model$' docs/ci-cd/two-lane-model.md
grep -q 'This file is intentionally inactive' .github/examples/fsassay-two-lane.yml
grep -q 'if: always()' .github/examples/fsassay-two-lane.yml
test "$(grep -c 'continue-on-error: true' .github/examples/fsassay-two-lane.yml)" -eq 6
test "$(grep -c 'if: always()' .github/examples/fsassay-two-lane.yml)" -eq 3
grep -A1 'Delegate the merge decision only to the authority gate' .github/examples/fsassay-two-lane.yml | grep -q 'if: always()'
bash eng/test-fsassay-authority-gate.sh

jq -e '
  .catalogueCount == 93 and
  (.blocking | length) == 0 and (.advisory | length) == 0 and
  (.experimental | length) == 35 and (.prototype | length) == 36 and
  (.dummy | length) == 22 and (.deprecated | length) == 0 and (.removed | length) == 0
' docs/contracts/fsassay-rule-classification-v1.json >/dev/null

if git diff --name-only "$base" HEAD -- FsAssay.Analyzers | grep -E '\.fs$'; then
  echo "M4 changed analyzer rule source" >&2
  exit 1
fi
test -z "$(git diff --name-only "$base" HEAD -- FsAssay.Runner ':!FsAssay.Runner/Program.fs' ':!FsAssay.Runner/FsAssay.Runner.fsproj')"

bash eng/assert-required-locks.sh
echo "M4 consumer/package contract verified"
