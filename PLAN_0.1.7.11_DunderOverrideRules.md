# Implementation Plan: Task 0.1.7.11 - Dunder Method Override Rules

## Executive Summary

Implement semantic validation that enforces `@override` decorator requirements for dunder methods that override `System.Object` methods (`__str__` → `ToString()`, `__eq__` → `Equals()`, `__hash__` → `GetHashCode()`).

## Current State Analysis

### What Works Now
- **RoslynEmitter (lines 1109-1118)**: Auto-adds `override` keyword in C# for `__str__`, `__eq__`, `__hash__`, `__repr__`
- **TypeChecker (line 226)**: Tracks `_currentMethodIsOverride` flag from decorator
- **ProtocolRegistry**: Maps `__str__` → `ToString`, `__hash__` → `GetHashCode`
- **OperatorSignatureValidator**: Handles `__eq__` as comparison operator

### Gap (Per Specification)
The `dunder_invocation_rules.md` spec (lines 105, 114-129) **requires** `@override` decorator:
```python
# Must use @override since __str__ is inherited from System.Object (ToString())
@override
def __str__(self) -> str:
    return f"MyClass({self.value})"
```

Currently, the emitter silently adds the C# `override` keyword, but no semantic error is raised if the user forgets `@override`.

---

## Step-by-Step Implementation

### Step 1: Define Object Dunder Constants

**File**: `src/Sharpy.Compiler/Semantic/ProtocolRegistry.cs`

Add a helper to identify dunders that override `System.Object`:

```csharp
/// <summary>
/// Dunder methods that implicitly override System.Object virtual methods.
/// These require @override decorator in Sharpy source code.
/// </summary>
public static class ObjectDunders
{
    public static readonly IReadOnlySet<string> Methods = new HashSet<string>
    {
        "__str__",   // → Object.ToString()
        "__eq__",    // → Object.Equals()
        "__hash__"   // → Object.GetHashCode()
    };

    public static bool RequiresOverride(string dunderName)
        => Methods.Contains(dunderName);
}
```

**Alternative**: Add a static method directly to `ProtocolRegistry`:
```csharp
public static bool IsObjectOverrideDunder(string methodName)
    => methodName is "__str__" or "__eq__" or "__hash__";
```

---

### Step 2: Add Semantic Validation in TypeChecker

**File**: `src/Sharpy.Compiler/Semantic/TypeChecker.cs`

In the `CheckFunction` method (around line 230, after setting `_currentMethodIsDunder`), add validation:

```csharp
// Validate @override is required for dunders that override System.Object methods
if (_currentClass != null && _currentMethodIsDunder)
{
    bool requiresOverride = ProtocolRegistry.IsObjectOverrideDunder(functionDef.Name);

    if (requiresOverride && !_currentMethodIsOverride)
    {
        AddError(
            $"Dunder method '{functionDef.Name}' overrides a System.Object method and requires the @override decorator",
            functionDef.LineStart,
            functionDef.ColumnStart);
    }
}
```

**Location**: After line 229 (after `_currentMethodIsDunder = IsDunderMethod(functionDef.Name);`)

---

### Step 3: Handle __repr__ Special Case

`__repr__` maps to a custom `__Repr__()` method, NOT `Object.ToString()`. Per current emitter logic:
- `__repr__` generates a `ToString()` override (line 1111)
- This is debatable - consider whether `__repr__` should require `@override`

**Decision needed**: Should `__repr__` require `@override`?
- **Option A (Conservative)**: No - `__repr__` generates `__Repr__()` method, not a true override
- **Option B (Emitter-aligned)**: Yes - emitter adds `override` for ToString, so require decorator

**Recommendation**: Option A (exclude `__repr__` from override requirement) since it generates a distinct method name per `ProtocolRegistry.ClrMethodName: null`.

---

### Step 4: Update RoslynEmitter (Cleanup/Consistency)

**File**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

The emitter already handles this correctly (lines 1109-1118). No changes needed unless we want to:
1. Add a debug assertion that `@override` is present (semantic check should catch this)
2. Remove the auto-add logic to make semantic errors more visible during development

**Recommendation**: Keep emitter logic as-is for robustness (belt-and-suspenders).

---

## Files to Modify

| File | Change |
|------|--------|
| `src/Sharpy.Compiler/Semantic/ProtocolRegistry.cs` | Add `IsObjectOverrideDunder()` helper method |
| `src/Sharpy.Compiler/Semantic/TypeChecker.cs` | Add validation in `CheckFunction()` for missing `@override` |

---

## Tests to Add

**File**: `src/Sharpy.Compiler.Tests/Semantic/TypeCheckerTests.cs` (or new `DunderOverrideTests.cs`)

### Positive Tests (Should Pass)
```csharp
[Fact]
public void DunderStr_WithOverrideDecorator_Succeeds()
{
    var code = @"
class Foo:
    @override
    def __str__(self) -> str:
        return ""Foo""
";
    // Assert no errors
}

[Fact]
public void DunderEq_WithOverrideDecorator_Succeeds()
{
    var code = @"
class Foo:
    @override
    def __eq__(self, other: object) -> bool:
        return True
";
    // Assert no errors
}

[Fact]
public void DunderHash_WithOverrideDecorator_Succeeds()
{
    var code = @"
class Foo:
    @override
    def __hash__(self) -> int:
        return 42
";
    // Assert no errors
}
```

### Negative Tests (Should Fail with Error)
```csharp
[Fact]
public void DunderStr_WithoutOverrideDecorator_ReportsError()
{
    var code = @"
class Foo:
    def __str__(self) -> str:
        return ""Foo""
";
    // Assert error: "__str__ overrides System.Object method and requires @override"
}

[Fact]
public void DunderEq_WithoutOverrideDecorator_ReportsError()
{
    var code = @"
class Foo:
    def __eq__(self, other: object) -> bool:
        return True
";
    // Assert error: "__eq__ overrides System.Object method and requires @override"
}

[Fact]
public void DunderHash_WithoutOverrideDecorator_ReportsError()
{
    var code = @"
class Foo:
    def __hash__(self) -> int:
        return 42
";
    // Assert error: "__hash__ overrides System.Object method and requires @override"
}
```

### Edge Case Tests
```csharp
[Fact]
public void DunderLen_WithoutOverrideDecorator_Succeeds()
{
    // __len__ does NOT override Object method, no @override needed
    var code = @"
class Foo:
    def __len__(self) -> int:
        return 0
";
    // Assert no errors
}

[Fact]
public void DunderRepr_WithoutOverrideDecorator_Succeeds()
{
    // __repr__ generates __Repr__(), not ToString() override
    var code = @"
class Foo:
    def __repr__(self) -> str:
        return ""Foo()""
";
    // Assert no errors (per Option A)
}

[Fact]
public void DunderAdd_WithoutOverrideDecorator_Succeeds()
{
    // Operator dunders don't override Object methods
    var code = @"
class Foo:
    def __add__(self, other: Foo) -> Foo:
        return Foo()
";
    // Assert no errors
}
```

---

## Potential Risks & Questions

### 1. **Breaking Change**
This will introduce compilation errors for existing code that defines `__str__`, `__eq__`, or `__hash__` without `@override`.

**Mitigation**: Document in release notes; error message clearly states the fix.

### 2. **Interface Implementation Interaction**
If a class implements an interface that has a `__str__` protocol method, does `@override` apply?

**Analysis**: The requirement is specifically for overriding `System.Object` methods. Interface implementation uses different C# semantics (implicit/explicit implementation). The current scope only covers `Object` overrides.

### 3. **Child Class Overriding Parent Dunder**
```python
class Parent:
    @override
    def __str__(self) -> str:
        return "Parent"

class Child(Parent):
    @override  # Is this required?
    def __str__(self) -> str:
        return "Child"
```

**Analysis**: Yes, `@override` is still required because:
- It overrides a virtual method (Parent's `__str__`)
- This is consistent with C# semantics where `override` is always required

### 4. **Abstract Base Class Scenario**
```python
@abstract
class Base:
    @abstract
    def __str__(self) -> str:
        ...

class Concrete(Base):
    @override  # Required even when implementing abstract?
    def __str__(self) -> str:
        return "Concrete"
```

**Analysis**: Yes, per C# semantics, implementing an abstract method requires `override`.

---

## Implementation Order

1. Add `IsObjectOverrideDunder()` to `ProtocolRegistry.cs` (5 min)
2. Add validation in `TypeChecker.CheckFunction()` (10 min)
3. Write unit tests (20 min)
4. Run full test suite and fix any regressions (15 min)
5. Update any affected sample code or documentation (10 min)

---

## Definition of Done

- [ ] `__str__`, `__eq__`, `__hash__` without `@override` produce semantic error
- [ ] `__str__`, `__eq__`, `__hash__` with `@override` compile successfully
- [ ] Other dunders (`__len__`, `__add__`, etc.) don't require `@override`
- [ ] All existing tests pass
- [ ] New unit tests for positive and negative cases
- [ ] Error message is clear and actionable
