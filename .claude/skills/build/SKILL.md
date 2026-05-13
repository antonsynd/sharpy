---
name: build
description: Build the Sharpy solution with smart output truncation
---

Build the Sharpy solution. Output is smart-truncated to avoid token overload while preserving full logs for investigation.

**Usage:** `/build`

**Behavior:**
- On success: Shows "BUILD SUCCEEDED" + last 10 lines
- On failure: Shows "BUILD FAILED" + last 100 lines + points to full log

**Log location:** `.claude/tmp/last-build.log`

## Steps

1. Run `mkdir -p .claude/tmp` to ensure log directory exists
2. Clear the old log with `rm -f .claude/tmp/last-build.log`
3. Run: `.claude/scripts/dotnet-serialized build sharpy.sln > .claude/tmp/last-build.log 2>&1`
4. Check exit code:
   - Exit 0: Print "=== BUILD SUCCEEDED ===" then `tail -10 .claude/tmp/last-build.log`
   - Exit non-zero: Print "=== BUILD FAILED (last 100 lines) ===" then `tail -100 .claude/tmp/last-build.log`, then echo "=== Full log: .claude/tmp/last-build.log ==="
5. Return the actual exit code from dotnet build
