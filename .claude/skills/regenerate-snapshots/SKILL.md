---
name: regenerate-snapshots
description: Regenerate C# snapshot tests after intentional codegen changes
---

Regenerate the `.expected.cs` snapshot files for file-based integration tests. Use after intentional codegen changes that you want to capture.

**Usage:** `/regenerate-snapshots`

**Behavior:**
- Sets `UPDATE_SNAPSHOTS=true` environment variable
- Runs file-based integration tests only
- On success: Shows completion summary
- On failure: Shows last 80 lines + points to full log

**Warning:** This modifies `.expected.cs` files. Review the changes with `git diff` afterwards.

**Log location:** `.claude/tmp/last-snapshot-regen.log`

**Workflow:**
1. Run `/regenerate-snapshots` to update snapshots
2. Run `/git diff` to review changes
3. If changes are intentional: `git add . && git commit`
4. If changes are unintentional: `git checkout -- src/**/*.expected.cs`

## Steps

1. Run `mkdir -p .claude/tmp` to ensure log directory exists
2. Clear the old log with `rm -f .claude/tmp/last-snapshot-regen.log`
3. Run: `UPDATE_SNAPSHOTS=true dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests" --no-build > .claude/tmp/last-snapshot-regen.log 2>&1`
4. Check exit code:
   - Exit 0: Print "=== SNAPSHOT REGENERATION COMPLETE ===" then `tail -30 .claude/tmp/last-snapshot-regen.log`
   - Exit non-zero: Print "=== REGENERATION FAILED (last 80 lines) ===" then `tail -80 .claude/tmp/last-snapshot-regen.log`, then echo "=== Full log: .claude/tmp/last-snapshot-regen.log ==="
5. Return the actual exit code
