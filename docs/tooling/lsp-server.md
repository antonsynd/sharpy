# Sharpy LSP Server

## Overview

Sharpy includes a built-in Language Server Protocol (LSP) server, accessible via `sharpyc lsp`. The server is implemented in the `Sharpy.Lsp` project using [OmniSharp.Extensions.LanguageServer](https://github.com/OmniSharp/csharp-language-server-protocol), providing IDE features to any editor that supports LSP.

## Architecture

The LSP server consists of three main layers:

- **SharpyWorkspace** -- Manages document state (open files, edits, file versions). Tracks document content in memory and synchronizes with the compiler.
- **CompilerApi** -- Provides analysis services (parsing, type checking, diagnostics) by invoking the Sharpy compiler pipeline on demand.
- **Handlers** -- Implement individual LSP protocol methods, delegating to CompilerApi for analysis and returning results in the LSP wire format.

```
Editor <-> stdio (JSON-RPC) <-> LSP Server
                                  |
                                  +-- Handlers (one per LSP method)
                                  |     |
                                  |     +-- CompilerApi (analysis)
                                  |           |
                                  |           +-- Sharpy Compiler Pipeline
                                  |
                                  +-- SharpyWorkspace (document state)
```

## Supported Features

### Document Synchronization

| Method | Description |
|--------|-------------|
| `textDocument/didOpen` | Track newly opened documents |
| `textDocument/didChange` | Apply incremental edits |
| `textDocument/didClose` | Release document state |
| `textDocument/didSave` | Trigger full re-analysis on save |

### Diagnostics

| Method | Description |
|--------|-------------|
| `textDocument/publishDiagnostics` | Push compiler errors, warnings, and info to the editor |

Diagnostics are published after each document change (debounced) and include all `SPY`-prefixed diagnostic codes from the compiler.

**In project mode** (a `.spyproj` in the workspace root), each source the project lists shows the diagnostics that the project's analysis reports for that file. That analysis is everything `sharpyc project` checks before code generation: parsing, name and import resolution, type checking and validation. Code-generation diagnostics (`SPY05xx`, `SPY0908` and C# compiler errors such as a missing entry point) appear only when the project is built.

- A file's squiggles are its own project-analysis diagnostics, including import, declaration, inheritance and cycle errors (`SPY0300`, `SPY0313`, `SPY0204`, `SPY0302`). The project's `<NoWarn>` and `<WarningsAsErrors>` apply.
- A project-analysis diagnostic that names no file appears on the entry file. That is the configured `<EntryPoint>`. Without one, an executable uses its `main` file, and a library its first listed source.
- Every project file is republished after each project analysis: initial indexing, an edit, a watched-file change (including creating a missing module), a `.spyproj` reload and a `sharpy.features` change. A hints-only configuration change republishes without reanalyzing.
- A project that fails to compile still publishes: indexing with errors puts each error on its file. An analysis superseded by a newer edit publishes nothing.
- Opening or editing a project file never replaces its project diagnostics with a single-file analysis. A file opened before indexing finishes shows a single-file result until the index publishes.
- Closing a project file keeps its diagnostics when the buffer matched the file on disk. If the buffer had unsaved changes, the project is re-analyzed from disk and republished. Closing any other file clears its diagnostics.
- An open project file's unsaved buffer is analyzed as that file, but files that import it are still checked against its disk text (#2304).
- When a `.spyproj` reload drops a file from the project, its project diagnostics are cleared. If the file is open, it shows its single-file result instead.
- A file outside the project that imports a project file shows only its own diagnostics. It never overwrites the imported file's squiggles.

### Navigation

| Method | Description |
|--------|-------------|
| `textDocument/definition` | Go-to-definition using `Symbol.DeclarationSpan` |
| `textDocument/references` | Find all references using SemanticInfo reference tracking |
| `textDocument/documentSymbol` | Hierarchical document outline (classes, functions, variables) |
| `textDocument/documentHighlight` | Highlight all occurrences of a symbol in the current document |
| `workspace/symbol` | Workspace-wide symbol search |

### Intelligence

| Method | Description |
|--------|-------------|
| `textDocument/hover` | Type information for identifiers, expressions, and function calls |
| `textDocument/completion` | Scope-aware, member, and type completion |
| `textDocument/signatureHelp` | Parameter hints during function calls |
| `textDocument/inlayHint` | Inferred type and parameter name annotations |

### Refactoring

| Method | Description |
|--------|-------------|
| `textDocument/rename` | Symbol rename with validation (rejects invalid names) |
| `textDocument/codeAction` | Quick fixes and refactorings: the actions listed under [Code actions](#code-actions) |

### Code actions

Each provider offers these actions (titles as the editor shows them; `<name>` and `<type>` are filled in):

| Provider | Actions |
|----------|---------|
| Quick fixes (from a diagnostic) | `Remove unused import` · `Prefix with '_' to mark as intentionally unused` · `Rename to '<name>'` |
| Organize imports | `Organize Imports` |
| Implement interface | `Implement interface '<name>'` · `Implement all interfaces` |
| Extract variable | `Extract variable 'result'` (or `'result: <type>'` when the type is known) |
| Extract method | `Extract method` |
| Convert forms | `Convert to match statement` · `Convert to if/elif/else` · `Add type annotation ': <type>'` · `Remove type annotation` · `Wrap in try/except` |
| Inline | `Inline variable '<name>'` · `Inline function '<name>'` |

#### Code actions keep the source shape

An action that moves or rewrites code you wrote keeps it the way you wrote it:

- **Moved lines keep their comments, blank lines and relative indentation.** A nested block moved into a `try:` or a
  `case` keeps its shape; only the whole region moves by one indentation step. Organize Imports is the one action
  that rebuilds a layout: it writes the sorted imports as one contiguous block, so a blank line *between* two imports
  is not kept, while the blank lines after the block are (`OrganizeImportsUnitTests.Cell22_…`).
- **A multi-line string's lines never move.** Re-indenting them would change the string's value.
- **A deleted statement takes its trailing comment with it.** Removing an unused import, or inlining a variable,
  deletes the comment on that line too; a comment on its own line above it stays.
- **The document's line breaks are kept.** In a document that uses `\r\n` or `\r`, every line an action writes ends
  the same way.
- **Where no legal text exists, the action is not offered.** No action turns a document that parses into one that
  does not. The known exceptions: Extract method and Extract variable can still spell an inferred parameter or
  variable type the parser does not accept (for example a builtin's `Callable[...]` type, or a bare list type), and
  Extract method can take a builtin or a name the selection binds as a parameter
  ([#2294](https://github.com/antonsynd/sharpy/issues/2294)); and Convert to match re-spells a comparand from
  the literal's value, so a raw-string comparand (`if x == r"\d":`) becomes `case r"\\d":`, which is not a
  pattern ([#2296](https://github.com/antonsynd/sharpy/issues/2296)). Wrap in try/except and Extract method are
  also not offered when the selection declares a nested `def` or `type` alias that is used after it (inside the new
  block the name would be out of scope there), or when it starts with a decorated declaration (the decorator sits
  above the lines that move).

Organize Imports on

```python
import sys
# string helpers
from os.path import (
    join,  # path joining
    basename,
)
import math  # numeric

def main():
    print(math.floor(1.5), join("a", "b"), basename("/x/y"), len(sys.argv) >= 0)
```

gives

```python
import math  # numeric
import sys
# string helpers
from os.path import (
    join,  # path joining
    basename,
)

def main():
    print(math.floor(1.5), join("a", "b"), basename("/x/y"), len(sys.argv) >= 0)
```

The comment above an import travels with it, the parenthesized import keeps its parentheses and inner comment, and
the blank line before `def main()` stays. Wrap in try/except, with lines 2–4 selected in

```python
def main():
    x = 1
    if x > 0:  # positive
        print(x)
    print("done")
```

gives

```python
def main():
    try:
        x = 1
        if x > 0:  # positive
            print(x)
    except Exception as e:
        raise
    print("done")
```

These texts are what the server returned for these documents. The tests
`OrganizeImportsUnitTests.Cell1_TheIssuesRepro_MovesUnitsVerbatim` and
`RegionMoverTests.Wrap_Cell3_NestedBodyKeepsItsRelativeIndentation` pin them byte for byte, and
`ProtocolTests.CodeAction_OrganizeImports_TheIssuesRepro_AppliesByteExact` /
`ProtocolTests.CodeAction_WrapInTryExcept_TheIssuesRepro_AppliesByteExact` pin them over the wire. The
code-action shape sweep (`CodeActionShapeSweepTests`) checks the rules above for every action over the test corpus.

### Display

| Method | Description |
|--------|-------------|
| `textDocument/semanticTokens` | Semantic highlighting (types, functions, parameters, etc.) |
| `textDocument/foldingRange` | Code folding for classes, functions, and block statements |
| `textDocument/codeLens` | Reference counts on symbols; run buttons for entry points |
| `textDocument/formatting` | Format the document with the same formatter as `sharpyc format` (see [The Sharpy Formatter](formatter.md)); no edits when formatting is declined (SPY0912), indentation-only fallback for a document that does not parse |
| `textDocument/rangeFormatting` | Format the selected lines, with the same rules |

## Transport

The LSP server communicates over **stdio** (stdin/stdout) using the JSON-RPC 2.0 protocol. This is the standard transport for LSP and works with all major editors.

```bash
sharpyc lsp
```

The server reads JSON-RPC messages from stdin and writes responses to stdout. Logging and diagnostics go to stderr.

## Threading Model

- **Document map**: `ConcurrentDictionary<Uri, Document>` for thread-safe document access
- **Per-document lock**: `SemaphoreSlim` per document prevents concurrent analysis of the same file
- **Debounce**: 300ms debounce on `didChange` events to avoid redundant re-analysis during rapid typing

## Configuration

The server accepts configuration via the LSP `workspace/didChangeConfiguration` notification. Settings are typically configured through the editor (e.g., VSCode settings). See [vscode-extension.md](vscode-extension.md) for the full settings reference.
