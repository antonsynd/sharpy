---
name: format
description: Format code whitespace per project conventions (required before commits)
type: agent
---

Format code whitespace using `dotnet format`. This is required before commits per CLAUDE.md guidelines.

**Usage:** `/format`

**Behavior:**
- On success: Shows "FORMAT COMPLETE" + output
- On failure: Shows "FORMAT FAILED" + last 50 lines + points to full log

**Log location:** `.claude/tmp/last-format.log`

## Steps

1. Run `mkdir -p .claude/tmp` to ensure log directory exists
2. Clear the old log with `rm -f .claude/tmp/last-format.log`
3. Run: `dotnet format whitespace > .claude/tmp/last-format.log 2>&1`
4. Check exit code:
   - Exit 0: Print "=== FORMAT COMPLETE ===" then `cat .claude/tmp/last-format.log`
   - Exit non-zero: Print "=== FORMAT FAILED (last 50 lines) ===" then `tail -50 .claude/tmp/last-format.log`, then echo "=== Full log: .claude/tmp/last-format.log ==="
5. Return the actual exit code
