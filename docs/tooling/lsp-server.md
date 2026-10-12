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
  does not. The known exception: Extract method and Extract variable can still spell an inferred parameter or
  variable type the parser does not accept (for example a builtin's `Callable[...]` type, or a bare list type), and
  Extract method can take a builtin or a name the selection binds as a parameter
  ([#2294](https://github.com/antonsynd/sharpy/issues/2294)).

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
`ProtocolTests.CodeAction_OrganizeImports_TheIssuesRepro_AppliesByteExact` pins the first one over the wire. The
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
