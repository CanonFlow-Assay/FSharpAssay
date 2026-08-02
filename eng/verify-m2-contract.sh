#!/usr/bin/env bash
set -euo pipefail

base="13e2314ec8676aaf224440d6a46d3196ac84d2ef"
policy="fsassay-policy.lock.json"
manifest="docs/evidence/m2-authority-manifest.json"

git merge-base --is-ancestor "$base" HEAD
test -z "$(git diff --name-only "$base" HEAD -- FsAssay.Analyzers)"

jq empty "$policy" \
  docs/contracts/fsassay-policy.schema.json \
  docs/contracts/fsassay-authority-receipt.schema.json \
  "$manifest"

for definition in candidate policyIdentity requiredTestPolicy toolchain reason counts project source test rule finding exception; do
  jq -e --arg definition "$definition" '."$defs"[$definition].type == "object" and ."$defs"[$definition].additionalProperties == false' \
    docs/contracts/fsassay-authority-receipt.schema.json >/dev/null
done

test "$(jq -r '.policySchemaVersion' "$policy")" = "fsassay-policy/1.0.0"
test "$(jq -r '.evidenceSchemaVersion' "$policy")" = "fsassay-authority-receipt/1.0.0"
test "$(jq -r '.authorityContractVersion' "$policy")" = "authority-contract/1.0.0"
test "$(jq -r '.shapeContractVersion' "$policy")" = "not-established"
test "$(jq '.approvedBlockingRules | length' "$policy")" -eq 0
test "$(jq '.advisoryRules | length' "$policy")" -eq 0
test "$(jq '.experimentalRules | length' "$policy")" -eq 93
test "$(jq '([.advisoryRules[], .experimentalRules[]] | unique | length)' "$policy")" -eq 93
test "$(jq '.requiredTests | length' "$policy")" -eq 1
test "$(jq -r '.requiredTests[0].minimumPassed' "$policy")" -eq 84
test "$(jq -r '.baseline.identity' "$policy")" = "none"
test "$(jq '.baseline.approvedFindings | length' "$policy")" -eq 0

test "$(sha256sum "$policy" | cut -d' ' -f1)" = "$(jq -r '.hashes.policyFileSha256' "$manifest")"
test "$(sha256sum docs/contracts/fsassay-policy.schema.json | cut -d' ' -f1)" = "$(jq -r '.hashes.policySchemaSha256' "$manifest")"
test "$(sha256sum docs/contracts/fsassay-authority-receipt.schema.json | cut -d' ' -f1)" = "$(jq -r '.hashes.receiptSchemaSha256' "$manifest")"
test "$(sha256sum docs/contracts/AUTHORITY-CONTRACT-v1.md | cut -d' ' -f1)" = "$(jq -r '.hashes.contractSha256' "$manifest")"

grep -q '| 1 | `ToolFailure` |' docs/contracts/AUTHORITY-CONTRACT-v1.md
grep -q '| 2 | `Fail` |' docs/contracts/AUTHORITY-CONTRACT-v1.md
grep -q '| 3 | `Inconclusive` |' docs/contracts/AUTHORITY-CONTRACT-v1.md
grep -q '| 4 | `Pass` |' docs/contracts/AUTHORITY-CONTRACT-v1.md
grep -q 'Gate C approval; none are approved in M2' FsAssay.Runner/Authority.fs
grep -q 'receipt reason set does not reconcile with itemized evidence' FsAssay.Runner/Authority.fs
grep -q 'M2 cannot claim applied suppressions' FsAssay.Runner/Authority.fs
grep -q 'common=(--minimum-expected-tests 84 ' eng/run-stable-tests.sh
! grep -q 'pull_request.merge_commit_sha' .github/workflows/fsassay.yml
grep -q 'FSASSAY_APPROVED_HEAD_SHA:' .github/workflows/fsassay.yml
grep -q '<FsAssayBaselineVersion>1.0.4</FsAssayBaselineVersion>' Directory.Build.props

echo "M2 authority contract verified"
