#!/usr/bin/env bash
# Runs the README's hello world on a sharpyc binary and asserts its output (#2291, R-GH).
#
# Usage: build_tools/smoke_hello.sh [--no-dotnet] <absolute path to sharpyc> [extra args before `run`...]
#
# The program is extracted from README.md (the first ```python block that opens with
# `# hello.spy`), so the smoke is the documented first command, not a copy that can drift.
# It runs exactly as the README spells it — `sharpyc run hello.spy` from the file's directory —
# and passes only when sharpyc exits 0 and the last non-empty output line is `Hello, World!`.
# `run` launches the compiled program with `dotnet`, so a .NET 10 runtime must be on PATH.
#
# --no-dotnet (#2292, R-GM): the same command on a host with no `dotnet` — what a user of the
# self-contained archive meets on a machine without .NET. PATH becomes an empty directory and
# DOTNET_ROOT* are unset (runner images ship .NET preinstalled, so the scrub is verified before
# anything is asserted), and the smoke passes only when sharpyc refuses BEFORE compiling: exit 1,
# SPY0913 in the output, no "Successfully compiled", no unhandled exception. If `run` ever executes
# the program in-process (#2293), this mode asserts `Hello, World!` instead.
set -euo pipefail

no_dotnet=0
if [ "${1:-}" = "--no-dotnet" ]; then
  no_dotnet=1
  shift
fi

if [ $# -eq 0 ]; then
  echo "usage: $0 [--no-dotnet] <absolute path to sharpyc> [args...]" >&2
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

if [ "$no_dotnet" -eq 1 ]; then
  bin="$work/empty-bin"
  mkdir "$bin"
  # Verify the instrument: sharpyc looks for dotnet beside itself, then on PATH.
  if PATH="$bin" command -v dotnet >/dev/null 2>&1; then
    echo "::error::the scrubbed PATH still resolves dotnet: $(PATH="$bin" command -v dotnet)" >&2
    exit 1
  fi
  for name in dotnet dotnet.exe; do
    if [ -e "$(dirname "$1")/$name" ]; then
      echo "::error::$(dirname "$1")/$name exists beside sharpyc; the no-dotnet cell cannot be reached" >&2
      exit 1
    fi
  done

  echo "--- $* run hello.spy  (PATH=$bin, DOTNET_ROOT* unset)"
  set +e
  out=$(cd "$work" && env -u DOTNET_ROOT -u DOTNET_ROOT_X64 -u DOTNET_ROOT_ARM64 -u DOTNET_ROOT_X86 \
    PATH="$bin" "$@" run hello.spy 2>&1)
  rc=$?
  set -e
  printf '%s\n' "$out"

  if [ "$rc" -ne 1 ] \
    || ! printf '%s' "$out" | grep -q 'SPY0913' \
    || printf '%s' "$out" | grep -q 'Successfully compiled' \
    || printf '%s' "$out" | grep -q 'Unhandled exception'; then
    echo "::error::sharpyc run hello.spy with no dotnet: exit $rc; expected exit 1 with SPY0913 before any compile (#2292)" >&2
    exit 1
  fi
  echo "smoke OK (no dotnet: refused with SPY0913 before compiling)"
  exit 0
fi

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
