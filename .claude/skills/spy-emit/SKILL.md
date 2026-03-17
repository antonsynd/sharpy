---
name: spy-emit
description: Emit compiler output (csharp, ast, tokens, diagnostics) from a .spy file or inline source
argument-hint: "<mode> <file.spy or inline source>"
---

Emit compiler output from Sharpy source. Supports all emit modes and both file paths and inline source code.

**Usage:**
- `/spy-emit csharp path/to/file.spy` — emit generated C#
- `/spy-emit ast path/to/file.spy` — emit parsed AST
- `/spy-emit tokens path/to/file.spy` — emit lexer tokens
- `/spy-emit diagnostics path/to/file.spy` — emit compiler diagnostics

**Inline source** (no file needed — avoids bash escaping issues):
- `/spy-emit csharp x: int = 42\nprint(x)`

**Behavior:**
- Shows last 100 lines of output on success
- Shows last 80 lines of errors on failure + points to full log

**Log location:** `.claude/tmp/last-spy-emit.log`

## Steps

1. Parse `$ARGUMENTS` to extract the mode (first word: `csharp`, `ast`, `tokens`, or `diagnostics`) and the remainder as the source argument.
2. If mode is missing or not one of the four valid modes, print an error and stop.
3. Determine if the source argument is a **file path** or **inline source**:
   - **File path**: The argument ends in `.spy` AND the file exists on disk. Use it directly.
   - **Inline source**: Anything else. Use the **Write tool** to write the source to `$TMPDIR/sharpy-emit-temp.spy`. Do NOT use bash echo/heredoc — always use the Write tool to avoid shell escaping issues with `#`, backticks, and other special characters.
4. Run `mkdir -p .claude/tmp` to ensure log directory exists.
5. Clear the old log with `rm -f .claude/tmp/last-spy-emit.log`.
6. Run: `dotnet run --project src/Sharpy.Cli -- emit <mode> "<source_file>" > .claude/tmp/last-spy-emit.log 2>&1` (with `dangerouslyDisableSandbox: true`).
7. Check exit code:
   - Exit 0: Print "=== <MODE> OUTPUT (last 100 lines) ===" then show `tail -100 .claude/tmp/last-spy-emit.log`, then "Full output: .claude/tmp/last-spy-emit.log"
   - Exit non-zero: Print "=== EMIT FAILED (last 80 lines) ===" then show `tail -80 .claude/tmp/last-spy-emit.log`, then "Full log: .claude/tmp/last-spy-emit.log"
8. If a temp file was created, clean it up with `rm -f $TMPDIR/sharpy-emit-temp.spy`.
9. Return the actual exit code.
