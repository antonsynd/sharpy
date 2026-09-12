#!/usr/bin/env bash
# Gate: every fenced Sharpy block in the language spec must compile, carry a
# marker, or be in the allowlist.
#
# Usage:
#   bash build_tools/check_spec_blocks.sh           # Run the sweep
#   bash build_tools/check_spec_blocks.sh --seed     # Seed the allowlist
#   bash build_tools/check_spec_blocks.sh --report   # Print class counts
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

if [[ -z "${SHARPYC:-}" ]]; then
    SHARPYC="dotnet run --project $REPO_ROOT/src/Sharpy.Cli --"
fi
export SHARPYC

exec python3 "$SCRIPT_DIR/spec_blocks.py" --spec-dir "$REPO_ROOT/docs/language_specification/" "$@"
