# Sharpy LSP Maturity Plan

> Staff engineer assessment — phased plan to evolve the Sharpy LSP from functional to mature.
> Benchmarked against rust-analyzer, gopls, Pyright, and clangd.

## Current State

The LSP is surprisingly far along for a young project. 17 handlers, ~6,400 lines of C# implementation, VS Code extension, E2E test infrastructure.

| Feature | Status |
|---|---|
| Semantic Tokens | **Done** (13 token types, 5 modifiers) |
| Inlay Hints | **Done** (inferred types + parameter names) |
| Code Lens | **Done** (reference counts + "Run" lens) |
| Hover | **Done** (type info + formatted markdown) |
| Go-to-Definition | **Done** (cross-file via project loading) |
| Find References | **Done** (workspace-wide) |
| Rename | **Done** (workspace-wide, scope-aware) |
| Signature Help | **Done** |
| Code Actions | **Basic** (remove unused imports/vars, fix naming) |
| Document Symbols | **Done** |
| Workspace Symbols | **Done** (open docs only) |
| Formatting | **Done** (full document) |
| Folding Ranges | **Done** |
| Document Highlights | **Done** |

### Critical Gap

The **analysis model** is the architectural bottleneck. Everything runs single-file through `CompilerApi.Analyze()`. The compiler has a mature `ProjectCompiler` with `DependencyGraph`, `IncrementalCompilationCache`, and `SymbolCache`, but the LSP doesn't use any of it. This gates every advanced feature.

---

## Phase 0: Foundation — Project-Aware Analysis Engine

**Scope: Large | Priority: Critical | Prerequisite for everything**

This is the single most important piece of work. Without it, every "advanced" feature is built on sand.

### 0.1 — Incremental Document Sync

Currently using `TextDocumentSyncKind.Full` — every keystroke sends the entire file. Switch to `TextDocumentSyncKind.Incremental` with text change deltas applied to a persistent `SourceText` buffer. The compiler's `SourceText` already supports offset-based operations; add an `ApplyChange(range, newText)` method.

**Why first:** Reduces I/O pressure on every keystroke. The 300ms debounce masks it now, but at scale it's untenable.

### 0.2 — Project-Wide Semantic Model

Build a `LanguageService` (or `ProjectAnalysisEngine`) that sits between `SharplyWorkspace` and the handlers:

```
SharplyWorkspace (document state)
    -> LanguageService (project-aware analysis)
        -> CompilerApi (single-file parse/analyze)
        -> ProjectCompiler (multi-file semantic model)
        -> IncrementalCompilationCache (staleness)
        -> DependencyGraph (invalidation)
```

Key behaviors:
- On workspace open: discover `.spyproj`, build full `DependencyGraph`, run full semantic analysis
- On file change: use `DependencyGraph.GetAffectedFiles()` to identify transitive dependents, re-analyze only those
- Cache `SemanticResult` per file; invalidate via content hash (infrastructure already exists in `IncrementalCompilationCache`)
- Expose a unified `GetSemanticModel(uri)` that always returns project-aware results

This unlocks every cross-file feature: accurate completions from imported modules, workspace-wide rename that actually understands imports, references across the whole project.

### 0.3 — Background Indexing with Progress Reporting

Initial project analysis should happen on a background thread pool with LSP `$/progress` notifications ("Indexing: 42/156 files"). Use `DependencyGraph.GetParallelizableGroups()` — it already exists — to maximize parallelism. The compiler's `DiagnosticBag` is already thread-safe.

**Deliverable:** Opening a 200-file project gets full semantic analysis in seconds, not minutes. "Go to Definition" works immediately for files already indexed.

---

## Phase 1: Deep Structural Navigation

**Scope: Medium | Priority: High | Depends on: Phase 0**

These features are high-value and the compiler already has the data — they just need handlers.

### 1.1 — Call Hierarchy

`callHierarchy/incomingCalls`, `callHierarchy/outgoingCalls`

`SemanticInfo.GetReferences(symbol)` already tracks all call sites with file/span/line/col. For outgoing calls, walk the function body AST and collect `FunctionCallExpression` nodes, resolve each via `GetCallTarget()`. For incoming, use the existing reference tracking. This is mostly wiring.

### 1.2 — Type Hierarchy

`typeHierarchy/supertypes`, `typeHierarchy/subtypes`

`TypeSymbol` already has `BaseType` and `Interfaces`. For subtypes, build an inverted index — a `Map<TypeSymbol, List<TypeSymbol>>` during project analysis. `NameResolver.ResolveInheritance()` already computes this; just expose it.

### 1.3 — Workspace Symbol Search (Full Project)

Currently limited to open documents. With the project-wide semantic model from Phase 0, extend to search all `SymbolTable` entries across all project files. Add fuzzy matching (the LSP spec supports `SymbolKindFilter` and pattern matching).

### 1.4 — Go-to-Implementation

For interfaces and abstract classes: use the subtype index from 1.2. For virtual methods: find all overrides across the type hierarchy. The symbol system already tracks `IsAbstract`, `IsVirtual`, `IsOverride`.

---

## Phase 2: Intelligent Refactoring & Code Actions

**Scope: Large | Priority: High | Depends on: Phase 0, partially Phase 1**

Current code actions are limited to "remove unused import/variable" and "fix naming convention." This phase makes the LSP an editor, not just a viewer.

### 2.1 — Extract Method / Extract Variable

- **Extract Variable:** Identify the selected expression, compute its type via `SemanticInfo.GetExpressionType()`, generate a variable declaration, replace all identical subexpressions in scope.
- **Extract Method:** More complex. Need to analyze the selected statements for: captured variables (read `SymbolTable` scopes), return type (last expression or void), parameters (variables read but not declared in selection). Generate a method stub with correct signature. The `NameMangler` handles Python-to-C# naming.

### 2.2 — Implement Interface Members

When a class declares `implements SomeInterface` but is missing methods: detect via `InterfaceConflictValidator` (already exists at validation order 170), offer a code action to generate stubs. `TypeSymbol.Methods` on the interface gives you the full signatures.

### 2.3 — Convert Between Forms

- `if/elif/else` to `match` statement (and vice versa) — structural AST transform
- Add/remove type annotations — `SemanticInfo` has the inferred types
- Wrap in `try/except` — straightforward AST wrapping

### 2.4 — Organize Imports

Sort imports alphabetically, group by stdlib vs. project vs. third-party. Remove unused (already detected by `UnusedImportValidator` at order 430). The import structure is well-modeled in the AST.

### 2.5 — Inline Variable / Inline Function

Inverse of extract. Find the single assignment, replace all references with the expression. For functions with single call site, inline the body. Requires the reference tracking from `SemanticInfo.GetReferences()`.

---

## Phase 3: Performance & Scale

**Scope: Medium | Priority: Medium | Can start in parallel with Phase 2**

### 3.1 — Syntax-Only Fast Path

For features that don't need semantics (folding, document symbols, formatting, basic highlighting), bypass semantic analysis entirely. Run only `CompilerApi.Parse()` — it's an order of magnitude faster. The current handlers already do this for some features but not consistently.

### 3.2 — Cancellation Pipeline

When a user types rapidly, cancel in-flight analysis before starting a new one. The `CancellationToken` plumbing exists in `CompilerApi` but the workspace's debounce-and-fire-and-forget model doesn't cancel previous analysis tasks aggressively enough. Implement a `CancellationTokenSource` chain: keystroke -> cancel previous -> debounce -> new analysis.

### 3.3 — Partial Re-Analysis

The nuclear option today is re-running the full semantic pipeline on every change. For a single function body edit, only re-run type checking on that function, not the whole module. This requires:
- Detecting that only a function body changed (AST diff)
- Preserving the `SymbolTable` from the previous analysis
- Re-running `TypeChecker` on just the changed scope

This is a significant compiler change but has massive payoff for large files.

### 3.4 — Memory-Mapped Source Files

For files not currently open, use memory-mapped I/O instead of reading entire files into strings. The `SourceText` class would need a lazy-loading variant.

---

## Phase 4: The 2026 Edge — Agentic Interoperability

**Scope: Medium-Large | Priority: Medium | Depends on: Phase 0-1 complete**

### 4.1 — MCP Server Integration

Expose the LSP's semantic model as an MCP server that AI assistants can query:

```
tools:
  - get_symbol_at_position(file, line, col) -> Symbol + Type + References
  - get_diagnostics(file?) -> structured diagnostic list
  - get_type_hierarchy(symbol) -> tree
  - get_call_graph(symbol, depth) -> directed graph
  - search_symbols(query, kind?) -> matches
  - get_semantic_context(file, line_range) -> annotated code with types
```

This is relatively straightforward — the data already exists in `SemanticInfo`, `SymbolTable`, and the handlers. You're building a JSON API on top of the same queries the handlers use. The compiler already has `SemanticQuery` as a clean read-only interface.

### 4.2 — Diagnostic Quick-Fix Suggestions for AI

Enrich diagnostics with structured "fix hints" — not just "type mismatch: expected int, got str" but also `{ suggestedFix: "wrap with int()", confidence: 0.9 }`. This lets AI tools auto-apply fixes without understanding the language.

### 4.3 — Semantic Code Graph Export

Export the project's full symbol graph (types, functions, imports, call relationships) as a queryable structure. This is the "structural context" that modern AI tools consume. The `DependencyGraph` + `SymbolTable` + `SemanticInfo.GetReferences()` give you all the edges.

---

## Phase 5: Polish & Parity

**Scope: Medium | Priority: Lower | Ongoing**

### 5.1 — Range Formatting

Currently only full-document formatting. Add `textDocument/rangeFormatting` to format a selection. The lexer-based formatter needs to handle partial indentation contexts.

### 5.2 — Selection Range

`textDocument/selectionRange` — expand selection to next syntactic scope (expression -> statement -> block -> function -> class -> module). Walk the AST containment hierarchy using `AstPositionIndex.FindAllContainingNodes()`.

### 5.3 — Linked Editing Ranges

For string interpolation, f-strings, or paired delimiters — highlight both ends simultaneously. Low priority but nice UX.

### 5.4 — Notebook Support

If Sharpy ever targets notebook-style execution (like Jupyter for Python), the LSP would need `NotebookDocumentSyncHandler`. Low priority.

---

## Phasing Summary

```
Phase 0  [Critical Foundation]
  0.1 Incremental sync         **  (small)
  0.2 Project-wide analysis    ******  (large, most important)
  0.3 Background indexing      ***  (medium)

Phase 1  [Structural Navigation]
  1.1 Call hierarchy           **  (small-medium, data exists)
  1.2 Type hierarchy           *   (small, data exists)
  1.3 Workspace symbols        *   (small, extends existing)
  1.4 Go-to-implementation     *   (small, combines 1.1+1.2)

Phase 2  [Refactoring]
  2.1 Extract method/variable  ****  (medium-large)
  2.2 Implement interface      **  (medium)
  2.3 Convert between forms    **  (medium)
  2.4 Organize imports         *   (small, validator exists)
  2.5 Inline variable/function **  (medium)

Phase 3  [Performance]                        <- can overlap Phase 2
  3.1 Syntax-only fast path    *   (small)
  3.2 Cancellation pipeline    **  (medium)
  3.3 Partial re-analysis      ****  (large, compiler change)
  3.4 Memory-mapped sources    *   (small)

Phase 4  [2026 Edge]
  4.1 MCP server               **  (medium, data exists)
  4.2 AI fix suggestions       **  (medium)
  4.3 Semantic graph export    **  (medium)

Phase 5  [Polish]                             <- ongoing
  5.1-5.4 Range fmt, selection, linked editing, notebooks
```

---

## Prioritization Rationale

**Phase 0.2 is the only thing that truly matters right now.** Everything else is incremental improvement on an already-functional LSP. But without project-wide analysis, the LSP is fundamentally lying — it shows completions and types based on single-file analysis, which means imported symbols are partially resolved, cross-file rename is fragile, and diagnostics may differ from actual compilation.

The good news: the compiler already built all the hard infrastructure (`ProjectCompiler`, `DependencyGraph`, `IncrementalCompilationCache`, `SymbolCache`). The work is integration — teaching the LSP to use `ProjectCompiler` instead of raw `CompilerApi.Analyze()`, and managing the lifecycle of a persistent project-wide semantic model.

After Phase 0, **Phase 1** next because it's high-value, low-effort (the data already exists), and users notice navigation features immediately. Then **Phase 2** and **Phase 3** in parallel — refactoring is high-impact for users while performance is high-impact for retention.

**Phase 4 (MCP/AI)** is strategically important for 2026 but can wait until the core is solid. Building an MCP server on top of a buggy semantic model just exports bugs to AI tools.

---

## Maturity Comparison: Basic vs. Mature vs. Sharpy Today

| Feature | Basic LSP | Mature (rust-analyzer) | Sharpy Today | Sharpy After Plan |
|---|---|---|---|---|
| Highlighting | Regex | Semantic | Semantic | Semantic |
| Navigation | Jump-to-def | Call/type hierarchies | Jump-to-def + refs | Full hierarchies |
| Hints | Hover only | Inlay hints | Inlay hints | Inlay hints |
| Fixes | Simple | Extract/inline/implement | Remove unused | Full refactoring suite |
| Cross-file | None | Full project | Single-file | Full project |
| AI Support | None | MCP/structured context | None | MCP + semantic graph |
| Performance | Full reparse | Incremental | Debounced full | Incremental + partial |
