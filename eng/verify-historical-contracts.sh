#!/usr/bin/env bash
set -euo pipefail
repository="$(git rev-parse --show-toplevel)"
baseline=0a2054be7dd690bc7961d1fc72b9de0d25dc4dec
scratch="$(mktemp -d)"
checkout="$scratch/history"
cleanup() {
  git -C "$repository" worktree remove --force "$checkout" >/dev/null 2>&1 || true
  rm -rf -- "$scratch"
}
trap cleanup EXIT
git -C "$repository" worktree add --detach "$checkout" "$baseline" >/dev/null
cd "$checkout"
for script in verify-m1-claims.sh verify-m2-contract.sh verify-m3-contract.sh verify-m4-contract.sh; do
  bash "eng/$script"
done
printf 'Historical source contracts verified at %s; changed source requires refresh qualification.\n' "$baseline"
