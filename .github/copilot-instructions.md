# Sharpy Compiler & Standard Library

Sharpy is a statically-typed Pythonic language for .NET. Source `.spy` files compile to C# via Roslyn.

## Architecture Overview

```
Source (.spy) → Lexer → Parser (AST) → Semantic Analysis → RoslynEmitter → C# → .NET IL
```

### Compiler Pipeline (`src/Sharpy.Compiler/`)
1. **Lexer/** - Tokenization (`Lexer.cs` → `Token.cs`)
2. **Parser/** - Recursive descent → AST nodes in `Parser/Ast/` (records with `LineStart`/`ColumnStart` location)
3. **Semantic/** - Multi-pass analysis:
   - `NameResolver` (pass 1: declarations, pass 2: inheritance)
   - `TypeResolver` (resolve type annotations)
   - `TypeChecker` (type validation, narrowing for `is None`/`isinstance`)
4. **CodeGen/** - Roslyn C# generation:
   - `RoslynEmitter*.cs` (split by concern: Expressions, Statements, ClassMembers, etc.)
   - `TypeMapper.cs` (Sharpy types → C# types)
   - `NameMangler.cs` (`snake_case` → `PascalCase`, dunder method mappings)

### Standard Library (`src/Sharpy.Core/`)
- **Partial class pattern**: Types split across `Partial.{Type}/` directories (e.g., `Partial.List/List.ISequence.cs`)
- **Builtins via `partial class Exports`**: Distributed across files (`Print.cs`, `Len.cs`, `Range.cs`, etc.)
- **Python semantics**: Slicing, negative indices, truthiness—verify behavior with `python3 -c "..."` when unsure

## Essential Commands

```bash
dotnet build sharpy.sln                              # Build all
dotnet test                                          # Run all tests
dotnet format                                        # Format before committing
dotnet run --project src/Sharpy.Cli -- run file.spy # Compile and execute
```

**Debugging codegen:** To inspect generated C# when troubleshooting code emission:
```bash
dotnet run --project src/Sharpy.Cli -- emit csharp file.spy
```

**Filtered tests:**
```bash
dotnet test --filter "FullyQualifiedName~Lexer"           # Component tests
dotnet test --filter "FullyQualifiedName~BasicProgram"    # Integration tests
dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"  # File-based tests
```

## Testing Patterns

**CRITICAL**: Never modify expected values to make tests pass. Fix the implementation.

### Integration Tests
- Inherit from `IntegrationTestBase` (`Integration/IntegrationTestBase.cs`)
- Use `CompileAndExecute(source)` → returns `ExecutionResult` with `Success`, `StandardOutput`, `CompilationErrors`
- Full pipeline: Lex → Parse → NameResolver → TypeChecker → RoslynEmitter → Roslyn compile → Execute in-memory

### File-Based Tests (`Integration/TestFixtures/`)
```
TestFixtures/
├── basics/hello_world.spy      # Source
├── basics/hello_world.expected # Expected stdout (exact match)
├── errors/undefined_var.spy    # Error case
└── errors/undefined_var.error  # Substring to match in error
```
Add new tests by creating `.spy` + `.expected` (or `.error`) pairs—auto-discovered at runtime.

**Skip flaky/broken tests:**
```csharp
[Fact(Skip = "TODO: Fix <specific issue>. See issue #123")]
```

## Code Patterns

**AST nodes** are immutable C# records:
```csharp
public record FunctionDef : Statement { public string Name { get; init; } ... }
```

**Semantic info** stored separately from AST in `SemanticInfo` class (not mutating AST nodes).

**RoslynEmitter** uses `SyntaxFactory` exclusively—no string templating:
```csharp
// Good: Use SyntaxFactory
ReturnStatement(LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(42)))
// Bad: String interpolation
$"return {value};"
```

**Type mappings** in `TypeMapper.cs`:
- `list[T]` → `global::Sharpy.Core.List<T>`
- `dict[K,V]` → `global::Sharpy.Core.Dict<K,V>`
- Nullable: `int?` stays `int?`

## Project Structure

| Path | Purpose |
|------|---------|
| `src/Sharpy.Compiler/` | Compiler (Lexer, Parser, Semantic, CodeGen) |
| `src/Sharpy.Core/` | Runtime standard library |
| `src/Sharpy.Cli/` | CLI tool |
| `src/*.Tests/` | Test projects |
| `snippets/*.spy` | Quick test programs |
| `samples/` | Example projects |
| `docs/language_specification/` | Language spec docs |
| `build_tools/` | Auxiliary tooling (dogfood, auto-builder)—separate from core compiler work |

## Key Design Principles

1. **.NET-first**: When Python and .NET conflict, prefer .NET unless zero-cost abstraction is possible
2. **Immutable AST**: All semantic annotations go in `SemanticInfo`, not on AST nodes
3. **Roslyn codegen**: Use `SyntaxFactory`, never string templates
4. **Type narrowing**: `TypeChecker._narrowedTypes` dictionary tracks narrowed types after `is None` checks
5. **Name mangling**: `NameMangler` handles `snake_case` → `PascalCase` and dunder methods (`__str__` → `ToString`)

## CI/CD

Workflows in `.github/workflows/`: `dotnet9.yml`, `dotnet10.yml` (tests on both .NET 9 and 10).
