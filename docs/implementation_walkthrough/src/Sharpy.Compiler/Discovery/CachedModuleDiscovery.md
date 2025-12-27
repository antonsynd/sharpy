# Walkthrough: CachedModuleDiscovery.cs

**Source File**: `src/Sharpy.Compiler/Discovery/CachedModuleDiscovery.cs`

---

## 1. Overview

`CachedModuleDiscovery` is the **high-level orchestrator** for discovering and caching .NET assembly metadata in the Sharpy compiler. When Sharpy code imports a .NET library (like `System.Collections` or `Sharpy.Core`), this class efficiently discovers all public functions available in that assembly.

**The Performance Problem It Solves:**

Using reflection to scan assemblies for functions is expensive—it can add hundreds of milliseconds to compilation time. `CachedModuleDiscovery` solves this by:
1. **First load**: Uses reflection to discover functions, then saves the results to disk
2. **Subsequent loads**: Reads pre-computed metadata from cache (typically 10-100x faster)
3. **Thread-safe**: Multiple compilation units can safely load assemblies concurrently

**Where It Fits in the Compiler Pipeline:**

```
Sharpy Source → Parser → Semantic Analysis
                              ↓
                   (imports a .NET module)
                              ↓
                   CachedModuleDiscovery ← You are here
                              ↓
                   Returns FunctionSymbols
                              ↓
                   Type Checker validates calls
```

---

## 2. Class Structure

### Main Class: `CachedModuleDiscovery`

This is a **thread-safe service class** with four key collaborators:

```csharp
public class CachedModuleDiscovery
{
    private readonly OverloadIndexCache _cache;          // Disk persistence
    private readonly OverloadIndexBuilder _builder;      // Reflection scanner
    private readonly TypeMapper _typeMapper;             // CLR→Sharpy type conversion
    private readonly ConcurrentDictionary<string, OverloadIndex> _loadedIndices;
                                                         // In-memory registry
}
```

**Design Pattern**: This class follows the **Facade pattern**—it provides a simple interface (`LoadAssembly`, `GetModuleFunctions`) while hiding the complexity of caching, reflection, and type conversion.

---

## 3. Key Methods

### 3.1 Constructor

```csharp
public CachedModuleDiscovery(OverloadIndexCache? cache)
{
    _cache = cache ?? new OverloadIndexCache();
    _builder = new OverloadIndexBuilder();
    _typeMapper = new TypeMapper();
}
```

**What it does:**
- Initializes the caching infrastructure
- If no custom cache is provided, uses the default cache directory (`~/.sharpy/cache` or similar)
- Creates the builder (reflection engine) and type mapper

**When to use custom cache:**
- Testing: provide a mock cache to avoid disk I/O
- Custom build systems: specify a shared cache directory

---

### 3.2 `LoadAssembly` - The Core Workflow

```csharp
public void LoadAssembly(Assembly assembly)
{
    var identity = AssemblyIdentity.FromAssembly(assembly);

    _loadedIndices.GetOrAdd(identity.Name, _ =>
    {
        var index = _cache.TryLoad(identity);  // 1. Try cache first
        
        if (index == null)
        {
            index = _builder.BuildFromAssembly(assembly);  // 2. Cache miss
            _cache.Save(index);                            // 3. Persist
        }
        
        return index;
    });
}
```

**Step-by-step breakdown:**

1. **Create assembly identity**: `AssemblyIdentity` captures name, version, and public key token—this uniquely identifies the assembly and serves as the cache key

2. **Thread-safe loading with `GetOrAdd`**:
   - `ConcurrentDictionary.GetOrAdd` ensures only ONE thread builds/loads a given assembly
   - Other threads wait and receive the same `OverloadIndex` instance
   - This prevents duplicate work in multi-threaded builds

3. **Cache lookup** (`_cache.TryLoad`):
   - Checks disk for a cached JSON file like `System.Collections.Generic-4.0.0.0.json`
   - Validates the cache format version matches current compiler
   - Returns `null` on cache miss or version mismatch

4. **Reflection fallback** (`_builder.BuildFromAssembly`):
   - Uses reflection to scan all public static methods in the assembly
   - Groups them by module name (usually the namespace)
   - Converts CLR types to Sharpy-friendly `TypeSignature` objects

5. **Persist to cache** (`_cache.Save`):
   - Serializes the `OverloadIndex` to JSON
   - Saves to `~/.sharpy/cache/assemblies/{AssemblyName}.json`
   - Future compilations skip steps 4-5

**Why the `_` parameter?**
```csharp
_loadedIndices.GetOrAdd(identity.Name, _ => { ... });
```
The underscore is a convention for "unused parameter." `GetOrAdd` passes the key (`identity.Name`) to the factory function, but we already have `identity` in scope, so we ignore it.

---

### 3.3 `GetModuleFunctions` - Querying Cached Data

```csharp
public List<FunctionSymbol> GetModuleFunctions(string moduleName)
{
    var functions = new List<FunctionSymbol>();

    foreach (var index in _loadedIndices.Values)  // 1. Search all assemblies
    {
        if (!index.Modules.TryGetValue(moduleName, out var moduleOverloads))
            continue;  // This assembly doesn't have this module

        foreach (var (functionName, signatures) in moduleOverloads.Functions)
        {
            foreach (var signature in signatures)  // 2. Handle overloads
            {
                functions.Add(ConvertToFunctionSymbol(signature, moduleName));
            }
        }
    }

    return functions;
}
```

**What it does:**
- Given a module name (e.g., `"System.Linq"`), returns all functions available
- Searches across ALL loaded assemblies (since multiple assemblies can contribute to the same namespace)
- Converts cached `FunctionSignature` objects back to live `FunctionSymbol` instances

**Example usage in the compiler:**
```csharp
// When Sharpy code does: from System.Linq import *
var linqFunctions = discovery.GetModuleFunctions("System.Linq");
symbolTable.AddFunctions(linqFunctions);
```

**Why iterate all indices?**

In .NET, namespaces can span multiple assemblies:
- `System.Linq` exists in `System.Linq.dll`
- But also `System.Linq.Expressions.dll`
- Both contribute functions to the `System.Linq` namespace

---

### 3.4 `ConvertToFunctionSymbol` - Rehydrating Cached Data

```csharp
private FunctionSymbol ConvertToFunctionSymbol(FunctionSignature signature, string moduleName)
{
    return new FunctionSymbol
    {
        Name = signature.Name,
        Kind = SymbolKind.Function,
        ReturnType = ConvertTypeSignature(signature.ReturnType),
        Parameters = signature.Parameters
            .Select(p => new ParameterSymbol
            {
                Name = p.Name,
                Type = ConvertTypeSignature(p.Type),
                HasDefault = p.HasDefault,
                DefaultValue = null  // TODO: Reconstruct from cached string
            })
            .ToList(),
        AccessLevel = AccessLevel.Public
    };
}
```

**The serialization challenge:**

`FunctionSymbol` contains rich .NET objects like `Expression` (for default values). These can't be trivially serialized to JSON. The caching layer handles this with a **lossy conversion**:

| Live Object | Cached Representation | Recovery Strategy |
|-------------|----------------------|-------------------|
| `SemanticType` | `TypeSignature` (JSON) | `ConvertTypeSignature` |
| `Expression` (default value) | `string?` | Currently `null`—**TODO** |
| Method reference | `MethodToken` string | Re-resolve via reflection if needed |

**Note the TODO comment**: Default parameter values aren't fully restored from cache. This is acceptable because:
- Most imported functions don't have default parameters
- The type checker can still validate calls
- Code generation can query the actual MethodInfo if needed

---

### 3.5 `ConvertTypeSignature` - The Type Mapping Brain

This is the **most complex method** in the file—it converts serialized type metadata back to Sharpy's semantic type system.

```csharp
private SemanticType ConvertTypeSignature(TypeSignature signature)
{
    // Fast path: primitives
    if (signature.Name == "int") return SemanticType.Int;
    if (signature.Name == "str") return SemanticType.Str;
    // ... more primitives ...

    // Generic types: List<T>, Dict<K,V>, etc.
    if (signature.IsGeneric)
    {
        return new GenericType
        {
            Name = signature.Name[..signature.Name.IndexOf('[')],
            TypeArguments = signature.TypeArguments
                .Select(ConvertTypeSignature)  // Recursive!
                .ToList()
        };
    }

    // CLR types: RangeIterator, custom .NET types
    if (!string.IsNullOrEmpty(signature.ClrTypeName))
    {
        var clrType = Type.GetType(signature.ClrTypeName)
            ?? AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType(signature.ClrTypeName))
                .FirstOrDefault(t => t != null);

        if (clrType != null)
        {
            return new BuiltinType { Name = signature.Name, ClrType = clrType };
        }
    }

    return SemanticType.Object;  // Fallback
}
```

**Three-tier type resolution:**

1. **Primitive fast path**: Direct lookups for common types (`int`, `str`, `bool`)
   - No allocation, no reflection
   - 90% of types hit this path

2. **Generic types**: Handles `List<int>`, `Dict<str, float>`, etc.
   - Recursively converts type arguments
   - Extracts base name from serialized form like `"List[int]"`

3. **CLR types**: Complex .NET types like `RangeIterator` or custom types
   - Uses `ClrTypeName` (assembly-qualified name) to resolve via reflection
   - Falls back to searching all loaded assemblies if `Type.GetType` fails
   - **Performance note**: This is slow but rare (only for exotic types)

**Edge case handling:**

```csharp
Name = signature.Name.Contains('[')
    ? signature.Name[..signature.Name.IndexOf('[')]
    : signature.Name
```

Why the conditional? The cached `Name` might be:
- Simple: `"List"`
- Formatted: `"List[int]"` (includes type args for readability)

The code defensively extracts just the base name.

---

### 3.6 `GetLoadedModules` - Discovery Metadata

```csharp
public IEnumerable<string> GetLoadedModules()
{
    return _loadedIndices.Values
        .SelectMany(index => index.Modules.Keys)
        .Distinct();
}
```

**Purpose**: Returns all available module names (namespaces) across loaded assemblies.

**Use case**: Implementing autocomplete in an IDE:
```csharp
var modules = discovery.GetLoadedModules();
// Suggests: "System.Linq", "System.Collections", "Sharpy.Core", ...
```

---

### 3.7 `ClearCache` - Maintenance Operation

```csharp
public void ClearCache()
{
    _cache.ClearAll();
}
```

**When to use:**
- .NET SDK version changed → cached metadata might be stale
- Compiler updated with new caching logic
- Debugging cache-related issues

**Note**: This only clears disk cache, not `_loadedIndices`. In-memory data persists until the `CachedModuleDiscovery` instance is disposed.

---

## 4. Dependencies

### Internal Dependencies (Sharpy Compiler)

| Dependency | Purpose | Location |
|------------|---------|----------|
| `OverloadIndexCache` | Disk serialization (JSON) | `Discovery/Caching/` |
| `OverloadIndexBuilder` | Reflection-based scanning | `Discovery/Caching/` |
| `OverloadIndex` | Data model for cached metadata | `Discovery/Caching/` |
| `TypeMapper` | CLR → Sharpy type conversion | `Discovery/` |
| `FunctionSymbol` | Compiler's function representation | `Semantic/Symbol.cs` |
| `SemanticType` | Sharpy's type system | `Semantic/SemanticType.cs` |

### External Dependencies (.NET BCL)

- `System.Reflection.Assembly` - Introspection of .NET assemblies
- `System.Collections.Concurrent.ConcurrentDictionary` - Thread-safe storage
- `System.Linq` - LINQ queries for collection transformations

---

## 5. Patterns and Design Decisions

### 5.1 Thread Safety Strategy

**Pattern**: Optimistic concurrency with `ConcurrentDictionary`

```csharp
_loadedIndices.GetOrAdd(identity.Name, _ => { ... });
```

**Why this works:**
- `GetOrAdd` is atomic—only one factory function executes per key
- Reflection/disk I/O happens once per assembly, even with 100 concurrent threads
- No locks needed! The dictionary handles synchronization

**Alternative considered**: Manual locking
```csharp
lock (_lock)
{
    if (!_loadedIndices.ContainsKey(key))
        _loadedIndices[key] = Build();
}
```
**Rejected because**: Slower, error-prone, doesn't scale as well.

---

### 5.2 Immutability of Cached Data

**Decision**: `OverloadIndex` and related types are mutable POCOs, not immutable records.

**Rationale:**
- JSON deserialization requires settable properties
- Performance: No allocations for `with` expressions
- Cache data is **read-only after creation**—mutation isn't a concern

**Trade-off**: Less safety, more performance. Acceptable for internal infrastructure.

---

### 5.3 Separation of Concerns

The class delegates specialized work:

| Concern | Handler |
|---------|---------|
| Reflection | `OverloadIndexBuilder` |
| Disk I/O | `OverloadIndexCache` |
| Type conversion | `TypeMapper` + `ConvertTypeSignature` |
| Thread safety | `ConcurrentDictionary` |

**Benefit**: Each class is testable in isolation. Easy to replace implementations (e.g., mock cache for testing).

---

### 5.4 The "Signature" vs "Symbol" Dichotomy

**Two parallel type hierarchies:**

1. **Signatures** (`FunctionSignature`, `TypeSignature`):
   - Serializable POCOs (JSON-friendly)
   - Store **names and strings**, not live .NET objects
   - Lightweight for caching

2. **Symbols** (`FunctionSymbol`, `SemanticType`):
   - Rich compiler objects
   - May contain `Expression` trees, `Type` instances, scope info
   - Not serializable

**The bridge**: `ConvertToFunctionSymbol` and `ConvertTypeSignature` convert between these worlds.

---

## 6. Debugging Tips

### 6.1 Cache Miss Debugging

**Symptom**: Compilation is slow; assembly always being rescanned.

**Check:**
```csharp
var index = _cache.TryLoad(identity);
if (index == null)
{
    Console.WriteLine($"Cache miss for {identity.Name}");
    // Add logging here to see why
}
```

**Common causes:**
- Cache format version mismatch (compiler was upgraded)
- File permissions issue (cache directory not writable)
- Assembly version changed (e.g., `System.Collections.4.0.0.0` → `4.1.0.0`)

**Quick fix**: Clear cache and rebuild
```bash
rm -rf ~/.sharpy/cache/assemblies/*
```

---

### 6.2 Type Conversion Failures

**Symptom**: Compilation error like "Cannot convert System.Linq.IEnumerable to Sharpy type"

**Debug strategy:**
1. Add logging in `ConvertTypeSignature`:
   ```csharp
   Console.WriteLine($"Converting: {signature.Name}, Generic={signature.IsGeneric}, ClrTypeName={signature.ClrTypeName}");
   ```

2. Check if type is falling through to `SemanticType.Object` fallback

3. Verify `TypeMapper` is generating correct `TypeSignature` during cache build

**Common issue**: Generic types with nested generics
- Example: `List<Dict<str, int>>`
- Ensure recursive `TypeArguments` are fully populated

---

### 6.3 Concurrent Loading Issues

**Symptom**: Rare exceptions or cache corruption under load.

**Verify thread safety:**
```csharp
// Add this to LoadAssembly to detect reentrancy bugs
var threadId = Environment.CurrentManagedThreadId;
Console.WriteLine($"[Thread {threadId}] Loading {assembly.FullName}");
```

**Known safe scenario**: Multiple threads loading **different** assemblies
**Potential issue**: Same assembly loaded from different file paths (symlinks, etc.)

---

### 6.4 Memory Leaks

**Watch for**: `_loadedIndices` growing unbounded in long-running processes (e.g., LSP server)

**Mitigation**: Add cache eviction if needed:
```csharp
if (_loadedIndices.Count > 1000)
{
    // Evict least recently used
}
```

Currently not implemented—assumes bounded number of assemblies per compilation.

---

## 7. Contribution Guidelines

### 7.1 Easy Wins

**Fix the TODO: Restore default parameter values**

Currently:
```csharp
DefaultValue = null  // TODO: Reconstruct from cached string
```

**Approach:**
1. Update `ParameterSignature` to store default value as JSON
2. Deserialize in `ConvertToFunctionSymbol`
3. Convert to appropriate `Expression` type (literal, constant, etc.)

**Test with**: C# methods like `void Foo(int x = 42)`

---

### 7.2 Performance Improvements

**Optimize CLR type resolution**

Current code searches all assemblies:
```csharp
AppDomain.CurrentDomain.GetAssemblies()
    .Select(a => a.GetType(signature.ClrTypeName))
    .FirstOrDefault(t => t != null);
```

**Improvement**: Cache assembly → types mapping
```csharp
private readonly ConcurrentDictionary<string, Type> _typeCache = new();
```

**Benchmark first**: Is this actually a bottleneck? Use BenchmarkDotNet.

---

### 7.3 Robustness Enhancements

**Add cache validation**

Before deserializing, verify:
- File is valid JSON
- `CacheFormatVersion` matches
- `AssemblyIdentity` matches expected

**Current behavior**: Silently treats bad cache as miss (rebuilds)
**Better behavior**: Log warning to help diagnose cache corruption

---

### 7.4 Testing Additions

**Missing test scenarios:**
- Concurrent loading of same assembly from multiple threads
- Cache invalidation when assembly version changes
- Generic types with complex nesting (`List<Dict<Tuple<int, str>, Set<float>>>`)

**Test structure:**
```csharp
[Fact]
public void LoadAssembly_Concurrent_LoadsOnlyOnce()
{
    var discovery = new CachedModuleDiscovery();
    var assembly = typeof(List<>).Assembly;
    
    Parallel.For(0, 10, _ => discovery.LoadAssembly(assembly));
    
    // Verify only one index exists
    Assert.Single(discovery.GetLoadedModules().Where(m => m.StartsWith("System.Collections")));
}
```

---

### 7.5 Architecture Evolution

**Potential future direction: Incremental caching**

Current limitation: Cache entire assembly or nothing
Better: Cache per-module (namespace) granularity

**Benefits:**
- Faster cache updates when assembly changes
- Lower memory footprint for large assemblies

**Trade-offs:**
- More complex cache management
- Need to track module → assembly mappings

---

## 8. Related Files

When working on `CachedModuleDiscovery`, you'll often touch:

| File | Relationship |
|------|-------------|
| `Discovery/Caching/OverloadIndexCache.cs` | Storage layer—handles JSON serialization and file I/O |
| `Discovery/Caching/OverloadIndexBuilder.cs` | Reflection engine—discovers functions from assemblies |
| `Discovery/Caching/OverloadIndex.cs` | Data models—defines cache format |
| `Discovery/TypeMapper.cs` | Type conversion—maps CLR types to Sharpy types |
| `Semantic/Symbol.cs` | Target format—defines `FunctionSymbol` and `ParameterSymbol` |
| `Semantic/SemanticType.cs` | Type system—defines Sharpy's type hierarchy |

---

## 9. Quick Reference

### Common Operations

**Load an assembly:**
```csharp
var discovery = new CachedModuleDiscovery();
var assembly = Assembly.LoadFrom("MyLibrary.dll");
discovery.LoadAssembly(assembly);
```

**Query functions:**
```csharp
var functions = discovery.GetModuleFunctions("MyLibrary.Core");
foreach (var func in functions)
{
    Console.WriteLine($"{func.Name}: {func.ReturnType}");
}
```

**Clear cache:**
```csharp
discovery.ClearCache();
```

**Custom cache directory:**
```csharp
var cache = new OverloadIndexCache("/path/to/cache");
var discovery = new CachedModuleDiscovery(cache);
```

---

## 10. Final Thoughts

`CachedModuleDiscovery` is a **critical performance component** that makes Sharpy compilation fast enough for interactive development. Its design balances:
- **Performance**: Aggressive caching eliminates repeated reflection
- **Correctness**: Thread-safe, handles edge cases gracefully
- **Simplicity**: Clean API hides caching complexity

When debugging, remember: **cache is an optimization, not correctness**. You can always delete the cache and rely on the reflection path to verify behavior.

**Key insight**: The entire `Discovery/` subsystem exists because .NET's reflection API is too slow for real-time compilation. This is a common pattern in compiler design—use expensive discovery once, cache aggressively, serve fast.
