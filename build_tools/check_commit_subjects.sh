#!/usr/bin/env bash
# Verify that every commit subject in a range is at most 72 characters (W3.4 / D5).
# Usage: bash build_tools/check_commit_subjects.sh [<git range>] [--max N]
#
# Exits 0 if clean, 1 if a subject is over the limit, 2 if git fails.
# Called from CI (.github/workflows/commit-subjects.yml) and the /push skill.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
exec python3 "$SCRIPT_DIR/check_commit_subjects.py" "$@"
