# Sharpy Compiler & Standard Library

Sharpy is a statically-typed Pythonic language for .NET. Source `.spy` files compile to C# via Roslyn.

> **See also:** [CLAUDE.md](../CLAUDE.md) (rules, workflow, operational contracts), [agents.md](agents.md) (domain experts), `docs/language_specification/` (authoritative spec). This file is the architecture reference.

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
| Lexer | `Compiler/Lexer/Lexer*.cs` (partials: FStrings, Indentation, Literals), `Token.cs` | Indentation-aware tokenization |
| Parser | `Compiler/Parser/Parser*.cs` (partials: Definitions, Expressions, Primaries, Statements, Types), `Ast/*.cs` | Immutable AST records; recursive descent with precedence levels |
| Semantic | `Compiler/Semantic/` | Ordered passes — see below |
| CodeGen | `Compiler/CodeGen/RoslynEmitter*.cs` | **SyntaxFactory only** — no string templating |

## Semantic Analysis Pipeline

**Pass 1 — Name Resolution** (`NameResolver.cs`): Collects all top-level declarations into `SymbolTable`. Runs `ResolveDeclarations()` then `ResolveInheritance()`.

**Pass 1.5 — Import Resolution** (`ImportResolver.cs`): Loads imported modules via `ModuleLoader` (caches parsed modules, detects circular imports). Registers imported symbols in the SymbolTable. `PackageResolver` handles `__init__.spy` packages.

**Pass 2 — Type Resolution** (`TypeResolver.cs`): Resolves type annotations on declarations to concrete types. Type inference provided by `TypeInferenceService` and `GenericTypeInferenceService`.

**Pass 3 — Type Checking** (`TypeChecker.cs`, split into partial files by area: Definitions, Expressions, Expressions.Access/.Calls/.Lambdas, Literals, Operators, Statements, Patterns, Utilities): Traverses the AST, infers types, records them in `SemanticInfo`, then runs the `ValidationPipeline`.

**Key registries**: `OperatorRegistry`, `ProtocolRegistry`, `BuiltinRegistry`, `ModuleRegistry`, `PrimitiveCatalog` (source of truth for primitive types and CLR mappings).

**Materialization points** — after each major phase, computed data is frozen from `SemanticBinding` onto `Symbol` properties:
1. After import resolution → `MaterializeInheritance()` (BaseType, Interfaces)
2. After type checking → `MaterializeVariableTypes()`, `MaterializeCodeGenInfo()`

### Type Narrowing

Statement-level flow (e.g. `if x is not None:` narrows `T?` → `T`) comes from CFG dataflow (`NarrowingFlowAnalysis`); expression-level scopes (ternary arms, `and`/`or` RHS) and the match-arm scope guard use `_narrowingContext` (`TypeNarrowingContext`). At every narrowed read the TypeChecker materializes a node-keyed `NarrowedReadLowering` in `SemanticInfo` (`.Unwrap()`/`.Value`/`!`/cast), which codegen applies verbatim — the emitter performs no narrowing flow re-derivation (#1081, #1080).

### Key Data Structures

- **`SemanticInfo`** — Maps AST nodes → types/symbols. Uses `ReferenceEqualityComparer` because AST nodes are records (value equality) but identity is needed. Per-file instances merge into the project-wide instance at `SemanticInfo.MergeFrom` — every node-keyed dictionary must participate or its entries are silently dropped.
- **`SemanticBinding`** — Stores computed semantic data (CodeGenInfo, variable types) separately from symbols, materialized at phase boundaries.
- **`SymbolTable`** — Global scope of all declared symbols.

### Symbol Hierarchy

Symbols are mutable records using **reference equality** (overridden from the record default) because their properties (Type, BaseType, CodeGenInfo) are set progressively across passes.

```
Symbol (abstract)         — DeclarationSpan, DeclaringFilePath (all symbols)
├── VariableSymbol        — Type set during type checking
├── FunctionSymbol        — Parameters, ReturnType, IsStatic/Abstract/Virtual/Override
├── TypeSymbol            — TypeKind, BaseType, Interfaces, Fields, Methods, DefiningFilePath
├── ModuleSymbol          — FilePath
├── TypeAliasSymbol       — Aliased type reference
└── TypeParameterSymbol   — Generic type parameters (T in class Box[T])

PropertySymbol / EventSymbol / ParameterSymbol — standalone records (not Symbol subclasses)
```

**Position fields**: `Symbol.DeclarationLine/Column` is the statement start (used for diagnostics and identity comparisons); `Symbol.NameDeclarationLine/Column` is the name-token position (used for text edits and highlight ranges); `Symbol.EffectiveNameLine/Column` is the preferred accessor (`NameDeclarationLine ?? DeclarationLine`), and `Symbol.EffectiveNameColumnEnd` is the exclusive end column of the name token — **LSP handlers must use these for text edits, highlight ranges, and name extents. Never reconstruct a name extent from `Name.Length`; use `SymbolExtents.NameExtentLength(symbol)` or the AST node's `NameColumnEnd - NameColumnStart` (#1454). `NameExtentReconstructionScanTests` enforces this**.

### SemanticType Hierarchy

All types are immutable records inheriting from `SemanticType` (`Semantic/SemanticType.cs`):

```
SemanticType (abstract)
├── BuiltinType       — Int, Long, Float, Double, Float32, Bool, Str (singletons)
├── GenericType       — list[int], dict[str, int] (Name + TypeArguments)
├── UserDefinedType   — Classes, structs, interfaces (Name + Symbol)
├── NullableType      — T? for .NET interop (UnderlyingType)
├── OptionalType      — T? as safe tagged union (UnderlyingType)
├── FunctionType      — Lambdas/delegates (ParameterTypes + ReturnType)
├── GenericFunctionType — Generic functions with type parameters
├── ConstructorReferenceType — Bare builtin type reference as a value (`f = int`) — Sharpy's method group
├── TupleType         — tuple[int, str] (ElementTypes)
├── ModuleType        — Imported modules as namespaces
├── TypeParameterType — Generic type parameters (T in class Box[T])
├── ResultType        — T !E tagged union (OkType + ErrorType)
├── SelfType          — Self type for covariant return annotations
├── UnionType         — Tagged unions (v0.2.x placeholder)
├── TaskType          — Async Task types (v0.2.x placeholder)
├── TemplateType      — Template/format string types
├── LiteralStringType — Compile-time string literal types
├── VoidType          — None return type
└── UnknownType       — Error recovery
```

## ValidationPipeline

Pluggable validators implement `ISemanticValidator` with an `Order` property (lower runs first). **Responsibility split**: the TypeChecker handles type mismatches and in-progress inference; the ValidationPipeline handles self-contained AST analyses that don't need active inference state. Base classes: `ValidatingAstWalker` (visitor-pattern traversal — override `VisitXxx`) or `SemanticValidatorBase` (custom traversal — override `Validate()`).

- **Order 50**: `ModuleLevelValidator` — Entry point validation
- **Order 52**: `CircularImportUsageValidator` — Circular import usage detection
- **Order 55**: `NamingConventionValidator` — Naming convention checks
- **Order 56**: `TransitionWarningValidator` — Transition hints for Python/C# behavioral differences
- **Order 57**: `BuiltinNameShadowingValidator` — Value-position builtin shadowing (SPY0483)
- **Order 58**: `LocalNameCollisionValidator` — Locals colliding after mangling (SPY0522)
- **Order 60**: `DecoratorValidator` — Decorator validation
- **Order 62**: `BodylessSyntaxValidator` — Deprecation warnings for body-less method syntax
- **Order 65**: `SourceGeneratorValidator` — Source generator attribute validation
- **Order 140**: `ConstructorOverloadValidator` — Duplicate constructor signatures
- **Order 145**: `StructRulesValidator` — Struct constructor field initialization
- **Order 146**: `AbstractMemberValidator` — Abstract member in non-abstract class (SPY0493)
- **Order 147**: `EnumRulesValidator` — Enum value type consistency
- **Order 150**: `SignatureValidator` — Dunder method signatures
- **Order 152**: `ConversionOperatorValidator` — Conversion operator validation
- **Order 155**: `GeneratorValidator` — Generator function validation
- **Order 160**: `EqualityContractValidator` — Equality contract checks
- **Order 170**: `InterfaceConflictValidator` — Interface conflict detection
- **Order 250**: `DefaultParameterValidator` — Default parameter validation
- **Order 400**: `ControlFlowValidator` — CFG-based unreachable code, missing returns
- **Order 405**: `ExhaustivenessValidator` — Match statement exhaustiveness checks
- **Order 410**: `PropertyValidator` — Property validation
- **Order 411**: `FinalFieldValidator` — Final field validation
- **Order 412**: `EventValidator` — Event validation
- **Order 415**: `VarianceValidator` — Variance validation
- **Order 420**: `UnusedVariableValidator` — Unused variable warnings
- **Order 430**: `UnusedImportValidator` — Unused import warnings
- **Order 435**: `MustUseValidator` — Unused must-use carrier warnings (Result/Optional, `@must_use`)
- **Order 450**: `AccessValidator` — Private/protected member access
- **Order 460**: `DunderInvocationValidator` — Direct dunder call warnings
- **Order 480**: `InterfaceImplementationValidator` — Interface method implementation checks
- **Order 500**: `ProtocolValidator` — Protocol validation
- **Order 501**: `OperatorValidator` — Operator validation

## Code Generation

The `RoslynEmitter` is split into partial files by area — entry/name resolution (`RoslynEmitter.cs`), `.Expressions.*` (Access, Calls, Comprehensions, Literals, Operators), `.Statements.*` (Assignments, ControlFlow), `.TypeDeclarations`, `.ClassMembers.*` (Constructors, Dataclass, Events, Iterators, LruCache, Methods, Properties), `.CompilationUnit`, `.ModuleClass`, `.Operators`, `.Patterns`, `.TestFixtures`, plus `RoslynEmitterFactory.cs`.

**Name resolution strategy**:
- Module-level symbols → `Symbol.CodeGenInfo` (precomputed during semantic analysis)
- Local variables → `Symbol.CodeGenInfo` (precomputed by `LocalNameAllocator` at `ComputeForModule`; monotonic versioning: x, x_1, x_2; rebinding chains share the root's spelling)
- Types → SymbolTable lookup

**Type mappings** (`CodeGen/TypeSyntaxMapper.cs`): `int` → `int`, `long` → `long`, `str` → `string`, `float` → `double`, `list[T]` → `Sharpy.List<T>`, `dict[K,V]` → `Sharpy.Dict<K,V>`, `set[T]` → `Sharpy.Set<T>` (Sharpy.Core wrappers delegate to .NET types internally). Collection type name constants live in `Shared/CSharpTypeNames.cs`. A separate `Discovery/ClrTypeMapper.cs` maps CLR types back to Sharpy `SemanticType` instances.

**Name mangling** (`Shared/NameMangler.cs`): `snake_case` → `PascalCase`, `__init__` → constructor, `__add__` → `operator+`, `__str__` → `ToString()`.

## Multi-File & Incremental Compilation

`ProjectCompiler` (in `Project/`) and `ProjectFileParser` (the single `.spyproj` parser + discovery helper, in `ProjectConfig.cs`) handle multi-file projects:

```bash
dotnet run --project src/Sharpy.Cli -- project path/to/project.spyproj [--incremental]
```

**Incremental** (`IncrementalCompilationCache`, `SymbolSerializer`, `SymbolCache` in `Project/`): first build compiles everything and caches symbols + generated C# to `obj/{Config}/.sharpy-symbols`; later builds skip files whose content hash matches `obj/{Config}/.sharpy-cache` AND whose dependencies are unchanged (transitive, via the cached dependency graph). Caches invalidate on compiler-version change, schema-version change, or source change; force a full rebuild with `--clean` or by deleting the cache files.

## Sharpy.Core

- **Wrap .NET internally, expose Python API** — `list.append()` not `Add()`
- **Partial class pattern**: types split across `Partial.{Type}/` directories (`Partial.List/List.Methods.cs`, `List.Slicing.cs`, `List.Interfaces.cs`)
- **Builtins**: `partial class Builtins` split across `Print.cs`, `Len.cs`, `Range.cs`, etc.
- **Core modules**: Operator (comparison helpers) and Copy (shallow/deep copy) stay in Core due to collection type dependencies
- **Python semantics**: negative indexing, slicing, Python-matching exceptions
- Multi-targets `net10.0;netstandard2.1` — C# 9.0 on `netstandard2.1`, `LangVersion 14` on `net10.0` (`#if NET10_0_OR_GREATER` for net10.0-only paths)

## Sharpy.Stdlib

- Standard library modules (json, os, re, numpy, datetime, …) live one directory per module in `src/Sharpy.Stdlib/`; `modules/` holds per-module `.csproj` packaging.
- `spy/` holds `.spy` **source** modules: for spy-sourced modules (the `MODULES` mapping in `build_tools/regenerate_spy_stdlib.sh`) the C# under `<Module>/` is *generated* — never hand-edit; run the script (CI gate: `check_spy_staleness.sh`).
- Depends on Sharpy.Core (ProjectReference), not the reverse; same multi-targeting as Core. The compiler has **zero compile-time dependency** on Stdlib — modules are discovered at runtime via `ModuleRegistry.LoadReference()`.
- NuGet deps: MathNet.Numerics (numpy), Microsoft.Data.Sqlite (sqlite3), Tomlyn (toml), YamlDotNet (yaml).
- **Protocol interfaces** enable builtin dispatch at compile time: `ISized` (`int Count`, for `len()`), `IBoolConvertible` (`bool IsTrue`, for `bool()`), `IReverseEnumerable<T>` (`GetReverseEnumerator()`, for `reversed()`). The emitter implicitly adds them to a class's base list when the corresponding dunder (`__len__`/`__bool__`/`__reversed__`) is present, emitting the SPY1001 info diagnostic.

## Testing Patterns

### File-Based Tests (`src/Sharpy.Compiler.Tests/Integration/TestFixtures/`)
```
feature/test.spy + test.expected      # Success (exact stdout match)
errors/bad.spy + bad.error            # Failure (substring in error; line ending @line:col also checks location)
multifile/main.spy + lib.spy + main.expected  # Multi-file (dir with main.spy entry point)
feature/test.spy + test.expected.cs   # C# snapshot (Roslyn-normalized)
```
Auto-discovered. `.skip` skips, `.warning` for warning expectations, `.features` enables experimental features (one name per line; unknown names fail discovery loudly).

Regenerate C# snapshots: `UPDATE_SNAPSHOTS=true dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"`

### Programmatic Tests
```csharp
var result = CompileAndExecute("print(1 + 2)");
Assert.Equal("3\n", result.StandardOutput);
```
Multi-file: `ProjectCompilationHelper` (`WithRootNamespace(...).AddSourceFile(...).CreateProjectFile()`, then `Compile()`).

## Feature Implementation Order

```
Lexer → Parser → Semantic → Validation → CodeGen → LSP → Tests
```

**Experimental features** (default-off, behind a flag) follow [docs/design/feature-lifecycle.md](../docs/design/feature-lifecycle.md): register in `FeatureFlags.KnownFeatures`, gate through the `FeatureGateChecker` registry (ungated use → SPY0331), then graduate or delete per that policy.

**SPY0908 policy:** [docs/design/spy0908-policy.md](../docs/design/spy0908-policy.md) — SPY0908 is a net, not an error channel; every fix names its semantic check or lowering.

## Compiler Subdirectories

| Path | Purpose |
|------|---------|
| `Analysis/ControlFlow/` | `ControlFlowGraph`, `ControlFlowGraphBuilder`, `BasicBlock` |
| `Diagnostics/` | `DiagnosticBag`, `DiagnosticCodes`, `DiagnosticExplanations`, `DiagnosticRenderer`, `CompilationMetrics` |
| `Discovery/` | CLR type discovery: `ClrTypeMapper`, `CachedModuleDiscovery`; `Caching/` holds `OverloadIndex`, `OverloadIndexCache`, `AssemblyIdentity` |
| `Shared/` | `CSharpKeywords` (keyword escaping), `CSharpTypeNames` (collection type constants), `NameMangler` |
| `Model/` | `CompilationUnit`, `CompilationUnitFactory`, `ProjectModel` |
| `Logging/` | `ICompilerLogger`, `StructuredLogger`, `ConsoleCompilerLogger`, `NullLogger` |
| `Project/` | `ProjectCompiler`, `DependencyGraph`, incremental-compilation caches (`.spyproj` parsing lives in `ProjectConfig.cs`'s `ProjectFileParser`) |
| `Services/` | `CompilerServices`, `CompilerServicesBuilder` (adapter pattern) |
| `Text/` | `ILocatable`, `SourceText`, `TextSpan` |
| `Utilities/` | `EditDistance`, `PathNormalizer` |

## Project Layout

| Path | Purpose |
|------|---------|
| `src/Sharpy.Compiler/` | Compiler pipeline |
| `src/Sharpy.Core/` | Runtime essentials: primitives, collections, builtins, protocol interfaces |
| `src/Sharpy.Stdlib/` | Standard library modules |
| `src/Sharpy.Cli/` | CLI (`System.CommandLine`) |
| `src/Sharpy.Lsp/` | Language Server Protocol server (OmniSharp-based) |
| `src/*.Tests/` | Unit + integration tests |
| `docs/language_specification/` | **Authoritative** spec |
| `build_tools/` | Python-based build automation and dogfooding tools (own CLAUDE.md) |
| `.claude/agents/` | Claude Code agent definitions (registry: [agents.md](agents.md)) |
| `.github/instructions/` | Per-component contribution guides |

## CI/CD

`.github/workflows/`: `dotnet10.yml` (tests on .NET 10), `docs.yml` (mkdocs + playground), `python-build-tools.yml` (pytest for `build_tools/`), `benchmarks.yml`, `cross-language-benchmarks.yml`, `vscode-extension.yml`, `auto-tag.yml`, `release.yml`. An `.editorconfig` at the repo root enforces C# formatting and naming conventions.
