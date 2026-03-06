# Editor Integration

Any editor supporting the Language Server Protocol can connect to the Sharpy language server. The server communicates over stdio using JSON-RPC.

```bash
sharpyc lsp
```

See [lsp-server.md](lsp-server.md) for the full list of supported LSP features.

## Neovim

### Using nvim-lspconfig

Add a custom server configuration:

```lua
local lspconfig = require('lspconfig')
local configs = require('lspconfig.configs')

-- Register the Sharpy language server
if not configs.sharpy then
  configs.sharpy = {
    default_config = {
      cmd = { 'sharpyc', 'lsp' },
      filetypes = { 'sharpy' },
      root_dir = function(fname)
        return lspconfig.util.find_git_ancestor(fname)
          or lspconfig.util.root_pattern('*.spyproj')(fname)
      end,
      settings = {},
    },
  }
end

lspconfig.sharpy.setup({})
```

### Filetype Detection

Add `.spy` filetype detection in `~/.config/nvim/filetype.lua`:

```lua
vim.filetype.add({
  extension = {
    spy = 'sharpy',
  },
})
```

### Optional: Tree-sitter Highlighting

If no tree-sitter grammar is available for Sharpy, Python highlighting is a reasonable fallback:

```lua
vim.treesitter.language.register('python', 'sharpy')
```

## Emacs

### Using eglot (built-in, Emacs 29+)

```elisp
;; Define a major mode for .spy files
(define-derived-mode sharpy-mode python-mode "Sharpy"
  "Major mode for Sharpy (.spy) files.")

(add-to-list 'auto-mode-alist '("\\.spy\\'" . sharpy-mode))

;; Register the LSP server with eglot
(with-eval-after-load 'eglot
  (add-to-list 'eglot-server-programs
               '(sharpy-mode "sharpyc" "lsp")))
```

### Using lsp-mode

```elisp
(require 'lsp-mode)

(define-derived-mode sharpy-mode python-mode "Sharpy"
  "Major mode for Sharpy (.spy) files.")

(add-to-list 'auto-mode-alist '("\\.spy\\'" . sharpy-mode))

(lsp-register-client
 (make-lsp-client
  :new-connection (lsp-stdio-connection '("sharpyc" "lsp"))
  :major-modes '(sharpy-mode)
  :server-id 'sharpy-lsp))

(add-hook 'sharpy-mode-hook #'lsp)
```

## Sublime Text

### Using the LSP Package

1. Install the [LSP](https://packagecontrol.io/packages/LSP) package via Package Control
2. Open **Preferences > Package Settings > LSP > Settings** and add:

```json
{
  "clients": {
    "sharpy": {
      "enabled": true,
      "command": ["sharpyc", "lsp"],
      "selector": "source.sharpy",
      "schemes": ["file"]
    }
  }
}
```

3. Create a `.sublime-syntax` file or use Python syntax as a fallback for `.spy` files.

## Helix

Add to `~/.config/helix/languages.toml`:

```toml
[[language]]
name = "sharpy"
scope = "source.sharpy"
injection-regex = "sharpy"
file-types = ["spy"]
roots = ["*.spyproj"]
comment-token = "#"
indent = { tab-width = 4, unit = "    " }

[language-server.sharpy]
command = "sharpyc"
args = ["lsp"]

[[language]]
name = "sharpy"
language-servers = ["sharpy"]
```

## Zed

Add to your Zed settings (`~/.config/zed/settings.json`):

```json
{
  "lsp": {
    "sharpy": {
      "binary": {
        "path": "sharpyc",
        "arguments": ["lsp"]
      }
    }
  },
  "languages": {
    "Sharpy": {
      "language_servers": ["sharpy"]
    }
  }
}
```

## General

For any LSP-capable editor not listed above, configure:

- **Command**: `sharpyc lsp`
- **Transport**: stdio (stdin/stdout)
- **File types**: `.spy`
- **Language ID**: `sharpy`
