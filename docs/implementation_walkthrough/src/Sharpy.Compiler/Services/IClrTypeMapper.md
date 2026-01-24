# Walkthrough: IClrTypeMapper.cs

**Source File**: `src/Sharpy.Compiler/Services/IClrTypeMapper.cs`

---

## Overview

`IClrTypeMapper` is a service interface that provides bidirectional mapping between Sharpy's semantic type system and the .NET Common Language Runtime (CLR) type system. This is a critical bridge component in the compiler pipeline, enabling the compiler to:

1. **Convert Sharpy types → CLR types** during code generation (for emitting C# code)
2. **Convert CLR types → Sharpy types** during interop (when calling .NET libraries)
3. **Perform reflection-based member lookups** on CLR types with caching for performance

**Position in Pipeline**: This service sits between **Semantic Analysis** and **Code Generation (RoslynEmitter)**, facilitating the transition from Sharpy's abstract type system to concrete .NET types.

```
Semantic Analysis → IClrTypeMapper → RoslynEmitter → C#
    (SemanticType)      ↕             (System.Type)
```

---

## Interface Structure

The interface defines four core methods that form a complete type mapping service:

```csharp
public interface IClrTypeMapper
{
    Type? GetClrType(SemanticType semanticType);
    SemanticType GetSemanticType(Type clrType);
    bool HasMember(Type clrType, string memberName);
    System.Reflection.MemberInfo? GetMember(Type clrType, string memberName);
}
```

This is an **interface**, not an implementation, following the **Dependency Inversion Principle**. Consumers depend on the abstraction, allowing for different implementations or testing with mocks.

---

## Key Methods

### 1. `GetClrType(SemanticType semanticType)` → Type?

**Purpose**: Converts a Sharpy semantic type to its corresponding CLR `System.Type`.

**Parameters**:
- `semanticType`: A `SemanticType` instance representing a Sharpy type (e.g., `SemanticType.Int`, `GenericType`, `UserDefinedType`)

**Returns**:
- `Type?`: The CLR type, or `null` if no mapping exists

**Use Cases**:
- **Code generation**: When RoslynEmitter needs to emit C# type syntax for variables, parameters, return types
- **Type checking**: Verifying whether a Sharpy type has a CLR equivalent
- **Interop**: Marshalling Sharpy types to .NET API calls

**Example Flow**:
```
SemanticType.Int → typeof(int)
SemanticType.Str → typeof(string)
GenericType(List, [Int]) → typeof(List<int>)
NullableType(Int) → typeof(Nullable<int>)
```

---

### 2. `GetSemanticType(Type clrType)` → SemanticType

**Purpose**: Reverse mapping from CLR types back to Sharpy semantic types.

**Parameters**:
- `clrType`: A .NET `System.Type` (e.g., `typeof(int)`, `typeof(string)`)

**Returns**:
- `SemanticType`: The corresponding Sharpy type

**Use Cases**:
- **Interop analysis**: When importing .NET libraries, converting their signatures to Sharpy types
- **Reflection-based type discovery**: Understanding what .NET members return
- **Error messages**: Presenting .NET types in Sharpy-friendly terms

**Example Flow**:
```
typeof(int) → SemanticType.Int
typeof(string) → SemanticType.Str
typeof(List<int>) → GenericType(List, [Int])
```

---

### 3. `HasMember(Type clrType, string memberName)` → bool

**Purpose**: Quick check if a CLR type has a specific member (method, property, or field).

**Parameters**:
- `clrType`: The type to inspect
- `memberName`: The member name to look for (e.g., `"Length"`, `"Add"`, `"ToString"`)

**Returns**:
- `bool`: `true` if the member exists, `false` otherwise

**Use Cases**:
- **Validation**: Checking if a method/property access is valid before code generation
- **Type checking**: Verifying member existence during semantic analysis
- **Autocomplete support**: Future IDE integration

**Performance Note**: Results are **cached** to avoid repeated reflection calls, which are expensive.

---

### 4. `GetMember(Type clrType, string memberName)` → MemberInfo?

**Purpose**: Retrieve detailed member information from a CLR type via reflection.

**Parameters**:
- `clrType`: The type to inspect
- `memberName`: The member name

**Returns**:
- `MemberInfo?`: Reflection metadata (`PropertyInfo`, `FieldInfo`, or `MethodInfo`), or `null` if not found

**Use Cases**:
- **Code generation**: Getting method signatures, property types, field attributes
- **Operator resolution**: Finding operator overloads for binary expressions
- **Attribute inspection**: Checking for decorators/annotations on members

**Performance Note**: Like `HasMember`, results are **cached** for efficiency.

---

## Dependencies

### Internal Dependencies

1. **`Sharpy.Compiler.Semantic`** namespace:
   - `SemanticType` (abstract record): Base type for all Sharpy types
   - `BuiltinType`: Primitive types (int, str, bool, etc.)
   - `UserDefinedType`: Classes, interfaces defined in Sharpy or .NET
   - `GenericType`: Generic types like `List<int>`, `Dict<str, int>`
   - `NullableType`: Optional types (e.g., `int?`)

2. **`ClrMemberCache`** (in implementation):
   - The actual implementation (`ClrTypeMapperAdapter`) depends on `ClrMemberCache` for storing reflection results
   - See: `src/Sharpy.Compiler/Semantic/ClrMemberCache.cs`

### External Dependencies

- **`System.Reflection`**: All member lookup operations use .NET reflection APIs
- **`System.Type`**: The CLR type system

---

## Patterns and Design Decisions

### 1. **Interface Segregation**

This interface is **deliberately small** (4 methods), focusing on a single responsibility: type mapping. It doesn't handle:
- Type inference (that's `TypeResolver`)
- Type checking (that's `TypeChecker`)
- Code emission (that's `RoslynEmitter`)

### 2. **Nullable Return Types**

Notice `GetClrType` returns `Type?` and `GetMember` returns `MemberInfo?`. This acknowledges that:
- Not all Sharpy types have CLR equivalents (e.g., future union types)
- Not all member names will exist on a type
- **Null is a valid result**, not an error condition

Consumers must handle `null` gracefully.

### 3. **Caching Strategy**

The interface **documents caching** in XML comments:
```csharp
/// <summary>
/// Check if a CLR type has a specific member (method, property, field).
/// Results are cached.
/// </summary>
```

This is a performance contract: implementers **must cache** to avoid O(n) reflection costs on hot paths.

### 4. **Bidirectional Mapping**

The interface provides **both directions**:
- `GetClrType`: Sharpy → CLR (used in code generation)
- `GetSemanticType`: CLR → Sharpy (used in interop)

This symmetry simplifies reasoning about type conversions.

---

## Implementation: ClrTypeMapperAdapter

The primary implementation is in **`ClrTypeMapperAdapter.cs`** (same directory). Key details:

### Constructor Dependency
```csharp
public ClrTypeMapperAdapter(ClrMemberCache clrCache)
```
Uses dependency injection to receive a `ClrMemberCache` instance.

### SemanticType → CLR Conversion
Uses **pattern matching** on `SemanticType` subtypes:
```csharp
return semanticType switch
{
    BuiltinType bt => bt.ClrType,              // Direct property access
    UserDefinedType udt => udt.Symbol?.ClrType, // Via TypeSymbol
    GenericType gt => GetGenericClrType(gt),   // Construct via MakeGenericType
    NullableType nt => GetNullableClrType(nt), // Wrap in Nullable<T>
    _ => null
};
```

### CLR → SemanticType Conversion
Uses **simple type checks** for primitives:
```csharp
if (clrType == typeof(int)) return SemanticType.Int;
if (clrType == typeof(string)) return SemanticType.Str;
// ... etc
```
Falls back to `SemanticType.Unknown` for unmapped types.

### Member Lookup Strategy
Searches in priority order:
1. **Properties** (most common in .NET APIs)
2. **Fields** (less common)
3. **Methods** (returns first overload if multiple exist)

Uses a `ConcurrentDictionary<(Type, string), MemberInfo?>` for **thread-safe caching**.

---

## Debugging Tips

### 1. **Trace Type Conversions**

If you see incorrect C# code generation, check:
```csharp
var clrType = mapper.GetClrType(semanticType);
Console.WriteLine($"{semanticType.GetDisplayName()} → {clrType?.FullName ?? "null"}");
```

### 2. **Inspect Member Lookup Failures**

When member access fails:
```csharp
var member = mapper.GetMember(clrType, memberName);
if (member == null)
{
    // Check: Is the member name correct? (case-sensitive!)
    // Check: Is the type correct? (check clrType.FullName)
    // Check: Is it a non-public member? (binding flags limitation)
}
```

### 3. **Generic Type Issues**

Generic types can fail silently. Debug with:
```csharp
if (semanticType is GenericType gt)
{
    Console.WriteLine($"Generic: {gt.GenericDefinition?.Name}");
    foreach (var arg in gt.TypeArguments)
    {
        Console.WriteLine($"  Arg: {arg.GetDisplayName()} → {mapper.GetClrType(arg)}");
    }
}
```

### 4. **Cache Invalidation**

The cache is **never invalidated**. If you modify types at runtime (unusual in compilers), recreate the mapper instance.

---

## Contribution Guidelines

### When to Modify This Interface

**DON'T add new methods lightly.** This interface is stable and used throughout the codebase. Consider:

1. **Can you use existing methods?** Composition > expansion.
2. **Is it truly about type mapping?** If it's about type checking or inference, it belongs elsewhere.
3. **Will all implementations support it?** Mock implementations for tests must implement every method.

### When You SHOULD Modify

- **New SemanticType subclasses**: If you add a new type (e.g., `UnionType`, `TaskType`), update `ClrTypeMapperAdapter.GetClrType` to handle it.
- **Performance improvements**: If profiling shows member lookup is slow, optimize caching strategies in the implementation.
- **Extended reflection**: If you need to query additional member metadata (e.g., attributes, accessibility), consider adding focused methods.

### Testing Changes

If you modify `IClrTypeMapper` or its implementation:

```bash
# Run semantic tests (type checking depends on this)
dotnet test --filter "FullyQualifiedName~Semantic"

# Run code generation tests (emission depends on this)
dotnet test --filter "FullyQualifiedName~CodeGen"

# Run integration tests (end-to-end verification)
dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"
```

---

## Cross-References

### Related Source Files

- **Implementation**: [`src/Sharpy.Compiler/Services/ClrTypeMapperAdapter.cs`](ClrTypeMapperAdapter.md) _(implementation of this interface)_
- **Cache Layer**: [`src/Sharpy.Compiler/Semantic/ClrMemberCache.cs`](../Semantic/ClrMemberCache.md) _(reflection caching)_
- **Type System**: [`src/Sharpy.Compiler/Semantic/SemanticType.cs`](../Semantic/SemanticType.md) _(base type for all Sharpy types)_
- **Type Resolution**: [`src/Sharpy.Compiler/Semantic/TypeResolver.cs`](../Semantic/TypeResolver.md) _(converts AST type annotations to SemanticType)_
- **Code Generation**: [`src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`](../CodeGen/RoslynEmitter.md) _(consumes CLR types to emit C#)_

### Related Specification Documents

- [Type Annotations](../../../../language_specification/type_annotations.md)
- [Type Casting](../../../../language_specification/type_casting.md)
- [Type Hierarchy](../../../../language_specification/type_hierarchy.md)

### Usage Examples in Codebase

To see how this interface is used in practice:

```bash
# Find all usages of IClrTypeMapper
grep -r "IClrTypeMapper" src/Sharpy.Compiler/

# Find instantiation points
grep -r "ClrTypeMapperAdapter" src/Sharpy.Compiler/
```

---

## Summary for New Engineers

**Key Takeaway**: `IClrTypeMapper` is the **Rosetta Stone** between Sharpy's type system and .NET's type system. When you see:

- Sharpy code: `x: int = 42`
- Becomes SemanticType: `SemanticType.Int`
- Maps to CLR type: `typeof(int)`
- Emits C# code: `int x = 42;`

This interface handles steps 2→3, enabling the compiler to generate valid C# that interoperates seamlessly with .NET libraries while preserving Sharpy's Pythonic syntax and semantics.

**When working on**:
- **Type checking**: You'll consume this to validate member access
- **Code generation**: You'll consume this to emit type syntax
- **Interop**: You'll consume this to understand .NET libraries
- **New type features**: You'll extend the implementation to support new `SemanticType` subclasses

Start by reading [`ClrTypeMapperAdapter.cs`](ClrTypeMapperAdapter.md) to see the concrete implementation in action.
