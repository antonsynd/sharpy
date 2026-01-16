# Implementation Plan: Task 0.1.9.5 - Null Conditional Operator (`?.`)

## Overview

The null conditional operator (`?.`) allows safe member access on nullable types. When applied to a nullable value, it:
1. Returns `None` if the object is `None`
2. Returns the member value (wrapped in nullable) if the object is not `None`

### Syntax Examples
```python
name: str? = "hello"
upper = name?.upper()  # upper is str? (not str!)
length = name?.length  # length is int?
```

## Current State Analysis

### Already Implemented
- **Lexer**: `TokenType.NullConditional` (`?.`) is already tokenized (Token.cs:132)
- **Parser**: `MemberAccess` AST node has `IsNullConditional` flag, parsing is complete (Parser.cs:1983-1999)
- **Code Generation**: `RoslynEmitter` already generates C# `ConditionalAccessExpression` for `IsNullConditional=true` (RoslynEmitter.cs:2938-2943)

### Missing Implementation
- **Semantic Analysis (TypeChecker)**: The `CheckMemberAccess` method does NOT handle `IsNullConditional`. It needs to:
  1. Validate that the object type is nullable when `?.` is used
  2. Ensure the result type is always nullable (wrapping non-nullable member types)
  3. Report an error if `?.` is used on a non-nullable type

## Step-by-Step Implementation

### Step 1: Modify TypeChecker.CheckMemberAccess (TypeChecker.cs ~line 1516)

**Current behavior**: Checks member access without considering `IsNullConditional` flag.

**Required changes**:
1. When `memberAccess.IsNullConditional == true`:
   - Validate that `objectType` is a `NullableType`
   - If not nullable, report error: "Null conditional operator '?.' can only be used on nullable types"
   - Unwrap the nullable to get the underlying type for member lookup
   - After finding the member type, wrap it in `NullableType` if not already nullable

2. Code structure:
```csharp
private SemanticType CheckMemberAccess(MemberAccess memberAccess)
{
    // Existing super() handling...

    var objectType = CheckExpression(memberAccess.Object);

    // NEW: Handle null conditional
    SemanticType memberLookupType = objectType;
    if (memberAccess.IsNullConditional)
    {
        if (objectType is not NullableType nullableObjectType)
        {
            AddError(
                $"Null conditional operator '?.' can only be used on nullable types, but got '{objectType.GetDisplayName()}'",
                memberAccess.LineStart, memberAccess.ColumnStart);
            return SemanticType.Unknown;
        }
        // Use underlying type for member lookup
        memberLookupType = nullableObjectType.UnderlyingType;
    }

    // Existing member lookup logic using memberLookupType instead of objectType...

    // After finding member type:
    var memberType = /* result from field/method lookup */;

    // NEW: Wrap result in nullable for null conditional access
    if (memberAccess.IsNullConditional && memberType is not NullableType)
    {
        return new NullableType { UnderlyingType = memberType };
    }
    return memberType;
}
```

### Step 2: Handle Method Calls on Null Conditional Access

When `obj?.method()` is called, the parser creates:
- `FunctionCall` with `Function` being a `MemberAccess` with `IsNullConditional=true`

The `CheckFunctionCall` method should handle this correctly because:
- The `MemberAccess` returns a `FunctionType` (already wrapped in nullable due to Step 1)
- But we need special handling: calling a nullable function type should:
  - Either be an error, OR
  - Result in a nullable return type

**Analysis needed**: Check how C# handles `obj?.Method()` - the entire call is null-conditional, not just the member access. The code generator already handles this via `ConditionalAccessExpression`.

For semantic analysis, when we see a `FunctionCall` where the `Function` is a `MemberAccess` with `IsNullConditional=true`:
- The call itself should return a nullable version of the method's return type
- This may require changes to `CheckFunctionCall` as well

### Step 3: Handle Chained Null Conditional Access

Example: `obj?.a?.b?.c`

Each `?.` in the chain should:
1. Check that the previous result is nullable
2. Return a nullable result

The parser creates nested `MemberAccess` nodes, so this should work naturally with the recursive checking.

## Files to Modify

| File | Changes |
|------|---------|
| `src/Sharpy.Compiler/Semantic/TypeChecker.cs` | Modify `CheckMemberAccess` to handle `IsNullConditional` flag |
| (possibly) `src/Sharpy.Compiler/Semantic/TypeChecker.cs` | Modify `CheckFunctionCall` for method calls via `?.` |

## Test Cases

### Positive Tests (Should Pass)
```python
# Basic null conditional member access
def test_basic():
    name: str? = "hello"
    length = name?.length  # Should be int?

# Null conditional method call
def test_method():
    name: str? = "hello"
    upper = name?.upper()  # Should be str?

# Chained null conditional
def test_chained():
    class Person:
        address: Address?
    class Address:
        city: str?
    p: Person? = None
    city = p?.address?.city  # Should be str?

# Null conditional on explicitly None
def test_none():
    name: str? = None
    length = name?.length  # Should be int?, value is None at runtime

# Mixed with null coalescing
def test_with_coalesce():
    name: str? = "hello"
    length: int = name?.length ?? 0  # int? ?? int -> int
```

### Negative Tests (Should Report Errors)
```python
# Error: Using ?. on non-nullable type
def test_error_non_nullable():
    name: str = "hello"
    length = name?.length  # ERROR: str is not nullable

# Error: Using ?. on primitive
def test_error_primitive():
    x: int = 5
    y = x?.bit_length()  # ERROR: int is not nullable
```

## Potential Risks and Questions

### 1. FunctionType Wrapping
**Question**: When `obj?.method()` is called, should the result be:
- `NullableType(FunctionType)` - the function itself is nullable
- Or should we special-case this to mean "call the function and wrap the result in nullable"?

**C# Behavior**: In C#, `obj?.Method()` means "if obj is null, return null; otherwise call Method() and return its result (which may be wrapped in nullable if it was value type)".

**Resolution**: The semantic analyzer should recognize that when calling a method via `?.`, the result type should be the method's return type wrapped in nullable.

### 2. Property vs Method Access
**Question**: Does the implementation differ between `obj?.property` and `obj?.method()`?

**Resolution**: No, the type wrapping logic is the same. The `MemberAccess` returns the member's type (field type or `FunctionType`), which then gets wrapped in nullable.

### 3. Chaining with Regular Member Access
**Example**: `obj?.a.b` - What if `a` is non-nullable but we access `.b` on it?

**Analysis**: Once we use `?.`, the entire chain becomes nullable. In C#:
- `obj?.a` returns `typeof(a)?`
- `obj?.a.b` returns `typeof(b)?` (b's type wrapped in nullable)

The parser handles this by generating:
```
MemberAccess(
  Object: MemberAccess(Object: obj, Member: "a", IsNullConditional: true),
  Member: "b",
  IsNullConditional: false
)
```

We need to ensure that when the object type is already nullable (from a previous `?.`), regular `.` access:
- Either requires unwrapping (error if not done)
- Or implicitly propagates the nullability

**C# Behavior**: The whole `obj?.a.b` is a single conditional access expression - if `obj` is null, the entire thing is null without evaluating `.b`.

**Resolution**: This requires careful handling in the semantic analyzer to propagate nullability through the chain.

### 4. Index Access with Null Conditional
**Example**: `arr?[0]` - This is `?[` not `?.`

**Status**: This is a separate feature (null conditional element access). Not in scope for this task which focuses on `?.` member access only.

## Implementation Order

1. **First**: Modify `CheckMemberAccess` for basic `?.` on fields
2. **Second**: Add tests for field access
3. **Third**: Handle method calls via `?.` (may need `CheckFunctionCall` changes)
4. **Fourth**: Add tests for method calls
5. **Fifth**: Verify chained access works correctly
6. **Sixth**: Add comprehensive test coverage

## Verification

After implementation:
1. Run existing tests to ensure no regressions
2. Run new null conditional tests
3. Verify generated C# code compiles and runs correctly
