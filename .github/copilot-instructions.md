# Sharpy Compiler & Standard Library

Sharpy is a statically-typed Pythonic language for .NET. Source `.spy` files compile to C# via Roslyn.

## Architecture Overview

```
Source (.spy) → Lexer → Parser (AST) → Semantic Analysis → RoslynEmitter → C# → .NET IL
```

**Key components in `src/Sharpy.Compiler/`:**
- `Lexer/` - Tokenization of Sharpy source
- `Parser/` - Recursive descent parser → AST (`Parser/Ast/Node.cs`, `Statement.cs`, `Expression.cs`)
- `Semantic/` - Multi-pass analysis: `NameResolver` → `TypeResolver` → `TypeChecker` (includes type narrowing for `is None`/`isinstance`)
- `CodeGen/RoslynEmitter.cs` - Generates C# using Roslyn syntax trees; `TypeMapper.cs` maps Sharpy→C# types

**Standard library in `src/Sharpy.Core/`:**
- Pythonic collections: `List<T>`, `Dict<K,V>`, `Set<T>` with Python semantics (slicing, negative indices)
- Builtins: `print()`, `len()`, `range()`, `enumerate()`, `zip()`, `map()`, `filter()`
- Uses `partial class Exports` pattern across multiple files (e.g., `Partial.List/`, `Partial.Str/`)

## Essential Commands

```bash
dotnet build sharpy.sln          # Build all
dotnet test                       # Run all tests
dotnet format                     # Format before committing
dotnet run --project src/Sharpy.Cli -- build file.spy  # Compile a file
```

**Filtered tests:**
```bash
dotnet test --filter "FullyQualifiedName~Lexer"     # Component tests
dotnet test --filter "FullyQualifiedName~BasicProgram"  # Integration tests
```

## Code Patterns

**AST nodes** use C# records with location info:
```csharp
public record FunctionDef : Statement { ... }
```

**Semantic analysis** builds a `SymbolTable` with scoped lookups:
```csharp
var nameResolver = new NameResolver(symbolTable, logger);
nameResolver.ResolveDeclarations(module);
var typeResolver = new TypeResolver(symbolTable, semanticInfo, logger);
```

**Code generation** maps Sharpy to C# via `RoslynEmitter`:
- Python `snake_case` → C# `PascalCase` via `NameMangler`
- Sharpy `list[T]` → `Sharpy.Core.List<T>`

**Standard library** matches Python behavior; verify with:
```bash
python3 -c "print([1,2,3].pop())"  # Check expected Python behavior
```

## Testing Conventions

**CRITICAL**: Never make tests pass by altering expected values. Fix the implementation.

If a test cannot be fixed immediately:
```csharp
[Fact(Skip = "TODO: Fix <specific issue>. See issue #123")]
```

**Integration tests** (`Integration/IntegrationTestBase.cs`) compile Sharpy → C# → execute in-memory.

## Project Structure

| Path | Purpose |
|------|---------|
| `src/Sharpy.Compiler/` | Compiler pipeline |
| `src/Sharpy.Core/` | Standard library |
| `src/Sharpy.Cli/` | CLI tool (`sharpyc`) |
| `snippets/*.spy` | Quick test programs |
| `samples/` | Full example projects |
| `docs/architecture/` | Design docs (e.g., `semantic-analyzer-architecture.md`) |

## Key Design Decisions

1. **Sharpy is .NET-first** - When Python and .NET conflict, prefer .NET unless zero-cost abstraction possible
2. **Immutable AST** - Semantic info stored in `SemanticInfo` class, not on AST nodes
3. **Roslyn for codegen** - No string templating; use `SyntaxFactory` for C# generation
4. **Type narrowing** - `TypeChecker` tracks narrowed types in `_narrowedTypes` dictionary for `is None` checks

## CI/CD

Workflows: `.github/workflows/dotnet9.yml`, `dotnet10.yml` - tests run on both .NET 9 and 10.
