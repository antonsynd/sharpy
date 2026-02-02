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

## The Three Axioms (Priority Order)

| Priority | Axiom | Principle |
|----------|-------|-----------|
| 1 (Highest) | .NET Runtime | Compiles to valid C# 9.0 for .NET CLR |
| 2 | Static Typing | Non-nullable by default, explicit types |
| 3 (Yields) | Python Syntax | Python 3 syntax and idioms |

When axioms conflict: **.NET > Type Safety > Python**

## Core Rules

1. **Never modify test expectations to pass** — fix the implementation instead
2. **Verify Python semantics first** — `python3 -c "..."` before implementing
3. **Follow existing patterns** — search codebase for similar code
4. **C# 9.0 target** — no file-scoped namespaces, global usings, record structs

## Feature Implementation Flow

For new language features, touch components **in this order** (dependencies flow left→right):

```
Lexer → Parser → Semantic → Validation → CodeGen → Tests
```

1. **Lexer** (`Lexer/`) — Add `TokenType` and recognition
2. **Parser** (`Parser/Ast/`) — Add AST record, parsing rule
3. **Semantic** (`Semantic/`) — Add type checking in `TypeChecker*.cs`
4. **Validation** (`Semantic/Validation/`) — Add validator if needed
5. **CodeGen** (`CodeGen/RoslynEmitter*.cs`) — Emit via `SyntaxFactory`
6. **Tests** — Unit tests per component + `.spy`/`.expected` integration tests

## Key Directories

| Path | Purpose |
|------|---------|
| `docs/language_specification/` | Authoritative language spec — check before implementing |
| `src/Sharpy.Compiler/Semantic/Validation/` | Pluggable validators (operators, protocols, access) |
| `src/Sharpy.Compiler.Tests/Integration/TestFixtures/` | File-based tests auto-discovered |
| `src/Sharpy.Core/Partial.*/` | Partial class pattern for stdlib types |

## Commands

```bash
dotnet build sharpy.sln && dotnet test           # Build + test all
dotnet format whitespace                         # Format before commit
dotnet run --project src/Sharpy.Cli -- emit csharp file.spy  # Debug codegen
python3 -c "..."                                 # Verify Python behavior
```

See [copilot-instructions.md](../copilot-instructions.md) for repository-wide guidance.
