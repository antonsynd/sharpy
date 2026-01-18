# Walkthrough: TypeMapper.cs

**Source File**: `src/Sharpy.Compiler/Discovery/TypeMapper.cs`

---

## Overview

The `TypeMapper.cs` file is the **bridge between .NET's type system and Sharpy's type system**. It performs bidirectional type mapping, converting CLR (Common Language Runtime) types to Sharpy's `SemanticType` instances during compilation.

**Role in Pipeline**: TypeMapper operates during the **semantic analysis phase** and **code generation phase**. When the compiler needs to understand external .NET types (from referenced assemblies, standard library, or platform APIs), TypeMapper translates those reflection-based CLR types into the semantic type representations that the rest of the compiler can work with.

**Key Responsibilities**:
- Convert CLR `System.Type` instances to Sharpy `SemanticType` instances
- Map primitive types (int, str, bool, etc.) to their semantic equivalents
- Handle complex generic types (List<T>, Dictionary<K,V>, etc.)
- Support nullable types and tuples
- Provide thread-safe caching for performance
- Handle special Sharpy runtime types (iterators)

**When This Runs**: TypeMapper is invoked when:
- Discovering members from .NET assemblies during module loading
- Type-checking calls to .NET framework methods
- Resolving return types from external library functions
- Processing type annotations that reference imported .NET types

---

## Class/Type Structure

### Main Class: `TypeMapper`

A stateless, thread-safe mapper with internal caching for performance optimization.

**Fields**:
```csharp
private readonly ConcurrentDictionary<Type, SemanticType> _typeCache
```

- **Purpose**: Memoizes CLR type → SemanticType mappings to avoid redundant work
- **Thread Safety**: Uses `ConcurrentDictionary` for safe concurrent access across compilation phases
- **Design Rationale**: Reflection is expensive, so caching dramatically improves performance when the same .NET types are encountered repeatedly (e.g., `int`, `string`, `List<T>`)

---

## Key Functions/Methods

### 1. `MapClrTypeToSemanticType(Type clrType)` - Public Entry Point

**Purpose**: Main public API for converting a CLR type to a SemanticType.

**Signature**:
```csharp
public SemanticType MapClrTypeToSemanticType(Type clrType)
```

**How It Works**:
```csharp
return _typeCache.GetOrAdd(clrType, MapTypeInternal);
```

- Uses `ConcurrentDictionary.GetOrAdd` for thread-safe lazy initialization
- First call for a type triggers `MapTypeInternal` and caches the result
- Subsequent calls return the cached value instantly

**Thread Safety Pattern**: The `GetOrAdd` method ensures that even if multiple threads request the same type simultaneously, `MapTypeInternal` will only execute once, and all threads will receive the same `SemanticType` instance.

**Example Usage**:
```csharp
var mapper = new TypeMapper();
var clrListType = typeof(List<int>);
var semanticType = mapper.MapClrTypeToSemanticType(clrListType);
// semanticType is now: GenericType { Name = "list", TypeArguments = [Int] }
```

**Connection to Pipeline**:
- **Upstream**: Called by `CachedModuleDiscovery` when loading external assemblies
- **Downstream**: Results consumed by `TypeChecker` during semantic analysis

---

### 2. `MapTypeInternal(Type clrType)` - Core Mapping Logic

**Purpose**: The workhorse method that performs the actual type classification and mapping.

**Signature**:
```csharp
private SemanticType MapTypeInternal(Type clrType)
```

**Algorithm Flow** (Hierarchical Decision Tree):

#### Step 1: Check Primitive Types (Lines 27-45)
```csharp
var primitiveInfo = PrimitiveCatalog.GetByClrType(clrType);
if (primitiveInfo != null)
{
    return primitiveInfo.SharpyName switch
    {
        "int" => SemanticType.Int,
        "long" => SemanticType.Long,
        "float" => SemanticType.Float,       // C# double → Sharpy float
        "float32" => SemanticType.Float32,   // C# float → Sharpy float32
        // ... other primitives
        _ => new BuiltinType { Name = primitiveInfo.SharpyName, ClrType = clrType }
    };
}
```

**Design Decision**: Returns **singleton instances** for common types (Int, Long, Float, etc.) to reduce memory allocations and enable reference equality checks. Less common primitives create new `BuiltinType` instances.

**Cross-Reference**: See `PrimitiveCatalog.cs:110` for the CLR type lookup logic and `docs/language_specification/type_annotations.md` for the language-level type naming conventions.

#### Step 2: Handle Arrays (Lines 47-62)
```csharp
if (clrType.IsArray)
{
    var elementType = clrType.GetElementType();
    return new GenericType
    {
        Name = "list",
        TypeArguments = new List<SemanticType>
        {
            MapClrTypeToSemanticType(elementType)  // Recursive call
        }
    };
}
```

**Key Insight**: C# arrays (`T[]`) are mapped to Sharpy's `list[T]` type. This reflects Sharpy's Python-like syntax where arrays are presented as lists.

**Recursive Pattern**: Notice the recursive call to `MapClrTypeToSemanticType` for the element type. This ensures nested types benefit from caching (e.g., `int[][]` becomes `list[list[int]]`).

**Defensive Programming**: Returns `SemanticType.Object` if `GetElementType()` returns null (though this should never happen for valid array types).

#### Step 3: Handle Nullable Value Types (Lines 64-72)
```csharp
var underlyingNullable = Nullable.GetUnderlyingType(clrType);
if (underlyingNullable != null)
{
    return new NullableType
    {
        UnderlyingType = MapClrTypeToSemanticType(underlyingNullable)
    };
}
```

**Purpose**: Converts C# nullable value types (`int?`, `bool?`, etc.) to Sharpy's `NullableType` wrapper.

**Example**: `typeof(int?)` → `NullableType { UnderlyingType = SemanticType.Int }`

**Limitation**: This only handles nullable value types. Reference types are always nullable in Sharpy (following Python semantics), so they don't need special handling here.

#### Step 4: Handle Iterator Types (Lines 74-84)
```csharp
var iteratorElementType = GetIteratorElementType(clrType);
if (iteratorElementType != null)
{
    return new BuiltinType
    {
        Name = clrType.Name,
        ClrType = clrType
    };
}
```

**Purpose**: Special handling for Sharpy runtime types that extend `Iterator<T>` (like `RangeIterator`).

**Design Rationale**: Iterator types are opaque to the type system but need to be recognized for protocol validation (supporting `for` loops).

**TODO Note**: The method comment at line 107 mentions this logic is duplicated in `Semantic/ProtocolValidator.cs` and should be consolidated in a future refactoring phase.

#### Step 5: Handle Generic Types (Lines 86-90)
```csharp
if (clrType.IsGenericType)
{
    return MapGenericType(clrType);
}
```

Delegates to specialized generic type handling (see below).

#### Step 6: Handle Enums (Lines 92-96)
```csharp
if (clrType.IsEnum)
{
    return SemanticType.Int;
}
```

**Design Decision**: All C# enums are treated as `int` in Sharpy's type system. This simplification aligns with Sharpy's philosophy of treating enums as named integer constants.

**Implication**: Enum type safety is lost at the Sharpy semantic level, but the generated C# code still has full enum safety.

#### Step 7: Fallback to Object (Lines 98-99)
```csharp
return SemanticType.Object;
```

**Defensive Default**: Unknown or complex types default to `object`, the universal base type. This prevents compilation crashes when encountering unfamiliar .NET types.

---

### 3. `GetIteratorElementType(Type clrType)` - Iterator Detection

**Purpose**: Detects if a type is or extends `Sharpy.Core.Iterator<T>` and extracts the element type.

**Signature**:
```csharp
private Type? GetIteratorElementType(Type clrType)
```

**Algorithm** (Lines 110-123):
```csharp
var currentType = clrType;
while (currentType != null)
{
    if (currentType.IsGenericType &&
        currentType.GetGenericTypeDefinition().FullName == "Sharpy.Core.Iterator`1")
    {
        return currentType.GetGenericArguments()[0];
    }
    currentType = currentType.BaseType;
}
return null;
```

**How It Works**:
1. Walk up the inheritance chain starting from `clrType`
2. Check each base type to see if it's the generic `Iterator<T>` definition
3. If found, extract the generic argument `T` (the element type)
4. Return `null` if no `Iterator<T>` is found in the hierarchy

**Example**:
- Input: `typeof(RangeIterator)` where `RangeIterator : Iterator<int>`
- Output: `typeof(int)`

**String Comparison Rationale**: Uses `FullName == "Sharpy.Core.Iterator`1"` instead of `typeof(Iterator<>)` to avoid circular assembly dependencies during bootstrapping.

**TODO**: Line 107-108 note that this is duplicated in `ProtocolValidator.cs` and should be extracted to a shared utility in Phase 7.

---

### 4. `MapGenericType(Type clrType)` - Generic Type Mapping

**Purpose**: Convert generic .NET types to their Sharpy equivalents.

**Signature**:
```csharp
private SemanticType MapGenericType(Type clrType)
```

**Prerequisite Extraction** (Lines 127-128):
```csharp
var genericDef = clrType.GetGenericTypeDefinition();
var typeArgs = clrType.GetGenericArguments();
```

- `genericDef`: The open generic type (e.g., `List<>` without type arguments)
- `typeArgs`: The closed type arguments (e.g., `[typeof(int)]` for `List<int>`)

#### Supported Generic Type Mappings:

| .NET Type(s) | Sharpy Type | Lines |
|--------------|-------------|-------|
| `List<T>`, `IList<T>` | `list[T]` | 131-142 |
| `Dictionary<K,V>`, `IDictionary<K,V>` | `dict[K, V]` | 144-157 |
| `HashSet<T>`, `ISet<T>` | `set[T]` | 159-171 |
| `IEnumerable<T>` | `list[T]` (Note: loses lazy evaluation) | 173-186 |
| `Tuple<...>`, `ValueTuple<...>` | `tuple[...]` | 188-198 |
| Unknown generic | `object` | 200-201 |

#### Example: Mapping `List<int>` (Lines 131-142)
```csharp
if (IsGenericTypeDefinition(genericDef, typeof(List<>)) ||
    IsGenericTypeDefinition(genericDef, typeof(IList<>)))
{
    return new GenericType
    {
        Name = "list",
        TypeArguments = new List<SemanticType>
        {
            MapClrTypeToSemanticType(typeArgs[0])  // Recursive mapping
        }
    };
}
```

**Recursive Mapping**: Each type argument is recursively mapped, allowing nested generics like `List<Dictionary<string, int>>` to work correctly.

#### Example: Mapping `Dictionary<string, int>` (Lines 144-157)
```csharp
if (IsGenericTypeDefinition(genericDef, typeof(Dictionary<,>)) ||
    IsGenericTypeDefinition(genericDef, typeof(IDictionary<,>)))
{
    return new GenericType
    {
        Name = "dict",
        TypeArguments = new List<SemanticType>
        {
            MapClrTypeToSemanticType(typeArgs[0]),  // Key type
            MapClrTypeToSemanticType(typeArgs[1])   // Value type
        }
    };
}
```

#### Important Limitation: `IEnumerable<T>` (Lines 178-179)
```csharp
// Note: IEnumerable<T> is mapped to list for simplicity, losing lazy evaluation semantics.
// This is a known limitation - iterables are treated as eager lists in the type system.
```

**Design Trade-off**: Sharpy's type system doesn't distinguish between lazy sequences and eager collections. `IEnumerable<T>` is mapped to `list[T]` for simplicity, even though this loses the lazy evaluation semantics.

**Implication**: LINQ queries returning `IEnumerable<T>` will type-check as `list[T]`, which may surprise developers expecting iterator semantics.

#### Tuple Handling (Lines 188-198)
```csharp
if (genericDef.FullName?.StartsWith("System.Tuple") == true ||
    genericDef.FullName?.StartsWith("System.ValueTuple") == true)
{
    return new TupleType
    {
        ElementTypes = typeArgs
            .Select(MapClrTypeToSemanticType)
            .ToList()
    };
}
```

**String Matching**: Uses `StartsWith` to handle all tuple arities (`Tuple<T1>`, `Tuple<T1,T2>`, etc.) without explicitly checking each one.

**Value vs Reference Tuples**: Both C# value tuples (`(int, string)`) and reference tuples (`Tuple<int, string>`) map to Sharpy's `TupleType`.

---

### 5. `IsGenericTypeDefinition(Type type, Type genericTypeDef)` - Helper

**Purpose**: Simple equality check for generic type definitions.

**Signature**:
```csharp
private bool IsGenericTypeDefinition(Type type, Type genericTypeDef)
{
    return type == genericTypeDef;
}
```

**Why a Separate Method?**:
- **Clarity**: Makes the calling code more readable
- **Future-Proofing**: Can be extended if more complex comparison logic is needed (e.g., handling type equivalence across assemblies)
- **Searchability**: Easy to find all generic type comparisons in the codebase

**Current Simplicity**: Right now it's just a direct equality check, but having it as a named method documents the intent.

---

## Dependencies

### Internal Dependencies

1. **`Sharpy.Compiler.Semantic.SemanticType`** (Line 4)
   - The target type hierarchy for all mapping operations
   - Provides `BuiltinType`, `GenericType`, `NullableType`, `TupleType` concrete implementations
   - Cross-reference: `src/Sharpy.Compiler/Semantic/SemanticType.cs`

2. **`Sharpy.Compiler.Semantic.PrimitiveCatalog`** (Line 28)
   - Authoritative registry of Sharpy's primitive types
   - Provides bidirectional lookup: Sharpy name ↔ CLR type
   - Cross-reference: `src/Sharpy.Compiler/Semantic/PrimitiveCatalog.cs:110` for `GetByClrType`

### External Dependencies

1. **`System.Collections.Concurrent.ConcurrentDictionary`** (Line 14)
   - Provides thread-safe caching infrastructure

2. **`System.Reflection`** (Line 3)
   - Enables runtime type inspection via `Type` class
   - Used for: `IsArray`, `IsGenericType`, `GetGenericTypeDefinition`, `GetGenericArguments`, etc.

### Cross-File References

- **`Semantic/ProtocolValidator.cs`**: Contains duplicate `GetIteratorElementType` logic (mentioned in TODO at line 107)
- **`CodeGen/RoslynEmitter.cs`**: Consumes TypeMapper during code generation when emitting calls to .NET APIs
- **`Discovery/ClrMemberCache.cs`** (Planned Phase 7): Will consolidate shared CLR reflection utilities

---

## Patterns and Design Decisions

### 1. **Singleton Pattern for Common Types**

**Pattern**:
```csharp
"int" => SemanticType.Int,
"long" => SemanticType.Long,
```

**Rationale**:
- Reduces memory allocations (one `Int` instance used everywhere)
- Enables fast reference equality checks: `type == SemanticType.Int`
- Improves cache locality and GC pressure

### 2. **Hierarchical Type Classification**

**Pattern**: The `MapTypeInternal` method uses a decision tree rather than polymorphism or a lookup table.

**Advantages**:
- **Performance**: Short-circuits on primitives (most common case) first
- **Clarity**: Explicit ordering shows priority (primitives before generics)
- **Flexibility**: Easy to insert new type categories

**Ordering Rationale**:
1. Primitives (most common, fastest check)
2. Arrays (common collection type)
3. Nullables (common for value types)
4. Iterators (Sharpy-specific)
5. Generics (more expensive reflection)
6. Enums (less common)
7. Fallback to object

### 3. **Recursive Mapping with Memoization**

**Pattern**:
```csharp
MapClrTypeToSemanticType(elementType)  // Recursive call goes through cache
```

**Key Insight**: Recursive calls go through the public `MapClrTypeToSemanticType` method, not `MapTypeInternal` directly. This ensures **nested types benefit from caching**.

**Example**:
- Mapping `List<int>` caches both `List<int>` and `int`
- Later mapping `Dictionary<int, string>` reuses the cached `int` mapping

### 4. **Defensive Programming**

**Examples**:
```csharp
if (elementType == null)
    return SemanticType.Object; // Defensive fallback (line 52)

return SemanticType.Object;  // Unknown type fallback (line 99)
```

**Philosophy**: TypeMapper never throws exceptions. Unknown or malformed types gracefully degrade to `object` rather than crashing the compiler.

**Trade-off**: May mask errors during development, but prevents brittle compilation failures when encountering unexpected .NET types from third-party libraries.

### 5. **Thread Safety via Immutability**

**Pattern**:
- `ConcurrentDictionary` for mutable cache
- `SemanticType` instances are immutable records
- No mutable state in TypeMapper fields

**Guarantee**: Multiple compilation threads can share a single `TypeMapper` instance safely.

---

## Debugging Tips

### 1. **Tracing Type Mapping**

Add logging to `MapTypeInternal` to see the mapping decisions:

```csharp
private SemanticType MapTypeInternal(Type clrType)
{
    Console.WriteLine($"Mapping CLR type: {clrType.FullName}");

    var primitiveInfo = PrimitiveCatalog.GetByClrType(clrType);
    if (primitiveInfo != null)
    {
        Console.WriteLine($"  → Mapped to primitive: {primitiveInfo.SharpyName}");
        // ...
    }
    // ...
}
```

### 2. **Inspecting the Type Cache**

The `_typeCache` is private, but you can add a debug property during development:

```csharp
#if DEBUG
public IReadOnlyDictionary<Type, SemanticType> GetCacheSnapshot()
    => _typeCache.ToDictionary(kv => kv.Key, kv => kv.Value);
#endif
```

### 3. **Common Issues**

**Issue**: "Expected `list[int]` but got `object`"
- **Cause**: TypeMapper encountered an unknown generic type and fell back to `object`
- **Fix**: Add explicit handling for the generic type in `MapGenericType`

**Issue**: "Type mismatch when calling .NET method"
- **Cause**: CLR type → SemanticType mapping doesn't match Sharpy's expectations
- **Debug**: Check what `MapClrTypeToSemanticType` returns for the method's return type

**Issue**: "Iterator type not recognized in for loop"
- **Cause**: `GetIteratorElementType` returned null
- **Debug**: Verify the type actually extends `Sharpy.Core.Iterator<T>` and the base class name matches exactly

### 4. **Unit Testing Strategy**

Test each type category independently:

```csharp
[Test]
public void MapClrTypeToSemanticType_Primitives()
{
    var mapper = new TypeMapper();
    Assert.That(mapper.MapClrTypeToSemanticType(typeof(int)), Is.EqualTo(SemanticType.Int));
    Assert.That(mapper.MapClrTypeToSemanticType(typeof(string)), Is.EqualTo(SemanticType.Str));
}

[Test]
public void MapClrTypeToSemanticType_Arrays()
{
    var mapper = new TypeMapper();
    var result = mapper.MapClrTypeToSemanticType(typeof(int[]));
    Assert.That(result, Is.InstanceOf<GenericType>());
    var generic = (GenericType)result;
    Assert.That(generic.Name, Is.EqualTo("list"));
    Assert.That(generic.TypeArguments[0], Is.EqualTo(SemanticType.Int));
}
```

---

## Contribution Guidelines

### What Kinds of Changes Might Be Made

#### 1. **Adding New Generic Type Mappings**

**When**: Supporting new .NET collection types or common generic patterns.

**Example**: Adding `Span<T>` support:

```csharp
// In MapGenericType method
if (IsGenericTypeDefinition(genericDef, typeof(Span<>)) ||
    IsGenericTypeDefinition(genericDef, typeof(ReadOnlySpan<>)))
{
    return new GenericType
    {
        Name = "list",  // or create a new SpanType if needed
        TypeArguments = new List<SemanticType>
        {
            MapClrTypeToSemanticType(typeArgs[0])
        }
    };
}
```

**Testing**: Add unit tests for the new mapping and integration tests using the type in Sharpy code.

#### 2. **Improving Iterator Detection**

**When**: Consolidating the duplicate `GetIteratorElementType` logic (TODO at line 107).

**Where to Move It**: Extract to a new `ClrReflectionUtils` class in a shared utilities namespace:

```csharp
namespace Sharpy.Compiler.Utils;

public static class ClrReflectionUtils
{
    public static Type? GetIteratorElementType(Type clrType) { /* ... */ }
}
```

**Impact**: Update both `TypeMapper.cs` and `ProtocolValidator.cs` to use the shared implementation.

#### 3. **Optimizing Cache Performance**

**When**: Profiling shows cache misses or excessive allocations.

**Ideas**:
- Pre-populate cache with common types (int, string, List<int>, etc.)
- Add cache hit/miss metrics for monitoring
- Consider `FrozenDictionary` for read-heavy scenarios (after warmup)

#### 4. **Supporting User-Defined Generic Types**

**When**: Sharpy adds support for generic class definitions in user code.

**Current Limitation**: TypeMapper only handles built-in .NET generics.

**Required Changes**:
- Add a case for Sharpy-defined generic types
- Likely need to query the `SymbolTable` or `SemanticInfo` for user type definitions
- Coordinate with semantic analysis phase

#### 5. **Better Error Reporting**

**When**: Debugging type mapping issues becomes common.

**Example**: Instead of silently falling back to `object`, emit warnings:

```csharp
// Fallback to object for unknown types
_logger?.LogWarning($"Unknown CLR type '{clrType.FullName}' mapped to 'object'");
return SemanticType.Object;
```

---

## Cross-References

### Related Files

- **`src/Sharpy.Compiler/Semantic/SemanticType.cs`**: Defines the target type hierarchy (`BuiltinType`, `GenericType`, `NullableType`, etc.)
- **`src/Sharpy.Compiler/Semantic/PrimitiveCatalog.cs`**: Authoritative registry for primitive type information
- **`src/Sharpy.Compiler/Semantic/ProtocolValidator.cs`**: Contains duplicate `GetIteratorElementType` logic (should be consolidated)
- **`src/Sharpy.Compiler/CodeGen/TypeMapper.cs`**: **Different file!** Maps Sharpy types → C# types (reverse direction)
- **`src/Sharpy.Compiler/Discovery/CachedModuleDiscovery.cs`**: Uses TypeMapper to import external assemblies

### Related Documentation

- **`docs/language_specification/type_annotations.md`**: Sharpy type naming conventions (int, str, float, etc.)
- **`docs/language_specification/type_hierarchy.md`**: Object as universal base type, assignability rules
- **`docs/language_specification/type_casting.md`**: Implicit and explicit conversion rules (though TypeMapper focuses on mapping, not conversion)
- **`docs/language_specification/type_narrowing.md`**: Type narrowing in control flow

### Future Refactoring

- **Phase 7 (Planned)**: Extract `ClrMemberCache` utilities, consolidate reflection helpers like `GetIteratorElementType`

---

## Quick Reference: Type Mapping Rules

| .NET Type | Sharpy Type | Notes |
|-----------|-------------|-------|
| `int`, `System.Int32` | `int` | Singleton instance |
| `string` | `str` | Singleton instance |
| `bool` | `bool` | Singleton instance |
| `void` | `None` | Via `SemanticType.Void` |
| `int?` | `int?` | Nullable value type |
| `int[]` | `list[int]` | Arrays become lists |
| `List<T>` | `list[T]` | Generic list |
| `Dictionary<K,V>` | `dict[K,V]` | Generic dictionary |
| `HashSet<T>` | `set[T]` | Generic set |
| `IEnumerable<T>` | `list[T]` | **Loses laziness!** |
| `(int, string)` | `tuple[int, str]` | Tuples preserved |
| `MyEnum` | `int` | Enums → int |
| `Iterator<T>` | `BuiltinType` | Preserves iterator semantics |
| Unknown type | `object` | Safe fallback |

---

## Summary

`TypeMapper.cs` is a **critical infrastructure component** that enables Sharpy to seamlessly interoperate with the .NET ecosystem. It's designed for:

- **Correctness**: Accurately maps .NET types to Sharpy's semantic model
- **Performance**: Aggressive caching for repeated lookups
- **Thread Safety**: Concurrent compilation support
- **Robustness**: Defensive fallbacks prevent crashes on unknown types

When working with TypeMapper, remember it's a **read-only translator** — it doesn't modify types or perform conversions, just maps them to Sharpy's internal representation for semantic analysis and code generation.

**Key Principle**: TypeMapper bridges .NET and Sharpy, making the compiler bilingual in type systems.
