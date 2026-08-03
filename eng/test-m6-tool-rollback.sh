#!/usr/bin/env bash
set -Eeuo pipefail

repository=$(git rev-parse --show-toplevel)
workspace=$(mktemp -d)
trap 'find "$workspace" -depth -delete 2>/dev/null || true' EXIT
checker="$repository/eng/verify-m6-tool-rollback.py"

# A removed manifest and an empty .NET 10 root manifest are both valid rollback outcomes.
python3 "$checker" "$workspace/absent/dotnet-tools.json"
mkdir -p "$workspace/empty"
printf '%s\n' '{"version":1,"isRoot":true,"tools":{}}' >"$workspace/empty/dotnet-tools.json"
python3 "$checker" "$workspace/empty/dotnet-tools.json"

# The checker must reject the actual root-manifest location when an identity survives uninstall.
mkdir -p "$workspace/populated"
printf '%s\n' '{"version":1,"isRoot":true,"tools":{"fsassay.cli":{"version":"1.0.4","commands":["fsassay"],"rollForward":false}}}' \
  >"$workspace/populated/dotnet-tools.json"
set +e
python3 "$checker" "$workspace/populated/dotnet-tools.json" \
  >"$workspace/populated.stdout" 2>"$workspace/populated.stderr"
populated_exit=$?
set -e
test "$populated_exit" -eq 1
test ! -s "$workspace/populated.stdout"
grep -Fxq 'M6 rollback verification failed: manifest still contains tool identities: fsassay.cli' \
  "$workspace/populated.stderr"

echo "M6 tool rollback regression passed"
