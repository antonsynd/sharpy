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

## Custom Agents

This repository has specialized agents defined in `.github/agents.md` for different areas:

- **compiler_expert** - For lexer, parser, semantic analysis, and code generation
- **core_library_expert** - For standard library (Pythonic APIs for .NET)
- **docs_writer** - For documentation and technical writing
- **test_expert** - For comprehensive testing
- **cli_expert** - For command-line interface

When working on a specific component, consider delegating to the appropriate specialized agent. See `.github/agents.md` for detailed capabilities and boundaries.

## Security Best Practices

- **Never commit secrets** - No API keys, passwords, or credentials in code
- **Validate all inputs** - Especially in compiler and CLI tools
- **Use secure defaults** - Favor security over convenience
- **Handle errors safely** - Don't expose sensitive information in error messages
- **Follow principle of least privilege** - Request only necessary permissions

## Issue and PR Guidelines

### When Working on Issues

- **Read the full issue description** - Understand requirements before coding
- **Check for related issues** - Avoid duplicate work
- **Ask for clarification** - If requirements are unclear, ask before implementing
- **Reference issue numbers** - In commit messages and PR descriptions

### Pull Request Best Practices

- **Keep changes focused** - One logical change per PR
- **Write descriptive titles** - Clearly state what the PR does
- **Include context** - Explain why the change is needed
- **Test thoroughly** - All tests must pass
- **Format code** - Run `dotnet format` before submitting
- **Update documentation** - For user-facing changes

## Debugging Workflow

### Compiler Issues
```bash
# Build with verbose output
dotnet build -v detailed src/Sharpy.Compiler/

# Run specific failing test
dotnet test --filter "FullyQualifiedName~TestName" --logger "console;verbosity=detailed"

# Use AST dumper to debug parser
# Add debug output in Parser.cs to visualize AST
```

### Standard Library Issues
```bash
# Compare with Python behavior
python3 -c "test code"

# Run specific test with details
dotnet test src/Sharpy.Core.Tests --filter "FullyQualifiedName~TestName"

# Check .NET behavior
dotnet script -c "using System; using System.Collections.Generic; /* test code */"
```

### Integration Issues
```bash
# Test full compilation
dotnet run --project src/Sharpy.Cli -- build test.spy

# Check generated C# code
# Add --emit-csharp flag (if implemented) or inspect temp files
```

## Performance Considerations

- **Module caching is enabled** - Avoid unnecessary recompilation
- **Parallel builds are supported** - Build solution instead of individual projects when possible
- **Test execution can be filtered** - Use `--filter` to run only relevant tests
- **Incremental compilation** - Only rebuild changed projects

## Environment Details

- **Target Framework**: .NET 9.0 (tested on .NET 10.0)
- **Build System**: MSBuild via `dotnet` CLI
- **Test Framework**: xUnit
- **Code Analysis**: Roslyn analyzers enabled
- **Formatting**: Code is formatted using `dotnet format` (default conventions)

## Quick Reference

### Most Common Commands
```bash
# Full build and test
dotnet build && dotnet test

# Format code
dotnet format

# Run specific test suite
dotnet test --filter "Namespace~Sharpy.Compiler.Tests.Lexer"

# Build specific project
dotnet build src/Sharpy.Core/Sharpy.Core.csproj

# Run compiler CLI
dotnet run --project src/Sharpy.Cli -- build file.spy
```

### File Patterns
- `*.spy` - Sharpy source files
- `*.spyproj` - Sharpy project files (XML format)
- `HOW_TO_CONTRIBUTE.instructions.md` - Component-specific guides
- `*Tests.cs` - Test files (xUnit)

## Continuous Integration

- **Workflows**: `.github/workflows/dotnet9.yml` and `.github/workflows/dotnet10.yml`
- **Automated testing**: Tests run on push and PR
- **Multi-framework**: Tests run on both .NET 9.0 and 10.0
- **Build validation**: All projects must build successfully
