# Contributing to Sharpy

Thank you for your interest in contributing to Sharpy! This guide will help you get started.

> **AI contributors:** See [CLAUDE.md](CLAUDE.md) and [.github/copilot-instructions.md](.github/copilot-instructions.md) for detailed AI-specific guidance.

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (or .NET 9)
- Python 3 (for verifying language semantics)
- A C# editor (VS Code with C# Dev Kit, Rider, or Visual Studio)

### Build and Test

```bash
git clone https://github.com/anthropics/sharpy.git
cd sharpy
dotnet build sharpy.sln    # Build everything
dotnet test                # Run all tests (~4400 tests)
```

### Run a Sharpy Program

```bash
dotnet run --project src/Sharpy.Cli -- run hello.spy
```

### Inspect Compiler Output

```bash
dotnet run --project src/Sharpy.Cli -- emit tokens file.spy   # Lexer tokens
dotnet run --project src/Sharpy.Cli -- emit ast file.spy      # Parsed AST
dotnet run --project src/Sharpy.Cli -- emit csharp file.spy   # Generated C#
dotnet run --project src/Sharpy.Cli -- emit parse file.spy    # Validate parse only
dotnet run --project src/Sharpy.Cli -- explain SPY0200        # Explain an error code
```

## Project Structure

```
Source (.spy) --> Lexer --> Parser (AST) --> Semantic --> ValidationPipeline --> RoslynEmitter --> C# --> .NET IL
```

| Directory | Purpose |
|-----------|---------|
| `src/Sharpy.Compiler/Lexer/` | Tokenization, indentation tracking |
| `src/Sharpy.Compiler/Parser/` | Recursive descent parser, immutable AST records |
| `src/Sharpy.Compiler/Semantic/` | Name resolution, type checking, import resolution |
| `src/Sharpy.Compiler/Semantic/Validation/` | Pluggable validation pipeline (control flow, access, protocols) |
| `src/Sharpy.Compiler/CodeGen/` | Roslyn SyntaxFactory-based C# code generation |
| `src/Sharpy.Compiler/Diagnostics/` | Error codes, diagnostic reporting |
| `src/Sharpy.Core/` | Runtime standard library |
| `src/Sharpy.Cli/` | CLI (`sharpyc`) using System.CommandLine |
| `src/Sharpy.Compiler.Tests/` | Unit and integration tests |
| `docs/language_specification/` | Authoritative language specification |

## How to Add a Language Feature

Adding a new feature touches the full pipeline. Here's the 6-step process:

### 1. Lexer (if new tokens are needed)

File: `src/Sharpy.Compiler/Lexer/Lexer.cs`

Add new token types to `TokenType` enum and handle them in the lexer's scanning logic. Most features reuse existing tokens.

### 2. Parser

Files: `src/Sharpy.Compiler/Parser/Parser.cs`, `src/Sharpy.Compiler/Parser/Ast/*.cs`

- Define new AST node(s) as immutable records in `Ast/`
- Add parsing logic in the appropriate `Parser.*.cs` partial class
- AST nodes are **immutable** -- never add mutable state to them

### 3. Semantic Analysis

Files: `src/Sharpy.Compiler/Semantic/NameResolver.cs`, `TypeChecker.cs`

- Register declarations in `NameResolver` (first pass)
- Add type checking logic in `TypeChecker` (second pass)
- Annotations go in `SemanticInfo`, never on AST nodes

### 4. Validation (if new rules are needed)

File: `src/Sharpy.Compiler/Semantic/Validation/`

- Create a new validator implementing `ISemanticValidator`
- Register it in `ValidationPipelineFactory.CreateDefault()`
- Validators are sorted by `Order` value

### 5. Code Generation

Files: `src/Sharpy.Compiler/CodeGen/RoslynEmitter*.cs`

- Use Roslyn `SyntaxFactory` exclusively -- **never string templates**
- Map types through `TypeMapper.cs`
- Map names through `NameMangler.cs` (`snake_case` to `PascalCase`, dunder methods to .NET equivalents)

### 6. Tests

- Unit tests for each stage (lexer, parser, semantic, codegen)
- Integration test using `CompileAndExecute()` in `IntegrationTestBase`
- File-based test fixture (see below)

## How to Add a Validation Rule

1. Create a class implementing `ISemanticValidator` in `src/Sharpy.Compiler/Semantic/Validation/`
2. Set `Name` and `Order` (determines execution sequence)
3. Implement `Validate(Module module, SemanticContext context)`
4. Add the validator to `ValidationPipelineFactory.CreateDefault()`
5. Add a diagnostic code in `DiagnosticCodes.cs`
6. Add an explanation in `DiagnosticExplanations.cs`
7. Write tests

## How to Add Error Test Fixtures

File-based tests live in `src/Sharpy.Compiler.Tests/Integration/TestFixtures/`. They're auto-discovered.

### Success test

Create a `.spy` file and a matching `.expected` file:

```
TestFixtures/basics/my_feature.spy       # Sharpy source
TestFixtures/basics/my_feature.expected  # Expected stdout (exact match)
```

### Error test

Create a `.spy` file and a matching `.error` file:

```
TestFixtures/errors/my_error.spy   # Sharpy source that should fail
TestFixtures/errors/my_error.error # Substring that must appear in error message
```

### Skipping a test

Add a `.skip` file alongside the `.spy` file to temporarily skip it.

### Running specific tests

```bash
dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"  # All file-based
dotnet test --filter "FullyQualifiedName~Lexer"                      # Lexer tests
dotnet test --filter "DisplayName~my_feature"                        # By name
```

## Debugging Tips

### Emit commands

The `emit` subcommands are your primary debugging tools:

- `emit tokens` -- see what the lexer produces
- `emit ast` -- see the parsed AST structure
- `emit csharp` -- see the generated C# code
- `emit parse` -- validate just lexing + parsing

### Explain error codes

```bash
dotnet run --project src/Sharpy.Cli -- explain SPY0265    # Detailed error explanation
dotnet run --project src/Sharpy.Cli -- explain --list      # All documented codes
```

### Compiler logging

```bash
dotnet run --project src/Sharpy.Cli -- --log-level Debug run file.spy
dotnet run --project src/Sharpy.Cli -- --log-level Debug --log-file debug.log run file.spy
```

### Verify Python behavior

When in doubt about how a feature should behave, check Python first:

```bash
python3 -c "print([1,2,3][-1])"
```

## Code Style and Conventions

### General

- **C# 9.0 target** -- no global usings, file-scoped namespaces, or record structs
- Code formatting runs automatically via Claude Code hooks on save
- Follow existing patterns in the codebase

### AST nodes

AST nodes are immutable records with `{ get; init; }` properties:

```csharp
public record FunctionDef : Statement
{
    public string Name { get; init; } = "";
    public ImmutableArray<Parameter> Parameters { get; init; }
    public TypeAnnotation? ReturnType { get; init; }
    public ImmutableArray<Statement> Body { get; init; }
}
```

### Code generation

Always use Roslyn SyntaxFactory:

```csharp
// Correct
ReturnStatement(LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(42)))

// Wrong - never use string templates
$"return {value};"
```

### Diagnostics

- Define error codes in `DiagnosticCodes.cs`
- Use the `CompilerDiagnostic` type with specific codes
- Add explanations for new codes in `DiagnosticExplanations.cs`

## The Three Axioms

All design decisions follow these axioms in priority order:

| Priority | Axiom | Principle |
|----------|-------|-----------|
| Highest | **.NET** | Sharpy compiles to C# 9.0 for the .NET CLR |
| Medium | **Type Safety** | Explicit static typing, non-nullable by default |
| Yields | **Python Syntax** | Sharpy uses Python 3 syntax and idioms |

When axioms conflict, higher-priority axioms win. For example, if Python semantics would require runtime type checking that .NET doesn't support efficiently, the .NET axiom takes precedence.

## Before Submitting

1. `dotnet build sharpy.sln` -- build succeeds
2. `dotnet test` -- all tests pass
3. `dotnet format whitespace` -- formatting is clean
4. Tests cover the change (unit + integration where applicable)
5. Never modify `.expected` files to make tests pass -- fix the implementation
