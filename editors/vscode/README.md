# Sharpy for Visual Studio Code

Sharpy language support for VS Code, powered by the Sharpy Language Server Protocol (LSP) implementation.

## Features

- **Diagnostics** -- Real-time error and warning reporting as you type
- **Hover** -- Type information and symbol documentation on hover
- **Go to Definition** -- Ctrl+Click / F12 to jump to symbol definitions
- **Syntax Highlighting** -- TextMate grammar for `.spy` files
- **Snippets** -- Code snippets for common constructs (`def`, `class`, `if`, `for`, `match`, `try`, `with`, `async def`)
- **Status Bar** -- Language server status indicator

## Requirements

The `sharpyc` CLI must be installed and available in your PATH, or configured via the `sharpy.serverPath` setting.

## Extension Settings

| Setting | Description | Default |
|---------|-------------|---------|
| `sharpy.serverPath` | Path to the `sharpyc` executable | `""` (uses PATH) |
| `sharpy.trace.server` | Trace communication with the language server | `"off"` |
| `sharpy.lsp.maxNumberOfProblems` | Maximum number of problems to show | `100` |
| `sharpy.format.indentSize` | Number of spaces per indent level | `4` |
| `sharpy.inlayHints.typeAnnotations` | Show inferred type annotations | `true` |
| `sharpy.inlayHints.parameterNames` | Show parameter names at call sites | `true` |

## Commands

| Command | Description |
|---------|-------------|
| `Sharpy: Restart Language Server` | Stop and restart the language server |
| `Sharpy: Show Output Channel` | Show the language server output channel |

## Getting Started

1. Install the Sharpy compiler (`sharpyc`)
2. Install this extension
3. Open a `.spy` file -- the language server starts automatically

## Debugging Sharpy Programs

VS Code does not yet include a built-in Debug Adapter Protocol (DAP) integration for Sharpy. To debug compiled Sharpy programs:

1. Compile your `.spy` file to a .NET assembly using `sharpyc run --emit-dll`
2. Use the **C# Dev Kit** or **.NET Core Launch** debug configuration to attach to the resulting .NET process
3. Alternatively, use `Console.WriteLine` / `print()` for simple debugging
