#!/bin/bash
# PreToolUse(Bash) hook: blocks raw `dotnet` verbs that build, test, or load the solution so
# parallel agents can't OOM the machine (a whole-solution test run peaks at 18.5 GB tree RSS,
# CLAUDE.md "Serialized dotnet"). Every such invocation must go through
# .claude/scripts/dotnet-serialized (exclusive flock lock).
#
# Verbs covered: build, test, vstest, run (builds the project first — /spy-run, /spy-emit,
# /quick-check, /lsp-hover, /benchmark), format (loads the whole solution workspace), pack,
# publish, msbuild, clean (deletes bin/obj under a concurrent build), stryker (builds + tests
# repeatedly). Not covered: `dotnet watch` (/playground, long-lived by design), `dotnet --version`,
# `dotnet tool`, `dotnet <app>.dll`.
#
# Each simple command is judged on its own (.claude/hooks/shell_commands.py splits lists, pipes,
# subshells, `$(...)`, `bash -c '...'`, `eval`, VAR=v / env / timeout / xargs prefixes): it is
# blocked when the program it runs is `dotnet` by any path (`/root/.dotnet/dotnet build`) and its
# first non-option argument is a covered verb. The wrapper is a different program, so it passes,
# and a raw call beside it in the same command (`echo dotnet-serialized; dotnet test`) is blocked.
# `dotnet build` as an argument (`grep "dotnet build" docs/`, `echo dotnet test`) is not a call.
#
# Cannot see: a script that calls dotnet internally (`bash build_tools/regenerate_spy_stdlib.sh`;
# those scripts take SHARPYC=<built sharpyc> to stay off the lock), a program named by a
# substitution (`$(which dotnet) build`) or a variable set earlier in the command, an alias or
# function, a python/node subprocess. An unparseable command (unterminated quote) is allowed:
# bash refuses to run it as well.
#
# Hook input is JSON on stdin; the command is tool_input.command.
# Falsified by .claude/hooks/test-hooks.sh.
input=$(cat)
read -r -d '' PROG <<'PY'
import json, os, sys
sys.dont_write_bytecode = True
sys.path.insert(0, os.environ["HOOKS_DIR"])
import shell_commands as sc

VERBS = {"build", "test", "vstest", "run", "format", "pack", "publish", "msbuild", "clean", "stryker"}
try:
    cmd = json.loads(os.environ["HOOK_INPUT"]).get("tool_input", {}).get("command", "")
except Exception:
    sys.exit(0)
if not isinstance(cmd, str):
    sys.exit(0)

def visit(argv, cwd):
    if os.path.basename(sc.expand(argv[0])) not in ("dotnet", "dotnet.exe"):
        return
    verb = next((a for a in argv[1:] if not a.startswith("-")), "")
    if verb in VERBS:
        sys.stderr.write(
            "BLOCKED by .claude/hooks/enforce-dotnet-serialized.sh: raw `dotnet " + verb + "` "
            "(any of build|test|vstest|run|format|pack|publish|msbuild|clean|stryker) is forbidden: "
            "parallel runs OOM the machine. Use the drop-in wrapper instead, same args, same "
            "output, same exit code: .claude/scripts/dotnet-serialized test --filter "
            "\"FullyQualifiedName~Lexer\" --no-build   (lock-free alternative for probes: "
            "src/Sharpy.Cli/bin/Debug/net10.0/sharpyc run file.spy)\n")
        sys.exit(2)

try:
    sc.walk(cmd, os.getcwd(), visit)
except sc.ParseError:
    pass
sys.exit(0)
PY
HOOK_INPUT="$input" HOOKS_DIR="$(cd "$(dirname "$0")" && pwd)" exec python3 -c "$PROG"
