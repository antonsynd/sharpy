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
# subshells, `$(...)`, `bash -c '...'`, a shell's here-string or here-document script, `eval`,
# VAR=v, `$X` naming the program (`D=dotnet; $D test`), the launchers in its PREFIXES — env,
# sudo, time, timeout, nice, ionice, taskset, chrt, flock, strace, watch, parallel, xargs, setsid,
# nohup, stdbuf — and `find -exec`): it is
# blocked when the program it runs is `dotnet` by any path (`/root/.dotnet/dotnet build`) and its
# first non-option argument is a covered verb. The wrapper is a different program, so it passes,
# and a raw call beside it in the same command (`echo dotnet-serialized; dotnet test`) is blocked.
# `dotnet build` as an argument (`grep "dotnet build" docs/`, `echo dotnet test`) or in a
# here-document fed to a non-shell (`cat > notes.md <<EOF`) is not a call.
#
# PATTERN-CHECKED instead of walked (a whole-string pattern for a raw dotnet verb blocks it):
# unparseable text, unless `bash -n` refuses it too (bash will not run it); and a script the text
# runs but does not show: `eval "$X"`, `bash -c "$X"`, `$(which dotnet) build`, `... | bash`,
# `sh -s`, `bash < file`, `bash <(...)`. Out of scope: a script file run by path (`bash
# build_tools/regenerate_spy_stdlib.sh`; those scripts take SHARPYC=<built sharpyc> to stay off
# the lock), `source`, an alias or function, a python/node subprocess, a launcher outside
# PREFIXES (ssh, su -c, script -c, docker exec, make, npm run).
#
# FAILS CLOSED, never weaker than the grep hook it replaced: unreadable input (not JSON, no
# string command) is blocked; an internal error applies the whole-string pattern (a superset of
# the old grep's); a python3 that fails to start or dies is blocked by the shell wrapper. The
# input is read from stdin by python (no size limit).
#
# Hook input is JSON on stdin; the command is tool_input.command.
# Falsified by .claude/hooks/test-hooks.sh.
read -r -d '' PROG <<'PY'
import json, os, re, sys
sys.dont_write_bytecode = True

VERBS = {"build", "test", "vstest", "run", "format", "pack", "publish", "msbuild", "clean", "stryker"}
RAW = re.compile(r"(^|[\s;&|(\"'`])(\S*/)?dotnet(\.exe)?[\"')`]*\s+(-\S+\s+)*(" + "|".join(VERBS)
                 + r")\b")

def block(verb, why=""):
    sys.stderr.write(
        "BLOCKED by .claude/hooks/enforce-dotnet-serialized.sh: " + why + "raw `dotnet " + verb
        + "` (any of build|test|vstest|run|format|pack|publish|msbuild|clean|stryker) is "
        "forbidden: parallel runs OOM the machine. Use the drop-in wrapper instead, same args, "
        "same output, same exit code: .claude/scripts/dotnet-serialized test --filter "
        "\"FullyQualifiedName~Lexer\" --no-build   (lock-free alternative for probes: "
        "src/Sharpy.Cli/bin/Debug/net10.0/sharpyc run file.spy)\n")
    sys.exit(2)

def by_pattern(why):
    m = RAW.search(cmd)
    if m:
        block(m.group(5), why)

try:
    cmd = json.loads(sys.stdin.buffer.read())["tool_input"]["command"]
    assert isinstance(cmd, str)
except Exception as e:
    sys.stderr.write("BLOCKED by .claude/hooks/enforce-dotnet-serialized.sh: cannot read the hook "
                     "input (%s: %s); the hook fails closed.\n" % (type(e).__name__, e))
    sys.exit(2)

def visit(argv, *_):
    if os.path.basename(argv[0]) not in ("dotnet", "dotnet.exe"):
        return
    verb = next((a for a in argv[1:] if not a.startswith("-")), "")
    if verb in VERBS:
        block(verb)

try:
    sys.path.insert(0, os.environ["HOOKS_DIR"])
    import shell_commands as sc
    try:
        sc.walk(cmd, os.getcwd(), visit,
                opaque=lambda cwd, what: by_pattern("a script the hook cannot read (%s): " % what))
    except sc.ParseError:
        if sc.bash_accepts(cmd):
            by_pattern("an unparseable command: ")
except Exception as e:
    by_pattern("the hook failed (%s: %s): " % (type(e).__name__, e))
sys.exit(0)
PY
HOOKS_DIR="$(cd "$(dirname "$0")" && pwd)" python3 -c "$PROG"
rc=$?
case $rc in
  0 | 2) exit "$rc" ;;
esac
echo "BLOCKED by .claude/hooks/enforce-dotnet-serialized.sh: the hook could not judge this" \
  "command (python3 exit $rc); it fails closed." >&2
exit 2
