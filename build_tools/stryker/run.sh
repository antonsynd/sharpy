#!/usr/bin/env bash
# Mutation-score one component with Stryker.NET (#2289). Holds the dotnet-serialized lock for the
# whole run — the Lexer pilot held it 7 h 23 min @ 258a7b66a (docs/audits/stryker-lexer-pilot-2026-10-11.md),
# so start it when nothing else needs dotnet, preferably in a separate worktree.
#
# Usage:
#   bash build_tools/stryker/run.sh build_tools/stryker/lexer.json <output-dir>
#
# The config's reporters must include `dots`: the default `progress` reporter prints nothing off a
# TTY, and the wrapper's stall watchdog (1728 s of log silence) kills the run mid-test.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

if [ $# -ne 2 ]; then
    echo "usage: $0 <stryker-config.json> <output-dir>" >&2
    exit 2
fi
CONFIG="$1"
OUT="$2"
grep -q '"dots"' "$CONFIG" || { echo "error: $CONFIG has no \"dots\" reporter (see header)" >&2; exit 2; }

cd "$REPO_ROOT"
"$REPO_ROOT/.claude/scripts/dotnet-serialized" tool restore
exec "$REPO_ROOT/.claude/scripts/dotnet-serialized" stryker -f "$CONFIG" -O "$OUT" --skip-version-check
