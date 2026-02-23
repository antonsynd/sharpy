---
name: spy-run
description: Run a Sharpy source file
argument-hint: "<file.spy>"
---

Compile and execute a Sharpy (.spy) source file.

**Usage:** `/spy-run <file.spy>`

**Behavior:**
- Shows full program output on success
- Shows last 80 lines of errors on failure + points to full log

**Log location:** `.claude/tmp/last-spy-run.log`

**Examples:**
```
/spy-run src/Sharpy.Compiler.Tests/Integration/TestFixtures/basics/hello_world.spy
/spy-run test.spy
```

## Steps

1. Validate `$ARGUMENTS` is non-empty
2. Run `mkdir -p .claude/tmp` to ensure log directory exists
3. Clear the old log with `rm -f .claude/tmp/last-spy-run.log`
4. Run: `dotnet run --project src/Sharpy.Cli -- run "$ARGUMENTS" > .claude/tmp/last-spy-run.log 2>&1`
5. Check exit code:
   - Exit 0: Print "=== RUN OUTPUT ===" then `cat .claude/tmp/last-spy-run.log`
   - Exit non-zero: Print "=== RUN FAILED (last 80 lines) ===" then `tail -80 .claude/tmp/last-spy-run.log`, then echo "=== Full log: .claude/tmp/last-spy-run.log ==="
6. Return the actual exit code
