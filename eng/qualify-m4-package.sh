#!/usr/bin/env bash
set -euo pipefail

candidate="${1:-}"
output="${2:-artifacts/m4}"
package_id="FsAssay.Cli"
package_version="1.0.4"
package_file="${package_id}.${package_version}.nupkg"
dotnet_executable=$(command -v dotnet)
dotnet_root="${DOTNET_ROOT:-$(dirname "$(readlink -f "$dotnet_executable")")}"

if [[ ! "$candidate" =~ ^[0-9a-f]{40}$ ]]; then
  echo "usage: $0 <candidate-sha> [output-directory]" >&2
  exit 64
fi

repository=$(git rev-parse --show-toplevel)
actual_head=$(git -C "$repository" rev-parse HEAD)
if [[ "$actual_head" != "$candidate" ]]; then
  echo "candidate mismatch: expected $candidate, found $actual_head" >&2
  exit 1
fi
if [[ -n "$(git -C "$repository" status --porcelain --untracked-files=no)" ]]; then
  echo "tracked candidate worktree is not clean" >&2
  exit 1
fi
for test_log in artifacts/test-results/stable-ordinary.log artifacts/test-results/stable-direct.log; do
  if [[ ! -f "$repository/$test_log" ]]; then
    echo "required stable test evidence is missing: $test_log" >&2
    exit 1
  fi
  grep -Eq 'total:[[:space:]]+93' "$repository/$test_log"
  grep -Eq 'failed:[[:space:]]+0' "$repository/$test_log"
  grep -Eq 'succeeded:[[:space:]]+93' "$repository/$test_log"
done

mkdir -p "$output"
output=$(cd "$output" && pwd)
workspace=$(mktemp -d)

build_package_root() {
  local root="$1"
  local cache="$2"
  local log="$3"
  local raw="$root/artifacts/m4-raw/$package_file"
  local normalized="$root/artifacts/m4-package/$package_file"

  git clone --quiet --no-local "$repository" "$root"
  git -C "$root" fetch --quiet "$repository" HEAD
  test "$(git -C "$root" rev-parse FETCH_HEAD)" = "$candidate"
  git -C "$root" checkout --quiet FETCH_HEAD
  git -C "$root" remote set-url origin https://github.com/CanonFlow-Assay/FSharpAssay.git
  test "$(git -C "$root" rev-parse HEAD)" = "$candidate"

  (
    cd "$root"
    NUGET_PACKAGES="$cache" dotnet restore FsAssay.Stable.slnx --locked-mode
    NUGET_PACKAGES="$cache" dotnet build FsAssay.Stable.slnx --configuration Release --no-restore
    mkdir -p artifacts/m4-raw artifacts/m4-package
    NUGET_PACKAGES="$cache" dotnet pack FsAssay.Runner/FsAssay.Runner.fsproj \
      --configuration Release --no-build --no-restore --output artifacts/m4-raw
    dotnet fsi eng/normalize-nupkg.fsx "$raw" "$normalized"
  ) >"$log" 2>&1
}

root_a="$workspace/root-a"
root_b="$workspace/root-b"
build_package_root "$root_a" "$workspace/cache-a" "$workspace/root-a.log" &
pid_a=$!
build_package_root "$root_b" "$workspace/cache-b" "$workspace/root-b.log" &
pid_b=$!

set +e
wait "$pid_a"
root_a_exit=$?
wait "$pid_b"
root_b_exit=$?
set -e
cp "$workspace/root-a.log" "$output/root-a-build.log"
cp "$workspace/root-b.log" "$output/root-b-build.log"
if [[ "$root_a_exit" -ne 0 || "$root_b_exit" -ne 0 ]]; then
  echo "independent package build failed: root-a=$root_a_exit root-b=$root_b_exit" >&2
  exit 1
fi

raw_a="$root_a/artifacts/m4-raw/$package_file"
raw_b="$root_b/artifacts/m4-raw/$package_file"
package_a="$root_a/artifacts/m4-package/$package_file"
package_b="$root_b/artifacts/m4-package/$package_file"

cmp "$package_a" "$package_b"
package_hash=$(sha256sum "$package_a" | cut -d' ' -f1)
package_bytes=$(stat -c '%s' "$package_a")
cp "$package_a" "$output/$package_file"
unzip -t "$output/$package_file" >"$output/package-integrity.log"

validate_hash() {
  local expected="$1"
  local file="$2"
  test "$(sha256sum "$file" | cut -d' ' -f1)" = "$expected"
}

validate_provenance() {
  local expected="$1"
  local file="$2"
  local nuspec
  nuspec=$(unzip -p "$file" '*.nuspec')
  grep -Fq '<id>FsAssay.Cli</id>' <<<"$nuspec"
  grep -Fq '<version>1.0.4</version>' <<<"$nuspec"
  grep -Fq 'repository type="git" url="https://github.com/CanonFlow-Assay/FSharpAssay.git"' <<<"$nuspec"
  grep -Fq "commit=\"$expected\"" <<<"$nuspec"
}

validate_hash "$package_hash" "$output/$package_file"
validate_provenance "$candidate" "$output/$package_file"
unzip -Z1 "$output/$package_file" >"$output/package-entries.txt"
grep -Fxq 'README.md' "$output/package-entries.txt"
grep -Fxq 'LICENSE' "$output/package-entries.txt"
dotnet fsi "$root_a/eng/extract-sourcelink.fsx" "$output/$package_file" \
  'tools/net10.0/any/FsAssay.Runner.pdb' >"$output/sourcelink.json"
dotnet fsi "$root_a/eng/extract-sourcelink.fsx" "$output/$package_file" \
  'tools/net10.0/any/FsAssay.Analyzers.pdb' >"$output/analyzers-sourcelink.json"
cmp "$output/sourcelink.json" "$output/analyzers-sourcelink.json"
jq -e --arg candidate "$candidate" '
  .documents
  | to_entries
  | length > 0 and all(
      (.key | startswith("/_/")) and
      (.value | startswith("https://raw.githubusercontent.com/CanonFlow-Assay/FSharpAssay/" + $candidate + "/")))
' "$output/sourcelink.json" >/dev/null
if grep -aFq "$root_a" "$output/$package_file" || grep -aFq "$root_b" "$output/$package_file"; then
  echo "package leaked an independent checkout path" >&2
  exit 1
fi

if validate_hash "$(printf '0%.0s' {1..64})" "$output/$package_file"; then
  echo "hash mismatch was not rejected" >&2
  exit 1
fi
if validate_provenance "$(printf '0%.0s' {1..40})" "$output/$package_file"; then
  echo "provenance mismatch was not rejected" >&2
  exit 1
fi

set +e
dotnet nuget verify --all "$output/$package_file" >"$output/nuget-signature.log" 2>&1
signature_exit=$?
set -e
test "$signature_exit" -eq 1
grep -q 'NU3004: The package is not signed' "$output/nuget-signature.log"

incomplete_feed="$workspace/incomplete-feed"
mkdir -p "$incomplete_feed"
cp "$output/$package_file" "$incomplete_feed/$package_file"
truncate -s 512 "$incomplete_feed/$package_file"
set +e
unzip -t "$incomplete_feed/$package_file" >"$output/incomplete-package.log" 2>&1
incomplete_exit=$?
set -e
test "$incomplete_exit" -ne 0

consumer="$workspace/consumer"
consumer_cache="$workspace/consumer-cache"
mkdir -p "$consumer/repository"
(
  cd "$consumer/repository"
  unshare -n env DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ROOT="$dotnet_root" NUGET_PACKAGES="$consumer_cache" \
    dotnet new tool-manifest --force >"$output/tool-manifest-create.log" 2>&1
)
manifest_before=$(sha256sum "$consumer/repository/dotnet-tools.json" | cut -d' ' -f1)
(
  cd "$consumer/repository"
  unshare -n env DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ROOT="$dotnet_root" NUGET_PACKAGES="$consumer_cache" \
    FSASSAY_M4_LOCAL_FEED="$output" dotnet tool install "$package_id" --version "$package_version" \
      --configfile "$root_a/eng/m4-offline-nuget.config" \
      >"$output/tool-install.log" 2>&1
)

run_tool() {
  (
    cd "$consumer/repository"
    unshare -n env DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ROOT="$dotnet_root" NUGET_PACKAGES="$consumer_cache" \
      dotnet tool run fsassay -- "$@"
  )
}

run_tool help >"$output/help.stdout" 2>"$output/help.stderr"
run_tool --help >"$output/help-long.stdout" 2>"$output/help-long.stderr"
run_tool -h >"$output/help-short.stdout" 2>"$output/help-short.stderr"
cmp "$output/help.stdout" "$output/help-long.stdout"
cmp "$output/help.stdout" "$output/help-short.stdout"
test ! -s "$output/help.stderr"
test ! -s "$output/help-long.stderr"
test ! -s "$output/help-short.stderr"
test "$(grep -c '^USAGE: fsassay' "$output/help.stdout")" -eq 1

run_tool doctor >"$output/doctor.stdout" 2>"$output/doctor.stderr"
test ! -s "$output/doctor.stderr"
grep -q '^Status: healthy$' "$output/doctor.stdout"
grep -q '^AnalysisNetworkDefault: offline$' "$output/doctor.stdout"
run_tool explain FSA-C02 >"$output/explain.stdout" 2>"$output/explain.stderr"
test ! -s "$output/explain.stderr"
grep -q '^M3AdmissionClass: experimental$' "$output/explain.stdout"
grep -q '^Authority: non-authoritative;' "$output/explain.stdout"

set +e
run_tool explain UNKNOWN0000 >"$output/explain-unknown.stdout" 2>"$output/explain-unknown.stderr"
unknown_rule_exit=$?
run_tool --definitely-unknown >"$output/invalid.stdout" 2>"$output/invalid.stderr"
invalid_exit=$?
set -e
test "$unknown_rule_exit" -eq 64
test "$invalid_exit" -eq 64
test ! -s "$output/explain-unknown.stdout"
test ! -s "$output/invalid.stdout"

source_file="$root_a/FsAssay.Runner/Domain.fs"
set +e
(
  cd "$consumer/repository"
  unshare -n env DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ROOT="$dotnet_root" NUGET_PACKAGES="$consumer_cache" \
    FSASSAY_PACKAGE_SHA256="$package_hash" dotnet tool run fsassay -- \
      --files "$source_file" --out-json "$output/analysis-first.json" --out-sarif "$output/analysis-first.sarif" "$source_file"
) >"$output/analysis-first.stdout" 2>"$output/analysis-first.stderr"
analysis_first_exit=$?
(
  cd "$consumer/repository"
  unshare -n env DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ROOT="$dotnet_root" NUGET_PACKAGES="$consumer_cache" \
    FSASSAY_PACKAGE_SHA256="$package_hash" dotnet tool run fsassay -- \
      --files "$source_file" --out-json "$output/analysis-second.json" --out-sarif "$output/analysis-second.sarif" "$source_file"
) >"$output/analysis-second.stdout" 2>"$output/analysis-second.stderr"
analysis_second_exit=$?
set -e
test "$analysis_first_exit" -eq 2
test "$analysis_second_exit" -eq 2
cmp "$output/analysis-first.json" "$output/analysis-second.json"
cmp "$output/analysis-first.sarif" "$output/analysis-second.sarif"
jq -e --arg hash "$package_hash" '
  .candidate.kind == "package" and .candidate.packageSha256 == $hash and
  .outcome == "Inconclusive" and .authoritative == false
' "$output/analysis-first.json" >/dev/null

empty_target="$consumer/empty-target"
mkdir -p "$empty_target"
set +e
run_tool "$empty_target" >"$output/zero-evidence.stdout" 2>"$output/zero-evidence.stderr"
zero_evidence_exit=$?
set -e
test "$zero_evidence_exit" -eq 2
grep -q '^Authoritative: false$' "$output/zero-evidence.stdout"

trace="$output/network.trace"
set +e
(
  cd "$consumer/repository"
  unshare -n strace -f -qq -e trace=network -o "$trace" \
    env DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ROOT="$dotnet_root" NUGET_PACKAGES="$consumer_cache" \
      FSASSAY_PACKAGE_SHA256="$package_hash" dotnet tool run fsassay -- \
        --files "$source_file" "$source_file"
) >"$output/offline-analysis.stdout" 2>"$output/offline-analysis.stderr"
offline_analysis_exit=$?
set -e
test "$offline_analysis_exit" -eq 2
if grep -E 'AF_INET|AF_INET6' "$trace"; then
  echo "analysis attempted an IP network syscall" >&2
  exit 1
fi

set +e
(
  cd "$workspace"
  unshare -n env DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ROOT="$dotnet_root" NUGET_PACKAGES="$workspace/incomplete-cache" \
    FSASSAY_M4_LOCAL_FEED="$incomplete_feed" dotnet tool install "$package_id" --version "$package_version" \
      --tool-path "$workspace/incomplete-tools" --configfile "$root_a/eng/m4-offline-nuget.config"
) >"$output/incomplete-install.stdout" 2>"$output/incomplete-install.stderr"
incomplete_install_exit=$?
set -e
test "$incomplete_install_exit" -ne 0

(
  cd "$consumer/repository"
  unshare -n env DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_ROOT="$dotnet_root" NUGET_PACKAGES="$consumer_cache" \
    dotnet tool uninstall "$package_id" >"$output/tool-uninstall.log" 2>&1
)
manifest_after=$(sha256sum "$consumer/repository/dotnet-tools.json" | cut -d' ' -f1)
test "$manifest_before" = "$manifest_after"
test "$(find "$consumer/repository/.store" -type f 2>/dev/null | wc -l)" -eq 0
test -z "$(git -C "$root_a" status --porcelain --untracked-files=no)"
test -z "$(git -C "$root_b" status --porcelain --untracked-files=no)"

json_hash=$(sha256sum "$output/analysis-first.json" | cut -d' ' -f1)
sarif_hash=$(sha256sum "$output/analysis-first.sarif" | cut -d' ' -f1)
raw_hash_a=$(sha256sum "$raw_a" | cut -d' ' -f1)
raw_hash_b=$(sha256sum "$raw_b" | cut -d' ' -f1)
jq -n -S \
  --arg candidate "$candidate" \
  --arg packageHash "$package_hash" \
  --argjson packageBytes "$package_bytes" \
  --arg rawHashA "$raw_hash_a" \
  --arg rawHashB "$raw_hash_b" \
  --arg jsonHash "$json_hash" \
  --arg sarifHash "$sarif_hash" \
  '{
    schema: "fsassay-m4-package-qualification/1",
    candidateSha: $candidate,
    package: {
      id: "FsAssay.Cli",
      version: "1.0.4",
      file: "FsAssay.Cli.1.0.4.nupkg",
      sha256: $packageHash,
      bytes: $packageBytes,
      repository: "https://github.com/CanonFlow-Assay/FSharpAssay.git",
      repositoryCommit: $candidate,
      nugetSignature: "unsigned-NU3004"
    },
    stableTests: {
      ordinary: {total: 93, passed: 93, failed: 0},
      direct: {total: 93, passed: 93, failed: 0}
    },
    reproducibility: {
      roots: "independent-clones-with-independent-empty-caches",
      normalizedPackagesByteIdentical: true,
      rawPackageSha256: [$rawHashA, $rawHashB],
      canonicalization: "fixed-source-paths-and-canonical-OPC-entry-order-identities-timestamps"
    },
    consumer: {
      repositoryLocalManifest: true,
      localFeedOnly: true,
      networkNamespaceDisabled: true,
      helpDoctorExplainPassed: true,
      analysisExitCode: 2,
      zeroEvidenceExitCode: 2,
      rollbackRestoredManifest: true
    },
    evidence: {
      jsonSha256: $jsonHash,
      sarifSha256: $sarifHash,
      repeatedEvidenceByteIdentical: true,
      noIpNetworkSyscallsObserved: true,
      sourceLinkVerifiedForRunnerAndAnalyzers: true
    },
    adversarial: {
      hashMismatchRejected: true,
      provenanceMismatchRejected: true,
      incompletePackageRejected: true,
      invalidInvocationRejected: true,
      zeroEvidenceNonAuthoritative: true
    }
  }' >"$output/package-manifest.json"

sha256sum "$output/$package_file" "$output/package-manifest.json" >"$output/sha256sums.txt"
echo "M4 package qualification passed for $candidate ($package_hash)"
