<!-- Verified by /verify-plan on 2026-03-22 -->
<!-- Verification result: PASS WITH CORRECTIONS -->

# Audit 2026-03-22: Implementation Plan

## Context

The [2026-03-22 compiler health audit](docs/audits/audit-2026-03-22.md) found zero critical issues but identified 10 warnings (W1–W10) and 12 opportunities (O1–O12) across architecture, correctness, testing, LSP, and future readiness. This plan addresses the P0 and P1 action items: thread safety for parallel builds, dead code removal, integer predicate consolidation, and test coverage gaps.

P2/P3 items (large refactors like splitting `CompileInternal` or `GenerateCall`, moving `DiagnosticExplanations` to resources) are deferred — they're low-risk and benefit from dedicated planning.

## Current State

- **9,758/9,759 tests passing** (1 intentional skip), 12 skipped fixtures (all tracked)
- **All 253 diagnostic codes active**, 11 TODOs all with GitHub issues
- **W2 partially tracked** via TODO(#414) — 3 duplicated generic inference helpers, but the 4 integer predicates are not yet tracked
- **W3, W4** — dead code with no GitHub issues
- **W10** — SemanticInfo has 3 `HashSet` fields not thread-safe, blocking parallel builds
- **O6–O9** — Shared (0.25 ratio), Services (0.39 ratio) components undertested; struct fixtures low (4 in `/structs/`)

## Design Decisions

1. **Integer predicate consolidation (W2):** Keep two tiers — `PrimitiveCatalog.IsInteger` as the single source of truth for "is any integer type" (CLR-aware via NumericKind), and add `PrimitiveCatalog.IsSharpy Integer` for the narrow int/long check. Remove `TypeUtils.IsInteger`, `TypeChecker.IsIntType`, and `RoslynEmitter.IsIntegerSemanticType` — they all delegate to PrimitiveCatalog.

2. **Thread safety (W10):** Replace the 3 `HashSet<T>` fields in SemanticInfo with `ConcurrentDictionary<T, byte>` (the established pattern in the same file for concurrent collections). This is minimal-risk since `ConcurrentDictionary` supports the same `TryAdd`/`Contains` patterns.

3. **Dead code (W3, W4):** Remove outright — the `ToPascalCase` in `Compiler.cs` is unused and duplicates `NameMangler`, and `_logger` in `SemanticBinding` has an explicit comment saying it's unused after the switch to exceptions.

4. **Testing (O6–O9):** Focus on `NameMangler` (complex logic, 0 dedicated tests for edge cases) and expand struct/import fixtures since those are the weakest coverage areas per the audit.

---

## Implementation

### Phase 1: Thread Safety for Parallel Builds (P0 — W10)

**Goal:** Make SemanticInfo safe for concurrent access, unblocking parallel builds.

#### Tasks

1. **Replace HashSet fields with ConcurrentDictionary in SemanticInfo** — `src/Sharpy.Compiler/Semantic/SemanticInfo.cs`
   - Replace `HashSet<FunctionDef> _generatorFunctions` (line 61) with `ConcurrentDictionary<FunctionDef, byte>`
   - Replace `HashSet<Expression> _eventAccessNodes` (line 65) with `ConcurrentDictionary<Expression, byte>`
   - Replace `HashSet<Expression> _errorRecoveryNodes` (line 76-77) with `ConcurrentDictionary<Expression, byte>`
   - Update all call sites: `.Add(x)` → `.TryAdd(x, 0)`, `.Contains(x)` → `.ContainsKey(x)`, `.Count` stays the same
   - Update the `[NotThreadSafe]` attribute to remove the HashSet caveat (or change to `[ThreadSafe]` if all fields are now concurrent)
   - Acceptance: All 9,758+ tests pass; `SemanticInfo` no longer has non-thread-safe mutable collections
   - Commit: `fix: replace SemanticInfo HashSet fields with ConcurrentDictionary for thread safety`

### Phase 2: Dead Code Removal (P1 — W3, W4)

**Goal:** Remove identified dead code artifacts.

#### Tasks

2. **Remove dead `ToPascalCase` from Compiler.cs** — `src/Sharpy.Compiler/Compiler.cs`
   - Delete the `ToPascalCase` method at lines 672–704
   - Verify no callers exist (confirmed: method defined but never called)
   - Acceptance: Build succeeds, all tests pass
   - Commit: `chore: remove dead ToPascalCase from Compiler.cs (duplicates NameMangler)`

3. **Remove dead `_logger` field from SemanticBinding** — `src/Sharpy.Compiler/Semantic/SemanticBinding.cs`
   - Delete the `_logger` field (lines 48–50) and its pragma suppression
   - Remove the `logger` parameter from the constructor and update all call sites passing it
   - Acceptance: Build succeeds, all tests pass
   - Commit: `chore: remove dead _logger field from SemanticBinding`

### Phase 3: Integer Predicate Consolidation (P1 — W2)

**Goal:** Single source of truth for "is integer type" checks, eliminating divergence risk.

#### Tasks

4. **Add `IsSharpy Integer` to PrimitiveCatalog** — `src/Sharpy.Compiler/Semantic/Registry/PrimitiveCatalog.cs`
   - Add `public static bool IsSharpyInteger(SemanticType type)` that returns true only for `SemanticType.Int` and `SemanticType.Long` (the Sharpy language integer types, not CLR interop types)
   - This parallels the existing `IsInteger` which is CLR-aware (includes byte, short, etc.)
   - Acceptance: Method exists, unit test verifies both `Int` and `Long` return true
   - Commit: `refactor: add PrimitiveCatalog.IsSharpyInteger for language-level integer check`

5. **Replace TypeChecker.IsIntType with PrimitiveCatalog.IsSharpyInteger** — `src/Sharpy.Compiler/Semantic/TypeChecker.Utilities.cs`
   - Delete `private static bool IsIntType(SemanticType type)` at lines 931–934
   - Replace all call sites within TypeChecker to use `PrimitiveCatalog.IsSharpyInteger`
   - Acceptance: Build succeeds, all tests pass
   - Commit: `refactor: replace TypeChecker.IsIntType with PrimitiveCatalog.IsSharpyInteger`

6. **Replace RoslynEmitter.IsIntegerSemanticType with PrimitiveCatalog.IsSharpyInteger** — `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Operators.cs`
   - Delete `private static bool IsIntegerSemanticType(SemanticType? type)` at lines 890–893
   - Replace all call sites within RoslynEmitter.Operators to use `PrimitiveCatalog.IsSharpyInteger` (handle nullable by adding `type != null &&` guard or `type is not null &&`)
   - Acceptance: Build succeeds, all tests pass
   - Commit: `refactor: replace RoslynEmitter.IsIntegerSemanticType with PrimitiveCatalog.IsSharpyInteger`

7. **Remove TypeUtils.IsInteger, delegate to PrimitiveCatalog** — `src/Sharpy.Compiler/Semantic/TypeUtils.cs`
   - Replace the body of `TypeUtils.IsInteger` (lines 42–56) to delegate to `PrimitiveCatalog.IsInteger` (which already handles CLR types via NumericKind) [CORRECTED: method spans lines 42–56, not 42–54]
   - Alternatively, if callers only need the CLR-aware check, just inline `PrimitiveCatalog.IsInteger` and remove `TypeUtils.IsInteger`
   - Check all callers via `find_referencing_symbols` to determine which flavor they need
   - Acceptance: Build succeeds, all tests pass, only PrimitiveCatalog has integer type logic
   - Commit: `refactor: consolidate TypeUtils.IsInteger to delegate to PrimitiveCatalog`

### Phase 4: Test Coverage — Shared Components (P1 — O6)

**Goal:** Improve test coverage for Shared/ components (currently 0.25 ratio).

#### Tasks

8. **Expand NameMangler edge case tests** — `src/Sharpy.Compiler.Tests/CodeGen/NameManglerTests.cs` (existing file) [CORRECTED: file already exists at CodeGen/NameManglerTests.cs, not Shared/. It already has ~200 lines covering: PascalCase (snake_case, empty string, leading underscores, dunder passthroughs, C# keywords), CamelCase (snake_case, keywords, escaping), ConstantCase, and literal names. Focus new tests on gaps only.]
   - Test `ToPascalCase` with: trailing underscores, screaming snake case (ALL_CAPS), already-PascalCase input, names starting with digits, consecutive underscores
   - Test `ToSnakeCase` with: PascalCase, camelCase, acronyms (HTTPResponse → http_response or similar), single word
   - Test dunder-to-operator mapping: `__add__` → `op_Addition`, `__str__` → `ToString`, etc. (DunderMapping coverage)
   - Acceptance: All new tests pass, covers gaps not in existing test file
   - Commit: `test: expand NameMangler edge case tests (O6)`

9. **Expand CSharpKeywords tests** — `src/Sharpy.Compiler.Tests/Shared/CSharpKeywordsTests.cs`
   - Review existing tests, add coverage for: contextual keywords (`async`, `await`, `var`, `dynamic`), escaped identifiers (`@class`), non-keyword inputs returning unchanged
   - Acceptance: New tests pass
   - Commit: `test: expand CSharpKeywords test coverage (O6)`

### Phase 5: Test Coverage — Module Import Fixtures (P1 — O8)

**Goal:** Expand module import test fixtures beyond the few that exist.

#### Tasks

10. **Add circular import detection test** — `src/Sharpy.Compiler.Tests/Integration/TestFixtures/`
    - Create multi-file test: `circular_import/main.spy` imports `a.spy` which imports `b.spy` which imports `a.spy`
    - Add `main.error` expecting circular import diagnostic
    - Acceptance: Test passes, circular import is detected and reported
    - Commit: `test: add circular import detection fixture (O8)`

11. **Add re-export / transitive import test** — `src/Sharpy.Compiler.Tests/Integration/TestFixtures/`
    - Create multi-file test: `main.spy` imports from `lib.spy`, which re-exports symbols from `utils.spy`
    - Add `main.expected` with expected output
    - Acceptance: Test passes
    - Commit: `test: add transitive import fixture (O8)`

12. **Add wildcard import test** — `src/Sharpy.Compiler.Tests/Integration/TestFixtures/`
    - Create `from module import *` test if not already covered
    - Acceptance: Test passes or produces expected error
    - Commit: `test: add wildcard import fixture (O8)`

### Phase 6: Test Coverage — Struct Fixtures (P2 — O9)

**Goal:** Expand struct test fixtures (currently 4 in `/structs/`).

#### Tasks

13. **Add struct equality semantics test** — `src/Sharpy.Compiler.Tests/Integration/TestFixtures/structs/`
    - Test value equality: two structs with same fields are equal
    - Test `__eq__` override on structs
    - Commit: `test: add struct equality semantics fixture (O9)`

14. **Add struct copy/mutation test** — `src/Sharpy.Compiler.Tests/Integration/TestFixtures/structs/`
    - Test that assigning struct to new variable creates a copy (value semantics)
    - Mutating copy doesn't affect original
    - Commit: `test: add struct copy/mutation fixture (O9)`

15. **Add struct with methods test** — `src/Sharpy.Compiler.Tests/Integration/TestFixtures/structs/`
    - Test struct with instance methods, static methods, properties
    - Commit: `test: add struct with methods fixture (O9)`

16. **Add struct error cases** — `src/Sharpy.Compiler.Tests/Integration/TestFixtures/structs/`
    - Test struct inheritance (should error — structs can't inherit)
    - Test struct missing field initialization in constructor (should error)
    - Commit: `test: add struct error case fixtures (O9)`

### Phase 7: Test Coverage — Services Layer (P1 — O7)

**Goal:** Improve test coverage for Services layer (currently 0.39 ratio).

#### Tasks

17. **Expand CompilerServicesBuilder tests** — `src/Sharpy.Compiler.Tests/Services/CompilerServicesTests.cs` (existing file) [CORRECTED: CompilerServicesBuilder is already extensively tested in CompilerServicesTests.cs and CompilerServicesIntegrationTests.cs (~15+ usages). Also, the builder has no `WithModuleSearchPaths` method — its actual fluent API is: WithConfiguration, WithLogger, WithSymbolTable, WithSemanticInfo, WithTypeResolver, WithClrCache, Build, CreateForTesting.]
    - Review existing test coverage and identify gaps in builder API testing
    - Test builder fluent API gaps: WithConfiguration, WithTypeResolver, WithClrCache
    - Test error cases: calling Build without required components (SymbolTable, SemanticInfo)
    - Test default values when optional builder methods not called
    - Acceptance: New tests pass
    - Commit: `test: expand CompilerServicesBuilder test coverage (O7)`

---

## Testing Strategy

- **Thread safety (Phase 1):** Run full test suite to verify no regressions from HashSet → ConcurrentDictionary change. The semantic behavior is identical; only the thread-safety guarantees differ.
- **Dead code removal (Phase 2):** Build + full test suite. These are pure deletions of unused code.
- **Integer consolidation (Phase 3):** Run full test suite after each task. Pay special attention to codegen tests involving integer arithmetic, bitwise operations, and type inference.
- **New tests (Phases 4–7):** Each new test file/fixture should pass independently. Run with `--filter` to verify, then full suite.

## Issues to Close

No existing GitHub issues are directly closed by this plan. However:
- **#414** (generic type inference consolidation) is tangentially related to Phase 3 — the integer predicate consolidation follows the same pattern but is a separate concern.

## GitHub Issues to Create

The following items from the audit should get GitHub issues for tracking (Phase 3 tasks reference them):
- W1: "Move validation methods from TypeChecker.Utilities to ValidationPipeline" (deferred to P2)
- W10: "SemanticInfo HashSet fields not thread-safe" (addressed in Phase 1)

## Assumptions

1. `ConcurrentDictionary<T, byte>` is an acceptable replacement for `HashSet<T>` — the SemanticInfo file already uses 8 ConcurrentDictionary fields for other data.
2. `PrimitiveCatalog` is the right home for integer type predicates — it already owns `NumericKind` and `IsInteger`.
3. Removing `_logger` from `SemanticBinding` constructor is safe — the comment explicitly says it's unused after the switch to exceptions.
4. The 4 integer predicates genuinely diverge on semantics (CLR-aware vs Sharpy-only) — we preserve both levels via `IsInteger` (CLR) and `IsSharpyInteger` (language).

## Verification Summary

**Result:** PASS WITH CORRECTIONS
**Verified on:** 2026-03-22
**Plan file:** `~/.claude/plans/plan-1bf54e.md`

### Corrections Made

1. **Task 7 — TypeUtils.IsInteger line range**: Changed "lines 42–54" → "lines 42–56". The method body including the closing brace and `return false;` extends to line 56.

2. **Task 8 — NameMangler test file path and scope**: Changed from creating new file at `Shared/NameManglerTests.cs` to expanding existing file at `CodeGen/NameManglerTests.cs`. The existing file (~200 lines) already covers: PascalCase (snake_case, empty string, leading underscores, dunder passthroughs, C# keywords), CamelCase, ConstantCase, and literal names. Adjusted task to focus on truly missing edge cases only.

3. **Task 17 — CompilerServicesBuilder test path and API**: Changed from creating new `CompilerServicesBuilderTests.cs` to expanding existing `CompilerServicesTests.cs`. The builder is already tested in ~15+ usages across two existing test files. Also corrected the API: `WithModuleSearchPaths` does not exist — the actual methods are `WithConfiguration`, `WithLogger`, `WithSymbolTable`, `WithSemanticInfo`, `WithTypeResolver`, `WithClrCache`, `Build`, `CreateForTesting`.

### Warnings

1. **SemanticInfo has a 4th non-thread-safe mutable field**: `Dictionary<Symbol, List<SymbolReference>> _symbolReferences` (line 89) is a plain `Dictionary`, not a `ConcurrentDictionary`. The plan correctly targets the 3 `HashSet` fields but should note this 4th field as a known remaining concern (it's documented as "single-threaded write access only" so may be acceptable to leave as-is).

2. **SemanticInfo `[NotThreadSafe]` attribute text is incomplete**: The attribute at line 20 mentions `_generatorFunctions` and `_eventAccessNodes` but omits `_errorRecoveryNodes`. When updating the attribute after Phase 1, ensure the description accurately reflects the change (removing all three HashSet fields from the caveat).

3. **Task 12 — Wildcard import**: No existing `from X import *` fixture was found, but verify whether Sharpy actually supports wildcard imports before creating the test. If unsupported, the test should be an `.error` case, not an `.expected` case.

4. **Phase 1 design note**: The plan's claim that "SemanticInfo already uses 8 ConcurrentDictionary fields" is correct (verified: `_expressionTypes`, `_identifierSymbols`, `_callTargets`, `_typeAnnotations`, `_narrowedExpressionTypes`, `_inferredTypeArguments`, `_memberAccessResolutions`, `_patternUnionCases`, `_contextManagerKinds` — actually 9+).

### Missing Steps Added

None — the plan is complete for its stated scope. The deferred items (P2/P3) are appropriately excluded.

### Unchecked Claims

1. **Test count "9,758/9,759"** — not independently verified (would require running the full test suite). The claim is plausible given the memory states 8,747 tests as of 2026-03-10 with growth expected.

2. **"All 253 diagnostic codes active"** — not independently verified (would require running the audit tool or counting DiagnosticCodes.cs entries).

3. **O6–O9 test ratios** (0.25, 0.39) — these come from the audit and were not re-computed.
