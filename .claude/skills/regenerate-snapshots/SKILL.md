---
name: regenerate-snapshots
description: Regenerate C# snapshot tests and spy stdlib after intentional codegen changes
---

Regenerate all generated artifacts after intentional codegen changes:
1. `.expected.cs` snapshot files for file-based integration tests
2. `.spy` stdlib generated C# files (`src/Sharpy.Stdlib/`)

**Usage:** `/regenerate-snapshots`

**Behavior:**
- Builds the solution
- Regenerates spy stdlib C# via `build_tools/regenerate_spy_stdlib.sh`
- Sets `UPDATE_SNAPSHOTS=true` and runs file-based integration tests to update snapshots
- On success: Shows completion summary
- On failure: Shows last 80 lines + points to full log

**Warning:** This modifies `.expected.cs` and stdlib `.cs` files. Review the changes with `git diff` afterwards.

**Log location:** `.claude/tmp/last-snapshot-regen.log`

**Workflow:**
1. Run `/regenerate-snapshots` to update all generated artifacts
2. Run `git diff` to review changes
3. If changes are intentional: `git add . && git commit`
4. If changes are unintentional: `git checkout -- src/**/*.expected.cs src/Sharpy.Stdlib/`

## Steps

1. Run `mkdir -p .claude/tmp` to ensure log directory exists
2. Clear the old log with `rm -f .claude/tmp/last-snapshot-regen.log`
3. Build first: `dotnet build sharpy.sln --nologo -v q >> .claude/tmp/last-snapshot-regen.log 2>&1`. If build fails, print "=== BUILD FAILED — cannot regenerate ===" then `tail -30 .claude/tmp/last-snapshot-regen.log` and stop.
4. Regenerate spy stdlib: `bash build_tools/regenerate_spy_stdlib.sh >> .claude/tmp/last-snapshot-regen.log 2>&1`. If it fails, print "=== STDLIB REGENERATION FAILED (last 30 lines) ===" then `tail -30 .claude/tmp/last-snapshot-regen.log` and stop.
5. Run: `UPDATE_SNAPSHOTS=true dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests" --no-build >> .claude/tmp/last-snapshot-regen.log 2>&1`
6. Check exit code:
   - Exit 0: Print "=== REGENERATION COMPLETE (snapshots + stdlib) ===" then `tail -30 .claude/tmp/last-snapshot-regen.log`
   - Exit non-zero: Print "=== SNAPSHOT REGENERATION FAILED (last 80 lines) ===" then `tail -80 .claude/tmp/last-snapshot-regen.log`, then echo "=== Full log: .claude/tmp/last-snapshot-regen.log ==="
7. Return the actual exit code
