# Walkthrough: TypeMapper.cs

**Source File**: `src/Sharpy.Compiler/Discovery/TypeMapper.cs`

---

## 1. Overview

### What This File Does

`TypeMapper.cs` serves as a **bridge between the .NET CLR type system and Sharpy's semantic type system**. When the Sharpy compiler imports .NET assemblies or needs to understand external types (e.g., from `System.*` or `Sharpy.Core.*`), this class translates CLR `System.Type` instances into Sharpy's internal `SemanticType` representations.

### Role in the Overall Project

```
┌─────────────────────────────────────────────────────────────┐
│  External .NET Assemblies / Sharpy.Core Standard Library    │
└────────────────────┬────────────────────────────────────────┘
                     │ System.Type objects
                     ↓
          ┌──────────────────────┐
          │     TypeMapper       │ ← This file
          │  MapClrTypeToSemantic│
          └──────────┬───────────┘
                     │ SemanticType objects
                     ↓
┌────────────────────────────────────────────────────────────┐
│  Semantic Analysis (TypeChecker, TypeResolver, etc.)       │
│  Uses SemanticType for type checking & inference           │
└────────────────────────────────────────────────────────────┘
```

**Key Insight**: Sharpy compiles to C# via Roslyn, but during compilation it needs to reason about types in its own semantic model. TypeMapper performs the essential translation so the compiler can:
- Type-check calls to .NET methods
- Understand return types from standard library functions
- Handle generic types like `List<int>` → `list[int]`
- Map primitive types consistently

### Thread Safety

The class is explicitly designed to be **thread-safe** using `ConcurrentDictionary`, allowing multiple compilation units to share a single `TypeMapper` instance without race conditions.

---

## 2. Class Structure

### Main Class: `TypeMapper`

```csharp
public class TypeMapper
{
    private readonly ConcurrentDictionary<Type, SemanticType> _typeCache = new();
    
    public SemanticType MapClrTypeToSemanticType(Type clrType) { ... }
    private SemanticType MapTypeInternal(Type clrType) { ... }
    private SemanticType MapGenericType(Type clrType) { ... }
    private Type? GetIteratorElementType(Type clrType) { ... }
    private bool IsGenericTypeDefinition(Type type, Type genericTypeDef) { ... }
}
```

**Architecture**: Single-purpose class with a clean public API (`MapClrTypeToSemanticType`) and internal helpers. No inheritance or complex state—just caching and type mapping logic.

---

## 3. Key Methods Walkthrough

### 3.1 `MapClrTypeToSemanticType(Type clrType)` - Public Entry Point

**Purpose**: The only public method—converts a CLR type to a Sharpy semantic type with caching.

```csharp
public SemanticType MapClrTypeToSemanticType(Type clrType)
{
    return _typeCache.GetOrAdd(clrType, MapTypeInternal);
}
```

**How It Works**:
1. Checks if `clrType` has already been mapped (cache lookup)
2. If not cached, calls `MapTypeInternal` to perform the mapping
3. Stores the result in `_typeCache` atomically (thread-safe)
4. Returns the `SemanticType`

**Key Parameters**:
- `clrType`: A `System.Type` from reflection (e.g., `typeof(int)`, `typeof(List<string>)`)

**Return Value**: A `SemanticType` (e.g., `SemanticType.Int`, `new GenericType { Name = "list", ... }`)

**Design Decision**: Using `GetOrAdd` ensures thread-safety and prevents duplicate work if multiple threads request the same type simultaneously.

---

### 3.2 `MapTypeInternal(Type clrType)` - Core Mapping Logic

**Purpose**: The workhorse method that performs the actual type mapping through a decision tree.

#### Decision Flow

```
MapTypeInternal(clrType)
    ↓
1. Is it a primitive type? → Check PrimitiveCatalog
    ↓ Yes: Return SemanticType.Int, SemanticType.Str, etc.
    ↓ No: Continue
    ↓
2. Is it an array? → T[] becomes list[T]
    ↓ No: Continue
    ↓
3. Is it a nullable value type? → int? becomes int?
    ↓ No: Continue
    ↓
4. Is it an Iterator<T>? → Special handling for Sharpy.Core iterators
    ↓ No: Continue
    ↓
5. Is it generic? → Delegate to MapGenericType()
    ↓ No: Continue
    ↓
6. Is it an enum? → Map to SemanticType.Int
    ↓ No: Continue
    ↓
7. Fallback → Return SemanticType.Object
```

#### Step-by-Step Breakdown

**Step 1: Primitive Types**
```csharp
var primitiveInfo = PrimitiveCatalog.GetByClrType(clrType);
if (primitiveInfo != null)
{
    return primitiveInfo.SharpyName switch
    {
        "int" => SemanticType.Int,      // Singleton instance
        "str" => SemanticType.Str,      // Singleton instance
        "bool" => SemanticType.Bool,    // Singleton instance
        // ... etc
    };
}
```
- Uses `PrimitiveCatalog` (a registry of all primitive types)
- Returns **singleton instances** for common types (memory efficient!)
- Falls back to `new BuiltinType` for less common primitives

**Step 2: Arrays**
```csharp
if (clrType.IsArray)
{
    var elementType = clrType.GetElementType();
    return new GenericType
    {
        Name = "list",
        TypeArguments = new List<SemanticType>
        {
            MapClrTypeToSemanticType(elementType)  // Recursive call!
        }
    };
}
```
- C# arrays (`int[]`) → Sharpy lists (`list[int]`)
- **Recursive mapping**: element type is mapped via the same method
- Note: This loses some array-specific behavior (fixed size) but matches Python semantics

**Step 3: Nullable Value Types**
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
- `int?` in C# → `int?` in Sharpy
- Again, recursive mapping for the underlying type

**Step 4: Iterator Types**
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
- Handles special case: types that extend `Sharpy.Core.Iterator<T>` (like `RangeIterator`)
- These are kept as builtin types rather than being mapped to `list` (preserving lazy evaluation)

**Step 5: Generic Types**
```csharp
if (clrType.IsGenericType)
{
    return MapGenericType(clrType);  // Delegate to specialized method
}
```
- Complex handling for `List<T>`, `Dictionary<K,V>`, etc.
- See section 3.3 below for details

**Step 6: Enums**
```csharp
if (clrType.IsEnum)
{
    return SemanticType.Int;
}
```
- All enums map to `int` (simplified but practical)
- Loses type safety but matches Python's `IntEnum` behavior

**Step 7: Fallback**
```csharp
return SemanticType.Object;
```
- Unknown types become `object` (top type in Sharpy's type hierarchy)
- Allows compilation to proceed with minimal type information

---

### 3.3 `MapGenericType(Type clrType)` - Generic Type Handling

**Purpose**: Maps .NET generic types to Sharpy's generic representations.

#### Supported Generic Types

**1. Lists** (`List<T>`, `IList<T>`)
```csharp
if (IsGenericTypeDefinition(genericDef, typeof(List<>)) ||
    IsGenericTypeDefinition(genericDef, typeof(IList<>)))
{
    return new GenericType
    {
        Name = "list",
        TypeArguments = new List<SemanticType>
        {
            MapClrTypeToSemanticType(typeArgs[0])  // Recursively map element type
        }
    };
}
```
- `List<int>` → `list[int]`
- `IList<string>` → `list[str]`

**2. Dictionaries** (`Dictionary<K,V>`, `IDictionary<K,V>`)
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
- `Dictionary<string, int>` → `dict[str, int]`

**3. Sets** (`HashSet<T>`, `ISet<T>`)
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
- `HashSet<int>` → `set[int]`

**4. Enumerables** (`IEnumerable<T>`)
```csharp
if (IsGenericTypeDefinition(genericDef, typeof(IEnumerable<>)))
{
    return new GenericType
    {
        // Note: IEnumerable<T> is mapped to list for simplicity
        Name = "list",
        TypeArguments = new List<SemanticType>
        {
            MapClrTypeToSemanticType(typeArgs[0])
        }
    };
}
```
- `IEnumerable<T>` → `list[T]`
- **Important Limitation**: This loses lazy evaluation semantics! The comment acknowledges this trade-off.

**5. Tuples** (`System.Tuple`, `System.ValueTuple`)
```csharp
if (genericDef.FullName?.StartsWith("System.Tuple") == true ||
    genericDef.FullName?.StartsWith("System.ValueTuple") == true)
{
    return new TupleType
    {
        ElementTypes = typeArgs
            .Select(MapClrTypeToSemanticType)  // Map each element type
            .ToList()
    };
}
```
- `(int, string)` → `tuple[int, str]`
- Handles both reference tuples and value tuples

**6. Unknown Generics**
```csharp
return SemanticType.Object;
```
- Any other generic type falls back to `object`

#### Algorithm Insight

The method uses the **type definition** (via `GetGenericTypeDefinition()`) to identify the generic type, then recursively maps the type arguments. This allows nested generics to work correctly:
- `List<List<int>>` → `list[list[int]]`
- `Dictionary<string, List<int>>` → `dict[str, list[int]]`

---

### 3.4 `GetIteratorElementType(Type clrType)` - Iterator Type Detection

**Purpose**: Checks if a type extends `Sharpy.Core.Iterator<T>` and extracts the element type.

```csharp
private Type? GetIteratorElementType(Type clrType)
{
    var currentType = clrType;
    while (currentType != null)
    {
        if (currentType.IsGenericType &&
            currentType.GetGenericTypeDefinition().FullName == "Sharpy.Core.Iterator`1")
        {
            return currentType.GetGenericArguments()[0];  // Extract T
        }
        currentType = currentType.BaseType;  // Walk up inheritance chain
    }
    return null;  // Not an Iterator<T>
}
```

**Algorithm**: Walks the **inheritance hierarchy** (base class chain) looking for `Iterator<T>`.

**Example**:
```csharp
class RangeIterator : Iterator<int>  // Defined in Sharpy.Core
```
- Input: `typeof(RangeIterator)`
- Output: `typeof(int)`

**Why This Matters**: Iterator types need special handling to preserve lazy evaluation semantics. They shouldn't be eagerly converted to `list`.

**TODO Note**: The comment mentions this method is duplicated in `Semantic/ProtocolValidator.cs`. This is a known code smell—both places need the same logic, suggesting future refactoring into a shared utility.

---

### 3.5 `IsGenericTypeDefinition(Type type, Type genericTypeDef)` - Type Definition Comparison

**Purpose**: Simple helper to check if a type matches a generic type definition.

```csharp
private bool IsGenericTypeDefinition(Type type, Type genericTypeDef)
{
    return type == genericTypeDef;
}
```

**Usage**: 
```csharp
IsGenericTypeDefinition(genericDef, typeof(List<>))
```

**Why It Exists**: Encapsulates the pattern for clarity. The actual comparison is trivial, but having a named method makes the calling code more readable.

---

## 4. Dependencies

### Internal Dependencies

| Dependency | Purpose | Location |
|------------|---------|----------|
| `SemanticType` | Base type for Sharpy's type system | `Semantic/SemanticType.cs` |
| `BuiltinType` | Represents primitive/builtin types | `Semantic/SemanticType.cs` |
| `GenericType` | Represents generic types like `list[T]` | `Semantic/SemanticType.cs` |
| `NullableType` | Represents nullable types like `int?` | `Semantic/SemanticType.cs` |
| `TupleType` | Represents tuple types | `Semantic/SemanticType.cs` |
| `PrimitiveCatalog` | Registry of all primitive types | `Semantic/PrimitiveCatalog.cs` |

### External Dependencies

- **System.Reflection**: For CLR type introspection
- **System.Collections.Concurrent**: For thread-safe caching
- **System.Collections**: For generic type matching

### Dependency Graph

```
TypeMapper
    ↓ uses
PrimitiveCatalog (lookup primitive types)
    ↓ returns
SemanticType hierarchy
    ├── BuiltinType
    ├── GenericType
    ├── NullableType
    └── TupleType
```

---

## 5. Patterns and Design Decisions

### 5.1 Singleton Pattern for Common Types

**Pattern**: `SemanticType.Int`, `SemanticType.Str`, etc. are reused instead of creating new instances.

**Why**: Memory efficiency and fast equality checks (reference equality instead of structural equality).

```csharp
"int" => SemanticType.Int,  // Reuse singleton
```

### 5.2 Thread-Safe Caching with ConcurrentDictionary

**Pattern**: All cached data uses `ConcurrentDictionary` with atomic `GetOrAdd`.

**Why**: TypeMapper may be shared across multiple compilation units in parallel builds. The cache prevents redundant work and ensures consistent mappings.

**Implementation**:
```csharp
return _typeCache.GetOrAdd(clrType, MapTypeInternal);
```
- If the key exists, returns cached value
- If not, calls `MapTypeInternal` and stores result atomically
- No locks needed—ConcurrentDictionary handles synchronization

### 5.3 Recursive Type Mapping

**Pattern**: Type arguments are mapped recursively through the same entry point.

**Why**: Handles arbitrarily nested generics correctly.

```csharp
MapClrTypeToSemanticType(elementType)  // Recursive call
```

**Example Flow**:
```
List<List<int>> 
    → GenericType(list, [?])
        → List<int>
            → GenericType(list, [?])
                → int
                    → SemanticType.Int
```

### 5.4 Decision Tree with Early Returns

**Pattern**: Series of `if` checks with early returns, no complex nested logic.

**Why**: Clear control flow, easy to understand precedence, and good performance (short-circuits on match).

```csharp
if (primitiveInfo != null) return ...;
if (clrType.IsArray) return ...;
if (underlyingNullable != null) return ...;
// ...
```

### 5.5 Defensive Fallback to `object`

**Pattern**: Unknown types → `SemanticType.Object`

**Why**: Allows compilation to proceed even with incomplete type information. Better to lose precision than fail compilation.

```csharp
return SemanticType.Object;  // Safe fallback
```

### 5.6 Interface and Implementation Unification

**Pattern**: Both `List<T>` and `IList<T>` map to `list[T]`.

**Why**: Sharpy doesn't expose interface/implementation distinction—Python doesn't have this concept. The mapper abstracts away .NET's interface vs. concrete class distinction.

---

## 6. Debugging Tips

### 6.1 Tracing Type Mappings

**Problem**: "Why is this .NET type mapping to the wrong Sharpy type?"

**Solution**: Add logging to `MapTypeInternal`:
```csharp
private SemanticType MapTypeInternal(Type clrType)
{
    Console.WriteLine($"Mapping CLR type: {clrType.FullName}");
    
    // ... existing logic
    
    var result = ...; // computed result
    Console.WriteLine($"  → {result.GetDisplayName()}");
    return result;
}
```

### 6.2 Cache Inspection

**Problem**: "Is the cache working correctly?"

**Solution**: Expose cache contents for debugging:
```csharp
// Add to TypeMapper class
public IReadOnlyDictionary<Type, SemanticType> GetCacheSnapshot()
{
    return new Dictionary<Type, SemanticType>(_typeCache);
}
```

### 6.3 Identifying Generic Type Issues

**Problem**: "Generic types not mapping correctly."

**Diagnostic**:
```csharp
// In MapGenericType, log the generic definition
var genericDef = clrType.GetGenericTypeDefinition();
Console.WriteLine($"Generic definition: {genericDef.FullName}");
Console.WriteLine($"Type args: {string.Join(", ", typeArgs.Select(t => t.Name))}");
```

### 6.4 Handling `null` or Invalid Types

**Current Behavior**: The code assumes valid `Type` objects. If `clrType` is `null`, you'll get a `NullReferenceException`.

**Defensive Fix** (if needed):
```csharp
public SemanticType MapClrTypeToSemanticType(Type clrType)
{
    if (clrType == null)
        throw new ArgumentNullException(nameof(clrType));
    
    return _typeCache.GetOrAdd(clrType, MapTypeInternal);
}
```

### 6.5 Common Pitfalls

**Pitfall 1**: Forgetting that `IEnumerable<T>` maps to `list[T]` (loses laziness).
- **Solution**: Check if the caller expects lazy evaluation; consider using `Iterator<T>` pattern instead.

**Pitfall 2**: Enum mapping loses type information (all enums → `int`).
- **Solution**: If you need enum-specific behavior, add a dedicated `EnumType` to `SemanticType` hierarchy.

**Pitfall 3**: Nested generics not resolving correctly.
- **Solution**: Ensure recursive calls are happening. Add logging to trace recursion depth.

### 6.6 Performance Profiling

**Concern**: Is type mapping a bottleneck?

**How to Profile**:
```csharp
private SemanticType MapTypeInternal(Type clrType)
{
    var sw = Stopwatch.StartNew();
    var result = /* ... actual mapping logic ... */;
    sw.Stop();
    
    if (sw.ElapsedMilliseconds > 10)  // Log slow mappings
        Console.WriteLine($"Slow mapping: {clrType.FullName} took {sw.ElapsedMilliseconds}ms");
    
    return result;
}
```

---

## 7. Contribution Guidelines

### 7.1 Adding Support for New .NET Types

**When**: You need to map a new .NET type that currently falls through to `object`.

**Steps**:
1. Determine the appropriate Sharpy type representation
2. Add a case in `MapTypeInternal` or `MapGenericType`
3. Add tests in `Sharpy.Compiler.Tests/Discovery/TypeMapperTests.cs`
4. Update this documentation

**Example**: Adding support for `Span<T>`
```csharp
// In MapGenericType, before the fallback:
if (IsGenericTypeDefinition(genericDef, typeof(Span<>)) ||
    IsGenericTypeDefinition(genericDef, typeof(ReadOnlySpan<>)))
{
    return new GenericType
    {
        Name = "list",  // Or a new "span" type if needed
        TypeArguments = new List<SemanticType>
        {
            MapClrTypeToSemanticType(typeArgs[0])
        }
    };
}
```

### 7.2 Optimizing Cache Performance

**When**: Compilation is slow and profiling shows cache contention.

**Ideas**:
- Use `ImmutableDictionary` for read-heavy scenarios
- Implement cache eviction for long-running processes
- Pre-populate cache with common types at startup

### 7.3 Improving Iterator Type Detection

**Current Issue**: `GetIteratorElementType` is duplicated in `ProtocolValidator.cs`.

**Refactoring Opportunity**:
1. Extract to a shared utility class (e.g., `Semantic/TypeIntrospection.cs`)
2. Add comprehensive tests for edge cases (multiple inheritance levels, interface implementations)
3. Update both call sites to use the shared method

**Suggested Location**:
```csharp
// Semantic/TypeIntrospection.cs
public static class TypeIntrospection
{
    public static Type? GetIteratorElementType(Type clrType) { ... }
    // Other type introspection utilities
}
```

### 7.4 Enhancing Type Information Preservation

**Current Limitation**: Some .NET types lose information when mapped.

**Examples**:
- `IEnumerable<T>` → `list[T]` (loses laziness)
- Enums → `int` (loses type safety)
- Arrays → `list` (loses fixed-size constraint)

**Contribution Ideas**:
- Add `LazyListType` or `IterableType` for `IEnumerable<T>`
- Add `EnumType` with original CLR type information
- Add metadata to track original .NET type characteristics

### 7.5 Adding Validation and Error Handling

**Current Behavior**: Fails silently with fallbacks to `object`.

**Improvement Ideas**:
- Add optional diagnostics/warnings for fallback cases
- Validate that mapped types are compatible with Sharpy's type system
- Add explicit error for unsupported types (e.g., pointers, ref structs)

**Example**:
```csharp
// In MapTypeInternal
if (clrType.IsByRef || clrType.IsPointer)
{
    throw new NotSupportedException(
        $"Type {clrType.FullName} is not supported in Sharpy (pointers/refs not allowed)");
}
```

### 7.6 Testing Checklist

When modifying `TypeMapper`, ensure:
- [ ] Thread-safety is preserved (concurrent access test)
- [ ] Cache hit rate is measured (performance test)
- [ ] Recursive mappings work correctly (nested generic test)
- [ ] Edge cases handled (`null` element types, empty tuples, etc.)
- [ ] Integration tests pass (end-to-end compilation with new type)

### 7.7 Documentation Maintenance

**When adding a new type mapping**, update:
1. This walkthrough document (add example to relevant section)
2. XML documentation comments in the source code
3. Type system specification (`docs/specs/type_system.md`)
4. Release notes if it's a user-visible change

---

## 8. Advanced Topics

### 8.1 Type Mapping and Type Erasure

**Concept**: Generic types in .NET retain their type arguments at runtime (reified generics), but Sharpy's compilation to C# needs to preserve this information through the pipeline.

**How TypeMapper Helps**: By creating `GenericType` instances with explicit `TypeArguments`, the semantic analysis phase can reason about type compatibility without losing information.

**Example**:
```python
# Sharpy code
def process(items: list[int]) -> int:
    return sum(items)
```

The compiler needs to know that `items` is specifically `list[int]`, not just `list`. TypeMapper ensures this information is captured when mapping .NET types back to Sharpy types.

### 8.2 Covariance and Contravariance

**Current Limitation**: TypeMapper doesn't consider variance when mapping generic types.

**Example**:
```csharp
IEnumerable<string> → list[str]
IEnumerable<object> → list[object]
```

In C#, `IEnumerable<string>` is covariant with `IEnumerable<object>`, but Sharpy's type system may not preserve this relationship.

**Future Work**: Add variance information to `GenericType` or implement variance checking in `TypeChecker`.

### 8.3 Nullable Reference Types (C# 8+)

**Current Behavior**: TypeMapper only handles nullable **value** types (`int?`).

**Missing**: Support for nullable reference types (`string?` in C# 8+).

**Why It's Tricky**: Nullable reference types are a compile-time feature, not represented in CLR metadata. The information is stored in attributes that require special parsing.

**Contribution Opportunity**: Add support by checking for `NullableAttribute` on types/members.

---

## 9. Related Files to Explore

| File | Why It's Related |
|------|------------------|
| `Semantic/SemanticType.cs` | Defines the `SemanticType` hierarchy that TypeMapper produces |
| `Semantic/PrimitiveCatalog.cs` | Registry of primitive types that TypeMapper consults |
| `Semantic/ProtocolValidator.cs` | Contains duplicate `GetIteratorElementType` logic |
| `Semantic/TypeChecker.cs` | Consumes `SemanticType` instances for type checking |
| `CodeGen/TypeMapper.cs` | **Different file!** Maps Sharpy types → C# types (reverse direction) |
| `Discovery/CachedModuleDiscovery.cs` | Uses TypeMapper to import external assemblies |

**Key Distinction**: There are **two** `TypeMapper.cs` files in the codebase:
1. `Discovery/TypeMapper.cs` (this file): CLR → Sharpy
2. `CodeGen/TypeMapper.cs`: Sharpy → C# (for code generation)

Don't confuse them!

---

## 10. Quick Reference: Type Mapping Rules

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

## Conclusion

`TypeMapper.cs` is a critical but conceptually simple component: it translates between two type systems. Its design prioritizes correctness (thread-safety), performance (caching), and maintainability (clear decision tree). Understanding this file is essential for anyone working on:

- Importing external .NET assemblies
- Adding new builtin types to Sharpy
- Debugging type checking errors with .NET interop
- Optimizing compilation performance

When in doubt, remember the core principle: **TypeMapper bridges .NET and Sharpy, making the compiler bilingual in type systems.**
