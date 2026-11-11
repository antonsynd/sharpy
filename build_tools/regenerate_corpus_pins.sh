#!/usr/bin/env bash
# Regenerate the LSP sweeps' per-stem set files from one run over the whole fixture corpus:
#   src/Sharpy.Lsp.Tests/Conformance/formatting-route-census.txt  (FormattingRouteParitySweepTests)
#   src/Sharpy.Lsp.Tests/Conformance/code-action-shape-stems.txt  (CodeActionShapeSweepTests)
# Run it after adding or changing a fixture under src/Sharpy.Compiler.Tests/Integration/TestFixtures/ when those
# sweeps name the file. Each twin's rows are rewritten in place (sorted, one per line); review `git diff`: a new
# fixture adds its own rows, and a changed or missing row of an existing stem is a finding until explained.
# The run fails before writing anything when a gate a regeneration cannot move does not hold: a stem above its route's
# refusal-ratio bound without a hand-written row in formatting-route-refusal-exceptions.txt, an excepted stem whose
# counts moved, or a corpus-level ceiling (refusal ratio, exemption cells per stem). It never writes on CI.
# The stems file and the census stay human-reviewed: read the row diff and the totals lines printed at the end.
#
# Usage:
#   bash build_tools/regenerate_corpus_pins.sh             # build Sharpy.Lsp.Tests, then regenerate
#   bash build_tools/regenerate_corpus_pins.sh --no-build  # reuse the last build
#
# Runs through .claude/scripts/dotnet-serialized (the serialized dotnet lock); its log path is the first line printed.
# Plain bash 3.2 (macOS): no arrays expanded empty under set -u.

set -euo pipefail

REPO_ROOT="$(git rev-parse --show-toplevel)"
cd "$REPO_ROOT"

build=""
case "${1:-}" in
    "") ;;
    --no-build) build="--no-build" ;;
    *) echo "usage: $0 [--no-build]" >&2; exit 2 ;;
esac

CONF="Sharpy.Lsp.Tests.Conformance"
# The formatting sweep first: a refusal spike stops the script there, before the code-action rows are written too.
for class in FormattingRouteParitySweepTests CodeActionShapeSweepTests; do
    # shellcheck disable=SC2086  # $build is empty or one word
    SHARPY_REGENERATE_CORPUS_PINS=1 .claude/scripts/dotnet-serialized test src/Sharpy.Lsp.Tests/Sharpy.Lsp.Tests.csproj $build \
        --filter "FullyQualifiedName~$CONF.$class." --logger "console;verbosity=minimal"
    build="--no-build"
done

FILES="src/Sharpy.Lsp.Tests/Conformance/formatting-route-census.txt src/Sharpy.Lsp.Tests/Conformance/code-action-shape-stems.txt"
# shellcheck disable=SC2086  # two plain paths
git --no-pager diff --stat -- $FILES
echo "Volume (generated totals lines that moved; none printed means none moved):"
# shellcheck disable=SC2086
git --no-pager diff -U0 -- $FILES | grep -E '^[-+]# total ' || true
