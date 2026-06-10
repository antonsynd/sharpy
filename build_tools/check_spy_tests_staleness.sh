#!/usr/bin/env bash
# Verify that committed generated C# matches .spy test sources.
# Usage: bash build_tools/check_spy_tests_staleness.sh
#
# Delegates to regenerate_spy_tests.sh --check, which emits fresh C#
# to a temp directory and diffs against committed files. Exits 1 if
# any file is stale.
#
# Called from CI (.github/workflows/dotnet10.yml).

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
exec bash "$SCRIPT_DIR/regenerate_spy_tests.sh" --check
