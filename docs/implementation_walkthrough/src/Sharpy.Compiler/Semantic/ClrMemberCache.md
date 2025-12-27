# Walkthrough: ClrMemberCache.cs

**Source File**: `src/Sharpy.Compiler/Semantic/ClrMemberCache.cs`

---

## 1. Overview

### What Does This File Do?

`ClrMemberCache` is a **performance optimization layer** that sits between the Sharpy compiler's semantic analyzer and .NET's reflection API. When Sharpy code interacts with .NET types (e.g., calling methods on `System.String` or using operators on `System.Numerics.BigInteger`), the compiler needs to discover what members those types have. Reflection is expensive, so this cache stores that metadata to avoid repeated lookups.

### Role in the Overall Project

In the Sharpy compilation pipeline:

```
Source (.spy) → Lexer → Parser (AST) → Semantic Analysis → CodeGen → C#
                                            ↑
                                    ClrMemberCache lives here
```

During **semantic analysis**, the compiler needs to:
- Resolve method calls on .NET types
- Check if a type supports certain operators (like `+` or `==`)
- Determine if a type is enumerable (for `for` loops)
- Find indexer properties (for `obj[index]` syntax)

`ClrMemberCache` makes these operations fast by caching reflection results.

### Key Characteristics

- **Lazy Loading**: Metadata is discovered only when first requested for a type
- **NOT Thread-Safe**: Designed for single-threaded compilation (important!)
- **Compilation-Scoped**: Cache is per-compilation, not meant to persist across compilations
- **Read-Only Results**: Returns immutable collections to prevent external modification

---

## 2. Class/Type Structure

### Main Class: `ClrMemberCache`

This is a straightforward cache class with four internal dictionaries, each caching a different kind of type metadata:

```csharp
public class ClrMemberCache
{
    // 1. Operator methods: Type → (op name → method list)
    private readonly Dictionary<Type, Dictionary<string, IReadOnlyList<MethodInfo>>> _operatorCache;
    
    // 2. Interfaces: Type → interface set
    private readonly Dictionary<Type, HashSet<Type>> _interfaceCache;
    
    // 3. Indexers: Type → (has indexer?, element type?)
    private readonly Dictionary<Type, (bool HasIndexer, Type? ElementType)> _indexerCache;
    
    // 4. Enumerators: Type → element type
    private readonly Dictionary<Type, Type?> _enumeratorCache;
}
```

**No Inheritance or Interfaces**: This is a simple, concrete class with no abstractions. It's meant to be instantiated directly by the semantic analyzer.

---

## 3. Key Functions/Methods

### 3.1 `GetOperatorMethods(Type clrType)`

**Purpose**: Discovers all operator overloads for a .NET type.

**Why It Exists**: When Sharpy code uses an operator like `a + b`, the compiler needs to check if type `A` defines an `op_Addition` method. This method finds all such operators.

```csharp
public IReadOnlyDictionary<string, IReadOnlyList<MethodInfo>> GetOperatorMethods(Type clrType)
{
    if (_operatorCache.TryGetValue(clrType, out var cached))
        return cached;
    
    var operators = DiscoverOperatorMethods(clrType);
    _operatorCache[clrType] = operators;
    return operators;
}
```

**Algorithm**:
1. **Check cache first**: If we've already looked up this type, return cached results
2. **Discovery**: Call `DiscoverOperatorMethods` to use reflection
3. **Store and return**: Cache the results for future use

**Return Value**: A dictionary where:
- **Key**: Operator name (e.g., `"op_Addition"`, `"op_Equality"`)
- **Value**: List of `MethodInfo` objects (multiple overloads possible)

**Example Use Case**:
```python
# Sharpy code
x: BigInteger = BigInteger(5)
y: BigInteger = BigInteger(10)
z = x + y  # Compiler looks up "op_Addition" on BigInteger
```

---

### 3.2 `DiscoverOperatorMethods(Type clrType)` (Private Helper)

**Purpose**: The actual reflection work for finding operators.

```csharp
private Dictionary<string, IReadOnlyList<MethodInfo>> DiscoverOperatorMethods(Type clrType)
{
    // Find all static methods starting with "op_"
    var methods = clrType.GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Where(m => m.Name.StartsWith("op_"));
    
    // Group by method name (to handle overloads)
    var grouped = new Dictionary<string, List<MethodInfo>>();
    foreach (var method in methods)
    {
        if (!grouped.TryGetValue(method.Name, out var methodList))
        {
            methodList = new List<MethodInfo>();
            grouped[method.Name] = methodList;
        }
        methodList.Add(method);
    }
    
    // Convert to read-only
    foreach (var kvp in grouped)
        result[kvp.Key] = kvp.Value.AsReadOnly();
    
    return result;
}
```

**Key Details**:
- **`BindingFlags.Static`**: Operator overloads in C# are always static methods
- **`"op_"` prefix**: C# compiler convention for operator names
  - `+` → `op_Addition`
  - `==` → `op_Equality`
  - `[]` → `get_Item`/`set_Item` (not operators, different mechanism)
- **Grouping**: Handles operator overloading (e.g., `operator +(int, int)` vs `operator +(double, double)`)
- **AsReadOnly()**: Prevents callers from modifying the cached lists

---

### 3.3 `GetImplementedInterfaces(Type clrType)`

**Purpose**: Gets all interfaces a type implements, including inherited ones.

**Why It Matters**: Sharpy needs to check type compatibility. For example:
```python
def process(items: IEnumerable[str]) -> None:
    # Can we pass a List[str]? Need to check if List<T> implements IEnumerable<T>
```

```csharp
public IReadOnlySet<Type> GetImplementedInterfaces(Type clrType)
{
    if (_interfaceCache.TryGetValue(clrType, out var cached))
        return cached;
    
    var interfaces = new HashSet<Type>(clrType.GetInterfaces());
    _interfaceCache[clrType] = interfaces;
    return interfaces;
}
```

**Implementation Notes**:
- **`GetInterfaces()`**: .NET reflection method that returns ALL interfaces, including those inherited from base classes
- **HashSet**: Used for O(1) lookup when checking "does this type implement IFoo?"
- **Returns `IReadOnlySet`**: Callers can check membership but can't add/remove interfaces

**Example**:
```csharp
// For type List<string>:
// Returns: { IEnumerable<string>, ICollection<string>, IList<string>, 
//            IEnumerable, ICollection, IList, IReadOnlyList<string>, ... }
```

---

### 3.4 `ImplementsInterface(Type clrType, Type interfaceType)`

**Purpose**: Checks if a type implements a specific interface, with special handling for generics.

```csharp
public bool ImplementsInterface(Type clrType, Type interfaceType)
{
    var interfaces = GetImplementedInterfaces(clrType);
    
    if (interfaceType.IsGenericTypeDefinition)
    {
        // Check against generic definition (e.g., IEnumerable<>)
        return interfaces.Any(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == interfaceType);
    }
    
    return interfaces.Contains(interfaceType);
}
```

**The Generic Type Problem**:

When checking if `List<string>` implements `IEnumerable<T>`, we can't just check equality:
- `List<string>` implements `IEnumerable<string>` (concrete)
- We want to check against `IEnumerable<>` (generic definition)

The code handles this by:
1. Checking if we're looking for a generic definition (`IEnumerable<>`)
2. If so, comparing generic definitions: `IEnumerable<string>.GetGenericTypeDefinition()` → `IEnumerable<>`
3. Otherwise, doing a simple `Contains` check

**Example Use Cases**:
```csharp
// Check if List<int> implements IEnumerable<>
ImplementsInterface(typeof(List<int>), typeof(IEnumerable<>))  // true

// Check if List<int> implements IEnumerable<int>
ImplementsInterface(typeof(List<int>), typeof(IEnumerable<int>))  // true

// Check if List<int> implements IEnumerable<string>
ImplementsInterface(typeof(List<int>), typeof(IEnumerable<string>))  // false
```

---

### 3.5 `GetIndexerInfo(Type clrType)`

**Purpose**: Determines if a type has an indexer property and what type it returns.

**What's an Indexer?** In C#, this is the `this[]` property:
```csharp
class MyClass
{
    public string this[int index] => ...;  // Indexer
}
```

```csharp
public (bool HasIndexer, Type? ElementType) GetIndexerInfo(Type clrType)
{
    if (_indexerCache.TryGetValue(clrType, out var cached))
        return cached;
    
    // Look for default property (indexer)
    var defaultMembers = clrType.GetDefaultMembers();
    var indexer = defaultMembers.OfType<PropertyInfo>()
        .FirstOrDefault(p => p.GetIndexParameters().Length > 0);
    
    var result = indexer != null
        ? (true, indexer.PropertyType)
        : (false, (Type?)null);
    
    _indexerCache[clrType] = result;
    return result;
}
```

**Key Concepts**:
- **Default Members**: In .NET metadata, indexers are marked with the `[DefaultMember]` attribute
- **`GetIndexParameters()`**: Properties with parameters are indexers
- **Tuple Return**: Returns both "does it have one?" and "what type does it return?"

**Example**:
```csharp
// For List<int>:
GetIndexerInfo(typeof(List<int>))  // → (true, typeof(int))

// For string:
GetIndexerInfo(typeof(string))  // → (true, typeof(char))

// For int:
GetIndexerInfo(typeof(int))  // → (false, null)
```

**Sharpy Use Case**:
```python
lst: List[int] = [1, 2, 3]
item = lst[0]  # Compiler checks: does List<int> have an indexer? What type does it return?
```

---

### 3.6 `GetEnumerableElementType(Type clrType)`

**Purpose**: Extracts the element type from `IEnumerable<T>` implementations.

**Why It's Needed**: For `for` loops, the compiler needs to know what type of elements a collection yields:

```python
items: List[str] = ["a", "b", "c"]
for item in items:  # What's the type of 'item'? Need to extract T from IEnumerable<T>
    print(item)
```

```csharp
public Type? GetEnumerableElementType(Type clrType)
{
    if (_enumeratorCache.TryGetValue(clrType, out var cached))
        return cached;
    
    Type? elementType = null;
    
    // Check implemented interfaces for IEnumerable<T>
    var interfaces = GetImplementedInterfaces(clrType);
    var enumerableInterface = interfaces
        .FirstOrDefault(i => i.IsGenericType &&
                             i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
    
    if (enumerableInterface != null)
    {
        elementType = enumerableInterface.GetGenericArguments()[0];
    }
    // Check if type itself IS IEnumerable<T>
    else if (clrType.IsGenericType &&
             clrType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
    {
        elementType = clrType.GetGenericArguments()[0];
    }
    
    _enumeratorCache[clrType] = elementType;
    return elementType;
}
```

**Algorithm**:
1. **Check implemented interfaces**: Look for `IEnumerable<T>` among interfaces
2. **Extract generic argument**: If found, the `T` is the first generic argument
3. **Handle direct IEnumerable<T>**: If the type itself is `IEnumerable<T>`, extract `T` directly
4. **Return null if not enumerable**: Non-enumerable types return `null`

**Examples**:
```csharp
// List<string> implements IEnumerable<string>
GetEnumerableElementType(typeof(List<string>))  // → typeof(string)

// Array implements IEnumerable<T>
GetEnumerableElementType(typeof(int[]))  // → typeof(int)

// IEnumerable<double> itself
GetEnumerableElementType(typeof(IEnumerable<double>))  // → typeof(double)

// Non-enumerable type
GetEnumerableElementType(typeof(int))  // → null
```

---

## 4. Dependencies

### Internal Sharpy Dependencies

This class is **remarkably self-contained**. It doesn't depend on other Sharpy compiler components:

- ❌ No dependency on AST classes
- ❌ No dependency on Symbol Table
- ❌ No dependency on Type System classes
- ✅ Only depends on `System.Reflection`

**Why This Matters**: It makes the class easy to test in isolation and easy to understand without knowledge of the rest of the codebase.

### External .NET Dependencies

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
```

Key APIs used:
- **`Type.GetMethods()`**: Gets method metadata
- **`Type.GetInterfaces()`**: Gets implemented interfaces
- **`Type.GetDefaultMembers()`**: Gets members marked with `[DefaultMember]`
- **`Type.IsGenericType`**: Checks if type is generic
- **`Type.GetGenericTypeDefinition()`**: Gets the unbound generic (e.g., `List<>`)
- **`Type.GetGenericArguments()`**: Gets type parameters (e.g., `T` in `List<T>`)

### Where It's Used

`ClrMemberCache` is consumed by:
- **`TypeResolver`**: When resolving types from .NET assemblies
- **`TypeChecker`**: When validating operations on .NET types
- **`RoslynEmitter`**: Potentially when generating calls to .NET APIs

---

## 5. Patterns and Design Decisions

### 5.1 Cache-Aside Pattern

The classic **Cache-Aside** (or Lazy Loading) pattern:

```csharp
if (_cache.TryGetValue(key, out var cached))
    return cached;  // Cache hit

var value = ExpensiveOperation(key);  // Cache miss
_cache[key] = value;
return value;
```

**Benefits**:
- Only pays reflection cost once per type
- Memory usage grows only with types actually used
- Simple to implement and understand

### 5.2 Immutability of Return Values

All public methods return **read-only collections**:
- `IReadOnlyDictionary`
- `IReadOnlySet`
- `IReadOnlyList`
- Tuples with nullable types

**Why?** Prevents external code from corrupting the cache. If you returned a mutable `List`, callers could modify it and affect future lookups.

### 5.3 Explicit Thread-Safety Warning

The XML comment is clear:
```csharp
/// NOT thread-safe. Cache is populated lazily per-type; 
/// not safe for concurrent access.
```

**Why Not Thread-Safe?** 
- Compilation is single-threaded in Sharpy
- Adding locks would hurt performance for no benefit
- Simpler code without synchronization

**If You Need Thread-Safety**: Wrap in `ConcurrentDictionary` or use locks, but beware of performance impact.

### 5.4 Separation of Concerns

Each cache type has its own:
- Dictionary
- Public getter method
- Private discovery method (for operators)

This makes the class easy to extend. Want to cache constructors? Add:
```csharp
private readonly Dictionary<Type, List<ConstructorInfo>> _constructorCache = new();
public IReadOnlyList<ConstructorInfo> GetConstructors(Type clrType) { ... }
```

### 5.5 Null Handling with C# 8.0 Nullable References

Notice the `Type?` nullable annotations:
```csharp
(bool HasIndexer, Type? ElementType)  // ElementType can be null
public Type? GetEnumerableElementType(Type clrType)  // Can return null
```

This makes the API clear: **some lookups might not find anything**, and that's expected behavior.

---

## 6. Debugging Tips

### 6.1 Cache Miss Problems

**Symptom**: Slow compilation or excessive memory usage

**Debug Steps**:
1. Add logging to see cache hit/miss rates:
   ```csharp
   if (_operatorCache.TryGetValue(clrType, out var cached))
   {
       Console.WriteLine($"Cache HIT: {clrType.Name}");
       return cached;
   }
   Console.WriteLine($"Cache MISS: {clrType.Name}");
   ```

2. Check if the same type is being looked up repeatedly with different `Type` instances (shouldn't happen, but possible if type resolution is broken)

### 6.2 Missing Operators or Members

**Symptom**: Compiler says "operator not found" but you know the type has it

**Debug Steps**:
1. Verify the operator exists in .NET:
   ```csharp
   var methods = typeof(YourType).GetMethods(BindingFlags.Public | BindingFlags.Static);
   foreach (var m in methods)
       Console.WriteLine($"{m.Name}: {m}");
   ```

2. Check if it starts with `"op_"`:
   ```csharp
   methods.Where(m => m.Name.StartsWith("op_")).ToList()
   ```

3. Ensure you're testing with the right CLR type (not a Sharpy type wrapper)

### 6.3 Generic Type Confusion

**Symptom**: `ImplementsInterface` returns wrong results for generics

**Debug Steps**:
1. Print the interfaces:
   ```csharp
   var interfaces = GetImplementedInterfaces(clrType);
   foreach (var i in interfaces)
       Console.WriteLine($"{i.Name}, IsGeneric: {i.IsGenericType}");
   ```

2. Check generic definitions:
   ```csharp
   if (i.IsGenericType)
       Console.WriteLine($"  Definition: {i.GetGenericTypeDefinition().Name}");
   ```

3. Common mistake: Comparing `IEnumerable<string>` to `IEnumerable<>` without using `GetGenericTypeDefinition()`

### 6.4 Reflection Performance

**Symptom**: Compilation is slow even with caching

**Profile**:
- Use a profiler to see if reflection calls dominate
- Check if you're calling `GetOperatorMethods` on primitive types repeatedly (shouldn't be cached!)
- Consider warming the cache for common types at startup

### 6.5 Debugging Workflow

```csharp
// In your test or compiler driver:
var cache = new ClrMemberCache();

// Test operator discovery
var ops = cache.GetOperatorMethods(typeof(decimal));
Console.WriteLine($"Decimal has {ops.Count} operator groups");

// Test interface checking
bool isEnumerable = cache.ImplementsInterface(typeof(List<int>), typeof(IEnumerable<>));
Console.WriteLine($"List<int> is IEnumerable<>: {isEnumerable}");

// Test element type extraction
var elementType = cache.GetEnumerableElementType(typeof(int[]));
Console.WriteLine($"int[] element type: {elementType?.Name}");
```

---

## 7. Contribution Guidelines

### 7.1 When to Modify This File

**Add Caching For**:
- New member types needed by semantic analysis (e.g., constructors, events)
- New patterns of member lookup (e.g., extension methods)
- Performance-critical reflection operations

**Don't Add**:
- Business logic (e.g., type compatibility checking) — that belongs in `TypeChecker`
- Sharpy-specific type information — that belongs in `SymbolTable`
- Parsing or AST manipulation

### 7.2 How to Add a New Cache

Follow the existing pattern:

```csharp
// 1. Add private cache dictionary
private readonly Dictionary<Type, YourDataType> _yourCache = new();

// 2. Add public getter with cache-aside pattern
public YourDataType GetYourData(Type clrType)
{
    if (_yourCache.TryGetValue(clrType, out var cached))
        return cached;
    
    var data = DiscoverYourData(clrType);
    _yourCache[clrType] = data;
    return data;
}

// 3. Add private discovery method
private YourDataType DiscoverYourData(Type clrType)
{
    // Use reflection here
    // Return immutable result
}

// 4. Add XML documentation comments
// 5. Add tests in Sharpy.Compiler.Tests
```

### 7.3 Testing Guidelines

**Unit Test Checklist**:
- [ ] Test cache hit (second call returns same instance)
- [ ] Test cache miss (first call uses reflection)
- [ ] Test with generic types
- [ ] Test with non-generic types
- [ ] Test with types that don't have the member (null/empty results)
- [ ] Test with BCL types (`List<T>`, `string`, `int[]`)
- [ ] Test edge cases (empty interfaces, types without operators)

**Example Test**:
```csharp
[Fact]
public void GetOperatorMethods_CachesResults()
{
    var cache = new ClrMemberCache();
    
    var first = cache.GetOperatorMethods(typeof(decimal));
    var second = cache.GetOperatorMethods(typeof(decimal));
    
    Assert.Same(first, second);  // Should be same instance
}

[Fact]
public void GetOperatorMethods_FindsDecimalOperators()
{
    var cache = new ClrMemberCache();
    var ops = cache.GetOperatorMethods(typeof(decimal));
    
    Assert.True(ops.ContainsKey("op_Addition"));
    Assert.True(ops.ContainsKey("op_Subtraction"));
    Assert.True(ops.ContainsKey("op_Multiply"));
}
```

### 7.4 Performance Considerations

**When Adding Features**:
1. **Minimize allocations**: Reuse collections where possible
2. **Return read-only views**: Use `AsReadOnly()`, `IReadOnlyList`, etc.
3. **Avoid over-caching**: Don't cache data that's already fast to compute
4. **Consider memory growth**: Large caches can impact compilation memory footprint

**Benchmarking**:
```csharp
// Before optimization
var watch = Stopwatch.StartNew();
for (int i = 0; i < 1000; i++)
{
    var ops = cache.GetOperatorMethods(typeof(decimal));
}
watch.Stop();
Console.WriteLine($"1000 calls: {watch.ElapsedMilliseconds}ms");
// Should be ~0-1ms for cache hits
```

### 7.5 Code Style

Follow the existing conventions:
- Private fields prefixed with `_` and camelCase: `_operatorCache`
- Public methods in PascalCase: `GetOperatorMethods`
- Private methods in PascalCase: `DiscoverOperatorMethods`
- XML doc comments for public members
- Clear parameter names: `clrType` (not `type` — distinguishes from Sharpy types)

### 7.6 Common Pitfalls to Avoid

**❌ Don't Do This**:
```csharp
// Returning mutable collection
public Dictionary<string, List<MethodInfo>> GetOperatorMethods(Type clrType)
{
    return _operatorCache[clrType];  // BAD: Caller can modify cache
}

// Caching without thread-safety comment
private readonly ConcurrentDictionary<Type, object> _cache = new();
// BAD: Implies thread-safety but no documentation
```

**✅ Do This**:
```csharp
// Return read-only wrapper
public IReadOnlyDictionary<string, IReadOnlyList<MethodInfo>> GetOperatorMethods(Type clrType)
{
    // ...
}

// Document thread-safety explicitly
/// <summary>
/// Thread-safe cache using ConcurrentDictionary.
/// Safe for concurrent access during multi-threaded compilation.
/// </summary>
```

### 7.7 Integration Points

When modifying this file, consider impact on:

1. **TypeResolver** (`src/Sharpy.Compiler/Semantic/TypeResolver.cs`)
   - Uses `GetImplementedInterfaces` for interface checking
   - Update if interface resolution logic changes

2. **TypeChecker** (`src/Sharpy.Compiler/Semantic/TypeChecker.cs`)
   - Uses `GetOperatorMethods` for operator overload resolution
   - Uses `GetEnumerableElementType` for loop type inference

3. **RoslynEmitter** (`src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`)
   - May use operator info for code generation
   - Verify generated C# still compiles after cache changes

### 7.8 Documentation Updates

If you add a new cache type, update:
- [ ] This walkthrough document
- [ ] XML doc comments in the source file
- [ ] Architecture documentation (`docs/architecture/semantic-analyzer-architecture.md`)
- [ ] Add examples to integration tests

---

## Summary

`ClrMemberCache` is a focused, well-designed caching layer that makes .NET interop efficient in the Sharpy compiler. Its simplicity is its strength: no complex abstractions, clear cache-aside pattern, and strong separation of concerns. When working with this file:

1. **Understand the reflection APIs** it wraps
2. **Follow the existing patterns** for consistency
3. **Test thoroughly**, especially with generics
4. **Document your changes** clearly
5. **Profile if you change performance characteristics**

The code is a great example of a single-purpose utility class done right. Learn from its patterns when writing your own caching or utility classes!

---

**Last Updated**: 2025-12-27  
**Maintainer**: Sharpy Compiler Team  
**Related Documentation**: 
- `docs/architecture/semantic-analyzer-architecture.md`
- `.github/instructions/Sharpy.Compiler/HOW_TO_CONTRIBUTE.instructions.md`
