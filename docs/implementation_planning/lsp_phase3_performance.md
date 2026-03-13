# LSP Phase 3: Performance & Scale

## Context

This plan implements Phase 3 of the [LSP Maturity Plan](docs/implementation_planning/lsp_maturity_plan.md) — Performance & Scale. The phase focuses on four improvements:

1. **Syntax-only fast path** — bypass semantic analysis for handlers that only need the AST
2. **Cancellation pipeline** — aggressively cancel stale analysis when new edits arrive
3. **Partial re-analysis** — re-type-check only changed function bodies, not entire modules
4. **Memory-mapped source files** — lazy-load project files not open in the editor

Phase 3 can run in parallel with Phase 2 (Refactoring). No GitHub issues exist for these items yet.

## Current State

### Handler Analysis Patterns

Every handler currently goes through `LanguageService.GetAnalysisAsync()` → full semantic analysis. But several handlers only use the AST:

| Handler | Needs Semantics? | Current Path |
|---------|-----------------|--------------|
| **FoldingRangeHandler** | No — walks `analysis.Ast.Body` | `GetAnalysisAsync()` |
| **DocumentSymbolHandler** | No — walks `analysis.Ast.Body` | `GetAnalysisAsync()` |
| **SemanticTokensHandler** | No — walks `analysis.Ast.Body` (no `SemanticQuery` calls) | `GetAnalysisAsync()` |
| **FormattingHandler** | No — uses Lexer directly | `GetDocument()` (already fast) |
| HoverHandler | Yes — `SemanticQuery` | `GetAnalysisAsync()` |
| CompletionHandler | Yes — `SymbolTable`, `SemanticQuery` | `GetAnalysisAsync()` |
| InlayHintHandler | Yes — `SemanticQuery`, `SymbolTable` | `GetAnalysisAsync()` |
| CodeLensHandler | Yes — `SemanticQuery`, `SymbolTable` | `GetAnalysisAsync()` |
| All others | Yes | `GetAnalysisAsync()` |

### Cancellation Infrastructure

- `SharplyWorkspace.ScheduleAnalysis()` — 300ms debounce timer per document
- `DocumentState._pendingCts` — cancels previous single-file analysis
- `LanguageService._documentCts` — cancels previous project-level reanalysis per document
- `LanguageService._analysisLock` — `SemaphoreSlim(1,1)` serializes all project reanalysis (head-of-line blocking)
- **Gap**: No coordination between parse-only and semantic analysis; no handler-level cancellation when superseded by a newer edit

### SourceText

`SourceText` (in `Text/SourceText.cs`) is fully immutable. It holds the entire file content as a `string` plus precomputed `int[] _lineStarts`. `WithChanges()` already creates a new instance. For non-open project files, `LanguageService.GetSourceText()` reads from disk with `File.ReadAllText()`.

### CompilerApi

`CompilerApi.Parse()` already exists and is documented as the fast path: "Useful for tooling that only needs syntax information (e.g., syntax highlighting, formatting)." It returns `ParseResult` with just `Success`, `Diagnostics`, and `Ast`.

## Design Decisions

1. **Parse cache is separate from semantic cache** — `DocumentState` will have both `CachedParseResult` and `CachedAnalysis`. Parse cache invalidates on text change; semantic cache also invalidates but takes longer to recompute. This lets syntax-only handlers respond immediately after a keystroke.

2. **No partial semantic results during indexing** — When background indexing is in progress, syntax-only handlers can still return results via the parse cache. Semantic handlers continue to fall back to single-file analysis via `SharplyWorkspace`.

3. **Cancellation is per-document, not global** — Editing file A should not cancel analysis of file B. The `_analysisLock` serialization is a correctness issue (Phase 3.2 addresses it).

4. **Partial re-analysis scope** — Phase 3.3 targets function-body-only changes (the common case while coding). Changes to signatures, class structure, or imports still trigger full reanalysis. This is the 80/20 split.

5. **LazySourceText is opt-in** — Only used for non-open project files in the `LanguageService`. Open documents always use the in-memory `DocumentState.SourceText`.

## Implementation

### Phase 3.1: Syntax-Only Fast Path

**Goal:** Handlers that only need the AST bypass semantic analysis, responding in milliseconds instead of hundreds of milliseconds.

#### Tasks

1. **Add `CachedParseResult` to `DocumentState`** — `src/Sharpy.Lsp/SharplyWorkspace.cs`
   - Add a `ParseResult? CachedParseResult` property alongside `CachedAnalysis`
   - In `Update()` and `ApplyIncrementalChanges()`, clear both caches
   - Add `GetOrRunParseAsync(CompilerApi api, CancellationToken ct)` method that calls `CompilerApi.Parse()` and caches the result
   - Parse result cache is populated independently and faster than the semantic cache
   - Acceptance: `DocumentState` has separate parse cache; `GetOrRunParseAsync` returns `ParseResult`
   - Commit: `feat(lsp): add parse-only cache to DocumentState`

2. **Add `GetParseResultAsync()` to `LanguageService`** — `src/Sharpy.Lsp/LanguageService.cs`
   - Add method: `public async Task<ParseResult?> GetParseResultAsync(string uri, CancellationToken ct)`
   - For project files: call `CompilerApi.Parse()` on the document text (no project-level analysis needed, parse is single-file)
   - For workspace files: delegate to `DocumentState.GetOrRunParseAsync()`
   - Does NOT acquire `_analysisLock` — parsing is stateless and can run concurrently with semantic analysis
   - Acceptance: `GetParseResultAsync` works for both project and non-project files
   - Commit: `feat(lsp): add parse-only fast path to LanguageService`

3. **Switch `FoldingRangeHandler` to parse-only** — `src/Sharpy.Lsp/Handlers/FoldingRangeHandler.cs`
   - Replace `_languageService.GetAnalysisAsync(uri, ct)` with `_languageService.GetParseResultAsync(uri, ct)`
   - Change null check from `analysis?.Ast` to `parseResult?.Ast`
   - Change `CollectFoldingRanges(analysis.Ast.Body, ...)` to use `parseResult.Ast.Body`
   - Acceptance: FoldingRange works without waiting for semantic analysis; existing `FoldingRangeTests` pass
   - Commit: `perf(lsp): switch FoldingRangeHandler to parse-only fast path`

4. **Switch `DocumentSymbolHandler` to parse-only** — `src/Sharpy.Lsp/Handlers/DocumentSymbolHandler.cs`
   - Replace `_languageService.GetAnalysisAsync(uri, ct)` with `_languageService.GetParseResultAsync(uri, ct)`
   - Change null check and body access accordingly
   - Acceptance: DocumentSymbol works without waiting for semantic analysis; existing `DocumentSymbolTests` pass
   - Commit: `perf(lsp): switch DocumentSymbolHandler to parse-only fast path`

5. **Switch `SemanticTokensHandler` to parse-only** — `src/Sharpy.Lsp/Handlers/SemanticTokensHandler.cs`
   - The `CollectTokens` method takes `SemanticResult analysis` but never accesses `SemanticQuery` or `SymbolTable` — it only reads `Ast.Body`
   - Change `Tokenize()` to call `GetParseResultAsync()` instead of `GetAnalysisAsync()`
   - Refactor `CollectTokens` and `CollectStatementTokens` signatures: replace `SemanticResult analysis` parameter with `Module ast` (since only `analysis.Ast.Body` is accessed)
   - Update the internal test helper `CollectTokens` call in `SemanticTokensTests` accordingly
   - Acceptance: Semantic tokens work without waiting for semantic analysis; existing `SemanticTokensTests` pass
   - Commit: `perf(lsp): switch SemanticTokensHandler to parse-only fast path`

6. **Add tests for parse-only fast path** — `src/Sharpy.Lsp.Tests/LanguageServiceTests.cs`
   - Add `GetParseResult_ReturnsAstWithoutSemanticAnalysis` — verify `GetParseResultAsync` returns an AST
   - Add `GetParseResult_WorksWithSyntaxErrors` — verify parse results include diagnostics for syntax errors
   - Add `GetParseResult_WorkspaceFallback` — verify non-project files use workspace parse
   - Acceptance: New tests pass; no regressions
   - Commit: `test(lsp): add tests for parse-only fast path`

### Phase 3.2: Cancellation Pipeline

**Goal:** When a user types rapidly, cancel in-flight analysis before starting new analysis. Reduce wasted work and improve responsiveness.

#### Tasks

7. **Add `AnalysisVersionTracker` to `DocumentState`** — `src/Sharpy.Lsp/SharplyWorkspace.cs`
   - Add an `int _analysisVersion` field (monotonically increasing) to `DocumentState`
   - Increment it in `Update()` and `ApplyIncrementalChanges()` (alongside cache invalidation)
   - Add `int AnalysisVersion { get; }` property
   - `GetOrRunAnalysisAsync` records the version at start and checks it at end — if version changed during analysis, discard result and retry (the stale analysis is useless)
   - Acceptance: Rapid edits cause stale analyses to be discarded
   - Commit: `feat(lsp): add analysis version tracking to DocumentState`

8. **Replace `_analysisLock` with per-document semaphores in `LanguageService`** — `src/Sharpy.Lsp/LanguageService.cs`
   - The current `SemaphoreSlim(1,1) _analysisLock` serializes ALL project reanalysis globally. Editing file A blocks reanalysis of file B.
   - Replace with a `ConcurrentDictionary<string, SemaphoreSlim> _documentAnalysisLocks` (one per document/file)
   - In `OnDocumentChangedAsync`: acquire lock for the changed document's path, not a global lock
   - Keep a separate `SemaphoreSlim _projectLock` for `InitializeProjectAsync` and `ReloadProjectAsync` (these are whole-project operations)
   - Note: `OnDocumentChangedAsync` currently re-runs `AnalyzeProject(config)` which is a full project re-analysis. This is the existing behavior — changing to incremental project re-analysis is Phase 3.3. For now, keep the `_projectLock` for `OnDocumentChangedAsync` too, but ensure it's cancellable.
   - Acceptance: Existing `LanguageServiceTests` pass; concurrent document operations don't deadlock
   - Commit: `perf(lsp): reduce head-of-line blocking in LanguageService reanalysis`

9. **Add `CancellableAnalysisScope` helper** — `src/Sharpy.Lsp/CancellableAnalysisScope.cs` (new file)
   - A lightweight `IDisposable` that wraps `CancellationTokenSource.CreateLinkedTokenSource()` with version-aware cancellation
   - Constructor: `CancellableAnalysisScope(ConcurrentDictionary<string, CancellationTokenSource> registry, string key, CancellationToken external)`
   - On create: cancel and dispose any existing CTS for the key, register new linked CTS
   - `Token` property: the linked CancellationToken
   - `Dispose`: remove from registry and dispose CTS (if still current)
   - This consolidates the cancel-previous-create-new pattern used in both `DocumentState._pendingCts` and `LanguageService._documentCts`
   - Acceptance: Replaces manual CTS management in DocumentState and LanguageService
   - Commit: `refactor(lsp): extract CancellableAnalysisScope for consistent cancellation`

10. **Integrate `CancellableAnalysisScope` into `DocumentState` and `LanguageService`** — `src/Sharpy.Lsp/SharplyWorkspace.cs`, `src/Sharpy.Lsp/LanguageService.cs`
    - In `DocumentState.GetOrRunAnalysisAsync`: replace manual `_pendingCts` management with `CancellableAnalysisScope`
    - In `LanguageService.OnDocumentChangedAsync`: replace manual `_documentCts` management with `CancellableAnalysisScope`
    - Acceptance: Existing cancellation tests pass; code is cleaner
    - Commit: `refactor(lsp): use CancellableAnalysisScope in workspace and language service`

11. **Add cancellation pipeline tests** — `src/Sharpy.Lsp.Tests/LanguageServiceTests.cs`
    - Add `RapidEdits_CancelStaleAnalysis_ParseStillWorks` — verify parse-only path returns results even during cancelled semantic analysis
    - Add `CancellableAnalysisScope_CancelsPrevious` — unit test for the scope helper
    - Acceptance: Tests pass; demonstrates cancellation behavior
    - Commit: `test(lsp): add cancellation pipeline tests`

### Phase 3.3: Partial Re-Analysis

**Goal:** For function-body-only edits, re-type-check only the changed function instead of the entire module. This is the highest-impact performance change for large files.

This is a significant compiler change split into sub-phases.

#### Tasks

12. **Add `AstFingerprint` utility** — `src/Sharpy.Compiler/Services/AstFingerprint.cs` (new file)
    - `static AstChangeKind Classify(Module oldAst, Module newAst)` returning one of:
      - `NoChange` — ASTs are identical
      - `BodyOnly(string functionName, int functionIndex)` — only a single function body changed, top-level structure is identical
      - `Structural` — anything else (new declarations, signature changes, import changes, etc.)
    - Implementation: Compare `Module.Body` by walking top-level statements. For each `FunctionDef`/`ClassDef`, compare name, parameters, decorators, return type annotation (but NOT body). If only one function body differs, return `BodyOnly`.
    - Key insight: AST nodes are records with value equality. But bodies contain position info that changes with every edit. So comparison must ignore line/column fields.
    - Add `bool StructuralEquals(Statement a, Statement b)` that compares statement structure ignoring positions.
    - Acceptance: Correctly classifies body-only vs structural changes
    - Commit: `feat(compiler): add AstFingerprint for classifying AST changes`

13. **Add `ScopedTypeChecker` API** — `src/Sharpy.Compiler/Semantic/ScopedTypeChecker.cs` (new file)
    - A focused re-checking API: given a previous `SemanticResult` (with `SemanticInfo` + `SymbolTable`) and a specific function body to re-check, re-run type checking on just that scope.
    - `public static SemanticResult RecheckFunction(SemanticResult previous, Module newAst, int functionIndex, CancellationToken ct)`
    - Steps:
      1. Copy `previous.SemanticInfo` (or create a new one seeded with previous data)
      2. Extract the function at `functionIndex` from `newAst`
      3. Create a new `TypeChecker` with the existing `SymbolTable`
      4. Run `CheckFunctionBody()` on just that function
      5. Merge the new function's type info into the copied `SemanticInfo`
      6. Return updated `SemanticResult`
    - **Critical constraint**: `SemanticInfo` uses `ReferenceEqualityComparer` for AST nodes. Since the new AST has different node instances, we must map from old nodes → types for unchanged code and only add new mappings for the re-checked function.
    - Acceptance: Re-checking a single function produces correct type info
    - Commit: `feat(compiler): add ScopedTypeChecker for partial re-analysis`

14. **Integrate partial re-analysis into `DocumentState`** — `src/Sharpy.Lsp/SharplyWorkspace.cs`
    - In `GetOrRunAnalysisAsync`:
      1. Parse the current text (fast)
      2. If `CachedAnalysis != null` and parse succeeds, call `AstFingerprint.Classify(cached.Ast, newAst)`
      3. If `BodyOnly`: call `ScopedTypeChecker.RecheckFunction(cached, newAst, functionIndex)`
      4. If `Structural` or `NoChange` or cache miss: fall through to full `api.Analyze()`
    - Store the previous `ParseResult` alongside `CachedAnalysis` to enable diff
    - Acceptance: Editing inside a function body uses partial re-analysis; editing signatures falls back to full
    - Commit: `perf(lsp): integrate partial re-analysis for function body edits`

15. **Add partial re-analysis tests** — `src/Sharpy.Compiler.Tests/Services/AstFingerprintTests.cs`, `src/Sharpy.Compiler.Tests/Semantic/ScopedTypeCheckerTests.cs` (new files)
    - `AstFingerprintTests`:
      - `Classify_NoChange_ReturnsSame`
      - `Classify_FunctionBodyChange_ReturnsBodyOnly`
      - `Classify_NewFunction_ReturnsStructural`
      - `Classify_ParameterChange_ReturnsStructural`
      - `Classify_ImportChange_ReturnsStructural`
    - `ScopedTypeCheckerTests`:
      - `RecheckFunction_BodyChange_UpdatesTypes`
      - `RecheckFunction_PreservesOtherFunctionTypes`
      - `RecheckFunction_DetectsNewTypeErrors`
    - Acceptance: All tests pass
    - Commit: `test(compiler): add tests for AstFingerprint and ScopedTypeChecker`

16. **Add LSP-level partial re-analysis tests** — `src/Sharpy.Lsp.Tests/WorkspaceTests.cs`
    - `DocumentState_FunctionBodyEdit_UsesPartialReanalysis` — verify that a body-only edit is faster than a structural edit (not a timing test — verify via a mock/spy that `ScopedTypeChecker.RecheckFunction` was called)
    - `DocumentState_StructuralEdit_UsesFullReanalysis` — verify full analysis on signature change
    - Acceptance: Tests pass
    - Commit: `test(lsp): add partial re-analysis integration tests`

### Phase 3.4: Memory-Mapped Source Files

**Goal:** Reduce memory usage for large projects by lazy-loading source text for files not open in the editor.

#### Tasks

17. **Add `LazySourceText` class** — `src/Sharpy.Compiler/Text/LazySourceText.cs` (new file)
    - Inherits no base class (composition over inheritance) but provides the same API surface as `SourceText` for the properties used by the LSP: `ToString()`, `Length`, `FilePath`
    - Constructor takes a file path; defers `File.ReadAllText()` until `ToString()` or `Length` is first accessed
    - Uses `Lazy<SourceText>` internally: `private readonly Lazy<SourceText> _inner;`
    - `public SourceText Materialize()` — forces loading and returns the inner `SourceText`
    - `public bool IsLoaded` — whether the file has been read yet
    - This is a simple wrapper, not a memory-mapped file (despite the maturity plan's name). Memory-mapping adds complexity for marginal gain at Sharpy's current scale. Lazy loading captures 90% of the benefit.
    - Acceptance: `LazySourceText` defers file I/O until accessed
    - Commit: `feat(compiler): add LazySourceText for deferred file loading`

18. **Use `LazySourceText` in `LanguageService.GetSourceText()`** — `src/Sharpy.Lsp/LanguageService.cs`
    - In `GetSourceText()`: for project files not open in the editor, return a lazily-loaded `SourceText` instead of eagerly reading with `File.ReadAllText()`
    - Change: `return new SourceText(File.ReadAllText(filePath), filePath)` → `return new LazySourceText(filePath).Materialize()`
    - Note: Since callers expect `SourceText`, `LazySourceText.Materialize()` returns the concrete type. The lazy behavior benefits callers that may not use the source text (e.g., `GetSourceText` is called speculatively by `DiagnosticPublisher`).
    - Actually, the bigger win is in `InitializeProjectAsync` — the per-file result cache doesn't need source text at all. Currently it reads every file into memory via `AnalyzeProject`. This is inherent to the compiler, not the LSP — so the LSP-level optimization is limited.
    - Acceptance: LSP behavior unchanged; no eager file reads for non-open project files
    - Commit: `perf(lsp): use lazy source text loading for non-open project files`

19. **Add `LazySourceText` tests** — `src/Sharpy.Compiler.Tests/Text/LazySourceTextTests.cs` (new file)
    - `Constructor_DoesNotReadFile` — verify file is not read on construction
    - `Materialize_ReadsFile` — verify file is read on `Materialize()`
    - `IsLoaded_FalseBeforeAccess_TrueAfter` — verify lazy behavior
    - `Materialize_NonexistentFile_Throws` — verify error handling
    - Acceptance: All tests pass
    - Commit: `test(compiler): add LazySourceText tests`

## Testing Strategy

### New Test Fixtures
No new `.spy`/`.expected` file-based tests needed — Phase 3 is LSP infrastructure, not language features.

### Unit Tests (new)
- `LanguageServiceTests` — parse-only fast path, cancellation
- `AstFingerprintTests` — AST change classification
- `ScopedTypeCheckerTests` — partial re-analysis correctness
- `LazySourceTextTests` — lazy loading behavior
- `CancellableAnalysisScope` — cancellation helper

### Regression Tests
- All existing LSP handler tests must pass unchanged
- All existing `LanguageServiceTests` must pass unchanged
- All existing `WorkspaceTests` must pass unchanged
- Full `dotnet test` must pass with zero regressions

### Edge Cases
- **Parse errors with syntax-only path**: Handlers should gracefully handle `ParseResult.Success == false` (return null/empty)
- **Rapid cancellation**: 10+ rapid edits should not leak `CancellationTokenSource` instances
- **Partial re-analysis boundary**: Editing a function's signature (not body) must trigger full re-analysis
- **Partial re-analysis with imports**: If a function body references a newly-imported symbol, partial re-analysis should still produce correct types (or fall back to full)
- **LazySourceText with deleted files**: `Materialize()` should throw `FileNotFoundException`

## Issues to Close

No existing GitHub issues. Consider creating tracking issues:
- "LSP: syntax-only fast path for folding, symbols, and semantic tokens" (closed by Phase 3.1, Tasks 1-6)
- "LSP: improved cancellation pipeline for rapid typing" (closed by Phase 3.2, Tasks 7-11)
- "LSP: partial re-analysis for function body edits" (closed by Phase 3.3, Tasks 12-16)
- "LSP: lazy source text loading for non-open files" (closed by Phase 3.4, Tasks 17-19)

## Risk Assessment

| Task | Risk | Mitigation |
|------|------|------------|
| Phase 3.1 | Low — straightforward handler changes | Existing tests catch regressions |
| Phase 3.2 | Medium — concurrent cancellation is subtle | `CancellableAnalysisScope` encapsulates the pattern; extensive tests |
| Phase 3.3 | High — `SemanticInfo` uses reference equality; partial re-analysis must handle node identity correctly | `AstFingerprint` has a clear fallback to full analysis; conservative classification (any ambiguity → `Structural`) |
| Phase 3.4 | Low — simple lazy wrapper | Minimal behavior change |

**Recommended implementation order**: 3.1 → 3.4 → 3.2 → 3.3 (easiest first; 3.3 is highest risk and should be last)
