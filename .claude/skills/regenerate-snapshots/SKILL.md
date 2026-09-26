---
name: regenerate-snapshots
description: Regenerate C# snapshot tests and spy stdlib after intentional codegen changes
---

Regenerate all generated artifacts after intentional codegen changes:
1. `.expected.cs` snapshot files for file-based integration tests
2. `.spy` stdlib generated C# files (`src/Sharpy.Stdlib/`)
3. `.spy` stdlib TEST generated C# files (`src/Sharpy.Stdlib.Tests/Spy/generated/`)

**Usage:** `/regenerate-snapshots`

**Behavior:**
- Builds the solution
- Regenerates spy stdlib C# via `build_tools/regenerate_spy_stdlib.sh`
- Regenerates spy-test C# via `build_tools/regenerate_spy_tests.sh` (codegen changes alter the generated test C# even when no `.spy` file changed — CI gate `check_spy_tests_staleness.sh`)
- Sets `UPDATE_SNAPSHOTS=true` and runs file-based integration tests to update snapshots
- On success: Shows completion summary
- On failure: Shows last 80 lines + points to full log

**Warning:** This modifies `.expected.cs`, stdlib `.cs` and spy-test `.cs` files. Review the changes with `git diff` afterwards — a regenerated diff is a finding until each hunk is explained.

**Log location:** `.claude/tmp/last-snapshot-regen.log`

**Workflow:**
1. Run `/regenerate-snapshots` to update all generated artifacts
2. Run `git diff --stat` and read the diff
3. If changes are intentional: stage the regenerated files by explicit pathspec (never `git add .` — the working tree may hold a peer's work) and commit
4. If changes are unintentional: do NOT `git checkout`/`git restore` the generated paths (a shared tree may hold peer edits in them, and a silent revert leaves no reflog). Fix the emitter, rebuild, and re-run `/regenerate-snapshots`; the regenerated files then return to their committed bytes, which `git status` confirms.

## Steps

1. Run `mkdir -p .claude/tmp` to ensure log directory exists
2. Clear the old log with `rm -f .claude/tmp/last-snapshot-regen.log`
3. Build first: `.claude/scripts/dotnet-serialized build sharpy.sln --nologo -v q >> .claude/tmp/last-snapshot-regen.log 2>&1` (`dangerouslyDisableSandbox: true`). If build fails, print "=== BUILD FAILED — cannot regenerate ===" then `tail -30 .claude/tmp/last-snapshot-regen.log` and stop.
4. Regenerate spy stdlib: `bash build_tools/regenerate_spy_stdlib.sh >> .claude/tmp/last-snapshot-regen.log 2>&1`. If it fails, print "=== STDLIB REGENERATION FAILED (last 30 lines) ===" then `tail -30 .claude/tmp/last-snapshot-regen.log` and stop.
5. Regenerate spy tests: `bash build_tools/regenerate_spy_tests.sh >> .claude/tmp/last-snapshot-regen.log 2>&1`. If it fails, print "=== SPY-TEST REGENERATION FAILED (last 30 lines) ===" then `tail -30 .claude/tmp/last-snapshot-regen.log` and stop. (Both regen scripts invoke `dotnet run` directly; when parallel agents are running dotnet, point them at the built apphost instead — a copy of the script with `SHARPYC=<repo>/src/Sharpy.Cli/bin/Debug/net10.0/sharpyc` — so they do not bypass the serialized lock.)
6. Run: `UPDATE_SNAPSHOTS=true .claude/scripts/dotnet-serialized test --filter "FullyQualifiedName~FileBasedIntegrationTests" --no-build >> .claude/tmp/last-snapshot-regen.log 2>&1`
7. Check exit code:
   - Exit 0: Print "=== REGENERATION COMPLETE (snapshots + stdlib + spy tests) ===" then `tail -30 .claude/tmp/last-snapshot-regen.log`
   - Exit non-zero: Print "=== SNAPSHOT REGENERATION FAILED (last 80 lines) ===" then `tail -80 .claude/tmp/last-snapshot-regen.log`, then echo "=== Full log: .claude/tmp/last-snapshot-regen.log ==="
8. Return the actual exit code
