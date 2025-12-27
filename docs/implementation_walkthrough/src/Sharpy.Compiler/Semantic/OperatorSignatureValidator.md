# Walkthrough: OperatorSignatureValidator.cs

**Source File**: `src/Sharpy.Compiler/Semantic/OperatorSignatureValidator.cs`

---

## Overview

The `OperatorSignatureValidator` class is a critical component of Sharpy's semantic analysis phase that validates operator overloading methods (known as "dunder methods" in Python, e.g., `__add__`, `__eq__`). 

**Primary Responsibilities:**
- Ensures operator methods have the correct number of parameters
- Validates return types match .NET operator semantics
- Enforces consistency between Python-style operator overloading and .NET's operator requirements

**When It Runs:** During the semantic analysis phase, specifically when the `NameResolver` processes class method definitions and encounters methods with dunder names.

**Why It Matters:** Sharpy compiles to C# via Roslyn, which has strict requirements for operator overloads. This validator catches mismatches early in the compilation pipeline before code generation, providing clear error messages to developers.

---

## Class/Type Structure

### Main Class: `OperatorSignatureValidator`

This is a static utility class (all methods are `public static`) that serves as a centralized validator for operator method signatures. It doesn't maintain state—all validation logic is stateless.

### Key Data Structures

The class defines several `HashSet<string>` collections that categorize operator methods by their semantic meaning:

#### 1. **BinaryArithmeticOps** (Lines 12-15)
```csharp
private static readonly HashSet<string> BinaryArithmeticOps = new()
{
    "__add__", "__sub__", "__mul__", "__truediv__", "__floordiv__", "__mod__", "__pow__"
};
```
- Mathematical operators that take two operands
- Maps to C# operators: `+`, `-`, `*`, `/`, `//` (floor division), `%`, `**` (power)
- Expected signature: `def __add__(self, other) -> T`

#### 2. **BinaryBitwiseOps** (Lines 17-20)
```csharp
private static readonly HashSet<string> BinaryBitwiseOps = new()
{
    "__and__", "__or__", "__xor__", "__lshift__", "__rshift__"
};
```
- Bitwise/logical operators
- Maps to C# operators: `&`, `|`, `^`, `<<`, `>>`
- Expected signature: `def __and__(self, other) -> T`

#### 3. **InPlaceOps** (Lines 22-26)
```csharp
private static readonly HashSet<string> InPlaceOps = new()
{
    "__iadd__", "__isub__", "__imul__", "__itruediv__", "__ifloordiv__", "__imod__", "__ipow__",
    "__iand__", "__ior__", "__ixor__", "__ilshift__", "__irshift__"
};
```
- In-place/augmented assignment operators (e.g., `+=`, `-=`)
- These modify the object in place and return it
- Expected signature: `def __iadd__(self, other) -> T`

#### 4. **ComparisonOps** (Lines 28-31)
```csharp
private static readonly HashSet<string> ComparisonOps = new()
{
    "__eq__", "__ne__", "__lt__", "__le__", "__gt__", "__ge__"
};
```
- Comparison operators: `==`, `!=`, `<`, `<=`, `>`, `>=`
- **Special requirement**: Must return `bool` (enforced by .NET)
- Expected signature: `def __eq__(self, other) -> bool`

#### 5. **UnaryOps** (Lines 33-36)
```csharp
private static readonly HashSet<string> UnaryOps = new()
{
    "__pos__", "__neg__", "__invert__"
};
```
- Single-operand operators: `+x`, `-x`, `~x`
- Expected signature: `def __neg__(self) -> T` (only `self` parameter)

#### 6. **AllOperatorDunders** (Lines 38, 40-48)
A unified collection of all recognized operator methods, built in the static constructor by merging all the above sets. Used for quick lookup to determine if a method name is an operator dunder.

---

## Key Functions/Methods

### 1. `IsOperatorDunder(string methodName)` (Lines 53-56)

**Purpose:** Quick check to determine if a method name corresponds to a recognized operator overload.

**Signature:**
```csharp
public static bool IsOperatorDunder(string methodName)
```

**Parameters:**
- `methodName`: The name of the method to check (e.g., `__add__`, `myMethod`)

**Returns:** `true` if the method is a recognized operator dunder, `false` otherwise

**Implementation:**
```csharp
return AllOperatorDunders.Contains(methodName);
```

**Usage Pattern:** Called by `NameResolver` when processing method definitions to decide if validation is needed:
```csharp
if (OperatorSignatureValidator.IsOperatorDunder(method.Name))
{
    var validationErrors = OperatorSignatureValidator.ValidateDunderSignature(method, owningType);
    // ... handle errors
}
```

**Performance Note:** Uses `HashSet.Contains()` for O(1) lookup—efficient even with many methods.

---

### 2. `ValidateDunderSignature(FunctionDef funcDef, TypeSymbol owningType)` (Lines 62-109)

**Purpose:** The main validation entry point. Checks that an operator method has the correct parameter count and return type.

**Signature:**
```csharp
public static List<SemanticError> ValidateDunderSignature(FunctionDef funcDef, TypeSymbol owningType)
```

**Parameters:**
- `funcDef`: AST node representing the function definition (from the parser)
  - Contains: `Name`, `Parameters` (list), `ReturnType` (nullable TypeAnnotation), location info
- `owningType`: The `TypeSymbol` representing the class that defines this method
  - Used for error messages to show which type has the invalid operator

**Returns:** `List<SemanticError>` - may be empty (valid signature) or contain errors

**Algorithm Flow:**

1. **Early Exit** (Lines 67-71): If not an operator dunder, return empty list
   ```csharp
   if (!IsOperatorDunder(methodName))
       return errors;
   ```

2. **Parameter Count Validation** (Lines 73-100):
   - **Unary operators** (Lines 76-86): Must have exactly 1 parameter (`self`)
     - If not: Add error like `"Unary operator method '__neg__' on 'MyClass' must have exactly 1 parameter (self), got 2"`
   
   - **Binary operators** (Lines 87-100): Must have exactly 2 parameters (`self`, `other`)
     - Applies to arithmetic, bitwise, in-place, and comparison operators
     - If not: Add error like `"Binary operator method '__add__' on 'MyClass' must have exactly 2 parameters (self, other), got 3"`

3. **Return Type Validation** (Lines 102-106): If a return type annotation exists, validate it
   ```csharp
   if (funcDef.ReturnType != null)
   {
       ValidateReturnType(funcDef, methodName, owningType.Name, errors);
   }
   ```

**Design Decision:** Validation is permissive about missing type annotations—only validates if present. This aligns with Sharpy's gradual typing philosophy.

---

### 3. `ValidateReturnType(FunctionDef funcDef, string methodName, string owningTypeName, List<SemanticError> errors)` (Lines 114-149)

**Purpose:** Enforces return type requirements specific to .NET operator semantics.

**Signature:**
```csharp
private static void ValidateReturnType(FunctionDef funcDef, string methodName, 
                                        string owningTypeName, List<SemanticError> errors)
```

**Parameters:**
- `funcDef`: The function definition (to access return type annotation and location)
- `methodName`: Name of the operator method
- `owningTypeName`: Name of the type (for error messages)
- `errors`: List to append errors to (mutated in place)

**Validation Rules:**

#### Comparison Operators (Lines 123-133)
Must return `bool` (non-nullable):
```csharp
if (ComparisonOps.Contains(methodName))
{
    if (!IsTypeAnnotationBool(returnType))
    {
        errors.Add(new SemanticError(
            $"Comparison operator method '{methodName}' on '{owningTypeName}' must return 'bool', got '{TypeAnnotationHelper.GetName(returnType)}'",
            funcDef.LineStart,
            funcDef.ColumnStart));
    }
}
```

**Why:** .NET's `==`, `!=`, `<`, etc. operators must return `bool`. This is a CLR requirement.

**Example Error:**
```
Semantic error at line 10, column 5: Comparison operator method '__eq__' on 'Point' must return 'bool', got 'int'
```

#### Arithmetic/Bitwise/Unary Operators (Lines 136-148)
Must return a non-void type:
```csharp
if (IsTypeAnnotationVoid(returnType))
{
    errors.Add(new SemanticError(
        $"Operator method '{methodName}' on '{owningTypeName}' must return a non-void type",
        funcDef.LineStart,
        funcDef.ColumnStart));
}
```

**Why:** Operators like `+`, `-`, `&`, etc. must produce a value. A void-returning operator makes no sense semantically.

**Example Error:**
```
Semantic error at line 15, column 5: Operator method '__add__' on 'Vector' must return a non-void type
```

---

### 4. Helper Methods

#### `IsTypeAnnotationBool(TypeAnnotation typeAnnotation)` (Lines 154-157)

Checks if a type annotation is exactly `bool` (not `bool?` or generic):
```csharp
return typeAnnotation.Name == "bool" 
    && typeAnnotation.TypeArguments.Count == 0 
    && !typeAnnotation.IsNullable;
```

**Strict Checking:** Rejects `bool?` for comparison operators since .NET doesn't allow nullable comparison results.

#### `IsTypeAnnotationVoid(TypeAnnotation typeAnnotation)` (Lines 162-165)

Checks if a type annotation represents `None` (Python's equivalent of void):
```csharp
return typeAnnotation.Name == "None" 
    && typeAnnotation.TypeArguments.Count == 0;
```

**Note:** `None` is not nullable (`None?` doesn't make sense), so no nullability check needed.

---

## Dependencies

### Internal Dependencies

1. **`Sharpy.Compiler.Parser.Ast`** (Line 1)
   - `FunctionDef`: AST node representing function definitions
   - `TypeAnnotation`: Represents type annotations in the AST
   - Provides location information (`LineStart`, `ColumnStart`) for error reporting

2. **`SemanticError`** (`Semantic/SemanticError.cs`)
   - Exception type for semantic analysis errors
   - Contains line/column information for precise error locations
   - Used to report validation failures back to the compiler

3. **`TypeSymbol`** (implied from `Semantic/Symbol.cs`)
   - Represents a type in the symbol table
   - Provides the type's name for error messages
   - Passed in from `NameResolver` during method validation

4. **`TypeAnnotationHelper`** (`Semantic/TypeAnnotationHelper.cs`)
   - `GetName(TypeAnnotation)`: Converts type annotations to readable strings
   - Handles generic types (e.g., `list[int]`), nullable types (e.g., `str?`)
   - Shared utility used in error messages

### External Dependencies

None—this is a pure C# utility class with no external library dependencies beyond .NET BCL (`System.Collections.Generic` for `HashSet` and `List`).

---

## Patterns and Design Decisions

### 1. **Static Utility Class Pattern**
All methods are static with no instance state. This makes sense because validation logic is stateless—it operates purely on input parameters.

**Benefits:**
- No need to instantiate validators
- Thread-safe by design
- Clear that no state is carried between validations
- Easy to test in isolation

### 2. **Category-Based Validation**
Operators are grouped by semantic category (arithmetic, bitwise, comparison, etc.) rather than individual switch cases.

**Benefits:**
- Easy to add new operators—just add to the appropriate `HashSet`
- Single point of truth for each category's requirements
- More maintainable than a giant switch statement

**Example:** Adding `__matmul__` (matrix multiplication `@` operator) would just require adding it to `BinaryArithmeticOps`.

### 3. **Error Accumulation**
Errors are collected in a list rather than thrown immediately:
```csharp
var errors = new List<SemanticError>();
// ... validation logic adds to errors
return errors;
```

**Benefits:**
- Multiple errors can be reported in a single compilation pass
- Caller decides how to handle errors (add to diagnostic list, throw, etc.)
- Aligns with compiler best practices (show all errors, don't stop at first)

### 4. **Gradual Typing Philosophy**
Return type validation only occurs if a type annotation exists:
```csharp
if (funcDef.ReturnType != null)
{
    ValidateReturnType(...);
}
```

This respects Sharpy's design as a gradually-typed language—type annotations are optional, but when provided, they're strictly enforced.

### 5. **Immutable AST Pattern**
The validator never modifies the AST (`FunctionDef` is a record)—it only reads and validates. This aligns with Sharpy's architecture where semantic information is stored separately in `SemanticInfo` rather than mutating AST nodes.

### 6. **Precise Error Location Tracking**
Every error includes `LineStart` and `ColumnStart` from the AST node:
```csharp
errors.Add(new SemanticError(message, funcDef.LineStart, funcDef.ColumnStart));
```

This ensures developers get precise error locations in their IDE or terminal.

---

## Debugging Tips

### 1. **Adding Debug Logging**

If validation isn't working as expected, add temporary logging:
```csharp
public static List<SemanticError> ValidateDunderSignature(FunctionDef funcDef, TypeSymbol owningType)
{
    var errors = new List<SemanticError>();
    var methodName = funcDef.Name;
    
    Console.WriteLine($"Validating {methodName} on {owningType.Name}");  // DEBUG
    Console.WriteLine($"Param count: {funcDef.Parameters.Count}");       // DEBUG
    
    // ... rest of validation
}
```

### 2. **Testing Individual Operators**

Create minimal Sharpy test files:
```python
# test_add.spy
class Point:
    x: int
    y: int
    
    def __add__(self, other: Point) -> Point:  # Valid
        return Point(self.x + other.x, self.y + other.y)
```

Compile and check for expected errors.

### 3. **Checking Which Category an Operator Falls Into**

If uncertain about classification, check the static collections:
```csharp
Console.WriteLine($"Is __eq__ comparison? {ComparisonOps.Contains("__eq__")}");
Console.WriteLine($"Is __add__ arithmetic? {BinaryArithmeticOps.Contains("__add__")}");
```

### 4. **Understanding Error Flow**

Set breakpoints in:
1. `IsOperatorDunder()` - to see which methods are being checked
2. `ValidateDunderSignature()` - to see parameter count validation
3. `ValidateReturnType()` - to see return type validation
4. Error creation points - to understand why specific errors are generated

### 5. **Common Issues**

**Issue:** Comparison operator accepts `bool?` when it shouldn't
- **Check:** `IsTypeAnnotationBool()` - ensure `!typeAnnotation.IsNullable` is present

**Issue:** New operator not recognized
- **Check:** Is it added to the appropriate `HashSet` and is `AllOperatorDunders` rebuilt in the static constructor?

**Issue:** Wrong parameter count accepted
- **Check:** Is the operator correctly categorized (unary vs binary)?

### 6. **Integration with NameResolver**

If validation seems to not run, check `NameResolver.cs`:
```csharp
if (OperatorSignatureValidator.IsOperatorDunder(method.Name))
{
    var validationErrors = OperatorSignatureValidator.ValidateDunderSignature(method, owningType);
    // Are errors being handled correctly here?
}
```

---

## Contribution Guidelines

### Adding New Operators

**Example:** Adding the matrix multiplication operator `@` (`__matmul__`)

1. **Determine the category**: Matrix multiplication is binary arithmetic
2. **Add to appropriate HashSet**:
   ```csharp
   private static readonly HashSet<string> BinaryArithmeticOps = new()
   {
       "__add__", "__sub__", "__mul__", "__truediv__", "__floordiv__", "__mod__", "__pow__",
       "__matmul__"  // NEW
   };
   ```
3. **Add in-place variant** if applicable:
   ```csharp
   private static readonly HashSet<string> InPlaceOps = new()
   {
       // ... existing operators
       "__imatmul__"  // NEW
   };
   ```
4. **No other changes needed** - the static constructor automatically includes it in `AllOperatorDunders`

5. **Add tests** in `Sharpy.Compiler.Tests/Semantic/OperatorSignatureValidatorTests.cs`:
   ```csharp
   [Fact]
   public void TestMatMulOperator_ValidSignature()
   {
       var funcDef = CreateFunctionDef("__matmul__", parameterCount: 2, returnType: "Matrix");
       var errors = OperatorSignatureValidator.ValidateDunderSignature(funcDef, mockType);
       Assert.Empty(errors);
   }
   ```

### Adding New Validation Rules

**Example:** Enforce that in-place operators return the same type as `self`

1. **Add new validation logic** to `ValidateReturnType()`:
   ```csharp
   else if (InPlaceOps.Contains(methodName))
   {
       // Verify return type matches owning type
       if (returnType.Name != owningTypeName)
       {
           errors.Add(new SemanticError(
               $"In-place operator '{methodName}' on '{owningTypeName}' must return '{owningTypeName}', got '{TypeAnnotationHelper.GetName(returnType)}'",
               funcDef.LineStart,
               funcDef.ColumnStart));
       }
   }
   ```

2. **Add tests** for the new rule
3. **Update documentation** in comments and this walkthrough

### Performance Improvements

If validation becomes a bottleneck (unlikely with current design):

1. **Profile first** - use a profiler to confirm this is the bottleneck
2. **Consider caching** - cache validation results if the same operators are validated multiple times
3. **Optimize HashSet lookups** - currently O(1), hard to improve further

### Code Quality

When contributing:

1. **Follow existing patterns** - use the same error message format, validation style
2. **Add XML documentation** - all public methods should have `/// <summary>` comments
3. **Keep it stateless** - don't add instance state
4. **Test edge cases**:
   - Methods with 0 parameters
   - Methods with 10+ parameters
   - Generic return types
   - Nullable types
5. **Run tests** before submitting:
   ```bash
   dotnet test --filter "FullyQualifiedName~OperatorSignatureValidator"
   ```

### Integration Points to Consider

When modifying this validator, consider impact on:

1. **NameResolver** - calls this during method processing
2. **RoslynEmitter** - generates C# operators; ensure validation aligns with codegen
3. **TypeChecker** - may have additional type checking for operators
4. **Error messages** - should be clear and actionable for users

### Related Files to Review

When working on operator validation, also review:

- `Semantic/OperatorValidator.cs` - Higher-level operator validation
- `Semantic/ProtocolSignatureValidator.cs` - Similar validator for protocol methods
- `CodeGen/RoslynEmitter.cs` - Code generation for operators
- `Parser/Ast/Statement.cs` - `FunctionDef` structure
- `Semantic/NameResolver.cs` - Where this validator is called

---

## Example Validation Scenarios

### Scenario 1: Valid Binary Operator

**Sharpy Code:**
```python
class Vector:
    def __add__(self, other: Vector) -> Vector:
        return Vector(self.x + other.x, self.y + other.y)
```

**Validation:**
- ✅ `__add__` recognized as binary arithmetic operator
- ✅ Parameter count: 2 (`self`, `other`)
- ✅ Return type: `Vector` (non-void)
- **Result:** No errors

### Scenario 2: Invalid Comparison Return Type

**Sharpy Code:**
```python
class Point:
    def __eq__(self, other: Point) -> int:  # ERROR: should return bool
        return 0
```

**Validation:**
- ✅ `__eq__` recognized as comparison operator
- ✅ Parameter count: 2
- ❌ Return type: `int` (should be `bool`)
- **Result:** Error generated:
  ```
  Semantic error at line 2, column 5: Comparison operator method '__eq__' on 'Point' must return 'bool', got 'int'
  ```

### Scenario 3: Invalid Unary Parameter Count

**Sharpy Code:**
```python
class Number:
    def __neg__(self, other: Number) -> Number:  # ERROR: unary should have 1 param
        return Number(-self.value)
```

**Validation:**
- ✅ `__neg__` recognized as unary operator
- ❌ Parameter count: 2 (should be 1)
- **Result:** Error generated:
  ```
  Semantic error at line 2, column 5: Unary operator method '__neg__' on 'Number' must have exactly 1 parameter (self), got 2
  ```

### Scenario 4: Void-Returning Arithmetic Operator

**Sharpy Code:**
```python
class Matrix:
    def __add__(self, other: Matrix) -> None:  # ERROR: can't return void
        pass
```

**Validation:**
- ✅ `__add__` recognized as binary arithmetic operator
- ✅ Parameter count: 2
- ❌ Return type: `None` (void not allowed)
- **Result:** Error generated:
  ```
  Semantic error at line 2, column 5: Operator method '__add__' on 'Matrix' must return a non-void type
  ```

---

## Summary

The `OperatorSignatureValidator` is a focused, well-designed validator that bridges Python's operator overloading syntax with .NET's operator requirements. Its strength lies in:

- **Clear categorization** of operators by semantic type
- **Precise error messages** with location information
- **Maintainability** through static, stateless design
- **Extensibility** for adding new operators
- **Alignment** with Sharpy's gradual typing philosophy

As a newcomer, understanding this file helps you grasp:
1. How Sharpy enforces .NET interop requirements
2. The semantic analysis phase's error reporting patterns
3. The relationship between Python-style syntax and C# codegen constraints

When debugging operator-related issues, this is the first place to check for signature validation logic.
