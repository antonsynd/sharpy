# Walkthrough: ClrTypeMapperAdapter.cs

**Source File**: `src/Sharpy.Compiler/Services/ClrTypeMapperAdapter.cs`

---

## Overview

`ClrTypeMapperAdapter` is a critical bridge component in the Sharpy compiler that handles bidirectional mapping between Sharpy's semantic type system and .NET's Common Language Runtime (CLR) type system. It implements the `IClrTypeMapper` interface and acts as an adapter around the existing `ClrMemberCache`.

**Role in the Compiler Pipeline:**
- **Position**: Services Layer (used during Semantic Analysis and Code Generation)
- **Purpose**: Translate between abstract Sharpy types (like `SemanticType.Int`) and concrete CLR types (like `System.Int32`)
- **Used By**: Validation pipeline, type checkers, code generators

**Key Responsibilities:**
1. Convert Sharpy semantic types to CLR `Type` objects
2. Convert CLR types back to Sharpy semantic types
3. Look up members (properties, fields, methods) on CLR types with caching
4. Handle special type mappings (generics, nullables)

---

## Class Structure

```csharp
public class ClrTypeMapperAdapter : IClrTypeMapper
```

### Dependencies

**Constructor Injection:**
```csharp
private readonly ClrMemberCache _clrCache;

public ClrTypeMapperAdapter(ClrMemberCache clrCache)
{
    _clrCache = clrCache ?? throw new ArgumentNullException(nameof(clrCache));
}
```

The adapter wraps `ClrMemberCache` (from `Sharpy.Compiler.Semantic`), which provides cached reflection-based metadata about CLR types. This is intentional - the adapter provides a simpler, focused interface while the cache handles the complex reflection operations.

**Internal Caching:**
```csharp
private readonly ConcurrentDictionary<(Type, string), MemberInfo?> _memberCache = new();
```

Thread-safe cache for member lookups to avoid repeated reflection calls. Note the use of tuple keys `(Type, string)` for efficient lookups.

---

## Key Methods

### 1. GetClrType: Sharpy → CLR Mapping

**Signature:**
```csharp
public Type? GetClrType(SemanticType semanticType)
```

**Purpose:**
Converts a Sharpy semantic type to its corresponding CLR `Type`. This is essential for code generation, where we need to emit C# code that references actual .NET types.

**Implementation Pattern:**
Uses pattern matching with a switch expression to handle different `SemanticType` variants:

```csharp
return semanticType switch
{
    BuiltinType bt => bt.ClrType,              // int, str, bool, etc.
    UserDefinedType udt => udt.Symbol?.ClrType, // User classes/structs
    GenericType gt => GetGenericClrType(gt),    // list[int], dict[str, int]
    NullableType nt => GetNullableClrType(nt),  // int?, str?
    _ => null                                   // Unknown types
};
```

**Key Design Decision:**
The method delegates to `SemanticType`'s built-in `ClrType` properties where possible. This follows the principle of using existing infrastructure rather than duplicating logic.

**Special Cases:**

- **BuiltinType**: Directly returns the pre-mapped CLR type (e.g., `SemanticType.Int` → `typeof(int)`)
- **UserDefinedType**: Accesses the symbol's CLR type (if bound during semantic analysis)
- **GenericType**: Requires special handling via `GetGenericClrType` (see below)
- **NullableType**: Requires special handling via `GetNullableClrType` (see below)

---

### 2. GetSemanticType: CLR → Sharpy Mapping

**Signature:**
```csharp
public SemanticType GetSemanticType(Type clrType)
```

**Purpose:**
Reverse mapping - converts a CLR type back to Sharpy's semantic type system. This is used when interoperating with .NET libraries or analyzing CLR members.

**Implementation:**
Uses a series of if-checks for common types:

```csharp
if (clrType == typeof(int))    return SemanticType.Int;
if (clrType == typeof(long))   return SemanticType.Long;
if (clrType == typeof(float))  return SemanticType.Float32;
if (clrType == typeof(double)) return SemanticType.Double;
if (clrType == typeof(bool))   return SemanticType.Bool;
if (clrType == typeof(string)) return SemanticType.Str;
if (clrType == typeof(void))   return SemanticType.Void;
if (clrType == typeof(object)) return SemanticType.Object;
```

**Important Limitation:**
For non-builtin types, returns `SemanticType.Unknown`. The comment indicates "caller should handle" - this is intentional, as complex types (generics, user types) require more context to map correctly.

**Why Only Builtins?**
This method is primarily used for quick lookups during member access validation. User-defined types are handled through the symbol table, not through reverse CLR mapping.

---

### 3. Member Lookup Methods

#### HasMember
```csharp
public bool HasMember(Type clrType, string memberName)
{
    return GetMember(clrType, memberName) != null;
}
```

Simple wrapper around `GetMember` for existence checks. Used in validation phases.

#### GetMember
```csharp
public MemberInfo? GetMember(Type clrType, string memberName)
```

**Purpose:**
Performs reflection-based member lookup with caching. This is called frequently during attribute access validation (e.g., `obj.property`).

**Caching Strategy:**
```csharp
return _memberCache.GetOrAdd((clrType, memberName), key =>
{
    // Lookup logic here
});
```

Uses `ConcurrentDictionary.GetOrAdd` for thread-safe lazy initialization. The cache key is a tuple of `(Type, memberName)`.

**Lookup Priority:**
The method follows a specific search order:

1. **Properties first** (most common for attribute access)
   ```csharp
   var property = type.GetProperty(name,
       BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
   ```

2. **Fields second**
   ```csharp
   var field = type.GetField(name,
       BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
   ```

3. **Methods last** (for callable attributes)
   ```csharp
   var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
       .Where(m => m.Name == name)
       .ToArray();
   if (methods.Length > 0)
       return methods[0]; // Return first overload
   ```

**Why This Order?**
Properties are the most frequently accessed members in Python-style code. Methods come last because Sharpy handles method overloading separately during call resolution.

**Overload Handling:**
For methods with multiple overloads, returns the first match. This is intentional - full overload resolution happens elsewhere in the type checker using signature matching.

---

### 4. Generic Type Handling

**Method:**
```csharp
private Type? GetGenericClrType(GenericType gt)
```

**Purpose:**
Constructs CLR generic types (e.g., `List<int>`) from Sharpy's `GenericType` representation.

**Algorithm:**
```csharp
if (gt.GenericDefinition?.ClrType != null && gt.TypeArguments.Count > 0)
{
    // 1. Recursively resolve all type arguments
    var typeArgs = gt.TypeArguments
        .Select(ta => GetClrType(ta))
        .Where(t => t != null)
        .ToArray();

    // 2. Ensure all arguments resolved successfully
    if (typeArgs.Length == gt.TypeArguments.Count)
    {
        try
        {
            // 3. Create the concrete generic type
            return gt.GenericDefinition.ClrType.MakeGenericType(typeArgs!);
        }
        catch
        {
            return null; // Invalid type combination
        }
    }
}
return gt.GenericDefinition?.ClrType; // Fallback to open generic
```

**Example:**
- Input: `GenericType` representing `list[int]`
  - `GenericDefinition.ClrType` = `typeof(List<>)` (open generic)
  - `TypeArguments[0]` = `SemanticType.Int`
- Output: `typeof(List<int>)` (closed generic)

**Error Handling:**
The `try-catch` handles invalid generic type combinations (e.g., trying to create `List<void>`). Returns `null` on failure, allowing the caller to handle the error appropriately.

**Recursive Resolution:**
Note the recursive call to `GetClrType(ta)` - this handles nested generics like `list[list[int]]`.

---

### 5. Nullable Type Handling

**Method:**
```csharp
private Type? GetNullableClrType(NullableType nt)
```

**Purpose:**
Maps Sharpy's nullable types (`int?`, `str?`) to CLR nullable types, respecting the difference between value types and reference types.

**Algorithm:**
```csharp
var underlyingClr = GetClrType(nt.UnderlyingType);
if (underlyingClr == null)
    return null;

// For value types, wrap in Nullable<T>
if (underlyingClr.IsValueType)
{
    return typeof(Nullable<>).MakeGenericType(underlyingClr);
}

// Reference types are already nullable
return underlyingClr;
```

**Key Insight:**
This reflects a fundamental .NET distinction:
- **Value types** (`int`, `bool`, `struct`) need explicit `Nullable<T>` wrapper
- **Reference types** (`string`, `class`) are inherently nullable in C#

**Examples:**
- `int?` → `typeof(Nullable<int>)` or `typeof(int?)`
- `str?` → `typeof(string)` (already nullable)
- `MyStruct?` → `typeof(Nullable<MyStruct>)`
- `MyClass?` → `typeof(MyClass)` (already nullable)

**Note on Null Safety:**
This mapping is crucial for Sharpy's null safety features. The type system tracks nullability explicitly, but the CLR representation depends on whether the type is a value or reference type.

---

## Dependencies and Integration

### Dependency on ClrMemberCache

The `ClrMemberCache` (in `src/Sharpy.Compiler/Semantic/ClrMemberCache.cs`) provides:
- Cached operator method lookups
- Interface hierarchy information
- Indexer metadata
- Enumerator type information

The adapter exposes this cache via:
```csharp
public ClrMemberCache UnderlyingCache => _clrCache;
```

This allows consumers to access advanced features (like operator overload resolution) without expanding the adapter's interface.

### Used In

**SemanticContext** (`src/Sharpy.Compiler/Semantic/Validation/SemanticContext.cs`):
- Stores the adapter as part of `CompilerServices`
- Passed to all validators for type checking

**CompilerServicesBuilder** (`src/Sharpy.Compiler/Services/CompilerServicesBuilder.cs`):
```csharp
var clrMapperAdapter = new ClrTypeMapperAdapter(clrCache);
```

Built during compiler initialization and shared across all validation passes.

---

## Patterns and Design Decisions

### 1. Adapter Pattern
The class wraps `ClrMemberCache` and provides a simplified interface. This decouples consumers from the complex reflection logic.

### 2. Thread-Safe Caching
Uses `ConcurrentDictionary` for member lookups to support potential future parallelization of validation passes.

### 3. Fail-Safe Null Returns
Methods return `null` rather than throwing exceptions for unmappable types. This allows graceful degradation and error reporting at higher layers.

### 4. Delegation to Existing Infrastructure
Prefers using `SemanticType.ClrType` properties over reimplementing mappings. This reduces duplication and maintains consistency.

### 5. Separation of Concerns
- **Adapter**: Handles type mapping
- **ClrMemberCache**: Handles reflection and caching
- **TypeResolver**: Handles type resolution from AST

Each component has a focused responsibility.

---

## Debugging Tips

### Common Issues

**1. GetClrType returns null**
- Check if the `SemanticType` is fully resolved (not `SemanticType.Unknown`)
- For `UserDefinedType`, ensure the symbol's `ClrType` property is set during semantic analysis
- For generics, verify all type arguments are resolvable

**2. GetMember returns null for known members**
- Verify the member is `public` (private members are not found)
- Check spelling and casing (C# members are case-sensitive)
- For methods, remember only the first overload is returned

**3. Generic type creation fails**
- The silent `catch` in `GetGenericClrType` can hide errors
- Add logging or breakpoints to see the specific `MakeGenericType` exception
- Common cause: type constraints not satisfied

### Logging Strategy

Add logging in these areas to trace type mapping:
```csharp
Logger.LogDebug($"Mapping {semanticType} to CLR type: {result}");
Logger.LogWarning($"Failed to resolve CLR type for {semanticType}");
```

### Breakpoint Locations

Set breakpoints at:
- Line 25: `GetClrType` switch expression - see which branch is taken
- Line 66: `_memberCache.GetOrAdd` - see cache hits/misses
- Line 110: `MakeGenericType` - catch generic type construction errors

---

## Contribution Guidelines

### When to Modify This File

**Add support for new builtin types:**
- Add mapping in `GetSemanticType` (CLR → Sharpy)
- Ensure corresponding `BuiltinType` has `ClrType` set

**Improve member lookup:**
- Modify `GetMember` if new member categories need support (e.g., events)
- Update the binding flags if different visibility is needed

**Handle new SemanticType variants:**
- Add new case to the switch in `GetClrType`
- Consider adding a private helper method (like `GetGenericClrType`)

### What NOT to Change

**Don't modify caching strategy:**
The `ConcurrentDictionary` is intentionally thread-safe for future parallelization. Don't replace with simple `Dictionary`.

**Don't add business logic:**
This is a pure adapter. Type validation rules belong in validators, not here.

**Don't expand GetSemanticType to handle all types:**
It's intentionally limited to builtins. Complex reverse mapping requires semantic context that this adapter doesn't have.

### Testing

When modifying, ensure tests cover:
1. Builtin type mappings (both directions)
2. Generic type construction with various argument counts
3. Nullable value types vs reference types
4. Member lookup caching (verify no repeated reflection)
5. Thread-safety of concurrent member lookups

See `src/Sharpy.Compiler.Tests/Services/ServiceAdapterTests.cs` for examples.

---

## Cross-References

### Related Files

**Interface Definition:**
- `src/Sharpy.Compiler/Services/IClrTypeMapper.cs` - Contract for CLR type mapping

**Dependencies:**
- `src/Sharpy.Compiler/Semantic/ClrMemberCache.cs` - Underlying reflection cache
  - [Walkthrough: ClrMemberCache.md](../Semantic/ClrMemberCache.md)

**Related Adapters:**
- `src/Sharpy.Compiler/Services/TypeResolverAdapter.cs` - Resolves AST type annotations to `SemanticType`
- `src/Sharpy.Compiler/Services/SymbolLookupAdapter.cs` - Looks up symbols by name

**Usage:**
- `src/Sharpy.Compiler/Services/CompilerServicesBuilder.cs` - Constructs the adapter
- `src/Sharpy.Compiler/Semantic/Validation/SemanticContext.cs` - Consumes via `CompilerServices`
  - [Walkthrough: SemanticContext.md](../Semantic/Validation/SemanticContext.md)

### Relevant Specifications

- `docs/language_specification/type_hierarchy.md` - Sharpy's type hierarchy and `object` type
- `docs/language_specification/type_annotations.md` - How types are annotated in source
- `docs/language_specification/type_casting.md` - Type conversion rules

### Architecture Context

```
Sharpy Source → Lexer → Parser → AST
                                  ↓
                         Semantic Analysis
                         (uses ClrTypeMapperAdapter)
                                  ↓
                         Validation Pipeline
                         (uses ClrTypeMapperAdapter)
                                  ↓
                         RoslynEmitter → C# → .NET IL
                         (uses ClrTypeMapperAdapter)
```

The adapter is used throughout the semantic and code generation phases to bridge Sharpy's abstract type system with .NET's concrete type system.
