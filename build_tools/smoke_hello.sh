#!/usr/bin/env bash
# Runs the README's hello world on a sharpyc binary and asserts its output (#2291, R-GH).
#
# Usage: build_tools/smoke_hello.sh <absolute path to sharpyc> [extra args before `run`...]
#
# The program is extracted from README.md (the first ```python block that opens with
# `# hello.spy`), so the smoke is the documented first command, not a copy that can drift.
# It runs exactly as the README spells it — `sharpyc run hello.spy` from the file's directory —
# and passes only when sharpyc exits 0 and the last non-empty output line is `Hello, World!`.
# `run` launches the compiled program with `dotnet`, so a .NET 10 runtime must be on PATH.
set -euo pipefail

if [ $# -eq 0 ]; then
  echo "usage: $0 <absolute path to sharpyc> [args...]" >&2
  exit 2
fi

repo=$(cd "$(dirname "$0")/.." && pwd)
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

awk '
  /^```python[[:space:]]*$/ { inb = 1; buf = ""; next }
  /^```[[:space:]]*$/ {
    if (inb && buf ~ /^# hello\.spy\n/) { printf "%s", buf; found = 1; exit }
    inb = 0; next
  }
  inb { buf = buf $0 "\n" }
  END { if (!found) exit 1 }
' "$repo/README.md" > "$work/hello.spy" || {
  echo "::error::README.md has no \`\`\`python block opening with '# hello.spy'" >&2
  exit 1
}

echo "--- hello.spy (from README.md)"
cat "$work/hello.spy"
echo "--- $* run hello.spy"

set +e
out=$(cd "$work" && "$@" run hello.spy 2>&1)
rc=$?
set -e
printf '%s\n' "$out"

last=$(printf '%s\n' "$out" | tr -d '\r' | awk 'NF { l = $0 } END { print l }')
if [ "$rc" -ne 0 ] || [ "$last" != "Hello, World!" ]; then
  echo "::error::sharpyc run hello.spy: exit $rc, last line '$last' (expected 'Hello, World!')" >&2
  exit 1
fi
echo "smoke OK"
