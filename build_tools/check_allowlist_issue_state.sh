#!/usr/bin/env bash
# Verify that allowlist rows and C# Skip attributes cite only OPEN GitHub issues.
# Usage: bash build_tools/check_allowlist_issue_state.sh [--paths FILE ...]
#
# Exits 0 if clean, 1 if stale rows found, 2 if gh is unavailable.
# Called from CI (.github/workflows/dev-staleness.yml) and the /push skill.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
exec python3 "$SCRIPT_DIR/allowlist_issue_state.py" "$@"
