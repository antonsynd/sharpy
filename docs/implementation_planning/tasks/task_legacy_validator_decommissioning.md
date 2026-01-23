# Task List: Legacy Validator Decommissioning

**Goal:** Remove legacy validators from TypeChecker after verifying V2 validators cover all cases, and extract any remaining type inference logic to TypeInferenceService.

**Priority:** Medium - Technical debt cleanup that simplifies the codebase.

**Prerequisites:** 
- Validation Pipeline fully implemented (✅ Done)
- V2 validators passing all tests (✅ Done)

**Estimated Total Effort:** 1-2 days

**Related Documents:**
- `architecture_review_and_recommendations.md` - Recommendation 3
- `task_compiler_services_layer.md` - CompilerServices integration

---

## Problem Summary

The `TypeChecker` currently maintains both legacy validators and V2 pipeline validators:

```csharp
// TypeChecker.cs - DEPRECATED but still present
private readonly ControlFlowValidator _controlFlowValidator;
private readonly AccessValidator _accessValidator;
private readonly OperatorValidator _operatorValidator;
private readonly ProtocolValidator _protocolValidator;
private readonly DefaultParameterValidator _defaultParameterValidator;
```

The legacy validators are kept "for type inference during type checking" according to comments, but this creates:
1. **Duplicate error reporting** - requires deduplication logic
2. **Maintenance burden** - two codepaths to maintain
3. **Confusion** - unclear which validator handles what

---

## Design Decisions

### Two-Way Door Decisions (Reversible)
1. **Gradual removal**: Remove one legacy validator at a time with verification
2. **Feature flags**: Keep ability to re-enable legacy validators during transition (if needed)

### One-Way Door Decisions (Commit Now)
1. **TypeInferenceService as extraction target**: All type inference logic goes to TypeInferenceService
2. **V2 validators as source of truth**: Error messages come from V2 validators only

---

## Phase 0: Analysis (1-2 hours)

### Task 0.1: Audit Legacy Validator Usage
**File:** `src/Sharpy.Compiler/Semantic/TypeChecker.cs` and partials
**Description:** Search for all usages of legacy validators to understand dependencies.

```bash
cd /Users/anton/Documents/github/sharpy/src/Sharpy.Compiler
grep -r "_controlFlowValidator\|_accessValidator\|_operatorValidator\|_protocolValidator\|_defaultParameterValidator" --include="*.cs"
```

Document each usage site and categorize:
- **Error reporting**: Can be removed (V2 handles this)
- **Type inference**: Must be extracted to TypeInferenceService
- **Side effects**: Must be preserved somehow

**Verification:**
- [x] All usages documented
- [x] Categorization complete

**Audit Results:**

| Validator | Location | Usage Type | Action |
|-----------|----------|------------|--------|
| `_controlFlowValidator` | TypeChecker.cs:144 | Error collection | Remove (V2 covers) |
| `_controlFlowValidator` | TypeChecker.Definitions.cs:282 | `ValidateFunction` | V2 covers (ControlFlowValidatorV3) |
| `_accessValidator` | TypeChecker.cs:145 | Error collection | Remove (V2 covers) |
| `_accessValidator` | TypeChecker.Definitions.cs:346,365,421,437 | `EnterClass/ExitClass` | Side effect - remove (V2 handles via AST traversal) |
| `_accessValidator` | TypeChecker.Expressions.cs:524,541 | `ValidateFieldAccess/ValidateMethodAccess` | V2 covers (AccessValidatorV2) |
| `_operatorValidator` | TypeChecker.cs:146 | Error collection | Remove (V2 covers) |
| `_operatorValidator` | TypeChecker.Statements.cs:171 | `ValidateAugmentedAssignment` | **Type inference** - needs migration |
| `_operatorValidator` | TypeChecker.Expressions.cs:122,404,454 | `ValidateBinaryOp/ValidateUnaryOp` | Fallback for type inference |
| `_protocolValidator` | TypeChecker.cs:147 | Error collection | Remove (V2 covers) |
| `_protocolValidator` | TypeChecker.Statements.cs:432 | `ValidateIteration` | Fallback for type inference |
| `_protocolValidator` | TypeChecker.Expressions.cs:680,780,1227,1293,1358 | Protocol methods | Fallback for type inference |
| `_defaultParameterValidator` | TypeChecker.cs:148 | Error collection | Remove (V2 covers) |
| `_defaultParameterValidator` | TypeChecker.Definitions.cs:217 | `ValidateFunctionDefaults` | V2 covers (DefaultParameterValidatorV2) |

**Categories Summary:**
- **Error Reporting (remove):** Error collection in `Errors` getter - V2 validators cover
- **Side Effects (remove):** `EnterClass/ExitClass` - V2 uses AST traversal, no need for state tracking
- **Type Inference (migrate):** `ValidateAugmentedAssignment` needs `InferAugmentedAssignmentType` in TypeInferenceService
- **Fallbacks (remove):** All other usages are fallbacks when TypeInferenceService returns null - TypeInferenceService already covers these cases adequately

---

### Task 0.2: Compare V2 vs Legacy Error Coverage
**File:** `src/Sharpy.Compiler.Tests/Semantic/Validation/`
**Description:** Ensure V2 validators cover all error cases from legacy validators.

Create a comparison matrix:

| Error Case | Legacy Validator | V2 Validator | Notes |
|------------|-----------------|--------------|-------|
| Break outside loop | ControlFlowValidator | ControlFlowValidatorV2/V3 | ✅ Covered |
| Missing return | ControlFlowValidator | ControlFlowValidatorV3 | ✅ CFG-based |
| Private access | AccessValidator | AccessValidatorV2 | ✅ Covered |
| Invalid operator | OperatorValidator | OperatorValidatorV2 | ✅ Covered |
| Protocol mismatch | ProtocolValidator | ProtocolValidatorV2 | ✅ Covered |
| Invalid default | DefaultParameterValidator | DefaultParameterValidatorV2 | ✅ Covered |

**Verification:**
- [x] All legacy error cases have V2 equivalents
- [x] No gaps identified

**Comparison Result:** All error cases are covered by V2 validators:

| Legacy Error | Legacy Validator | V2/V3 Validator | Confirmed |
|--------------|-----------------|-----------------|-----------|
| Break outside loop | ControlFlowValidator | ControlFlowValidatorV2/V3 | ✅ |
| Continue outside loop | ControlFlowValidator | ControlFlowValidatorV2/V3 | ✅ |
| Unreachable code | ControlFlowValidator | ControlFlowValidatorV2/V3 | ✅ |
| Missing return | ControlFlowValidator | ControlFlowValidatorV3 | ✅ CFG-based |
| Private member access | AccessValidator | AccessValidatorV2 | ✅ |
| Protected member access | AccessValidator | AccessValidatorV2 | ✅ |
| Invalid binary operator | OperatorValidator | OperatorValidatorV2 | ✅ |
| Invalid unary operator | OperatorValidator | OperatorValidatorV2 | ✅ |
| Invalid in/not in | ProtocolValidator | ProtocolValidatorV2 | ✅ |
| Invalid indexing | ProtocolValidator | ProtocolValidatorV2 | ✅ |
| Invalid iteration | ProtocolValidator | ProtocolValidatorV2 | ✅ |
| Invalid len() | ProtocolValidator | ProtocolValidatorV2 | ✅ |
| Invalid default value | DefaultParameterValidator | DefaultParameterValidatorV2 | ✅ |
| Non-constant default | DefaultParameterValidator | DefaultParameterValidatorV2 | ✅ |
| Mutable default | DefaultParameterValidator | DefaultParameterValidatorV2 | ✅ |

---

## Phase 1: Extract Type Inference Logic (2-4 hours)

### Task 1.1: Audit Type Inference in OperatorValidator
**File:** `src/Sharpy.Compiler/Semantic/OperatorValidator.cs`
**Description:** Identify type inference logic that needs extraction.

Look for methods like:
- `InferBinaryOperatorResultType`
- `InferComparisonResultType`
- `InferUnaryOperatorResultType`

```csharp
// Example of what to look for:
public SemanticType? InferBinaryOperatorResultType(string op, SemanticType left, SemanticType right)
{
    // This logic needs to move to TypeInferenceService
}
```

**Verification:**
- [x] Type inference methods identified
- [x] Dependencies mapped

**Audit Result:**

| Method | Purpose | In TypeInferenceService? | Action |
|--------|---------|--------------------------|--------|
| `ValidateBinaryOp` | Binary op result type | ✅ `InferBinaryOpType` | Remove fallback |
| `ValidateUnaryOp` | Unary op result type | ✅ `InferUnaryOpType` | Remove fallback |
| `ValidateAugmentedAssignment` | Augmented assignment type | ❌ Missing | Add `InferAugmentedAssignmentType` |
| `TryResolveOperatorOverloadWithoutLogging` | Internal helper | ✅ Logic duplicated in TypeInferenceService | Remove |

**Key Method to Migrate:** `ValidateAugmentedAssignment` handles:
- In-place operators (`__iadd__`, `__isub__`, etc.)
- Fallback to binary operators (`__add__`, `__sub__`, etc.)
- Null coalesce assignment (`??=`)

---

### Task 1.2: Migrate Operator Type Inference to TypeInferenceService
**File:** `src/Sharpy.Compiler/Semantic/TypeInferenceService.cs`
**Description:** Add operator result type inference methods.

```csharp
public class TypeInferenceService
{
    // ... existing code ...
    
    /// <summary>
    /// Infer the result type of a binary operation.
    /// </summary>
    public SemanticType? InferBinaryOperatorResult(string op, SemanticType left, SemanticType right)
    {
        // Arithmetic operators
        if (op is "+" or "-" or "*" or "/" or "//" or "%" or "**")
        {
            return InferArithmeticResult(op, left, right);
        }
        
        // Comparison operators always return bool
        if (op is "==" or "!=" or "<" or "<=" or ">" or ">=")
        {
            return BuiltinType.Bool;
        }
        
        // Logical operators
        if (op is "and" or "or")
        {
            return BuiltinType.Bool;
        }
        
        // Bitwise operators
        if (op is "&" or "|" or "^" or "<<" or ">>")
        {
            return InferBitwiseResult(left, right);
        }
        
        return null;
    }
    
    private SemanticType? InferArithmeticResult(string op, SemanticType left, SemanticType right)
    {
        // Numeric promotion rules
        // ...
    }
    
    /// <summary>
    /// Infer the result type of a unary operation.
    /// </summary>
    public SemanticType? InferUnaryOperatorResult(string op, SemanticType operand)
    {
        return op switch
        {
            "-" or "+" or "~" => operand, // Same type as operand for numeric
            "not" => BuiltinType.Bool,
            _ => null
        };
    }
}
```

**Verification:**
- [x] TypeInferenceService methods work
- [x] Unit tests added (18 tests for `InferAugmentedAssignmentType`)

**Commit:** `feat(semantic): Add augmented assignment type inference to TypeInferenceService`

---

### Task 1.3: Audit Type Inference in ProtocolValidator
**File:** `src/Sharpy.Compiler/Semantic/ProtocolValidator.cs`
**Description:** Identify type inference for protocol methods like `__len__`, `__iter__`, etc.

Look for:
- Return type inference for dunder methods
- Collection type inference (e.g., `__iter__` returns `Iterator[T]`)

**Verification:**
- [x] Protocol type inference identified

**Audit Result:** All protocol type inference methods are already in TypeInferenceService:

| ProtocolValidator Method | Returns | TypeInferenceService Method | Status |
|--------------------------|---------|----------------------------|--------|
| `ValidateLen` | `Int` | `InferLenType` | ✅ Already covered |
| `ValidateIteration` | Element type | `InferIterableElementType` | ✅ Already covered |
| `ValidateMembership` | `Bool` | `InferMembershipType` | ✅ Already covered |
| `ValidateIndexAccess` | Element type | `InferIndexAccessType` | ✅ Already covered |
| `ValidateBoolConversion` | `Bool` | N/A (trivial, always bool) | ✅ No migration needed |

**Conclusion:** No additional migration required - TypeInferenceService already has complete protocol type inference coverage.

---

### Task 1.4: Migrate Protocol Type Inference to TypeInferenceService
**File:** `src/Sharpy.Compiler/Semantic/TypeInferenceService.cs`
**Description:** Add protocol-related type inference.

```csharp
/// <summary>
/// Infer the type that results from calling a protocol method.
/// </summary>
public SemanticType? InferProtocolMethodResult(string protocolMethod, SemanticType targetType)
{
    return protocolMethod switch
    {
        "__len__" => BuiltinType.Int,
        "__bool__" => BuiltinType.Bool,
        "__str__" => BuiltinType.Str,
        "__hash__" => BuiltinType.Int,
        "__iter__" => InferIteratorType(targetType),
        "__next__" => InferIteratorElementType(targetType),
        "__getitem__" => InferIndexerReturnType(targetType),
        _ => null
    };
}
```

**Verification:**
- [x] Protocol type inference works (already in TypeInferenceService)
- [x] Tests added (existing tests cover all protocol methods)

**Note:** Task 1.4 is complete without code changes - TypeInferenceService already has all protocol type inference methods:
- `InferIterableElementType` - for iteration
- `InferIndexAccessType` - for indexing
- `InferMembershipType` - for `in` operator
- `InferLenType` - for `len()`

**Commit:** No commit needed - existing implementation is complete.

---

## Phase 2: Update TypeChecker to Use TypeInferenceService (2-4 hours)

### Task 2.1: Replace Legacy Validator Type Inference Calls
**File:** `src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.cs`
**Description:** Replace calls to legacy validators with TypeInferenceService.

Find patterns like:
```csharp
// OLD
var resultType = _operatorValidator.InferBinaryOperatorResultType(op, leftType, rightType);

// NEW
var resultType = _typeInference.InferBinaryOperatorResult(op, leftType, rightType);
```

**Verification:**
- [x] All inference calls replaced
- [x] Tests pass

**Implementation Notes:**
- Removed fallback calls to `_operatorValidator.ValidateBinaryOp/ValidateUnaryOp`
- Removed fallback calls to `_protocolValidator.ValidateIteration/ValidateIndexAccess/ValidateLen`
- Added direct error reporting in TypeChecker when TypeInferenceService returns null
- Updated deduplication logic to handle similar operator error messages from different validators
- Added `GetOperatorSymbol` helper methods for error message formatting

**Commit:** `refactor(semantic): Use TypeInferenceService for operator/protocol inference`

---

### Task 2.2: Remove Legacy Validator Error Collection from TypeChecker.Errors
**File:** `src/Sharpy.Compiler/Semantic/TypeChecker.cs`
**Description:** Simplify the `Errors` getter to not collect from legacy validators.

Current code:
```csharp
public IReadOnlyList<SemanticError> Errors
{
    get
    {
        var allErrors = new List<SemanticError>(_errors);
        
        // Legacy validators - REMOVE THIS SECTION
        var legacyErrors = new List<SemanticError>();
        legacyErrors.AddRange(_controlFlowValidator.Errors);
        legacyErrors.AddRange(_accessValidator.Errors);
        // ... deduplication logic ...
        
        return allErrors;
    }
}
```

New code:
```csharp
public IReadOnlyList<SemanticError> Errors
{
    get
    {
        // Errors come from:
        // 1. Direct TypeChecker errors (_errors)
        // 2. TypeResolver errors
        // 3. V2 validators via ValidationPipeline (merged in CheckModule)
        return _errors.AsReadOnly();
    }
}
```

**Verification:**
- [ ] Errors getter simplified
- [ ] All tests still pass (V2 validators cover cases)

**Commit:** `refactor(semantic): Remove legacy validator error collection`

---

## Phase 3: Remove Legacy Validators (1-2 hours)

### Task 3.1: Remove Legacy Validator Fields
**File:** `src/Sharpy.Compiler/Semantic/TypeChecker.cs`
**Description:** Remove the legacy validator field declarations.

```csharp
// REMOVE these fields:
// private readonly ControlFlowValidator _controlFlowValidator;
// private readonly AccessValidator _accessValidator;
// private readonly OperatorValidator _operatorValidator;
// private readonly ProtocolValidator _protocolValidator;
// private readonly DefaultParameterValidator _defaultParameterValidator;
```

Also remove their instantiation in the constructor.

**Verification:**
- [ ] Fields removed
- [ ] Constructor updated
- [ ] Compilation succeeds

**Commit:** `refactor(semantic): Remove legacy validator fields from TypeChecker`

---

### Task 3.2: Remove Legacy Validator Files (Optional)
**Files:** 
- `src/Sharpy.Compiler/Semantic/ControlFlowValidator.cs`
- `src/Sharpy.Compiler/Semantic/AccessValidator.cs`
- `src/Sharpy.Compiler/Semantic/OperatorValidator.cs`
- `src/Sharpy.Compiler/Semantic/ProtocolValidator.cs`
- `src/Sharpy.Compiler/Semantic/DefaultParameterValidator.cs`

**Description:** Delete the legacy validator files if they're no longer used anywhere.

```bash
# Check for any remaining usages first
grep -r "ControlFlowValidator\|AccessValidator\|OperatorValidator\|ProtocolValidator\|DefaultParameterValidator" --include="*.cs" src/
```

**Note:** Keep these files if they're used elsewhere (e.g., tests that specifically test legacy behavior). Mark them as `[Obsolete]` instead.

**Verification:**
- [ ] No remaining usages (or files marked obsolete)
- [ ] Compilation succeeds
- [ ] Tests pass

**Commit:** `refactor(semantic): Remove legacy validator files`

---

## Phase 4: Verification (30 minutes)

### Task 4.1: Run Full Test Suite
```bash
dotnet test Sharpy.Compiler.Tests --verbosity minimal
```

**Verification:**
- [ ] All tests pass
- [ ] No regressions

---

### Task 4.2: Verify Error Messages Unchanged
**Description:** Spot-check that error messages from V2 validators match expected format.

Run a few compilation tests manually:
```bash
cd examples
dotnet run --project ../src/Sharpy.Cli -- compile error_test.spy
```

**Verification:**
- [ ] Error messages are helpful
- [ ] Line numbers are correct

---

### Task 4.3: Update Documentation
**File:** `docs/implementation_planning/architecture_summary.md`
**Description:** Update to reflect legacy validators removed.

**Verification:**
- [ ] Documentation updated

**Commit:** `docs: Update architecture summary after legacy validator removal`

---

## Summary

After completing these tasks:

1. ✅ TypeChecker uses TypeInferenceService for all type inference
2. ✅ Legacy validators removed from TypeChecker
3. ✅ V2 validators are sole source of validation errors
4. ✅ Cleaner, more maintainable codebase
5. ✅ No duplicate error reporting or deduplication logic

Benefits:
- Reduced code complexity
- Single source of truth for validation
- Easier to add new validators
- Better testability
