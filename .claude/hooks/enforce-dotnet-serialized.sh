#!/bin/bash
# PreToolUse(Bash) hook: blocks raw `dotnet` verbs that build, test, or load the solution so
# parallel agents can't OOM the machine (a whole-solution test run peaks at 18.5 GB tree RSS,
# CLAUDE.md "Serialized dotnet"). Every such invocation must go through
# .claude/scripts/dotnet-serialized (exclusive mkdir lock).
#
# Verbs covered: build, test, run (builds the project first — /spy-run, /spy-emit, /quick-check,
# /lsp-hover, /benchmark), format (loads the whole solution workspace), pack, publish, msbuild,
# clean (deletes bin/obj under a concurrent build), stryker (builds + tests repeatedly).
# Not covered: `dotnet watch` (/playground, long-lived by design), `dotnet --version`, `dotnet tool`.
# A script that calls dotnet internally (`bash build_tools/regenerate_spy_stdlib.sh`) is not seen
# by this hook — those scripts take SHARPYC=<built sharpyc> to stay off the lock.
#
# Hook input is JSON on stdin; the command is tool_input.command.
# Falsified by .claude/hooks/test-hooks.sh.
input=$(cat)
cmd=$(printf '%s' "$input" | python3 -c "import json,sys; print(json.load(sys.stdin).get('tool_input',{}).get('command',''))" 2>/dev/null)

if printf '%s' "$cmd" | grep -qE '(^|[;&|[:space:]("])dotnet[[:space:]]+(build|test|run|format|pack|publish|msbuild|clean|stryker)([[:space:]]|$|")' \
   && ! printf '%s' "$cmd" | grep -q 'dotnet-serialized'; then
  echo 'BLOCKED: raw `dotnet build|test|run|format|pack|publish|msbuild|clean|stryker` is forbidden (parallel runs OOM the machine). Use the drop-in wrapper instead — same args, same output, same exit code: .claude/scripts/dotnet-serialized test --filter "FullyQualifiedName~Lexer" --no-build   (lock-free alternative for probes: src/Sharpy.Cli/bin/Debug/net10.0/sharpyc run file.spy)' >&2
  exit 2
fi
exit 0
