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
| `textDocument/codeAction` | Quick fixes for naming conventions, unused imports, unused variables |

### Display

| Method | Description |
|--------|-------------|
| `textDocument/semanticTokens` | Semantic highlighting (types, functions, parameters, etc.) |
| `textDocument/foldingRange` | Code folding for classes, functions, and block statements |
| `textDocument/codeLens` | Reference counts on symbols; run buttons for entry points |
| `textDocument/formatting` | Indentation normalization |

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
