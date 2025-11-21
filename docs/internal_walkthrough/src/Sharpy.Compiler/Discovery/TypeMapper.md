# Walkthrough: TypeMapper.cs

**Source File**: `src/Sharpy.Compiler/Discovery/TypeMapper.cs`

---

## 1. Overview

The `TypeMapper` class is a critical bridge component in the Sharpy compiler that translates between two different type systems:

- **CLR Types** (Common Language Runtime / .NET types): The actual types used by .NET assemblies (`System.Int32`, `List<T>`, `Dictionary<K,V>`, etc.)
- **Sharpy SemanticTypes**: The compiler's internal representation of types used during semantic analysis and type checking

This translation is essential when Sharpy code imports and uses .NET libraries. When you write `from System.Collections.Generic import List` in Sharpy, the TypeMapper converts the CLR `List<T>` type into Sharpy's `list[T]` semantic type so the compiler can properly type-check your code.

**Key Responsibilities:**
- Map primitive CLR types to Sharpy builtin types (`int`, `string`, `bool`, etc.)
- Convert .NET generic collections to Sharpy's Pythonic equivalents (`List<T>` → `list[T]`, `Dictionary<K,V>` → `dict[K,V]`)
- Handle nullable types, arrays, tuples, and enums
- Cache mappings for performance (thread-safe)

**Location in Pipeline:** The TypeMapper is primarily used during the **module discovery** phase when the compiler analyzes imported .NET assemblies to understand what types and functions are available.

---

## 2. Class Structure

### TypeMapper Class

```csharp
public class TypeMapper
{
    private readonly ConcurrentDictionary<Type, SemanticType> _typeCache = new();
    
    public SemanticType MapClrTypeToSemanticType(Type clrType)
    private SemanticType MapTypeInternal(Type clrType)
    private SemanticType MapGenericType(Type clrType)
    private bool IsGenericTypeDefinition(Type type, Type genericTypeDef)
}
```

**Design Characteristics:**
- **Thread-safe**: Uses `ConcurrentDictionary` for safe concurrent access during parallel module discovery
- **Stateless logic**: All mapping logic is deterministic based on the input CLR type
- **Caching**: Memoizes results to avoid redundant type analysis (important for performance when analyzing large assemblies)

---

## 3. Key Methods

### 3.1 `MapClrTypeToSemanticType(Type clrType)` - Public API

**Purpose:** The main entry point for type mapping. This is the only public method.

**Signature:**
```csharp
public SemanticType MapClrTypeToSemanticType(Type clrType)
```

**How it works:**
1. Checks the cache to see if this CLR type has been mapped before
2. If cached, returns the cached result (O(1) lookup)
3. If not cached, calls `MapTypeInternal()` to compute the mapping
4. Stores the result in cache and returns it

**Why caching matters:**
- A single .NET assembly can contain hundreds of types
- The same type may appear in many method signatures
- Recursive types (e.g., `List<List<int>>`) would cause redundant work without caching

**Thread-safety:** Uses `ConcurrentDictionary.GetOrAdd()` which is atomic - only one thread will compute the mapping for a given CLR type, even if multiple threads request it simultaneously.

---

### 3.2 `MapTypeInternal(Type clrType)` - Core Mapping Logic

**Purpose:** Contains the actual mapping logic. This is where the "translation rules" live.

**Signature:**
```csharp
private SemanticType MapTypeInternal(Type clrType)
```

**Mapping Strategy (in order of evaluation):**

#### 1. Primitive Types (Lines 28-35)
Maps fundamental .NET types to Sharpy builtins:

```csharp
if (clrType == typeof(int)) return SemanticType.Int;
if (clrType == typeof(string)) return SemanticType.Str;
// ... etc
```

| CLR Type | Sharpy Type |
|----------|-------------|
| `int` | `int` |
| `long` | `long` |
| `float` | `float` |
| `double` | `double` |
| `bool` | `bool` |
| `string` | `str` |
| `void` | `None` (VoidType) |
| `object` | `object` |

**Why this order?** Primitives are the most common types, so checking them first optimizes for the common case.

#### 2. Arrays (Lines 38-52)
Converts CLR arrays to Sharpy `list[T]`:

```csharp
if (clrType.IsArray)
{
    var elementType = clrType.GetElementType();
    return new GenericType
    {
        Name = "list",
        TypeArguments = new List<SemanticType>
        {
            MapClrTypeToSemanticType(elementType)  // Recursive!
        }
    };
}
```

**Examples:**
- `int[]` → `list[int]`
- `string[]` → `list[str]`
- `int[][]` → `list[list[int]]` (recursive mapping)

**Defensive programming:** The null check on `elementType` (line 41-42) handles edge cases where reflection might return null for exotic array types.

#### 3. Nullable Value Types (Lines 54-62)
Maps `Nullable<T>` to Sharpy's optional type syntax `T?`:

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

**Examples:**
- `int?` → `int?`
- `bool?` → `bool?`

**Why this check is needed:** In .NET, `Nullable<T>` is a special generic struct that requires specific handling. The `Nullable.GetUnderlyingType()` method returns non-null only for nullable value types.

#### 4. Generic Types (Lines 64-68)
Delegates to `MapGenericType()` for complex generic types:

```csharp
if (clrType.IsGenericType)
{
    return MapGenericType(clrType);
}
```

This handles:
- `List<T>`, `Dictionary<K,V>`, `HashSet<T>`
- `IList<T>`, `IDictionary<K,V>`, `IEnumerable<T>`
- `Tuple<...>`, `ValueTuple<...>`
- Any other generic type

#### 5. Enums (Lines 70-74)
Simplifies .NET enums to `int`:

```csharp
if (clrType.IsEnum)
{
    return SemanticType.Int;
}
```

**Design decision:** Sharpy treats enums as integers rather than creating a separate enum type. This is a pragmatic choice that simplifies the type system while maintaining interop with .NET enums.

**Implication:** You lose compile-time enum checking when using .NET enums from Sharpy, but you gain simplicity.

#### 6. Fallback (Lines 76-77)
Unknown or unsupported types become `object`:

```csharp
return SemanticType.Object;
```

**Why `object` and not an error?** 
- Allows gradual typing - you can use .NET types the compiler doesn't fully understand
- Prevents compilation failures when encountering exotic types
- Maintains compatibility with the full .NET ecosystem

---

### 3.3 `MapGenericType(Type clrType)` - Generic Type Specialist

**Purpose:** Handles the complexities of .NET generic types, focusing on common collection types.

**Signature:**
```csharp
private SemanticType MapGenericType(Type clrType)
```

**How it works:**

1. **Extract generic definition and type arguments:**
```csharp
var genericDef = clrType.GetGenericTypeDefinition();  // e.g., List<>
var typeArgs = clrType.GetGenericArguments();          // e.g., [int]
```

2. **Pattern match against known generic types:**

#### List Types (Lines 86-97)
```csharp
if (IsGenericTypeDefinition(genericDef, typeof(List<>)) ||
    IsGenericTypeDefinition(genericDef, typeof(IList<>)))
{
    return new GenericType
    {
        Name = "list",
        TypeArguments = new List<SemanticType>
        {
            MapClrTypeToSemanticType(typeArgs[0])
        }
    };
}
```

**Maps:**
- `List<int>` → `list[int]`
- `IList<string>` → `list[str]`
- `List<List<bool>>` → `list[list[bool]]` (recursive)

#### Dictionary Types (Lines 100-112)
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

**Maps:**
- `Dictionary<string, int>` → `dict[str, int]`
- `IDictionary<int, List<string>>` → `dict[int, list[str]]`

#### Set Types (Lines 115-126)
```csharp
if (IsGenericTypeDefinition(genericDef, typeof(HashSet<>)) ||
    IsGenericTypeDefinition(genericDef, typeof(ISet<>)))
{
    return new GenericType
    {
        Name = "set",
        TypeArguments = new List<SemanticType>
        {
            MapClrTypeToSemanticType(typeArgs[0])
        }
    };
}
```

**Maps:**
- `HashSet<int>` → `set[int]`
- `ISet<string>` → `set[str]`

#### IEnumerable Types (Lines 129-141)
```csharp
if (IsGenericTypeDefinition(genericDef, typeof(IEnumerable<>)))
{
    return new GenericType
    {
        Name = "list",  // Note: Mapped to list, not iterator!
        TypeArguments = new List<SemanticType>
        {
            MapClrTypeToSemanticType(typeArgs[0])
        }
    };
}
```

**⚠️ Important Limitation (Lines 133-135):**
```csharp
// Note: IEnumerable<T> is mapped to list for simplicity, losing lazy evaluation semantics.
// This is a known limitation - iterables are treated as eager lists in the type system.
```

**What this means:**
- `IEnumerable<int>` → `list[int]`
- Lazy evaluation semantics are lost
- Sharpy treats all enumerables as if they were materialized lists

**Why this trade-off?**
- Simplifies the type system (no separate iterator type)
- Most Sharpy code expects list-like behavior
- Known limitation to be improved in future versions

#### Tuple Types (Lines 144-153)
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

**Maps both reference and value tuples:**
- `Tuple<int, string>` → `tuple[int, str]`
- `ValueTuple<bool, double>` → `tuple[bool, double]`
- `(int, string, bool)` → `tuple[int, str, bool]`

**Implementation detail:** Uses string comparison on `FullName` rather than type comparison because there are multiple tuple arities (Tuple<T1>, Tuple<T1,T2>, etc.)

#### Unknown Generic Types (Lines 156)
```csharp
return SemanticType.Object;
```

**Fallback:** Any generic type not explicitly handled becomes `object`.

**Examples of unsupported generics:**
- Custom generic types from third-party libraries
- Less common BCL types like `Span<T>`, `Memory<T>`
- Future: Could be extended to support more types

---

### 3.4 `IsGenericTypeDefinition(Type type, Type genericTypeDef)` - Helper

**Purpose:** Utility method to check if a type matches a generic type definition.

**Signature:**
```csharp
private bool IsGenericTypeDefinition(Type type, Type genericTypeDef)
{
    return type == genericTypeDef;
}
```

**Why it exists:** Improves code readability in `MapGenericType()`. The comparison checks if a type (e.g., `List<>`) matches a generic definition.

**Usage example:**
```csharp
var listOfInt = typeof(List<int>);
var genericDef = listOfInt.GetGenericTypeDefinition();  // Returns List<>
IsGenericTypeDefinition(genericDef, typeof(List<>))      // Returns true
```

---

## 4. Dependencies

### Internal Dependencies (Sharpy.Compiler)

**`Sharpy.Compiler.Semantic.SemanticType`** (Lines 4, 19-77)
- The target type system for mappings
- Includes: `SemanticType`, `GenericType`, `NullableType`, `TupleType`, `BuiltinType`, `UserDefinedType`
- Used to construct Sharpy type representations

**Related Components:**
- **`CachedModuleDiscovery`**: Primary consumer of TypeMapper
- **`OverloadIndexBuilder`**: Uses TypeMapper to analyze .NET method signatures
- **Code Generator (`CodeGen/TypeMapper.cs`)**: Separate class that does the reverse mapping (Sharpy → C#)

### External Dependencies (.NET BCL)

**`System.Collections`** (Line 1)
- Used for type checking against `IEnumerable<T>`, `IList<T>`, `IDictionary<K,V>`, `ISet<T>`

**`System.Collections.Concurrent.ConcurrentDictionary`** (Line 2)
- Thread-safe cache implementation
- Essential for concurrent module discovery

**`System.Reflection`** (Line 3)
- `Type` class for CLR type introspection
- Methods like `IsArray`, `IsGenericType`, `GetGenericTypeDefinition()`, `GetGenericArguments()`
- `Nullable.GetUnderlyingType()` for nullable detection

---

## 5. Design Patterns & Architectural Decisions

### Design Patterns

#### 1. **Flyweight Pattern (via Caching)**
The `_typeCache` implements a flyweight pattern to share `SemanticType` instances:
- Reduces memory usage when the same CLR type is encountered multiple times
- Improves performance by avoiding redundant type analysis

#### 2. **Strategy Pattern (Implicit)**
The series of `if` checks in `MapTypeInternal()` and `MapGenericType()` implements a strategy pattern where different mapping strategies are selected based on type characteristics.

#### 3. **Recursive Descent**
Type mapping is inherently recursive:
```csharp
List<List<int>> 
  → list[
      list[int]  // Recursive call to map List<int>
    ]
```

### Architectural Decisions

#### ✅ Why Map CLR → Sharpy (not bidirectional in this class)?
- **Separation of concerns**: This class handles interop *importing* .NET types
- **Different class for export**: `CodeGen/TypeMapper.cs` handles the reverse (Sharpy → C# code generation)
- **Unidirectional flow**: Discovery happens once at compile-time, mapping is one-way

#### ✅ Why Thread-Safe with ConcurrentDictionary?
- **Parallel module discovery**: The compiler may analyze multiple .NET assemblies concurrently
- **Shared state**: Multiple threads may request mapping for the same type simultaneously
- **Performance**: `ConcurrentDictionary.GetOrAdd()` ensures only one thread computes each mapping

#### ✅ Why Fallback to `object` Instead of Error?
- **Robustness**: Allows using .NET libraries with exotic types
- **Gradual typing**: Permits dynamic-like behavior for unknown types
- **Future-proof**: New .NET types won't break existing Sharpy code

#### ⚠️ Why Map `IEnumerable<T>` to `list[T]`?
**Known limitation documented in code (lines 133-135)**

**Pros:**
- Simplifies type system
- Most Sharpy code expects materialized collections
- Avoids need for separate iterator types

**Cons:**
- Loses lazy evaluation semantics
- `LINQ` queries evaluated eagerly
- Potential performance impact for large sequences

**Future improvement:** Could introduce an `Iterable<T>` type to preserve lazy semantics.

#### ✅ Why Treat Enums as `int`?
- **Simplicity**: Avoids complex enum type system
- **Pragmatism**: Most enum usage is compatible with integer operations
- **Trade-off**: Loses type safety for enum values, but maintains .NET interop

---

## 6. Debugging Tips

### Common Issues & How to Debug Them

#### Issue 1: "Type X is being mapped to `object` unexpectedly"

**Symptoms:** A .NET type you expect to work is showing up as `object` in error messages.

**Debugging steps:**
1. Add logging to `MapTypeInternal()`:
   ```csharp
   Console.WriteLine($"Mapping CLR type: {clrType.FullName}");
   ```
2. Check if the type is:
   - A generic type not in the supported list
   - A custom type from a third-party library
   - A newer .NET type not yet supported
3. Look at the type's characteristics:
   ```csharp
   Console.WriteLine($"IsArray: {clrType.IsArray}");
   Console.WriteLine($"IsGenericType: {clrType.IsGenericType}");
   Console.WriteLine($"IsEnum: {clrType.IsEnum}");
   ```

**Solution:** Add support for the type in `MapGenericType()` or accept `object` as the mapping.

#### Issue 2: "Recursive type mapping causes stack overflow"

**Symptoms:** Compiler crashes when analyzing certain .NET types.

**Example problematic type:**
```csharp
class Node { Node Next; }  // Self-referential type
```

**Current behavior:** TypeMapper doesn't detect cycles - it relies on the SemanticType system to handle recursive types.

**Debugging:**
1. Add depth tracking:
   ```csharp
   private SemanticType MapTypeInternal(Type clrType, int depth = 0)
   {
       if (depth > 100) throw new Exception($"Recursion depth exceeded for {clrType}");
       // ... rest of mapping with depth+1 for recursive calls
   }
   ```

2. Check if the type is self-referential:
   ```csharp
   Console.WriteLine($"Type: {clrType.Name}, Members: {string.Join(", ", clrType.GetProperties().Select(p => p.PropertyType.Name))}");
   ```

#### Issue 3: "Generic type arguments are wrong"

**Symptoms:** `List<string>` is mapped to `list[object]` instead of `list[str]`.

**Debugging:**
1. Verify generic arguments are extracted correctly:
   ```csharp
   var typeArgs = clrType.GetGenericArguments();
   Console.WriteLine($"Type args: {string.Join(", ", typeArgs.Select(t => t.FullName))}");
   ```

2. Check if mapping is recursive:
   ```csharp
   foreach (var arg in typeArgs)
   {
       var mapped = MapClrTypeToSemanticType(arg);
       Console.WriteLine($"{arg.Name} → {mapped.GetDisplayName()}");
   }
   ```

#### Issue 4: "Thread-safety violations"

**Symptoms:** Intermittent crashes or corrupted type mappings in parallel builds.

**Debugging:**
1. Verify `ConcurrentDictionary` is used correctly (it is in current code)
2. Check if `SemanticType` instances are immutable (they are - record types)
3. Look for shared mutable state outside the cache (there isn't any)

**Current implementation is thread-safe** ✅

### Useful Debugging Additions

Add these temporary debug helpers when troubleshooting:

```csharp
// Log all mappings
public SemanticType MapClrTypeToSemanticType(Type clrType)
{
    var result = _typeCache.GetOrAdd(clrType, MapTypeInternal);
    Console.WriteLine($"[TypeMapper] {clrType.FullName} → {result.GetDisplayName()}");
    return result;
}

// Inspect cache contents
public void DumpCache()
{
    foreach (var (clrType, semanticType) in _typeCache)
    {
        Console.WriteLine($"{clrType.FullName} → {semanticType.GetDisplayName()}");
    }
}
```

---

## 7. Contribution Guidelines

### Types of Contributions

#### 1. **Adding Support for New .NET Types**

**Good candidates:**
- `Span<T>`, `ReadOnlySpan<T>`, `Memory<T>` (modern .NET types)
- `IAsyncEnumerable<T>` (async streams)
- `ImmutableList<T>`, `ImmutableDictionary<K,V>` (immutable collections)
- `ConcurrentBag<T>`, `ConcurrentQueue<T>` (concurrent collections)

**Implementation pattern:**
```csharp
// In MapGenericType():
if (IsGenericTypeDefinition(genericDef, typeof(Span<>)))
{
    return new GenericType
    {
        Name = "list",  // or create new SpanType?
        TypeArguments = new List<SemanticType>
        {
            MapClrTypeToSemanticType(typeArgs[0])
        }
    };
}
```

**Testing checklist:**
- [ ] Add test in `Sharpy.Compiler.Tests/Discovery/TypeMapperTests.cs`
- [ ] Test with generic type arguments (e.g., `Span<int>`, `Span<string>`)
- [ ] Test nested generics (e.g., `Span<List<int>>`)
- [ ] Verify cache works correctly
- [ ] Update documentation

#### 2. **Improving IEnumerable Handling**

**Current limitation:** `IEnumerable<T>` maps to `list[T]`, losing lazy evaluation.

**Possible improvement:**
```csharp
// Create new IterableType in SemanticType.cs
public record IterableType : SemanticType
{
    public SemanticType ElementType { get; init; }
    public override string GetDisplayName() => $"Iterable[{ElementType.GetDisplayName()}]";
}

// Update TypeMapper
if (IsGenericTypeDefinition(genericDef, typeof(IEnumerable<>)))
{
    return new IterableType
    {
        ElementType = MapClrTypeToSemanticType(typeArgs[0])
    };
}
```

**Impact analysis needed:**
- How does this affect type checking in `TypeChecker.cs`?
- Do we need conversion rules between `Iterable[T]` and `list[T]`?
- How does code generation handle iterables?

#### 3. **Performance Optimizations**

**Profiling opportunities:**
```csharp
// Add metrics
private int _cacheHits = 0;
private int _cacheMisses = 0;

public SemanticType MapClrTypeToSemanticType(Type clrType)
{
    bool wasAdded = false;
    var result = _typeCache.GetOrAdd(clrType, t => {
        wasAdded = true;
        return MapTypeInternal(t);
    });
    
    if (wasAdded) _cacheMisses++;
    else _cacheHits++;
    
    return result;
}

public void LogCacheStats()
{
    var hitRate = (double)_cacheHits / (_cacheHits + _cacheMisses);
    Console.WriteLine($"TypeMapper cache hit rate: {hitRate:P}");
}
```

**Optimization ideas:**
- Pre-populate cache with common types during initialization
- Use `FrozenDictionary` for read-heavy scenarios (.NET 8+)
- Consider struct-based types to reduce allocations

#### 4. **Better Error Reporting**

**Current behavior:** Unknown types silently become `object`.

**Improvement:**
```csharp
private SemanticType MapTypeInternal(Type clrType)
{
    // ... existing checks ...
    
    // Before fallback to object:
    _logger?.LogWarning($"Unknown CLR type '{clrType.FullName}' mapped to 'object'. Consider adding explicit support.");
    
    return SemanticType.Object;
}
```

#### 5. **Supporting Nullable Reference Types**

**Current gap:** Only nullable value types (`int?`) are handled. C# 8+ nullable reference types (`string?`) are not distinguished from non-nullable references.

**Challenge:** Nullable reference types are annotations, not runtime types - they don't exist in reflection metadata by default.

**Possible approach:**
```csharp
// Check for nullable annotation using Nullable attribute
private bool IsNullableReferenceType(Type type, MemberInfo? member)
{
    if (!type.IsValueType && member != null)
    {
        var nullable = member.CustomAttributes
            .FirstOrDefault(x => x.AttributeType.FullName == "System.Runtime.CompilerServices.NullableAttribute");
        return nullable != null;
    }
    return false;
}
```

**Note:** This requires passing member context (parameter, property, etc.) which would require API changes.

### Contribution Process

1. **Check existing issues:** Look for related feature requests or bug reports
2. **Write tests first:** Add failing tests to `Sharpy.Compiler.Tests/Discovery/TypeMapperTests.cs`
3. **Implement changes:** Modify `TypeMapper.cs` following existing patterns
4. **Update tests:** Ensure all tests pass, including related semantic tests
5. **Document changes:** Update this walkthrough if adding significant features
6. **Performance check:** Verify cache hit rates don't degrade
7. **Submit PR:** Reference related issues, include examples

### Testing Your Changes

```bash
# Run TypeMapper-specific tests
dotnet test --filter "FullyQualifiedName~TypeMapperTests"

# Run full Discovery tests
dotnet test --filter "FullyQualifiedName~Discovery"

# Run integration tests that use .NET interop
dotnet test --filter "FullyQualifiedName~Integration"

# Full test suite
dotnet test
```

### Code Style Guidelines

**Follow existing patterns:**
- Use `if` chains for type checking (not switch expressions) for consistency
- Add comments for non-obvious mappings (see `IEnumerable<T>` example)
- Order checks from most-specific to most-general
- Use descriptive variable names (`underlyingNullable`, not `u`)
- Maintain thread-safety (avoid mutable shared state)

**Example addition:**
```csharp
// Handle ReadOnlySpan<T> - map to list for simplicity
// Note: This loses the read-only and stack-allocated semantics
if (IsGenericTypeDefinition(genericDef, typeof(ReadOnlySpan<>)))
{
    return new GenericType
    {
        Name = "list",
        TypeArguments = new List<SemanticType>
        {
            MapClrTypeToSemanticType(typeArgs[0])
        }
    };
}
```

---

## Summary

The `TypeMapper` is a focused, well-designed component that bridges .NET's type system with Sharpy's semantic analysis. Its key strengths are:

- ✅ **Thread-safe** caching for performance
- ✅ **Comprehensive** coverage of common .NET types
- ✅ **Recursive** handling of complex generic types
- ✅ **Pragmatic** fallbacks for unknown types
- ✅ **Well-documented** trade-offs and limitations

When working with this file, remember:
1. It's about **importing** .NET types into Sharpy (one direction)
2. The **cache is critical** for performance
3. **Thread-safety matters** due to parallel module discovery
4. **Fallbacks are intentional** - not all types need perfect mappings
5. **Known limitations** (like `IEnumerable<T>`) are documented and acceptable

For questions or clarifications, consult:
- `docs/architecture/type_system.md` (if it exists)
- `Sharpy.Compiler.Tests/Discovery/TypeMapperTests.cs` (test examples)
- The companion `CodeGen/TypeMapper.cs` (reverse mapping)
