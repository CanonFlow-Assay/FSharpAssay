#!/usr/bin/env bash
set -Eeuo pipefail

candidate="${1:-}"
output="${2:-artifacts/m7}"
base="ccd17a1fa3fdb080f1420605b7682c740e9c2cfa"
package_file="FsAssay.Cli.1.0.4.nupkg"
repository=$(git rev-parse --show-toplevel)
workspace=$(mktemp -d)
trap 'find "$workspace" -depth -delete 2>/dev/null || true' EXIT

if [[ ! "$candidate" =~ ^[0-9a-f]{40}$ ]]; then
  echo "usage: $0 <candidate-sha> [output-directory]" >&2
  exit 64
fi
if [[ "${FSASSAY_M7_EXTERNAL_ACTIONS:-deny}" != "deny" ]]; then
  echo "M7 external release actions must remain denied" >&2
  exit 1
fi

test "$(git rev-parse HEAD)" = "$candidate"
test -z "$(git status --porcelain --untracked-files=no)"
test "$(dotnet --version)" = "10.0.301"
bash eng/verify-m7-contract.sh "$candidate"

mkdir -p "$output/package"
output=$(cd "$output" && pwd)
printf '%s\n' \
  'release actions authorized: false' \
  'tag mutation: denied' \
  'package publication: denied' \
  'GitHub release mutation: denied' \
  'documentation deployment: denied' \
  'candidate-push provenance attestation: evidence only' \
  >"$output/no-external-action-assertion.txt"

bash eng/inventory-m7-external-state.sh "$candidate" "$output/external-state-before.json" \
  >"$output/external-state-before.log"

dotnet restore FsAssay.Stable.slnx --locked-mode
dotnet build FsAssay.Stable.slnx --no-restore --configuration Release
bash eng/run-stable-tests.sh ordinary
bash eng/run-stable-tests.sh direct
bash eng/assert-zero-test-fails.sh

bash eng/qualify-m4-package.sh "$candidate" "$output/package"

bash eng/inventory-m7-external-state.sh "$candidate" "$output/external-state-after.json" \
  >"$output/external-state-after.log"
cmp "$output/external-state-before.json" "$output/external-state-after.json"

m4_manifest="$output/package/package-manifest.json"
package="$output/package/$package_file"
test -f "$package"
jq -e --arg candidate "$candidate" '
  .candidateSha == $candidate and
  .package.id == "FsAssay.Cli" and .package.version == "1.0.4" and
  .package.repositoryCommit == $candidate and
  .package.nugetSignature == "unsigned-NU3004" and
  .stableTests.ordinary == {total:93,passed:93,failed:0} and
  .stableTests.direct == {total:93,passed:93,failed:0} and
  .reproducibility.normalizedPackagesByteIdentical == true and
  .consumer.rollbackRestoredManifest == true and
  .evidence.repeatedEvidenceByteIdentical == true
' "$m4_manifest" >/dev/null

package_hash=$(sha256sum "$package" | cut -d' ' -f1)
m4_manifest_hash=$(sha256sum "$m4_manifest" | cut -d' ' -f1)
external_state_hash=$(sha256sum "$output/external-state-after.json" | cut -d' ' -f1)
test "$package_hash" = "$(jq -r '.package.sha256' "$m4_manifest")"

jq -n -S \
  --arg base "$base" \
  --arg candidate "$candidate" \
  --arg packageHash "$package_hash" \
  --arg m4ManifestHash "$m4_manifest_hash" \
  --arg externalStateHash "$external_state_hash" \
  --slurpfile m4 "$m4_manifest" '
    {
      schema: "fsassay-m7-release-preparation/1",
      baseSha: $base,
      candidateSha: $candidate,
      releaseIdentity: {packageId: "FsAssay.Cli", version: "1.0.4"},
      preparationComplete: true,
      releaseEligible: false,
      authority: "human-gate-d-required",
      package: {
        file: $m4[0].package.file,
        sha256: $packageHash,
        bytes: $m4[0].package.bytes,
        repositoryCommit: $m4[0].package.repositoryCommit,
        nugetSignature: $m4[0].package.nugetSignature,
        githubAttestation: {
          statusInManifest: "pending-workflow-step",
          requirement: "create and verify on candidate push only"
        }
      },
      qualification: {
        stableTests: $m4[0].stableTests,
        reproducibility: $m4[0].reproducibility,
        consumer: $m4[0].consumer,
        evidence: $m4[0].evidence,
        adversarial: $m4[0].adversarial,
        m4ManifestSha256: $m4ManifestHash,
        externalStateObservationSha256: $externalStateHash
      },
      externalActionsPerformedByQualifier: {
        tagMutation: false,
        packagePublication: false,
        githubReleaseMutation: false,
        documentationDeployment: false
      },
      actionGates: {
        protectedTag: {status: "blocked", reason: "separate protection verification and Human Gate D required"},
        packagePublication: {status: "blocked", reason: "protected exact tag and separate Human Gate D required"},
        githubRelease: {status: "blocked", reason: "public package verification and separate Human Gate D required"},
        documentationDeployment: {status: "blocked", reason: "separate reviewed deployment and Human Gate D required"},
        humanGateD: {status: "blocked", reason: "not granted to M7 Draft preparation"}
      },
      adversarialReleasePreparation: {
        candidateMismatchRejected: true,
        packageHashMismatchRejected: true,
        failOpenGateStateRejected: true
      },
      limitations: [
        "local package is unsigned (NU3004)",
        "GitHub attestation is provenance evidence, not publication or a NuGet signature",
        "external endpoint observations do not prove universal nonexistence",
        "FsAssay test ingestion remains unavailable, so self-analysis can remain non-authoritative"
      ]
    }
  ' >"$output/release-preparation-manifest.json"

validate_release_manifest() {
  local expected_candidate="$1"
  local expected_package_hash="$2"
  local manifest="$3"
  jq -e --arg candidate "$expected_candidate" --arg packageHash "$expected_package_hash" '
    .candidateSha == $candidate and
    .package.repositoryCommit == $candidate and
    .package.sha256 == $packageHash and
    .preparationComplete == true and .releaseEligible == false and
    .authority == "human-gate-d-required" and
    .package.githubAttestation.statusInManifest == "pending-workflow-step" and
    ([.externalActionsPerformedByQualifier[]] | all(. == false)) and
    ([.actionGates[].status] | all(. == "blocked"))
  ' "$manifest" >/dev/null
}

release_manifest="$output/release-preparation-manifest.json"
validate_release_manifest "$candidate" "$package_hash" "$release_manifest"
if validate_release_manifest "$(printf '0%.0s' {1..40})" "$package_hash" "$release_manifest"; then
  echo "M7 candidate mismatch was not rejected" >&2
  exit 1
fi
if validate_release_manifest "$candidate" "$(printf '0%.0s' {1..64})" "$release_manifest"; then
  echo "M7 package hash mismatch was not rejected" >&2
  exit 1
fi
jq '.releaseEligible = true | .actionGates.packagePublication.status = "complete"' \
  "$release_manifest" >"$workspace/fail-open-manifest.json"
if validate_release_manifest "$candidate" "$package_hash" "$workspace/fail-open-manifest.json"; then
  echo "M7 fail-open release gate state was not rejected" >&2
  exit 1
fi

sha256sum \
  "$package" \
  "$m4_manifest" \
  "$output/external-state-after.json" \
  "$release_manifest" \
  >"$output/sha256sums.txt"

test -z "$(git -C "$repository" status --porcelain --untracked-files=no)"
echo "M7 release preparation passed for $candidate; every external action remains blocked"
