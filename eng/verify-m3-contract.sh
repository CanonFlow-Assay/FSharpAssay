#!/usr/bin/env bash
set -euo pipefail

base="8da5c3305489d0ac4d07339c400b5fdd7ebed1b1"
m4_base="36c1b9264618344878cbf9dcca11363f5ea3d59b"
policy="fsassay-policy.lock.json"
classification="docs/contracts/fsassay-rule-classification-v1.json"
shape="docs/contracts/fsassay-shape-v1.json"
manifest="docs/evidence/m3-shape-rule-admission-manifest.json"

git merge-base --is-ancestor "$base" HEAD
test -z "$(git diff --name-only "$base" HEAD -- FsAssay.Analyzers | grep -E '\.fs$' || true)"
test -z "$(git diff --name-only "$m4_base" HEAD -- FsAssay.Analyzers \
  ':!FsAssay.Analyzers/FsAssay.Analyzers.fsproj')"
analyzer_project="FsAssay.Analyzers/FsAssay.Analyzers.fsproj"
test "$(git diff --numstat "$m4_base" HEAD -- "$analyzer_project")" = \
  $'3\t0\tFsAssay.Analyzers/FsAssay.Analyzers.fsproj'
grep -Fxq '    <ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>' "$analyzer_project"
grep -Fxq '    <PathMap>$(MSBuildProjectDirectory)=/_/FsAssay.Analyzers</PathMap>' "$analyzer_project"
grep -Fxq '    <EmbedUntrackedSources>false</EmbedUntrackedSources>' "$analyzer_project"

jq empty "$policy" "$classification" "$shape" "$manifest" \
  docs/contracts/fsassay-policy.schema.json \
  docs/contracts/fsassay-authority-receipt.schema.json

jq -e '
  .policySchemaVersion == "fsassay-policy/1.1.0" and
  .evidenceSchemaVersion == "fsassay-authority-receipt/1.1.0" and
  .authorityContractVersion == "authority-contract/1.0.0" and
  .shapeContractVersion == "fsharp-shape/1.0.0" and
  .evaluationDate == "2026-08-03" and
  (.approvedBlockingRules | length) == 0 and
  (.advisoryRules | length) == 0 and
  (.experimentalRules | length) == 35 and
  (.prototypeRules | length) == 36 and
  (.dummyRules | length) == 22 and
  (.deprecatedRules | length) == 0 and
  (.removedRules | length) == 0 and
  ([.approvedBlockingRules[], .advisoryRules[], .experimentalRules[], .prototypeRules[], .dummyRules[], .deprecatedRules[], .removedRules[]] | length) == 93 and
  ([.approvedBlockingRules[], .advisoryRules[], .experimentalRules[], .prototypeRules[], .dummyRules[], .deprecatedRules[], .removedRules[]] | unique | length) == 93 and
  .requiredTests[0].minimumPassed == 92 and
  .baseline == {"identity":"none","reviewedBy":"","reviewedOn":"","records":[]} and
  (.exceptions | length) == 0
' "$policy" >/dev/null

jq -e '
  .catalogueCount == 93 and .humanGate == "C" and
  (.blocking | length) == 0 and (.advisory | length) == 0 and
  (.experimental | length) == 35 and (.prototype | length) == 36 and
  (.dummy | length) == 22 and (.deprecated | length) == 0 and (.removed | length) == 0 and
  ([.blocking[], .advisory[], .experimental[], .prototype[], .dummy[], .deprecated[], .removed[]] | unique | length) == 93
' "$classification" >/dev/null

jq -e '
  .contractVersion == "fsharp-shape/1.0.0" and
  (.newClauses | length) == 9 and (.convergeClauses | length) == 10 and
  .authorityLaws.missingEvidenceCanPass == false and
  .authorityLaws.exceptionsSuppressAuthorityEvidence == false and
  .authorityLaws.llmJudgmentIsAuthoritative == false and
  .authorityLaws.expiryComparison == "evaluationDate <= expiresOn"
' "$shape" >/dev/null

grep -q 'Implemented means executable, not admitted' docs/contracts/M3-SHAPE-RULE-ADMISSION.md
grep -q 'wall-clock time is not consulted' docs/contracts/SHAPE-CONVERGE-v1.md
grep -q 'new-blocking-finding' FsAssay.Runner/Authority.fs
grep -q 'reappearing-blocking-finding' FsAssay.Runner/Authority.fs
grep -q 'common=(--minimum-expected-tests 93 ' eng/run-stable-tests.sh

check_hash() {
  local path="$1"
  local key="$2"
  test "$(sha256sum "$path" | cut -d' ' -f1)" = "$(jq -r --arg key "$key" '.hashes[$key]' "$manifest")"
}

check_hash "$policy" policyFileSha256
check_hash docs/contracts/fsassay-policy.schema.json policySchemaSha256
check_hash docs/contracts/fsassay-authority-receipt.schema.json receiptSchemaSha256
check_hash "$shape" shapeMachineContractSha256
check_hash docs/contracts/SHAPE-NEW-v1.md shapeNewSha256
check_hash docs/contracts/SHAPE-CONVERGE-v1.md shapeConvergeSha256
check_hash "$classification" ruleClassificationSha256
check_hash docs/contracts/M3-SHAPE-RULE-ADMISSION.md admissionContractSha256
check_hash docs/evidence/m3-shape-rule-admission-inventory.md inventorySha256
check_hash FsAssay.Runner/Authority.fs authorityImplementationSha256
check_hash FsAssay.Runner/Program.fs authorityProducerSha256

jq -e '
  .authorizedBaseCommit == "8da5c3305489d0ac4d07339c400b5fdd7ebed1b1" and
  .gate == "Human Gate C pending; zero-admission classification proposed" and
  .stableTests.expected == 92 and .stableTests.m3Added == 7 and
  .repositoryAudit.expectedOutcome == "Inconclusive" and
  .repositoryAudit.expectedAuthoritative == false and
  .repositoryAudit.findings == 563 and
  .repositoryAudit.determinismScope.json == {"sameRootRepeat":true,"crossRoot":true} and
  .repositoryAudit.determinismScope.sarif == {"sameRootRepeat":true,"crossRoot":true} and
  .repositoryAudit.determinismScope.toolchain == {"sameRootRepeat":true,"crossRoot":true} and
  .repositoryAudit.determinismScope.rateCardMarkdown.sameRootRepeat == true and
  .repositoryAudit.determinismScope.rateCardMarkdown.crossRoot == false and
  .repositoryAudit.determinismScope.dashboardHtml.sameRootRepeat == true and
  .repositoryAudit.determinismScope.dashboardHtml.crossRoot == false and
  .analyzerRulesChanged == false and .packagePublished == false and
  .releaseCreated == false and .pullRequestMerged == false
' "$manifest" >/dev/null

echo "M3 Shape and rule admission contract verified"
