#!/usr/bin/env bash
set -euo pipefail

root="$(git rev-parse --show-toplevel)"
manifest="$root/eng/required-locks.txt"
mkdir -p "$root/artifacts/restore"
log="$root/artifacts/restore/missing-lock-negative.log"
tmp="$(mktemp -d)"
trap 'rm -rf -- "$tmp"' EXIT

mapfile -t required < "$manifest"
for path in "${required[@]}"; do
  mkdir -p "$tmp/$(dirname "$path")"
  cp "$root/$path" "$tmp/$path"
done

git -C "$tmp" init -q
git -C "$tmp" add .
git -C "$tmp" -c user.name=M1 -c user.email=m1@example.invalid commit -q -m fixture

missing="${required[0]}"
mv "$tmp/$missing" "$tmp/missing-lock.fixture"

set +e
bash "$root/eng/assert-required-locks.sh" "$tmp" "$manifest" >"$log" 2>&1
status=$?
set -e

cat "$log"
if [[ "$status" -eq 0 ]]; then
  echo "missing required lock unexpectedly passed M1 policy" >&2
  exit 1
fi
grep -q "required lock file is missing: $missing" "$log"
echo "missing required lock failed M1 policy as required (exit $status)"
