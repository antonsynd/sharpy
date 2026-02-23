---
name: build-verbose
description: Build with verbose diagnostics for debugging complex build issues
---

Build the Sharpy solution with diagnostic verbosity. Use when investigating complex build failures.

**Usage:** `/build-verbose`

**Behavior:**
- Shows diagnostic-level verbosity
- On success: Shows "BUILD SUCCEEDED" + last 10 lines
- On failure: Shows "BUILD FAILED" + last 100 lines + points to full log

**Log location:** `.claude/tmp/last-build-verbose.log`

**Warning:** This produces much larger output. Only use when normal build output is insufficient.

## Steps

1. Run `mkdir -p .claude/tmp` to ensure log directory exists
2. Clear the old log with `rm -f .claude/tmp/last-build-verbose.log`
3. Run: `dotnet build sharpy.sln --verbosity diag > .claude/tmp/last-build-verbose.log 2>&1`
4. Check exit code:
   - Exit 0: Print "=== BUILD SUCCEEDED ===" then `tail -10 .claude/tmp/last-build-verbose.log`
   - Exit non-zero: Print "=== BUILD FAILED (last 100 lines) ===" then `tail -100 .claude/tmp/last-build-verbose.log`, then echo "=== Full log: .claude/tmp/last-build-verbose.log ==="
5. Return the actual exit code from dotnet build
