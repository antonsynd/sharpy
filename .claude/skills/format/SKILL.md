---
name: format
description: Format C# whitespace solution-wide per project conventions (the save hook is per-file and best-effort)
---

Format code whitespace using `dotnet format` through the serialized wrapper. The PostToolUse hook `.claude/hooks/format-on-save.sh` already formats each edited `.cs` against its own project, but best-effort (capped at 8 s, skipped on failure), so run this when `/push`'s solution-wide `--verify-no-changes` gate reports files.

**Usage:** `/format`

**Behavior:**
- On success: Shows "FORMAT COMPLETE" + output
- On failure: Shows "FORMAT FAILED" + last 50 lines + points to full log

**Log location:** `.claude/tmp/last-format.log`

## Steps

1. Run `mkdir -p .claude/tmp` to ensure log directory exists
2. Clear the old log with `rm -f .claude/tmp/last-format.log`
3. Run: `.claude/scripts/dotnet-serialized format whitespace sharpy.sln > .claude/tmp/last-format.log 2>&1` (with `dangerouslyDisableSandbox: true`; raw `dotnet format` is hook-blocked — it loads the whole solution workspace)
4. Check exit code:
   - Exit 0: Print "=== FORMAT COMPLETE ===" then `cat .claude/tmp/last-format.log`
   - Exit non-zero: Print "=== FORMAT FAILED (last 50 lines) ===" then `tail -50 .claude/tmp/last-format.log`, then echo "=== Full log: .claude/tmp/last-format.log ==="
5. Return the actual exit code
