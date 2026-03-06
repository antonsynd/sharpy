# Sharpy for Visual Studio Code

Sharpy language support for VS Code, powered by the Sharpy Language Server Protocol (LSP) implementation.

## Features

- **Diagnostics** — Real-time error and warning reporting as you type
- **Hover** — Type information and symbol documentation on hover
- **Go to Definition** — Ctrl+Click / F12 to jump to symbol definitions
- **Syntax Highlighting** — TextMate grammar for `.spy` files

## Requirements

The `sharpyc` CLI must be installed and available in your PATH, or configured via the `sharpy.serverPath` setting.

## Extension Settings

| Setting | Description | Default |
|---------|-------------|---------|
| `sharpy.serverPath` | Path to the `sharpyc` executable | `""` (uses PATH) |
| `sharpy.trace.server` | Trace communication with the language server | `"off"` |

## Getting Started

1. Install the Sharpy compiler (`sharpyc`)
2. Install this extension
3. Open a `.spy` file — the language server starts automatically
