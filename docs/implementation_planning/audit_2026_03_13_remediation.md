<!-- Verified by /verify-plan on 2026-03-13 -->
<!-- Verification result: PASS WITH CORRECTIONS -->

# Audit 2026-03-13 Remediation Plan

## Context

The [2026-03-13 compiler health audit](docs/audits/audit-2026-03-13.md) identified several areas for improvement across architecture, testing, and code organization. This plan addresses all P0 and P1 items, plus select P2 items that are low-risk and high-value.

**Items intentionally excluded:**
- **83 unpaired .spy files** — Research confirmed all are intentional supporting files in multi-file test directories. No action needed.
- **CLI Program.cs split** (P2) — Lower priority, separate plan.
- **FStringEmitter/ComprehensionEmitter extraction** (P2) — Lower priority, separate plan.
- **Project/ test coverage expansion** (P2) — Separate plan.
- **LSP CompletionItem/Resolve, PrepareRename** (P3) — Future work.
- **Trivia attachment for formatter** (P3) — Large effort, separate plan.
- **Per-file SymbolTable isolation** (P3) — Large effort, separate plan.

## Current State

- 9,122 tests passing, 0 failures, 1 skip [CORRECTED: audit says 9,122, not 9,123]
- 19 validators each implement their own AST traversal (~1,000+ lines of duplicated switch/foreach)
- `DependencyGraphBuilder` lives in `Semantic/` but constructs `Project.DependencyGraph` (bidirectional coupling)
- `Shared/` has 15 files but only 3 have tests (NameMangler, DunderNameMapping, NameFormDetector) [CORRECTED: DunderDetector has no dedicated tests; only 3 files tested, not 4]
- 113/244 diagnostic codes unused — no documentation distinguishing reserves from future placeholders [CORRECTED: audit found 113 unused, not 167]
- RoslynEmitter has 3 narrowing-related fields + 9 methods scattered across the class [CORRECTED: 9 methods, not 8 — PushNarrowing, PopNarrowing, IsNarrowed, IsNullableNarrowed, ClearNarrowing, PushIsInstanceNarrowing, PopIsInstanceNarrowing, IsInstanceNarrowed, GetIsInstanceNarrowedType]
- DiagnosticQuickFixProvider has integration tests via CodeActionTests.cs but no dedicated unit tests

## Design Decisions

1. **Validator traversal**: Add a `ValidatingAstWalker` base class that wraps `AstVisitor` with `SemanticContext`, rather than modifying `SemanticValidatorBase` itself. This preserves backward compatibility — validators that don't need traversal keep using `SemanticValidatorBase` directly. Validators opt in by inheriting from `ValidatingAstWalker` instead.

2. **Dependency decoupling**: Introduce `IDependencyRecorder` in `Services/` (where `IDependencyQuery` already lives) and move `DependencyGraphBuilder` to `Project/`. `ImportResolver` takes `IDependencyRecorder` instead of concrete `DependencyGraphBuilder`. This preserves the existing interface-based pattern.

3. **NarrowingState extraction**: Extract to a nested `NarrowingState` class inside `RoslynEmitter.cs` to keep it co-located. The 3 fields and 8 methods form a cohesive unit.

4. **Diagnostic codes**: Document in-place with `#region` blocks and comments in `DiagnosticCodes.cs`. No code changes — purely documentation. Explicitly mark each range as "Reserved", "Allocated (not yet emitted)", or "Active".

## Implementation

### Phase 1: Unit Tests for Shared/ (P0)

**Goal:** Close the highest-priority testing gap identified by the audit.

#### Tasks

1. **Add CSharpKeywords tests** — `src/Sharpy.Compiler.Tests/Shared/CSharpKeywordsTests.cs`
   - Test `EscapeIfNeeded()` returns `@name` for C# keywords, passes through non-keywords
   - Test `IsKeyword()` returns true for all 84+ C# keywords
   - Test edge cases: empty string, already-escaped names, case sensitivity
   - Acceptance: All C# keywords verified, edge cases covered
   - Commit: `test(shared): add CSharpKeywords unit tests`

2. **Add CSharpTypeNames tests** — `src/Sharpy.Compiler.Tests/Shared/CSharpTypeNamesTests.cs`
   - Test `FromSharpyName()` maps "list" → "Sharpy.List", "dict" → "Sharpy.Dict", "set" → "Sharpy.Set", etc.
   - Test constants: `SharpyList`, `SharpyDict`, `SharpySet`, `SharpyOptional`, `SharpyResult`
   - Test unknown names return null
   - Acceptance: All 5 type mappings and constants verified
   - Commit: `test(shared): add CSharpTypeNames unit tests`

3. **Add AstHelper tests** — `src/Sharpy.Compiler.Tests/Shared/AstHelperTests.cs`
   - Test `TryGetConstantIntIndex()` with integer literals, negative literals, non-integer expressions
   - Test `ExtractNarrowingKey()` with simple names, attribute access chains, unsupported expressions
   - Acceptance: Both methods tested with positive and negative cases
   - Commit: `test(shared): add AstHelper unit tests`

4. **Add StatementWalker tests** — `src/Sharpy.Compiler.Tests/Shared/StatementWalkerTests.cs`
   - Test `Any()` finds matching statements in nested bodies (if/while/for/try)
   - Test `FirstOrDefault<T>()` returns first match or null
   - Test empty bodies, deeply nested statements
   - Acceptance: Both methods tested with nested control flow
   - Commit: `test(shared): add StatementWalker unit tests`

### Phase 2: Semantic-Project Decoupling (P1)

**Goal:** Break the bidirectional coupling between Semantic and Project layers.

#### Tasks

1. **Create IDependencyRecorder interface** — `src/Sharpy.Compiler/Services/IDependencyRecorder.cs`
   - Methods: `AddFile(string filePath)`, `AddDependency(string fromFile, string toFile)`, `SetFileHash(string filePath, string contentHash)`
   - Place in `Services/` alongside existing `IDependencyQuery`
   - Acceptance: Interface compiles, no consumers yet
   - Commit: `refactor(services): add IDependencyRecorder interface`

2. **Move DependencyGraphBuilder to Project/ and implement IDependencyRecorder** — `src/Sharpy.Compiler/Project/DependencyGraphBuilder.cs`
   - Move file from `Semantic/` to `Project/`
   - Update namespace from `Sharpy.Compiler.Semantic` to `Sharpy.Compiler.Project`
   - Implement `IDependencyRecorder` on `DependencyGraphBuilder`
   - Acceptance: File moved, namespace updated, builds clean
   - Commit: `refactor(project): move DependencyGraphBuilder to Project layer`

3. **Update ImportResolver to use IDependencyRecorder** — `src/Sharpy.Compiler/Semantic/ImportResolver.cs`
   - Change `SetDependencyGraphBuilder(DependencyGraphBuilder)` to `SetDependencyRecorder(IDependencyRecorder)`
   - Update field type from `DependencyGraphBuilder?` to `IDependencyRecorder?`
   - Update call sites: `_graphBuilder.AddDependency()` → `_recorder.AddDependency()`
   - Acceptance: ImportResolver no longer references `Project` namespace
   - Commit: `refactor(semantic): decouple ImportResolver from DependencyGraphBuilder`

4. **Update ProjectCompiler to wire IDependencyRecorder** — `src/Sharpy.Compiler/Project/ProjectCompiler.Initialization.cs`
   - Change `ImportResolver.SetDependencyGraphBuilder()` → `ImportResolver.SetDependencyRecorder()`
   - No functional change — same object, accessed via interface
   - Acceptance: All tests pass, no behavior change
   - Commit: `refactor(project): wire IDependencyRecorder in ProjectCompiler`

### Phase 3: Diagnostic Code Documentation (P1)

**Goal:** Document the 113 unused diagnostic codes so future developers know which are intentionally reserved vs. allocated for future use. [CORRECTED: 113 per audit, not 167]

#### Tasks

1. **Add region markers and status comments to DiagnosticCodes.cs** — `src/Sharpy.Compiler/Diagnostics/DiagnosticCodes.cs`
   - Add `#region` blocks for each phase: Lexer, Parser, Semantic, Validation, CodeGen, Infrastructure, Info
   - Mark each code with a trailing comment: `// Active`, `// Reserved`, or `// Allocated (not yet emitted)`
   - For the 6 explicitly reserved codes (SPY0134, SPY0137, SPY0289, SPY0379, SPY0424, SPY0521), keep existing "reserved" comments [CORRECTED: 6 codes listed, not 5]
   - Acceptance: Every code has a status comment; `#region` blocks organize by phase
   - Commit: `docs(diagnostics): document status of all 244 diagnostic codes`

### Phase 4: RoslynEmitter NarrowingState Extraction (P1)

**Goal:** Group the 3 narrowing fields and 9 methods into a cohesive inner class, reducing RoslynEmitter's field count from 27 to 25. [CORRECTED: 9 methods not 8; actual instance field count is 27 not 46]

#### Tasks

1. **Extract NarrowingState inner class** — `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`
   - Create `private class NarrowingState` inside `RoslynEmitter`
   - Move fields: `_narrowedOptionals`, `_isNullableNarrowing`, `_isInstanceNarrowed`
   - Move methods: `PushNarrowing`, `PopNarrowing`, `IsNarrowed`, `IsNullableNarrowed`, `PushIsInstanceNarrowing`, `PopIsInstanceNarrowing`, `IsInstanceNarrowed`, `GetIsInstanceNarrowedType`, `ClearNarrowing`
   - Add `Reset()` method (called from `ResetMethodScope()`)
   - Replace field access sites in `RoslynEmitter.Expressions.cs`, `.Expressions.Access.cs`, `.Statements.cs`, `.ModuleClass.cs` with `_narrowing.XXX()`
   - Acceptance: All tests pass, no behavior change, field count reduced
   - Commit: `refactor(codegen): extract NarrowingState from RoslynEmitter`

### Phase 5: DiagnosticQuickFixProvider Tests (P1)

**Goal:** Add dedicated unit tests for the only untested LSP refactoring provider.

#### Tasks

1. **Add DiagnosticQuickFixProviderTests** — `src/Sharpy.Lsp.Tests/Refactoring/DiagnosticQuickFixProviderTests.cs`
   - Test "Remove unused import" action for SPY0452 diagnostic
   - Test "Prefix with '_'" action for SPY0451 diagnostic
   - Test "Rename to 'X'" action for SPY0453 diagnostic with `suggestedName` in data
   - Test null/missing data handling (SPY0453 with no `suggestedName`)
   - Test diagnostic outside supported codes returns no actions
   - Follow pattern from existing provider tests (e.g., `ExtractVariableProviderTests`)
   - Acceptance: All 3 quick fix types tested with positive and negative cases
   - Commit: `test(lsp): add DiagnosticQuickFixProvider unit tests`

### Phase 6: Validator Traversal Refactor (P0)

**Goal:** Eliminate ~1,000+ lines of duplicated AST traversal across validators by providing a reusable walker base class. This is the largest change, so it comes last to build on the testing foundation from earlier phases.

#### Tasks

1. **Create ValidatingAstWalker base class** — `src/Sharpy.Compiler/Semantic/Validation/ValidatingAstWalker.cs`
   - Inherit from `SemanticValidatorBase`
   - Compose an inner `AstVisitor` that dispatches to overridable `VisitXxx()` methods
   - Implement `Validate(Module, SemanticContext)` to walk the module body automatically
   - Provide `SemanticContext` access to subclasses
   - Validators override only the `VisitXxx()` methods they care about (e.g., `VisitFunctionDef`, `VisitClassDef`)
   - Default behavior: continue traversal via `base.VisitXxx()` (delegates to `DefaultVisit`)
   - Acceptance: Base class compiles, has no consumers yet
   - Commit: `refactor(validation): add ValidatingAstWalker base class`

2. **Migrate UnusedImportValidator to ValidatingAstWalker** — `src/Sharpy.Compiler/Semantic/Validation/UnusedImportValidator.cs`
   - Replace manual `CollectReferencesFromStatement()` switch with `VisitXxx()` overrides
   - Replace `CollectReferencesFromExpression()` switch with expression visitor overrides
   - Acceptance: All existing tests pass, no behavior change
   - Commit: `refactor(validation): migrate UnusedImportValidator to ValidatingAstWalker`

3. **Migrate NamingConventionValidator to ValidatingAstWalker** — `src/Sharpy.Compiler/Semantic/Validation/NamingConventionValidator.cs`
   - Replace `ValidateStatement()` and `ValidateBody()` switches with visitor overrides
   - Keep validation logic unchanged, only replace traversal
   - Acceptance: All existing tests pass, no behavior change
   - Commit: `refactor(validation): migrate NamingConventionValidator to ValidatingAstWalker`

4. **Migrate AccessValidator to ValidatingAstWalker** — `src/Sharpy.Compiler/Semantic/Validation/AccessValidator.cs`
   - Replace `ValidateTopLevelStatement()` and `ValidateStatement()` switches
   - Replace `ValidateExpression()` switch with expression visitor overrides
   - Acceptance: All existing tests pass, no behavior change
   - Commit: `refactor(validation): migrate AccessValidator to ValidatingAstWalker`

5. **Migrate UnusedVariableValidator to ValidatingAstWalker** — `src/Sharpy.Compiler/Semantic/Validation/UnusedVariableValidator.cs`
   - Replace `CollectFromStatement()` switch with visitor overrides
   - Replace `CollectReadsFromExpression()` with expression visitor
   - Acceptance: All existing tests pass, no behavior change
   - Commit: `refactor(validation): migrate UnusedVariableValidator to ValidatingAstWalker`

6. **Migrate ControlFlowValidator to ValidatingAstWalker** — `src/Sharpy.Compiler/Semantic/Validation/ControlFlowValidator.cs`
   - Replace `ValidateTopLevelStatement()` and `ValidateNestedFunctions()` switches
   - Acceptance: All existing tests pass, no behavior change
   - Commit: `refactor(validation): migrate ControlFlowValidator to ValidatingAstWalker`

7. **Migrate remaining validators** — Apply to remaining validators that have manual traversal
   - Candidates: `DunderInvocationValidator`, `DecoratorValidator`, `SignatureValidator`, `GeneratorValidator`, `ModuleLevelValidator`, `DefaultParameterValidator`, `PropertyValidator`, `EventValidator`, `VarianceValidator`
   - For each: replace manual traversal with visitor overrides, run tests
   - Skip validators that only inspect top-level statements (no nested traversal needed)
   - Acceptance: All tests pass after each migration
   - Commit: `refactor(validation): migrate remaining validators to ValidatingAstWalker`

8. **Add ValidatingAstWalker tests** — `src/Sharpy.Compiler.Tests/Semantic/Validation/ValidatingAstWalkerTests.cs`
   - Test that walker visits all statement types in nested bodies
   - Test that overriding a `VisitXxx()` method receives the correct node
   - Test that `base.VisitXxx()` continues traversal into children
   - Test that not overriding a method still traverses children (default behavior)
   - Acceptance: Walker behavior verified independently of validators
   - Commit: `test(validation): add ValidatingAstWalker unit tests`

## Testing Strategy

- **Phase 1**: Creates ~4 new test files covering CSharpKeywords, CSharpTypeNames, AstHelper, StatementWalker
- **Phase 2**: No new tests needed — existing tests verify no behavior change
- **Phase 3**: No tests — documentation only
- **Phase 4**: Existing codegen tests verify no behavior change
- **Phase 5**: 1 new test file for DiagnosticQuickFixProvider
- **Phase 6**: 1 new test file for ValidatingAstWalker + all existing validator tests serve as regression tests

**Regression strategy:** After each phase, run full test suite (`dotnet test`) to ensure no regressions. Phases are independently committable and can be merged separately.

**Edge cases to cover in Shared/ tests:**
- CSharpKeywords: contextual keywords (`var`, `dynamic`, `async`), non-keywords that look like keywords
- CSharpTypeNames: case sensitivity, partial matches, empty strings
- AstHelper: deeply nested attribute access, non-constant expressions, edge integer values
- StatementWalker: empty bodies, single-statement bodies, try/except/finally nesting

## Issues to Close

No GitHub issues are directly referenced by this plan. The audit findings should be converted to GitHub issues before implementation begins. Suggested issues:

- "Add unit tests for Shared/ utilities (CSharpKeywords, CSharpTypeNames, AstHelper, StatementWalker)"
- "Decouple Semantic/DependencyGraphBuilder from Project layer via IDependencyRecorder"
- "Document diagnostic code statuses (reserved vs. allocated vs. active)"
- "Extract NarrowingState from RoslynEmitter"
- "Add DiagnosticQuickFixProvider dedicated unit tests"
- "Add ValidatingAstWalker to eliminate validator traversal duplication"

## Verification Summary

**Result:** PASS WITH CORRECTIONS
**Verified on:** 2026-03-13
**Plan file:** docs/implementation_planning/audit_2026_03_13_remediation.md

### Corrections Made

1. **Test count** (Current State): 9,123 → 9,122. The audit report says 9,122.
2. **Unused diagnostic codes** (Current State + Phase 3): 167/244 → 113/244. The audit found 113 unused codes, not 167.
3. **Narrowing method count** (Current State + Phase 4): 8 → 9. Actual methods: PushNarrowing, PopNarrowing, IsNarrowed, IsNullableNarrowed, ClearNarrowing, PushIsInstanceNarrowing, PopIsInstanceNarrowing, IsInstanceNarrowed, GetIsInstanceNarrowedType.
4. **Reserved code count** (Phase 3): "5 explicitly reserved codes" → 6. The plan lists 6 codes (SPY0134, SPY0137, SPY0289, SPY0379, SPY0424, SPY0521).
5. **RoslynEmitter field count** (Phase 4): 46 → 44 corrected to 27 → 25. Only 27 instance fields exist in RoslynEmitter (verified by grep across all 16 partial files). Extracting 3 narrowing fields and adding 1 `_narrowing` reference = 25.
6. **Shared/ tested files** (Current State): "4 have tests" → 3. DunderDetector has no dedicated tests; only NameMangler, DunderNameMapping, and NameFormDetector have test files.

### Warnings

- **ResetMethodScope missing `_isInstanceNarrowed.Clear()`**: `ResetMethodScope()` (RoslynEmitter.cs:262) clears `_narrowedOptionals` and `_isNullableNarrowing` but does NOT clear `_isInstanceNarrowed`. This may cause isinstance narrowing state to leak between method scopes. The NarrowingState extraction in Phase 4 should include clearing `_isInstanceNarrowed` in its `Reset()` method.
- **SPY0521 is defined, not just reserved**: The plan lists SPY0521 among "explicitly reserved codes", but `DiagnosticCodes.cs` defines it as `TypeReExportNotSupported = "SPY0521"` — it's an allocated constant for future type re-export support, not merely a comment-reserved code like the other 5.

### Missing Steps Added

- None needed. All pipeline phases are covered for each change. Test strategy is sound.

### Verified Claims (Spot-Checked)

- **Shared/ has 15 files** — Confirmed (15 .cs files in `src/Sharpy.Compiler/Shared/`).
- **4 Shared/ files have tests** — Confirmed: NameMangler, DunderNameMapping, NameFormDetector, DunderDetector have tests; CSharpKeywords, CSharpTypeNames, AstHelper, StatementWalker do not.
- **CSharpKeywords methods** — `EscapeIfNeeded()` and `IsKeyword()` confirmed at lines 29, 37.
- **CSharpTypeNames methods/constants** — `FromSharpyName()`, `SharpyList`, `SharpyDict`, `SharpySet`, `SharpyOptional`, `SharpyResult` all confirmed.
- **AstHelper methods** — `TryGetConstantIntIndex()` and `ExtractNarrowingKey()` confirmed at lines 15, 39.
- **StatementWalker methods** — `Any()` and `FirstOrDefault<T>()` confirmed at lines 16, 24.
- **DependencyGraphBuilder in Semantic/** — Confirmed at `src/Sharpy.Compiler/Semantic/DependencyGraphBuilder.cs`.
- **IDependencyQuery in Services/** — Confirmed at `src/Sharpy.Compiler/Services/IDependencyQuery.cs`.
- **ImportResolver.SetDependencyGraphBuilder()** — Confirmed with `DependencyGraphBuilder?` field `_graphBuilder`.
- **ProjectCompiler.Initialization.cs** — Confirmed, calls `ImportResolver.SetDependencyGraphBuilder(GraphBuilder)` at line 29.
- **3 narrowing fields** — `_narrowedOptionals`, `_isNullableNarrowing`, `_isInstanceNarrowed` confirmed in `RoslynEmitter.cs`.
- **Narrowing usage files** — `.Expressions.cs`, `.Expressions.Access.cs`, `.Statements.cs`, `.ModuleClass.cs` confirmed.
- **ResetMethodScope** — Confirmed at line 262, clears narrowing state.
- **19 validators** — Confirmed (19 *Validator.cs files in Semantic/Validation/).
- **SemanticValidatorBase** — Confirmed in `ISemanticValidator.cs` line 41.
- **AstVisitor** — Confirmed at `Parser/Ast/AstVisitor.cs` (abstract class + generic variant).
- **ValidationPipelineFactory** — Confirmed at `Semantic/Validation/ValidationPipelineFactory.cs`.
- **DiagnosticQuickFixProvider** — Confirmed, handles SPY0451 (UnusedVariable), SPY0452 (UnusedImport), SPY0453 (NamingConventionWarning) with `suggestedName` data extraction.
- **CodeActionTests.cs tests DiagnosticQuickFixProvider** — Confirmed.
- **ExtractVariableProviderTests.cs** — Confirmed in `src/Sharpy.Lsp.Tests/Refactoring/`.
- **Validator method names for migration** — All confirmed: UnusedImportValidator (CollectReferencesFromStatement/Expression), NamingConventionValidator (ValidateStatement/ValidateBody), AccessValidator (ValidateTopLevelStatement/ValidateStatement/ValidateExpression), UnusedVariableValidator (CollectFromStatement/CollectReadsFromExpression), ControlFlowValidator (ValidateTopLevelStatement/ValidateNestedFunctions).
- **Audit file** — Confirmed at `docs/audits/audit-2026-03-13.md`.
- **244 total diagnostic codes** — Confirmed (244 `const string` declarations in DiagnosticCodes.cs).

### Unchecked Claims

- **"~1,000+ lines of duplicated switch/foreach"** across validators — not line-counted, but extensive duplication confirmed by the large switch/foreach patterns found in each validator.
