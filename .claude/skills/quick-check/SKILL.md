---
name: quick-check
description: Emit C# and run a .spy file or inline source in one shot
argument-hint: "<file.spy or inline source>"
---

Combined emit-csharp + run for quick debugging. Shows what C# is generated AND what the program outputs, in one invocation.

**Usage:**
- `/quick-check path/to/file.spy` — check a .spy file
- `/quick-check x: int = 42\nprint(x)` — check inline source

**Behavior:**
- Shows generated C# (last 100 lines), then runs the program
- Shows both outputs for quick comparison

**Log locations:** `.claude/tmp/last-quick-check-emit.log`, `.claude/tmp/last-quick-check-run.log`

## Steps

1. Validate `$ARGUMENTS` is non-empty.
2. Determine if the argument is a **file path** or **inline source**:
   - **File path**: The argument ends in `.spy` AND the file exists on disk. Use it directly.
   - **Inline source**: Anything else. Use the **Write tool** to write the source to `$TMPDIR/sharpy-quick-check-temp.spy`. Do NOT use bash echo/heredoc — always use the Write tool to avoid shell escaping issues with `#`, backticks, and other special characters.
3. Run `mkdir -p .claude/tmp` to ensure log directory exists.

**Phase 1 — Emit C#:**

4. Run: `dotnet run --project src/Sharpy.Cli -- emit csharp "<source_file>" > .claude/tmp/last-quick-check-emit.log 2>&1` (with `dangerouslyDisableSandbox: true`).
5. If exit 0: Print "=== GENERATED C# (last 100 lines) ===" then show `tail -100 .claude/tmp/last-quick-check-emit.log`
6. If exit non-zero: Print "=== C# EMIT FAILED (last 80 lines) ===" then show `tail -80 .claude/tmp/last-quick-check-emit.log`. Skip Phase 2.

**Phase 2 — Run:**

7. Run: `dotnet run --project src/Sharpy.Cli -- run "<source_file>" > .claude/tmp/last-quick-check-run.log 2>&1` (with `dangerouslyDisableSandbox: true`).
8. If exit 0: Print "=== RUN OUTPUT ===" then show contents of `.claude/tmp/last-quick-check-run.log`
9. If exit non-zero: Print "=== RUN FAILED (last 80 lines) ===" then show `tail -80 .claude/tmp/last-quick-check-run.log`

**Cleanup:**

10. If a temp file was created, clean it up with `rm -f $TMPDIR/sharpy-quick-check-temp.spy`.
11. Print full log paths for both phases.
