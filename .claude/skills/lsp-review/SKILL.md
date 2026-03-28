---
name: lsp-review
description: Interactive LSP review session — user reports hover/coloring issues from VS Code, Claude files GitHub issues
argument-hint: "[fixture path or directory]"
---

Start an interactive LSP review session. The user opens test fixture `.spy` files in VS Code and reports issues they observe with hover tooltips, syntax coloring, type inference, diagnostics, etc. For each reported issue, investigate the root cause and create a GitHub issue (or add to an existing one if it's the same bug).

**Usage:**
- `/lsp-review` — start a session, user provides fixtures one at a time
- `/lsp-review src/Sharpy.Compiler.Tests/Integration/TestFixtures/async/` — start with a specific directory context

## Session Protocol

1. **Wait for the user** to provide a fixture path and describe what they see (wrong hover, missing tooltip, bad coloring, incorrect type, red squiggles, etc.)
2. **Read the fixture file** to understand the code context
3. **Investigate the root cause** by reading relevant LSP handler code, semantic analysis, or parser code
4. **Check existing issues** before creating new ones — search open GitHub issues for related bugs
5. **Create or update a GitHub issue** with:
   - Clear reproduction steps referencing the specific fixture and line
   - Root cause analysis (which file/function is responsible)
   - Expected vs actual behavior
   - Related issues cross-linked
6. **Maintain a running tally** of all issues created/updated in the session
7. **Repeat** for the next fixture the user provides

## Issue Creation Rules

- **Always run `gh` with `dangerouslyDisableSandbox: true`** (required due to TLS cert issues in sandbox)
- **Check for duplicates first**: Search existing issues before creating. If the same root cause was already filed, add a comment with the new reproduction case instead.
- **Cross-link related issues**: Add `## Related` sections linking issues that share root causes or categories
- **Be specific in root cause**: Name the exact file, function, and line number responsible. Don't just describe symptoms.
- **Use consistent title format**: `LSP: <concise description>` for LSP issues, plain titles for compiler/spec issues

## Common Issue Categories

From prior sessions, these are recurring patterns to watch for:

| Pattern | Typical Root Cause |
|---------|-------------------|
| No hover at declaration sites (functions, variables, fields, params) | Name is a `string` not an `Identifier` node; `AstPositionService` can't find it; `HoverHandler` missing case |
| `<?>` / UnknownType on expressions | Semantic analysis type resolution gap (e.g., `async with` bindings, `Task.Result`) |
| Wrong syntax coloring for keywords | `SemanticTokensHandler` doesn't emit `TKeyword` tokens; TextMate grammar may be suppressed |
| C# type names leaking (double, long) | `SemanticType` field `Name` uses C# name instead of Sharpy name |
| Missing documentation in hover | XML doc comments not flowing through `XmlDocReader` -> `Symbol.Documentation` pipeline |
| Type annotations not hoverable | `TypeAnnotation` is not a `Node` subclass; position matching needs to recurse into type args |
| Operator/keyword hover showing raw type | Generic `Expression` catch-all in `HoverHandler` is too broad |

## Running Tally

Maintain and display a markdown table after each issue creation/update:

```
| # | Issue | Fixture(s) | Category | Status |
|---|-------|-----------|----------|--------|
| 123 | brief title | fixture_name.spy | Category | OPEN/CLOSED |
```

## Key Files for Investigation

- `src/Sharpy.Lsp/Handlers/HoverHandler.cs` — hover tooltip logic
- `src/Sharpy.Lsp/Handlers/SemanticTokensHandler.cs` — syntax coloring
- `src/Sharpy.Lsp/SymbolFormatter.cs` — hover text formatting
- `src/Sharpy.Lsp/ModuleDocumentation.cs` — module summary lookup
- `src/Sharpy.Compiler/Services/AstPositionService.cs` — cursor position to AST node
- `src/Sharpy.Compiler/Semantic/TypeChecker*.cs` — type inference
- `src/Sharpy.Compiler/Semantic/SemanticType.cs` — type definitions and display names
- `src/Sharpy.Compiler/Parser/Ast/Statement.cs` — AST node definitions
- `src/Sharpy.Compiler/Parser/Ast/Types.cs` — `TypeAnnotation` record
- `editors/vscode/syntaxes/sharpy.tmLanguage.json` — TextMate grammar
