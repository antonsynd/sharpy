#!/usr/bin/env bash
# Block edits to sensitive files (credentials, keys, secrets).
set -euo pipefail

FILE="${TOOL_INPUT_FILE_PATH:-}"
[ -z "$FILE" ] && exit 0

if echo "$FILE" | grep -qE '\.(env|pem|key|pfx)$|credentials|secrets'; then
    echo '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":"Blocked edit to sensitive file"}}'
fi
