# Sharpy VSCode Extension

## Installation

### From Marketplace

Search for **Sharpy** in the VSCode Extensions panel (`Ctrl+Shift+X` / `Cmd+Shift+X`) and click Install.

### Manual Install

Build the extension from source and install the `.vsix` file:

```bash
cd editors/vscode
npm install
npm run package
code --install-extension sharpy-*.vsix
```

## Features

- **Syntax highlighting** -- TextMate grammar for `.spy` files
- **LSP integration** -- Full language server support via `sharpyc lsp`
- **Code snippets** -- Common patterns (class, function, main, etc.)
- **Language configuration** -- Bracket matching, comment toggling, auto-closing pairs

## Settings Reference

All settings are prefixed with `sharpy.`.

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `sharpy.server.path` | `string` | `"sharpyc"` | Path to the `sharpyc` executable |
| `sharpy.server.args` | `string[]` | `["lsp"]` | Arguments passed to the language server |
| `sharpy.trace.server` | `string` | `"off"` | Trace level for LSP communication (`"off"`, `"messages"`, `"verbose"`) |
| `sharpy.diagnostics.enabled` | `boolean` | `true` | Enable compiler diagnostics |
| `sharpy.inlayHints.typeAnnotations` | `boolean` | `true` | Show inferred type annotations |
| `sharpy.inlayHints.parameterNames` | `boolean` | `true` | Show parameter name hints in function calls |
| `sharpy.codeLens.enabled` | `boolean` | `true` | Show reference counts and run buttons |
| `sharpy.formatting.enabled` | `boolean` | `true` | Enable document formatting |

## Commands

| Command | Title | Description |
|---------|-------|-------------|
| `sharpy.restartServer` | Sharpy: Restart Language Server | Restart the LSP server process |
| `sharpy.showOutputChannel` | Sharpy: Show Output | Open the Sharpy output channel for server logs |

## Keyboard Shortcuts

Standard VSCode/LSP shortcuts apply:

| Shortcut | Action |
|----------|--------|
| `F2` | Rename symbol |
| `F12` | Go to definition |
| `Shift+F12` | Find all references |
| `Ctrl+Shift+O` / `Cmd+Shift+O` | Go to symbol in file |
| `Ctrl+T` / `Cmd+T` | Go to symbol in workspace |
| `Ctrl+Space` | Trigger completion |
| `Ctrl+Shift+Space` | Trigger signature help |
| `Ctrl+.` / `Cmd+.` | Quick fix (code action) |
| `Ctrl+Shift+I` / `Cmd+Shift+I` | Format document |

## Troubleshooting

### Check the Output Channel

Open the Sharpy output channel via the command palette: **Sharpy: Show Output**. This displays LSP server logs including startup messages, errors, and request traces.

### Enable Trace Logging

Set `sharpy.trace.server` to `"verbose"` in your settings to see full JSON-RPC message traffic:

```json
{
  "sharpy.trace.server": "verbose"
}
```

### Restart the Server

If the language server becomes unresponsive, restart it via the command palette: **Sharpy: Restart Language Server**.

### Verify Server Path

Ensure `sharpyc` is on your PATH or set `sharpy.server.path` to the full path:

```json
{
  "sharpy.server.path": "/path/to/sharpyc"
}
```

### Common Issues

| Issue | Solution |
|-------|----------|
| No diagnostics appearing | Check that `sharpy.diagnostics.enabled` is `true` and the server is running |
| Server not starting | Verify `sharpyc` is installed and the path is correct |
| Slow completions | Check the output channel for compilation errors slowing analysis |
| Extension not activating | Ensure the file has a `.spy` extension |
