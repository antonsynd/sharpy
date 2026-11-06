#!/usr/bin/env bash
# PreToolUse(Edit|Write) hook: deny edits to secret-store files (keys, certs, .env, credentials).
#
# Claude Code passes hook input as JSON on stdin — {"tool_input":{"file_path":...},...} — and
# sets NO TOOL_INPUT_* environment variables. The version of this hook before 2026-11-06 read
# $TOOL_INPUT_FILE_PATH, so it always saw an empty path and exited 0: a no-op for its whole life.
#
# Fails CLOSED: if the input cannot be parsed, or carries no file_path, the edit is denied.
# Matching is on the BASENAME against secret-store naming conventions, not on "secrets" anywhere
# in the path: this repo ships a stdlib `secrets` module (src/Sharpy.Stdlib/spy/secrets_module.spy,
# docs/stdlib/secrets.md, stdlib_secrets.spy fixtures) that a path-substring rule would lock out.
#
# Falsified by .claude/hooks/test-hooks.sh (synthetic stdin; mutation record in the commit body).
set -uo pipefail

deny() {
  # $1 is a reason; keep it JSON-safe (basenames are the only user-controlled text we echo).
  local reason
  reason=$(printf '%s' "$1" | tr -cd 'A-Za-z0-9 ._:/-')
  printf '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":"%s"}}\n' "$reason"
  exit 0
}

input=$(cat)
file=$(printf '%s' "$input" | python3 -c '
import json, sys
d = json.load(sys.stdin)
p = d["tool_input"]["file_path"]
if not isinstance(p, str) or not p:
    raise SystemExit(1)
print(p)
' 2>/dev/null) || deny "block-secrets: could not read tool_input.file_path from hook stdin - refusing to edit blind"

base=$(basename -- "$file")

# .env and its variants, except the committed templates.
case "$base" in
  .env.example|.env.sample|.env.template) ;;
  .env|.env.*) deny "Blocked edit to sensitive file: $base" ;;
esac

# Private keys, certificates, keystores.
case "$base" in
  *.pem|*.key|*.pfx|*.p12|*.jks|*.keystore|id_rsa|id_dsa|id_ecdsa|id_ed25519)
    deny "Blocked edit to sensitive file: $base" ;;
esac

# Credential/secret stores by conventional name (config-style extensions only).
if printf '%s' "$base" | grep -qE '^(credentials|secrets)\.(json|ya?ml|toml|ini|cfg|conf|txt|properties)$'; then
  deny "Blocked edit to sensitive file: $base"
fi

exit 0
