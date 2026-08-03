#!/usr/bin/env bash
set -Eeuo pipefail

candidate="${1:-}"
output="${2:-artifacts/m6}"
package_id="FsAssay.Cli"
package_version="1.0.4"
package_file="$package_id.$package_version.nupkg"
repository=$(git rev-parse --show-toplevel)
workspace=$(mktemp -d)
trap 'find "$workspace" -depth -delete 2>/dev/null || true' EXIT

if [[ ! "$candidate" =~ ^[0-9a-f]{40}$ ]]; then
  echo "usage: $0 <candidate-sha> [output-directory]" >&2
  exit 64
fi
test "$(git rev-parse HEAD)" = "$candidate"
test -z "$(git status --porcelain)"
test "$(dotnet --version)" = "10.0.301"

mkdir -p "$output/feed" "$output/tool" "$output/analysis"
output=$(cd "$output" && pwd)
python3 eng/verify-m6-docs.py "$repository" >"$output/static-validation.log"

dotnet restore FsAssay.Stable.slnx --locked-mode
dotnet build FsAssay.Stable.slnx --no-restore --configuration Release
dotnet pack FsAssay.Runner/FsAssay.Runner.fsproj --no-build --no-restore \
  --configuration Release --output "$output/feed" -p:ContinuousIntegrationBuild=true
test -f "$output/feed/$package_file"
unzip -p "$output/feed/$package_file" '*.nuspec' | grep -Fq "commit=\"$candidate\""
package_hash=$(sha256sum "$output/feed/$package_file" | cut -d' ' -f1)

consumer="$workspace/consumer"
mkdir -p "$consumer"
dotnet new tool-manifest --output "$consumer" >"$output/tool-manifest.log"
(
  cd "$consumer"
  dotnet tool install "$package_id" --version "$package_version" --source "$output/feed" \
    >"$output/tool-install.log"
  dotnet tool restore >"$output/tool-restore.log"
  dotnet tool run fsassay -- help >"$output/help.stdout" 2>"$output/help.stderr"
  dotnet tool run fsassay -- --help >"$output/help-long.stdout" 2>"$output/help-long.stderr"
  dotnet tool run fsassay -- -h >"$output/help-short.stdout" 2>"$output/help-short.stderr"
  dotnet tool run fsassay -- doctor >"$output/doctor.stdout" 2>"$output/doctor.stderr"
  dotnet tool run fsassay -- explain FSA-C02 >"$output/explain.stdout" 2>"$output/explain.stderr"
)

cmp "$output/help.stdout" "$output/help-long.stdout"
cmp "$output/help.stdout" "$output/help-short.stdout"
test ! -s "$output/help.stderr"
test ! -s "$output/help-long.stderr"
test ! -s "$output/help-short.stderr"
test ! -s "$output/doctor.stderr"
test ! -s "$output/explain.stderr"
test "$(grep -c '^USAGE: fsassay' "$output/help.stdout")" -eq 1
grep -q '^    help ' "$output/help.stdout"
grep -q '^    doctor ' "$output/help.stdout"
grep -q '^    explain <RULE>' "$output/help.stdout"
grep -q -- '--out-json' "$output/help.stdout"
grep -q -- '--out-sarif' "$output/help.stdout"
grep -q -- '--docs' "$output/help.stdout"
test -z "$(grep -E '^    (catalog|check|verify) ' "$output/help.stdout" || true)"
grep -q '^Status: healthy$' "$output/doctor.stdout"
grep -q '^AnalysisNetworkDefault: offline$' "$output/doctor.stdout"
grep -q '^M3AdmissionClass: experimental$' "$output/explain.stdout"
grep -q '^Authority: non-authoritative;' "$output/explain.stdout"

set +e
(
  cd "$consumer"
  FSASSAY_PACKAGE_SHA256="$package_hash" dotnet tool run fsassay -- \
    --out-json "$output/analysis/result.json" \
    --out-sarif "$output/analysis/result.sarif" \
    "$repository/playground/m5/FsAssay.Playground.M5.slnx"
) >"$output/analysis.stdout" 2>"$output/analysis.stderr"
analysis_exit=$?
set -e
test "$analysis_exit" -eq 2
jq -e --arg candidate "$candidate" --arg packageHash "$package_hash" '
  .candidate.kind == "package" and
  .candidate.commitSha == $candidate and
  .candidate.packageSha256 == $packageHash and
  .outcome == "Inconclusive" and .authoritative == false and
  (.toolFailures | length) == 0
' "$output/analysis/result.json" >/dev/null

(
  cd "$consumer"
  dotnet tool uninstall "$package_id" >"$output/tool-uninstall.log"
)
if test -f "$consumer/.config/dotnet-tools.json"; then
  test "$(jq '.tools | length' "$consumer/.config/dotnet-tools.json")" -eq 0
else
  test ! -e "$consumer/.config/dotnet-tools.json"
fi

jq -n -S --arg candidate "$candidate" --arg packageHash "$package_hash" \
  --arg jsonHash "$(sha256sum "$output/analysis/result.json" | cut -d' ' -f1)" \
  --arg sarifHash "$(sha256sum "$output/analysis/result.sarif" | cut -d' ' -f1)" '
  {
    schema:"fsassay-m6-documentation-qualification/1",
    candidateSha:$candidate,
    package:{id:"FsAssay.Cli",version:"1.0.4",publicNuGet:false,sha256:$packageHash},
    docs:{staticLinksAndClaims:true,homepageDeployment:false},
    quickStart:{manifest:true,localFeedInstall:true,restore:true,help:true,doctor:true,explain:true,analysis:true,rollback:true},
    evidence:{analysisExitCode:2,outcome:"Inconclusive",authoritative:false,jsonSha256:$jsonHash,sarifSha256:$sarifHash}
  }' >"$output/manifest.json"

echo "M6 documentation qualification passed for $candidate"
