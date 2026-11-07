---
name: run-tests
description: Run Sharpy tests with smart output - shows summary on success, last 80 lines on failure with full log saved
argument-hint: "[filter]"
---

Run Sharpy tests with optional filter. Output is smart-truncated to avoid token overload while preserving full logs for investigation.

This is the local edit loop. The whole-solution commit gate is `/ci-gate` by default; an unfiltered `/run-tests` is its local fallback (contract §6).

**Usage:** `/run-tests [filter]` where filter can be:
- `Lexer`, `Parser`, `Semantic`, `Codegen` - component names
- `Integration`, `FileBased` - test categories
- Any substring matching a specific test name

**Behavior:**
- On success: Shows "TESTS PASSED" + last 20 lines (summary)
- On failure: Shows "TESTS FAILED" + last 80 lines + points to full log

**Log location:** each wrapper call's own `.claude/tmp/dotnet-serialized-<time>-<pid>.log`, whose path the wrapper prints (`dotnet-serialized: log -> <path>`); concurrent runs never share one. Below, `RUN <args>` means `.claude/scripts/dotnet-serialized <args> | sed -n 's/^dotnet-serialized: log -> //p'; echo "exit=${PIPESTATUS[0]}"`, which prints that path (`<log>`) and the exit code.

## Steps

1. Build first: `RUN build sharpy.sln --nologo -v q`. If it fails, print "=== BUILD FAILED — cannot run tests ===" then `tail -30 <log>` and stop.
2. If `$ARGUMENTS` is non-empty: `RUN test --filter "FullyQualifiedName~$ARGUMENTS&Category!=Benchmark" --no-build --logger "console;verbosity=normal"`; otherwise `RUN test --filter "Category!=Benchmark" --no-build --logger "console;verbosity=normal"`.
3. Exit 0: print "=== TESTS PASSED ===" then `tail -20 <log>`. Non-zero: print "=== TESTS FAILED (last 80 lines) ===" then `tail -80 <log>`, then "Full log: <log>" (`grep -n "Exception\|Failed" <log>` to dig deeper).
4. Return the actual exit code from dotnet test
