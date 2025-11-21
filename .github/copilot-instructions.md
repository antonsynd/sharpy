# Sharpy Repository Guide

This document provides guidance for engineers and agents working on the Sharpy compiler and standard library.

## Project Overview

**Sharpy** is a modern, statically-typed Pythonic language for .NET that combines Python's elegant syntax with .NET's type safety and performance. The project includes a full compiler toolchain, standard library, and CLI.

## Repository Structure

```
sharpy/
├── src/
│   ├── Sharpy.Core/              # Standard library (Pythonic APIs for .NET)
│   ├── Sharpy.Compiler/          # Compiler implementation (lexer, parser, semantic analyzer, code generator)
│   ├── Sharpy.Cli/               # CLI tool (sharpyc)
│   ├── Sharpy.Core.Tests/        # Standard library tests
│   └── Sharpy.Compiler.Tests/    # Compiler tests
├── docs/                         # Documentation (manual, specs, architecture)
├── samples/                      # Example Sharpy programs and projects
├── snippets/                     # Small code snippets for testing
├── lsp/sharpy/                   # VS Code extension (syntax highlighting)
└── .github/
    └── instructions/             # Detailed contribution guides for each component
```

### Key Directories

**Source Code:**
- **`src/Sharpy.Core/`** - Standard Pythonic library (`list[T]`, `dict[K,V]`, `set[T]`, `str`, `len()`, `print()`, `range()`, etc.)
- **`src/Sharpy.Compiler/`** - Compiler pipeline:
  - `Lexer/` - Tokenization
  - `Parser/` - AST generation
  - `Semantic/` - Type checking, name resolution, type narrowing
  - `CodeGen/` - C# code generation via Roslyn
  - `Discovery/` - Module discovery and caching
- **`src/Sharpy.Cli/`** - Command-line interface for the compiler

**Tests:**
- **`src/Sharpy.Core.Tests/`** - Unit tests for standard library
- **`src/Sharpy.Compiler.Tests/`** - Compiler tests organized by component:
  - `Lexer/` - Tokenization tests
  - `Parser/` - AST parsing tests
  - `Semantic/` - Type checking and analysis tests
  - `CodeGen/` - Code generation tests
  - `Integration/` - End-to-end compilation tests

**Documentation:**
- **`docs/manual/`** - User guides (variables, functions, types, control flow, errors)
- **`docs/specs/`** - Language specifications
- **`docs/architecture/`** - Design documentation

**Examples:**
- **`samples/`** - Full example projects demonstrating Sharpy features
- **`snippets/`** - Small code examples for testing features

## How to Build

### Build Everything
```bash
# From repository root
dotnet build sharpy.sln
```

### Build Specific Projects
```bash
# Standard library
dotnet build src/Sharpy.Core/Sharpy.Core.csproj

# Compiler
dotnet build src/Sharpy.Compiler/Sharpy.Compiler.csproj

# CLI
dotnet build src/Sharpy.Cli/Sharpy.Cli.csproj

# Tests
dotnet build src/Sharpy.Core.Tests/Sharpy.Core.Tests.csproj
dotnet build src/Sharpy.Compiler.Tests/Sharpy.Compiler.Tests.csproj
```

## How to Run Tests

### Run All Tests
```bash
# From repository root
dotnet test
```

### Run Specific Test Projects
```bash
# Standard library tests only
dotnet test src/Sharpy.Core.Tests

# Compiler tests only
dotnet test src/Sharpy.Compiler.Tests
```

### Run Filtered Tests
```bash
# Run tests matching a pattern
dotnet test --filter "FullyQualifiedName~Lexer"
dotnet test --filter "FullyQualifiedName~Parser"
dotnet test --filter "FullyQualifiedName~BasicProgram"

# Run tests in a specific namespace
dotnet test --filter "Namespace~Sharpy.Compiler.Tests.Lexer"
```

## How to Use the Compiler

### Install the CLI tool using Chiri

Chiri is a custom build system wrapper. The most important command is `chiri pkg -- release`, which builds, tests, and installs the Sharpy compiler to `~/.local/bin/sharpyc`.

```bash
# Assuming `chiri` is in PATH, usually at ~/.chiri/bin/chiri
chiri pkg -- release

# Install only (skip tests/build)
chiri pkg -- install
```

### Run the Sharpy Compiler

```bash
# Then from anywhere
sharpyc <args>

# Examples:
sharpyc build snippets/hello.spy
sharpyc --help
```

### Compile a Sharpy Project
```bash
# Auto-discover .spyproj in current directory
sharpyc project

# Compile specific project
sharpyc project samples/calculator_app/calculator.spyproj

# Compile in Release mode
sharpyc project --configuration Release
```

## Formatting

```bash
# Format all code in solution
dotnet format

# Format specific project
dotnet format src/Sharpy.Compiler/Sharpy.Compiler.csproj
```

## Best Practices

### Testing Best Practices

**CRITICAL: When writing tests, NEVER artificially make them pass by:**
- Altering inputs to match incorrect outputs
- Removing failing assertions without fixing the root cause
- Changing expected values to match buggy behavior
- Commenting out or skipping tests without understanding why they fail

**Instead, ALWAYS:**
1. **Fix the root cause** - Investigate why the test fails and fix the underlying bug in the implementation
2. **Update tests legitimately** - Only change tests if requirements have changed or the test itself is incorrect
3. **Mark as skipped with context** - If a test cannot be fixed immediately, mark it as skipped with:
   ```csharp
   [Fact(Skip = "TODO: Fix issue with <specific problem>. See issue #123")]
   public void TestThatNeedsWork()
   {
       // Test code
   }
   ```
4. **Add issue references** - Include issue numbers or detailed TODO comments explaining what needs to be fixed

### Code Quality
- Run `dotnet format` before committing
- Ensure all tests pass before submitting PRs
- Add tests for new features
- Update documentation when changing public APIs

### Commit Messages
- Use clear, descriptive commit messages
- Reference issue numbers when applicable
- Keep commits focused on single logical changes

## Development Workflow

1. **Make changes** to source files
2. **Build** to check for compilation errors: `dotnet build`
3. **Run tests** to verify correctness: `dotnet test`
4. **Format code**: `dotnet format`
5. **Commit** with descriptive message
6. **Push** and create PR

## Getting Help

- **Check existing documentation** in `docs/` directory
- **Review tests** to understand how features work
- **Explore examples** in `samples/` and `snippets/`
- **Read component-specific guides** in `.github/instructions/`

## General Guidance

- **No need to create summary documents** unless explicitly requested
- **No need to create demo programs** unless explicitly requested
- **Focus on minimal, targeted changes** that solve specific problems
- **Leverage existing patterns** from the codebase
