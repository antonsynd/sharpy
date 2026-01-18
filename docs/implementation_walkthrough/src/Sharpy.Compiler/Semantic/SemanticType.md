# Walkthrough: SemanticType.cs

**Source File**: `src/Sharpy.Compiler/Semantic/SemanticType.cs`

---

## Overview

`SemanticType.cs` defines the type system used during semantic analysis in the Sharpy compiler. This file contains the core abstraction for representing types after parsing but before code generation. Think of it as the "type vocabulary" the compiler uses to reason about type compatibility, assignability, and type checking.

**Role in Pipeline**:
- **Input**: Receives type information from the Parser (via AST type annotations)
- **Processing**: Validates type compatibility, checks assignability, supports type inference
- **Output**: Provides type information to CodeGen (RoslynEmitter) for C# code generation

**Key Insight**: While the AST has a `TypeExpression` that represents what the programmer *wrote*, `SemanticType` represents what the compiler *understands* about types. This distinction is crucial for proper type checking.

---

## Class/Type Structure

The file defines a type hierarchy using C# records (immutable reference types):

```
SemanticType (abstract base)
├── UnknownType          - Error recovery
├── VoidType             - None/void return
├── BuiltinType          - int, str, bool, float, etc.
├── GenericType          - list[int], dict[str, int]
├── UserDefinedType      - Custom classes, structs, interfaces
├── NullableType         - T? (nullable wrapper)
├── FunctionType         - Lambda and function types
├── TupleType            - tuple[int, str]
├── ModuleType           - Imported modules as namespaces
├── TypeParameterType    - Generic type parameters (T, U)
└── GenericFunctionType  - Instantiated generic functions
```

**Design Decision**: Using C# `record` types provides:
- Immutability (types don't change after creation)
- Value-based equality by default (two `int` types are equal)
- Pattern matching support (useful for type checking)

---

## Singleton Type Instances

The base `SemanticType` class defines commonly-used type singletons:

```csharp
public static readonly SemanticType Unknown = new UnknownType();
public static readonly SemanticType Void = new VoidType();
public static readonly SemanticType Int = new BuiltinType { Name = "int", ClrType = typeof(int) };
public static readonly SemanticType Float = new BuiltinType { Name = "float", ClrType = typeof(double) };
// ... more primitives
```

**Why Singletons?**
- Performance: Avoid allocating the same type repeatedly
- Identity checks: Can use reference equality for common types
- Consistency: Everyone uses the same `Int` instance

**Important Note**: Sharpy's `float` maps to C# `double` (64-bit), while `float32` maps to C# `float` (32-bit). See line 13-16 for this mapping.

---

## Key Methods

### `IsAssignableTo(SemanticType other)` - The Core Type Compatibility Check

This virtual method determines if a value of this type can be assigned to a variable of another type. Every `SemanticType` subclass overrides this to define its specific assignability rules.

**Base Implementation** (lines 24-31):
```csharp
public virtual bool IsAssignableTo(SemanticType other)
{
    // All types are assignable to object
    if (other is UserDefinedType { Name: "object" })
        return true;

    return this.Equals(other);
}
```

**Key Design Pattern**: The method uses C# pattern matching extensively. For example:
- `other is UserDefinedType { Name: "object" }` - checks if `other` is a UserDefinedType AND its Name is "object"

**Why Virtual?** Each type has unique assignability rules:

1. **UnknownType** (line 46): Always assignable to anything to avoid cascading errors during error recovery
2. **VoidType** (lines 56-63): Can be assigned to nullable types (None → T?)
3. **BuiltinType** (lines 76-90): Delegates to `PrimitiveCatalog` for implicit conversion rules (e.g., int → long)
4. **GenericType** (lines 108-125): Checks type arguments match exactly (no variance support yet - see line 114 comment)
5. **UserDefinedType** (lines 172-196): Walks inheritance chain and checks interfaces
6. **NullableType** (lines 208-219): Supports implicit unwrapping (T? → T)
7. **TupleType** (lines 250-262): Element-wise assignability check

### `GetDisplayName()` - Human-Readable Type Names

Returns a string representation for error messages and debugging.

**Examples**:
- `int` → "int"
- `list[int]` → "list[int]"
- `int?` → "int?"
- `(int, str) -> bool` → "(int, str) -> bool"
- Module → "module 'math'"
- Unknown → "<?>"

**Usage**: TypeChecker uses this when reporting type mismatch errors to users.

---

## Detailed Type Breakdown

### UnknownType - The Error Recovery Type

```csharp
public record UnknownType : SemanticType
{
    public override string GetDisplayName() => "<?>";
    public override bool IsAssignableTo(SemanticType other) => true;
}
```

**Purpose**: When type checking encounters an error (e.g., undefined variable), it returns `Unknown` instead of crashing. The `IsAssignableTo` always returns `true` to prevent cascading errors.

**Example Scenario**:
```python
x = undefined_variable  # x gets type Unknown
y: int = x              # Doesn't report a type error, avoiding cascading noise
```

### VoidType - Representing "None"

```csharp
public record VoidType : SemanticType
{
    public override string GetDisplayName() => "None";

    public override bool IsAssignableTo(SemanticType other)
    {
        // None can be assigned to any nullable type
        if (other is NullableType)
            return true;

        return base.IsAssignableTo(other);
    }
}
```

**Key Feature**: Sharpy's `None` can be assigned to nullable types, implementing the common pattern:
```python
x: int? = None  # Valid
y: int = None   # Type error
```

### BuiltinType - Primitives with CLR Mapping

```csharp
public record BuiltinType : SemanticType
{
    public string Name { get; init; } = string.Empty;
    public Type? ClrType { get; init; }  // Maps to .NET runtime type

    public override bool IsAssignableTo(SemanticType other)
    {
        if (base.IsAssignableTo(other)) return true;

        // Use PrimitiveCatalog for implicit conversion rules
        var thisInfo = PrimitiveCatalog.GetPrimitiveInfo(this);
        var otherInfo = PrimitiveCatalog.GetPrimitiveInfo(other);

        if (thisInfo != null && otherInfo != null)
            return PrimitiveCatalog.CanImplicitlyConvert(thisInfo, otherInfo);

        return false;
    }
}
```

**ClrType Property**: Links Sharpy types to .NET types for code generation. Example: `SemanticType.Str` has `ClrType = typeof(string)`.

**Implicit Conversion**: Delegates to `PrimitiveCatalog` which implements the numeric promotion rules (int → long, int → float, etc.).

**Builtin Types Table**:

| Sharpy Name | CLR Type | Line | Notes |
|-------------|----------|------|-------|
| `int` | `System.Int32` | 11 | Default integer type |
| `long` | `System.Int64` | 12 | Large integers |
| `float` | `System.Double` | 14 | **64-bit** (Sharpy float = C# double) |
| `double` | `System.Double` | 15 | Alias for float |
| `float32` | `System.Single` | 16 | 32-bit float (explicit when needed) |
| `bool` | `System.Boolean` | 17 | Boolean values |
| `str` | `System.String` | 18 | Immutable strings |

### GenericType - Parameterized Types

```csharp
public record GenericType : SemanticType
{
    public string Name { get; init; } = string.Empty;
    public List<SemanticType> TypeArguments { get; init; } = new();
    public TypeSymbol? GenericDefinition { get; init; }

    public override string GetDisplayName()
    {
        var args = string.Join(", ", TypeArguments.Select(t => t.GetDisplayName()));
        return $"{Name}[{args}]";
    }
}
```

**Components**:
- `Name`: The generic type name (e.g., "list", "dict")
- `TypeArguments`: Concrete types supplied (e.g., [int] for list[int])
- `GenericDefinition`: Reference to the TypeSymbol defining the generic class

**Custom Equality** (lines 129-159): Overrides `Equals` and `GetHashCode` to compare by content, not reference. This is crucial for:
- Cache effectiveness in `OperatorValidator` (see line 128 comment)
- Correct type comparison (two `list[int]` instances should be equal)

**Assignability Logic**:
The `IsAssignableTo` method (lines 108-125) currently requires exact type argument matches. For example:
- `list[int]` is assignable to `list[int]` ✅
- `list[int]` is NOT assignable to `list[long]` ❌

**Future Work**: Line 114 notes that covariance/contravariance is not yet implemented. This would allow derived types in certain positions (e.g., `list[Dog]` → `IEnumerable[Animal]` for read-only collections).

### UserDefinedType - Custom Classes and Structs

```csharp
public record UserDefinedType : SemanticType
{
    public string Name { get; init; } = string.Empty;
    public TypeSymbol? Symbol { get; init; }  // Links to symbol table

    public override bool IsAssignableTo(SemanticType other)
    {
        if (base.IsAssignableTo(other)) return true;

        if (other is UserDefinedType otherUdt && Symbol != null)
        {
            // Same type
            if (Symbol == otherUdt.Symbol || Name == otherUdt.Name)
                return true;

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
}
```

**Symbol Linkage**: The `Symbol` property connects to the `TypeSymbol` in the symbol table, providing access to:
- Base class hierarchy
- Implemented interfaces
- Member methods and fields

**Inheritance Check**: Walks the inheritance chain to support derived-to-base assignments:
```python
class Animal: pass
class Dog(Animal): pass

def feed(animal: Animal): pass

dog = Dog()
feed(dog)  # Valid: Dog is assignable to Animal
```

**Note on `object` type**: The special `object` type (line 19) is defined as a `UserDefinedType` rather than a `BuiltinType`. This represents the universal base type that all other types can be assigned to (see base `IsAssignableTo` implementation at line 27).

### NullableType - Optional Values

```csharp
public record NullableType : SemanticType
{
    public SemanticType UnderlyingType { get; init; } = SemanticType.Unknown;

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
}
```

**Key Feature**: Implicit unwrapping (line 211) allows:
```python
x: int? = 42
y: int = x  # Valid in Sharpy (unsafe but convenient)
```

**Nullable Chain**: `int?` is assignable to both `int` (via unwrapping) and to `int?` (via underlying type check).

**Relationship with VoidType**: The `VoidType` (representing `None`) can be assigned to any `NullableType` (see VoidType.IsAssignableTo at line 59), enabling:
```python
x: int? = None  # None is assignable to int?
```

### FunctionType - First-Class Functions

```csharp
public record FunctionType : SemanticType
{
    public List<SemanticType> ParameterTypes { get; init; } = new();
    public SemanticType ReturnType { get; init; } = SemanticType.Void;

    public override string GetDisplayName()
    {
        var params_ = string.Join(", ", ParameterTypes.Select(p => p.GetDisplayName()));
        return $"({params_}) -> {ReturnType.GetDisplayName()}";
    }
}
```

**Usage**: Represents lambda expressions and function references:
```python
f: (int, int) -> int = lambda x, y: x + y
```

**Note**: Currently doesn't override `IsAssignableTo`, meaning function type compatibility is based on structural equality (exact parameter and return type match).

### TupleType - Structural Product Types

```csharp
public record TupleType : SemanticType
{
    public List<SemanticType> ElementTypes { get; init; } = new();

    public override bool IsAssignableTo(SemanticType other)
    {
        if (other is TupleType otherTuple && ElementTypes.Count == otherTuple.ElementTypes.Count)
        {
            for (int i = 0; i < ElementTypes.Count; i++)
            {
                if (!ElementTypes[i].IsAssignableTo(otherTuple.ElementTypes[i]))
                    return false;
            }
            return true;
        }
        return base.IsAssignableTo(other);
    }
}
```

**Element-wise Assignability**: Tuples are assignable if each element is assignable:
```python
x: tuple[int, str] = (42, "hello")
y: tuple[int, object] = x  # Valid: str is assignable to object
```

**Custom Equality**: Like `GenericType`, overrides equality (lines 265-290) for content-based comparison. This ensures two `tuple[int, str]` instances are considered equal even if they're different object instances.

### ModuleType - Namespace Representation

```csharp
public record ModuleType : SemanticType
{
    public ModuleSymbol Symbol { get; init; } = null!;

    public override string GetDisplayName() => $"module '{Symbol.Name}'";
}
```

**Usage**: Represents imported modules used as namespaces:
```python
import math
x = math.pi  # 'math' has type ModuleType
```

**No Assignability**: Modules are not assignable to other types (doesn't override `IsAssignableTo`).

### TypeParameterType - Generic Type Variables

```csharp
public record TypeParameterType : SemanticType
{
    public string Name { get; init; } = string.Empty;
    public TypeSymbol? DeclaringType { get; init; }

    public override bool IsAssignableTo(SemanticType other)
    {
        // Type parameters can be assigned to themselves
        if (other is TypeParameterType otherParam && Name == otherParam.Name)
            return true;

        // Type parameters can be assigned to object
        return base.IsAssignableTo(other);
    }
}
```

**Context**: Used inside generic class/function bodies:
```python
class Box[T]:
    def get(self) -> T:
        # Inside here, T is a TypeParameterType
        return self.value
```

**Limited Assignability**: Only assignable to itself or `object`, reflecting that we don't know the concrete type at compile time.

### GenericFunctionType - Instantiated Generic Functions

```csharp
public record GenericFunctionType : SemanticType
{
    public FunctionSymbol FunctionSymbol { get; init; } = null!;
    public List<SemanticType> TypeArguments { get; init; } = new();

    public override string GetDisplayName()
    {
        var typeArgs = string.Join(", ", TypeArguments.Select(t => t.GetDisplayName()));
        var paramTypes = string.Join(", ", FunctionSymbol.Parameters.Select(p => p.Type.GetDisplayName()));
        return $"{FunctionSymbol.Name}[{typeArgs}]({paramTypes}) -> {FunctionSymbol.ReturnType.GetDisplayName()}";
    }
}
```

**Purpose**: Internal bridge type used when calling generic functions with explicit type arguments (see line 328 comment):
```python
def identity[T](value: T) -> T:
    return value

x = identity[int](42)  # identity[int] becomes GenericFunctionType
```

**Flow**: `IndexAccess` node creates `GenericFunctionType` → `FunctionCall` node consumes it for type inference (line 329).

---

## Dependencies

This file has minimal dependencies, making it a foundational type:

1. **PrimitiveCatalog** (`Sharpy.Compiler.Semantic.PrimitiveCatalog`):
   - Used by `BuiltinType.IsAssignableTo` for implicit conversion rules (line 81-86)
   - See: `docs/implementation_walkthrough/src/Sharpy.Compiler/Semantic/PrimitiveCatalog.md`

2. **Symbol** (`Sharpy.Compiler.Semantic.Symbol`):
   - Several types reference symbol table entries:
     - `GenericType.GenericDefinition` → `TypeSymbol` (line 100)
     - `UserDefinedType.Symbol` → `TypeSymbol` (line 168)
     - `ModuleType.Symbol` → `ModuleSymbol` (line 298)
     - `GenericFunctionType.FunctionSymbol` → `FunctionSymbol` (line 332)
   - See: `docs/implementation_walkthrough/src/Sharpy.Compiler/Semantic/Symbol.md`

3. **.NET BCL**:
   - `System.Type` for CLR type mapping in `BuiltinType` (line 72)
   - `System.Linq` for collection operations

---

## Patterns and Design Decisions

### 1. Immutable Records for Type Safety

Using C# `record` types (line 6) ensures:
- Types cannot be mutated after creation
- Thread-safe (no concurrent modification issues)
- Value-based equality (two structurally identical types are equal)

### 2. Virtual Method Pattern for Extensibility

The `IsAssignableTo` and `GetDisplayName` methods are virtual, allowing each subclass to define its behavior. This is the classic object-oriented "Template Method" pattern.

### 3. Content-Based Equality for Composite Types

`GenericType` (lines 129-159) and `TupleType` (lines 265-290) override `Equals` and `GetHashCode` to compare their collections:
```csharp
// Two list[int] types with different List instances are still equal
var type1 = new GenericType { Name = "list", TypeArguments = [SemanticType.Int] };
var type2 = new GenericType { Name = "list", TypeArguments = [SemanticType.Int] };
type1.Equals(type2) // true
```

**Why?** Enables caching in `OperatorValidator` (per line 128 comment) and correct type comparisons.

### 4. Singleton Pattern for Common Types

Pre-allocated singletons (lines 9-19) avoid repeated allocation:
```csharp
SemanticType.Int  // Always the same instance
```

### 5. Null Object Pattern for Unknown Type

`UnknownType` acts as a null object, allowing the compiler to continue type checking even after errors without null reference exceptions (line 46).

### 6. Bridge Pattern for Symbol Table Integration

Types like `UserDefinedType` and `GenericType` hold references to symbol table entries, bridging the type system and symbol resolution.

---

## Debugging Tips

### Debugging Type Assignability Issues

When a type check fails unexpectedly:

1. **Check the IsAssignableTo chain**: Add breakpoints in the relevant `IsAssignableTo` override
2. **Inspect GetDisplayName**: Use this in the debugger to see human-readable type names
3. **Check symbol linkage**: For `UserDefinedType`, verify the `Symbol` property is not null
4. **Verify type arguments**: For `GenericType`, inspect the `TypeArguments` list

**Common Pitfall**: Forgetting that `NullableType` allows implicit unwrapping (T? → T).

### Inspecting Types in the Debugger

Set a watch expression:
```csharp
someType.GetDisplayName()  // Shows human-readable name
someType.GetType().Name    // Shows C# class name (e.g., "GenericType")
```

### Tracking Type Flow

To understand where a type comes from:
1. Set breakpoint on the `SemanticType` constructor
2. Enable "Break on all CLR exceptions" for type errors
3. Use call stack to trace back to TypeChecker or TypeResolver

### Testing Type Relationships

You can write unit tests using the existing singleton types:
```csharp
Assert.True(SemanticType.Int.IsAssignableTo(SemanticType.Long));  // Numeric widening
Assert.True(SemanticType.Void.IsAssignableTo(new NullableType { UnderlyingType = SemanticType.Int }));  // None → int?
```

---

## Contribution Guidelines

### When to Modify This File

You should modify `SemanticType.cs` when:

1. **Adding a new type category** (e.g., union types, intersection types)
   - Create a new record inheriting from `SemanticType`
   - Override `GetDisplayName` and `IsAssignableTo`
   - Add custom equality if the type has collections

2. **Changing assignability rules** (e.g., adding variance support)
   - Modify the relevant `IsAssignableTo` override
   - Update tests in `Sharpy.Tests` to cover new behavior
   - Consider impact on TypeChecker and CodeGen

3. **Adding new built-in types** (e.g., `byte`, `decimal`)
   - Add a new singleton to the base class
   - Map it to the appropriate CLR type
   - Register in `PrimitiveCatalog` if it's a numeric type

4. **Adding metadata to types** (e.g., nullability annotations, variance markers)
   - Add properties to the relevant record
   - Update equality/hashcode if needed

### What NOT to Change

- **Don't break immutability**: Never add settable properties
- **Don't add logic to the base class**: Keep `SemanticType` abstract and simple
- **Don't skip equality overrides**: If your type has collections, override `Equals`/`GetHashCode`

### Testing Changes

When modifying this file:
1. Run type checker tests: `dotnet test --filter TypeChecker`
2. Run semantic analysis tests: `dotnet test --filter Semantic`
3. Add new test cases demonstrating the changed behavior
4. Test edge cases (e.g., nested generics, inheritance chains)

### Code Style Conventions

Follow these patterns established in the file:
- Use `record` for immutability
- Use init-only properties (`{ get; init; }`)
- Use pattern matching in `IsAssignableTo`
- Provide XML doc comments for new types
- Keep display names concise and readable

---

## Cross-References

**Closely Related Files**:

- **Symbol.md** (`docs/implementation_walkthrough/src/Sharpy.Compiler/Semantic/Symbol.md`): Defines `TypeSymbol`, `FunctionSymbol`, `ModuleSymbol` referenced by several `SemanticType` subclasses
- **PrimitiveCatalog.md** (`docs/implementation_walkthrough/src/Sharpy.Compiler/Semantic/PrimitiveCatalog.md`): Implements numeric promotion and implicit conversion rules used by `BuiltinType`
- **TypeChecker.md** (`docs/implementation_walkthrough/src/Sharpy.Compiler/Semantic/TypeChecker.md`): Primary consumer of `SemanticType`, performs type checking using `IsAssignableTo`
- **TypeResolver.md** (`docs/implementation_walkthrough/src/Sharpy.Compiler/Semantic/TypeResolver.md`): Converts AST `TypeExpression` nodes into `SemanticType` instances
- **OperatorValidator.md** (`docs/implementation_walkthrough/src/Sharpy.Compiler/Semantic/OperatorValidator.md`): Uses type equality for operator overload caching

**Specification References**:
- `docs/language_specification/type_annotations.md`: Defines how types are written in Sharpy
- `docs/language_specification/type_hierarchy.md`: Specifies the type inheritance rules
- `docs/language_specification/type_narrowing.md`: Describes type refinement (may use nullable types)
- `docs/language_specification/type_casting.md`: Describes explicit type conversions

**CodeGen Integration**:
- **RoslynEmitter.md** (`docs/implementation_walkthrough/src/Sharpy.Compiler/CodeGen/RoslynEmitter.md`): Translates `SemanticType` to C# Roslyn syntax nodes
- **TypeMapper.md** (`docs/implementation_walkthrough/src/Sharpy.Compiler/CodeGen/TypeMapper.md`): Maps `SemanticType` to Roslyn type syntax

---

## Conclusion

`SemanticType.cs` is the foundation of Sharpy's type system during semantic analysis. Its design emphasizes:
- **Immutability**: Types are values, not mutable state
- **Extensibility**: Virtual methods allow subclasses to customize behavior
- **Integration**: Bridges to symbol table for inheritance/interface checks
- **Performance**: Singletons and custom equality for caching

Understanding this file is essential for working on type checking, type inference, or adding new language features involving types. The clean separation between type representation (this file) and type checking logic (TypeChecker.cs) makes the codebase maintainable and testable.
