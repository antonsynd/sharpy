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

# Compiler to drive the sweep with. Prefer the already-built apphost: `dotnet run` takes the
# NuGet/MSBuild lock, so with --jobs > 1 the parallel invocations serialize on it (and collide with
# any other dotnet running in the tree). The apphost is lock-free, so the sweep actually parallelizes.
# Falls back to `dotnet run` when the binary has not been built yet, and an explicit SHARPYC always
# wins.
if [[ -z "${SHARPYC:-}" ]]; then
    SHARPYC_APPHOST="$REPO_ROOT/src/Sharpy.Cli/bin/Debug/net10.0/sharpyc"
    if [[ -x "$SHARPYC_APPHOST" ]]; then
        SHARPYC="$SHARPYC_APPHOST"
    else
        SHARPYC="dotnet run --project $REPO_ROOT/src/Sharpy.Cli --"
    fi
fi
export SHARPYC

exec python3 "$SCRIPT_DIR/spec_blocks.py" --spec-dir "$REPO_ROOT/docs/language_specification/" "$@"
