#!/usr/bin/env bash
set -euo pipefail

# M2 is immutable historical evidence. M3 supersedes the working policy and
# schemas, so verify the reviewed M2 candidate rather than rewriting its files.
m2="a7183ae6c8f9bdf968fa87af9ec70251d99d49a8"
manifest="docs/evidence/m2-authority-manifest.json"

git merge-base --is-ancestor "$m2" HEAD
jq empty "$manifest"

historical_hash() {
  git show "$m2:$1" | sha256sum | cut -d' ' -f1
}

test "$(historical_hash fsassay-policy.lock.json)" = "$(jq -r '.hashes.policyFileSha256' "$manifest")"
test "$(historical_hash docs/contracts/fsassay-policy.schema.json)" = "$(jq -r '.hashes.policySchemaSha256' "$manifest")"
test "$(historical_hash docs/contracts/fsassay-authority-receipt.schema.json)" = "$(jq -r '.hashes.receiptSchemaSha256' "$manifest")"
test "$(historical_hash docs/contracts/AUTHORITY-CONTRACT-v1.md)" = "$(jq -r '.hashes.contractSha256' "$manifest")"

git show "$m2:fsassay-policy.lock.json" | jq -e '
  .policySchemaVersion == "fsassay-policy/1.0.0" and
  .evidenceSchemaVersion == "fsassay-authority-receipt/1.0.0" and
  .shapeContractVersion == "not-established" and
  (.approvedBlockingRules | length) == 0 and
  (.experimentalRules | length) == 93 and
  .requiredTests[0].minimumPassed == 85 and
  .baseline.identity == "none" and
  (.baseline.approvedFindings | length) == 0
' >/dev/null

echo "M2 historical authority evidence verified at $m2"
