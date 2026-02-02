# LSP/VS Code Extension

**Location:** `lsp/sharpy/`

**Status:** Planned for future development. Not yet implemented.

## When Contributing

Once implemented, this will provide:
- Syntax highlighting for `.spy` files
- IntelliSense and autocompletion
- Go to definition
- Error highlighting
- Hover documentation

## Planned Architecture

The extension will use the Language Server Protocol (LSP) to communicate with a Sharpy language server that reuses the compiler frontend (Lexer, Parser, Semantic).
