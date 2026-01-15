# Implementation Plan: Task 0.1.6.3 - `self` Handling in Semantic Analysis

## Executive Summary

**Current State:** The Sharpy compiler already has **foundational support for `self`** that covers most use cases. After thorough analysis, this task is primarily a **verification task** with minor enhancements needed.

**What Works:**
- ✅ `self` parameter validation (must be first parameter of instance methods)
- ✅ `self` type resolution (typed as `UserDefinedType { Symbol = _currentClass }`)
- ✅ Member access resolution (`self.field` and `self.method()`)
- ✅ Access level validation for private/protected members
- ✅ Comprehensive existing tests

**What Needs Enhancement:**
- ⚠️ Better error messages for `self` misuse
- ⚠️ Validation that `self` cannot be used outside class methods
- ⚠️ Validation that `self` cannot be reassigned

---

## Step-by-Step Implementation Approach

### Step 1: Add `self` Usage Validation Outside Class Context

**File:** `src/Sharpy.Compiler/Semantic/TypeChecker.cs`

**Current Behavior:** If `self` is used outside a class method, it will fail to resolve as an identifier (no symbol found), giving a generic "undefined identifier" error.

**Enhancement:** Add a specific check in `CheckIdentifier()` to provide a clear error message when `self` is used outside a class context.

```csharp
// In CheckIdentifier() method, around line 970
if (identifier.Name == "self")
{
    if (_currentClass == null)
    {
        AddError("'self' can only be used inside instance methods",
            identifier.LineStart, identifier.ColumnStart);
        return SemanticType.Unknown;
    }
    // Normal identifier lookup follows...
}
```

**Risk Level:** Low - Adds validation without changing existing behavior

---

### Step 2: Validate `self` Cannot Be Reassigned

**File:** `src/Sharpy.Compiler/Semantic/TypeChecker.cs`

**Current Behavior:** The type checker may allow `self = something` without specific error.

**Enhancement:** Add validation in `CheckAssignment()` to prevent reassigning `self`.

```csharp
// In CheckAssignment() method
if (assignment.Target is Identifier id && id.Name == "self")
{
    AddError("Cannot reassign 'self'",
        assignment.LineStart, assignment.ColumnStart);
    return SemanticType.Unknown;
}
```

**Risk Level:** Low - Pure validation addition

---

### Step 3: Ensure `self` Is Registered in Method Scope

**File:** `src/Sharpy.Compiler/Semantic/TypeChecker.cs`

**Current Behavior:** The `self` parameter is already typed correctly at lines 241-245, but verify it's properly registered in the symbol table during function checking.

**Verification Points:**
- Line 241-245: `self` parameter gets `UserDefinedType { Symbol = _currentClass }`
- Line 233-237: All parameters are registered via `_symbolTable.CurrentScope.Define()`

**Action:** Verify this works correctly through tests. No code changes expected.

---

### Step 4: Verify `self.x` Field Access Resolution

**File:** `src/Sharpy.Compiler/Semantic/TypeChecker.cs`

**Current Behavior:** `CheckMemberAccess()` at lines 1343-1377 already handles this:
1. `CheckExpression(memberAccess.Object)` resolves `self` to class type
2. Field lookup happens on the class type
3. Access validation occurs

**Verification Points:**
- Confirm `self.field` resolves to correct field type
- Confirm `self.method()` resolves to correct method signature
- Confirm access modifiers are enforced (private fields accessible via self)

**Action:** Add/verify tests. No code changes expected.

---

### Step 5: Verify `self.x` Method Call Resolution

**File:** `src/Sharpy.Compiler/Semantic/TypeChecker.cs`

**Current Behavior:** Method calls on `self` go through:
1. `CheckCall()` → detects member access as callee
2. `CheckMemberAccess()` → resolves method
3. Returns `FunctionType` for method reference

**Action:** Add tests for `self.method(args)` calls. No code changes expected.

---

### Step 6: Add Comprehensive Tests

**File:** `src/Sharpy.Compiler.Tests/Semantic/SelfHandlingTests.cs` (new file)

**Test Categories:**

#### 6.1 Positive Tests (should pass)
```csharp
[Fact]
public void SelfParameter_ResolesToClassType()
{
    var source = @"
class Person:
    name: str

    def get_name(self) -> str:
        return self.name
";
    // Verify no errors
}

[Fact]
public void SelfFieldAccess_ResolvesCorrectly()
{
    var source = @"
class Point:
    x: int
    y: int

    def magnitude(self) -> int:
        return self.x * self.x + self.y * self.y
";
    // Verify self.x resolves to int
}

[Fact]
public void SelfMethodCall_ResolvesCorrectly()
{
    var source = @"
class Calculator:
    def add(self, a: int, b: int) -> int:
        return a + b

    def add_three(self, a: int, b: int, c: int) -> int:
        return self.add(a, b) + c
";
    // Verify self.add() call resolves correctly
}

[Fact]
public void SelfInInit_WorksCorrectly()
{
    var source = @"
class Person:
    name: str
    age: int

    def __init__(self, name: str, age: int):
        self.name = name
        self.age = age
";
    // Verify field assignments work
}
```

#### 6.2 Negative Tests (should produce errors)
```csharp
[Fact]
public void SelfOutsideClass_ProducesError()
{
    var source = @"
def greet():
    print(self.name)  # Error: self outside class
";
    // Verify error message
}

[Fact]
public void SelfReassignment_ProducesError()
{
    var source = @"
class Person:
    def reset(self):
        self = Person()  # Error: cannot reassign self
";
    // Verify error message
}

[Fact]
public void SelfWithoutMethod_ProducesError()
{
    var source = @"
class Person:
    name: str = self.name  # Error: self at class level
";
    // Verify error message
}

[Fact]
public void WrongFirstParameter_ProducesError()
{
    var source = @"
class Person:
    def greet(other):  # Error: should be 'self'
        pass
";
    // Verify error message (already implemented)
}

[Fact]
public void SelfAccessNonexistentField_ProducesError()
{
    var source = @"
class Person:
    name: str

    def greet(self):
        return self.age  # Error: no field 'age'
";
    // Verify error message
}
```

---

## Key Files to Modify

| File | Changes | Priority |
|------|---------|----------|
| `src/Sharpy.Compiler/Semantic/TypeChecker.cs` | Add `self` validation in `CheckIdentifier()` and `CheckAssignment()` | High |
| `src/Sharpy.Compiler.Tests/Semantic/SelfHandlingTests.cs` | New comprehensive test file | High |
| `src/Sharpy.Compiler/Semantic/SymbolTable.cs` | No changes expected | - |

---

## Tests to Verify

### Existing Tests (should continue passing)
- `TypeCheckerTests.ChecksClassMethods` - basic self usage
- `SemanticAnalyzerNegativeTests.RejectsWrongFirstParameterName`
- `SemanticAnalyzerNegativeTests.RejectsInstanceMethodWithNoParams`
- `SemanticAnalyzerNegativeTests.AllowsCorrectSelfParameter`
- All NameResolver tests using `self.field` patterns

### New Tests to Add
1. `SelfParameter_ResolesToClassType`
2. `SelfFieldAccess_ResolvesCorrectly`
3. `SelfMethodCall_ResolvesCorrectly`
4. `SelfInInit_WorksCorrectly`
5. `SelfOutsideClass_ProducesError`
6. `SelfReassignment_ProducesError`
7. `SelfWithoutMethod_ProducesError`
8. `SelfAccessNonexistentField_ProducesError`
9. `PrivateFieldAccess_ViaSelft_Allowed`
10. `ProtectedFieldAccess_ViaSelf_Allowed`

### Run All Tests
```bash
dotnet test src/Sharpy.Compiler.Tests --filter "FullyQualifiedName~Self"
dotnet test src/Sharpy.Compiler.Tests --filter "FullyQualifiedName~TypeChecker"
dotnet test src/Sharpy.Compiler.Tests  # Full suite
```

---

## Potential Risks and Questions

### Low Risk
1. **Adding validation to `CheckIdentifier()`** - May need to ensure the check happens before symbol lookup to provide the specific error message

2. **Self reassignment check** - Need to verify AST structure for assignment targets

### Questions to Clarify
1. **Should `self` be allowed in class-level expressions?**
   - Example: `class Foo: x: int = self.compute()`
   - Recommendation: No, this should error

2. **Should `self` be usable in static methods with explicit error?**
   - Example: `@static def helper(self): pass`
   - Current: The `self` parameter check already catches "method must have self"
   - This is the inverse: static method incorrectly has `self`
   - Recommendation: Add explicit check that static methods cannot have `self` parameter

3. **Should nested function inside method access `self`?**
   - Example:
     ```python
     def outer(self):
         def inner():
             return self.name  # Should this work?
     ```
   - Python behavior: Yes, closures capture `self`
   - Recommendation: Verify current behavior matches Python (likely already works due to scope chain)

---

## Implementation Order

1. **Run existing tests** to establish baseline (15 min)
2. **Create `SelfHandlingTests.cs`** with positive tests (30 min)
3. **Add negative tests** to document expected errors (30 min)
4. **Implement `self` outside class validation** in `CheckIdentifier()` (30 min)
5. **Implement `self` reassignment validation** in `CheckAssignment()` (30 min)
6. **Run all tests** and fix any issues (30 min)
7. **Verify edge cases** (nested functions, inheritance) (30 min)

**Total Estimated Time:** 3 hours

---

## Success Criteria

1. ✅ All existing tests continue to pass
2. ✅ `self` outside class context produces clear error
3. ✅ `self` reassignment produces clear error
4. ✅ `self.field` resolves to correct field type
5. ✅ `self.method()` resolves to correct method type
6. ✅ Access modifiers enforced for `self.field` access
7. ✅ New comprehensive test suite passes
