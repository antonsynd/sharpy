# Implementation Plan: Task 0.1.6.7 - Static Method Detection and Code Generation

## Summary

Implement static method detection based on the **absence of `self` parameter** per the language specification, rather than relying solely on decorators.

## Current State Analysis

### Problem
The current implementation uses **decorator-based detection** (`@static` or `@staticmethod`), but the specification states:

> "Sharpy has no annotation/decorator/keyword for static methods. Static methods on a class, struct, etc. are like regular methods, except they do not have a `self` parameter"

### Current Implementation
- **NameResolver.cs:307**: `bool isStatic = method.Decorators.Any(d => d.Name == "static" || d.Name == "staticmethod");`
- **TypeChecker.cs:211-226**: Validates that instance methods MUST have `self` - but this validation happens unconditionally for ALL methods in a class
- **RoslynEmitter.cs:548-550**: Generates `static` keyword based on decorators only

### Bug in TypeChecker
The TypeChecker currently requires ALL methods in a class to have a `self` parameter (lines 211-226). This is incorrect - static methods should NOT have `self`.

---

## Step-by-Step Implementation

### Step 1: Update NameResolver.cs - Detect Static Methods by Absence of `self`

**File:** `src/Sharpy.Compiler/Semantic/NameResolver.cs`
**Line:** ~307

**Change:**
```csharp
// OLD:
bool isStatic = method.Decorators.Any(d => d.Name == "static" || d.Name == "staticmethod");

// NEW:
// Primary mechanism: method is static if it doesn't have 'self' as first parameter
// Decorators are optional/redundant but still supported for compatibility
bool hasSelfParameter = method.Parameters.Count > 0 &&
    method.Parameters[0].Name == "self" &&
    method.Parameters[0].Type == null;  // self has no type annotation
bool hasStaticDecorator = method.Decorators.Any(d => d.Name == "static" || d.Name == "staticmethod");
bool isStatic = !hasSelfParameter || hasStaticDecorator;
```

**Rationale:** Per spec, a method without `self` as the first untyped parameter is static.

---

### Step 2: Update TypeChecker.cs - Remove Incorrect Validation

**File:** `src/Sharpy.Compiler/Semantic/TypeChecker.cs`
**Lines:** ~211-226

**Change:** The validation that all methods must have `self` should only apply to **instance methods**, not static methods.

```csharp
// OLD (unconditional validation):
if (_currentClass != null && functionDef.Parameters.Count > 0)
{
    if (functionDef.Parameters[0].Name != "self")
    {
        AddError($"Instance method '{functionDef.Name}' must have 'self' as the first parameter", ...);
    }
}
else if (_currentClass != null && functionDef.Parameters.Count == 0)
{
    AddError($"Instance method '{functionDef.Name}' is missing required 'self' parameter", ...);
}

// NEW (check if method is static first):
if (_currentClass != null)
{
    // Determine if this is a static method (no 'self' parameter)
    bool hasSelfParameter = functionDef.Parameters.Count > 0 &&
        functionDef.Parameters[0].Name == "self" &&
        functionDef.Parameters[0].Type == null;
    bool hasStaticDecorator = functionDef.Decorators.Any(d =>
        d.Name == "static" || d.Name == "staticmethod");
    bool isStaticMethod = !hasSelfParameter || hasStaticDecorator;

    // Only validate 'self' parameter for instance methods
    if (!isStaticMethod)
    {
        if (functionDef.Parameters.Count == 0 || functionDef.Parameters[0].Name != "self")
        {
            AddError($"Instance method '{functionDef.Name}' must have 'self' as the first parameter",
                functionDef.LineStart, functionDef.ColumnStart);
        }
    }
}
```

---

### Step 3: Update RoslynEmitter.cs - Generate Static Based on Symbol

**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

The emitter currently uses `GenerateMethodModifiersFromDecorators()` which only checks decorators. We need to also check the `IsStatic` flag from the symbol.

**Option A (Preferred):** Pass the `FunctionSymbol` to `GenerateClassMethod` and use its `IsStatic` property.

**Option B:** Check for absence of `self` parameter directly in the emitter.

**Implementation for Option B** (simpler, self-contained):

In `GenerateClassMethod()` around line 1061, modify the modifiers logic:

```csharp
// Process decorators to determine modifiers
var modifiers = GenerateMethodModifiersFromDecorators(func.Decorators);

// Check if this is a static method (no 'self' parameter with no type annotation)
bool hasSelfParameter = func.Parameters.Count > 0 &&
    func.Parameters[0].Name == "self" &&
    func.Parameters[0].Type == null;
bool isStatic = !hasSelfParameter;

// Add static keyword if method is static and not already present
if (isStatic && !modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword)))
{
    modifiers = modifiers.Add(Token(SyntaxKind.StaticKeyword));
}
```

---

### Step 4: Add Warning for Redundant `@static` Decorator (Optional)

Since the spec says decorators are "valid but optional/redundant", consider adding a warning when `@static` is used on a method that's already static (no `self`).

**File:** `src/Sharpy.Compiler/Semantic/NameResolver.cs` or `TypeChecker.cs`

```csharp
// If method has @static decorator but no self parameter, warn about redundancy
if (hasStaticDecorator && !hasSelfParameter)
{
    AddWarning($"Method '{method.Name}' is already static (no 'self' parameter). The @static decorator is redundant.",
        method.LineStart, method.ColumnStart);
}
```

---

## Key Files to Modify

| File | Changes |
|------|---------|
| `src/Sharpy.Compiler/Semantic/NameResolver.cs` | Update `IsStatic` detection logic (line ~307) |
| `src/Sharpy.Compiler/Semantic/TypeChecker.cs` | Fix validation to skip static methods (lines ~211-226) |
| `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` | Add static keyword based on `self` absence (line ~1061) |

---

## Tests to Add/Verify

### 1. New Tests for `self`-based Static Detection

**File:** `src/Sharpy.Compiler.Tests/Semantic/NameResolverTests.cs`

```csharp
[Fact]
public void TestStaticMethodWithoutSelfParameter()
{
    var source = @"
class Math:
    def add(a: int, b: int) -> int:  # Static - no self parameter
        return a + b

    def instance_method(self) -> int:  # Instance - has self
        return 1
";
    var (resolver, module, symbolTable) = CreateResolver(source);
    resolver.ResolveDeclarations(module);

    Assert.Empty(resolver.Errors);

    var mathType = symbolTable.LookupType("Math");
    var addMethod = mathType.Methods.First(m => m.Name == "add");
    Assert.True(addMethod.IsStatic);  // No self = static

    var instanceMethod = mathType.Methods.First(m => m.Name == "instance_method");
    Assert.False(instanceMethod.IsStatic);  // Has self = instance
}
```

### 2. Code Generation Test

**File:** `src/Sharpy.Compiler.Tests/CodeGen/RoslynEmitterDefinitionTests.cs`

```csharp
[Fact]
public void GenerateMethod_WithoutSelfParameter_GeneratesStaticMethod()
{
    var source = @"
class Math:
    def add(a: int, b: int) -> int:
        return a + b
";
    // Compile and verify output contains "public static int Add(int a, int b)"
}
```

### 3. Integration Test - Mixed Methods

```csharp
[Fact]
public void GenerateClass_WithMixedStaticAndInstanceMethods()
{
    var source = @"
struct Foo:
    x: int

    def name() -> str:  # static
        return ""Foo""

    def get_x(self) -> int:  # instance
        return self.x
";
    // Verify: static method has `static` keyword, instance method doesn't
}
```

### 4. Edge Case - `self` with Type Annotation

```csharp
[Fact]
public void TestSelfWithTypeAnnotation_TreatedAsRegularParameter()
{
    var source = @"
class Foo:
    def weird(self: int) -> int:  # 'self' with type = NOT the special self
        return self
";
    // This should be treated as a static method with a parameter named 'self'
    // Per spec: "self parameter (which has no type annotation)"
}
```

---

## Potential Risks and Questions

### Risks

1. **Breaking Change:** Existing code using `@static` decorators will still work (decorator sets `IsStatic = true`), but code that relies on the TypeChecker error for missing `self` may behave differently.

2. **`self` with Type Annotation:** Per spec, `self` must have NO type annotation to be the special instance reference. A method like `def foo(self: int)` would be treated as static with a parameter named `self`. Need to verify this edge case is handled correctly.

3. **Interface Methods:** Spec says "interfaces cannot have static methods; all interface methods are instance methods." Need to add validation that interface methods MUST have `self`.

### Questions to Clarify

1. **Warning for Redundant Decorator?** Should we warn when `@static` is used on a method that's already static due to no `self`? The task description says "Valid but OPTIONAL/redundant for methods".

2. **Constructors (`__init__`):** Should `__init__` always be treated as instance method? Current code registers it specially. Need to ensure static detection doesn't affect constructors.

3. **`cls` Parameter:** For classmethods, is `cls` treated like `self`? The emitter currently filters both `self` and `cls` (line 1079-1080). Need to clarify if `cls` makes a method non-static.

---

## Validation Checklist

- [ ] Static methods (no `self`) get `IsStatic = true` in NameResolver
- [ ] TypeChecker doesn't error on static methods without `self`
- [ ] RoslynEmitter generates `static` keyword for methods without `self`
- [ ] Instance methods (with `self`) work unchanged
- [ ] Decorators (`@static`, `@staticmethod`) still work for compatibility
- [ ] Interface methods validate that `self` is required
- [ ] Edge case: `self` WITH type annotation treated as regular parameter
- [ ] All existing tests pass
- [ ] New tests added for `self`-based detection
