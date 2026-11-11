#!/usr/bin/env bash
# Regenerate the LSP sweeps' per-stem set files from one run over the whole fixture corpus:
#   src/Sharpy.Lsp.Tests/Conformance/formatting-route-census.txt  (FormattingRouteParitySweepTests)
#   src/Sharpy.Lsp.Tests/Conformance/code-action-shape-stems.txt  (CodeActionShapeSweepTests)
# Run it after adding or changing a fixture under src/Sharpy.Compiler.Tests/Integration/TestFixtures/ when those
# sweeps name the file. Each twin's rows are rewritten in place (sorted, one per line); review `git diff`: a new
# fixture adds its own rows, and a changed or missing row of an existing stem is a finding until explained.
#
# Usage:
#   bash build_tools/regenerate_corpus_pins.sh             # build Sharpy.Lsp.Tests, then regenerate
#   bash build_tools/regenerate_corpus_pins.sh --no-build  # reuse the last build
#
# Runs through .claude/scripts/dotnet-serialized (the serialized dotnet lock); its log path is the first line printed.

set -euo pipefail

REPO_ROOT="$(git rev-parse --show-toplevel)"
cd "$REPO_ROOT"

build=()
case "${1:-}" in
    "") ;;
    --no-build) build=(--no-build) ;;
    *) echo "usage: $0 [--no-build]" >&2; exit 2 ;;
esac

CONF="Sharpy.Lsp.Tests.Conformance"
UPDATE_SNAPSHOTS=true .claude/scripts/dotnet-serialized test src/Sharpy.Lsp.Tests/Sharpy.Lsp.Tests.csproj "${build[@]}" \
    --filter "FullyQualifiedName~$CONF.FormattingRouteParitySweepTests.|FullyQualifiedName~$CONF.CodeActionShapeSweepTests." \
    --logger "console;verbosity=minimal"

git --no-pager diff --stat -- src/Sharpy.Lsp.Tests/Conformance/formatting-route-census.txt \
    src/Sharpy.Lsp.Tests/Conformance/code-action-shape-stems.txt
