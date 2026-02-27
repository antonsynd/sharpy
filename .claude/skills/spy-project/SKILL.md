---
name: spy-project
description: Compile and run a multi-file Sharpy project (.spyproj)
argument-hint: "<path/to/project.spyproj> [--incremental]"
---

Compile and execute a multi-file Sharpy project using a `.spyproj` file.

**Usage:**
- `/spy-project path/to/project.spyproj` — compile and run
- `/spy-project path/to/project.spyproj --incremental` — incremental compilation

**Behavior:**
- Shows full program output on success
- Shows last 80 lines of errors on failure + points to full log

**Log location:** `.claude/tmp/last-spy-project.log`

## Steps

1. Validate `$ARGUMENTS` is non-empty
2. Run `mkdir -p .claude/tmp` to ensure log directory exists
3. Clear the old log with `rm -f .claude/tmp/last-spy-project.log`
4. Run: `dotnet run --project src/Sharpy.Cli -- project $ARGUMENTS > .claude/tmp/last-spy-project.log 2>&1`
5. Check exit code:
   - Exit 0: Print "=== PROJECT RUN OUTPUT ===" then `cat .claude/tmp/last-spy-project.log`
   - Exit non-zero: Print "=== PROJECT RUN FAILED (last 80 lines) ===" then `tail -80 .claude/tmp/last-spy-project.log`, then echo "=== Full log: .claude/tmp/last-spy-project.log ==="
6. Return the actual exit code
