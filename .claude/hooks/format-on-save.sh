#!/usr/bin/env bash
# PostToolUse(Edit|Write|NotebookEdit) hook: whitespace-format the ONE file that was just written.
#
# Hook input is JSON on stdin ({"tool_input":{"file_path":...}}); there is no TOOL_INPUT_* env var,
# which is why the pre-2026-11-06 version of this hook (reading $TOOL_INPUT_FILE_PATH) never ran.
# file_path is ABSOLUTE, so the globs below carry a leading `*` — the old relative globs
# (`build_tools/*.py`) could never match it.
#
# Budget: the hook has a 10 s timeout (.claude/settings.json) and runs on EVERY edit, so each
# branch must be cheap and must touch only $FILE.
#
# C#: `dotnet format whitespace` has no single-file mode — `--include <file>` still loads an
# MSBuild workspace — so the hook formats against the file's OWN project (nearest *.csproj up the
# tree), never the solution: measured 1.9 s warm on Sharpy.Compiler.csproj and 1.6 s on
# Sharpy.Core.csproj (`--include <one file> --verify-no-changes --no-restore`, @ ee9a007a1,
# 2026-11-06); a solution-wide `--include` would not fit the timeout. The call is capped at 8 s
# by this script itself and the child is killed on expiry, so a harness timeout can never leave an
# orphaned formatter that rewrites the file after a later edit. It is best-effort (`--no-restore`,
# failures ignored): `/push` and CI still gate the whole solution with `--verify-no-changes`.
# Raw `dotnet format` is used deliberately, not the serialized wrapper — a hook that waits on the
# dotnet lock would be killed at the timeout and the wrapper's eventual run would be the orphan
# described above.
#
# Falsified by .claude/hooks/test-hooks.sh.
set -uo pipefail

input=$(cat)
file=$(printf '%s' "$input" | python3 -c '
import json, sys
ti = json.load(sys.stdin).get("tool_input", {})
print(ti.get("file_path") or ti.get("notebook_path") or "")
' 2>/dev/null) || { echo "format-on-save: could not parse hook stdin" >&2; exit 1; }
[ -z "$file" ] && exit 0
[ -f "$file" ] || exit 0

root="${CLAUDE_PROJECT_DIR:-$(cd "$(dirname "$0")/../.." && pwd)}"

# nearest_csproj DIR → prints the single *.csproj at or above DIR (stops at $root / filesystem root).
nearest_csproj() {
  local dir="$1" matches
  while :; do
    matches=$(ls "$dir"/*.csproj 2>/dev/null)
    if [ -n "$matches" ]; then
      [ "$(printf '%s\n' "$matches" | wc -l)" -eq 1 ] && printf '%s' "$matches"
      return
    fi
    [ "$dir" = "$root" ] || [ "$dir" = "/" ] || [ -z "$dir" ] && return
    dir=$(dirname "$dir")
  done
}

# run_capped SECONDS CMD... → runs CMD, kills it if still alive after SECONDS; never fails the hook.
run_capped() {
  local budget="$1"; shift
  "$@" >/dev/null 2>&1 &
  local pid=$! ticks=$((budget * 10))
  trap 'kill "$pid" 2>/dev/null' TERM INT HUP
  while [ "$ticks" -gt 0 ] && kill -0 "$pid" 2>/dev/null; do sleep 0.1; ticks=$((ticks - 1)); done
  kill -0 "$pid" 2>/dev/null && { kill "$pid" 2>/dev/null; sleep 0.2; kill -9 "$pid" 2>/dev/null; }
  wait "$pid" 2>/dev/null || true
}

case "$file" in
  *.cs)
    # `--include` matches paths RELATIVE TO THE CURRENT DIRECTORY; an absolute path matches
    # nothing ("Formatted 0 of 427 files", exit 0 — measured 2026-11-06). So: cd to the project
    # directory and pass the file relative to it.
    if command -v dotnet >/dev/null 2>&1; then
      proj=$(nearest_csproj "$(dirname "$file")")
      if [ -n "$proj" ]; then
        projdir=$(dirname "$proj")
        rel=$(python3 -c 'import os,sys; print(os.path.relpath(sys.argv[1], sys.argv[2]))' "$file" "$projdir")
        ( cd "$projdir" && run_capped 8 dotnet format whitespace "$(basename "$proj")" --include "$rel" --no-restore )
      fi
    fi
    ;;
  *build_tools/*.py)
    if command -v ruff >/dev/null 2>&1; then
      ruff format --quiet -- "$file" 2>/dev/null || true
    elif command -v black >/dev/null 2>&1; then
      black --quiet -- "$file" 2>/dev/null || true
    fi
    ;;
  *editors/vscode/*.ts)
    if command -v prettier >/dev/null 2>&1; then
      prettier --write --log-level warn -- "$file" 2>/dev/null || true
    elif [ -x "$root/editors/vscode/node_modules/.bin/prettier" ]; then
      "$root/editors/vscode/node_modules/.bin/prettier" --write --log-level warn -- "$file" 2>/dev/null || true
    fi
    ;;
esac
exit 0
