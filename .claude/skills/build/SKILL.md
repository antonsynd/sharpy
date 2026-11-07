---
name: build
description: Build the Sharpy solution with smart output truncation
---

Build the Sharpy solution. Output is smart-truncated to avoid token overload while preserving full logs for investigation.

**Usage:** `/build`

**Behavior:**
- On success: Shows "BUILD SUCCEEDED" + last 10 lines
- On failure: Shows "BUILD FAILED" + last 100 lines + points to full log

**Log location:** the wrapper's own per-invocation log, whose path it prints (`dotnet-serialized: log -> <path>`).

## Steps

1. Run: `.claude/scripts/dotnet-serialized build sharpy.sln | sed -n 's/^dotnet-serialized: log -> //p'; echo "exit=${PIPESTATUS[0]}"` — prints the log path (`<log>`) and the exit code.
2. Exit 0: print "=== BUILD SUCCEEDED ===" then `tail -10 <log>`. Non-zero: print "=== BUILD FAILED (last 100 lines) ===" then `tail -100 <log>`, then "=== Full log: <log> ===".
3. Return the actual exit code from dotnet build
