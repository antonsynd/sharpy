# Contributing to LSP/VS Code Extension

## Overview

**lsp/sharpy/** contains the VS Code extension for Sharpy, providing syntax highlighting and basic language support for `.spy` files.

**Location:** `lsp/sharpy/`

## What's in This Directory

### Directory Structure

```
lsp/sharpy/
├── package.json           # Extension manifest and dependencies
├── syntaxes/
│   └── sharpy.tmLanguage.json  # TextMate grammar for syntax highlighting
├── language-configuration.json # Language configuration (brackets, comments, etc.)
└── README.md             # Extension documentation (if exists)
```

### Components

**package.json**
- Extension metadata (name, version, publisher)
- Activation events
- Language definition
- Dependencies

**syntaxes/sharpy.tmLanguage.json**
- TextMate grammar rules
- Syntax highlighting patterns
- Scope definitions
- Tokenization rules

**language-configuration.json**
- Bracket matching
- Comment definitions
- Auto-closing pairs
- Indentation rules

## Features

### Currently Implemented
- ✅ Syntax highlighting for `.spy` files
- ✅ Keyword recognition (def, class, if, for, etc.)
- ✅ String highlighting (f-strings, raw strings, triple-quoted)
- ✅ Comment highlighting
- ✅ Number literal highlighting
- ✅ Operator highlighting
- ✅ Bracket matching
- ✅ Auto-closing pairs
- ✅ Comment toggling (Ctrl+/)

### Not Yet Implemented
- ❌ Language Server Protocol (LSP) support
- ❌ IntelliSense/auto-completion
- ❌ Go to definition
- ❌ Error checking/diagnostics
- ❌ Hover information
- ❌ Refactoring support
- ❌ Debugging support

## How to Build/Install

### Install the Extension in VS Code

```bash
# From the lsp/sharpy directory
cd lsp/sharpy

# Install dependencies (if any)
npm install

# Package the extension (requires vsce)
npm install -g @vscode/vsce
vsce package

# Install the generated .vsix file in VS Code
# Method 1: Via command palette
# Press Ctrl+Shift+P -> "Extensions: Install from VSIX"

# Method 2: Via command line
code --install-extension sharpy-*.vsix
```

### Development Mode

```bash
# Open the extension in VS Code
code lsp/sharpy

# Press F5 to launch Extension Development Host
# This opens a new VS Code window with the extension loaded

# Open a .spy file to test syntax highlighting
```

## How to Test

### Manual Testing

1. **Open a Sharpy file** (`.spy`) in VS Code
2. **Verify syntax highlighting:**
   - Keywords are highlighted (def, class, if, etc.)
   - Strings are highlighted correctly
   - Comments are highlighted
   - Numbers and operators are highlighted

3. **Test language features:**
   - Type `#` - should recognize as comment
   - Type `"""` - should auto-close with `"""`
   - Select code and press Ctrl+/ - should toggle comments
   - Type `(` - should auto-insert `)`

### Test Files

Use files in `snippets/` for testing:
```bash
code snippets/example_v05.spy
code snippets/functions.spy
code snippets/sharpy_features.spy
```

## Important Things to Note

### TextMate Grammar

The syntax highlighting uses TextMate grammar, which is based on regular expressions and scopes.

**Scope naming convention:**
- `keyword.control.sharpy` - Control flow keywords (if, while, for)
- `keyword.declaration.sharpy` - Declaration keywords (def, class)
- `string.quoted.sharpy` - String literals
- `comment.line.sharpy` - Comments
- `constant.numeric.sharpy` - Numbers
- `entity.name.function.sharpy` - Function names

**Pattern matching:**
```json
{
  "match": "\\b(def|class|if|else|elif)\\b",
  "name": "keyword.control.sharpy"
}
```

### Language Configuration

**Bracket pairs:**
```json
{
  "brackets": [
    ["{", "}"],
    ["[", "]"],
    ["(", ")"]
  ]
}
```

**Auto-closing pairs:**
```json
{
  "autoClosingPairs": [
    { "open": "(", "close": ")" },
    { "open": "[", "close": "]" },
    { "open": "{", "close": "}" },
    { "open": "\"", "close": "\"" },
    { "open": "'", "close": "'" }
  ]
}
```

## Common Development Tasks

### Adding Syntax Highlighting for a New Keyword

1. **Edit `syntaxes/sharpy.tmLanguage.json`:**
   ```json
   {
     "patterns": [
       {
         "match": "\\b(newkeyword)\\b",
         "name": "keyword.control.sharpy"
       }
     ]
   }
   ```

2. **Test in Extension Development Host** (F5)

3. **Verify** the keyword is highlighted in `.spy` files

### Adding a New File Pattern

To highlight a specific syntax pattern:

```json
{
  "patterns": [
    {
      "name": "meta.decorator.sharpy",
      "match": "@[a-zA-Z_][a-zA-Z0-9_]*",
      "captures": {
        "0": { "name": "entity.name.function.decorator.sharpy" }
      }
    }
  ]
}
```

### Updating Comment Definitions

Edit `language-configuration.json`:
```json
{
  "comments": {
    "lineComment": "#",
    "blockComment": ["'''", "'''"]
  }
}
```

### Adding Auto-Indent Rules

```json
{
  "indentationRules": {
    "increaseIndentPattern": "^\\s*(def|class|if|elif|else|for|while|try|except|finally).*:$",
    "decreaseIndentPattern": "^\\s*(return|break|continue|pass|raise)\\b"
  }
}
```

## Future: Language Server Protocol (LSP)

To add full language support, a Language Server is needed:

### What LSP Would Provide
- **IntelliSense** - Auto-completion based on types
- **Diagnostics** - Real-time error checking
- **Go to Definition** - Navigate to function/class definitions
- **Hover** - Show type information on hover
- **Rename** - Refactor symbol names
- **References** - Find all references to a symbol

### Implementation Plan
1. Create a Language Server in C# using OmniSharp.Extensions.LanguageServer
2. Integrate with Sharpy.Compiler for:
   - Parsing and AST generation
   - Type checking and semantic analysis
   - Symbol resolution
3. Update `package.json` to activate the language server
4. Add server communication logic

### Example LSP Integration
```json
// In package.json
{
  "activationEvents": ["onLanguage:sharpy"],
  "contributes": {
    "languages": [{
      "id": "sharpy",
      "extensions": [".spy"],
      "configuration": "./language-configuration.json"
    }]
  },
  "main": "./out/extension.js"
}
```

## Testing Best Practices

### Syntax Highlighting Tests
- Test all keyword categories
- Test string variations (f-strings, raw strings, triple-quoted)
- Test comment styles
- Test nested structures
- Test edge cases (strings with quotes, escaped characters)

### Language Configuration Tests
- Test bracket matching with nested brackets
- Test auto-closing in different contexts
- Test comment toggling with different selections
- Test indentation in various scenarios

## Common Issues and Solutions

### Syntax Highlighting Not Working
- **Check:** File extension is `.spy`
- **Check:** Extension is installed and enabled
- **Check:** VS Code has reloaded after changes
- **Solution:** Reload window (Ctrl+R in Extension Development Host)

### Keywords Not Highlighted
- **Check:** Pattern matches in `sharpy.tmLanguage.json`
- **Check:** Word boundaries (`\b`) are correct
- **Check:** JSON syntax is valid
- **Solution:** Test regex pattern separately

### Auto-Closing Not Working
- **Check:** `autoClosingPairs` in `language-configuration.json`
- **Check:** VS Code setting for auto-closing is enabled
- **Solution:** Check VS Code settings: "Editor: Auto Closing Brackets"

## Dependencies

- **VS Code** - Editor platform
- **@vscode/vsce** (optional) - For packaging
- **Node.js** (optional) - For LSP implementation

## Related Documentation

- **Main README:** `README.md` (root)
- **VS Code Extension API:** https://code.visualstudio.com/api
- **TextMate Grammars:** https://macromates.com/manual/en/language_grammars
- **Language Server Protocol:** https://microsoft.github.io/language-server-protocol/

## File Structure for Full LSP

```
lsp/sharpy/
├── package.json                    # Extension manifest
├── tsconfig.json                   # TypeScript config (if using TS)
├── syntaxes/
│   └── sharpy.tmLanguage.json     # Syntax highlighting
├── language-configuration.json     # Language config
├── src/
│   ├── extension.ts               # Extension entry point
│   └── server/
│       ├── server.ts              # Language server
│       ├── parser.ts              # Integration with Sharpy.Compiler
│       ├── symbols.ts             # Symbol provider
│       ├── diagnostics.ts         # Error checking
│       └── completion.ts          # Auto-completion
└── README.md
```

## Contributing Guidelines

### Code Style
- Use consistent formatting in JSON files
- Use 2-space indentation for JSON
- Group related patterns together
- Add comments for complex regex patterns

### Testing
- Test with real Sharpy code from `snippets/`
- Test edge cases
- Verify in both light and dark themes
- Check performance with large files

### Documentation
- Document new patterns in comments
- Update README when adding features
- Include examples of what gets highlighted

## Getting Help

- Review existing patterns in `sharpy.tmLanguage.json`
- Check VS Code extension documentation
- Look at other language extensions for examples
- Test incrementally in Extension Development Host
