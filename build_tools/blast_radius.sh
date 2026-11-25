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
# The tool is built first when a source of it, or of a project it references, is newer than the
# last build this script made. The build goes through the serialized dotnet wrapper; the run itself
# is lock-free and takes about a quarter of a minute.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT="$REPO_ROOT/src/Sharpy.BlastRadius"
BINARY="$PROJECT/bin/Debug/net10.0/sharpy-blast.dll"
# A stamp, not the binary: a build that changes no public surface leaves the binary's date alone.
STAMP="$PROJECT/obj/blast-radius.built"

if [[ ! -f "$BINARY" || ! -f "$STAMP" ]] || [[ -n "$(find "$PROJECT" "$REPO_ROOT/src/Sharpy.Compiler" \
        "$REPO_ROOT/src/Sharpy.Core" "$REPO_ROOT/src/Sharpy.Stdlib" \( -name '*.cs' -o -name '*.csproj' \) \
        -not -path '*/obj/*' -not -path '*/bin/*' -newer "$STAMP" -print -quit)" ]]; then
    "$REPO_ROOT/.claude/scripts/dotnet-serialized" build "$PROJECT/Sharpy.BlastRadius.csproj" >&2
    touch "$STAMP"
fi

if [[ "${1:-}" == "run" ]]; then
    shift
    exec dotnet "$BINARY" run "$@" --repo "$REPO_ROOT"
fi
exec dotnet "$BINARY" "$@"
