#!/usr/bin/env bash
set -euo pipefail

repository="${1:-.}"
external_manifest="${2:-}"
repository="$(git -C "$repository" rev-parse --show-toplevel)"

if [[ -n "$external_manifest" ]]; then
  manifest="$external_manifest"
else
  manifest="$repository/eng/required-locks.txt"
  git -C "$repository" ls-files --error-unmatch -- eng/required-locks.txt >/dev/null
  expected_manifest_blob="$(git -C "$repository" rev-parse HEAD:eng/required-locks.txt)"
  actual_manifest_blob="$(git -C "$repository" hash-object -- "$manifest")"
  if [[ "$actual_manifest_blob" != "$expected_manifest_blob" ]]; then
    echo "required-lock manifest differs from its tracked Git blob" >&2
    exit 1
  fi
fi

test -f "$manifest"
LC_ALL=C sort -c "$manifest"
test -z "$(uniq -d "$manifest")"

mapfile -t required < "$manifest"
test "${#required[@]}" -eq 8

mapfile -t tracked < <(git -C "$repository" ls-files '*/packages.lock.json' | LC_ALL=C sort)
if ! diff -u <(printf '%s\n' "${required[@]}") <(printf '%s\n' "${tracked[@]}"); then
  echo "tracked lock files do not match M1 required-lock policy" >&2
  exit 1
fi

for path in "${required[@]}"; do
  if [[ "$path" = /* || "$path" == *../* || "$path" == ../* ]]; then
    echo "invalid required-lock path: $path" >&2
    exit 1
  fi
  git -C "$repository" ls-files --error-unmatch -- "$path" >/dev/null
  if [[ ! -f "$repository/$path" ]]; then
    echo "required lock file is missing: $path" >&2
    exit 1
  fi
  expected_blob="$(git -C "$repository" rev-parse "HEAD:$path")"
  actual_blob="$(git -C "$repository" hash-object -- "$repository/$path")"
  if [[ "$actual_blob" != "$expected_blob" ]]; then
    echo "required lock file differs from its tracked Git blob: $path" >&2
    exit 1
  fi
  jq -e '.version == 2 and (.dependencies | type == "object")' "$repository/$path" >/dev/null
done

echo "required lock policy verified (${#required[@]} tracked files)"
