# Task List: Fix Type Coercion and Method Override Bugs

## Summary

Two bugs were discovered during cross-module inheritance testing:
1. **Issue 1**: `int to str` generates invalid C# code `(string)value` instead of being rejected at compile time
2. **Issue 2**: `@override` on derived class methods fails when base class method lacks `@virtual`

---

## Issue 1: Type Coercion Bug (`int to str`)

### Problem Statement

The Sharpy code:
```python
def to_string(self) -> str:
    return self.name + "(" + (self.id to str) + ")"
```

Produces C# error: `CS0030: Cannot convert type 'int' to 'string'`

### Root Cause Analysis

According to `docs/language_specification/type_casting.md`:
- The `to` operator is for **type casting** (downcast, interface cast, unboxing), NOT type conversion
- `int to str` should be a **compile-time error** per the spec's "Invalid Casts" section
- The spec suggests using `str(self.id)` instead

Two failures occurred:
1. **Semantic Analysis** didn't reject the invalid cast
2. **Code Generation** produced invalid C# `(string)value` as a fallback

### Correct Fix

The user code should be changed to:
```python
def to_string(self) -> str:
    return self.name + "(" + str(self.id) + ")"
```

However, the compiler should provide a helpful error message when this pattern is attempted.

---

### Task 1.1: Add Semantic Validation for Invalid Type Coercions

**File**: `src/Sharpy.Compiler/Semantic/TypeChecker.cs` (or create `TypeCoercionValidator.cs`)

**Description**: Add validation to reject `to` casts between incompatible types at compile time.

**Implementation Steps**:

1. Create a new method `ValidateTypeCoercion(TypeCoercion node, SemanticType sourceType, SemanticType targetType)`:
   ```csharp
   private void ValidateTypeCoercion(TypeCoercion node, SemanticType sourceType, SemanticType targetType)
   {
       // Check if this is a valid cast scenario:
       // 1. Downcast (derived to base) - always valid
       // 2. Upcast (base to derived) - runtime check
       // 3. Interface cast - runtime check
       // 4. Unboxing (object to value type) - runtime check
       // 5. Numeric narrowing (e.g., long to int) - valid with overflow check

       // Invalid: primitives to string (except object)
       if (IsNumericPrimitive(sourceType) && IsStringType(targetType))
       {
           ReportError(node, $"Cannot cast {sourceType.GetDisplayName()} to {targetType.GetDisplayName()}. Use str({GetExpressionText(node.Value)}) instead.");
       }

       // Invalid: unrelated class types
       if (!CanPotentiallyCast(sourceType, targetType))
       {
           ReportError(node, $"Cannot cast {sourceType.GetDisplayName()} to {targetType.GetDisplayName()} (no inheritance relationship).");
       }
   }
   ```

2. Add helper methods:
   ```csharp
   private bool IsNumericPrimitive(SemanticType type)
   {
       if (type is not BuiltinType builtin) return false;
       var info = PrimitiveCatalog.GetByName(builtin.Name);
       return info?.Kind != NumericKind.None && info?.ClrType != typeof(string);
   }

   private bool IsStringType(SemanticType type)
   {
       return type is BuiltinType builtin && builtin.Name == "str";
   }

   private bool CanPotentiallyCast(SemanticType source, SemanticType target)
   {
       // Check for inheritance relationship, interface implementation, or unboxing potential
       // Return true if cast COULD succeed at runtime
       // Return false if cast is statically impossible
   }
   ```

3. Call `ValidateTypeCoercion` when visiting `TypeCoercion` nodes in the type checker.

**Verification**:
```sharpy
# Should produce compile error:
x: int = 42
s = x to str  # ERROR: Cannot cast int to str. Use str(x) instead.

# Should be valid:
s = str(x)  # OK
```

**Estimated Effort**: 2-3 hours

---

### Task 1.2: Add Fallback Code Generation for Primitive Conversions (Optional/Alternative)

> **Note**: This task is an ALTERNATIVE approach if you want `to str` to work for primitives. The spec suggests this should be an error, so Task 1.1 is the recommended approach.

**File**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs`

**Description**: If you decide `int to str` should work (not spec-compliant), modify `GenerateTypeCoercion` to handle primitive-to-string conversions.

**Implementation**:

```csharp
private ExpressionSyntax GenerateTypeCoercion(TypeCoercion coercion)
{
    var value = GenerateExpression(coercion.Value);
    var targetTypeName = coercion.TargetType.Name;

    // Special case: converting to str - use ToString()
    if (targetTypeName == "str" || targetTypeName == "string")
    {
        // Check if source is a value type that needs ToString()
        var sourceType = _context.GetExpressionType(coercion.Value);
        if (sourceType is BuiltinType builtin)
        {
            var info = PrimitiveCatalog.GetByName(builtin.Name);
            if (info != null && info.ClrType != typeof(string))
            {
                // Generate: value.ToString()
                return InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        value,
                        IdentifierName("ToString")));
            }
        }
    }

    // ... rest of existing implementation
}
```

**Verification**: `(42 to str)` should generate `42.ToString()`

**Estimated Effort**: 1-2 hours

---

### Task 1.3: Add Unit Tests for Type Coercion Validation

**File**: `src/Sharpy.Compiler.Tests/Semantic/TypeCoercionValidationTests.cs` (new file)

**Description**: Add tests to verify invalid casts are rejected.

**Test Cases**:
```csharp
[Theory]
[InlineData("int", "str", false)]      // primitives to string - invalid
[InlineData("long", "str", false)]     // primitives to string - invalid
[InlineData("float", "str", false)]    // primitives to string - invalid
[InlineData("long", "int", true)]      // numeric narrowing - valid
[InlineData("int", "long", true)]      // numeric widening - valid
[InlineData("object", "int", true)]    // unboxing - valid
[InlineData("Animal", "Dog", true)]    // downcast - valid
[InlineData("Dog", "Cat", false)]      // unrelated types - invalid
public void TypeCoercion_Validation(string sourceType, string targetType, bool shouldBeValid)
```

**Estimated Effort**: 1-2 hours

---

## Issue 2: Method Override Bug (`@override` without `@virtual`)

### Problem Statement

The Sharpy code in `user.spy`:
```python
@override
def to_string(self) -> str:
    base_str: str = super().to_string()
    return base_str + "<" + self.email + ">"
```

Produces C# error: `CS0506: 'User.ToString()': cannot override inherited member 'NamedEntity.ToString()' because it is not marked virtual`

### Root Cause Analysis

1. `NamedEntity.to_string` has **no decorator** → generates as plain C# method (no `virtual`)
2. `User.to_string` has `@override` → generates with `override` keyword
3. C# requires the base method to be `virtual` or `abstract` to override it

**Note**: The method `to_string` is NOT the dunder `__str__`, so it doesn't receive automatic override handling.

### Design Decision Required

Choose one approach:

| Option | Description | Pros | Cons |
|--------|-------------|------|------|
| **A** | Require explicit `@virtual` on base class methods | Matches C# semantics | Verbose, easy to forget |
| **B** | Make all methods implicitly `virtual` | Matches Python semantics | Performance overhead, different from C# |
| **C** | Add semantic validation to catch missing `@virtual` | Best of both worlds | More compiler work |

**Recommended**: Option C - Add semantic validation

---

### Task 2.1: Add Semantic Validation for Override/Virtual Mismatch

**File**: `src/Sharpy.Compiler/Semantic/InheritanceValidator.cs` (new file or add to existing)

**Description**: Detect when `@override` is used but the base method isn't `virtual`, `abstract`, or `override`.

**Implementation Steps**:

1. During semantic analysis, when processing a class with a method marked `@override`:
   ```csharp
   private void ValidateOverrideDecorator(ClassDef classDef, FunctionDef method)
   {
       if (!HasDecorator(method.Decorators, "override"))
           return;

       // Find the base class method
       var baseMethod = FindBaseMethod(classDef, method.Name);

       if (baseMethod == null)
       {
           ReportError(method, $"Method '{method.Name}' is marked @override but no matching method exists in base class.");
           return;
       }

       // Check if base method is virtual, abstract, or override
       if (!IsVirtualOrAbstract(baseMethod))
       {
           ReportError(method,
               $"Cannot override '{method.Name}' because the base class method is not marked @virtual or @abstract. " +
               $"Add @virtual to the method in '{GetBaseClassName(classDef)}'.");
       }
   }
   ```

2. Add helper methods:
   ```csharp
   private FunctionDef? FindBaseMethod(ClassDef classDef, string methodName)
   {
       // Walk up the inheritance chain to find a method with matching name
   }

   private bool IsVirtualOrAbstract(FunctionDef method)
   {
       return HasDecorator(method.Decorators, "virtual") ||
              HasDecorator(method.Decorators, "abstract") ||
              HasDecorator(method.Decorators, "override");
   }
   ```

**Verification**:
```sharpy
# Should produce helpful error:
class Base:
    def method(self) -> str:  # Missing @virtual
        return "base"

class Derived(Base):
    @override
    def method(self) -> str:  # ERROR: Cannot override 'method' because base method is not @virtual
        return "derived"
```

**Estimated Effort**: 3-4 hours

---

### Task 2.2: Fix Test Fixture - Add @virtual to Base Class

**File**: `src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/mixed_inheritance_interface/named_entity.spy`

**Description**: Add `@virtual` decorator to the base class method as the immediate fix.

**Change**:
```python
# Before:
def to_string(self) -> str:
    return self.name + "(" + str(self.id) + ")"

# After:
@virtual
def to_string(self) -> str:
    return self.name + "(" + str(self.id) + ")"
```

**Note**: Also fix the `to str` issue:
```python
# Before:
return self.name + "(" + (self.id to str) + ")"

# After:
return self.name + "(" + str(self.id) + ")"
```

**Estimated Effort**: 5 minutes

---

### Task 2.3: Add Unit Tests for Override Validation

**File**: `src/Sharpy.Compiler.Tests/Semantic/OverrideValidationTests.cs` (new file)

**Description**: Add tests to verify `@override` / `@virtual` validation.

**Test Cases**:
```csharp
[Fact]
public void Override_WithoutVirtualBase_ProducesError()
{
    var source = @"
class Base:
    def method(self) -> str:
        return 'base'

class Derived(Base):
    @override
    def method(self) -> str:
        return 'derived'
";
    var errors = CompileAndGetErrors(source);
    errors.Should().ContainSingle(e => e.Contains("not marked @virtual"));
}

[Fact]
public void Override_WithVirtualBase_Succeeds()
{
    var source = @"
class Base:
    @virtual
    def method(self) -> str:
        return 'base'

class Derived(Base):
    @override
    def method(self) -> str:
        return 'derived'
";
    var errors = CompileAndGetErrors(source);
    errors.Should().BeEmpty();
}

[Fact]
public void Override_WithAbstractBase_Succeeds()
{
    var source = @"
@abstract
class Base:
    @abstract
    def method(self) -> str:
        ...

class Derived(Base):
    @override
    def method(self) -> str:
        return 'derived'
";
    var errors = CompileAndGetErrors(source);
    errors.Should().BeEmpty();
}
```

**Estimated Effort**: 1-2 hours

---

### Task 2.4 (Optional): Consider Implicit Virtual for All Methods

**Description**: If you want Python-like behavior where all methods are implicitly virtual, modify the code generator.

**File**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.ClassMembers.cs`

**Change in `GenerateClassMethod`**:
```csharp
// After processing decorators, add virtual by default for instance methods
// unless they're already static, abstract, override, or explicitly sealed
if (!modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword) ||
                         m.IsKind(SyntaxKind.AbstractKeyword) ||
                         m.IsKind(SyntaxKind.OverrideKeyword) ||
                         m.IsKind(SyntaxKind.SealedKeyword)))
{
    modifiers = modifiers.Add(Token(SyntaxKind.VirtualKeyword));
}
```

**Trade-offs**:
- ✅ Matches Python behavior (all methods overridable)
- ❌ Performance overhead (virtual dispatch even when not needed)
- ❌ Different from C# conventions

**Estimated Effort**: 1 hour (but requires design decision approval)

---

## Summary of Tasks

| Priority | Task | Effort | Dependencies |
|----------|------|--------|--------------|
| 🔴 High | 1.1: Add semantic validation for invalid type coercions | 2-3h | None |
| 🔴 High | 2.2: Fix test fixture (add @virtual, use str()) | 5min | None |
| 🟡 Medium | 1.3: Add type coercion validation tests | 1-2h | Task 1.1 |
| 🟡 Medium | 2.1: Add @override/@virtual semantic validation | 3-4h | None |
| 🟡 Medium | 2.3: Add override validation tests | 1-2h | Task 2.1 |
| 🟢 Low | 1.2: Alternative - generate ToString() for primitives | 1-2h | Design decision |
| 🟢 Low | 2.4: Consider implicit virtual for all methods | 1h | Design decision |

---

## Files to Modify

1. `src/Sharpy.Compiler/Semantic/TypeChecker.cs` - Add type coercion validation
2. `src/Sharpy.Compiler/Semantic/InheritanceValidator.cs` - New file for override validation
3. `src/Sharpy.Compiler.Tests/Integration/TestFixtures/cross_module_inheritance/mixed_inheritance_interface/named_entity.spy` - Fix test fixture
4. `src/Sharpy.Compiler.Tests/Semantic/TypeCoercionValidationTests.cs` - New test file
5. `src/Sharpy.Compiler.Tests/Semantic/OverrideValidationTests.cs` - New test file

---

## Verification Criteria

After implementing these tasks:

1. ✅ `int to str` produces a compile-time error with helpful message
2. ✅ `str(42)` works correctly and generates `42.ToString()`
3. ✅ `@override` without `@virtual` in base class produces helpful error
4. ✅ Cross-module inheritance tests pass
5. ✅ All existing tests continue to pass
