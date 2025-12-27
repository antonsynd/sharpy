# Walkthrough: SemanticType.cs

**Source File**: `src/Sharpy.Compiler/Semantic/SemanticType.cs`

---

## Overview

`SemanticType.cs` is the **heart of Sharpy's type system** during semantic analysis. It defines the abstract type hierarchy that represents every possible type in a Sharpy program after parsing but before code generation.

Think of this file as the "type DNA" of Sharpy: it encodes what types exist (primitives, generics, user-defined classes), how they relate to each other (inheritance, assignability), and how they're displayed to users in error messages.

**Key Role**: This file bridges the gap between:
- **Parser output** (AST with type annotations as strings like `"list[int]"`)
- **Type checker** (needs to reason about type compatibility and conversions)
- **Code generator** (needs to emit proper C# types)

---

## Architecture Position

```
Lexer → Parser (AST) → [SemanticType lives here] → TypeChecker → CodeGen
                        ↓
                   Converts type annotations
                   to SemanticType instances
```

During the **semantic analysis phase**:
1. `TypeResolver` converts AST type annotations into `SemanticType` instances
2. `TypeChecker` uses `SemanticType.IsAssignableTo()` to validate assignments
3. `SemanticInfo` stores the mapping: `Expression → SemanticType`

---

## Class/Type Structure

### Base Class: `SemanticType`

```csharp
public abstract record SemanticType
```

An **abstract record** that uses C# 9's record feature for:
- **Immutability**: Types don't change once created (functional programming style)
- **Value equality**: Two `BuiltinType { Name = "int" }` instances are equal
- **Concise syntax**: `record` auto-generates `Equals()`, `GetHashCode()`, `ToString()`

#### Singleton Instances (Type Constants)

```csharp
public static readonly SemanticType Unknown = new UnknownType();
public static readonly SemanticType Void = new VoidType();
public static readonly SemanticType Int = new BuiltinType { Name = "int", ClrType = typeof(int) };
// ... etc
```

**Design Decision**: Common types are pre-allocated singletons to:
- **Reduce allocations**: `SemanticType.Int` reused everywhere instead of creating new instances
- **Enable reference equality**: `type == SemanticType.Int` works quickly
- **Improve debugging**: Easier to see "ah, this is THE Int type" in the debugger

---

## Type Hierarchy

```
SemanticType (abstract)
├── UnknownType          // Error recovery
├── VoidType             // "None" in Python, void in C#
├── BuiltinType          // int, float, bool, str, etc.
├── GenericType          // list[T], dict[K,V]
├── UserDefinedType      // Classes, interfaces
├── NullableType         // T?
├── FunctionType         // (int, str) -> bool
└── TupleType            // tuple[int, str, float]
```

Each type implements:
- `GetDisplayName()`: Human-readable name for error messages
- `IsAssignableTo(SemanticType other)`: Core type compatibility logic

---

## Key Methods Deep Dive

### `IsAssignableTo(SemanticType other)` - The Assignability Engine

This is **the most important method** in the type system. It answers: "Can I assign a value of `this` type to a variable of `other` type?"

#### Base Implementation (in `SemanticType`)

```csharp
public virtual bool IsAssignableTo(SemanticType other)
{
    // All types are assignable to object
    if (other is UserDefinedType { Name: "object" })
        return true;

    return this.Equals(other);
}
```

**Logic**:
1. **Universal base type**: Everything can be assigned to `object` (Python's object / .NET's `System.Object`)
2. **Default**: Two types are compatible if they're equal (same type)

#### Overrides in Derived Types

Each type refines this logic:

**`UnknownType.IsAssignableTo()`**:
```csharp
public override bool IsAssignableTo(SemanticType other) => true;
```
- **Always returns true** to prevent cascading errors
- Example: If `x` has type `Unknown` (due to parsing error), don't emit 100 "incompatible type" errors downstream

**`VoidType.IsAssignableTo()`** (representing `None`):
```csharp
public override bool IsAssignableTo(SemanticType other)
{
    // None can be assigned to any nullable type
    if (other is NullableType)
        return true;

    return base.IsAssignableTo(other);
}
```
- Python's `None` can be assigned to `int?`, `str?`, etc.
- Cannot be assigned to non-nullable types (caught by base implementation)

**`BuiltinType.IsAssignableTo()`**:
```csharp
public override bool IsAssignableTo(SemanticType other)
{
    if (base.IsAssignableTo(other)) return true;

    // Use PrimitiveCatalog for implicit conversion rules
    var thisInfo = PrimitiveCatalog.GetPrimitiveInfo(this);
    var otherInfo = PrimitiveCatalog.GetPrimitiveInfo(other);

    if (thisInfo != null && otherInfo != null)
    {
        return PrimitiveCatalog.CanImplicitlyConvert(thisInfo, otherInfo);
    }

    return false;
}
```
- **Delegates to `PrimitiveCatalog`** for numeric conversion rules
- Examples:
  - `int` → `long` ✅ (widening conversion)
  - `int` → `float` ✅ (implicit)
  - `long` → `int` ❌ (narrowing, requires explicit cast)

**`NullableType.IsAssignableTo()`**:
```csharp
public override bool IsAssignableTo(SemanticType other)
{
    // Nullable T is assignable to T (implicit unwrapping)
    if (UnderlyingType.IsAssignableTo(other))
        return true;

    // Nullable T is assignable to Nullable T
    if (other is NullableType otherNullable)
        return UnderlyingType.IsAssignableTo(otherNullable.UnderlyingType);

    return base.IsAssignableTo(other);
}
```
- **Key insight**: `int?` can be assigned to `int` (with null check at runtime)
- `int?` can also be assigned to `long?` if `int` → `long` is valid

**`UserDefinedType.IsAssignableTo()`**:
```csharp
public override bool IsAssignableTo(SemanticType other)
{
    if (base.IsAssignableTo(other)) return true;

    if (other is UserDefinedType otherUdt && Symbol != null)
    {
        // Check inheritance chain
        var current = Symbol.BaseType;
        while (current != null)
        {
            if (current == otherUdt.Symbol || current.Name == otherUdt.Name)
                return true;
            current = current.BaseType;
        }

        // Check interfaces
        return Symbol.Interfaces.Any(i => i == otherUdt.Symbol || i.Name == otherUdt.Name);
    }

    return false;
}
```
- **Walks the inheritance tree**: If `Dog : Animal`, then `Dog` → `Animal`
- **Checks interfaces**: `Dog : IComparable` means `Dog` → `IComparable`
- **Uses `Symbol`**: The `TypeSymbol` holds inheritance/interface info

---

### `GetDisplayName()` - User-Facing Type Names

Every type implements this for error messages.

**Examples**:

| Type | Display Name |
|------|-------------|
| `UnknownType` | `<?>` |
| `VoidType` | `None` |
| `BuiltinType { Name = "int" }` | `int` |
| `GenericType { Name = "list", TypeArguments = [Int] }` | `list[int]` |
| `NullableType { UnderlyingType = Int }` | `int?` |
| `FunctionType { ParameterTypes = [Int, Str], ReturnType = Bool }` | `(int, str) -> bool` |
| `TupleType { ElementTypes = [Int, Str] }` | `tuple[int, str]` |

**Why important**: When type checking fails, users see messages like:
```
Error: Cannot assign 'str' to variable of type 'int'
```
The display names come from `GetDisplayName()`.

---

## Individual Type Classes

### `UnknownType` - Error Recovery Hero

```csharp
public record UnknownType : SemanticType
{
    public override string GetDisplayName() => "<?>";
    public override bool IsAssignableTo(SemanticType other) => true;
}
```

**Purpose**: Graceful degradation when type resolution fails.

**Example scenario**:
```python
# In Sharpy code
x = undefined_variable  # Parser/semantic error
y = x + 5               # Should we emit error here too?
```

By assigning `x` the type `Unknown`, the compiler:
1. Reports the first error (`undefined_variable`)
2. Lets `x + 5` type-check successfully (since `Unknown` is assignable to anything)
3. Avoids cascading errors that confuse users

---

### `BuiltinType` - Primitives

```csharp
public record BuiltinType : SemanticType
{
    public string Name { get; init; } = string.Empty;
    public Type? ClrType { get; init; }
}
```

**Represents**: Sharpy's built-in primitives that map to .NET types

| Sharpy Name | CLR Type | Notes |
|-------------|----------|-------|
| `int` | `System.Int32` | Default integer |
| `long` | `System.Int64` | Large integers |
| `float` | `System.Single` | 32-bit float |
| `double` | `System.Double` | 64-bit float (default floating) |
| `bool` | `System.Boolean` | True/False |
| `str` | `System.String` | Immutable strings |

**The `ClrType` field**: Critical for code generation! The `RoslynEmitter` uses this to emit the correct C# type.

---

### `GenericType` - The Complex One

```csharp
public record GenericType : SemanticType
{
    public string Name { get; init; } = string.Empty;
    public List<SemanticType> TypeArguments { get; init; } = new();
    public TypeSymbol? GenericDefinition { get; init; }
}
```

**Represents**: Parameterized types like `list[int]`, `dict[str, User]`

**Key fields**:
- `Name`: The generic type name (e.g., `"list"`)
- `TypeArguments`: Concrete type parameters (e.g., `[SemanticType.Int]`)
- `GenericDefinition`: Points to the symbol defining the generic type (e.g., the `list` class)

**Assignability logic**:
```csharp
public override bool IsAssignableTo(SemanticType other)
{
    if (other is GenericType otherGeneric
        && Name == otherGeneric.Name
        && TypeArguments.Count == otherGeneric.TypeArguments.Count)
    {
        // Check if type arguments match exactly
        for (int i = 0; i < TypeArguments.Count; i++)
        {
            if (!TypeArguments[i].Equals(otherGeneric.TypeArguments[i]))
                return false;
        }
        return true;
    }
    return base.IsAssignableTo(other);
}
```

**Current limitation**: Requires **exact match** of type arguments
- `list[int]` → `list[int]` ✅
- `list[int]` → `list[long]` ❌
- Future: Covariance/contravariance (e.g., `list[Dog]` → `list[Animal]`)

#### Custom Equality Implementation

```csharp
public virtual bool Equals(GenericType? other)
{
    // Compare Name, GenericDefinition, and all TypeArguments
    // ...
}

public override int GetHashCode()
{
    var hash = new HashCode();
    hash.Add(Name);
    hash.Add(GenericDefinition);
    foreach (var arg in TypeArguments) hash.Add(arg);
    return hash.ToHashCode();
}
```

**Why override?**: The comment says "improves cache effectiveness in OperatorValidator"
- `OperatorValidator` caches operator signatures by type
- With proper `Equals()`/`GetHashCode()`, `list[int]` always hashes the same
- Without this, two `list[int]` instances might be unequal due to reference equality

---

### `UserDefinedType` - Classes and Interfaces

```csharp
public record UserDefinedType : SemanticType
{
    public string Name { get; init; } = string.Empty;
    public TypeSymbol? Symbol { get; init; }
}
```

**Represents**: User-defined classes, structs, interfaces from Sharpy code

**The `Symbol` field**: Links to `TypeSymbol` which contains:
- Base class (`BaseType`)
- Implemented interfaces (`Interfaces`)
- Members (fields, methods, properties)
- Operator/protocol methods

**Example**:
```python
# Sharpy code
class Animal:
    def speak(self) -> str:
        return "..."

class Dog(Animal):
    def speak(self) -> str:
        return "Woof!"
```

- `Dog` → `UserDefinedType { Name = "Dog", Symbol = <TypeSymbol for Dog> }`
- `Symbol.BaseType` → `<TypeSymbol for Animal>`
- `IsAssignableTo()` walks from `Dog` → `Animal` → checks match

---

### `NullableType` - Optional Values

```csharp
public record NullableType : SemanticType
{
    public SemanticType UnderlyingType { get; init; } = SemanticType.Unknown;
}
```

**Represents**: Sharpy's `T?` syntax (Python's `Optional[T]`)

**Examples**:
- `int?` → `NullableType { UnderlyingType = Int }`
- `list[str]?` → `NullableType { UnderlyingType = GenericType { Name = "list", TypeArguments = [Str] } }`

**Critical assignability rule**: `int?` can be assigned to `int`
```csharp
if (UnderlyingType.IsAssignableTo(other))
    return true;
```

This enables:
```python
x: int? = 5
y: int = x  # Type checks! Runtime: null check inserted
```

---

### `FunctionType` - First-Class Functions

```csharp
public record FunctionType : SemanticType
{
    public List<SemanticType> ParameterTypes { get; init; } = new();
    public SemanticType ReturnType { get; init; } = SemanticType.Void;
}
```

**Represents**: Function signatures for lambdas, delegates, callbacks

**Example**:
```python
# Sharpy code
def process(callback: (int, str) -> bool) -> None:
    result = callback(42, "test")
```

- `callback` has type: `FunctionType { ParameterTypes = [Int, Str], ReturnType = Bool }`

**Future work**: Currently no `IsAssignableTo()` override, so function types must match exactly. Future: Covariance/contravariance for parameters/return types.

---

### `TupleType` - Heterogeneous Collections

```csharp
public record TupleType : SemanticType
{
    public List<SemanticType> ElementTypes { get; init; } = new();
}
```

**Represents**: Fixed-size, heterogeneous tuples

**Example**:
```python
# Sharpy code
point: tuple[int, int] = (10, 20)
mixed: tuple[str, int, bool] = ("hello", 42, true)
```

- `point` → `TupleType { ElementTypes = [Int, Int] }`
- `mixed` → `TupleType { ElementTypes = [Str, Int, Bool] }`

**Note**: Currently no custom `IsAssignableTo()`, so tuples must match exactly (same count and types).

---

## Dependencies

### Internal Dependencies

1. **`Symbol.cs`** (especially `TypeSymbol`):
   - `UserDefinedType.Symbol` links here
   - `GenericType.GenericDefinition` links here
   - Provides inheritance/interface data for assignability checks

2. **`PrimitiveCatalog.cs`**:
   - `BuiltinType.IsAssignableTo()` delegates conversion rules here
   - Centralizes knowledge of numeric promotions (int→long, float→double, etc.)

3. **`SemanticInfo.cs`**:
   - Stores the mapping: `Expression → SemanticType`
   - The "database" that remembers what type each AST node has

### Usage Sites

- **`TypeResolver.cs`**: Constructs `SemanticType` instances from AST type annotations
- **`TypeChecker.cs`**: Calls `IsAssignableTo()` to validate assignments, parameters, return types
- **`RoslynEmitter.cs`** (CodeGen): Converts `SemanticType` → Roslyn syntax nodes for C# types
- **`OperatorValidator.cs`**: Caches operator signatures using `GenericType` equality

---

## Patterns and Design Decisions

### 1. **Immutable Records Pattern**

**Decision**: Use C# `record` instead of `class`

**Why**:
- **Functional purity**: Types don't mutate; easier to reason about
- **Value equality**: `new BuiltinType { Name = "int" }` equals another `new BuiltinType { Name = "int" }`
- **Pattern matching**: Easy to use with `if (type is GenericType { Name: "list" })`
- **Cache-friendly**: Same types produce same hash codes

### 2. **Singleton Pattern for Common Types**

**Decision**: Pre-allocate `SemanticType.Int`, `SemanticType.Str`, etc.

**Why**:
- **Performance**: Avoid allocating thousands of `BuiltinType { Name = "int" }` instances
- **Reference equality**: Can use `==` for quick checks (though value equality also works)
- **Canonical types**: One authoritative `Int` type instance

### 3. **Visitor Pattern (Not Used)**

**Decision**: No visitor pattern; use pattern matching instead

**Why**:
- C# 9+ pattern matching (`switch` expressions, `is` patterns) is ergonomic
- Adding a new type requires updating switch expressions (compile error if missed)
- Visitor pattern adds boilerplate without much benefit in modern C#

### 4. **Delegation to `PrimitiveCatalog`**

**Decision**: `BuiltinType` doesn't contain conversion logic; delegates to `PrimitiveCatalog`

**Why**:
- **Separation of concerns**: Conversion rules are complex and centralized
- **Easier to test**: Test conversion rules independently of the type system
- **Consistency**: All numeric conversions follow the same rules

### 5. **Type Narrowing (Implicit in Design)**

**Note**: `SemanticType` itself doesn't handle type narrowing (e.g., `if x is not None: ...`). That's handled by `TypeChecker` with a separate `_narrowedTypes` dictionary.

**Why separate**:
- Type narrowing is **context-dependent** (depends on control flow)
- `SemanticType` is **context-independent** (just describes the type)
- Keeps `SemanticType` simple and reusable

---

## Debugging Tips

### 1. **Trace Assignability Checks**

Add a breakpoint in `IsAssignableTo()` to see why types are/aren't compatible:

```csharp
// In SemanticType.cs
public virtual bool IsAssignableTo(SemanticType other)
{
    // Set breakpoint here ← Debugger will hit for every assignability check
    if (other is UserDefinedType { Name: "object" })
        return true;
    return this.Equals(other);
}
```

**Pro tip**: Use a conditional breakpoint:
```
other.GetDisplayName() == "SomeTypeYouCareAbout"
```

### 2. **Inspect Display Names**

When debugging, hover over a `SemanticType` and call `GetDisplayName()` in the watch window:
```
type.GetDisplayName()  // Shows "list[int]" instead of memory address
```

### 3. **Check Type Equality**

If assignability is failing unexpectedly, verify:
```csharp
// In watch window or immediate mode
this.Equals(other)              // Should match?
this.GetHashCode() == other.GetHashCode()  // Should be equal if types are equal
```

For `GenericType`, check:
```csharp
TypeArguments[0].Equals(otherGeneric.TypeArguments[0])
```

### 4. **Look for Null Symbols**

`UserDefinedType` and `GenericType` have `Symbol` and `GenericDefinition` fields that can be `null`:
```csharp
if (Symbol != null)  // Always check before dereferencing
```

**Common bug**: Symbol is `null` because `TypeResolver` failed to resolve the type → creates `UserDefinedType` with no symbol → assignability check fails silently.

### 5. **Trace Type Resolution**

If a type isn't what you expect, trace backward:
1. Set breakpoint in `TypeResolver.ResolveType()`
2. See what AST type annotation it's processing
3. See what `SemanticType` it creates

### 6. **Use `ToString()` for Quick Inspection**

Records auto-generate `ToString()`:
```csharp
var type = new GenericType { Name = "list", TypeArguments = new() { SemanticType.Int } };
Console.WriteLine(type);
// Output: GenericType { Name = list, TypeArguments = [BuiltinType { Name = int, ClrType = System.Int32 }], ... }
```

---

## Common Issues and Solutions

### Issue: "Types should match but don't"

**Symptom**: `list[int]` not assignable to `list[int]`

**Cause**: Two separate `GenericType` instances with different `GenericDefinition` symbols

**Solution**: Ensure `TypeResolver` reuses the same `TypeSymbol` for generic types

---

### Issue: "Implicit conversion not working"

**Symptom**: `int` not assignable to `long`

**Cause**: `PrimitiveCatalog` may not be set up correctly

**Solution**:
1. Check `PrimitiveCatalog.CanImplicitlyConvert()`
2. Verify `BuiltinType.IsAssignableTo()` is calling the catalog

---

### Issue: "Nullable assignability wrong"

**Symptom**: `int?` not assignable to `int`

**Cause**: Check `NullableType.IsAssignableTo()` logic

**Solution**: Verify the unwrapping logic:
```csharp
if (UnderlyingType.IsAssignableTo(other))
    return true;  // This should allow int? → int
```

---

## Contribution Guidelines

### Adding a New Type

**Example**: Adding `ArrayType` for fixed-size arrays:

1. **Define the record**:
```csharp
/// <summary>
/// Fixed-size array type (e.g., int[10])
/// </summary>
public record ArrayType : SemanticType
{
    public SemanticType ElementType { get; init; } = SemanticType.Unknown;
    public int Size { get; init; }

    public override string GetDisplayName() => $"{ElementType.GetDisplayName()}[{Size}]";

    public override bool IsAssignableTo(SemanticType other)
    {
        // Arrays of same element type and size
        if (other is ArrayType otherArray)
            return Size == otherArray.Size && ElementType.Equals(otherArray.ElementType);

        return base.IsAssignableTo(other);
    }
}
```

2. **Update `TypeResolver.cs`** to construct `ArrayType` from AST
3. **Update `RoslynEmitter.cs`** to emit C# array syntax
4. **Add tests** in `Sharpy.Compiler.Tests/Semantic/TypeCheckerTests.cs`

---

### Modifying Assignability Rules

**Example**: Allow `list[Dog]` to be assigned to `list[Animal]` (covariance)

**Current code**:
```csharp
// In GenericType.IsAssignableTo()
if (!TypeArguments[i].Equals(otherGeneric.TypeArguments[i]))
    return false;  // Requires exact match
```

**Proposed change**:
```csharp
// Check if type arguments are assignable (not just equal)
if (!TypeArguments[i].IsAssignableTo(otherGeneric.TypeArguments[i]))
    return false;
```

**⚠️ Warning**: This enables covariance but is **unsound** for mutable collections!
- Safe: `IEnumerable[Dog]` → `IEnumerable[Animal]` (read-only)
- Unsafe: `List[Dog]` → `List[Animal]` (can insert `Cat` into `List[Dog]`!)

**Proper solution**: Add variance annotations to generic type parameters (like C#'s `in`/`out`).

---

### Testing Assignability

**Pattern**:
```csharp
[Fact]
public void TestAssignability_IntToLong()
{
    var intType = SemanticType.Int;
    var longType = SemanticType.Long;

    Assert.True(intType.IsAssignableTo(longType));
    Assert.False(longType.IsAssignableTo(intType));  // Narrowing
}
```

**Pro tip**: Test both directions and edge cases (nullables, generics, etc.).

---

## Future Enhancements

### 1. **Variance for Generic Types**

**Goal**: Support covariance/contravariance

```python
# Sharpy code (future)
animals: list[Animal] = list[Dog]()  # Covariance
```

**Implementation**:
- Add `Variance` enum (`Covariant`, `Contravariant`, `Invariant`)
- Store variance per type parameter in `TypeSymbol`
- Update `GenericType.IsAssignableTo()` to respect variance

---

### 2. **Union Types**

**Goal**: Support `int | str` (TypeScript-style unions)

```csharp
public record UnionType : SemanticType
{
    public List<SemanticType> Alternatives { get; init; } = new();

    public override bool IsAssignableTo(SemanticType other)
    {
        // Union is assignable if ALL alternatives are assignable
        return Alternatives.All(alt => alt.IsAssignableTo(other));
    }
}
```

---

### 3. **Structural Subtyping**

**Goal**: Duck typing for protocols/interfaces

```python
# Sharpy code (future)
protocol Drawable:
    def draw(self) -> None

# Any type with draw() is assignable to Drawable
```

**Implementation**:
- Add `ProtocolType` (like `UserDefinedType` but checks members structurally)
- Update `IsAssignableTo()` to check method signatures match

---

### 4. **Better Error Messages**

**Goal**: When assignability fails, explain why

```csharp
public virtual (bool, string?) IsAssignableToWithReason(SemanticType other)
{
    if (IsAssignableTo(other))
        return (true, null);

    return (false, $"Cannot assign '{GetDisplayName()}' to '{other.GetDisplayName()}'");
}
```

Then `TypeChecker` can show: `"Cannot assign 'Dog' to 'Cat': types are unrelated"`

---

## Summary

`SemanticType.cs` is the **type system kernel** of Sharpy:
- **Defines all type variants**: Built-ins, generics, user types, nullables, functions, tuples
- **Implements assignability**: The `IsAssignableTo()` method is the decision engine for type compatibility
- **Bridges phases**: Connects AST (strings) → semantic analysis (types) → code gen (Roslyn)

**Key insights**:
- Immutable records enable value equality and functional reasoning
- Singleton instances reduce allocations
- Delegation to `PrimitiveCatalog` centralizes conversion rules
- Future enhancements require careful design (variance, unions, structural typing)

**When debugging**: Focus on `IsAssignableTo()` logic and ensure `TypeResolver` creates types correctly.

**When contributing**: Add tests for new types, update assignability rules carefully (soundness!), and maintain the clean separation between type definitions and type checking logic.
