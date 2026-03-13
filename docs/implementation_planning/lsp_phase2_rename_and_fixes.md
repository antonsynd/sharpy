<!-- Verified by /verify-plan on 2026-03-13 -->
<!-- Verification result: PASS WITH CORRECTIONS -->

# LSP Phase 2: Sharply→Sharpy Rename + Issues #378–#380

## Context

Three issues from the LSP Phase 2 verification audit need resolution, plus a pervasive naming bug where AI agents hallucinated "Sharply" instead of "Sharpy" for all LSP class names. This has happened multiple times across agent sessions and needs a definitive fix.

- [#378](https://github.com/antonsynd/sharpy/issues/378) — CodeActionHandler should use LanguageService exclusively
- [#379](https://github.com/antonsynd/sharpy/issues/379) — ExtractMethodProvider.TryResolveVariableType misleading comment and missing UnknownType handling
- [#380](https://github.com/antonsynd/sharpy/issues/380) — Add test infrastructure for ImplementInterfaceProvider positive path testing

## Current State

### Sharply Naming Bug
25 classes across `src/Sharpy.Lsp/` and `src/Sharpy.Lsp.Tests/` use the prefix `Sharply` instead of `Sharpy`. Three files also have `Sharply` in their filenames. Two docs files reference `SharplyWorkspace`. The correct prefix is `Sharpy` — the project name is "Sharpy", not "Sharply".

**Files needing rename:**
- `src/Sharpy.Lsp/SharplyWorkspace.cs` → `SharpyWorkspace.cs` [CORRECTED: original said rename to `SharplyWorkspace.cs` (no change), should be `SharpyWorkspace.cs`]
- `src/Sharpy.Lsp/Refactoring/SharplySourceGenerator.cs` → `SharpySourceGenerator.cs`
- `src/Sharpy.Lsp.Tests/Refactoring/SharplySourceGeneratorTests.cs` → `SharpySourceGeneratorTests.cs`

**25 classes needing rename** (all `Sharply*` → `Sharpy*`):
- SharplyWorkspace, SharplySourceGenerator, SharplySourceGeneratorTests
- 22 handlers [CORRECTED: was "20", actual count is 22]: SharplyHoverHandler, SharplyDefinitionHandler, SharplyCompletionHandler, SharplyReferencesHandler, SharplyRenameHandler, SharplyDocumentSymbolHandler, SharplySignatureHelpHandler, SharplySemanticTokensHandler, SharplyCodeActionHandler, SharplyFormattingHandler, SharplyFoldingRangeHandler, SharplyWorkspaceSymbolHandler, SharplyInlayHintHandler, SharplyDocumentHighlightHandler, SharplyCodeLensHandler, SharplyCallHierarchyPrepareHandler, SharplyCallHierarchyIncomingHandler, SharplyCallHierarchyOutgoingHandler, SharplyTypeHierarchyPrepareHandler, SharplyTypeHierarchySupertypesHandler, SharplyTypeHierarchySubtypesHandler, SharplyImplementationHandler
- Plus 2 docs files with `SharplyWorkspace` references

### Issue #378 — CodeActionHandler Uses SharplyWorkspace Directly
`CodeActionHandler` currently holds three dependencies: `_languageService`, `_compilerApi`, and `_workspace`. The handler accesses `_workspace.GetDocument()` directly for document text and falls back to `doc?.CachedAnalysis` when LanguageService isn't ready. The handler should only use `LanguageService` as the abstraction layer.

Key finding: No code action provider actually uses `context.CompilerApi`. The `CompilerApi` parameter in `CodeActionProviderContext` is unused by all providers.

### Issue #379 — ExtractMethodProvider Misleading Comment
In `ExtractMethodProvider.TryResolveVariableType()` (line ~226), the comment says "Use object as a placeholder" but the code returns `SemanticType.Unknown`. The `UnknownType` propagates through `BuildParameters()` to `SharplySourceGenerator.FormatFunctionDef()`. Need to verify how `FormatTypeAnnotation()` handles `UnknownType` and fix the comment + add explicit handling.

### Issue #380 — ImplementInterfaceProvider Untestable Positive Paths
The Sharpy compiler rejects classes with missing interface implementations (SPY0320), causing `SymbolTable` to be null in analysis results. This makes the stub generation logic in `ImplementInterfaceProvider` unreachable from tests. Current tests only cover 9 negative/error paths. The core stub generation logic (methods `GenerateStubs`, `FormatMethodStub`, plus `SharplySourceGenerator.FormatPropertyDef`) is untested. [CORRECTED: `GenerateStubsForInterface` is actually `GenerateStubs`; `FormatPropertyStub` doesn't exist — properties use `SharplySourceGenerator.FormatPropertyDef`]

## Design Decisions

1. **Rename order matters**: The Sharply→Sharpy rename must happen first (Phase 1), because subsequent phases modify renamed files. This avoids merge conflicts.

2. **LanguageService as sole mediator** (#378): Add `GetDocumentText(uri)` to LanguageService and remove direct workspace access from CodeActionHandler. Also remove `CompilerApi` from `CodeActionProviderContext` since no provider uses it.

3. **UnknownType handling** (#379): Fix the misleading comment. In `FormatTypeAnnotation()`, explicitly handle `UnknownType` by omitting the type annotation (Python-style untyped parameter), which is the safest LSP behavior when type inference fails.

4. **Testability via extraction** (#380): Extract stub generation into a static testable method rather than mocking the compiler. This follows the principle of making code testable through better design, not through mocks. The stub generation methods (`FormatMethodStub`, `FormatPropertyStub`) can be tested directly with hand-constructed `FunctionSymbol`/`PropertySymbol` objects.

## Implementation

### Phase 1: Rename Sharply → Sharpy

**Goal:** Fix the naming hallucination across the entire LSP codebase.

#### Tasks

1. **Rename all `Sharply` class prefixes to `Sharpy` in source files** — `src/Sharpy.Lsp/`
   - Global find-and-replace `Sharply` → `Sharpy` in all `.cs` files under `src/Sharpy.Lsp/` and `src/Sharpy.Lsp.Tests/`
   - This covers class names, constructor names, field types, comments, and `using static` directives
   - Rename files: `SharplyWorkspace.cs` → `SharpyWorkspace.cs`, `SharplySourceGenerator.cs` → `SharpySourceGenerator.cs`, `SharplySourceGeneratorTests.cs` → `SharpySourceGeneratorTests.cs`
   - Acceptance: `grep -r "Sharply" src/Sharpy.Lsp/ src/Sharpy.Lsp.Tests/` returns zero results
   - Commit: `fix(lsp): rename Sharply to Sharpy across all LSP classes and files`

2. **Update documentation references** — `docs/`
   - Replace `SharplyWorkspace` → `SharpyWorkspace` in `docs/implementation_planning/lsp_maturity_plan.md` and `docs/tooling/lsp-server.md`
   - Acceptance: `grep -r "Sharply" docs/` returns zero results
   - Commit: `docs: update Sharply references to Sharpy in LSP documentation`

3. **Build and test** — verify no regressions
   - Run `dotnet build sharpy.sln` and `dotnet test`
   - Acceptance: All tests pass, zero build warnings
   - No separate commit (verification only)

### Phase 2: CodeActionHandler LanguageService Refactor (#378)

**Goal:** Make CodeActionHandler use LanguageService exclusively, removing direct workspace and CompilerApi dependencies.

#### Tasks

1. **Add `GetDocumentText` method to LanguageService** — `src/Sharpy.Lsp/LanguageService.cs`
   - Add `public string? GetDocumentText(string uri)` that delegates to `_workspace.GetDocument(uri)?.Text`
   - This encapsulates workspace access behind the LanguageService abstraction
   - Acceptance: Method exists and returns document text or null
   - Commit: `feat(lsp): add GetDocumentText method to LanguageService`

2. **Remove CompilerApi from CodeActionProviderContext** — `src/Sharpy.Lsp/Refactoring/ICodeActionProvider.cs` [CORRECTED: `CodeActionProviderContext` is defined in `ICodeActionProvider.cs`, not a standalone file]
   - Remove the `CompilerApi CompilerApi` parameter from the `CodeActionProviderContext` record
   - Verify no provider accesses `context.CompilerApi` (confirmed: none do)
   - Update all call sites constructing `CodeActionProviderContext`
   - Acceptance: `CodeActionProviderContext` no longer has a `CompilerApi` property
   - Commit: `refactor(lsp): remove unused CompilerApi from CodeActionProviderContext`

3. **Refactor CodeActionHandler to use LanguageService only** — `src/Sharpy.Lsp/Handlers/CodeActionHandler.cs`
   - Remove `_workspace` field and `SharpyWorkspace` constructor parameter
   - Remove `_compilerApi` field and `CompilerApi` constructor parameter (keep `_languageService` only)
   - Replace `_workspace.GetDocument(uri)?.Text` with `_languageService.GetDocumentText(uri)`
   - Replace `doc?.CachedAnalysis` fallback: add `GetCachedAnalysis(string uri)` to LanguageService if needed, or use existing `GetAnalysisAsync` flow
   - Update DI registration in `Program.cs` (remove workspace/api injection for this handler)
   - Acceptance: CodeActionHandler constructor takes only `LanguageService` and `IEnumerable<ICodeActionProvider>` (plus any remaining necessary deps)
   - Commit: `refactor(lsp): CodeActionHandler uses LanguageService exclusively (#378)`

4. **Update CodeActionTests** — `src/Sharpy.Lsp.Tests/CodeActionTests.cs`
   - Update test constructor to match new CodeActionHandler signature
   - Acceptance: All existing code action tests pass
   - Commit: `test(lsp): update CodeActionTests for LanguageService-only handler (#378)`

### Phase 3: ExtractMethodProvider Fix (#379)

**Goal:** Fix misleading comment and add proper UnknownType handling.

#### Tasks

1. **Fix misleading comment in TryResolveVariableType** — `src/Sharpy.Lsp/Refactoring/ExtractMethodProvider.cs`
   - Change comment from `// Last resort: unknown type. Use object as a placeholder.` to `// Last resort: return unknown type (parameter will be emitted without type annotation).`
   - Acceptance: Comment accurately describes behavior
   - Commit: `fix(lsp): correct misleading comment in ExtractMethodProvider.TryResolveVariableType (#379)`

2. **Add explicit UnknownType handling in SharpySourceGenerator.FormatTypeAnnotation** — `src/Sharpy.Lsp/Refactoring/SharpySourceGenerator.cs` (post-rename)
   - `FormatTypeAnnotation` currently falls through to `_ => type.GetDisplayName()` which returns `"<?>"` for `UnknownType` — this needs an explicit case [CORRECTED: clarified current behavior]
   - Add an explicit case: when type is `UnknownType`, return `""` (empty string) so the parameter is emitted without a type annotation
   - Also update `FormatParameter` which unconditionally emits `{name}: {FormatTypeAnnotation(type)}` — when type is `UnknownType`, it should emit just `{name}` (no annotation)
   - Note: `FormatMethodStub` already handles `UnknownType` for return types (line 423: `returnType is not null and not VoidType and not UnknownType`)
   - Acceptance: `FormatTypeAnnotation(SemanticType.Unknown)` returns `""` (no type annotation)
   - Commit: `fix(lsp): handle UnknownType explicitly in SharpySourceGenerator (#379)`

3. **Add test for UnknownType formatting** — `src/Sharpy.Lsp.Tests/Refactoring/SharpySourceGeneratorTests.cs` (post-rename)
   - Add test: `FormatTypeAnnotation_WithUnknownType_ReturnsEmptyString`
   - Add test: `FormatFunctionDef_WithUnknownParameterType_OmitsTypeAnnotation`
   - Acceptance: Tests verify that unknown-typed parameters produce valid untyped Sharpy syntax
   - Commit: `test(lsp): add UnknownType handling tests for SharpySourceGenerator (#379)`

### Phase 4: ImplementInterfaceProvider Test Infrastructure (#380)

**Goal:** Enable testing of the stub generation positive path without compiler cooperation.

#### Tasks

1. **Extract stub generation into static testable methods** — `src/Sharpy.Lsp/Refactoring/ImplementInterfaceProvider.cs`
   - Make `FormatMethodStub` (currently `private static`) into `internal static` [CORRECTED: only `FormatMethodStub` needs extraction; `FormatPropertyStub` doesn't exist — properties are formatted via the already-public `SharplySourceGenerator.FormatPropertyDef`]
   - Make `GenerateStubs` (currently `private static`) into `internal static` [CORRECTED: original said `GenerateStubsForInterface` which doesn't exist]
   - `InternalsVisibleTo("Sharpy.Lsp.Tests")` is already present in `Sharpy.Lsp.csproj`
   - Acceptance: Stub generation can be called directly from tests with hand-constructed symbols
   - Commit: `refactor(lsp): extract ImplementInterfaceProvider stub generation as internal static methods (#380)`

2. **Add positive-path tests for stub generation** — `src/Sharpy.Lsp.Tests/Refactoring/ImplementInterfaceProviderTests.cs`
   - Create `FunctionSymbol` and `PropertySymbol` objects directly (no compiler needed)
   - Test `FormatMethodStub` with: simple method, method with parameters, method with return type, abstract method
   - Test `SharplySourceGenerator.FormatPropertyDef` with: read-only property, read-write property, property with type annotation [CORRECTED: `FormatPropertyStub` doesn't exist; use `FormatPropertyDef` from `SharplySourceGenerator`]
   - Test `GenerateStubs` with a full interface symbol containing multiple methods and properties [CORRECTED: was `GenerateStubsForInterface`]
   - Acceptance: At least 6 positive-path tests covering the stub generation matrix
   - Commit: `test(lsp): add positive-path tests for ImplementInterfaceProvider stub generation (#380)`

3. **Verify full integration test approach** — `src/Sharpy.Lsp.Tests/Refactoring/ImplementInterfaceProviderTests.cs`
   - Investigate whether a valid Sharpy source with a partial interface implementation (some methods implemented, some missing) can pass compilation
   - If possible, add a full end-to-end test where the provider generates stubs for missing methods
   - If not possible due to SPY0320, document this limitation in a code comment
   - Acceptance: Either an integration test or a documented explanation of why it's not feasible
   - Commit: `test(lsp): add integration test or document limitation for ImplementInterfaceProvider (#380)`

## Testing Strategy

- **Phase 1 (rename):** Full build + full test suite. This is a mechanical rename — any failure indicates a missed reference.
- **Phase 2 (#378):** Existing CodeActionTests must pass with refactored handler. No new functionality, just dependency cleanup.
- **Phase 3 (#379):** New unit tests for `FormatTypeAnnotation(UnknownType)` and `FormatFunctionDef` with unknown-typed params.
- **Phase 4 (#380):** New positive-path unit tests for stub generation. At least 6 tests covering method stubs (no params, with params, with return type) and property stubs (read-only, read-write, typed).

**Edge cases:**
- Rename: Ensure no `Sharply` remains in string literals, comments, or XML docs
- #378: Handler must still work when LanguageService is not ready (graceful fallback)
- #379: `UnknownType` should never produce `object` in generated Sharpy code
- #380: Hand-constructed symbols must mirror what the compiler would produce

**Negative test cases:**
- #379: `FormatTypeAnnotation(null)` should not throw
- #380: Stub generation with empty interface (no methods/properties) returns empty string

## Issues to Close

- #378 — LSP: CodeActionHandler should use LanguageService exclusively (closed by Phase 2, Tasks 1–4)
- #379 — LSP: ExtractMethodProvider.TryResolveVariableType misleading comment and missing UnknownType handling (closed by Phase 3, Tasks 1–3)
- #380 — LSP: Add test infrastructure for ImplementInterfaceProvider positive path testing (closed by Phase 4, Tasks 1–3)

## Verification Summary

**Result:** PASS WITH CORRECTIONS
**Verified on:** 2026-03-13
**Plan file:** `~/.claude/plans/plan-752b1c.md`

### Corrections Made

1. **Line 17: File rename target** — Changed `SharplyWorkspace.cs → SharplyWorkspace.cs` to `→ SharpyWorkspace.cs` (was a no-op rename, contradicted by line 58 which had it right)
2. **Line 23: Handler count** — Changed "20 handlers" to "22 handlers" (actual count from codebase grep: 22 `Sharply*Handler` classes)
3. **Line 84: CodeActionProviderContext file path** — Changed `CodeActionProviderContext.cs` to `ICodeActionProvider.cs` (the record is defined in that file, no standalone file exists)
4. **Line 35 / Phase 4: Method names** — `GenerateStubsForInterface` → `GenerateStubs` (actual method name); `FormatPropertyStub` doesn't exist (properties use `SharplySourceGenerator.FormatPropertyDef` which is already `public static`)
5. **Phase 3 Task 2: FormatTypeAnnotation current behavior** — Clarified that `UnknownType` currently falls to `_ => type.GetDisplayName()` returning `"<?>"`, added note about `FormatParameter` also needing update, and noted `FormatMethodStub` already handles `UnknownType` for return types

### Warnings

1. **FormatFunctionDef doesn't check UnknownType** — `FormatFunctionDef` (used by ExtractMethodProvider) checks `returnType is not VoidType` but not `UnknownType`, so it would emit `-> <?>` for unknown return types. `FormatMethodStub` already handles this correctly. Plan should consider also fixing `FormatFunctionDef`.
2. **FormatParameter unconditionally emits type** — `FormatParameter` does `$"{name}: {FormatTypeAnnotation(type)}"` which would produce `name: <?>` for `UnknownType`. The plan's `FormatTypeAnnotation` fix returning `""` would produce `name: ` (trailing colon-space). Consider updating `FormatParameter` to omit the annotation entirely when type is `UnknownType`.
3. **SPY0320 naming** — Plan says "rejects classes with missing interface implementations". The actual diagnostic is `ProtocolMissingMethod` — close enough but worth noting for precision.

### Missing Steps Added

- Phase 3 Task 2 should also update `FormatParameter` to handle `UnknownType` (omit type annotation entirely)
- Phase 3 Task 2 should consider also fixing `FormatFunctionDef` to skip `UnknownType` return type annotation (like `FormatMethodStub` does)

### Unchecked Claims

- Plan claims "9 negative/error paths" for ImplementInterfaceProvider tests — verified: 9 test methods exist, all covering negative/graceful-failure scenarios ✓
- Plan claims `doc?.CachedAnalysis` is used as fallback in CodeActionHandler — verified at line 48 of CodeActionHandler.cs ✓
- All file paths verified to exist
- All class names verified against codebase grep
- `InternalsVisibleTo("Sharpy.Lsp.Tests")` confirmed present in `Sharpy.Lsp.csproj`
- `context.CompilerApi` confirmed unused by any provider (grep returned 0 matches)
