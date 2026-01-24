# Walkthrough: OperatorSignatureValidator.cs

**Source File**: `src/Sharpy.Compiler/Semantic/OperatorSignatureValidator.cs`

---

## 1. Overview

`OperatorSignatureValidator` is a **compile-time validation utility** that ensures operator methods (dunder methods like `__add__`, `__eq__`, `__neg__`) in Sharpy classes have **correct signatures**. This validator sits in the semantic analysis phase of the compiler pipeline and acts as a gatekeeper to prevent invalid operator definitions from being compiled to C#.

### Role in the Compiler Pipeline

```
Parser → AST → [Semantic Analysis] → ValidationPipeline → CodeGen
                      ↑
              OperatorSignatureValidator validates dunder signatures here
```

The validator runs **after** the parser creates the AST but **before** code generation. It checks:
- **Parameter count**: Does the operator have the right number of parameters?
- **Return type**: Does the operator return the correct type (e.g., `bool` for comparisons)?
- **Type constraints**: Is the return type non-void for arithmetic operations?

**Key Design Philosophy**: Catch errors early. Better to fail at compile-time with a clear error message than generate invalid C# code or produce runtime errors.

---

## 2. Class/Type Structure

### Main Class: `OperatorSignatureValidator`

This is a **static utility class** with no instance state. All methods are static because validation is a pure function: given a `FunctionDef` and its owning type, return a list of errors.

```csharp
public class OperatorSignatureValidator
{
    // Static collections categorizing operator types
    private static readonly HashSet<string> BinaryArithmeticOps;
    private static readonly HashSet<string> BinaryBitwiseOps;
    private static readonly HashSet<string> InPlaceOps;
    private static readonly HashSet<string> ComparisonOps;
    private static readonly HashSet<string> UnaryOps;
    private static readonly HashSet<string> AllOperatorDunders;
}
```

### Operator Categories

The validator organizes operators into five categories:

| Category | Examples | Parameter Count |
|----------|----------|----------------|
| **BinaryArithmeticOps** | `__add__`, `__sub__`, `__mul__`, `__truediv__` | 2 (self + other) |
| **BinaryBitwiseOps** | `__and__`, `__or__`, `__xor__`, `__lshift__` | 2 (self + other) |
| **InPlaceOps** | `__iadd__`, `__isub__`, `__imul__` | 2 (self + other) |
| **ComparisonOps** | `__eq__`, `__ne__`, `__lt__`, `__le__` | 2 (self + other) |
| **UnaryOps** | `__pos__`, `__neg__`, `__invert__` | 1 (self only) |

**Why these categories?** Each category has different validation rules. Comparison operators must return `bool`, while arithmetic operators can return any non-void type.

---

## 3. Key Functions/Methods

### 3.1 Static Constructor

```csharp
static OperatorSignatureValidator()
{
    AllOperatorDunders = new HashSet<string>();
    AllOperatorDunders.UnionWith(BinaryArithmeticOps);
    AllOperatorDunders.UnionWith(BinaryBitwiseOps);
    AllOperatorDunders.UnionWith(InPlaceOps);
    AllOperatorDunders.UnionWith(ComparisonOps);
    AllOperatorDunders.UnionWith(UnaryOps);
}
```

**Purpose**: Combines all operator categories into a single lookup set `AllOperatorDunders` for fast `O(1)` membership testing.

**Design Decision**: Using `HashSet.UnionWith` is more efficient than repeatedly adding individual strings. This runs once when the class is first loaded.

---

### 3.2 `IsOperatorDunder(string methodName)`

```csharp
public static bool IsOperatorDunder(string methodName)
{
    return AllOperatorDunders.Contains(methodName);
}
```

**Purpose**: Quick check to determine if a method name represents an operator dunder.

**Parameters**:
- `methodName`: The method name to check (e.g., `"__add__"`, `"my_method"`)

**Returns**: `true` if it's a recognized operator dunder, `false` otherwise

**Usage Context**: Called by the validation pipeline to decide whether a method needs operator signature validation. Regular methods bypass this validator.

**Example**:
```csharp
IsOperatorDunder("__add__")    // true
IsOperatorDunder("__eq__")     // true
IsOperatorDunder("my_method")  // false
```

---

### 3.3 `ValidateDunderSignature(FunctionDef funcDef, TypeSymbol owningType)`

This is the **main entry point** for validation.

```csharp
public static List<SemanticError> ValidateDunderSignature(
    FunctionDef funcDef, 
    TypeSymbol owningType)
```

**Purpose**: Validates that a dunder method has the correct signature according to Sharpy's operator protocol.

**Parameters**:
- `funcDef`: The AST node representing the function definition (from the parser)
- `owningType`: The class/struct that owns this method (for error messages)

**Returns**: A list of `SemanticError` objects. Empty list means validation passed.

**Algorithm**:

1. **Early exit**: If not an operator dunder, return empty error list
2. **Parameter count validation**: 
   - Unary operators → must have 1 parameter (self)
   - Binary operators → must have 2 parameters (self, other)
3. **Return type validation**: If a return type annotation exists, validate it

**Key Implementation Details**:

```csharp
var paramCount = funcDef.Parameters.Length;

if (UnaryOps.Contains(methodName))
{
    if (paramCount != 1)
    {
        errors.Add(new SemanticError(
            $"Unary operator method '{methodName}' on '{owningType.Name}' must have exactly 1 parameter (self), got {paramCount}",
            funcDef.LineStart,
            funcDef.ColumnStart));
    }
}
else if (BinaryArithmeticOps.Contains(methodName) ||
         BinaryBitwiseOps.Contains(methodName) ||
         InPlaceOps.Contains(methodName) ||
         ComparisonOps.Contains(methodName))
{
    if (paramCount != 2)
    {
        errors.Add(new SemanticError(
            $"Binary operator method '{methodName}' on '{owningType.Name}' must have exactly 2 parameters (self, other), got {paramCount}",
            funcDef.LineStart,
            funcDef.ColumnStart));
    }
}
```

**Why check parameter count first?** Because a method with the wrong number of parameters can't be a valid operator, regardless of its return type.

---

### 3.4 `ValidateReturnType(FunctionDef funcDef, string methodName, string owningTypeName, List<SemanticError> errors)`

```csharp
private static void ValidateReturnType(
    FunctionDef funcDef, 
    string methodName, 
    string owningTypeName, 
    List<SemanticError> errors)
```

**Purpose**: Enforces return type constraints for operator methods based on .NET operator semantics.

**Key Rules**:

1. **Comparison operators must return `bool`**:
   ```csharp
   if (ComparisonOps.Contains(methodName))
   {
       if (!IsTypeAnnotationBool(returnType))
       {
           errors.Add(new SemanticError(
               $"Comparison operator method '{methodName}' on '{owningTypeName}' must return 'bool', got '{TypeAnnotationHelper.GetName(returnType)}'",
               ...));
       }
   }
   ```

2. **Arithmetic/bitwise/unary operators must return non-void**:
   ```csharp
   if (IsTypeAnnotationVoid(returnType))
   {
       errors.Add(new SemanticError(
           $"Operator method '{methodName}' on '{owningTypeName}' must return a non-void type",
           ...));
   }
   ```

**Why these rules?**
- C# requires comparison operators (`==`, `<`, etc.) to return `bool`
- Arithmetic operators (`+`, `-`, etc.) must return a value (can't be void)
- This ensures generated C# code will compile

---

### 3.5 Helper Methods

#### `IsTypeAnnotationBool(TypeAnnotation typeAnnotation)`

```csharp
private static bool IsTypeAnnotationBool(TypeAnnotation typeAnnotation)
{
    return typeAnnotation.Name == "bool" 
        && typeAnnotation.TypeArguments.Length == 0 
        && !typeAnnotation.IsNullable;
}
```

**Purpose**: Checks if a type annotation represents exactly `bool` (not `bool?`, not `bool[int]`).

**Three-part check**:
1. Name must be `"bool"`
2. No type arguments (not a generic like `bool[T]`)
3. Not nullable (not `bool?`)

#### `IsTypeAnnotationVoid(TypeAnnotation typeAnnotation)`

```csharp
private static bool IsTypeAnnotationVoid(TypeAnnotation typeAnnotation)
{
    return typeAnnotation.Name == "None" 
        && typeAnnotation.TypeArguments.Length == 0;
}
```

**Purpose**: Checks if a type annotation represents `None` (Sharpy's void/no-return type).

**Why "None" instead of "void"?** Sharpy follows Python convention where functions without a return value have type `None`.

---

## 4. Dependencies

### Upstream Dependencies (What This File Uses)

1. **`Sharpy.Compiler.Parser.Ast`**:
   - `FunctionDef`: AST node representing function definitions
   - `TypeAnnotation`: Represents type annotations in the source code

2. **`Sharpy.Compiler.Semantic`**:
   - `SemanticError`: Error objects with file location info
   - `TypeSymbol`: Represents the class/struct owning the operator method
   - `TypeAnnotationHelper`: Utility for formatting type names in error messages

### Downstream Dependencies (What Uses This File)

1. **Validation Pipeline**:
   - The new V2 validation pipeline uses this validator through wrapper classes
   - Called during semantic analysis phase

2. **Type Checkers**:
   - Type checking logic consults operator signatures to validate usage
   - Ensures operators are called with compatible types

### Related Files

- **`OperatorValidator.cs`** (deprecated): Legacy validator that combined signature checking with type inference
- **`ProtocolSignatureValidator.cs`**: Similar validator for protocol methods (like `__iter__`, `__len__`)
- **`Validation/SignatureValidatorV2.cs`**: New V2 wrapper that uses this validator in the modern pipeline

---

## 5. Patterns and Design Decisions

### 5.1 Static Utility Pattern

**Design**: All methods are static, no instance state.

**Rationale**:
- Validation is a pure function (no side effects)
- No need to carry state between validations
- Can be called from anywhere without instantiation
- Thread-safe by design (no mutable state)

### 5.2 Early Exit Optimization

```csharp
if (!IsOperatorDunder(methodName))
{
    return errors;  // Empty list
}
```

**Rationale**: Skip validation for regular methods. Most methods in a class aren't operators, so this avoids unnecessary work.

### 5.3 Error Collection Pattern

```csharp
var errors = new List<SemanticError>();
// ... collect errors ...
return errors;
```

**Rationale**:
- Report all errors at once (better developer experience)
- Don't fail fast - let developers see all issues
- Caller decides whether to treat errors as fatal

### 5.4 Category-Based Validation

Operators are grouped into categories with shared validation rules.

**Benefits**:
- Clear organization
- Easy to extend (add new operator → add to appropriate category)
- Performance: `O(1)` category membership check via HashSet

### 5.5 Separation of Concerns

This validator **only checks signatures**, not:
- Type compatibility (handled by `TypeChecker`)
- Operator usage validation (handled by `OperatorValidator`)
- Code generation (handled by `RoslynEmitter`)

**Why?** Single Responsibility Principle. Each validator does one thing well.

---

## 6. Debugging Tips

### 6.1 When Adding New Operators

If you're adding a new operator to Sharpy:

1. **Add to appropriate category**:
   ```csharp
   private static readonly HashSet<string> BinaryArithmeticOps = new()
   {
       "__add__", "__sub__", "__my_new_op__"  // Add here
   };
   ```

2. **Update the static constructor**: It automatically picks up new operators via `UnionWith`.

3. **Test with invalid signatures**:
   ```python
   class MyClass:
       # Should fail: wrong parameter count
       def __my_new_op__(self) -> int:
           return 42
   ```

### 6.2 Common Error Scenarios

**Error**: "Unary operator method `__neg__` must have exactly 1 parameter"
- **Cause**: Developer added extra parameters to unary operator
- **Fix**: Remove extra parameters; unary operators only take `self`

**Error**: "Comparison operator method `__eq__` must return 'bool', got 'int'"
- **Cause**: Wrong return type annotation
- **Fix**: Change return type to `bool`

**Error**: "Operator method `__add__` must return a non-void type"
- **Cause**: Return type is `None` or missing
- **Fix**: Add explicit return type annotation

### 6.3 Debugging Workflow

1. **Set breakpoint in `ValidateDunderSignature`**
2. **Inspect `funcDef.Parameters.Length`** - is it what you expect?
3. **Check `funcDef.ReturnType`** - is it null or has wrong annotation?
4. **Print error messages** - they include line/column info for tracing

### 6.4 Testing the Validator

```csharp
var funcDef = new FunctionDef
{
    Name = "__add__",
    Parameters = new[] { /* ... */ },
    ReturnType = new TypeAnnotation { Name = "int" },
    LineStart = 10,
    ColumnStart = 5
};

var errors = OperatorSignatureValidator.ValidateDunderSignature(
    funcDef, 
    myTypeSymbol);

// Should be empty for valid signature
Assert.Empty(errors);
```

---

## 7. Contribution Guidelines

### 7.1 Adding Support for New Operators

**Steps**:

1. **Add operator name to appropriate category**:
   ```csharp
   private static readonly HashSet<string> UnaryOps = new()
   {
       "__pos__", "__neg__", "__invert__", "__my_new_unary_op__"
   };
   ```

2. **Update language specification** in `docs/language_specification/operator_overloading.md`

3. **Add tests** in `Sharpy.Compiler.Tests/Semantic/OperatorSignatureValidatorTests.cs`

4. **Verify C# mapping** - ensure the operator can be mapped to valid C# code

### 7.2 Modifying Validation Rules

**Example**: Make comparison operators allow nullable bool (`bool?`)

```csharp
private static bool IsTypeAnnotationBool(TypeAnnotation typeAnnotation)
{
    // Original: strict bool only
    return typeAnnotation.Name == "bool" 
        && typeAnnotation.TypeArguments.Length == 0 
        && !typeAnnotation.IsNullable;

    // Modified: allow bool?
    return typeAnnotation.Name == "bool" 
        && typeAnnotation.TypeArguments.Length == 0;
        // Removed nullable check
}
```

**Testing checklist**:
- ✅ Add test with `bool?` return type
- ✅ Verify C# code generation works
- ✅ Update spec documentation
- ✅ Run full test suite

### 7.3 Error Message Guidelines

**Good error messages**:
- Include method name, type name, and location
- State what was expected vs. what was found
- Use consistent terminology

**Example**:
```csharp
$"Binary operator method '{methodName}' on '{owningType.Name}' must have exactly 2 parameters (self, other), got {paramCount}"
```

**Poor error message**:
```csharp
"Wrong parameter count"  // ❌ Too vague
```

### 7.4 Common Pitfall: In-Place Operators

In-place operators (`__iadd__`, `__isub__`, etc.) follow the same rules as binary operators:
- Must have 2 parameters (self, other)
- Must return non-void (typically return `self` for chaining)

**Don't confuse with**:
- Python's in-place semantics (modify self)
- These still need proper return types in Sharpy for .NET interop

---

## 8. Cross-References

### Related Documentation Files

- **[Symbol.md](./Symbol.md)**: Explains `TypeSymbol` structure used in validation
- **[SemanticError.md](./SemanticError.md)**: Error reporting conventions
- **[ProtocolSignatureValidator.md](./ProtocolSignatureValidator.md)**: Similar validator for protocol methods
- **[Validation Pipeline](./Validation/README.md)**: How validators are orchestrated

### Related Source Files

- **`src/Sharpy.Compiler/Semantic/OperatorValidator.cs`** (deprecated): Legacy validator - being phased out
- **`src/Sharpy.Compiler/Semantic/Validation/SignatureValidatorV2.cs`**: V2 wrapper that uses this validator
- **`src/Sharpy.Compiler/Semantic/TypeAnnotationHelper.cs`**: Shared utility for type name formatting
- **`src/Sharpy.Compiler/CodeGen/RoslynEmitter*.cs`**: Consumes validated operators to generate C# code

### Related Specifications

- **`docs/language_specification/operator_overloading.md`**: Authoritative spec for operator signatures
- **`docs/language_specification/dunder_invocation_rules.md`**: When and how operators are invoked
- **`docs/language_specification/arithmetic_operators.md`**: Arithmetic operator semantics

---

## 9. Future Enhancements

### 9.1 Planned Features

1. **More detailed error messages**: Suggest fixes (e.g., "Did you mean to add a parameter?")
2. **Protocol integration**: Validate operators implement corresponding protocols (`IAddable`, etc.)
3. **Generic constraints**: Validate type parameters on generic operators
4. **Overload resolution**: Validate multiple overloads don't conflict

### 9.2 Known Limitations

- **No parameter type validation**: Only checks count, not types (handled by `TypeChecker`)
- **No body validation**: Doesn't check if implementation matches signature
- **No reflection support**: Doesn't validate operators imported from .NET assemblies

---

## Summary

`OperatorSignatureValidator` is a focused, efficient validator that:
- ✅ Ensures operator methods have correct signatures
- ✅ Catches errors early in compilation
- ✅ Provides clear error messages with location info
- ✅ Integrates with the modern validation pipeline
- ✅ Follows single-responsibility principle

**Key Takeaway**: This validator is the first line of defense against invalid operator definitions, ensuring Sharpy code can be reliably compiled to valid C# that matches .NET operator semantics.
