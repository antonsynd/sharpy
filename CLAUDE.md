# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

> **See also:** [.github/copilot-instructions.md](.github/copilot-instructions.md) for full architecture and patterns, [.github/agents.md](.github/agents.md) for the agent registry.

## Repository

- **GitHub owner:** `antonsynd`
- **GitHub repo:** `antonsynd/sharpy`

## Quick Reference

> **Prerequisites:** .NET 10 SDK (`net10.0` TFM). Python 3.9+ for `build_tools/`.

```bash
dotnet build sharpy.sln                              # Build all
dotnet test                                          # Run all tests
dotnet format whitespace                             # Format code (auto-formatted on save by Claude hook)
dotnet run --project src/Sharpy.Cli -- run file.spy # Compile and execute
dotnet run --project src/Sharpy.Cli -- emit csharp file.spy  # Inspect generated C#
dotnet run --project src/Sharpy.Cli -- emit ast file.spy     # Inspect parsed AST
dotnet run --project src/Sharpy.Cli -- emit tokens file.spy  # Inspect lexer tokens
```

> **Sandbox note:** All `dotnet` commands (especially `build` and `test`) hang when run inside the default sandbox. When operating in a sandboxed environment, run `dotnet` commands with `dangerouslyDisableSandbox: true`.

> **Prefer skills over raw commands:** Use `/build`, `/run-tests`, `/spy-emit`, `/spy-run`, `/quick-check` instead of raw `dotnet` commands. Skills handle logging, truncation, and temp file management (avoiding bash escaping issues with `#` and backticks in Sharpy source).

## Architecture

```
Source (.spy) → Lexer → Parser (AST) → Semantic → ValidationPipeline → RoslynEmitter → C# → .NET IL
```

| Component | Location | Purpose |
|-----------|----------|---------|
| Compiler | `src/Sharpy.Compiler/` | Lexer, Parser, Semantic, CodeGen |
| Stdlib | `src/Sharpy.Core/` | Runtime library (partial class pattern in `Partial.{Type}/`) |
| CLI | `src/Sharpy.Cli/` | Command-line interface (`sharpyc`, uses `System.CommandLine`) |
| LSP | `src/Sharpy.Lsp/` | Language Server Protocol server (OmniSharp-based) |
| Tests | `src/*.Tests/` | Unit and integration tests |
| Specs | `docs/language_specification/` | Authoritative language specification |
| Build Tools | `build_tools/` | Python-based build automation and dogfooding tools |
| Agents | `.github/agents/` | Domain-specific agent guidance (copilot/AI) |
| Instructions | `.github/instructions/` | Per-component contribution guides |

## Critical Rules

1. **Never modify expected values to make tests pass** — fix the implementation
2. **RoslynEmitter uses SyntaxFactory exclusively** — no string templating
3. **Immutable AST** — annotations go in `SemanticInfo`, not AST nodes
4. **Axiom precedence**: .NET > Type Safety > Python Syntax
5. **C# 9.0 target for Sharpy.Core only** — `Sharpy.Core` targets `netstandard2.1;netstandard2.0` with `LangVersion 9.0` (no global usings, file-scoped namespaces, or record structs). `Sharpy.Compiler` and `Sharpy.Cli` target `net10.0` with `LangVersion latest`.
6. **Always verify Python behavior first** — run `python3 -c "..."` before implementing Python semantics
7. **Language spec is authoritative** — check `docs/language_specification/` before implementing; change implementation to match spec, not the other way around
8. **TODO/BUG/FIXME comments must have GitHub issues** — when leaving a `TODO`, `BUG`, or `FIXME` comment in code, always create a corresponding GitHub issue first (via `gh issue create`) and reference it in the comment (e.g., `// TODO(#123): ...`). This makes deferred work visible at the project level, not buried in code.
9. **Warnings are errors** — `TreatWarningsAsErrors` is enabled solution-wide via `Directory.Build.props`

## Semantic Analysis Pipeline

The semantic phase runs multiple ordered passes. Understanding this is critical for implementation work.

**Pass 1 — Name Resolution** (`NameResolver.cs`): Collects all top-level declarations into `SymbolTable`. Runs `ResolveDeclarations()` then `ResolveInheritance()`.

**Pass 1.5 — Import Resolution** (`ImportResolver.cs`): Loads imported modules via `ModuleLoader` (which caches parsed modules and detects circular imports). Registers imported symbols in SymbolTable. `PackageResolver` handles `__init__.spy` packages.

**Pass 2 — Type Resolution** (`TypeResolver.cs`): Resolves type annotations on declarations to concrete types. Type inference provided by `TypeInferenceService` and `GenericTypeInferenceService`.

**Pass 3 — Type Checking** (`TypeChecker.cs`, split into 10 partial files: `.cs`, `.Definitions.cs`, `.Expressions.cs`, `.Expressions.Access.cs`, `.Expressions.Access.Calls.cs`, `.Expressions.Access.Lambdas.cs`, `.Expressions.Literals.cs`, `.Expressions.Operators.cs`, `.Statements.cs`, `.Utilities.cs`): Traverses AST, infers types, records them in `SemanticInfo`. Then runs `ValidationPipeline`. Type narrowing (e.g., `if x is not None:` narrows `T?` → `T`) is tracked via `_narrowingContext` (`TypeNarrowingContext`).

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
Symbol (abstract)              — DeclarationSpan, DeclaringFilePath (all symbols)
├── VariableSymbol        — Type set during type checking
├── FunctionSymbol        — Parameters, ReturnType, IsStatic/Abstract/Virtual/Override
├── TypeSymbol            — TypeKind, BaseType, Interfaces, Fields, Methods, DefiningFilePath
├── ModuleSymbol          — FilePath
├── TypeAliasSymbol       — Aliased type reference
└── TypeParameterSymbol   — Generic type parameters (T in class Box[T])

PropertySymbol   — Standalone record (not a Symbol subclass)
EventSymbol      — Standalone record (not a Symbol subclass)
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
├── SelfType          — Self type for covariant return annotations
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
- **Order 62**: `BodylessSyntaxValidator` — Deprecation warnings for body-less method syntax
- **Order 140**: `ConstructorOverloadValidator` — Duplicate constructor signatures
- **Order 145**: `StructRulesValidator` — Struct constructor field initialization
- **Order 147**: `EnumRulesValidator` — Enum value type consistency
- **Order 150**: `SignatureValidator` — Dunder method signatures
- **Order 155**: `GeneratorValidator` — Generator function validation
- **Order 160**: `EqualityContractValidator` — Equality contract checks
- **Order 170**: `InterfaceConflictValidator` — Interface conflict detection
- **Order 250**: `DefaultParameterValidator` — Default parameter validation
- **Order 400**: `ControlFlowValidator` — CFG-based unreachable code, missing returns
- **Order 405**: `ExhaustivenessValidator` — Match statement exhaustiveness checks
- **Order 410**: `PropertyValidator` — Property validation
- **Order 412**: `EventValidator` — Event validation
- **Order 415**: `VarianceValidator` — Variance validation
- **Order 420**: `UnusedVariableValidator` — Unused variable warnings
- **Order 430**: `UnusedImportValidator` — Unused import warnings
- **Order 450**: `AccessValidator` — Private/protected member access
- **Order 460**: `DunderInvocationValidator` — Direct dunder call warnings
- **Order 480**: `InterfaceImplementationValidator` — Interface method implementation checks
- **Order 500**: `ProtocolValidator` — Protocol validation
- **Order 501**: `OperatorValidator` — Operator validation

**Validator base classes**:
- `ValidatingAstWalker` — for validators that traverse the AST via visitor pattern (e.g., ProtocolValidator, AccessValidator). Override `VisitXxx` methods to inspect nodes.
- `SemanticValidatorBase` — for validators with custom traversal logic (e.g., SignatureValidator, ModuleLevelValidator). Override `Validate()` and walk the AST manually.

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
| SPY0900–SPY0999 | Error | Infrastructure (compilation, file I/O) |
| SPY1000–SPY1099 | Info | Informational (e.g., implicit interface synthesis) |

## Code Generation

The `RoslynEmitter` is split into 18 partial classes (~15,108 lines total): `RoslynEmitter.cs` (entry, name resolution), `.Expressions.cs`, `.Expressions.Access.cs`, `.Expressions.Comprehensions.cs`, `.Expressions.Literals.cs`, `.Expressions.Operators.cs`, `.Statements.cs`, `.TypeDeclarations.cs`, `.ClassMembers.cs`, `.ClassMembers.Constructors.cs`, `.ClassMembers.Dataclass.cs`, `.ClassMembers.Iterators.cs`, `.ClassMembers.Methods.cs`, `.ClassMembers.Properties.cs`, `.CompilationUnit.cs`, `.ModuleClass.cs`, `.Operators.cs`, `.Patterns.cs`.

**Name resolution strategy**:
- Module-level symbols → `Symbol.CodeGenInfo` (precomputed during semantic analysis)
- Local variables → runtime tracking via `_variableVersions` (handles redeclarations: x, x_1, x_2)
- Types → SymbolTable lookup

**Type mappings** (`CodeGen/TypeSyntaxMapper.cs`): `int` → `int`, `long` → `long`, `str` → `string`, `float` → `double`, `list[T]` → `Sharpy.List<T>`, `dict[K,V]` → `Sharpy.Dict<K,V>`, `set[T]` → `Sharpy.Set<T>` (Sharpy.Core wrappers delegate to .NET types internally). Collection type name constants live in `Shared/CSharpTypeNames.cs`. Note: a separate `Discovery/ClrTypeMapper.cs` maps CLR types back to Sharpy `SemanticType` instances.

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
Lexer → Parser → Semantic → Validation → CodeGen → LSP → Tests
```

1. **Lexer** (`Lexer/`) — Add `TokenType` and recognition
2. **Parser** (`Parser/Ast/`) — Add AST record, parsing rule. Parser is split into 6 partial files: `.cs`, `.Definitions.cs`, `.Expressions.cs`, `.Primaries.cs`, `.Statements.cs`, `.Types.cs`
3. **Semantic** (`Semantic/`) — Add type checking in `TypeChecker*.cs`
4. **Validation** (`Semantic/Validation/`) — Add validator if needed
5. **CodeGen** (`CodeGen/RoslynEmitter*.cs`) — Emit via `SyntaxFactory`
6. **LSP** (`src/Sharpy.Lsp/Handlers/`) — Update handlers if new AST nodes or semantic types affect IDE features (hover, completion, semantic tokens, etc.)
7. **Tests** — Unit tests per component + `.spy`/`.expected` integration tests + LSP handler tests

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
- **29 stdlib modules**: Argparse, Bisect, Builtins, Collections, Copy, Csv, Datetime, Fnmatch, Functools, Glob, Hashlib, Heapq, Io, Itertools, Json, Logging, Math, Operator, Os, Pathlib, Random, Re, Shutil, Statistics, String, Sys, Tempfile, Textwrap, Time
- **Python semantics**: Negative indexing, slicing, Python-matching exceptions

### Protocol Interfaces

Protocol interfaces enable builtin function dispatch (e.g., `len()`, `bool()`) via compile-time interfaces:

- `ISized` — `int Count { get; }` — implemented by List, Set, Dict; synthesized when `__len__` is present
- `IBoolConvertible` — `bool IsTrue { get; }` — synthesized when `__bool__` is present; enables `bool()` dispatch
- `IReverseEnumerable<T>` — `IEnumerator<T> GetReverseEnumerator()` — synthesized when `__reversed__` is present

The emitter implicitly adds these interfaces to a class's base list when the corresponding dunder method is detected (emits SPY1001 info diagnostic).

## Skills

Available in `.claude/skills/`:

### Build & Test
All commands below log full output to `.claude/tmp/*.log` for investigation while showing truncated summaries. Test skills auto-build before running.

| Command | Purpose |
|---------|---------|
| `/run-tests [filter]` | Build + run tests; shows last 80 lines on failure (log: `last-test-run.log`) |
| `/build` | Build solution; shows last 100 lines on failure (log: `last-build.log`) |
| `/format` | Format whitespace (auto-formatted on save by Claude hook) |
| `/regenerate-snapshots` | Build + update `.expected.cs` files after codegen changes |

### Debug & Development

All `/spy-*` skills accept **inline source** or a file path. Inline source is written to a temp file via the Write tool (no bash escaping needed), so agents should prefer these skills over raw `dotnet run` commands.

| Command | Purpose |
|---------|---------|
| `/spy-emit <mode> <source>` | Emit compiler output: `csharp`, `ast`, `tokens`, or `diagnostics` (log: `last-spy-emit.log`) |
| `/spy-run <source>` | Compile and execute (log: `last-spy-run.log`) |
| `/quick-check <source>` | Emit C# + run in one shot (logs: `last-quick-check-{emit,run}.log`) |
| `/lsp-hover <pos>` | Get LSP hover tooltip for a position in a .spy file (emulates VS Code hover) |
| `/lsp-review` | Interactive LSP review session — report hover/coloring issues from VS Code |
| `/verify-python <expr>` | Run Python 3 to verify behavior before implementing |
| `/clean-dotnet` | Kill zombie dotnet processes that cause hangs |

### Git Workflow

| Command | Purpose |
|---------|---------|
| `/commit [message]` | Stage and commit changes with auto-generated message |
| `/push [--close-issues N,N]` | Push current branch; optionally close GitHub issues |
| `/close-issues [N,N]` | Close GitHub issues that have been implemented, with verification |

### Analysis & Planning

| Command | Purpose |
|---------|---------|
| `/create-plan <issues or desc>` | Create implementation plan from GitHub issues or description |
| `/compiler-audit [focus]` | Run a comprehensive compiler health audit |
| `/dogfood-analyze [dir]` | Analyze dogfood results and classify failures |
| `/dogfood-run` | Run dogfooding iterations to test the compiler |
| `/verify-plan <plan.md>` | Verify a plan for accuracy and architectural soundness |
| `/implement-plan <plan.md>` | Implement a plan with a coordinated agent team |
| `/verify-implementation <plan.md>` | Verify implementation, fix gaps/bugs/regressions |
| `/add-test-fixture <desc>` | Create a file-based integration test (`.spy` + `.expected`/`.error`) |
| `/gap-analysis` | Run all gap discovery tests and present a unified summary |

**Investigate failures:** Read logs with `/read .claude/tmp/last-test-run.log` (or other log files).

## Testing

```bash
dotnet test --filter "FullyQualifiedName~Lexer"            # By component
dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"  # File-based tests
dotnet test --filter "DisplayName~test_name"               # By test name
dotnet test --filter "FullyQualifiedName~Lsp"              # LSP tests
dotnet test --filter "FullyQualifiedName~Lsp.Tests.E2E"    # LSP E2E protocol tests
```

### File-Based Tests

Location: `src/Sharpy.Compiler.Tests/Integration/TestFixtures/`

**Single-file tests**: `.spy` + `.expected` (exact stdout match) or `.spy` + `.error` (substring match in error; line ending with `@line:col` also verifies diagnostic location)

**Multi-file tests**: A subdirectory with multiple `.spy` files and a `main.spy` entry point, plus `main.expected` or `main.error`.

**Warning tests**: `.warning` file — empty means expect no warnings, non-empty lines are expected substrings. Can combine with `.expected`.

**C# snapshot tests**: `.expected.cs` file — the expected generated C# output (Roslyn-normalized). Used selectively for ~174 representative fixtures to detect codegen changes that don't affect runtime output. To regenerate: `UPDATE_SNAPSHOTS=true dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"`.

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
| `Discovery/` | CLR type discovery: `ClrTypeMapper`, `CachedModuleDiscovery` |
| `Shared/` | `CSharpKeywords` (keyword escaping), `CSharpTypeNames` (collection type constants), `NameMangler` |
| `Discovery/Caching/` | `OverloadIndex`, `OverloadIndexCache`, `AssemblyIdentity` |
| `Model/` | `CompilationUnit`, `CompilationUnitFactory`, `ProjectModel` |
| `Logging/` | `ICompilerLogger`, `StructuredLogger`, `ConsoleCompilerLogger`, `NullLogger` |
| `Project/` | `ProjectCompiler` (8 partial files), `SpyProject`, `DependencyGraph` |
| `Services/` | `CompilerServices`, `CompilerServicesBuilder` (adapter pattern) |
| `Text/` | `ILocatable`, `SourceText`, `TextSpan` |
| `Utilities/` | `EditDistance`, `PathNormalizer` |

## CI/CD

`.github/workflows/`:
- `dotnet10.yml` — Active; tests on .NET 10
- `playground.yml` — Blazor WASM "Try Sharpy Online" playground
- `python-build-tools.yml` — Runs pytest for `build_tools/` on Python 3.12
- `benchmarks.yml` — Performance benchmarks
- `vscode-extension.yml` — VS Code extension CI

An `.editorconfig` at the repo root enforces C# formatting and naming conventions.

## MCP Servers for Codebase Navigation

Two MCP servers provide deeper codebase understanding than grep/glob. Use them instead of raw search tools for structural queries.

### Serena (Symbol-Level Navigation & Editing)

Serena provides LSP-powered, symbol-aware operations. **Prefer Serena over Grep/Glob/Read for:**

| Task | Instead of... | Use Serena... |
|------|---------------|---------------|
| Find a method body | `Grep` for method name + `Read` the file | `find_symbol` with `include_body=True` |
| Find all callers of a function | `Grep` for function name (noisy, misses renames) | `find_referencing_symbols` (precise, cross-file) |
| Understand a class's shape | `Read` entire file | `get_symbols_overview` or `find_symbol` with `depth=1` |
| Replace a method body | `Edit` with `old_string`/`new_string` | `replace_symbol_body` (knows exact boundaries) |
| Add code before/after a symbol | `Edit` with surrounding context | `insert_before_symbol` / `insert_after_symbol` |
| Rename a symbol across codebase | Multiple `Edit` calls + `Grep` | `rename_symbol` (handles all references) |

**When to still use Grep/Glob:** Pattern searches across strings/comments, searching for non-symbol text (TODO comments, magic values), file discovery by path pattern.

### CodeGraphContext (Architecture & Relationship Queries)

CodeGraphContext pre-indexes the codebase into a graph database. **Prefer CodeGraphContext for:**

| Task | Instead of... | Use CodeGraphContext... |
|------|---------------|------------------------|
| Impact analysis before refactoring | Manual Grep for callers across files | `analyze_code_relationships` with `find_callers` |
| Find dead code | Guess-and-grep | `find_dead_code` |
| Complexity triage | Read files manually | `find_most_complex_functions` |
| "How does system X work?" | Multi-file Read + Grep | `analyze_code_relationships` |
| Cross-language navigation | Separate Grep passes per file type | `find_code` (semantic search) |
| Call chain analysis | Recursive Grep + Read | `execute_cypher_query` for graph traversal |

**First-time setup:** Run `add_code_to_graph` on `src/Sharpy.Compiler/` and `src/Sharpy.Core/` to index the codebase. Use `watch_directory` for ongoing updates during active development.

**Destructive tools** (`delete_repository`, `unwatch_directory`) require manual approval.

### Decision Guide: Which Tool When?

```
Need to find a file by name/pattern?      → Glob
Need to search text/regex across files?   → Grep
Need symbol definition, callers, shape?   → Serena (find_symbol, find_referencing_symbols)
Need to edit a whole method/function?     → Serena (replace_symbol_body)
Need call chains, impact, dead code?      → CodeGraphContext
Need architecture-level understanding?    → CodeGraphContext
Need to edit a few lines within a method? → Edit (or Serena replace_content)
```

<!-- code-review-graph MCP tools -->
## MCP Tools: code-review-graph

**IMPORTANT: This project has a knowledge graph. ALWAYS use the
code-review-graph MCP tools BEFORE using Grep/Glob/Read to explore
the codebase.** The graph is faster, cheaper (fewer tokens), and gives
you structural context (callers, dependents, test coverage) that file
scanning cannot.

> **Note:** The code-review-graph MCP server is configured in `.mcp.json`. If it is not connected in the current session, fall back to Serena and CodeGraphContext for structural queries, or Grep/Glob for text search.

### When to use graph tools FIRST

- **Exploring code**: `semantic_search_nodes` or `query_graph` instead of Grep
- **Understanding impact**: `get_impact_radius` instead of manually tracing imports
- **Code review**: `detect_changes` + `get_review_context` instead of reading entire files
- **Finding relationships**: `query_graph` with callers_of/callees_of/imports_of/tests_for
- **Architecture questions**: `get_architecture_overview` + `list_communities`

Fall back to Grep/Glob/Read **only** when the graph doesn't cover what you need.

### Key Tools

| Tool | Use when |
|------|----------|
| `detect_changes` | Reviewing code changes — gives risk-scored analysis |
| `get_review_context` | Need source snippets for review — token-efficient |
| `get_impact_radius` | Understanding blast radius of a change |
| `get_affected_flows` | Finding which execution paths are impacted |
| `query_graph` | Tracing callers, callees, imports, tests, dependencies |
| `semantic_search_nodes` | Finding functions/classes by name or keyword |
| `get_architecture_overview` | Understanding high-level codebase structure |
| `refactor_tool` | Planning renames, finding dead code |

### Workflow

1. The graph auto-updates on file changes (via hooks).
2. Use `detect_changes` for code review.
3. Use `get_affected_flows` to understand impact.
4. Use `query_graph` pattern="tests_for" to check coverage.
