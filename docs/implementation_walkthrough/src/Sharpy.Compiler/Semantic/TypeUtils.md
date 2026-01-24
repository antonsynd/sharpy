# Walkthrough: TypeUtils.cs

**Source File**: `src/Sharpy.Compiler/Semantic/TypeUtils.cs`

---

## Overview

`TypeUtils.cs` is a **static utility class** that centralizes common type-checking operations used throughout the Sharpy compiler's semantic analysis phase. Think of it as a type-checking Swiss Army knife—it provides predicates, type extraction helpers, and type compatibility logic that would otherwise be scattered across the codebase.

**Role in the Compiler Pipeline:**
- **Upstream**: Consumed by `TypeChecker`, `TypeResolver`, and various validators (e.g., `OperatorValidator`, `ProtocolValidator`)
- **Downstream**: Helps inform code generation decisions by answering questions like "Is this a numeric type?" or "What's the common type for these two operands?"
- **Purpose**: Avoids duplication of type-checking logic and provides a single source of truth for type classification

This file does **not** perform type resolution or type inference—it operates on already-resolved `SemanticType` instances.

---

## Key Concepts

Before diving into the methods, understand these fundamental concepts:

### SemanticType Hierarchy

The `SemanticType` class hierarchy (defined in `SemanticType.cs`) represents resolved types:

```csharp
SemanticType (abstract)
├── UnknownType (error recovery)
├── VoidType (None/void)
├── BuiltinType (int, str, bool, etc.)
│   └── ClrType property links to System.Type
├── GenericType (list[T], dict[K,V], set[T])
│   └── TypeArguments: List<SemanticType>
├── UserDefinedType (classes, interfaces)
├── NullableType (T?)
│   └── UnderlyingType: SemanticType
├── TupleType ((int, str))
└── FunctionType ((int, str) -> bool)
```

### Type Categories

TypeUtils groups types into logical categories:
- **Numeric**: All integer and floating-point types
- **Integer**: Non-floating-point numerics (`int`, `long`, etc.)
- **Floating-point**: `float`, `double`, `decimal`
- **Collections**: `list[T]`, `dict[K,V]`, `set[T]`
- **Primitives**: `bool`, `str`

---

## Class Structure

```csharp
public static class TypeUtils
{
    // Type classification predicates (13 methods)
    public static bool IsNumeric(SemanticType type)
    public static bool IsInteger(SemanticType type)
    public static bool IsFloatingPoint(SemanticType type)
    public static bool IsString(SemanticType type)
    public static bool IsBool(SemanticType type)
    public static bool IsCollection(SemanticType type)
    public static bool IsList(SemanticType type)
    public static bool IsDict(SemanticType type)
    public static bool IsSet(SemanticType type)
    public static bool IsTuple(SemanticType type)
    
    // Type extraction helpers (2 methods)
    public static SemanticType? GetElementType(SemanticType type)
    public static SemanticType? GetKeyType(SemanticType type)
    
    // Nullable handling (1 method)
    public static SemanticType UnwrapAllNullable(SemanticType type)
    
    // Type equivalence and compatibility (2 methods)
    public static bool AreEquivalent(SemanticType a, SemanticType b)
    public static SemanticType? GetCommonType(SemanticType a, SemanticType b)
}
```

The class is **stateless** and all methods are **pure functions** (no side effects).

---

## Key Methods

### Type Classification Predicates

#### `IsNumeric(SemanticType type)`

**Purpose**: Determines if a type represents any numeric type (integer or floating-point).

**Implementation Details**:
```csharp
public static bool IsNumeric(SemanticType type)
{
    if (type is BuiltinType builtin && builtin.ClrType != null)
    {
        return builtin.ClrType == typeof(int) ||
               builtin.ClrType == typeof(long) ||
               builtin.ClrType == typeof(float) ||
               builtin.ClrType == typeof(double) ||
               builtin.ClrType == typeof(decimal) ||
               builtin.ClrType == typeof(short) ||
               builtin.ClrType == typeof(byte) ||
               builtin.ClrType == typeof(sbyte) ||
               builtin.ClrType == typeof(ushort) ||
               builtin.ClrType == typeof(uint) ||
               builtin.ClrType == typeof(ulong);
    }
    return false;
}
```

**Key Design Decisions**:
- Uses **CLR type comparison** (`typeof(int)`) rather than string name comparison
- Covers all .NET numeric types, including unsigned variants
- Returns `false` for `GenericType`, `UserDefinedType`, etc.—only `BuiltinType` can be numeric

**Usage Example** (from TypeChecker):
```csharp
if (TypeUtils.IsNumeric(leftType) && TypeUtils.IsNumeric(rightType))
{
    // Allow arithmetic operations
    return TypeUtils.GetCommonType(leftType, rightType);
}
```

**Related Methods**:
- `IsInteger()`: Subset of numeric types (excludes `float`, `double`, `decimal`)
- `IsFloatingPoint()`: Subset of numeric types (only `float`, `double`, `decimal`)

---

#### `IsCollection(SemanticType type)`

**Purpose**: Checks if a type is one of Sharpy's built-in collection types.

**Implementation**:
```csharp
public static bool IsCollection(SemanticType type)
{
    return type is GenericType generic &&
        (generic.Name == "list" || generic.Name == "dict" || generic.Name == "set");
}
```

**Key Insights**:
- Collections are always `GenericType` instances (even unparameterized `list` becomes `list[?]`)
- Uses **name-based matching** (`"list"`, `"dict"`, `"set"`)
- Does **not** include tuples (`TupleType`) or user-defined collection classes

**Usage Context**:
- Iterator validation (checking if `for x in collection:` is valid)
- Protocol checking (does this type support `__iter__`?)
- Operator validation (can we use `in` operator?)

**Related Methods**:
- `IsList()`, `IsDict()`, `IsSet()`: More specific checks
- `IsTuple()`: Separate check since tuples use `TupleType`, not `GenericType`

---

### Type Extraction Helpers

#### `GetElementType(SemanticType type)`

**Purpose**: Extracts the element type from a collection type.

**Behavior by Collection Type**:
| Input | Output |
|-------|--------|
| `list[int]` | `int` |
| `set[str]` | `str` |
| `dict[str, int]` | `int` (value type, not key!) |
| Non-collection | `null` |

**Implementation**:
```csharp
public static SemanticType? GetElementType(SemanticType type)
{
    if (type is GenericType generic)
    {
        if (generic.Name == "list" || generic.Name == "set")
            return generic.TypeArguments.FirstOrDefault();
        if (generic.Name == "dict")
            return generic.TypeArguments.Skip(1).FirstOrDefault(); // Value type
    }
    return null;
}
```

**Critical Detail**: For dictionaries, returns the **value type** (second type argument), not the key type. This aligns with Python's iteration semantics:
```python
# In Python, iterating over a dict yields keys:
for key in my_dict:  # key is the element type
    ...

# But TypeUtils.GetElementType returns VALUES for symmetry
# with list comprehensions and other contexts
```

**Common Pitfall**: If you need the key type of a dict, use `GetKeyType()` instead!

---

#### `GetKeyType(SemanticType type)`

**Purpose**: Extracts the key type from a dictionary.

**Implementation**:
```csharp
public static SemanticType? GetKeyType(SemanticType type)
{
    if (type is GenericType { Name: "dict" } generic)
    {
        return generic.TypeArguments.FirstOrDefault();
    }
    return null;
}
```

**Design Note**: This method only makes sense for dictionaries. For `list` or `set`, it returns `null`.

**Usage Example**:
```csharp
// Validating dict[K, V] key access: my_dict[key]
var keyType = TypeUtils.GetKeyType(dictType);
if (keyType != null && !indexType.IsAssignableTo(keyType))
{
    Error($"Key type mismatch: expected {keyType}, got {indexType}");
}
```

---

### Nullable Handling

#### `UnwrapAllNullable(SemanticType type)`

**Purpose**: Recursively unwraps nested nullable types until reaching a non-nullable core type.

**Examples**:
| Input | Output |
|-------|--------|
| `int?` | `int` |
| `int??` | `int` |
| `list[str]?` | `list[str]` |
| `int` | `int` (unchanged) |

**Implementation**:
```csharp
public static SemanticType UnwrapAllNullable(SemanticType type)
{
    while (type is NullableType nullable)
        type = nullable.UnderlyingType;
    return type;
}
```

**Design Philosophy**: While nested nullables (`T??`) are rare in practice, the compiler doesn't prohibit them during construction. This method handles arbitrary nesting depth defensively.

**Usage Context**:
- **Type equivalence checking**: `int?` and `int` might be equivalent in some contexts
- **Operator resolution**: Arithmetic operators work on `int`, not `int?`—unwrap first
- **Protocol matching**: Check if `T?` implements a protocol by checking if `T` does

**Contrast with `SemanticType.UnwrapNullable()`**: That method unwraps **one level** only. `UnwrapAllNullable()` is recursive.

---

### Type Equivalence and Compatibility

#### `AreEquivalent(SemanticType a, SemanticType b)`

**Purpose**: Checks if two types are structurally equivalent, considering nullability.

**Rules**:
1. **Unwrap nullables** for both types
2. **Compare nullability flags**: `int?` ≠ `int`
3. **Structural equality**: Delegates to `SemanticType.Equals()`

**Implementation**:
```csharp
public static bool AreEquivalent(SemanticType a, SemanticType b)
{
    // Unwrap nullables for comparison
    var unwrappedA = UnwrapAllNullable(a);
    var unwrappedB = UnwrapAllNullable(b);

    // Check nullable mismatch
    bool aNullable = a is NullableType;
    bool bNullable = b is NullableType;
    if (aNullable != bNullable)
        return false;

    return unwrappedA.Equals(unwrappedB);
}
```

**Key Difference from `Equals()`**:
- `int?.Equals(int?)` → `true` (record equality)
- `AreEquivalent(int?, int)` → `false` (explicit nullability check)

**Usage Context**:
- **Function overload resolution**: Are two function signatures equivalent?
- **Type inference**: Did we infer the same type from multiple branches?
- **Validation**: Checking if a variable's declared type matches its inferred type

---

#### `GetCommonType(SemanticType a, SemanticType b)`

**Purpose**: Finds a common supertype for two types, primarily for arithmetic operations and binary operators.

**Algorithm**:
1. **Identity**: If `a == b`, return `a`
2. **Numeric widening**: Apply numeric type promotion rules
3. **Nullable propagation**: If either type is nullable, make result nullable
4. **Failure**: Return `null` if no common type exists

**Numeric Widening Rules**:
```
byte/short → int → long → float → double
              ↑
           decimal (doesn't participate in widening)
```

**Implementation Highlights**:
```csharp
public static SemanticType? GetCommonType(SemanticType a, SemanticType b)
{
    // Same type
    if (a.Equals(b))
        return a;

    // Numeric widening: int -> long -> float -> double
    if (IsNumeric(a) && IsNumeric(b))
    {
        if (aClr == typeof(double) || bClr == typeof(double))
            return SemanticType.Double;
        if (aClr == typeof(float) || bClr == typeof(float))
            return SemanticType.Float;
        if (aClr == typeof(long) || bClr == typeof(long))
            return SemanticType.Long;
        return SemanticType.Int;
    }

    // Nullable handling
    if (a is NullableType nullableA)
    {
        var commonInner = GetCommonType(nullableA.UnderlyingType, b);
        if (commonInner != null)
            return new NullableType { UnderlyingType = commonInner };
    }
    // ... similar for b is NullableType

    return null; // No common type
}
```

**Examples**:
| Input A | Input B | Output |
|---------|---------|--------|
| `int` | `int` | `int` |
| `int` | `long` | `long` |
| `int` | `float` | `float` |
| `float` | `double` | `double` |
| `int?` | `int` | `int?` |
| `int` | `str` | `null` (no common type) |

**Usage Context**:
```csharp
// Type checking a binary operation: x + y
var leftType = GetType(leftOperand);
var rightType = GetType(rightOperand);
var commonType = TypeUtils.GetCommonType(leftType, rightType);

if (commonType == null)
{
    Error($"Cannot apply operator to {leftType} and {rightType}");
}
else
{
    // Result type of the operation is commonType
    SetType(binaryExpr, commonType);
}
```

**Important**: This method implements **implicit conversion rules**, not explicit casting. For explicit casts, see `TypeCastValidator`.

---

## Dependencies

`TypeUtils.cs` depends on:

1. **`SemanticType.cs`**: All type hierarchy classes
   - `BuiltinType`, `GenericType`, `NullableType`, `TupleType`, etc.
   - `SemanticType.Equals()` for structural equality

2. **`PrimitiveCatalog.cs`** (indirectly, via `BuiltinType.IsAssignableTo()`):
   - Defines implicit conversion rules between numeric types
   - Not directly called by `TypeUtils`, but influences behavior

3. **System Types**:
   - `System.Type` for CLR type comparisons
   - `System.Linq` for `FirstOrDefault()`, `Skip()`

**Files that depend on TypeUtils**:
- `TypeChecker.cs` (extensively—almost every expression/statement type check)
- `OperatorValidator.cs` (validating operator overloads)
- `ProtocolValidator.cs` (checking protocol implementations)
- `TypeInferenceService.cs` (inferring types from expressions)
- `RoslynEmitter*.cs` (deciding which C# types/operators to emit)

---

## Design Patterns and Conventions

### 1. **Static Utility Class Pattern**

All methods are `public static`, and the class has no constructor or instance state. This is appropriate for pure, side-effect-free functions.

**Benefits**:
- No memory allocation overhead
- Easy to call from anywhere in the compiler
- Thread-safe by design (no mutable state)

**Alternative Considered**: Dependency injection of an `ITypeUtilities` service. Rejected because:
- No need for testability via mocking (these are pure functions)
- No need for swappable implementations
- No need for scoped state

### 2. **Type Pattern Matching**

Heavy use of C# pattern matching for type discrimination:

```csharp
// C# 9.0 pattern matching with property patterns
return type is BuiltinType { Name: "str" or "string" };
return type is GenericType { Name: "dict" } generic;
```

**Advantages**:
- Concise, readable
- Type-safe (compiler checks exhaustiveness)
- Extracts nested properties inline

### 3. **Nullable Return Types**

Methods that might not find a result return `SemanticType?` (nullable reference type):

```csharp
public static SemanticType? GetElementType(SemanticType type)
public static SemanticType? GetKeyType(SemanticType type)
public static SemanticType? GetCommonType(SemanticType a, SemanticType b)
```

**Callers must handle null**:
```csharp
var elemType = TypeUtils.GetElementType(collectionType);
if (elemType == null)
{
    Error("Not a collection type");
}
```

### 4. **Fail-Fast Predicates**

All `IsXxx()` methods return `false` for unexpected input rather than throwing exceptions:

```csharp
// IsNumeric returns false for non-BuiltinType, not an exception
if (type is not BuiltinType builtin)
    return false;
```

**Design Rationale**: During error recovery, the semantic analyzer may construct malformed types. Predicates should be tolerant.

---

## Common Usage Patterns

### Pattern 1: Type Category Dispatch

```csharp
if (TypeUtils.IsNumeric(type))
{
    // Handle arithmetic
}
else if (TypeUtils.IsString(type))
{
    // Handle string concatenation
}
else if (TypeUtils.IsCollection(type))
{
    // Handle iteration
}
else
{
    Error("Unsupported type for this operation");
}
```

### Pattern 2: Collection Element Access

```csharp
// For list/set: get element type
// For dict: get value type
var elementType = TypeUtils.GetElementType(collectionType);
if (elementType == null)
{
    Error($"{collectionType} is not a collection");
}
else
{
    // Use elementType for validation
}
```

### Pattern 3: Nullable-Aware Type Checking

```csharp
// Unwrap nullable before checking category
var unwrapped = TypeUtils.UnwrapAllNullable(type);
if (TypeUtils.IsInteger(unwrapped))
{
    // type is int? or int?? or int
    // Process as integer
}
```

### Pattern 4: Binary Operation Type Inference

```csharp
var commonType = TypeUtils.GetCommonType(leftType, rightType);
if (commonType == null)
{
    Error($"No common type for {leftType} and {rightType}");
}
else if (TypeUtils.IsNumeric(commonType))
{
    // Result is numeric
    SetType(binaryExpr, commonType);
}
```

---

## Debugging Tips

### 1. **Unexpected `false` from Predicates**

**Symptom**: `IsNumeric()` returns `false` for what looks like a numeric type.

**Check**:
- Is it actually a `BuiltinType`? Use debugger to inspect runtime type.
- Does `builtin.ClrType` have a value? (It should never be null for numerics, but bugs happen.)
- Is the type wrapped in `NullableType`? Predicates check the **outer** type, not the inner.

**Fix**: Unwrap nullable first:
```csharp
var unwrapped = TypeUtils.UnwrapAllNullable(type);
if (TypeUtils.IsNumeric(unwrapped)) { ... }
```

### 2. **`GetElementType()` Returns Unexpected `null`**

**Symptom**: You expect a type to be a collection, but `GetElementType()` returns `null`.

**Check**:
- Is the type actually a `GenericType`? User-defined classes that implement `IEnumerable<T>` are **not** detected by `GetElementType()`.
- Did you mean to call `GetKeyType()` for a dict instead?
- Is the collection unparameterized (`list` without `[T]`)? Even then, it should have a type argument (possibly `UnknownType`).

**Debugging Commands**:
```csharp
Console.WriteLine($"Type: {type.GetType().Name}");
if (type is GenericType gt)
    Console.WriteLine($"Name: {gt.Name}, Args: {gt.TypeArguments.Count}");
```

### 3. **`GetCommonType()` Returns `null` for Compatible Types**

**Symptom**: Two types that should have a common type (like `int` and `long`) return `null`.

**Check**:
- Are both types `BuiltinType` with `ClrType` set?
- Is one of them `UnknownType` or `VoidType`?
- Are they both actually numeric according to `IsNumeric()`?

**Debugging**:
```csharp
Console.WriteLine($"A: {a} (IsNumeric: {TypeUtils.IsNumeric(a)})");
Console.WriteLine($"B: {b} (IsNumeric: {TypeUtils.IsNumeric(b)})");
if (a is BuiltinType ba)
    Console.WriteLine($"A ClrType: {ba.ClrType}");
if (b is BuiltinType bb)
    Console.WriteLine($"B ClrType: {bb.ClrType}");
```

### 4. **Nullable Handling Confusion**

**Symptom**: Type checks behave unexpectedly with nullable types.

**Remember**:
- `IsNumeric(int?)` → `false` (because `int?` is `NullableType`, not `BuiltinType`)
- `IsNumeric(UnwrapAllNullable(int?))` → `true`
- `AreEquivalent(int?, int)` → `false` (different nullability)
- `GetCommonType(int?, int)` → `int?` (nullable propagates)

**Rule of Thumb**: If in doubt, unwrap nullables explicitly before classification checks.

---

## Contribution Guidelines

### When to Add a New Method

Add a method to `TypeUtils` if:

1. **Reusable**: The logic is needed in 3+ places across the compiler
2. **Pure**: The method has no side effects and always returns the same output for the same input
3. **Type-focused**: It's fundamentally about classifying, comparing, or extracting information from types

**Don't add** if:
- Logic is specific to one component (put it in that component's helper class)
- It requires access to `SemanticInfo` or other context (use `TypeChecker` helper methods)
- It performs type resolution or inference (use `TypeResolver`/`TypeInferenceService`)

### Adding a Type Category Predicate

**Example**: Adding `IsCallable(SemanticType type)` to check if a type is callable.

**Steps**:

1. **Define the predicate**:
   ```csharp
   /// <summary>
   /// Check if a type is callable (function or callable object).
   /// </summary>
   public static bool IsCallable(SemanticType type)
   {
       return type is FunctionType ||
              (type is UserDefinedType udt && udt.Symbol?.HasCallMethod == true);
   }
   ```

2. **Add XML documentation** explaining:
   - What constitutes "callable" in Sharpy's type system
   - Any edge cases (e.g., lambdas, delegates)

3. **Update this walkthrough** with the new method.

4. **Add tests** (see `Sharpy.Compiler.Tests/Semantic/TypeUtilsTests.cs`—create if needed):
   ```csharp
   [Fact]
   public void IsCallable_FunctionType_ReturnsTrue()
   {
       var funcType = new FunctionType { /* ... */ };
       Assert.True(TypeUtils.IsCallable(funcType));
   }
   ```

5. **Consider symmetry**: If you add `IsCallable`, should you also add `GetCallSignature`?

### Extending `GetCommonType`

**Scenario**: You need to support common types for user-defined types (e.g., class inheritance).

**Considerations**:

1. **Complexity**: `GetCommonType` is already complex. Adding inheritance logic increases cognitive load.
2. **Alternative**: Consider adding a separate method `GetCommonTypeWithInheritance` that delegates to `GetCommonType` for primitives.
3. **Testing**: Extensive test coverage required for inheritance diamonds, multiple inheritance, etc.
4. **Performance**: Type resolution can be hot-path. Profile before adding expensive logic.

**Pattern to Follow**:
```csharp
public static SemanticType? GetCommonTypeWithInheritance(
    SemanticType a, 
    SemanticType b,
    SymbolTable symbolTable) // Need symbol table for inheritance queries
{
    // Try fast path first
    var simpleCommon = GetCommonType(a, b);
    if (simpleCommon != null)
        return simpleCommon;

    // Fallback: check inheritance
    if (a is UserDefinedType udtA && b is UserDefinedType udtB)
    {
        // Find common base class
        // ...
    }

    return null;
}
```

### Refactoring Existing Code to Use TypeUtils

If you find repeated type-checking logic elsewhere in the compiler:

1. **Extract** the logic into a new `TypeUtils` method
2. **Replace** all call sites with the new method
3. **Add tests** for the extracted method
4. **Verify** that behavior hasn't changed (run full test suite)

**Example Refactoring**:

**Before** (in `TypeChecker.cs`):
```csharp
// Scattered throughout the file:
if (type is BuiltinType bt && bt.ClrType == typeof(int)) { ... }
if (type is BuiltinType bt && bt.ClrType == typeof(long)) { ... }
// ... repeated 20+ times
```

**After**:
```csharp
// In TypeUtils:
public static bool IsIntegerType(SemanticType type)
{
    return type is BuiltinType { ClrType: var clr } && 
           (clr == typeof(int) || clr == typeof(long));
}

// In TypeChecker:
if (TypeUtils.IsIntegerType(type)) { ... }
```

---

## Cross-References

### Related Semantic Analysis Files

- **[`SemanticType.cs`](./SemanticType.md)** *(if walkthrough exists)*: Type hierarchy definitions
  - Defines `BuiltinType`, `GenericType`, `NullableType`, etc.
  - `IsAssignableTo()` method for type compatibility
  
- **[`TypeChecker.cs`](./TypeChecker.md)** *(if walkthrough exists)*: Main type checking logic
  - Heavily uses `TypeUtils` for type classification
  - Implements type narrowing and inference
  
- **[`TypeResolver.cs`](./TypeResolver.md)** *(if walkthrough exists)*: Resolves type annotations to `SemanticType`
  - Converts AST type annotations (`TypeAnnotation`) to `SemanticType` instances
  
- **[`PrimitiveCatalog.cs`](./PrimitiveCatalog.md)** *(if walkthrough exists)*: Numeric type conversion rules
  - Defines which numeric types can be implicitly converted

### Related Validation Files

- **`Semantic/Validation/OperatorValidator.cs`**: Uses `IsNumeric`, `GetCommonType`
- **`Semantic/Validation/ProtocolValidator.cs`**: Uses `IsCollection`, `IsList`, etc.
- **`Semantic/Validation/TypeCastValidator.cs`**: Uses `UnwrapAllNullable`, `AreEquivalent`

### Related CodeGen Files

- **`CodeGen/RoslynEmitter.cs`**: Uses type predicates to determine which C# operators to emit
- **`CodeGen/TypeMapper.cs`**: Maps `SemanticType` to Roslyn `TypeSyntax`

### Relevant Specification Documents

- [`docs/language_specification/type_hierarchy.md`](../../../../language_specification/type_hierarchy.md): Sharpy type system overview
- [`docs/language_specification/type_annotations.md`](../../../../language_specification/type_annotations.md): How types are declared
- [`docs/language_specification/type_casting.md`](../../../../language_specification/type_casting.md): Implicit vs. explicit conversion rules
- [`docs/language_specification/type_narrowing.md`](../../../../language_specification/type_narrowing.md): Type narrowing in control flow

---

## Summary

`TypeUtils.cs` is a **foundational utility** for the Sharpy compiler's semantic analysis. It provides:

✅ **Type classification predicates** (`IsNumeric`, `IsCollection`, etc.)  
✅ **Type extraction helpers** (`GetElementType`, `GetKeyType`)  
✅ **Nullable handling** (`UnwrapAllNullable`)  
✅ **Type compatibility logic** (`AreEquivalent`, `GetCommonType`)

**Key Takeaways for Newcomers**:

1. **This is a pure utility class** — no side effects, no state, just helper functions
2. **It operates on resolved types** (`SemanticType`), not AST nodes or strings
3. **Nullable handling is explicit** — most predicates check the outer type, so unwrap first
4. **`GetCommonType` implements implicit conversion** — not explicit casting
5. **Pattern matching is your friend** — learn C# 9.0 patterns to read this code fluently

When in doubt, **read the tests** (once they exist) or **trace through a call site** in the debugger to understand how these utilities are used in practice.

Happy hacking! 🚀
