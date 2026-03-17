---
name: spy-run
description: Compile and run a Sharpy source file or inline source code
argument-hint: "<file.spy or inline source>"
---

Compile and execute Sharpy source. Accepts either a file path or inline source code.

**Usage:**
- `/spy-run path/to/file.spy` — run a .spy file
- `/spy-run x: int = 42\nprint(x)` — run inline source (no temp file management needed by caller)

**Behavior:**
- Shows full program output on success
- Shows last 80 lines of errors on failure + points to full log

**Log location:** `.claude/tmp/last-spy-run.log`

## Steps

1. Validate `$ARGUMENTS` is non-empty.
2. Determine if the argument is a **file path** or **inline source**:
   - **File path**: The argument ends in `.spy` AND the file exists on disk. Use it directly.
   - **Inline source**: Anything else. Use the **Write tool** to write the source to `$TMPDIR/sharpy-run-temp.spy`. Do NOT use bash echo/heredoc — always use the Write tool to avoid shell escaping issues with `#`, backticks, and other special characters.
3. Run `mkdir -p .claude/tmp` to ensure log directory exists.
4. Clear the old log with `rm -f .claude/tmp/last-spy-run.log`.
5. Run: `dotnet run --project src/Sharpy.Cli -- run "<source_file>" > .claude/tmp/last-spy-run.log 2>&1` (with `dangerouslyDisableSandbox: true`).
6. Check exit code:
   - Exit 0: Print "=== RUN OUTPUT ===" then show contents of `.claude/tmp/last-spy-run.log`
   - Exit non-zero: Print "=== RUN FAILED (last 80 lines) ===" then show `tail -80 .claude/tmp/last-spy-run.log`, then "Full log: .claude/tmp/last-spy-run.log"
7. If a temp file was created, clean it up with `rm -f $TMPDIR/sharpy-run-temp.spy`.
8. Return the actual exit code.
