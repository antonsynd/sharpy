---
name: spy-emit-csharp
description: Emit generated C# from a .spy file for codegen debugging
argument-hint: "<file.spy>"
---

Generate and display the C# code that would be produced from a Sharpy (.spy) source file. Useful for debugging code generation.

**Usage:** `/spy-emit-csharp <file.spy>`

**Behavior:**
- Shows last 100 lines of generated C# on success
- Shows last 80 lines of errors on failure + points to full log

**Log location:** `.claude/tmp/last-spy-emit-csharp.log`

**Examples:**
```
/spy-emit-csharp src/Sharpy.Compiler.Tests/Integration/TestFixtures/basics/hello_world.spy
/spy-emit-csharp test.spy
```

## Steps

1. Validate `$ARGUMENTS` is non-empty
2. Run `mkdir -p .claude/tmp` to ensure log directory exists
3. Clear the old log with `rm -f .claude/tmp/last-spy-emit-csharp.log`
4. Run: `dotnet run --project src/Sharpy.Cli -- emit csharp "$ARGUMENTS" > .claude/tmp/last-spy-emit-csharp.log 2>&1`
5. Check exit code:
   - Exit 0: Print "=== GENERATED C# (last 100 lines) ===" then `tail -100 .claude/tmp/last-spy-emit-csharp.log`, then echo "Full output: .claude/tmp/last-spy-emit-csharp.log"
   - Exit non-zero: Print "=== EMIT FAILED (last 80 lines) ===" then `tail -80 .claude/tmp/last-spy-emit-csharp.log`, then echo "=== Full log: .claude/tmp/last-spy-emit-csharp.log ==="
6. Return the actual exit code
