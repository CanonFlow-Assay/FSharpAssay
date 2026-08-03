#!/usr/bin/env bash
set -Eeuo pipefail

candidate="${1:-}"
output="${2:-artifacts/m5}"
repository=$(git rev-parse --show-toplevel)
package_id="FsAssay.Cli"
package_version="1.0.4"
package_file="$package_id.$package_version.nupkg"
workspace=$(mktemp -d)
trap 'find "$workspace" -depth -delete 2>/dev/null || true' EXIT

if [[ ! "$candidate" =~ ^[0-9a-f]{40}$ ]]; then
  echo "usage: $0 <candidate-sha> [output-directory]" >&2
  exit 64
fi
test "$(git rev-parse HEAD)" = "$candidate"
test -z "$(git status --porcelain)"
mkdir -p "$output/feed" "$output/tools"
output=$(cd "$output" && pwd)

dotnet restore FsAssay.Stable.slnx --locked-mode
dotnet build FsAssay.Stable.slnx --no-restore --configuration Release
dotnet pack FsAssay.Runner/FsAssay.Runner.fsproj --no-build --no-restore \
  --configuration Release --output "$output/feed" -p:ContinuousIntegrationBuild=true
test -f "$output/feed/$package_file"
unzip -p "$output/feed/$package_file" '*.nuspec' | grep -Fq "commit=\"$candidate\""
package_hash=$(sha256sum "$output/feed/$package_file" | cut -d' ' -f1)

FSASSAY_M5_LOCAL_FEED="$output/feed" dotnet tool install "$package_id" \
  --version "$package_version" --tool-path "$output/tools" \
  --configfile eng/m5-offline-nuget.config >"$output/tool-install.log"

dotnet restore playground/m5/FsAssay.Playground.M5.slnx --locked-mode
dotnet build playground/m5/FsAssay.Playground.M5.slnx --no-restore --configuration Release
dotnet test playground/m5/FsAssay.Playground.M5.slnx --no-build --configuration Release \
  >"$output/tests.log" 2>"$output/tests.stderr"
grep -Eq 'total:[[:space:]]+15' "$output/tests.log"
grep -Eq 'failed:[[:space:]]+0' "$output/tests.log"
grep -Eq 'succeeded:[[:space:]]+15' "$output/tests.log"

run_analysis() {
  local name="$1"
  set +e
  FSASSAY_PACKAGE_SHA256="$package_hash" "$output/tools/fsassay" \
    --out-json "$output/$name.json" --out-sarif "$output/$name.sarif" \
    playground/m5/FsAssay.Playground.M5.slnx \
    >"$output/$name.stdout" 2>"$output/$name.stderr"
  local status=$?
  set -e
  test "$status" -eq 2
}

run_analysis first
run_analysis second
cmp "$output/first.json" "$output/second.json"
cmp "$output/first.sarif" "$output/second.sarif"

jq -e --arg candidate "$candidate" --arg packageHash "$package_hash" '
  .candidate.kind == "package" and
  .candidate.commitSha == $candidate and
  .candidate.packageSha256 == $packageHash and
  .outcome == "Inconclusive" and .authoritative == false and
  .counts.projectsDiscovered == 4 and .counts.projectsLoaded == 4 and
  .counts.projectsFailed == 0 and .counts.projectsUnsupported == 0 and
  (.toolFailures | length) == 0 and
  ([.tests[] | select(.id == "m5-shape-playground" and .status == "notRun")] | length) == 1 and
  ([.reasons[].code] | index("required-test-not-run")) != null
' "$output/first.json" >/dev/null

jq -S '[.findings[] | {ruleId,path,fingerprint}] | sort_by(.ruleId,.path,.fingerprint)' \
  "$output/first.json" >"$workspace/actual.json"
jq -S '[.expected[] | {ruleId,path,fingerprint}] | sort_by(.ruleId,.path,.fingerprint)' \
  playground/m5/expected-findings.json >"$workspace/expected.json"
cmp "$workspace/expected.json" "$workspace/actual.json"

json_hash=$(sha256sum "$output/first.json" | cut -d' ' -f1)
sarif_hash=$(sha256sum "$output/first.sarif" | cut -d' ' -f1)
jq -n -S --arg candidate "$candidate" --arg packageHash "$package_hash" \
  --arg jsonHash "$json_hash" --arg sarifHash "$sarif_hash" '
  {
    schema:"fsassay-m5-playground-evidence/1",
    candidateSha:$candidate,
    package:{id:"FsAssay.Cli",version:"1.0.4",publicNuGet:false,sha256:$packageHash},
    tests:{total:15,passed:15,failed:0},
    receipt:{outcome:"Inconclusive",authoritative:false,findings:19,toolFailures:0,
      jsonSha256:$jsonHash,sarifSha256:$sarifHash,repeatedArtifactsByteIdentical:true},
    limitation:"required tests are executed separately but FsAssay 1.0.4 cannot ingest that result"
  }' >"$output/evidence-manifest.json"

echo "M5 Playground qualification passed for $candidate"
