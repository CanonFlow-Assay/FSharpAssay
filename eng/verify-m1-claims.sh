#!/usr/bin/env bash
set -euo pipefail

base="f773b3090ffd86cb5600fdaf3aca20ec9cc19606"
base_tree="417d216dd00d7d8f082627f1f859f413044a8c6d"
analyzer_tree="bbd34a4fe89f692347f6f5706258252ffa69c32e"
manifest="docs/evidence/m1-deletions.tsv"

test "$(git rev-parse "${base}^{tree}")" = "$base_tree"
test "$(git rev-parse "${base}:FsAssay.Analyzers")" = "$analyzer_tree"
mapfile -t analyzer_changes < <(git diff --name-only "$base" HEAD -- FsAssay.Analyzers)
test "${#analyzer_changes[@]}" -eq 1
test "${analyzer_changes[0]}" = "FsAssay.Analyzers/packages.lock.json"
test -z "$(git diff --name-only "$base" HEAD -- \
  FsAssay.Analyzers ':!FsAssay.Analyzers/packages.lock.json')"

if grep -Eini 'pages|id-token|environment:|deploy-pages|upload-pages' .github/workflows/*.yml; then
  echo "deployment capability remains in a workflow" >&2
  exit 1
fi

grep -q '93 rule identifiers' README.md
grep -q '35 marked `Implemented`, 22 `Dummy`' README.md
grep -q 'and 36 `Prototype`' README.md
grep -q -- '--out-json artifacts/result.json' README.md
test "$(jq -r '.sdk.version' global.json)" = "10.0.301"
test "$(jq -r '.sdk.rollForward' global.json)" = "disable"
test "$(grep -h "dotnet-version: '10.0.301'" .github/workflows/*.yml | wc -l)" -eq 3
test "$(grep -h 'dotnet --version' .github/workflows/*.yml | wc -l)" -eq 3
test "$(grep -h 'DOTNET_INSTALL_DIR:' .github/workflows/*.yml | wc -l)" -eq 3

test "$(head -n 1 "$manifest")" = $'path\tbase_blob\tbytes\tcategory\trecovery'
tail -n +2 "$manifest" | cut -f1 | LC_ALL=C sort -c
test "$(tail -n +2 "$manifest" | wc -l)" -eq 2296
test -z "$(tail -n +2 "$manifest" | cut -f1 | uniq -d)"

while IFS=$'\t' read -r path blob bytes category recovery; do
  test -n "$category"
  test "$recovery" = "git show ${base}:${path}"
  test "$(git rev-parse "${base}:${path}")" = "$blob"
  test "$(git cat-file -s "$blob")" = "$bytes"
  test ! -e "$path"
done < <(tail -n +2 "$manifest")

mapfile -t recorded < <(tail -n +2 "$manifest" | cut -f1)
mapfile -t deleted < <(git diff --diff-filter=D --name-only "$base" HEAD | LC_ALL=C sort)
if ! diff -u <(printf '%s\n' "${recorded[@]}") <(printf '%s\n' "${deleted[@]}"); then
  echo "deletion ledger does not match the candidate diff" >&2
  exit 1
fi

for project in \
  FsAssay.Analyzers FsAssay.CanonFlow.Plugin FsAssay.Runner FsAssay.Tests \
  FsAssay.Desktop FsAssay.TypeGym FsAssay.Web.Tests \
  FsAssay.Web/src/FsAssay.Web.Client; do
  test -f "$project/packages.lock.json"
done

if git ls-files | grep -E '(^|/)(node_modules|public_html)/|^docs/(_content|_framework|css)/|^adjudicate\.log$|^out-toolchain\.json$|^e2e/failure\.png$'; then
  echo "tracked generated or machine-local debt remains" >&2
  exit 1
fi

echo "M1 claims verified"
