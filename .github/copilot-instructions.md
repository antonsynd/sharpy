# Sharpy Compiler & Standard Library

Sharpy is a statically-typed Pythonic language for .NET. Source `.spy` files compile to C# via Roslyn.

> **See also:** [CLAUDE.md](../CLAUDE.md) for detailed architecture, [agents/](agents/) for domain experts, [agents.md](agents.md) for agent reference.

## The Three Axioms

When design decisions conflict, this precedence applies:

| Priority | Axiom | Principle |
|----------|-------|-----------|
| 1 (Highest) | **.NET** | Compiles to valid C# for .NET CLR |
| 2 | **Types** | Explicit static typing, non-nullable by default |
| 3 (Yields) | **Python** | Python 3 syntax and idioms |

## Architecture

```
Source (.spy) → Lexer → Parser (AST) → Semantic → ValidationPipeline → RoslynEmitter → C# → .NET IL
```

### Compiler Pipeline (`src/Sharpy.Compiler/`)
| Stage | Key Files | Purpose |
|-------|-----------|---------|
| Lexer | `Lexer/Lexer.cs`, `Token.cs` | Tokenization, indentation tracking |
| Parser | `Parser/Parser*.cs` (6 partials), `Ast/*.cs` | Recursive descent → immutable AST records |
| Semantic | `Semantic/{NameResolver,TypeResolver,TypeChecker}.cs` | Multi-pass: declarations → inheritance → types |
| Validation | `Semantic/Validation/ValidationPipeline.cs` | Pluggable validators (operators, protocols, access) |
| CodeGen | `CodeGen/RoslynEmitter*.cs` (8 partials), `TypeMapper.cs` | Roslyn `SyntaxFactory` → C# AST |

### Semantic Analysis Pipeline (Critical)
Understanding the pass order is essential for compiler work:
1. **NameResolver.ResolveDeclarations()** — Build symbol table
2. **NameResolver.ResolveInheritance()** — Resolve base classes
3. **ImportResolver** — Load imported modules, detect circular imports
4. **TypeResolver** — Resolve type annotations to concrete types
5. **TypeChecker** (5 partials) — Infer types, run ValidationPipeline

**Key data structures**: `SemanticInfo` (AST node → type/symbol using `ReferenceEqualityComparer`), `SymbolTable` (global scope), `SemanticBinding` (computed data frozen at phase boundaries)

**Key registries**: `OperatorRegistry`, `ProtocolRegistry`, `BuiltinRegistry`, `PrimitiveCatalog` (source of truth for primitive types)

### Standard Library (`src/Sharpy.Core/`)
- **Partial class pattern**: `Partial.{Type}/` directories (e.g., `Partial.List/List.Methods.cs`, `List.Slicing.cs`)
- **Builtins**: `partial class Exports` split across `Print.cs`, `Len.cs`, `Range.cs`, etc.
- **Python semantics**: Negative indexing, slicing, Python-matching exceptions

## Essential Commands

```bash
dotnet build sharpy.sln                              # Build all
dotnet test                                          # Run all tests
dotnet format whitespace                             # Format before committing
dotnet run --project src/Sharpy.Cli -- run file.spy # Compile and execute
dotnet run --project src/Sharpy.Cli -- emit csharp file.spy  # Debug codegen
dotnet run --project src/Sharpy.Cli -- emit ast file.spy     # Debug parser
dotnet run --project src/Sharpy.Cli -- emit tokens file.spy  # Debug lexer
```

**Filtered tests:**
```bash
dotnet test --filter "FullyQualifiedName~Lexer"                  # By component
dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"  # File-based
dotnet test --filter "DisplayName~test_name"                     # By test name
```

**Python verification** (always verify Python semantics first):
```bash
python3 -c "print([1,2,3][-1])"  # Verify expected behavior before implementing
```

## Critical Rules

1. **Never modify expected values to make tests pass** — fix the implementation
2. **RoslynEmitter uses SyntaxFactory exclusively** — no string templating
3. **Immutable AST** — annotations go in `SemanticInfo`, not AST nodes
4. **Axiom precedence**: .NET > Type Safety > Python Syntax
5. **C# targets**: `Sharpy.Core` → C# 9.0 (`netstandard2.0;2.1`); Compiler/CLI → `net10.0` with `LangVersion latest`
6. **Language spec is authoritative** — check `docs/language_specification/` before implementing
7. **Always verify Python behavior first** — run `python3 -c "..."` before implementing Python semantics

## Testing

### File-Based Tests (`Integration/TestFixtures/`)
```
TestFixtures/
├── basics/hello_world.spy      # Source
├── basics/hello_world.expected # Expected stdout (exact match)
├── errors/undefined_var.spy    # Error case
└── errors/undefined_var.error  # Substring in error message
```
Add `.spy` + `.expected` (or `.error`) pairs—auto-discovered. Skip with `.skip` file. Warnings: `.warning` file.

**Multi-file tests**: A subdirectory with multiple `.spy` files + `main.spy` entry point + `main.expected` or `main.error`.

### Programmatic Tests
Inherit `IntegrationTestBase`, use `CompileAndExecute(source)`:
```csharp
var result = CompileAndExecute("print(1 + 2)");
Assert.True(result.Success);
Assert.Equal("3\n", result.StandardOutput);
```

For multi-file programmatic tests, use `ProjectCompilationHelper`:
```csharp
using var helper = new ProjectCompilationHelper(output);
helper.WithRootNamespace("Test")
    .AddSourceFile("main.spy", "...")
    .AddSourceFile("lib.spy", "...")
    .CreateProjectFile();
var result = helper.Compile();
```

## Code Patterns

**RoslynEmitter** uses SyntaxFactory exclusively:
```csharp
// ✅ Correct
ReturnStatement(LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(42)))
// ❌ Wrong — no string templating
$"return {value};"
```

**Type mappings** (`CodeGen/TypeMapper.cs`): `int` → `long`, `str` → `string`, `list[T]` → `global::Sharpy.Core.List<T>`

**Name mangling** (`NameMangler.cs`): `snake_case` → `PascalCase`, `__init__` → constructor, `__str__` → `ToString()`

**Sharpy.Core patterns**: Wrap .NET internally, expose Python API (`list.append()` not `Add()`)

## Feature Implementation Order

For new language features, touch components in dependency order:
```
Lexer → Parser → Semantic → Validation → CodeGen → Tests
```

## Project Layout

| Path | Purpose |
|------|---------|
| `src/Sharpy.Compiler/` | Compiler (Lexer, Parser, Semantic, CodeGen) |
| `src/Sharpy.Core/` | Runtime stdlib (collections, builtins) |
| `src/Sharpy.Cli/` | CLI entry point (`System.CommandLine`) |
| `src/*.Tests/` | Test projects |
| `docs/language_specification/` | **Authoritative** language spec |
| `snippets/*.spy` | Quick test programs |
| `.github/agents/` | Domain-specific agent guidance |

## CI/CD

`.github/workflows/dotnet10.yml` runs tests on .NET 10. An `.editorconfig` enforces formatting.
