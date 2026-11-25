#!/usr/bin/env bash
# How many existing programs would a proposed language rule touch?
# Runs one rule's probe over every Sharpy program in the repository (docs/process/blast-radius.md).
#
# Usage:
#   bash build_tools/blast_radius.sh list                       # the rules that have a probe
#   bash build_tools/blast_radius.sh run R-RO                   # Markdown report on standard output
#   bash build_tools/blast_radius.sh run R-RO --md r.md --json r.json
#   bash build_tools/blast_radius.sh run R-RO --area mine=path/to/programs
#
# The tool is built first when its binary is missing or older than its sources. The build goes
# through the serialized dotnet wrapper; the run itself is lock-free and takes about half a minute.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT="$REPO_ROOT/src/Sharpy.BlastRadius"
BINARY="$PROJECT/bin/Debug/net10.0/sharpy-blast.dll"

if [[ ! -f "$BINARY" ]] || [[ -n "$(find "$PROJECT" "$REPO_ROOT/src/Sharpy.Compiler" -name '*.cs' \
        -not -path '*/obj/*' -not -path '*/bin/*' -newer "$BINARY" -print -quit)" ]]; then
    "$REPO_ROOT/.claude/scripts/dotnet-serialized" build "$PROJECT/Sharpy.BlastRadius.csproj" >&2
fi

if [[ "${1:-}" == "run" ]]; then
    shift
    exec dotnet "$BINARY" run "$@" --repo "$REPO_ROOT"
fi
exec dotnet "$BINARY" "$@"
