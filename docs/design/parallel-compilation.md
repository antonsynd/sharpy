# Parallel Compilation via Per-File Symbol Namespaces

**Issue:** [#610](https://github.com/antonsynd/sharpy/issues/610)
**Status:** Design
**Author:** antonsynd
**Date:** 2026-04-28

## 1. Overview

The Sharpy compiler currently processes files sequentially during multi-file
project compilation. `ProjectCompiler` creates a single shared `SymbolTable`
and `SemanticInfo`, then walks files in dependency order one at a time. This
approach does not scale for large projects because CPU-bound phases (name
resolution, type checking, validation, code generation) cannot utilize multiple
cores.

This document proposes a per-file symbol namespace architecture that enables
parallel compilation while preserving correctness. The key insight is that most
compilation phases operate on a single file at a time, reading cross-file
information only from a shared, read-only global symbol table.

## 2. Current Architecture

The current pipeline processes every file through these phases sequentially:

```
For each file (in dependency order):
  1. NameResolver.ResolveDeclarations()   -- populate shared SymbolTable
  2. NameResolver.ResolveInheritance()    -- resolve base classes
  3. ImportResolver                       -- resolve imports via ModuleLoader
  4. TypeResolver.ResolveTypes()          -- resolve type annotations
  5. TypeChecker.CheckModule()            -- type checking + inference
  6. ValidationPipeline.Validate()        -- pluggable validators
  7. RoslynEmitter                        -- code generation
```

**Shared mutable state:**

| Component | Thread Safety | Notes |
|-----------|--------------|-------|
| `SymbolTable` | Not thread-safe | `Dictionary<string, Scope>` for `_moduleScopes`, `Stack<Scope>` for `_scopeStack` |
| `SemanticInfo` | Partially thread-safe | Most fields use `ConcurrentDictionary`, but `_symbolReferences` is a plain `Dictionary` |
| `DiagnosticBag` | Thread-safe | Uses `lock(_lock)` on all public methods |
| `SemanticBinding` | Not thread-safe | Shared store for computed semantic data |
| `SymbolSerializer` | Stateless | Safe for concurrent use |
| `IncrementalCompilationCache` | Per-file storage | Already stores symbols per-file -- aligns with per-file approach |

## 3. Proposed Architecture: Per-File + Merge

The design splits compilation into five phases with explicit synchronization
barriers between parallel and sequential sections.

### Phase 1: Parallel Name Resolution

Each file gets its own `SymbolTable` instance. `NameResolver.ResolveDeclarations()`
runs in parallel per file, collecting module-level declarations (classes,
functions, variables, type aliases).

- No cross-file dependencies at this stage -- each file's declarations are
  self-contained.
- Per-file `DiagnosticBag` instances avoid lock contention.
- Per-file scope stacks (`_scopeStack`) are inherent to single-file AST
  traversal.

```csharp
// Pseudocode
var perFileTables = await Parallel.ForEachAsync(files, async (file, ct) =>
{
    var localTable = new SymbolTable(builtinRegistry);
    var localDiagnostics = new DiagnosticBag();
    var resolver = new NameResolver(localTable, localDiagnostics);
    resolver.ResolveDeclarations(file.Ast);
    return (file, localTable, localDiagnostics);
});
```

### Phase 2: Merge into GlobalSymbolTable

A single synchronization barrier merges per-file symbol tables into a shared
read-only `GlobalSymbolTable`.

**Critical invariant:** Symbols use reference equality (overridden from record
default) because their properties (`Type`, `BaseType`, `CodeGenInfo`) are set
progressively across passes. The merge must canonicalize references, not copy --
all downstream phases must refer to the same `Symbol` instances.

Merge responsibilities:
- Collect all module-level symbols from per-file tables into a unified global
  scope.
- Detect and report conflicts (duplicate module-level names across files).
- Freeze the global table as read-only for subsequent parallel phases.

```csharp
// Pseudocode
var globalTable = GlobalSymbolTable.MergeFrom(perFileTables);
// globalTable is immutable after this point
```

### Phase 3: Import Resolution

Import resolution requires cross-file information and must execute after the
global symbol table is available.

- `ImportResolver` resolves imports using `GlobalSymbolTable` for cross-file
  symbol lookups.
- `ModuleLoader` caches parsed modules and detects circular imports -- its
  internal cache must be thread-safe if parallelized.
- **Initial approach:** Run sequentially. Import resolution is typically fast
  relative to type checking and does not benefit as much from parallelism.
- **Future optimization:** Parallelize per-file with a concurrent module cache
  and read-only global table access.

### Phase 4: Sequential Inheritance Resolution

`NameResolver.ResolveInheritance()` must run after all imports are resolved
because a class may inherit from a type defined in another file.

- Runs sequentially against the merged `GlobalSymbolTable`.
- Could be parallelized per-file in the future if inheritance chains are fully
  resolved in the global table, but the sequential approach is simpler and
  inheritance resolution is not a bottleneck.

### Phase 5: Parallel Type Checking, Validation, and Code Generation

With the global symbol table frozen and all imports/inheritance resolved, the
remaining phases can run in parallel per file:

- **Per-file `SemanticInfo`** -- Each file gets its own instance. This aligns
  with the existing recommendation in `ProjectCompiler.cs` (lines 24-27) and
  avoids contention on the `ConcurrentDictionary` internals.
- **Per-file `SemanticBinding`** -- Stores computed semantic data separately,
  materialized at phase boundaries.
- **Per-file `DiagnosticBag`** -- Avoids lock contention. Merged after
  completion for deterministic output.
- **Shared read-only `GlobalSymbolTable`** -- Cross-file type lookups.
- `TypeResolver`, `TypeChecker`, `ValidationPipeline`, and `RoslynEmitter` all
  run in parallel per file.

```csharp
// Pseudocode
await Parallel.ForEachAsync(files, async (file, ct) =>
{
    var localSemanticInfo = new SemanticInfo();
    var localBinding = new SemanticBinding();
    var localDiagnostics = new DiagnosticBag();

    var typeResolver = new TypeResolver(globalTable, localSemanticInfo, localDiagnostics);
    typeResolver.ResolveTypes(file.Ast);

    var typeChecker = new TypeChecker(globalTable, localSemanticInfo, localBinding, localDiagnostics);
    typeChecker.CheckModule(file.Ast);

    var pipeline = new ValidationPipeline(globalTable, localSemanticInfo, localDiagnostics);
    pipeline.Validate(file.Ast);

    var emitter = new RoslynEmitter(globalTable, localSemanticInfo, localDiagnostics);
    var csharp = emitter.Emit(file.Ast);

    return (file, csharp, localDiagnostics);
});
```

## 4. Key Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Scope stack | Per-file instances | Inherent to tree traversal -- each file's AST walk needs its own stack |
| Symbol identity | Merge must canonicalize, not copy | Symbols use reference equality (overridden from record default); properties are set progressively across passes |
| Global table mutability | Read-only after merge | Eliminates need for synchronization during parallel phases |
| Incremental cache | Per-file SymbolTable aligns with `SymbolSerializer` | `IncrementalCompilationCache` already stores symbols per-file |
| Error accumulation | Per-file `DiagnosticBag`, merged after completion | Avoids lock contention; `DiagnosticBag` is already thread-safe as fallback |
| Import resolution | Sequential initially | Cross-file dependency graph makes parallelism complex; not a bottleneck |
| SemanticInfo | Per-file instances | Existing code comment recommends this; avoids `ConcurrentDictionary` overhead |

## 5. Threading Model

```
┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│   File A     │  │   File B     │  │   File C     │
│ NameResolver │  │ NameResolver │  │ NameResolver │
│  (parallel)  │  │  (parallel)  │  │  (parallel)  │
└──────┬───────┘  └──────┬───────┘  └──────┬───────┘
       │                 │                 │
       ▼                 ▼                 ▼
  ══════════════════ BARRIER: Merge ══════════════════
                         │
                         ▼
               ┌──────────────────┐
               │ GlobalSymbolTable │
               │   (read-only)    │
               └────────┬─────────┘
                        │
                        ▼
               ┌─────────────────┐
               │ Import + Inherit │
               │  (sequential)    │
               └────────┬─────────┘
                        │
        ┌───────────────┼───────────────┐
        ▼               ▼               ▼
┌──────────────┐ ┌──────────────┐ ┌──────────────┐
│   File A     │ │   File B     │ │   File C     │
│ TypeChecker  │ │ TypeChecker  │ │ TypeChecker  │
│ Validation   │ │ Validation   │ │ Validation   │
│ CodeGen      │ │ CodeGen      │ │ CodeGen      │
│  (parallel)  │ │  (parallel)  │ │  (parallel)  │
└──────────────┘ └──────────────┘ └──────────────┘
        │               │               │
        ▼               ▼               ▼
  ══════════════════ BARRIER: Merge ══════════════════
                         │
                         ▼
               ┌──────────────────┐
               │ Merged diagnostics│
               │ + generated C#   │
               └──────────────────┘
```

**Concurrency primitives:**
- `Parallel.ForEachAsync` with configurable `MaxDegreeOfParallelism`
  (default: `Environment.ProcessorCount`).
- Merge barriers are implicit -- `await` the parallel phase before starting the
  next sequential phase.
- No locks required during parallel phases because all shared state is
  read-only.

## 6. LSP Impact

The Language Server Protocol server (`Sharpy.Lsp`) currently serializes project
reanalysis via `_analysisLock` (a `SemaphoreSlim(1, 1)` in `LanguageService`).

With per-file analysis:
- **Single-file edits** can be re-analyzed without acquiring the full project
  lock. Only the edited file needs re-type-checking against the existing
  `GlobalSymbolTable`.
- **File-level change detection** can skip unchanged files, aligning with the
  incremental compilation cache.
- **Diagnostic updates** can be pushed per-file as each file completes analysis,
  rather than waiting for the entire project.

The `_analysisLock` would shift from protecting the entire analysis pipeline to
protecting only the `GlobalSymbolTable` rebuild (merge phase). Individual file
analyses would not need the lock.

## 7. Migration Path

Each phase below is independently valuable and shippable. They can be released
incrementally without requiring a flag day.

### Phase 0: Refactor SemanticInfo to be per-file (low risk, high value)
- Create per-file `SemanticInfo` instances in `ProjectCompiler`.
- Pass the per-file instance to `TypeResolver`, `TypeChecker`, and
  `ValidationPipeline`.
- Merge results after analysis (or keep per-file for LSP use).
- **Value:** Enables per-file LSP reanalysis immediately, even without
  parallelism.

### Phase 1: Extract GlobalSymbolTable interface from SymbolTable
- Define a read-only `IGlobalSymbolTable` interface with lookup methods.
- `SymbolTable` implements `IGlobalSymbolTable`.
- Downstream consumers (`TypeResolver`, `TypeChecker`, etc.) depend on
  `IGlobalSymbolTable` for cross-file lookups.
- **Value:** Clear separation of read-only cross-file state from mutable
  per-file state.

### Phase 2: Implement per-file name resolution with merge
- Each file gets its own `SymbolTable` during `NameResolver.ResolveDeclarations()`.
- Implement `GlobalSymbolTable.MergeFrom()` to combine per-file results.
- Run name resolution in parallel.
- **Value:** First parallel phase; validates the per-file + merge approach.

### Phase 3: Parallelize type checking
- Per-file `SemanticInfo`, `SemanticBinding`, and `DiagnosticBag`.
- `TypeResolver` and `TypeChecker` run in parallel per file, reading from the
  frozen `GlobalSymbolTable`.
- `ValidationPipeline` and `RoslynEmitter` also run in parallel.
- **Value:** Full parallel compilation; largest performance win for large
  projects.

## 8. Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Symbol identity breakage | Type checking produces wrong results if symbols are copied instead of canonicalized during merge | Extensive test suite (10,218 tests) catches reference equality issues; add specific merge identity tests |
| Import resolution ordering | Circular imports or missing symbols if parallelized incorrectly | Maintain sequential import resolution as initial approach; parallelize only after correctness is proven |
| DiagnosticBag merge ordering | Non-deterministic diagnostic output confuses users and breaks snapshot tests | Sort diagnostics by file path + line number after merge; snapshot tests already normalize by file |
| Progressive symbol mutation | Symbols are mutated across passes (Type, BaseType, CodeGenInfo set at different times) | Merge happens after name resolution but before type checking; all mutations in type checking use per-file state with shared read-only global lookups |
| ModuleLoader thread safety | `ModuleLoader` caches parsed modules in a non-concurrent dictionary | Either keep import resolution sequential or add `ConcurrentDictionary` to `ModuleLoader` cache |
| Incremental cache interaction | Per-file SymbolTable must be compatible with `SymbolSerializer` | `IncrementalCompilationCache` already stores symbols per-file; per-file tables align naturally |
