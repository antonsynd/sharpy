<!-- Verified by /verify-plan on 2026-03-16 -->
<!-- Verification result: PASS WITH CORRECTIONS -->

# Core & LSP Cleanup: Issues #382–#387

## Context

Six issues identified during core library cleanup verification and LSP E2E test hardening. They span two components:

- **Sharpy.Core** (#382, #383, #384): Visibility, exception consistency, documentation
- **Sharpy.Lsp** (#385, #386, #387): Single-file analysis, cross-file crash, text extraction bug

Issues: [#382](https://github.com/antonsynd/sharpy/issues/382), [#383](https://github.com/antonsynd/sharpy/issues/383), [#384](https://github.com/antonsynd/sharpy/issues/384), [#385](https://github.com/antonsynd/sharpy/issues/385), [#386](https://github.com/antonsynd/sharpy/issues/386), [#387](https://github.com/antonsynd/sharpy/issues/387)

## Current State

- `PrintWithOptions` is `public` but is internal infrastructure called only by `Print()` — not referenced by the emitter or any external assembly
- Four string methods (`Split`, `Rsplit`, `Partition`, `Rpartition`) throw `ArgumentNullException` instead of Sharpy's `TypeError.ArgNone()` — inconsistent with stdlib conventions
- `Math.Ceil`/`Math.Floor` return `double` (Axiom 1: .NET), but XML docs don't note the deviation from Python's `int` return
- `ImplementInterfaceProvider` returns empty in single-file LSP mode — interface members aren't resolved
- `ImplementationHandler` crashes (broken pipe) on cross-file `textDocument/implementation` requests
- `ConvertFormsProvider.ExtractSourceText` has an off-by-one error — ColumnEnd is 1-based exclusive but used as 0-based exclusive, extracting one extra character (the trailing colon in `match x:`)

## Design Decisions

1. **#382 — `internal` is safe**: `PrintWithOptions` is NOT called by generated code. The emitter generates `Sharpy.Builtins.Print(args)` for all `print()` calls; `PrintWithOptions` is only called internally by `Print()`. `InternalsVisibleTo("Sharpy.Core.Tests")` already exists in the csproj, so test access is preserved.

2. **#383 — Python exception conventions**: Sharpy stdlib consistently uses Python-style exceptions (`TypeError`, `ValueError`). `TypeError.ArgNone(method, arg)` exists and is the standard pattern. Ref: `src/Sharpy.Core/Builtins/Exceptions.cs:32`.

3. **#384 — Axiom 1 documentation**: The `double` return is correct by design (Axiom 1: .NET > Python). Documentation should be explicit about the deviation so users aren't surprised.

4. **#387 — Coordinate system fix**: The parser sets `ColumnEnd = token.Column + token.Value.Length` where `Column` is 1-based. This makes `ColumnEnd` a 1-based exclusive end. `ExtractSourceText` converts `ColumnStart` to 0-based (`ColumnStart - 1`) but forgets to do the same for `ColumnEnd`. Fix: `node.ColumnEnd - 1` in both single-line and multi-line branches.

5. **#385/#386 — LSP robustness**: Both are LSP bugs that require investigation into how single-file analysis populates `TypeSymbol.Interfaces` and how `TypeHierarchyIndex.Build` handles cross-file symbols. Both have skipped E2E tests ready to unskip.

## Implementation

### Phase 1: Core Library Fixes (#382, #383, #384)

**Goal:** Fix Sharpy.Core visibility, exception consistency, and documentation.

#### Tasks

1. **Make `PrintWithOptions` internal** — `src/Sharpy.Core/Builtins/Print.cs:36`
   - Change `public static void PrintWithOptions` to `internal static void PrintWithOptions`
   - No `InternalsVisibleTo` changes needed — `Sharpy.Core.Tests` is already listed
   - Verify build passes and `PrintTests` still work
   - Commit: `refactor(core): make PrintWithOptions internal`

2. **Replace `ArgumentNullException` with `TypeError.ArgNone` in string split methods** — `src/Sharpy.Core/Partial.String/StringExtensions.Split.cs`
   - Line 162-164: `Split` — replace `throw new ArgumentNullException(nameof(sep))` with `throw TypeError.ArgNone("split", "sep")`
   - Line 222-224: `Rsplit` — replace with `throw TypeError.ArgNone("rsplit", "sep")`
   - Line 278-280: `Partition` — replace with `throw TypeError.ArgNone("partition", "sep")`
   - Line 310-312: `Rpartition` — replace with `throw TypeError.ArgNone("rpartition", "sep")`
   - Also add exception doc comments: add `<exception cref="TypeError">` for null sep in all four methods (no existing `ArgumentNullException` doc comments exist to change) [CORRECTED: there are no `<exception cref="ArgumentNullException">` doc comments in the file; the existing `<exception>` comments only reference `ValueError` for empty separator]
   - Verify Python behavior: `python3 -c "'a.b'.split(None)"` — Python's `split(None)` means whitespace split, but `split(sep=None)` where `None` is explicitly passed should raise `TypeError` in the typed Sharpy context
   - Commit: `fix(core): use TypeError instead of ArgumentNullException in string split methods`

3. **Add `<remarks>` to `Math.Ceil` and `Math.Floor`** — `src/Sharpy.Core/Math/Math.cs`
   - Add after the `</example>` tag for `Ceil` (before line 48):
     ```xml
     /// <remarks>
     /// Unlike Python's <c>math.ceil</c> which returns <c>int</c>,
     /// Sharpy returns <c>double</c> to match .NET's <see cref="System.Math.Ceiling(double)"/>
     /// (Axiom 1: .NET compatibility). Cast to int if needed: <c>int(math.ceil(x))</c>.
     /// </remarks>
     ```
   - Add the same pattern for `Floor` (before line 61), referencing `math.floor` and `System.Math.Floor`
   - Commit: `docs(core): note Math.Ceil/Floor return type deviation from Python`

### Phase 2: LSP Text Extraction Fix (#387)

**Goal:** Fix the off-by-one in `ExtractSourceText` that causes the trailing colon bug.

#### Tasks

4. **Fix `ExtractSourceText` coordinate conversion** — `src/Sharpy.Lsp/Refactoring/ConvertFormsProvider.cs:548-588`
   - **Root cause**: `ColumnEnd` is 1-based exclusive (parser sets it as `token.Column + token.Value.Length` where `Column` is 1-based). `ExtractSourceText` converts `ColumnStart` to 0-based (`ColumnStart - 1`) but uses `ColumnEnd` raw, making it off by one.
   - Single-line branch (line 559): Change `var end = System.Math.Min(line.Length, node.ColumnEnd);` to `var end = System.Math.Min(line.Length, node.ColumnEnd - 1);`
   - Multi-line branch (line 577): Change `var end = System.Math.Min(line.Length, node.ColumnEnd);` to `var end = System.Math.Min(line.Length, node.ColumnEnd - 1);`
   - Commit: `fix(lsp): fix off-by-one in ExtractSourceText ColumnEnd conversion`

5. **Update test assertion to verify exact output** — `src/Sharpy.Lsp.Tests/Refactoring/ConvertFormsProviderTests.cs:286-291`
   - Remove the workaround comment block (lines 286-289)
   - Replace loose assertions with exact text match: `newText.Should().Contain("if x == 1:");`
   - Keep `newText.Should().Contain("else:");` and `newText.Should().NotContain("elif");`
   - Commit: `test(lsp): verify exact match-to-if output now that #387 is fixed`

### Phase 3: LSP Single-File Interface Analysis (#385)

**Goal:** Make `ImplementInterfaceProvider` work in single-file LSP analysis.

#### Tasks

6. **Investigate single-file interface resolution** — `src/Sharpy.Lsp/Refactoring/ImplementInterfaceProvider.cs`
   - Debug the code path: `analysis.SymbolTable.LookupType(className)` → check if `typeSymbol.Interfaces` is populated → check if `ifaceRef.Definition` is non-null → check if `ifaceDef.Methods` has entries
   - The likely root cause is one of:
     - (a) `typeSymbol.Interfaces` is empty because `MaterializeInheritance()` wasn't called in the LSP analysis pipeline
     - (b) `ifaceRef.Definition` is null because the interface `TypeSymbol` wasn't linked
     - (c) `ifaceDef.Methods` is empty because method resolution didn't run
   - Check how `LanguageService.AnalyzeFileAsync` runs semantic passes — does it call `MaterializeInheritance()` and populate interface method lists?
   - Files to investigate: `src/Sharpy.Lsp/LanguageService.cs`, `src/Sharpy.Compiler/CompilerApi.cs` [CORRECTED: CompilerApi is defined in Sharpy.Compiler, not Sharpy.Lsp]
   - Fix the pipeline to ensure interface members are resolved in single-file mode
   - Commit: `fix(lsp): resolve interface members in single-file analysis for ImplementInterfaceProvider`

7. **Unskip and verify `CodeAction_ImplementInterface_ReturnsAction` test** — `src/Sharpy.Lsp.Tests/E2E/ProtocolTests.cs:664`
   - Remove `Skip = "TODO(#385): ..."` from the `[Fact]` attribute
   - Run the test to verify it passes
   - Commit: `test(lsp): unskip CodeAction_ImplementInterface_ReturnsAction E2E test`

### Phase 4: LSP Cross-File Implementation Crash (#386)

**Goal:** Fix the crash in `textDocument/implementation` for cross-file interfaces.

#### Tasks

8. **Investigate and fix cross-file implementation crash** — `src/Sharpy.Lsp/Handlers/ImplementationHandler.cs`
   - The crash likely occurs in `TypeHierarchyIndex.Build(symbolTable)` (line 164) or in `SymbolLocationHelper.GetSymbolLocation` when the symbol has a `DeclaringFilePath` from a different file
   - Check `src/Sharpy.Lsp/TypeHierarchyIndex.cs` — does `Build()` handle symbols from imported modules correctly? Look for null references on imported type symbols
   - Check `GetBestSymbolTable` (line 145-150) — does `_languageService.ProjectAnalysis` return a valid project-wide symbol table?
   - Add try-catch around the handler's critical path to prevent server crashes. The LSP spec allows returning `null` for failed requests — a crash is never acceptable
   - Wrap the `Handle` method body in a try-catch that logs the error and returns `null` instead of crashing
   - Fix the underlying issue (likely a null reference in TypeHierarchyIndex or SymbolLocationHelper)
   - Commit: `fix(lsp): prevent crash on cross-file textDocument/implementation requests`

9. **Unskip and verify `MultiFile_Implementation_CrossFile` test** — `src/Sharpy.Lsp.Tests/E2E/MultiFileTests.cs:371`
   - Remove `Skip = "TODO(#386): ..."` from the `[Fact]` attribute
   - Run the test to verify it passes
   - Commit: `test(lsp): unskip MultiFile_Implementation_CrossFile E2E test`

## Testing Strategy

### Phase 1 Tests
- Existing `PrintTests` should pass unchanged (InternalsVisibleTo already set for test assembly)
- Add/update tests for null separator → verify `TypeError` is thrown (not `ArgumentNullException`) in `StringExtensionsSplitTests`
- Build with `GenerateDocumentationFile` to verify XML docs are well-formed

### Phase 2 Tests
- The existing `ConvertFormsProviderTests` match-to-if test becomes the verification (update assertions to exact match)
- Verify `ExtractSourceText` doesn't break other callers (for-to-while conversion, if-to-match, etc.) by running all ConvertFormsProvider tests

### Phase 3 Tests
- Unskip `CodeAction_ImplementInterface_ReturnsAction` E2E test
- Consider adding a unit test for single-file interface resolution in `ImplementInterfaceProviderTests`

### Phase 4 Tests
- Unskip `MultiFile_Implementation_CrossFile` E2E test
- Consider a negative test: cross-file implementation where the implementing file has a compile error

### Edge Cases
- **#383**: What about `null` sep in overloads without maxsplit? Check if there are other `Split`/`Rsplit` overloads with the same issue
- **#387**: Verify `ExtractSourceText` with multi-line scrutinee expressions (e.g., `match long_expression\n    .something:`)
- **#385**: Interface with only properties (no methods), empty interface, interface inheritance chains
- **#386**: Three-file chains (interface → abstract class → concrete class across files)

## Issues to Close

- #382 — Make PrintWithOptions internal (closed by Phase 1, Task 1)
- #383 — Use TypeError instead of ArgumentNullException in StringExtensions (closed by Phase 1, Task 2)
- #384 — Note Math.Ceil/Floor return type deviation (closed by Phase 1, Task 3)
- #385 — ImplementInterfaceProvider single-file analysis (closed by Phase 3, Tasks 6-7)
- #386 — LSP crash on cross-file implementation (closed by Phase 4, Tasks 8-9)
- #387 — ConvertFormsProvider trailing colon bug (closed by Phase 2, Tasks 4-5)

## Verification Summary

**Result:** PASS WITH CORRECTIONS
**Verified on:** 2026-03-16
**Plan file:** `docs/implementation_planning/core_lsp_cleanup_382_387.md`

### Corrections Made
1. **Task 2 — Doc comment claim**: Plan said "change `<exception cref="ArgumentNullException">` to `<exception cref="TypeError">`" but there are no `<exception cref="ArgumentNullException">` doc comments in `StringExtensions.Split.cs`. The existing `<exception>` comments only reference `ValueError` for empty separator. Corrected to "add `<exception cref="TypeError">`".
2. **Task 6 — CompilerApi file path**: Plan referenced `src/Sharpy.Lsp/CompilerApi.cs` which does not exist. `CompilerApi` is defined in `src/Sharpy.Compiler/CompilerApi.cs`. Corrected inline.

### Warnings
- None

### Missing Steps Added
- None — all phases and tasks are complete and well-ordered

### Unchecked Claims
- **Python behavior for `split(None)`** (Task 2): The plan suggests verifying `python3 -c "'a.b'.split(None)"` but does not run it. Python's `split(None)` splits on whitespace (it's not an error). The null check in Sharpy's typed context is reasonable but the Python analogy is slightly different — Python `split(None)` is equivalent to `split()`, not a `TypeError`.
