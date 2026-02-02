# Sharpy Compiler & Standard Library

Sharpy is a statically-typed Pythonic language for .NET. Source `.spy` files compile to C# via Roslyn.

> **Deep dives:** [CLAUDE.md](../CLAUDE.md) (architecture), [agents.md](agents.md) (domain experts), `docs/language_specification/` (authoritative spec)

## The Three Axioms (Design Precedence)

| Priority | Axiom | When conflicts arise... |
|----------|-------|------------------------|
| 1 | **.NET** | Always compiles to valid C# for CLR |
| 2 | **Types** | Static typing, non-nullable by default |
| 3 | **Python** | Syntax/idioms yield to above |

## Architecture & Pipeline

```
.spy → Lexer → Parser (AST) → Semantic → ValidationPipeline → RoslynEmitter → C# → IL
```

| Stage | Key Files | Notes |
|-------|-----------|-------|
| Lexer | `Compiler/Lexer/Lexer.cs`, `Token.cs` | Indentation-aware tokenization |
| Parser | `Compiler/Parser/Parser*.cs` (6 partials), `Ast/*.cs` | Immutable AST records |
| Semantic | `Compiler/Semantic/{NameResolver,TypeResolver,TypeChecker}.cs` | 5 ordered passes—see below |
| CodeGen | `Compiler/CodeGen/RoslynEmitter*.cs` (8 partials) | **SyntaxFactory only**—no string templating |

### Semantic Pass Order (Critical)
1. `NameResolver.ResolveDeclarations()` → symbol table
2. `NameResolver.ResolveInheritance()` → base classes
3. `ImportResolver` → module loading
4. `TypeResolver` → type annotations
5. `TypeChecker` (5 partials) → inference + `ValidationPipeline`

**Key structures:** `SemanticInfo` (AST→type via `ReferenceEqualityComparer`), `SymbolTable`, `SemanticBinding`

## Essential Commands

```bash
dotnet build sharpy.sln && dotnet test               # Build + test
dotnet run --project src/Sharpy.Cli -- run file.spy  # Execute .spy file
dotnet run --project src/Sharpy.Cli -- emit csharp file.spy  # Debug codegen
dotnet run --project src/Sharpy.Cli -- emit ast file.spy     # Debug parser
dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"  # File-based tests
python3 -c "..."                                     # Verify Python semantics FIRST
```

## Critical Rules

1. **Never modify `.expected` to pass tests**—fix the implementation
2. **RoslynEmitter**: `SyntaxFactory` only, no `$"return {x};"` strings
3. **Immutable AST**: annotations go in `SemanticInfo`, not AST nodes
4. **C# targets**: `Sharpy.Core` → C# 9.0 (`netstandard2.0;2.1`); others → `net10.0`
5. **Spec is authoritative**: check `docs/language_specification/` before implementing
6. **Verify Python first**: `python3 -c "print([1,2,3][-1])"` before coding

## Testing Patterns

### File-Based Tests (`src/Sharpy.Compiler.Tests/Integration/TestFixtures/`)
```
feature/test.spy + test.expected  # Success (exact stdout match)
errors/bad.spy + bad.error        # Failure (substring in error)
multifile/main.spy + lib.spy + main.expected  # Multi-file
```
Auto-discovered. Add `.skip` to skip, `.warning` for warning tests.

### Programmatic Tests
```csharp
var result = CompileAndExecute("print(1 + 2)");
Assert.Equal("3\n", result.StandardOutput);
```

## Key Mappings

| Sharpy | C# | Notes |
|--------|-----|-------|
| `int` | `int` | 32-bit |
| `long` | `long` | 64-bit |
| `str` | `string` | |
| `list[T]` | `System.Collections.Generic.List<T>` | v0.1.x; `Sharpy.Core.List<T>` in v0.2.x+ |
| `snake_case` | `PascalCase` | Via `NameMangler` |
| `__init__` | constructor | |
| `__str__` | `ToString()` | |

## Sharpy.Core Patterns

- **Partial class pattern**: `Partial.List/List.Methods.cs`, `List.Slicing.cs`
- **Builtins**: `partial class Exports` in `Print.cs`, `Len.cs`, `Range.cs`
- **Python semantics**: negative indexing, slicing, Python-matching exceptions

## Feature Implementation Order

```
Lexer → Parser → Semantic → Validation → CodeGen → Tests
```

## Project Layout

| Path | Purpose |
|------|---------|
| `src/Sharpy.Compiler/` | Compiler pipeline |
| `src/Sharpy.Core/` | Runtime stdlib |
| `src/Sharpy.Cli/` | CLI (`System.CommandLine`) |
| `docs/language_specification/` | **Authoritative** spec (100+ files) |
| `.github/agents/` | Domain-specific AI agents |
