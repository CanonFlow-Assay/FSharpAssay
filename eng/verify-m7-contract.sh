#!/usr/bin/env bash
set -Eeuo pipefail

candidate="${1:-}"
base="ccd17a1fa3fdb080f1420605b7682c740e9c2cfa"
base_tree="c0e6a5f54922276e2382c709ed205a0b7c8bd202"
workflow=".github/workflows/m7-release-preparation.yml"

if [[ ! "$candidate" =~ ^[0-9a-f]{40}$ ]]; then
  echo "usage: $0 <candidate-sha>" >&2
  exit 64
fi

test "$(git rev-parse HEAD)" = "$candidate"
test "$(git rev-parse "$base^{tree}")" = "$base_tree"
git merge-base --is-ancestor "$base" "$candidate"
test '<FsAssayBaselineVersion>1.0.4</FsAssayBaselineVersion>' = \
  "$(sed -n 's/^[[:space:]]*\(<FsAssayBaselineVersion>[^<]*<\/FsAssayBaselineVersion>\)[[:space:]]*$/\1/p' Directory.Build.props)"
grep -Fq '<PackageId>FsAssay.Cli</PackageId>' FsAssay.Runner/FsAssay.Runner.fsproj
grep -Fq '<Version>$(FsAssayBaselineVersion)</Version>' FsAssay.Runner/FsAssay.Runner.fsproj

test -z "$(git diff --name-only "$base" HEAD -- \
  ':(glob)**/*.fs' ':(glob)**/*.fsproj' ':(glob)**/*.props' ':(glob)**/*.targets' \
  global.json ':(glob)**/packages.lock.json' docs/contracts/fsassay-*.json)"

allowed_paths=$(printf '%s\n' \
  .github/workflows/m7-release-preparation.yml \
  docs/contracts/M7-RELEASE-GATE.md \
  docs/evidence/m7-release-preparation.md \
  eng/inventory-m7-external-state.sh \
  eng/qualify-m7-release.sh \
  eng/verify-m7-contract.sh)
actual_paths=$(git diff --name-only "$base" HEAD | LC_ALL=C sort)
test "$actual_paths" = "$(printf '%s\n' "$allowed_paths" | LC_ALL=C sort)"

test -x eng/inventory-m7-external-state.sh
test -x eng/qualify-m7-release.sh
test -x eng/verify-m7-contract.sh
grep -Fxq '# M7 release gate for FsAssay 1.0.4' docs/contracts/M7-RELEASE-GATE.md
grep -Fxq '# M7 release-preparation inventory' docs/evidence/m7-release-preparation.md
grep -Fq 'releaseEligible: false' docs/evidence/m7-release-preparation.md
grep -Fq 'Human Gate D is mandatory' docs/contracts/M7-RELEASE-GATE.md
grep -Fq 'does not prove universal nonexistence' eng/inventory-m7-external-state.sh

grep -Fq "branches: [ 'uplift/m7-release-preparation' ]" "$workflow"
grep -Fq 'pull_request:' "$workflow"
grep -Fq 'actions/attest-build-provenance@v2' "$workflow"
grep -Fq "if: github.event_name == 'push'" "$workflow"
grep -Fq 'gh attestation verify' "$workflow"
grep -Fq 'actions/upload-artifact@v4' "$workflow"

if grep -Eini \
  'workflow_dispatch|tags:|dotnet[[:space:]]+nuget[[:space:]]+push|gh[[:space:]]+release[[:space:]]+create|actions/(deploy|configure|upload)-pages|git[[:space:]]+tag' \
  "$workflow" eng/inventory-m7-external-state.sh eng/qualify-m7-release.sh; then
  echo "M7 contains an unauthorized external release-action surface" >&2
  exit 1
fi

test -z "$(git tag --points-at "$candidate")"
test -z "$(git status --porcelain --untracked-files=no)"
echo "M7 release-preparation contract verified"

