#!/usr/bin/env bash
# Verify that every commit subject in a range is at most 72 characters (W3.4 / D5) and that no
# commit message has a GitHub closing keyword (2026-11-18).
# Usage: bash build_tools/check_commit_subjects.sh [<git range>] [--max N] [--grandfather <sha>|none]
#        [--pr-author=<login>] [--pr-title=<title>] [--pr-body=<description>]
#
# Exits 0 if clean, 1 if a subject is over the limit or a message has a closing keyword, 2 if git
# fails.
# Called from CI (.github/workflows/commit-subjects.yml) and the /push skill.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
exec python3 "$SCRIPT_DIR/check_commit_subjects.py" "$@"
