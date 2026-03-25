#!/usr/bin/env bash
# Auto-format files after Edit/Write based on file type.
# Called by Claude Code PostToolUse hook with $TOOL_INPUT_FILE_PATH set.
set -euo pipefail

FILE="${TOOL_INPUT_FILE_PATH:-}"
[ -z "$FILE" ] && exit 0

case "$FILE" in
    *.cs)
        dotnet format whitespace --include "$FILE" 2>/dev/null || true
        ;;
    build_tools/*.py)
        if command -v ruff &>/dev/null; then
            ruff format --quiet "$FILE" 2>/dev/null || true
        elif command -v black &>/dev/null; then
            black --quiet "$FILE" 2>/dev/null || true
        fi
        ;;
    editors/vscode/*.ts)
        if command -v prettier &>/dev/null; then
            prettier --write --log-level warn "$FILE" 2>/dev/null || true
        elif [ -x "editors/vscode/node_modules/.bin/prettier" ]; then
            editors/vscode/node_modules/.bin/prettier --write --log-level warn "$FILE" 2>/dev/null || true
        fi
        ;;
esac
