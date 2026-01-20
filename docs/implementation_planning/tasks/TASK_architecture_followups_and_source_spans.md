# Architecture Follow-ups and Source Span Migration

**Status:** ✅ COMPLETE
**Assignee:** Junior Engineer / Claude Sonnet
**Estimated Effort:** 3-5 days
**Prerequisites:** All existing tests passing

---

## Overview

This task list covers:
1. **CodeGenInfo Emitter Migration** - Wire up the pre-computed CodeGenInfo in RoslynEmitter
2. **Validation Pipeline Cleanup** - Remove legacy dual-path code
3. **Source Span Migration** (Rec #10) - Complete TextSpan population across AST nodes

### Guiding Principles

- **Test-driven:** Run tests after each checkpoint; all existing passing tests must continue to pass
- **Two-way door preferred:** Changes should be reversible where possible
- **Incremental commits:** Commit at each major step for easy revert/bisection
- **No breaking changes:** The compiler should produce identical output before and after (except for better error locations)

### Long-term Considerations

These changes enable future features:
- **LSP (v0.2.x+):** Requires precise source locations for hover, go-to-definition, error squiggles
- **Async/Await (v0.2.x+):** CFG construction benefits from accurate spans
- **Tagged Unions (v0.2.x+):** Exhaustive pattern matching errors need precise locations
- **Debugger Support:** PDB generation requires accurate source mapping

---

## Pre-flight Checklist

Before starting, ensure your environment is ready:

- [x] Clone latest `main` branch
- [x] Run `dotnet build sharpy.sln` - should succeed
- [x] Run `dotnet test` - record baseline: **3762 tests passed, 0 failed, 10 skipped**
- [x] Note any pre-existing failing tests (do not fix these as part of this task)

```bash
# Save baseline test results
dotnet test --logger "console;verbosity=normal" 2>&1 | tee baseline_tests.log
grep -E "(Passed|Failed|Skipped)" baseline_tests.log | tail -5
```

---

## Part 1: CodeGenInfo Emitter Migration ✅ COMPLETE

**Goal:** Migrate RoslynEmitter to use pre-computed CodeGenInfo instead of runtime tracking sets.

> **Note:** The infrastructure (CodeGenInfo, CodeGenInfoComputer, helper methods) already exists. This task wires up the actual emission code to use it.

### 1.1 Audit Current Helper Methods ✅

- [x] Open `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`
- [x] Locate existing helper methods (search for `GetCSharpNameForSymbol`):
  - `GetCSharpNameForSymbol(Symbol symbol)` ✅ (line 201)
  - `IsModuleLevelConstant(Symbol symbol)` ✅ (line 225)
  - `IsModuleLevelVariable(Symbol symbol)` ✅ (line 233)
  - `HasExecutionOrderIssues(Symbol symbol)` ✅ (line 241)
  - `IsFromImportSymbol(Symbol symbol)` ✅ (line 249)
  - `GetOriginalImportName(Symbol symbol)` ✅ (line 258)
- [x] Verify each helper has fallback logic for when `CodeGenInfo` is null ✅
- [x] If any helpers are missing, they may have been implemented already ✅

### 1.2 Create Migration Utility Method ⏭️ SKIPPED

> **Decision:** The DEBUG validation helper was not implemented because the migration was validated via comprehensive test suite (3762+ tests). The helper methods work correctly without runtime comparison.

### 1.3 Migrate Variable Name Resolution ✅

- [x] `TryGetCSharpNameFromCodeGenInfo()` method (lines 86-111) provides CodeGenInfo-first resolution
- [x] `GetMangledVariableName()` (lines 119-188) uses CodeGenInfo via the helper method
- [x] Local scope tracking still uses `_variableVersions` for redeclarations (by design)

### 1.4 Migrate Module-Level Detection ✅

- [x] `IsModuleLevelConstant(Symbol)` helper implemented
- [x] `IsModuleLevelVariable(Symbol)` helper implemented
- [x] Used via `TryGetCSharpNameFromCodeGenInfo()` for module-level symbols

### 1.5 Migrate Import Symbol Handling ✅

- [x] `IsFromImportSymbol(Symbol)` helper checks `CodeGenInfo.ImportKind`
- [x] `GetOriginalImportName(Symbol)` helper returns `CodeGenInfo.OriginalImportName`

### 1.6 Migrate Execution Order Detection ✅

- [x] `HasExecutionOrderIssues(Symbol)` helper checks `CodeGenInfo.HasExecutionOrderIssues`
- [x] Used in `RoslynEmitter.Statements.cs` (line 433)

### 1.7 Add Deprecation Markers to Legacy Fields ⏭️ NOT APPLICABLE

> **Decision:** The local scope tracking fields (`_variableVersions`, `_constVariables`, `_moduleFieldNames`, `_declaredVariables`) are **intentionally kept without `[Obsolete]` attributes** because they are still needed for local variable redeclarations during emission. CodeGenInfo cannot pre-compute local variable versions because redeclarations happen during emission, not semantic analysis.
>
> The fields are documented with comments (lines 17-52) explaining their purpose and why they're needed alongside CodeGenInfo.

### 1.8 Remove DEBUG Validation Code ⏭️ NOT APPLICABLE

> No DEBUG validation code was added (see 1.2).

**Checkpoint:** ✅ All tests passing. CodeGenInfo is the primary source for module-level code generation metadata. Local scope tracking uses runtime fields by design.

---

## Part 2: Validation Pipeline Cleanup ✅ COMPLETE

**Goal:** Remove dual-path code now that the validation pipeline is the default.

### 2.1 Verify Pipeline Is Default ✅

- [x] Open `src/Sharpy.Compiler/Semantic/TypeChecker.cs`
- [x] Pipeline is created via `ValidationPipelineFactory.CreateDefault(logger)` (line 72)
- [x] Default pipeline is always used - no conditional logic

### 2.2 Remove `_usePipeline` Field ✅

- [x] Search for `_usePipeline` in TypeChecker.cs - **not found**
- [x] Field does not exist - pipeline is always enabled
- [x] No dual-path conditionals to remove

### 2.3 Clean Up Error Aggregation ⚠️ PARTIALLY COMPLETE

The `Errors` property (lines 133-167) still collects from legacy validators for deduplication:
- Legacy validators are still called during type checking for type inference
- V2 validators duplicate many legacy validations
- Deduplication logic prevents double-reporting

> **Note:** This is intentional during the migration period. Once V2 validators cover all legacy validations AND type inference is fully extracted to TypeInferenceService, legacy validator error collection can be removed. See TODO comment at line 142.

### 2.4 Update SemanticContext Legacy Properties ✅

- [x] `CurrentClass` has `[Obsolete("Use Traversal.CurrentClass instead...")]`
- [x] `CurrentFunction` has `[Obsolete("Use Traversal.CurrentFunction instead...")]`
- [x] `InLoop` has `[Obsolete("Use Traversal.InLoop instead...")]`
- [x] `LoopDepth` has `[Obsolete("Use Traversal.LoopDepth instead...")]`

**Checkpoint:** ✅ Validation pipeline is the only code path. Legacy property deprecation is in place.

---

## Part 3: Source Span Migration (Recommendation #10) ✅ COMPLETE

**Goal:** Populate `TextSpan` on all AST nodes for precise source location tracking.

### Architecture Decision: Span Population Strategy

**Decision:** Populate spans in the Parser during AST construction (not retroactively).

**Rationale:**
- The Lexer already tracks token positions via `Token.Position`
- Parser has access to start and end tokens for each construct
- This is a **one-way door** (changing later would require parser rewrite), but:
  - It's the same approach used by Roslyn, TypeScript, and other production compilers
  - Enables future incremental parsing
  - Required for LSP anyway

### 3.1 Audit Current Span Infrastructure ✅

- [x] `TextSpan` exists: `src/Sharpy.Compiler/Text/TextSpan.cs`
- [x] `ILocatable` exists: `src/Sharpy.Compiler/Text/ILocatable.cs`
- [x] `Node` base class has `Span` property: `src/Sharpy.Compiler/Parser/Ast/Node.cs`
- [x] `Token` has position tracking: `Position` property and `GetSpan()` method
- [x] `source_span_migration_status.md` updated to reflect current state

### 3.2 Create Parser Helper Methods ✅

**File:** `src/Sharpy.Compiler/Parser/Parser.Types.cs` (lines 324-353)

- [x] `GetSpanFromToken(Token)` - Create span from single token
- [x] `GetSpanFromTokens(Token, Token)` - Create span covering token range
- [x] `CombineSpans(TextSpan?, TextSpan?)` - Merge two spans

### 3.3 Phase A: High-Priority Expressions ✅

#### 3.3.1 Literals ✅

- [x] `IntegerLiteral` - Parser.Primaries.cs:53
- [x] `FloatLiteral` - Parser.Primaries.cs:81
- [x] `StringLiteral` - Parser.Primaries.cs:98
- [x] `BooleanLiteral` - Parser.Primaries.cs:115, 137
- [x] `NoneLiteral` - Parser.Primaries.cs:152
- [x] `EllipsisLiteral` - Parser.Primaries.cs:166

#### 3.3.2 Call Expressions ✅

- [x] `FunctionCall` - Parser.Expressions.cs:740

#### 3.3.3 Member Access ✅

- [x] `MemberAccess` - Parser.Expressions.cs:641

#### 3.3.4 Index Access ✅

- [x] `IndexAccess` - Parser.Expressions.cs:660

#### 3.3.5 Binary and Unary Operators ✅

- [x] `BinaryOp` - Multiple locations in Parser.Expressions.cs (163, 188, 213, etc.)
- [x] `UnaryOp` - Parser.Expressions.cs:75, 98, 238, 586

### 3.4 Phase B: Statements ✅

#### 3.4.1 Simple Statements ✅

- [x] `ReturnStatement` - Parser.Statements.cs:391
- [x] `BreakStatement` - Parser.Statements.cs:503
- [x] `ContinueStatement` - Parser.Statements.cs:522
- [x] `PassStatement` - Parser.Statements.cs:484
- [x] `AssertStatement` - Parser.Statements.cs:465

#### 3.4.2 Variable Declaration and Assignment ✅

- [x] `VariableDeclaration` - Parser.Definitions.cs:121
- [x] `Assignment` - Parser.Definitions.cs:64, 89

#### 3.4.3 Control Flow Statements ✅

- [x] `IfStatement` - Parser.Statements.cs:82 (including elif at line 56)
- [x] `WhileStatement` - Parser.Statements.cs:123
- [x] `ForStatement` - Parser.Statements.cs:171

#### 3.4.4 Exception Handling ✅

- [x] `TryStatement` - Parser.Statements.cs:366 (handlers at line 327)
- [x] `RaiseStatement` - Parser.Statements.cs:433

### 3.5 Phase C: Definitions ✅

#### 3.5.1 Function Definition ✅

- [x] `FunctionDef` - Parser.Definitions.cs:262 (includes decorators via Parser.cs:148)

#### 3.5.2 Class, Struct, Interface, Enum Definitions ✅

- [x] `ClassDef` - Parser.Definitions.cs:331
- [x] `StructDef` - Parser.Definitions.cs:400
- [x] `InterfaceDef` - Parser.Definitions.cs:469
- [x] `EnumDef` - Parser.Definitions.cs:649

### 3.6 Phase D: Remaining Expressions ✅

#### 3.6.1 Collection Literals ✅

- [x] `ListLiteral` - Parser.Primaries.cs:213
- [x] `DictLiteral` - Parser.Primaries.cs:335
- [x] `SetLiteral` - Parser.Primaries.cs:371
- [x] `TupleLiteral` - Parser.Primaries.cs:272

#### 3.6.2 Comprehensions ✅

- [x] `ListComprehension` - Parser.Primaries.cs:233
- [x] `DictComprehension` - Parser.Primaries.cs:397
- [x] `SetComprehension` - Parser.Primaries.cs:424

#### 3.6.3 Other Expressions ✅

- [x] `TernaryExpression` - Parser.Expressions.cs:138
- [x] `LambdaExpression` - Parser.Primaries.cs:509
- [x] `SliceAccess` - Parser.Expressions.cs:670

### 3.7 Phase E: Import Statements and Type Annotations ✅

#### 3.7.1 Import Statements ✅

- [x] `ImportStatement` - Parser.Statements.cs:580
- [x] `FromImportStatement` - Parser.Statements.cs:651
- [x] `ImportAlias` - Parser.Statements.cs:561, 629

#### 3.7.2 Type Annotations ✅

- [x] `SimpleTypeAnnotation` - Parser.Types.cs:62
- [x] `GenericTypeAnnotation` - Parser.Types.cs:141
- [x] `NullableTypeAnnotation` - Parser.Types.cs:79
- [x] `UnionTypeAnnotation` - Parser.Types.cs:214
- [x] `FunctionTypeAnnotation` - Parser.Types.cs:296
- [x] `TupleTypeAnnotation` - Parser.Types.cs:234

### 3.8 Update Migration Status Document ✅

- [x] Updated `docs/implementation_planning/source_span_migration_status.md`
- [x] All node types marked as complete
- [x] Test coverage documented

### 3.9 Add Span Tests ✅

**File:** `src/Sharpy.Compiler.Tests/Parser/ParserSpanTests.cs`

- [x] 22 comprehensive span tests covering:
  - Literals (6 tests)
  - Collections (2 tests)
  - Operators (2 tests)
  - Access expressions (3 tests)
  - Statements (4 tests)
  - Definitions (3 tests)
  - Type annotations (2 tests)

---

## Part 4: Final Verification ✅

### 4.1 Full Test Suite Verification ✅

- [x] Run complete test suite: `dotnet test`
- [x] Baseline: 3762 passed, 0 failed, 10 skipped
- [x] All previously passing tests still pass
- [x] Span tests pass

### 4.2 Build Verification ✅

- [x] Clean and rebuild: `dotnet clean && dotnet build`
- [x] No new warnings

### 4.3 Integration Test ✅

Error messages reference correct line/column positions.

### 4.4 Final Commit ✅

All work has been committed incrementally throughout the implementation.

---

## Appendix A: Troubleshooting

### Tests Fail After Span Changes

**Symptom:** Tests that check AST structure fail because `Span` is now populated.

**Fix:** Tests should not break due to new `Span` values. If they do:
1. Check if tests are using strict equality on AST nodes
2. `Span` is optional (nullable), so existing comparisons should work
3. Update tests only if they explicitly expected `Span = null`

### Lexer Position Not Tracked

**Symptom:** `GetSpanFromToken()` returns `null` because `Token.Position` is `-1`.

**Fix:** The Lexer must set `Token.Position`. Check:
1. `src/Sharpy.Compiler/Lexer/Lexer.cs`
2. Ensure `_position` is tracked and assigned to tokens
3. See existing implementation in `Token.cs`

### Overlapping/Invalid Spans

**Symptom:** Spans have negative length or don't cover expected range.

**Fix:**
1. Verify start/end token order
2. Use `TextSpan.FromBounds()` which validates bounds
3. Log suspicious spans during development

---

## Appendix B: Files Modified Summary

| File | Part | Changes |
|------|------|---------|
| `RoslynEmitter.cs` | 1 | CodeGenInfo helper methods (lines 190-261) |
| `TypeChecker.cs` | 2 | Pipeline is default, Errors property with deduplication |
| `SemanticContext.cs` | 2 | Legacy properties marked `[Obsolete]` |
| `Parser.Primaries.cs` | 3 | 25 span assignments |
| `Parser.Expressions.cs` | 3 | 26 span assignments |
| `Parser.Statements.cs` | 3 | 19 span assignments |
| `Parser.Definitions.cs` | 3 | 16 span assignments |
| `Parser.Types.cs` | 3 | Helper methods + 8 span assignments |
| `Parser.cs` | 3 | Decorator span handling |
| `source_span_migration_status.md` | 3 | Updated to reflect completion |
| `ParserSpanTests.cs` | 3 | 22 comprehensive tests |

---

## Appendix C: Decision Log

| Decision | Type | Rationale |
|----------|------|-----------|
| Populate spans in Parser | One-way door | Industry standard; enables incremental parsing; required for LSP |
| Keep local scope tracking fields | Two-way door | Cannot be pre-computed; needed for variable redeclaration during emission |
| Use `CombineSpans()` helper | Two-way door | Cleaner than `TextSpan.Union()`; null-safe |
| Span is nullable | Two-way door | Backward compatible; allows opt-in during migration |
| Skip DEBUG validation helper | Two-way door | Comprehensive test suite (3762+ tests) provides sufficient validation |
