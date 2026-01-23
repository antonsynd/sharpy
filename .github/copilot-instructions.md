# Sharpy Compiler & Standard Library

Sharpy is a statically-typed Pythonic language for .NET. Source `.spy` files compile to C# via Roslyn.

## Architecture

```
Source (.spy) → Lexer → Parser (AST) → Semantic Analysis → RoslynEmitter → C# → .NET IL
```

### Compiler Pipeline (`src/Sharpy.Compiler/`)
| Stage | Key Files | Purpose |
|-------|-----------|---------|
| Lexer | `Lexer/Lexer.cs` | Tokenization |
| Parser | `Parser/Parser.cs`, `Parser/Ast/` | Recursive descent → immutable AST records |
| Semantic | `Semantic/NameResolver.cs`, `TypeResolver.cs`, `TypeChecker*.cs` | Multi-pass: declarations → inheritance → types → validation |
| CodeGen | `CodeGen/RoslynEmitter*.cs`, `TypeMapper.cs`, `NameMangler.cs` | Roslyn `SyntaxFactory` → C# AST |

### Standard Library (`src/Sharpy.Core/`)
- **Partial class pattern**: `Partial.{Type}/` directories (e.g., `Partial.List/List.ISequence.cs`)
- **Builtins**: `partial class Exports` split across `Print.cs`, `Len.cs`, `Range.cs`, etc.
- **Python semantics**: Verify behavior with `python3 -c "..."` when unsure

## Commands

```bash
dotnet build sharpy.sln                              # Build all
dotnet test                                          # Run all tests
dotnet format                                        # Format before committing
dotnet run --project src/Sharpy.Cli -- run file.spy # Compile and execute
dotnet run --project src/Sharpy.Cli -- emit csharp file.spy  # Inspect generated C#
```

**Filtered tests:**
```bash
dotnet test --filter "FullyQualifiedName~Lexer"                  # Component
dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"  # File-based
```

## Testing

**CRITICAL**: Never modify expected values to make tests pass. Fix the implementation.

### Integration Tests
Inherit `IntegrationTestBase`, use `CompileAndExecute(source)` → `ExecutionResult` with `Success`, `StandardOutput`, `CompilationErrors`.

### File-Based Tests (`Integration/TestFixtures/`)
```
TestFixtures/
├── basics/hello_world.spy      # Source
├── basics/hello_world.expected # Expected stdout (exact match)
├── errors/undefined_var.spy    # Error case
└── errors/undefined_var.error  # Substring in error message
```
Add `.spy` + `.expected` (or `.error`) pairs—auto-discovered. Skip with `.skip` file.

## Code Patterns

**AST nodes** are immutable records with location info:
```csharp
public record FunctionDef : Statement { public string Name { get; init; } ... }
```

**Semantic info** stored in `SemanticInfo` class, never mutate AST nodes.

**RoslynEmitter** uses `SyntaxFactory` exclusively—no string templating:
```csharp
// ✅ Good
ReturnStatement(LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(42)))
// ❌ Bad
$"return {value};"
```

**Type mappings** (`TypeMapper.cs`): `list[T]` → `global::Sharpy.Core.List<T>`, `dict[K,V]` → `global::Sharpy.Core.Dict<K,V>`

**Name mangling** (`NameMangler.cs`): `snake_case` → `PascalCase`, `__str__` → `ToString()`, `__add__` → `operator+`

## Design Principles

1. **.NET-first**: When Python and .NET conflict, prefer .NET unless zero-cost
2. **Immutable AST**: Annotations in `SemanticInfo`, not on AST nodes
3. **C# 9.0 target**: No global usings, file-scoped namespaces, or record structs
4. **Type narrowing**: `TypeChecker._narrowedTypes` tracks types after `is None`/`isinstance`

## Axiom Precedence (when conflicts arise)

**Axiom 1 (.NET) > Axiom 3 (Type Safety) > Axiom 2 (Python Syntax)**

## Project Layout

| Path | Purpose |
|------|---------|
| `src/Sharpy.Compiler/` | Compiler pipeline |
| `src/Sharpy.Core/` | Runtime stdlib |
| `src/Sharpy.Cli/` | CLI entry point |
| `src/*.Tests/` | Test projects |
| `snippets/*.spy` | Quick test programs |
| `docs/language_specification/` | Language spec |
| `.github/agents/` | Domain-specific agent guidance |

## CI/CD

`.github/workflows/`: `dotnet9.yml`, `dotnet10.yml` (tests on both .NET 9 and 10).
