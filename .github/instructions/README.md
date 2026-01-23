# Component Instructions

Component-specific guides for the Sharpy codebase. Each guide covers patterns, workflows, and conventions for its domain.

## Quick Reference

| Working on | Guide |
|------------|-------|
| New language feature | [Sharpy.Compiler](./Sharpy.Compiler/HOW_TO_CONTRIBUTE.instructions.md) |
| Builtin function or collection | [Sharpy.Core](./Sharpy.Core/HOW_TO_CONTRIBUTE.instructions.md) |
| Compiler tests | [Sharpy.Compiler.Tests](./Sharpy.Compiler.Tests/HOW_TO_CONTRIBUTE.instructions.md) |
| Library tests | [Sharpy.Core.Tests](./Sharpy.Core.Tests/HOW_TO_CONTRIBUTE.instructions.md) |
| CLI options | [Sharpy.Cli](./Sharpy.Cli/HOW_TO_CONTRIBUTE.instructions.md) |
| Example programs | [samples](./samples/HOW_TO_CONTRIBUTE.instructions.md) |
| VS Code extension | [lsp](./lsp/HOW_TO_CONTRIBUTE.instructions.md) |

## Core Principles

1. **Fix root causes** — Never artificially make tests pass
2. **Match Python semantics** — Test against `python3 -c "..."` for expected behavior
3. **Follow existing patterns** — Check similar code in the codebase
4. **C# 9.0 target** — No file-scoped namespaces, global usings, or record structs

## Feature Implementation Flow

For new language features, touch these components in order:

1. **Lexer** (`Lexer/`) — Add tokens if needed
2. **Parser** (`Parser/`) — Add AST nodes and parsing rules
3. **Semantic** (`Semantic/`) — Add type checking rules
4. **CodeGen** (`CodeGen/`) — Emit C# via Roslyn SyntaxFactory
5. **Tests** — Unit tests + file-based integration tests

## Key Specs

Language specification lives in `docs/language_specification/`. Always check specs before implementing.

See [copilot-instructions.md](../copilot-instructions.md) for repository-wide guidance.
