# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

> **See also:** [.github/copilot-instructions.md](.github/copilot-instructions.md) for full architecture and patterns.

## Repository

- **GitHub owner:** `antonsynd`
- **GitHub repo:** `antonsynd/sharpy`

## Quick Reference

```bash
dotnet build sharpy.sln                              # Build all
dotnet test                                          # Run all tests
dotnet format whitespace                             # Format before committing
dotnet run --project src/Sharpy.Cli -- run file.spy # Compile and execute
dotnet run --project src/Sharpy.Cli -- emit csharp file.spy  # Inspect generated C#
dotnet run --project src/Sharpy.Cli -- emit ast file.spy     # Inspect parsed AST
dotnet run --project src/Sharpy.Cli -- emit tokens file.spy  # Inspect lexer tokens
```

## Architecture

```
Source (.spy) → Lexer → Parser (AST) → Semantic → ValidationPipeline → RoslynEmitter → C# → .NET IL
```

| Component | Location | Purpose |
|-----------|----------|---------|
| Compiler | `src/Sharpy.Compiler/` | Lexer, Parser, Semantic, CodeGen |
| Stdlib | `src/Sharpy.Core/` | Runtime library (partial class pattern in `Partial.{Type}/`) |
| CLI | `src/Sharpy.Cli/` | Command-line interface (`sharpyc`, uses `System.CommandLine`) |
| Tests | `src/*.Tests/` | Unit and integration tests |
| Specs | `docs/language_specification/` | Authoritative language specification |
| Agents | `.github/agents/` | Domain-specific agent guidance (copilot/AI) |
| Instructions | `.github/instructions/` | Per-component contribution guides |
| Snippets | `snippets/*.spy` | Quick test programs |
| Samples | `samples/` | Example projects with `.spyproj` files |

## Critical Rules

1. **Never modify expected values to make tests pass** — fix the implementation
2. **RoslynEmitter uses SyntaxFactory exclusively** — no string templating
3. **Immutable AST** — annotations go in `SemanticInfo`, not AST nodes
4. **Axiom precedence**: .NET > Type Safety > Python Syntax
5. **C# 9.0 target** — no global usings, file-scoped namespaces, or record structs
6. **Always verify Python behavior first** — run `python3 -c "..."` before implementing Python semantics
7. **Language spec is authoritative** — check `docs/language_specification/` before implementing; change implementation to match spec, not the other way around

## Semantic Analysis Pipeline

The semantic phase runs multiple ordered passes. Understanding this is critical for implementation work.

**Pass 1 — Name Resolution** (`NameResolver.cs`): Collects all top-level declarations into `SymbolTable`. Runs `ResolveDeclarations()` then `ResolveInheritance()`.

**Pass 1.5 — Import Resolution** (`ImportResolver.cs`): Loads imported modules via `ModuleLoader` (which caches parsed modules and detects circular imports). Registers imported symbols in SymbolTable. `PackageResolver` handles `__init__.spy` packages.

**Pass 2 — Type Resolution** (`TypeResolver.cs`): Resolves type annotations on declarations to concrete types.

**Pass 3 — Type Checking** (`TypeChecker.cs`, split into 5 partial files: `.cs`, `.Definitions.cs`, `.Expressions.cs`, `.Statements.cs`, `.Utilities.cs`): Traverses AST, infers types, records them in `SemanticInfo`. Then runs `ValidationPipeline`. Type narrowing (e.g., `if x is not None:` narrows `T?` → `T`) is tracked via `_narrowedTypes` dictionary.

**Materialization Points**: After each major phase, computed data is frozen from `SemanticBinding` onto `Symbol` properties:
1. After import resolution → `MaterializeInheritance()` (BaseType, Interfaces)
2. After type checking → `MaterializeVariableTypes()`, `MaterializeCodeGenInfo()`

### Key Data Structures

- **`SemanticInfo`** — Maps AST nodes → types/symbols. Uses `ReferenceEqualityComparer` because AST nodes are records (value equality) but we need identity.
- **`SemanticBinding`** — Stores computed semantic data (CodeGenInfo, variable types) separately from symbols, materialized at phase boundaries.
- **`SymbolTable`** — Global scope of all declared symbols.

### Symbol Hierarchy

Symbols are mutable records that use **reference equality** (overridden from record default) because their properties (Type, BaseType, CodeGenInfo) are set progressively across passes.

```
Symbol (abstract)
├── VariableSymbol   — Type set during type checking
├── FunctionSymbol   — Parameters, ReturnType, IsStatic/Abstract/Virtual/Override
├── TypeSymbol       — TypeKind, BaseType, Interfaces, Fields, Methods
└── ModuleSymbol     — FilePath
```

### SemanticType Hierarchy

All types are immutable records inheriting from `SemanticType` (`Semantic/SemanticType.cs`):

```
SemanticType (abstract)
├── BuiltinType      — Int, Long, Float, Double, Float32, Bool, Str (singletons)
├── GenericType       — list[int], dict[str, int] (Name + TypeArguments)
├── UserDefinedType   — Classes, structs, interfaces (Name + Symbol)
├── NullableType      — T? for .NET interop (UnderlyingType)
├── OptionalType      — T? as safe tagged union (UnderlyingType)
├── FunctionType      — Lambdas/delegates (ParameterTypes + ReturnType)
├── TupleType         — tuple[int, str] (ElementTypes)
├── ModuleType        — Imported modules as namespaces
├── TypeParameterType — Generic type parameters (T in class Box[T])
├── ResultType        — T !E tagged union (OkType + ErrorType)
├── UnionType         — Tagged unions (v0.2.x placeholder)
├── TaskType          — Async Task types (v0.2.x placeholder)
├── VoidType          — None return type
└── UnknownType       — Error recovery
```

### ValidationPipeline

Pluggable validators implement `ISemanticValidator` with an `Order` property (lower runs first). Key validators:

- **Order 50**: `ModuleLevelValidator` — Entry point validation
- **Order 150**: `SignatureValidator` — Dunder method signatures
- **Order 400**: `ControlFlowValidator` — CFG-based unreachable code, missing returns
- **Order 450**: `AccessValidator` — Private/protected member access
- **Order 500**: `ProtocolValidator`, `OperatorValidator` — Protocol/operator validation

**Responsibility split**: TypeChecker handles type mismatches and in-progress inference. ValidationPipeline handles self-contained AST analyses that don't need active inference state.

## Code Generation

The `RoslynEmitter` is split into 8 partial classes (~220KB total): `RoslynEmitter.cs` (entry, name resolution), `.Expressions.cs`, `.Statements.cs`, `.TypeDeclarations.cs`, `.ClassMembers.cs`, `.CompilationUnit.cs`, `.ModuleClass.cs`, `.Operators.cs`.

**Name resolution strategy**:
- Module-level symbols → `Symbol.CodeGenInfo` (precomputed during semantic analysis)
- Local variables → runtime tracking via `_variableVersions` (handles redeclarations: x, x_1, x_2)
- Types → SymbolTable lookup

**Type mappings** (`TypeMapper.cs`): `int` → `long`, `str` → `string`, `float` → `double`, `list[T]` → `global::Sharpy.Core.List<T>`, `dict[K,V]` → `global::Sharpy.Core.Dict<K,V>`

**Name mangling** (`NameMangler.cs`): `snake_case` → `PascalCase`, `__init__` → constructor, `__add__` → `operator+`, `__str__` → `ToString()`

## Design Anti-Patterns

Avoid these patterns (from `.github/agents/design-philosophy-guardian.agent.md`):

| Pattern | Problem |
|---------|---------|
| "Add X because Python has it" | Feature creep — each feature must earn its complexity |
| Runtime type checking | Should be compile-time |
| Wrapper types for Pythonic API | Use extension methods instead |
| Multiple ways to do same thing | Consistency issue |
| Magic behavior | Unpredictable; prefer explicit |

## Axiom Conflict Resolutions

When the three axioms conflict, precedence is: **Axiom 1 (.NET) > Axiom 3 (Types) > Axiom 2 (Python)**. If a conflict can be resolved at zero cost, satisfy all axioms. Common resolved conflicts:

| Conflict | Resolution |
|----------|------------|
| Integer division (`//`) | Axiom 1 wins — provide `math.floor_div()` helper |
| String indexing (code points vs UTF-16) | Axiom 1 wins — use UTF-16 with helper methods |
| `global`/`nonlocal` keywords | Axiom 1 wins — C# scoping rules apply |
| Duck typing | Axiom 1+3 win — use explicit interfaces |

## Feature Implementation Order

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

## Multi-File Compilation

`AssemblyCompiler` handles multi-file projects using `.spyproj` files:
```bash
dotnet run --project src/Sharpy.Cli -- project samples/calculator_app/calculator.spyproj
```

Programmatic multi-file tests use `ProjectCompilationHelper`:
```csharp
using var helper = new ProjectCompilationHelper(output);
helper.WithRootNamespace("Test")
    .AddSourceFile("main.spy", "...")
    .AddSourceFile("lib.spy", "...")
    .CreateProjectFile();
var result = helper.Compile();
```

## Sharpy.Core Patterns

- **Wrap .NET internally, expose Python API** — `list.append()` not `Add()`
- **Partial class pattern**: Types split across `Partial.{Type}/` directories (e.g., `Partial.List/List.ISequence.cs`)
- **Builtins**: `partial class Exports` split across `Print.cs`, `Len.cs`, `Range.cs`, etc.
- **Python semantics**: Negative indexing, slicing, Python-matching exceptions

## Custom Slash Commands

Available in `.claude/commands/`:

| Command | Purpose |
|---------|---------|
| `/project:implement <task>` | Implement a feature end-to-end |
| `/project:review <target>` | Code review (read-only analysis) |
| `/project:plan <feature>` | Decompose complex task into subtasks |
| `/project:test <component>` | Run tests for a component |
| `/project:emit <file.spy>` | Inspect generated C# code |
| `/project:verify-python <expr>` | Verify Python behavior |
| `/project:fix-issue <issue>` | Diagnose and fix a GitHub issue |
| `/project:add-test-fixture <desc>` | Create file-based test |
| `/project:check-axioms <decision>` | Verify axiom compliance |

## Testing

```bash
dotnet test --filter "FullyQualifiedName~Lexer"            # By component
dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"  # File-based tests
dotnet test --filter "DisplayName~test_name"               # By test name
```

### File-Based Tests

Location: `src/Sharpy.Compiler.Tests/Integration/TestFixtures/`

**Single-file tests**: `.spy` + `.expected` (exact stdout match) or `.spy` + `.error` (substring match in error)

**Multi-file tests**: A subdirectory with multiple `.spy` files and a `main.spy` entry point, plus `main.expected` or `main.error`.

**Warning tests**: `.warning` file — empty means expect no warnings, non-empty lines are expected substrings. Can combine with `.expected`.

**Skip**: Add a `.skip` file next to the `.spy` file.

### Integration Test Base

Programmatic tests inherit `IntegrationTestBase` and use `CompileAndExecute(source)`:
```csharp
var result = CompileAndExecute("print(1 + 2)");
Assert.True(result.Success);
Assert.Equal("3\n", result.StandardOutput);
```

## CI/CD

`.github/workflows/`: `dotnet9.yml` (tests on .NET 9), `dotnet10.yml` (tests on .NET 10).
