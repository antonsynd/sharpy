# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Sharpy is a statically-typed Pythonic language that compiles to .NET. Source `.spy` files compile to C# via Roslyn, targeting .NET 9.0 and .NET 10.0.

## Build & Test Commands

```bash
dotnet build sharpy.sln                              # Build all
dotnet test                                          # Run all tests
dotnet format                                        # Format before committing
dotnet run --project src/Sharpy.Cli -- run file.spy # Compile and execute
```

**Inspect generated C#:** `dotnet run --project src/Sharpy.Cli -- emit csharp file.spy`

**Run filtered tests:**
```bash
dotnet test --filter "FullyQualifiedName~Lexer"              # Component tests
dotnet test --filter "FullyQualifiedName~BasicProgram"       # Integration tests
dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"  # File-based tests
dotnet test --filter "DisplayName~arithmetic"                # Specific test by name
```

## Architecture

```
Source (.spy) → Lexer → Parser (AST) → Semantic Analysis → RoslynEmitter → C# → .NET IL
```

### Compiler Pipeline (`src/Sharpy.Compiler/`)
1. **Lexer/** - Tokenization
2. **Parser/** - Recursive descent parser producing AST nodes (immutable records with `LineStart`/`ColumnStart`)
3. **Semantic/** - Multi-pass analysis:
   - `NameResolver` (pass 1: declarations, pass 2: inheritance)
   - `TypeResolver` (resolve type annotations)
   - `TypeChecker` (type validation, narrowing after `is None`/`isinstance`)
4. **CodeGen/** - Roslyn C# generation:
   - `RoslynEmitter*.cs` (split by concern: Expressions, Statements, ClassMembers, etc.)
   - `TypeMapper.cs` (Sharpy types → C# types)
   - `NameMangler.cs` (`snake_case` → `PascalCase`, dunder method mappings)

### Standard Library (`src/Sharpy.Core/`)
- **Partial class pattern**: Types split across `Partial.{Type}/` directories
- **Builtins via `partial class Exports`**: Distributed across root-level files (`Print.cs`, `Len.cs`, `Range.cs`, etc.) and subdirectory exports (`Math/Exports.cs`, `Random/Exports.cs`, etc.)
- **Python semantics**: Slicing, negative indices, truthiness—verify with `python3 -c "..."` when unsure

### CLI Tool (`src/Sharpy.Cli/`)
Entry point for compilation. Uses System.CommandLine for argument parsing.

## Testing

**CRITICAL**: Never modify expected values to make tests pass. Fix the implementation.

### Integration Tests
- Inherit from `IntegrationTestBase` (`Integration/IntegrationTestBase.cs`)
- Use `CompileAndExecute(source)` → returns `ExecutionResult` with `Success`, `StandardOutput`, `CompilationErrors`
- Uses process-based execution for .NET 10 compatibility

### File-Based Tests (`Integration/TestFixtures/`)
```
TestFixtures/
├── basics/arithmetic.spy           # Source
├── basics/arithmetic.expected      # Expected stdout (exact match)
├── errors/empty_list_shorthand.spy # Error case
└── errors/empty_list_shorthand.error # Substring to match in error
```
Add new tests by creating `.spy` + `.expected` (or `.error`) pairs—auto-discovered at runtime.

**Skip broken tests:** `[Fact(Skip = "TODO: Fix <specific issue>")]`

## Key Design Patterns

1. **Immutable AST**: All semantic annotations go in `SemanticInfo` class, not on AST nodes
2. **RoslynEmitter uses SyntaxFactory exclusively**—no string templating for C# generation
3. **Type narrowing**: `TypeChecker._narrowedTypes` tracks narrowed types after null checks
4. **Type mappings**: `list[T]` → `global::Sharpy.Core.List<T>`, `dict[K,V]` → `global::Sharpy.Core.Dict<K,V>`
5. **Name mangling**: `snake_case` → `PascalCase`, `__str__` → `ToString()`, `__add__` → `operator+`

## When .NET and Python Conflict

Prefer .NET semantics unless zero-cost abstraction is possible. Sharpy is a .NET-first language.

## CI/CD

Workflows in `.github/workflows/`:
- **dotnet10.yml** - Primary CI (active)
- **dotnet9.yml** - Legacy support
