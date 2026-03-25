# Component Instructions

Component-specific guides for the Sharpy codebase. Each guide covers patterns, workflows, and conventions for its domain.

> **See also:** [../copilot-instructions.md](../copilot-instructions.md) for repository-wide guidance, [../agents.md](../agents.md) for domain-specific agents.

## Quick Reference

| Working on | Guide | Key Patterns |
|------------|-------|--------------|
| New language feature | [Sharpy.Compiler](./Sharpy.Compiler/HOW_TO_CONTRIBUTE.instructions.md) | Lexer→Parser→Semantic→CodeGen flow |
| Builtin function or collection | [Sharpy.Core](./Sharpy.Core/HOW_TO_CONTRIBUTE.instructions.md) | Partial class pattern, Python naming |
| Compiler tests | [Sharpy.Compiler.Tests](./Sharpy.Compiler.Tests/HOW_TO_CONTRIBUTE.instructions.md) | File-based tests, `IntegrationTestBase` |
| Library tests | [Sharpy.Core.Tests](./Sharpy.Core.Tests/HOW_TO_CONTRIBUTE.instructions.md) | Python verification workflow |
| CLI options | [Sharpy.Cli](./Sharpy.Cli/HOW_TO_CONTRIBUTE.instructions.md) | `System.CommandLine` patterns |

## The Three Axioms (Priority Order)

| Priority | Axiom | Principle |
|----------|-------|-----------|
| 1 (Highest) | .NET Runtime | Compiles to valid C# 9.0 for .NET CLR |
| 2 | Static Typing | Non-nullable by default, explicit types |
| 3 (Yields) | Python Syntax | Python 3 syntax and idioms |

When axioms conflict: **.NET > Type Safety > Python Syntax**

## Core Rules

1. **Never modify test expectations to pass** — fix the implementation instead
2. **Language spec is authoritative** — check `docs/language_specification/` before implementing
3. **Verify Python semantics first** — `python3 -c "..."` before implementing
4. **Follow existing patterns** — search codebase for similar code
5. **C# 9.0 target for Sharpy.Core** — no file-scoped namespaces, global usings, record structs
6. **C# latest for Compiler/CLI** — `Sharpy.Compiler` and `Sharpy.Cli` target `net10.0`
7. **Warnings are errors** — `TreatWarningsAsErrors` is enabled solution-wide via `Directory.Build.props`
8. **TODO/BUG/FIXME → create GitHub issues** — create issue first (`gh issue create`), reference in comment (`// TODO(#123): ...`)

## Feature Implementation Flow

For new language features, touch components **in this order** (dependencies flow left→right):

```
Lexer → Parser → Semantic → Validation → CodeGen → Tests
```

1. **Lexer** (`Lexer/`) — Add `TokenType` and recognition
2. **Parser** (`Parser/Ast/`) — Add AST record, parsing rule (6 partial files)
3. **Semantic** (`Semantic/`) — Add type checking in `TypeChecker*.cs` (8 partial files)
4. **Validation** (`Semantic/Validation/`) — Add validator if needed (pluggable pipeline)
5. **CodeGen** (`CodeGen/RoslynEmitter*.cs`) — Emit via `SyntaxFactory` (11 partial files)
6. **Tests** — Unit tests per component + `.spy`/`.expected` file-based tests

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
dotnet format whitespace                         # Format code (auto-formatted on save by Claude hook)
dotnet run --project src/Sharpy.Cli -- emit csharp file.spy  # Debug codegen
dotnet run --project src/Sharpy.Cli -- emit ast file.spy     # Debug parser
dotnet run --project src/Sharpy.Cli -- emit tokens file.spy  # Debug lexer
python3 -c "..."                                 # Verify Python behavior
```

## Debugging Workflow

When something doesn't compile or behave correctly:
1. `emit tokens` — Is the lexer producing correct tokens?
2. `emit ast` — Is the parser building the correct AST?
3. `emit csharp` — Is the generated C# code correct?
4. Run tests for the specific component to isolate the issue

See [copilot-instructions.md](../copilot-instructions.md) for repository-wide guidance.
