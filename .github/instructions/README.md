# Component-Specific Instructions

This directory contains detailed contribution guides for each major component of the Sharpy compiler and standard library.

## Available Guides

### Compiler Components

- **[Sharpy.Compiler](./Sharpy.Compiler/HOW_TO_CONTRIBUTE.instructions.md)** - Core compiler implementation
  - Lexer (tokenization)
  - Parser (AST generation)
  - Semantic Analyzer (type checking)
  - Code Generator (C# via Roslyn)
  - Module discovery and caching

- **[Sharpy.Compiler.Tests](./Sharpy.Compiler.Tests/HOW_TO_CONTRIBUTE.instructions.md)** - Compiler test suite
  - Lexer tests
  - Parser tests
  - Semantic analysis tests
  - Code generation tests
  - Integration tests

### Standard Library

- **[Sharpy.Core](./Sharpy.Core/HOW_TO_CONTRIBUTE.instructions.md)** - Standard library implementation
  - Collections (`list`, `dict`, `set`, `str`)
  - Builtin functions (`len`, `print`, `range`, etc.)
  - Operator protocols
  - Type conversions

- **[Sharpy.Core.Tests](./Sharpy.Core.Tests/HOW_TO_CONTRIBUTE.instructions.md)** - Standard library tests
  - Collection tests
  - Builtin function tests
  - Protocol tests
  - Edge case validation

### CLI Tool

- **[Sharpy.Cli](./Sharpy.Cli/HOW_TO_CONTRIBUTE.instructions.md)** - Command-line interface
  - Argument parsing
  - Compilation orchestration
  - Error handling
  - User experience

### Examples and Extensions

- **[samples](./samples/HOW_TO_CONTRIBUTE.instructions.md)** - Example programs and projects
  - Full applications
  - Language feature demonstrations
  - Integration test examples

- **[lsp](./lsp/HOW_TO_CONTRIBUTE.instructions.md)** - Language Server Protocol / VS Code extension
  - Syntax highlighting
  - Future: autocomplete, diagnostics

## How to Use These Guides

1. **Identify the component** you're working on
2. **Read the corresponding guide** for specific instructions
3. **Follow the build and test procedures** outlined in each guide
4. **Consult the main copilot-instructions.md** for repository-wide guidance

## Quick Navigation

Working on | Read This
-----------|----------
Adding a new language feature | [Sharpy.Compiler](./Sharpy.Compiler/HOW_TO_CONTRIBUTE.instructions.md)
Implementing a builtin function | [Sharpy.Core](./Sharpy.Core/HOW_TO_CONTRIBUTE.instructions.md)
Writing compiler tests | [Sharpy.Compiler.Tests](./Sharpy.Compiler.Tests/HOW_TO_CONTRIBUTE.instructions.md)
Writing library tests | [Sharpy.Core.Tests](./Sharpy.Core.Tests/HOW_TO_CONTRIBUTE.instructions.md)
Adding CLI options | [Sharpy.Cli](./Sharpy.Cli/HOW_TO_CONTRIBUTE.instructions.md)
Creating examples | [samples](./samples/HOW_TO_CONTRIBUTE.instructions.md)

## General Principles

All guides emphasize:
- **Fix root causes**, never artificially make tests pass
- **Match Python semantics** where applicable
- **Test thoroughly** with comprehensive test cases
- **Document intentional differences** from Python
- **Follow existing patterns** in the codebase

## Related Documentation

- **Repository Overview**: `../../README.md`
- **Main Copilot Instructions**: `../copilot-instructions.md`
- **Custom Agents**: `../agents.md`
- **Language Manual**: `../../docs/manual/`
- **Language Specs**: `../../docs/specs/`
- **Architecture Docs**: `../../docs/architecture/`
