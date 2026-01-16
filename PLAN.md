# Implementation Plan: Task 0.1.9.6 - Type Narrowing

## Executive Summary

**Status: Type narrowing is already substantially implemented.** The codebase has a working type narrowing system in `TypeChecker.cs` that handles the core patterns specified in the task description. This analysis identifies what exists, what might be enhanced, and potential gaps.

---

## Current Implementation Analysis

### What Already Exists (in `TypeChecker.cs`)

1. **Core Infrastructure** (lines 31, 806-910, 2248-2345):
   - `_narrowedTypes` dictionary tracks narrowed types per variable
   - `ExtractNarrowedTypes()` extracts narrowing patterns from conditions
   - `ExtractNarrowingKey()` generates keys for narrowed variables (supports identifiers and index access)
   - Save/restore mechanism for scope management

2. **Supported Narrowing Patterns**:
   - `x is not None` → narrows `T?` to `T` in positive branch (lines 2274-2289)
   - `x is None` → narrows `T?` to `T` in negative branch (else) (lines 2290-2305)
   - `isinstance(x, Type)` → narrows to `Type` in positive branch (lines 2306-2328)
   - `A and B` → combines narrowings from both conditions (lines 2252-2272)
   - Subscript narrowing: `arr[i] is not None` (via `ExtractNarrowingKey`)

3. **Scope Integration**:
   - `CheckIf()` applies narrowings in then/elif/else branches (lines 806-879)
   - `CheckWhile()` applies narrowings in loop body (lines 881-910)
   - Proper save/restore of narrowed types on scope entry/exit

4. **Type Lookup**:
   - `CheckIdentifier()` returns narrowed type if available (lines 1148-1150)
   - `CheckIndexAccess()` supports narrowed subscript expressions (lines 1648-1650)

### Existing Tests (in `TypeCheckerTests.cs`)

| Test Name | Pattern Tested |
|-----------|----------------|
| `InfersNullableTypeFromNone` | `x is not None` narrowing |
| `TypeNarrowingWithIsInstance` | `isinstance(x, Type)` narrowing |
| `TypeNarrowingWithIsInstanceDoesNotAffectElseBranch` | Else branch isolation |
| `TypeNarrowingWithIsInstanceInWhileLoop` | While loop + `and` combination |
| `IsInstanceWithMultipleTypeChecks` | Multiple sequential isinstance |
| `CombinedTypeNarrowingIsNotNoneAndIsInstance` | Combined `is not None and isinstance` |

---

## Gap Analysis: What Might Be Missing

### 1. **`or` Pattern Non-Narrowing (Spec Compliance)**
The spec says: "Type narrowing does not occur with `or` as type union semantics do not exist in Sharpy"

**Current State**: No explicit handling for `or` - the code simply doesn't extract narrowings from `or` patterns (correct behavior).

**Status**: ✅ Compliant by omission (no narrowing occurs with `or`)

### 2. **`is None` Narrowing to Never-Type**
The spec says: "`is None` narrows to never-type in the `if` branch"

**Current State**: The implementation narrows in the *else* branch but doesn't narrow to a "never-type" in the *if* branch. The spec's "never-type" concept may not be implemented.

**Status**: ⚠️ Partial - The else branch is correct, but no "never-type" narrowing in the if branch.

### 3. **Property Access Narrowing**
The current implementation only narrows:
- Simple identifiers (`value`)
- Index access (`arr[i]`)

Not supported:
- Property access (`obj.field is not None`)

**Status**: ⚠️ Gap - May need enhancement if property narrowing is desired.

### 4. **Early Return Pattern**
Pattern like:
```python
if value is None:
    return
# After this, value should be narrowed
```

**Current State**: This flow-sensitive narrowing is NOT supported. The narrowing is scoped to conditional blocks only.

**Status**: ⚠️ Gap - This is a more advanced flow analysis feature.

---

## Recommended Implementation Steps

Since the core implementation exists, this task should focus on **verification and potential enhancements**:

### Step 1: Verify Existing Implementation (Verification Phase)

1. **Run existing tests** to confirm narrowing works:
   ```bash
   dotnet test --filter "TypeNarrowing|InfersNullable|IsInstance"
   ```

2. **Review test coverage** against the spec patterns in `type_narrowing.md`

### Step 2: Add Missing Test Cases (If Needed)

Add tests for any gaps identified:

```csharp
// Test: or pattern does NOT narrow (spec compliance)
[Fact]
public void TypeNarrowingWithOrDoesNotNarrow()
{
    var source = @"
class Animal: ...
class Dog(Animal): ...
class Cat(Animal): ...

animal: Animal = Dog()
if isinstance(animal, Dog) or isinstance(animal, Cat):
    # animal should NOT be narrowed here
    a: Animal = animal  # This should work (not narrowed to Dog or Cat)
";
    // Assert no errors - animal remains Animal type
}

// Test: is None in else branch narrows
[Fact]
public void TypeNarrowingWithIsNoneElseBranch()
{
    var source = @"
value: str? = get_value()
if value is None:
    pass  # value is None here
else:
    x: str = value  # value is narrowed to str
";
    // Assert no errors
}

// Test: nested and narrowing
[Fact]
public void TypeNarrowingWithMultipleAnd()
{
    var source = @"
a: str? = None
b: int? = None
if a is not None and b is not None:
    x: str = a
    y: int = b
";
    // Assert no errors
}
```

### Step 3: Potential Enhancements (Optional)

If enhancements are needed based on requirements:

#### 3a. Property Access Narrowing
Extend `ExtractNarrowingKey()` to support member access:

```csharp
private string? ExtractNarrowingKey(Expression expr)
{
    return expr switch
    {
        Identifier id => id.Name,
        IndexAccess indexAccess => $"{ExtractNarrowingKey(indexAccess.Object)}[{ExtractNarrowingKey(indexAccess.Index)}]",
        MemberAccess memberAccess => $"{ExtractNarrowingKey(memberAccess.Object)}.{memberAccess.Member}",
        _ => null
    };
}
```

Then add to `CheckMemberAccess()` to look up narrowed types for property expressions.

#### 3b. Early Return Pattern (Advanced)
This requires more significant changes:
- Track "definite exit" paths (return, raise, break)
- After an exit path with a condition, apply inverse narrowing to subsequent code

**Recommendation**: Defer to a future task if needed.

---

## Key Files to Modify

| File | Purpose |
|------|---------|
| `src/Sharpy.Compiler/Semantic/TypeChecker.cs` | Main narrowing logic (lines 2248-2345) |
| `src/Sharpy.Compiler.Tests/Semantic/TypeCheckerTests.cs` | Add/verify test cases |

The `SemanticAnalyzer.cs` file mentioned in the task description does NOT contain type narrowing logic - it's all in `TypeChecker.cs`.

---

## Tests to Verify

### Existing Tests (Should All Pass)
- `InfersNullableTypeFromNone`
- `TypeNarrowingWithIsInstance`
- `TypeNarrowingWithIsInstanceDoesNotAffectElseBranch`
- `TypeNarrowingWithIsInstanceInWhileLoop`
- `IsInstanceWithMultipleTypeChecks`
- `CombinedTypeNarrowingIsNotNoneAndIsInstance`

### New Tests to Add
1. `TypeNarrowingWithOrDoesNotNarrow` - Verify `or` doesn't narrow
2. `TypeNarrowingWithIsNoneElseBranch` - Verify `is None` else branch
3. `TypeNarrowingWithMultipleAnd` - Verify multiple `and` conditions
4. `TypeNarrowingRestoredAfterBlock` - Verify narrowing doesn't leak

---

## Potential Risks and Questions

### Risks

1. **Scope Leakage**: Narrowing must not persist after the conditional block. Current save/restore mechanism handles this, but tests should verify.

2. **Reassignment Invalidation**: If a variable is reassigned inside a narrowed block, the narrowing should potentially be invalidated. Current implementation doesn't handle this (common limitation).

3. **Loop Iteration**: In while loops, if the variable is modified in the loop body, the narrowing from the condition may be invalid on subsequent iterations.

### Questions for Clarification

1. **Is property narrowing required?** (e.g., `if obj.field is not None`)

2. **Is early return narrowing required?** (e.g., `if x is None: return` should narrow x after)

3. **Should reassignment invalidate narrowing?** (e.g., `if x is not None: x = None` - should x still be narrowed?)

4. **Is the current test coverage sufficient, or are additional specific scenarios needed?**

---

## Conclusion

**The type narrowing implementation is already complete for the patterns described in the task and spec.** The recommended action is:

1. **Verify** existing tests pass
2. **Add additional tests** if coverage is incomplete
3. **Optionally enhance** property narrowing if required

No major implementation work is needed unless the requirements extend beyond what's currently supported.
