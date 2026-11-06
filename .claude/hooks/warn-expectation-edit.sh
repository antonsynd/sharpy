#!/usr/bin/env bash
# PreToolUse(Edit|Write) hook: when the target is a test-expectation file, put a warning into
# Claude's context (CLAUDE.md Rule 1: never modify expected values to make tests pass).
#
# Hook input is JSON on stdin ({"tool_input":{"file_path":...}}); there is no TOOL_INPUT_* env var,
# which is why the inline `echo "$TOOL_INPUT_FILE_PATH" | grep ...` this replaces never fired.
# Plain stdout from a PreToolUse hook goes only to the debug log, so the warning is delivered as
# hookSpecificOutput.additionalContext (no permissionDecision: it must not block or auto-allow).
#
# Falsified by .claude/hooks/test-hooks.sh.
set -uo pipefail

input=$(cat)
file=$(printf '%s' "$input" | python3 -c '
import json, sys
print(json.load(sys.stdin)["tool_input"]["file_path"])
' 2>/dev/null) || { echo "warn-expectation-edit: could not read tool_input.file_path from hook stdin" >&2; exit 1; }

if printf '%s' "$file" | grep -qE '\.(expected|error|warning|expected\.cs)$'; then
  base=$(basename -- "$file" | tr -cd 'A-Za-z0-9 ._-')
  printf '{"hookSpecificOutput":{"hookEventName":"PreToolUse","additionalContext":"WARN: %s is a test expectation file. CLAUDE.md Rule 1: never modify expected values to make tests pass - fix the implementation. Proceed only if the expectation itself was wrong, and say so."}}\n' "$base"
fi
exit 0
