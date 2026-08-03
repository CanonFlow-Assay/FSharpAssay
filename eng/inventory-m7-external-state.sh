#!/usr/bin/env bash
set -Eeuo pipefail

candidate="${1:-}"
output="${2:-artifacts/m7-external-state.json}"
repository_slug="${GITHUB_REPOSITORY:-CanonFlow-Assay/FSharpAssay}"
workspace=$(mktemp -d)
trap 'find "$workspace" -depth -delete 2>/dev/null || true' EXIT

if [[ ! "$candidate" =~ ^[0-9a-f]{40}$ ]]; then
  echo "usage: $0 <candidate-sha> [output-file]" >&2
  exit 64
fi

mkdir -p "$(dirname "$output")"
output=$(cd "$(dirname "$output")" && pwd)/$(basename "$output")

probe() {
  local name="$1"
  local url="$2"
  local authentication="$3"
  local body="$workspace/$name.body"
  local status="000"
  local curl_status=0
  local -a headers=(--header 'Accept: application/vnd.github+json')

  if [[ "$authentication" = "github" && -n "${GH_TOKEN:-}" ]]; then
    headers+=(--header "Authorization: Bearer $GH_TOKEN")
  fi

  set +e
  status=$(curl --silent --show-error --location --output "$body" --write-out '%{http_code}' \
    "${headers[@]}" "$url")
  curl_status=$?
  set -e

  if [[ "$curl_status" -ne 0 ]]; then
    status="000"
  fi

  local access="unavailable"
  local observation="transport-or-access-unavailable"
  local count="null"
  case "$status" in
    200)
      access="available"
      observation="response-observed"
      if jq -e 'type == "array"' "$body" >/dev/null 2>&1; then
        count=$(jq 'length' "$body")
        if [[ "$count" -eq 0 ]]; then
          observation="empty-array-observed"
        else
          observation="nonempty-array-observed"
        fi
      fi
      ;;
    403) observation="permission-denied-at-endpoint" ;;
    404) observation="not-found-at-endpoint" ;;
    000) observation="transport-or-credential-unavailable" ;;
    *) observation="http-response-observed" ;;
  esac

  jq -n -S \
    --arg name "$name" \
    --arg url "$url" \
    --arg status "$status" \
    --arg access "$access" \
    --arg observation "$observation" \
    --argjson count "$count" '
      {
        name: $name,
        endpoint: $url,
        httpStatus: $status,
        access: $access,
        observation: $observation,
        arrayCount: $count,
        inference: "endpoint observation only; absence or access failure does not prove universal nonexistence"
      }
    ' >"$workspace/$name.json"
}

github_api="https://api.github.com/repos/$repository_slug"
probe rulesets "$github_api/rulesets" github
probe proposedTag "$github_api/git/ref/tags/v1.0.4" github
probe proposedRelease "$github_api/releases/tags/v1.0.4" github
probe documentationHosting "$github_api/pages" github
probe githubNuGetPackages "https://api.github.com/orgs/CanonFlow-Assay/packages?package_type=nuget" github
probe publicNuGetPackage "https://api.nuget.org/v3-flatcontainer/fsassay.cli/index.json" public

jq -n -S \
  --arg candidate "$candidate" \
  --arg repository "$repository_slug" \
  --slurpfile rulesets "$workspace/rulesets.json" \
  --slurpfile proposedTag "$workspace/proposedTag.json" \
  --slurpfile proposedRelease "$workspace/proposedRelease.json" \
  --slurpfile documentationHosting "$workspace/documentationHosting.json" \
  --slurpfile githubNuGetPackages "$workspace/githubNuGetPackages.json" \
  --slurpfile publicNuGetPackage "$workspace/publicNuGetPackage.json" '
    {
      schema: "fsassay-m7-external-state-observation/1",
      candidateSha: $candidate,
      repository: $repository,
      observationSemantics: "read-only endpoint evidence; unavailable, denied, empty and not-found states fail closed",
      endpoints: {
        rulesets: $rulesets[0],
        proposedTag: $proposedTag[0],
        proposedRelease: $proposedRelease[0],
        documentationHosting: $documentationHosting[0],
        githubNuGetPackages: $githubNuGetPackages[0],
        publicNuGetPackage: $publicNuGetPackage[0]
      },
      releaseActionsAuthorized: false,
      actionGates: {
        protectedTag: "blocked",
        packagePublication: "blocked",
        githubRelease: "blocked",
        documentationDeployment: "blocked",
        humanGateD: "blocked"
      }
    }
  ' >"$output"

jq -e '
  .releaseActionsAuthorized == false and
  ([.actionGates[]] | all(. == "blocked")) and
  ([.endpoints[].inference] | all(contains("does not prove universal nonexistence")))
' "$output" >/dev/null

echo "M7 external state recorded with every release action blocked"

