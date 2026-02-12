# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

> **See also:** [.github/copilot-instructions.md](.github/copilot-instructions.md) for full architecture and patterns, [.github/agents.md](.github/agents.md) for the agent registry.

## Repository

- **GitHub owner:** `antonsynd`
- **GitHub repo:** `antonsynd/sharpy`

## Quick Reference

```bash
dotnet build sharpy.sln                              # Build all
dotnet test                                          # Run all tests
dotnet format whitespace                             # Required before committing
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
| Build Tools | `build_tools/` | Python-based build automation and dogfooding tools |
| Agents | `.github/agents/` | Domain-specific agent guidance (copilot/AI) |
| Instructions | `.github/instructions/` | Per-component contribution guides |
| Snippets | `snippets/*.spy` | Quick test programs |

## Critical Rules

1. **Never modify expected values to make tests pass** — fix the implementation
2. **RoslynEmitter uses SyntaxFactory exclusively** — no string templating
3. **Immutable AST** — annotations go in `SemanticInfo`, not AST nodes
4. **Axiom precedence**: .NET > Type Safety > Python Syntax
5. **C# 9.0 target for Sharpy.Core only** — `Sharpy.Core` targets `netstandard2.1;netstandard2.0` with `LangVersion 9.0` (no global usings, file-scoped namespaces, or record structs). `Sharpy.Compiler` and `Sharpy.Cli` target `net10.0` with `LangVersion latest`.
6. **Always verify Python behavior first** — run `python3 -c "..."` before implementing Python semantics
7. **Language spec is authoritative** — check `docs/language_specification/` before implementing; change implementation to match spec, not the other way around
8. **TODO/BUG/FIXME comments must have GitHub issues** — when leaving a `TODO`, `BUG`, or `FIXME` comment in code, always create a corresponding GitHub issue first (via `gh issue create`) and reference it in the comment (e.g., `// TODO(#123): ...`). This makes deferred work visible at the project level, not buried in code.

## Semantic Analysis Pipeline

The semantic phase runs multiple ordered passes. Understanding this is critical for implementation work.

**Pass 1 — Name Resolution** (`NameResolver.cs`): Collects all top-level declarations into `SymbolTable`. Runs `ResolveDeclarations()` then `ResolveInheritance()`.

**Pass 1.5 — Import Resolution** (`ImportResolver.cs`): Loads imported modules via `ModuleLoader` (which caches parsed modules and detects circular imports). Registers imported symbols in SymbolTable. `PackageResolver` handles `__init__.spy` packages.

**Pass 2 — Type Resolution** (`TypeResolver.cs`): Resolves type annotations on declarations to concrete types. Type inference provided by `TypeInferenceService` and `GenericTypeInferenceService`.

**Pass 3 — Type Checking** (`TypeChecker.cs`, split into 5 partial files: `.cs`, `.Definitions.cs`, `.Expressions.cs`, `.Statements.cs`, `.Utilities.cs`): Traverses AST, infers types, records them in `SemanticInfo`. Then runs `ValidationPipeline`. Type narrowing (e.g., `if x is not None:` narrows `T?` → `T`) is tracked via `_narrowingContext` (`TypeNarrowingContext`).

**Key registries**: `OperatorRegistry`, `ProtocolRegistry`, `BuiltinRegistry`, `ModuleRegistry`, `PrimitiveCatalog` (source of truth for primitive types and CLR mappings).

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
├── VariableSymbol        — Type set during type checking
├── FunctionSymbol        — Parameters, ReturnType, IsStatic/Abstract/Virtual/Override
├── TypeSymbol            — TypeKind, BaseType, Interfaces, Fields, Methods
├── ModuleSymbol          — FilePath
├── TypeAliasSymbol       — Aliased type reference
└── TypeParameterSymbol   — Generic type parameters (T in class Box[T])

PropertySymbol   — Standalone record (not a Symbol subclass)
ParameterSymbol  — Standalone record (not a Symbol subclass)
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
├── GenericFunctionType — Generic functions with type parameters
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

Pluggable validators implement `ISemanticValidator` with an `Order` property (lower runs first):

- **Order 50**: `ModuleLevelValidator` — Entry point validation
- **Order 55**: `NamingConventionValidator` — Naming convention checks
- **Order 60**: `DecoratorValidator` — Decorator validation
- **Order 150**: `SignatureValidator` — Dunder method signatures
- **Order 250**: `DefaultParameterValidator` — Default parameter validation
- **Order 400**: `ControlFlowValidator` — CFG-based unreachable code, missing returns
- **Order 420**: `UnusedVariableValidator` — Unused variable warnings
- **Order 430**: `UnusedImportValidator` — Unused import warnings
- **Order 450**: `AccessValidator` — Private/protected member access
- **Order 500**: `ProtocolValidator`, `OperatorValidator` — Protocol/operator validation

**Responsibility split**: TypeChecker handles type mismatches and in-progress inference. ValidationPipeline handles self-contained AST analyses that don't need active inference state.

## Diagnostic Code Ranges

All diagnostics use `SPY` prefix (`Diagnostics/DiagnosticCodes.cs`):

| Range | Level | Component |
|-------|-------|-----------|
| SPY0001–SPY0099 | Error | Lexer |
| SPY0100–SPY0199 | Error | Parser |
| SPY0200–SPY0399 | Error | Semantic |
| SPY0400–SPY0449 | Error | Validation |
| SPY0450–SPY0499 | Warning | Validation (unreachable code, naming conventions) |
| SPY0500–SPY0599 | Error | Code generation |
| SPY1000–SPY1099 | Info | Informational (e.g., implicit interface synthesis) |

## Code Generation

The `RoslynEmitter` is split into 8 partial classes (~6,225 lines total): `RoslynEmitter.cs` (entry, name resolution), `.Expressions.cs`, `.Statements.cs`, `.TypeDeclarations.cs`, `.ClassMembers.cs`, `.CompilationUnit.cs`, `.ModuleClass.cs`, `.Operators.cs`.

**Name resolution strategy**:
- Module-level symbols → `Symbol.CodeGenInfo` (precomputed during semantic analysis)
- Local variables → runtime tracking via `_variableVersions` (handles redeclarations: x, x_1, x_2)
- Types → SymbolTable lookup

**Type mappings** (`CodeGen/TypeMapper.cs`): `int` → `int`, `long` → `long`, `str` → `string`, `float` → `double`, `list[T]` → `System.Collections.Generic.List<T>`, `dict[K,V]` → `System.Collections.Generic.Dictionary<K,V>` (v0.1.x uses .NET types directly; Sharpy.Core wrappers planned for v0.2.x+). Note: a separate `Discovery/TypeMapper.cs` maps CLR types back to Sharpy `SemanticType` instances.

**Name mangling** (`NameMangler.cs`): `snake_case` → `PascalCase`, `__init__` → constructor, `__add__` → `operator+`, `__str__` → `ToString()`

## Design Anti-Patterns

Avoid these patterns:

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
2. **Parser** (`Parser/Ast/`) — Add AST record, parsing rule. Parser is split into 6 partial files: `.cs`, `.Definitions.cs`, `.Expressions.cs`, `.Primaries.cs`, `.Statements.cs`, `.Types.cs`
3. **Semantic** (`Semantic/`) — Add type checking in `TypeChecker*.cs`
4. **Validation** (`Semantic/Validation/`) — Add validator if needed
5. **CodeGen** (`CodeGen/RoslynEmitter*.cs`) — Emit via `SyntaxFactory`
6. **Tests** — Unit tests per component + `.spy`/`.expected` integration tests

## Multi-File Compilation

`ProjectCompiler` and `SpyProject`/`SpyProjectLoader` (in `Project/`) handle multi-file projects using `.spyproj` files:
```bash
dotnet run --project src/Sharpy.Cli -- project path/to/project.spyproj
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

### Incremental Compilation

Enable incremental compilation with `--incremental` to skip unchanged files:

```bash
dotnet run --project src/Sharpy.Cli -- project path/to/project.spyproj --incremental
```

**How it works:**

1. **First build**: All files are compiled, symbols and generated C# are cached to `obj/{Config}/.sharpy-symbols`
2. **Subsequent builds**: Files are skipped if their content hash matches the cache AND no dependencies changed
3. **Transitive dependencies**: If file A imports B and B changes, A is recompiled (uses cached dependency graph)

**Cache files** (in `obj/{Config}/`):
- `.sharpy-cache` — File content hashes (SHA-256) with compiler version
- `.sharpy-symbols` — Serialized symbols and generated C# per file with schema version

**Cache invalidation**: Caches are automatically invalidated when:
- Compiler version changes (assembly hash changes)
- Symbol cache schema version changes
- Source file content changes

**Force full rebuild**: Delete the cache files or use `--clean` flag.

**Implementation**: `IncrementalCompilationCache`, `SymbolSerializer`, `SymbolCache` (all in `Project/`)

## Sharpy.Core Patterns

- **Wrap .NET internally, expose Python API** — `list.append()` not `Add()`
- **Partial class pattern**: Types split across `Partial.{Type}/` directories (e.g., `Partial.List/List.Methods.cs`, `List.Slicing.cs`, `List.Interfaces.cs`)
- **Builtins**: `partial class Builtins` split across `Print.cs`, `Len.cs`, `Range.cs`, etc.
- **Python semantics**: Negative indexing, slicing, Python-matching exceptions

### Protocol Interfaces

Protocol interfaces enable builtin function dispatch (e.g., `len()`, `bool()`) via compile-time interfaces:

- `ISized` — `int Count { get; }` — implemented by List, Set, Dict; synthesized when `__len__` is present
- `IBoolConvertible` — `bool __Bool__()` — synthesized when `__bool__` is present

The emitter implicitly adds these interfaces to a class's base list when the corresponding dunder method is detected (emits SPY1001 info diagnostic).

## Skills

Available in `.claude/skills/`:

| Command | Purpose |
|---------|---------|
| `/project:emit <file.spy>` | Inspect generated C#, AST, tokens, and parse output |
| `/project:verify-python <expr>` | Run Python 3 to verify behavior before implementing |
| `/project:add-test-fixture <desc>` | Create a file-based integration test |
| `/project:dogfood-analyze [dir]` | Analyze dogfood results and classify failures by root cause |

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

**C# snapshot tests**: `.expected.cs` file — the expected generated C# output (Roslyn-normalized). Used selectively for ~15 representative fixtures to detect codegen changes that don't affect runtime output. To regenerate: `UPDATE_SNAPSHOTS=true dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"`.

**Skip**: Add a `.skip` file next to the `.spy` file.

### Integration Test Base

Programmatic tests inherit `IntegrationTestBase` and use `CompileAndExecute(source)`:
```csharp
var result = CompileAndExecute("print(1 + 2)");
Assert.True(result.Success);
Assert.Equal("3\n", result.StandardOutput);
```

## Compiler Subdirectories

Key subdirectories within `src/Sharpy.Compiler/` not covered above:

| Path | Purpose |
|------|---------|
| `Analysis/ControlFlow/` | `ControlFlowGraph`, `ControlFlowGraphBuilder`, `BasicBlock` |
| `Diagnostics/` | `DiagnosticBag`, `DiagnosticCodes`, `DiagnosticRenderer`, `CompilationMetrics` |
| `Discovery/` | CLR type discovery: `TypeMapper`, `CachedModuleDiscovery` |
| `Discovery/Caching/` | `OverloadIndex`, `OverloadIndexCache`, `AssemblyIdentity` |
| `Model/` | `CompilationUnit`, `CompilationUnitFactory`, `ProjectModel` |
| `Project/` | `ProjectCompiler`, `SpyProject`, `DependencyGraph` |
| `Services/` | `CompilerServices`, `CompilerServicesBuilder` (adapter pattern) |
| `Text/` | `ILocatable`, `SourceText`, `TextSpan` |

## CI/CD

`.github/workflows/`:
- `dotnet10.yml` — Active; tests on .NET 10
- `python-build-tools.yml` — Runs pytest for `build_tools/` on Python 3.11 and 3.12

An `.editorconfig` at the repo root enforces C# formatting and naming conventions.
