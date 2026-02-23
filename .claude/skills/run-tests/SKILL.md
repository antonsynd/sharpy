---
name: run-tests
description: Run Sharpy tests with smart output - shows summary on success, last 80 lines on failure with full log saved
argument-hint: "[filter]"
---

Run Sharpy tests with optional filter. Output is smart-truncated to avoid token overload while preserving full logs for investigation.

**Usage:** `/run-tests [filter]` where filter can be:
- `Lexer`, `Parser`, `Semantic`, `Codegen` - component names
- `Integration`, `FileBased` - test categories
- Any substring matching a specific test name

**Behavior:**
- On success: Shows "TESTS PASSED" + last 20 lines (summary)
- On failure: Shows "TESTS FAILED" + last 80 lines + points to full log

**Log location:** `.claude/tmp/last-test-run.log`

**To investigate failures deeper:**
```
/head -200 .claude/tmp/last-test-run.log
/grep "Exception" .claude/tmp/last-test-run.log
```

## Steps

1. Run `mkdir -p .claude/tmp` to ensure log directory exists
2. Clear the old log with `rm -f .claude/tmp/last-test-run.log`
3. If `$ARGUMENTS` is non-empty, run: `dotnet test --filter "FullyQualifiedName~$ARGUMENTS" --no-build --logger "console;verbosity=normal" > .claude/tmp/last-test-run.log 2>&1`
4. Otherwise run: `dotnet test --no-build --logger "console;verbosity=normal" > .claude/tmp/last-test-run.log 2>&1`
5. Check exit code:
   - Exit 0: Print "=== TESTS PASSED ===" then `tail -20 .claude/tmp/last-test-run.log`
   - Exit non-zero: Print "=== TESTS FAILED (last 80 lines) ===" then `tail -80 .claude/tmp/last-test-run.log`, then echo "Full log: .claude/tmp/last-test-run.log"
6. Return the actual exit code from dotnet test
