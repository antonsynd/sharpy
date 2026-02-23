---
name: spy-emit-ast
description: Emit parsed AST from a .spy file for parser debugging
argument-hint: "<file.spy>"
---

Generate and display the parsed AST from a Sharpy (.spy) source file. Useful for debugging parser issues.

**Usage:** `/spy-emit-ast <file.spy>`

**Behavior:**
- Shows last 100 lines of AST output on success
- Shows last 80 lines of errors on failure + points to full log

**Log location:** `.claude/tmp/last-spy-emit.log`

## Steps

1. Validate `$ARGUMENTS` is non-empty
2. Run `mkdir -p .claude/tmp` to ensure log directory exists
3. Clear the old log with `rm -f .claude/tmp/last-spy-emit.log`
4. Run: `dotnet run --project src/Sharpy.Cli -- emit ast "$ARGUMENTS" > .claude/tmp/last-spy-emit.log 2>&1`
5. Check exit code:
   - Exit 0: Print "=== AST OUTPUT (last 100 lines) ===" then `tail -100 .claude/tmp/last-spy-emit.log`, then echo "Full output: .claude/tmp/last-spy-emit.log"
   - Exit non-zero: Print "=== AST EMIT FAILED (last 80 lines) ===" then `tail -80 .claude/tmp/last-spy-emit.log`, then echo "=== Full log: .claude/tmp/last-spy-emit.log ==="
6. Return the actual exit code
