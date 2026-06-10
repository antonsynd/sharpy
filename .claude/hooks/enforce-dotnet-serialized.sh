#!/bin/bash
# PreToolUse(Bash) hook: blocks raw `dotnet build` / `dotnet test` so parallel
# agents can't OOM the machine (each dotnet test eats 5-10 GB). All build/test
# invocations must go through .claude/scripts/dotnet-serialized (flock).
input=$(cat)
cmd=$(printf '%s' "$input" | python3 -c "import json,sys; print(json.load(sys.stdin).get('tool_input',{}).get('command',''))" 2>/dev/null)

if printf '%s' "$cmd" | grep -qE '(^|[;&|[:space:]("])dotnet[[:space:]]+(build|test)([[:space:]]|$|")' \
   && ! printf '%s' "$cmd" | grep -q 'dotnet-serialized'; then
  echo 'BLOCKED: raw `dotnet build`/`dotnet test` is forbidden (parallel runs OOM the machine). Use the drop-in wrapper instead — same args, same output, same exit code: .claude/scripts/dotnet-serialized test --filter "FullyQualifiedName~Lexer" --no-build' >&2
  exit 2
fi
exit 0
