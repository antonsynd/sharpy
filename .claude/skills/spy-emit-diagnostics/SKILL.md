---
name: spy-emit-diagnostics
description: Emit compiler diagnostics from a .spy file for debugging
argument-hint: "<file.spy> [--format json] [--include-codegen]"
---

Display all compiler diagnostics (errors, warnings, info) for a Sharpy (.spy) source file. Useful for debugging semantic analysis and validation.

**Usage:** `/spy-emit-diagnostics <file.spy>`

**Behavior:**
- Shows last 100 lines of diagnostics on success
- Shows last 80 lines of errors on failure + points to full log

**Log location:** `.claude/tmp/last-spy-emit-diagnostics.log`

**Examples:**
```
/spy-emit-diagnostics src/Sharpy.Compiler.Tests/Integration/TestFixtures/basics/hello_world.spy
/spy-emit-diagnostics test.spy --format json
/spy-emit-diagnostics test.spy --include-codegen
```

## Steps

1. Validate `$ARGUMENTS` is non-empty
2. Run `mkdir -p .claude/tmp` to ensure log directory exists
3. Clear the old log with `rm -f .claude/tmp/last-spy-emit-diagnostics.log`
4. Run: `dotnet run --project src/Sharpy.Cli -- emit diagnostics $ARGUMENTS > .claude/tmp/last-spy-emit-diagnostics.log 2>&1`
5. Check exit code:
   - Exit 0: Print "=== DIAGNOSTICS (last 100 lines) ===" then `tail -100 .claude/tmp/last-spy-emit-diagnostics.log`, then echo "Full output: .claude/tmp/last-spy-emit-diagnostics.log"
   - Exit non-zero: Print "=== DIAGNOSTICS FAILED (last 80 lines) ===" then `tail -80 .claude/tmp/last-spy-emit-diagnostics.log`, then echo "=== Full log: .claude/tmp/last-spy-emit-diagnostics.log ==="
6. Return the actual exit code
