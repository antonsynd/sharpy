---
name: lsp-hover
description: Get LSP hover tooltip for a position in a .spy file (emulates VS Code hover)
argument-hint: "<file.spy> <line> <col>"
---

Query the hover tooltip that VS Code would show at a specific position in a Sharpy source file. Useful for verifying type inference, symbol resolution, and hover formatting without opening VS Code.

**Usage:**
- `/lsp-hover path/to/file.spy 18 9` — hover at line 18, column 9
- `/lsp-hover path/to/file.spy 18:9` — alternate format with colon separator

**Inline source** (no file needed):
- `/lsp-hover "x: int = 42\nprint(x)" 2 1` — hover over `print` at line 2, column 1

**Behavior:**
- Shows the hover markdown (same as VS Code tooltip)
- Shows `(no hover)` if no hover information at that position
- Shows compilation errors on stderr if the file has errors

**Log location:** `.claude/tmp/last-lsp-hover.log`

## Steps

1. Parse `$ARGUMENTS` to extract the source, line, and column:
   - Split arguments. The last two tokens are line and column (or a single `line:col` token).
   - Everything before that is the source argument.
2. Determine if the source argument is a **file path** or **inline source**:
   - **File path**: The argument ends in `.spy` AND the file exists on disk. Use it directly.
   - **Inline source**: Anything else. Use the **Write tool** to write the source to `$TMPDIR/sharpy-hover-temp.spy`. Do NOT use bash echo/heredoc — always use the Write tool to avoid shell escaping issues.
3. Parse the position: if the position argument contains `:`, split on `:` to get line and col. Otherwise, use the two separate arguments.
4. Run `mkdir -p .claude/tmp` to ensure log directory exists.
5. Clear the old log with `rm -f .claude/tmp/last-lsp-hover.log`.
6. Run: `dotnet run --project src/Sharpy.Cli -- emit hover "<source_file>" --line <line> --col <col> > .claude/tmp/last-lsp-hover.log 2>&1` (with `dangerouslyDisableSandbox: true`).
7. Check exit code:
   - Exit 0: Show the full contents of `.claude/tmp/last-lsp-hover.log`
   - Exit non-zero: Print "=== HOVER FAILED ===" then show `.claude/tmp/last-lsp-hover.log`, then "Full log: .claude/tmp/last-lsp-hover.log"
8. If a temp file was created, clean it up with `rm -f $TMPDIR/sharpy-hover-temp.spy`.
9. Return the actual exit code.
