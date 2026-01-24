# Walkthrough: SemanticType.cs

**Source File**: `src/Sharpy.Compiler/Semantic/SemanticType.cs`

---

## Overview

`SemanticType.cs` is the **heart of Sharpy's type system**. It defines how types are represented during semantic analysis—after parsing but before code generation. This is where the compiler reasons about type compatibility, inheritance, generics, and nullability.

**Role in Compiler Pipeline**:
- **Upstream**: TypeResolver converts AST `TypeAnnotation` nodes → `SemanticType` instances
- **Processing**: TypeChecker uses `SemanticType` for type checking, assignability, and type narrowing
- **Downstream**: CodeGen (RoslynEmitter) uses `SemanticType` to generate correct C# type references

**Critical Distinction**: 
- `TypeAnnotation` (AST) = what the programmer *wrote* in source code
- `SemanticType` = what the compiler *understands* about types
- `TypeSymbol` (Symbol) = the *declaration* of a type (class/struct/interface)

**Design Philosophy**: `SemanticType` represents **TYPE USAGE**, not TYPE DECLARATION. For declarations, see `TypeSymbol` in `Symbol.cs`.

---

## Architecture: Type Hierarchy

The file uses C# `record` types for immutable, value-based type representations:

```
SemanticType (abstract base + ITypeInfo)
├── UnknownType            - Error recovery (<?>, assignable to anything)
├── VoidType               - None/void return type
├── BuiltinType            - Primitives: int, float, bool, str
├── GenericType            - Generic instantiations: list[int], dict[str, float]
├── UserDefinedType        - Classes, structs, interfaces from user code
├── NullableType           - Nullable wrapper: T?
├── FunctionType           - Function signatures for lambdas/delegates
├── TupleType              - Tuple types: tuple[int, str, bool]
├── ModuleType             - Module references for imports
├── TypeParameterType      - Generic type parameters: T, U, TKey
├── GenericFunctionType    - Instantiated generic functions: identity[int]
├── UnionType              - Tagged unions (v0.2.x placeholder)
└── TaskType               - Async Task types (v0.2.x placeholder)
```

**Why Records?**
- **Immutable**: Once created, types never change (thread-safe, cacheable)
- **Value-based equality**: Two `list[int]` instances are equal by structure
- **Pattern matching**: Enables clean type checking logic

---

## Key Design Invariants

From the source documentation (lines 6-21):

1. **Immutability**: `SemanticType` is IMMUTABLE—once created, it never changes
2. **Usage vs Declaration**: Represents TYPE USAGE, not TYPE DECLARATION
3. **Symbol Linkage**: `UserDefinedType` always references its declaring `TypeSymbol`
4. **Generic Resolution**: `GenericType` contains resolved type arguments, not parameters

---

## Singleton Type Instances

Common types are pre-allocated as singletons for efficiency:

```csharp
public static readonly SemanticType Unknown = new UnknownType();
public static readonly SemanticType Void = new VoidType();
public static readonly SemanticType Int = new BuiltinType { Name = "int", ClrType = typeof(int) };
public static readonly SemanticType Float = new BuiltinType { Name = "float", ClrType = typeof(double) };
public static readonly SemanticType Bool = new BuiltinType { Name = "bool", ClrType = typeof(bool) };
public static readonly SemanticType Str = new BuiltinType { Name = "str", ClrType = typeof(string) };
public static readonly SemanticType Object = new UserDefinedType { Name = "object" };
```

**Important Note**: Sharpy `float` → C# `double` (64-bit), `float32` → C# `float` (32-bit).

---

## ITypeInfo Interface Implementation

`SemanticType` implements `ITypeInfo` (defined in `ITypeInfo.cs`), providing a unified abstraction:

```csharp
public abstract record SemanticType : ITypeInfo
{
    string ITypeInfo.DisplayName => GetDisplayName();
    public virtual bool IsNullable => false;
    public virtual bool IsValueType => false;
    public virtual Type? ClrType { get => null; }
    public virtual TypeSymbol? DeclaringSymbol => null;
}
```

**Key Properties**:
- `DisplayName`: Human-readable name for diagnostics ("list[int]", "MyClass")
- `IsNullable`: Can this type hold null? (True for `T?` and reference types)
- `IsValueType`: Struct/primitive vs reference type
- `ClrType`: The underlying .NET type (if known)
- `DeclaringSymbol`: Link to the `TypeSymbol` for user-defined types

---

## Core Type Operations

### 1. Assignability Checking

The heart of type compatibility:

```csharp
public virtual bool IsAssignableTo(SemanticType other)
{
    // All types are assignable to object
    if (other is UserDefinedType { Name: "object" })
        return true;
    
    return this.Equals(other);
}
```

**Design Pattern**: Each type overrides `IsAssignableTo` to implement specific rules:
- `BuiltinType`: Uses `PrimitiveCatalog` for implicit conversions (int → float)
- `UserDefinedType`: Checks inheritance chains and interface implementation
- `GenericType`: Checks structural equality (covariance planned for v0.2.x)
- `NullableType`: Allows implicit unwrapping (T? → T)

### 2. Nullable Operations

```csharp
public virtual ITypeInfo MakeNullable()
{
    if (this is NullableType)
        return this;  // Already nullable
    return new NullableType { UnderlyingType = this };
}

public virtual ITypeInfo UnwrapNullable()
{
    if (this is NullableType nullable)
        return nullable.UnderlyingType;
    return this;
}
```

**Usage**: Type narrowing (`if x is not None:` narrows `T?` → `T`)

---

## Type Implementations Deep Dive

### UnknownType (Error Recovery)

```csharp
public record UnknownType : SemanticType
{
    public override string GetDisplayName() => "<?>";
    public override bool IsAssignableTo(SemanticType other) => true; // Prevent cascading errors
}
```

**Purpose**: When type resolution fails, use `Unknown` to avoid cascading errors. It's assignable to everything so compilation can continue and report all errors at once.

### VoidType (None)

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

**Key Rule**: `None` is assignable to nullable types (`T?`), implementing Python's `None` semantics.

### BuiltinType (Primitives)

```csharp
public record BuiltinType : SemanticType
{
    public string Name { get; init; } = string.Empty;
    public new Type? ClrType { get; init; }
    
    public override bool IsValueType => ClrType?.IsValueType ?? false;
    
    public override bool IsAssignableTo(SemanticType other)
    {
        if (base.IsAssignableTo(other))
            return true;
        
        // Use PrimitiveCatalog for implicit conversion rules
        var thisInfo = PrimitiveCatalog.GetPrimitiveInfo(this);
        var otherInfo = PrimitiveCatalog.GetPrimitiveInfo(other);
        
        if (thisInfo != null && otherInfo != null)
            return PrimitiveCatalog.CanImplicitlyConvert(thisInfo, otherInfo);
        
        return false;
    }
}
```

**Important**: Delegates to `PrimitiveCatalog` for implicit conversion rules (e.g., `int` → `float`).

### GenericType (Generic Instantiations)

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

**Custom Equality**: Overrides `Equals` and `GetHashCode` to compare type arguments structurally:

```csharp
public virtual bool Equals(GenericType? other)
{
    if (Name != other.Name || TypeArguments.Count != other.TypeArguments.Count)
        return false;
    
    for (int i = 0; i < TypeArguments.Count; i++)
    {
        if (!TypeArguments[i].Equals(other.TypeArguments[i]))
            return false;
    }
    return true;
}
```

**Why?** Improves cache effectiveness in `OperatorValidator` and ensures `list[int]` equals `list[int]`.

### UserDefinedType (Classes/Structs/Interfaces)

```csharp
public record UserDefinedType : SemanticType
{
    public string Name { get; init; } = string.Empty;
    public TypeSymbol? Symbol { get; init; }
    
    public override bool IsValueType => Symbol?.TypeKind == TypeKind.Struct;
    public override TypeSymbol? DeclaringSymbol => Symbol;
}
```

**Assignability Logic** (lines 273-298):
1. Check if same type (by `Symbol` or `Name`)
2. Walk inheritance chain (check `BaseType` recursively)
3. Check interface implementation (including inherited interfaces)

**Interface Implementation Check** (lines 304-340):
Uses **BFS** to traverse interface inheritance hierarchy:

```csharp
private static bool ImplementsInterface(TypeSymbol type, UserDefinedType targetInterface)
{
    var visited = new HashSet<string>();
    var queue = new Queue<TypeSymbol>();
    
    // Add direct interfaces from type + base classes
    var currentType = type;
    while (currentType != null)
    {
        foreach (var iface in currentType.Interfaces)
            queue.Enqueue(iface);
        currentType = currentType.BaseType;
    }
    
    // BFS through interface inheritance
    while (queue.Count > 0)
    {
        var iface = queue.Dequeue();
        if (!visited.Add(iface.Name))
            continue;
        
        if (iface == targetInterface.Symbol || iface.Name == targetInterface.Name)
            return true;
        
        // Add base interfaces
        foreach (var baseIface in iface.Interfaces)
            queue.Enqueue(baseIface);
    }
    return false;
}
```

**Why BFS?** Handles complex interface inheritance chains correctly, including diamond inheritance.

### NullableType (T?)

```csharp
public record NullableType : SemanticType
{
    public SemanticType UnderlyingType { get; init; } = SemanticType.Unknown;
    
    public override bool IsNullable => true;
    
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

**Key Feature**: Implicit unwrapping—`int?` is assignable to `int` (with runtime null check).

### FunctionType (Lambdas/Delegates)

```csharp
public record FunctionType : SemanticType
{
    public List<SemanticType> ParameterTypes { get; init; } = new();
    public SemanticType ReturnType { get; init; } = SemanticType.Void;
    
    /// <summary>
    /// When true, skip argument validation (for .NET types with overloaded constructors).
    /// C# compiler handles overload resolution at compile time.
    /// </summary>
    public bool SkipArgumentValidation { get; init; } = false;
    
    public override string GetDisplayName()
    {
        var params_ = string.Join(", ", ParameterTypes.Select(p => p.GetDisplayName()));
        return $"({params_}) -> {ReturnType.GetDisplayName()}";
    }
}
```

**Special Case**: `SkipArgumentValidation` handles .NET types with multiple constructor overloads—delegates validation to C# compiler.

### TupleType (Tuples)

```csharp
public record TupleType : SemanticType
{
    public List<SemanticType> ElementTypes { get; init; } = new();
    
    public override bool IsAssignableTo(SemanticType other)
    {
        if (other is TupleType otherTuple && ElementTypes.Count == otherTuple.ElementTypes.Count)
        {
            // All elements must be assignable
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

**Custom Equality**: Like `GenericType`, overrides equality to compare elements structurally.

### TypeParameterType (Generic Type Parameters)

```csharp
public record TypeParameterType : SemanticType
{
    public string Name { get; init; } = string.Empty;
    public TypeSymbol? DeclaringType { get; init; }
    
    public override bool IsAssignableTo(SemanticType other)
    {
        // Type parameters assignable to themselves
        if (other is TypeParameterType otherParam && Name == otherParam.Name)
            return true;
        
        // Type parameters assignable to object
        return base.IsAssignableTo(other);
    }
}
```

**Usage**: Represents `T` in `class Box[T]:` during type checking within generic class bodies.

### GenericFunctionType (Instantiated Generic Functions)

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

**Purpose**: Internal type to pass type arguments from `IndexAccess` to `FunctionCall`.

**Example**: `identity[int]` from `def identity[T](value: T) -> T:`

### ModuleType (Module References)

```csharp
public record ModuleType : SemanticType
{
    public ModuleSymbol Symbol { get; init; } = null!;
    
    public override string GetDisplayName() => $"module '{Symbol.Name}'";
}
```

**Usage**: Represents imported modules used as namespaces (`import math; math.sqrt(4)`).

---

## Future Extensions (v0.2.x)

### UnionType (Tagged Unions/ADTs)

Placeholder for algebraic data types:

```csharp
public record UnionType : SemanticType
{
    public string Name { get; init; } = string.Empty;
    public TypeSymbol? Symbol { get; init; }
    public List<SemanticType> CaseTypes { get; init; } = new();
}
```

**Planned Usage** (from documentation, lines 506-516):
```python
type Result[T, E]:
    Ok(value: T)
    Err(error: E)

def divide(a: int, b: int) -> Result[int, str]:
    if b == 0:
        return Err("division by zero")
    return Ok(a / b)
```

### TaskType (Async/Await)

Placeholder for async programming:

```csharp
public record TaskType : SemanticType
{
    public SemanticType? ResultType { get; init; }
    
    public override Type? ClrType =>
        ResultType == null
            ? typeof(System.Threading.Tasks.Task)
            : null; // Generic Task<T> needs runtime resolution
}
```

**Planned Usage** (from documentation, lines 547-551):
```python
async def fetch_data(url: str) -> str:
    response = await http_get(url)
    return response.body
```

---

## Dependencies

### Internal Dependencies
- **`ITypeInfo.cs`**: Interface that `SemanticType` implements
- **`Symbol.cs`**: `TypeSymbol`, `FunctionSymbol`, `ModuleSymbol` for declarations
- **`PrimitiveCatalog.cs`**: Implicit conversion rules for primitive types
- **`TypeResolver.cs`**: Converts AST `TypeAnnotation` → `SemanticType`
- **`TypeChecker.cs`**: Uses `SemanticType` for type checking and narrowing

### External Dependencies
- **Parser AST**: `TypeAnnotation`, `TypeParameterDef` from `Parser/Ast/`
- **.NET Reflection**: `System.Type` for CLR type mapping

---

## Patterns and Design Decisions

### 1. Immutability via Records

**Why?**
- Thread-safe (no mutation concerns)
- Cacheable (types can be safely reused)
- Predictable (no spooky action at a distance)

### 2. Virtual `IsAssignableTo` Pattern

Each type overrides assignability rules:

```csharp
// Base: equality check + object assignability
SemanticType.IsAssignableTo(other)

// BuiltinType: add implicit conversions
BuiltinType.IsAssignableTo(other) → delegates to PrimitiveCatalog

// UserDefinedType: add inheritance + interfaces
UserDefinedType.IsAssignableTo(other) → walks inheritance chains

// NullableType: add unwrapping
NullableType.IsAssignableTo(other) → allows T? → T
```

### 3. Structural Equality for Generic Types

`GenericType` and `TupleType` override `Equals`/`GetHashCode` to compare contents:

**Why?** Ensures `list[int]` from two different sources are considered equal, improving cache hit rates and correct type comparisons.

### 4. Error Recovery via UnknownType

**Philosophy**: When type resolution fails, use `Unknown` (assignable to everything) to prevent cascading errors. This allows the compiler to continue and report multiple errors in one pass.

### 5. Separation of Usage and Declaration

**Key Insight**:
- `SemanticType` = **how a type is used** (in expressions, parameters, returns)
- `TypeSymbol` = **how a type is declared** (class/struct/interface definition)

This separation keeps concerns clean and prevents circular dependencies.

---

## Debugging Tips

### 1. Type Assignability Issues

If types aren't assignable when they should be:

```csharp
// Add breakpoints in:
SemanticType.IsAssignableTo()
BuiltinType.IsAssignableTo()
UserDefinedType.IsAssignableTo()

// Check:
- Are both types the correct subclass?
- For UserDefinedType: Is Symbol populated?
- For GenericType: Do TypeArguments match?
```

### 2. Generic Type Equality Problems

If `list[int]` doesn't equal `list[int]`:

```csharp
// Check GenericType.Equals():
- Is Name the same?
- Are TypeArguments.Count equal?
- Do all TypeArguments match (recursively)?
```

### 3. Interface Implementation Not Working

If a class should implement an interface but doesn't:

```csharp
// Trace UserDefinedType.ImplementsInterface():
- Is Symbol.Interfaces populated?
- Check base class Interfaces too
- Is interface inheritance chain correct?
```

### 4. Nullable Type Issues

If `T?` behaves incorrectly:

```csharp
// Verify:
NullableType.UnderlyingType is correct
NullableType.IsAssignableTo() handles unwrapping
Type narrowing in TypeChecker updates types correctly
```

### 5. Display Names for Diagnostics

If error messages show wrong type names:

```csharp
// Check GetDisplayName() for each type:
GenericType: Should show "list[int]" not "list"
NullableType: Should show "int?" not "int"
FunctionType: Should show full signature
```

---

## Contribution Guidelines

### When to Modify This File

1. **Adding a new type variant**:
   - Add new `record` inheriting from `SemanticType`
   - Override `GetDisplayName()` and `IsAssignableTo()`
   - Add tests in `Sharpy.Compiler.Tests/Semantic/SemanticTypeTests.cs`

2. **Changing assignability rules**:
   - Modify `IsAssignableTo()` in appropriate type
   - **CRITICAL**: Update tests, don't break existing behavior
   - Verify against Python semantics if applicable

3. **Adding type properties**:
   - Add virtual property to base `SemanticType`
   - Override in specific types (like `IsValueType`, `IsNullable`)
   - Update `ITypeInfo` if it's part of public contract

4. **Performance optimization**:
   - Consider caching in `TypeRegistry` or `TypeResolver`
   - Override `Equals`/`GetHashCode` if needed for caching
   - Profile before optimizing (measure, don't guess)

### Checklist for Changes

- [ ] Does the change maintain immutability?
- [ ] Are `Equals` and `GetHashCode` consistent?
- [ ] Does `IsAssignableTo` handle the new case?
- [ ] Are error recovery scenarios handled?
- [ ] Do tests cover edge cases?
- [ ] Is documentation updated?
- [ ] Does it work with type narrowing?

### Related Files to Update

When modifying `SemanticType.cs`, consider updating:

- **`TypeResolver.cs`**: If adding new AST → SemanticType conversion
- **`TypeChecker.cs`**: If changing assignability affects type checking
- **`RoslynEmitter.cs`**: If new type needs C# code generation
- **`TypeMapper.cs`**: If mapping to .NET types changes
- **`PrimitiveCatalog.cs`**: If adding new primitives
- **Tests**: Always update `Sharpy.Compiler.Tests/Semantic/`

### Common Mistakes to Avoid

1. **Mutating types**: SemanticType is immutable—create new instances
2. **Breaking equality**: If you override `Equals`, override `GetHashCode` too
3. **Forgetting error recovery**: Handle `UnknownType` gracefully
4. **Ignoring inheritance**: Check `UserDefinedType` base classes and interfaces
5. **Hardcoding type names**: Use pattern matching and type checks, not string comparisons

---

## Cross-References

### Related Documentation

- **`ITypeInfo.md`**: The interface `SemanticType` implements
- **`Symbol.md`**: Type declarations (`TypeSymbol`, `FunctionSymbol`)
- **`TypeResolver.md`**: Converts AST → `SemanticType`
- **`TypeChecker.md`**: Uses `SemanticType` for type checking
- **`PrimitiveCatalog.md`**: Primitive type information and conversion rules

### Related Specification Documents

- `docs/language_specification/type_annotations.md`
- `docs/language_specification/type_hierarchy.md`
- `docs/language_specification/nullable_types.md`
- `docs/language_specification/generics.md`
- `docs/language_specification/type_narrowing.md`

### Code Generation

- **`src/Sharpy.Compiler/CodeGen/TypeMapper.cs`**: Maps `SemanticType` → C# type strings
- **`src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`**: Emits C# code using `SemanticType`

---

## Summary

`SemanticType.cs` is the **semantic layer's type vocabulary**. It bridges the gap between what the programmer writes (AST) and what the compiler generates (C#). Understanding this file is essential for:

- Adding new type features
- Debugging type checking issues
- Implementing type inference
- Understanding compiler error messages

**Key Takeaways**:
1. **Immutable by design**: Types never change after creation
2. **Usage, not declaration**: Represents how types are used, not defined
3. **Extensible hierarchy**: Easy to add new type variants
4. **Error recovery built-in**: `UnknownType` prevents cascading errors
5. **Structural equality**: Generic and tuple types compare by structure

When in doubt, remember: `SemanticType` is about **what the compiler knows**, not what the programmer wrote.
