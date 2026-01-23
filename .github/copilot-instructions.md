# Sharpy Compiler & Standard Library

Sharpy is a statically-typed Pythonic language for .NET. Source `.spy` files compile to C# via Roslyn.

> **Specialized Guidance:** See [agents/](agents/) for domain experts and [instructions/](instructions/) for component guides.

## Architecture

```
Source (.spy) → Lexer → Parser (AST) → Semantic → ValidationPipeline → RoslynEmitter → C# → .NET IL
```

### Compiler Pipeline (`src/Sharpy.Compiler/`)
| Stage | Key Files | Purpose |
|-------|-----------|---------|
| Lexer | `Lexer/Lexer.cs`, `Token.cs` | Tokenization, indentation tracking |
| Parser | `Parser/Parser.cs`, `Parser/Ast/*.cs` | Recursive descent → immutable AST records |
| Semantic | `Semantic/{NameResolver,TypeResolver,TypeChecker}.cs` | Multi-pass: declarations → inheritance → types |
| Validation | `Semantic/Validation/ValidationPipeline.cs` | Pluggable validators (operators, protocols, access) |
| CodeGen | `CodeGen/RoslynEmitter*.cs`, `TypeMapper.cs`, `NameMangler.cs` | Roslyn `SyntaxFactory` → C# AST |

### Standard Library (`src/Sharpy.Core/`)
- **Partial class pattern**: `Partial.{Type}/` directories (e.g., `Partial.List/List.ISequence.cs`)
- **Builtins**: `partial class Exports` split across `Print.cs`, `Len.cs`, `Range.cs`, etc.
- **Operator protocols**: `I*.cs` interfaces (`IAddable`, `IEquatable`, etc.)

## Essential Commands

```bash
dotnet build sharpy.sln                              # Build all
dotnet test                                          # Run all tests
dotnet format whitespace                             # Format before committing
dotnet run --project src/Sharpy.Cli -- run file.spy # Compile and execute
dotnet run --project src/Sharpy.Cli -- emit csharp file.spy  # Debug codegen
dotnet run --project src/Sharpy.Cli -- emit ast file.spy     # Debug parser
```

**Filtered tests:**
```bash
dotnet test --filter "FullyQualifiedName~Lexer"                  # By component
dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"  # File-based
```

**Python verification** (always verify Python semantics first):
```bash
python3 -c "print([1,2,3][-1])"  # Verify expected behavior
```

## Critical Rules

1. **Never modify expected values to make tests pass** — fix the implementation
2. **RoslynEmitter uses SyntaxFactory exclusively** — no string templating
3. **Immutable AST** — annotations go in `SemanticInfo`, not AST nodes
4. **Axiom precedence**: .NET > Type Safety > Python Syntax
5. **C# 9.0 target** — no global usings, file-scoped namespaces, or record structs

## Testing

### Integration Tests
Inherit `IntegrationTestBase`, use `CompileAndExecute(source)`:
```csharp
var result = CompileAndExecute("print(1 + 2)");
Assert.True(result.Success);
Assert.Equal("3\n", result.StandardOutput);
```

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

**AST nodes** are immutable records:
```csharp
public record FunctionDef : Statement { public string Name { get; init; } ... }
```

**RoslynEmitter** uses SyntaxFactory exclusively:
```csharp
// ✅ Correct
ReturnStatement(LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(42)))
// ❌ Wrong
$"return {value};"
```

**Type mappings** (`TypeMapper.cs`): `list[T]` → `global::Sharpy.Core.List<T>`

**Name mangling** (`NameMangler.cs`): `snake_case` → `PascalCase`, `__str__` → `ToString()`

## The Three Axioms

| Axiom | Principle | Priority |
|-------|-----------|----------|
| **1 (.NET)** | Sharpy compiles to C# 9.0 for the .NET CLR | Highest |
| **3 (Types)** | Explicit static typing, non-nullable by default | Medium |
| **2 (Python)** | Sharpy uses Python 3 syntax and idioms | Yields |

## Project Layout

| Path | Purpose |
|------|---------|
| `src/Sharpy.Compiler/` | Compiler (Lexer, Parser, Semantic, CodeGen) |
| `src/Sharpy.Core/` | Runtime stdlib (collections, builtins) |
| `src/Sharpy.Cli/` | CLI entry point (`System.CommandLine`) |
| `src/*.Tests/` | Test projects |
| `docs/language_specification/` | Authoritative language spec |
| `snippets/*.spy` | Quick test programs |
| `samples/` | Example projects |
| `.github/agents/` | Domain-specific agent guidance |
| `.github/instructions/` | Component contribution guides |

## CI/CD

`.github/workflows/`: `dotnet9.yml` (tests on .NET 9), `dotnet10.yml` (tests on .NET 10).
