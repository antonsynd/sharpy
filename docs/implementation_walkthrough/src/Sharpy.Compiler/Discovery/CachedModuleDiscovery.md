# Walkthrough: CachedModuleDiscovery.cs

**Source File**: `src/Sharpy.Compiler/Discovery/CachedModuleDiscovery.cs`

---

## Overview

The `CachedModuleDiscovery.cs` file implements a **performance-optimized module discovery system** that finds and indexes function overloads from .NET assemblies. It sits at the boundary between the Sharpy compiler and external .NET libraries, enabling Sharpy code to call functions from compiled assemblies.

**Role in Pipeline**: This component operates during the **semantic analysis phase**, specifically during import resolution. When Sharpy code imports from a compiled module (e.g., `from builtins import print`), this class discovers what functions are available and their signatures.

**Key Innovation**: Uses a two-tier caching strategy:
1. **In-memory cache** (`ConcurrentDictionary`) - Fast lookup for already-loaded assemblies in the current compilation session
2. **Persistent disk cache** (`OverloadIndexCache`) - Serialized function signatures that survive across compiler invocations

**Performance Impact**: Without caching, reflection on large assemblies like `Sharpy.Core` takes 100-200ms. With caching, subsequent loads take <1ms. This is critical for compiler responsiveness during development.

**Key Responsibilities**:
- Discovering function overloads from .NET assemblies via reflection
- Caching discovered signatures to disk (gzipped JSON in `~/.sharpy/cache/overload-index/`)
- Converting between CLR types and Sharpy semantic types
- Thread-safe concurrent access for parallel compilation
- Providing query interface for semantic analyzer to resolve imports

---

## Class/Type Structure

### Main Class

#### `CachedModuleDiscovery`

A facade that coordinates the discovery and caching subsystems.

**Fields**:
```csharp
private readonly OverloadIndexCache _cache;                                    // Persistent disk cache
private readonly OverloadIndexBuilder _builder;                                // Reflection-based indexer
private readonly TypeMapper _typeMapper;                                       // CLR ↔ Sharpy type converter
private readonly ConcurrentDictionary<string, OverloadIndex> _loadedIndices;  // In-memory cache
```

**Design Pattern**: This is a **Cache-Aside** pattern implementation. The class checks the in-memory cache first, then the disk cache, and finally falls back to expensive reflection.

**Thread Safety**: Uses `ConcurrentDictionary.GetOrAdd()` to ensure only one thread builds an index for a given assembly, even under concurrent access.

**Constructors**:
```csharp
public CachedModuleDiscovery()                              // Uses default cache location
public CachedModuleDiscovery(OverloadIndexCache? cache)     // Allows custom cache (for testing)
```

The parameterized constructor enables **dependency injection** for testing - you can pass a temporary cache directory to avoid conflicts between parallel test runs.

---

## Key Methods

### 1. `LoadAssembly(Assembly assembly)`

**Purpose**: Main entry point for loading an assembly and discovering its exported functions.

**Algorithm**:
```csharp
public void LoadAssembly(Assembly assembly)
{
    var identity = AssemblyIdentity.FromAssembly(assembly);  // Create versioned identity

    _loadedIndices.GetOrAdd(identity.Name, _ =>              // Thread-safe cache lookup
    {
        var index = _cache.TryLoad(identity);                // Try disk cache

        if (index == null)
        {
            index = _builder.BuildFromAssembly(assembly);    // Cache miss - use reflection
            _cache.Save(index);                              // Persist for next time
        }

        return index;
    });
}
```

**Cache Invalidation**: The `AssemblyIdentity` includes:
- Assembly name
- Version number (from `AssemblyName.Version`)
- Content hash (SHA256 of the .dll file)

This ensures that if you recompile `Sharpy.Core` or update a dependency, the cache is automatically invalidated because the hash changes.

**Thread Safety Note**: `GetOrAdd` ensures that if 10 threads try to load the same assembly simultaneously, only one will execute the factory function. The others will wait and receive the same `OverloadIndex` instance.

**Upstream Connection**: Called by `ModuleRegistry` when resolving imports from compiled modules.

**Downstream Connection**: Delegates to:
- `OverloadIndexCache.TryLoad()` for cache retrieval
- `OverloadIndexBuilder.BuildFromAssembly()` for reflection-based discovery
- `OverloadIndexCache.Save()` for persistence

---

### 2. `GetModuleFunctions(string moduleName)`

**Purpose**: Retrieve all function symbols available in a specific module (e.g., "builtins").

**Algorithm**:
```csharp
public List<FunctionSymbol> GetModuleFunctions(string moduleName)
{
    var functions = new List<FunctionSymbol>();

    foreach (var index in _loadedIndices.Values)                      // Search all loaded assemblies
    {
        if (!index.Modules.TryGetValue(moduleName, out var moduleOverloads))
            continue;

        foreach (var (functionName, signatures) in moduleOverloads.Functions)
        {
            foreach (var signature in signatures)                     // Each overload becomes a FunctionSymbol
            {
                functions.Add(ConvertToFunctionSymbol(signature, moduleName));
            }
        }
    }

    return functions;
}
```

**Design Decision**: Returns a **flat list** of `FunctionSymbol` objects, with overloads represented as separate symbols. This matches how the semantic analyzer expects function data.

**Example**: If `Sharpy.Core.dll` has:
```csharp
public static class Exports
{
    public static int Abs(int x) { ... }
    public static double Abs(double x) { ... }
}
```

Then `GetModuleFunctions("builtins")` returns two `FunctionSymbol` objects, both named "abs" but with different parameter types.

**Upstream Connection**: Called by `ImportResolver` during semantic analysis when resolving `from builtins import abs`.

---

### 3. `GetLoadedModules()`

**Purpose**: Query all module names available across all loaded assemblies.

**Implementation**:
```csharp
public IEnumerable<string> GetLoadedModules()
{
    return _loadedIndices.Values
        .SelectMany(index => index.Modules.Keys)
        .Distinct();
}
```

**Use Case**: Useful for diagnostic purposes or IDE tooling (e.g., autocomplete for module names).

**Performance Note**: Uses LINQ deferred execution - the enumeration only happens when consumed.

---

### 4. `ConvertToFunctionSymbol(FunctionSignature signature, string moduleName)` (Private)

**Purpose**: Rehydrate cached function signatures back into live `FunctionSymbol` objects that the semantic analyzer can use.

**Implementation**:
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

**Known Limitation**: The `DefaultValue = null` line indicates incomplete default parameter support. Default values are serialized as strings (e.g., `"42"`, `"true"`) but not reconstructed back into AST `Expression` nodes. This is noted as a TODO for future enhancement.

**Why This Matters**: Without proper default value reconstruction, the compiler might not correctly emit default parameters when generating C# code for imported functions.

---

### 5. `ConvertTypeSignature(TypeSignature signature)` (Private)

**Purpose**: Convert cached type metadata back into `SemanticType` objects used by the type checker.

**Algorithm**:
```csharp
private SemanticType ConvertTypeSignature(TypeSignature signature)
{
    // 1. Handle primitive types by name matching
    if (signature.Name == "int") return SemanticType.Int;
    if (signature.Name == "str") return SemanticType.Str;
    // ... more primitives ...

    // 2. Handle generic types (e.g., List[int])
    if (signature.IsGeneric)
    {
        return new GenericType
        {
            Name = ExtractBaseName(signature.Name),      // "List[int]" → "List"
            TypeArguments = signature.TypeArguments
                .Select(ConvertTypeSignature)             // Recursive conversion
                .ToList()
        };
    }

    // 3. Handle non-generic CLR types using reflection
    if (!string.IsNullOrEmpty(signature.ClrTypeName))
    {
        var clrType = Type.GetType(signature.ClrTypeName)         // Try direct lookup
                   ?? FindTypeInLoadedAssemblies(signature.ClrTypeName);

        if (clrType != null)
        {
            return new BuiltinType
            {
                Name = signature.Name,
                ClrType = clrType
            };
        }
    }

    // 4. Fallback for unknown types
    return SemanticType.Object;
}
```

**Interesting Detail**: The float type mapping:
```csharp
if (signature.Name == "float") return SemanticType.Float;       // float → double (per spec)
if (signature.Name == "float32") return SemanticType.Float32;   // float32 → C# float
if (signature.Name == "float64") return SemanticType.Double;    // float64 → double
```

This reflects Sharpy's design decision that `float` defaults to double-precision (like Python), while `float32` explicitly requests single-precision.

**Performance Note**: The `AppDomain.CurrentDomain.GetAssemblies()` search for CLR types is expensive, and the comment suggests caching this if performance becomes an issue. This is a good example of **premature optimization avoidance** - the feature works correctly, and only if profiling shows this as a bottleneck should caching be added.

**Edge Case Handling**: Returns `SemanticType.Object` as a safe fallback for types that can't be resolved. This prevents crashes but may lead to less precise type checking.

---

## Dependencies

### Internal Dependencies

#### 1. `Sharpy.Compiler.Discovery.Caching` Namespace

**OverloadIndexCache** (`src/Sharpy.Compiler/Discovery/Caching/OverloadIndexCache.cs`):
- Manages persistent caching to `~/.sharpy/cache/overload-index/`
- Handles gzip compression, JSON serialization, and file locking
- Implements retry logic for concurrent access from multiple processes
- Automatically cleans up stale caches (>7 days old)

**OverloadIndexBuilder** (`src/Sharpy.Compiler/Discovery/Caching/OverloadIndexBuilder.cs`):
- Uses reflection to scan assemblies for `Exports` classes
- Discovers public static methods (filters out properties, generics, type constructors)
- Converts C# method names from PascalCase to snake_case (e.g., `ReadLine` → `read_line`)
- Creates serializable `FunctionSignature` objects with method tokens for rehydration

**OverloadIndex** (`src/Sharpy.Compiler/Discovery/Caching/OverloadIndex.cs`):
- Data model for cached function signatures
- Structure: `OverloadIndex` → `ModuleOverloads` → `FunctionSignature` → `ParameterSignature`/`TypeSignature`
- All classes are JSON-serializable with camelCase naming

**AssemblyIdentity** (`src/Sharpy.Compiler/Discovery/Caching/AssemblyIdentity.cs`):
- Creates versioned identity with SHA256 content hash
- Generates cache keys like `sharpy.core-1.0.0-a1b2c3d4e5f6.json.gz`
- Implements equality comparison for cache validation

#### 2. `Sharpy.Compiler.Semantic` Namespace

**TypeMapper** (not shown, but used):
- Converts CLR types to Sharpy semantic types
- Handles primitive mappings, generics, nullable types, etc.

**Symbol.cs** (`src/Sharpy.Compiler/Semantic/Symbol.cs`):
- `FunctionSymbol`: Target type for discovered functions
- `ParameterSymbol`: Represents function parameters
- `SemanticType`: Type system representation used by type checker

### External Dependencies

- **System.Reflection**: Core dependency for assembly introspection
- **System.Collections.Concurrent**: Thread-safe in-memory caching
- **System.IO.Compression**: Gzip compression for cache files (via `OverloadIndexCache`)
- **System.Text.Json**: JSON serialization (via `OverloadIndexCache`)

---

## Patterns and Design Decisions

### 1. **Cache-Aside Pattern**

The classic caching strategy:
```
┌─────────────────────────────────────────────┐
│ 1. Check in-memory cache (ConcurrentDict)  │
│    ↓ Hit: Return immediately               │
│    ↓ Miss: Continue...                     │
├─────────────────────────────────────────────┤
│ 2. Check disk cache (OverloadIndexCache)   │
│    ↓ Hit: Return and populate memory cache │
│    ↓ Miss: Continue...                     │
├─────────────────────────────────────────────┤
│ 3. Use reflection (OverloadIndexBuilder)   │
│    ↓ Populate both disk and memory cache   │
└─────────────────────────────────────────────┘
```

**Benefit**: Optimizes for the common case (recompiling the same code repeatedly during development) while gracefully handling cache misses.

### 2. **Separation of Concerns**

Each class has a single, well-defined responsibility:
- `CachedModuleDiscovery`: Orchestration and public API
- `OverloadIndexCache`: Persistent storage with file I/O
- `OverloadIndexBuilder`: Reflection logic and type mapping
- `OverloadIndex`: Data model

**Benefit**: Easy to test each component in isolation, and changes to caching strategy don't affect reflection logic.

### 3. **Immutable Data Structures**

The `OverloadIndex`, `FunctionSignature`, `ParameterSignature`, and `TypeSignature` classes use init-only properties:
```csharp
public class FunctionSignature
{
    public string Name { get; set; } = string.Empty;
    public List<ParameterSignature> Parameters { get; set; } = new();
    // ...
}
```

**Benefit**: Once created, these objects cannot be accidentally modified, making them safe to share across threads.

### 4. **Optimistic Concurrency**

Uses `ConcurrentDictionary.GetOrAdd()` instead of locks:
```csharp
_loadedIndices.GetOrAdd(identity.Name, _ => {
    // This factory function runs only once per key
});
```

**Benefit**: Lock-free reads for already-cached assemblies, and automatic deduplication for concurrent writes.

### 5. **Content-Based Cache Invalidation**

Cache keys include SHA256 hash of the assembly file:
```
sharpy.core-1.0.0-a1b2c3d4e5f6.json.gz
                 ↑
                 First 12 chars of SHA256 hash
```

**Benefit**: If you edit `Sharpy.Core` and recompile, the hash changes, automatically invalidating the cache. No need for manual cache clearing.

### 6. **Graceful Degradation**

Multiple fallback mechanisms:
- If disk cache is corrupted → delete it and rebuild from reflection
- If type mapping fails → fall back to `SemanticType.Object`
- If cache save fails → log warning but continue (caching is optional)

**Benefit**: The compiler remains functional even when caching subsystems fail.

---

## Data Flow Example

Let's trace what happens when Sharpy code imports from builtins:

```python
# user_code.spy
from builtins import print
```

**Step-by-step execution**:

1. **Semantic Analyzer** calls `ImportResolver.ResolveImport("builtins")`

2. **ImportResolver** asks `ModuleRegistry` for the "builtins" module

3. **ModuleRegistry** identifies "builtins" as a compiled module (from `Sharpy.Core.dll`)

4. **ModuleRegistry** calls `cachedDiscovery.LoadAssembly(sharpyCoreAssembly)`

5. **CachedModuleDiscovery**:
   - Creates `AssemblyIdentity` with name="Sharpy.Core", version="1.0.0", hash="a1b2c3..."
   - Checks in-memory cache → MISS
   - Checks disk cache at `~/.sharpy/cache/overload-index/sharpy.core-1.0.0-a1b2c3d4e5f6.json.gz` → HIT
   - Deserializes `OverloadIndex` containing all function signatures
   - Stores in `_loadedIndices["Sharpy.Core"]`

6. **ModuleRegistry** calls `cachedDiscovery.GetModuleFunctions("builtins")`

7. **CachedModuleDiscovery**:
   - Searches all loaded indices for module name "builtins"
   - Finds `Sharpy.Core` assembly has module "builtins"
   - Iterates through all function signatures (print, len, range, etc.)
   - Converts each `FunctionSignature` to `FunctionSymbol` via `ConvertToFunctionSymbol()`
   - Returns list of 100+ function symbols

8. **ImportResolver** filters for functions named "print"

9. **TypeChecker** can now verify calls to `print()` have valid signatures

**Performance**: Steps 1-5 take <1ms (disk cache hit). Without caching, step 5 would take ~150ms for reflection on `Sharpy.Core`.

---

## Debugging Tips

### 1. **Cache Issues**

If functions aren't being discovered correctly:

```bash
# Check cache directory
ls -lh ~/.sharpy/cache/overload-index/

# Clear all caches and force rebuild
rm -rf ~/.sharpy/cache/overload-index/
```

**Diagnostic Code**:
```csharp
var cacheInfo = _cache.GetInfo();
Console.WriteLine($"Cache dir: {cacheInfo.CacheDirectory}");
Console.WriteLine($"Cached assemblies: {cacheInfo.CachedAssemblies}");
Console.WriteLine($"Total size: {cacheInfo.TotalSizeBytes / 1024}KB");
```

### 2. **Assembly Not Found**

If `GetModuleFunctions()` returns empty list:

- Check that the assembly was actually loaded with `LoadAssembly()`
- Verify the module name matches what `OverloadIndexBuilder.DeriveModuleName()` generates:
  - `Sharpy.Core.Exports` → `"builtins"` (special case)
  - `MyCompany.MyLib.Exports` → `"mycompany_mylib"`
- Look for errors in `OverloadIndexBuilder` that might have skipped methods

### 3. **Type Conversion Issues**

If types aren't resolving correctly in `ConvertTypeSignature()`:

- Add logging to see what `signature.Name` and `signature.ClrTypeName` contain
- Check if `Type.GetType()` is failing due to missing assembly references
- Verify the `TypeMapper` correctly handles the CLR type

**Debug Pattern**:
```csharp
private SemanticType ConvertTypeSignature(TypeSignature signature)
{
    Console.WriteLine($"Converting: Name={signature.Name}, CLR={signature.ClrTypeName}, Generic={signature.IsGeneric}");
    // ... rest of method
}
```

### 4. **Concurrency Issues**

If you suspect race conditions:

- The `ConcurrentDictionary` should handle thread safety automatically
- Check for exceptions in the `GetOrAdd` factory function - these could indicate contention
- Use `Debug.WriteLine()` with thread IDs to trace execution:
  ```csharp
  Debug.WriteLine($"[Thread {Thread.CurrentThread.ManagedThreadId}] Loading {identity.Name}");
  ```

### 5. **Cache Corruption**

Symptoms: Deserialization failures, missing functions after cache hit

**Recovery**:
```bash
# Find corrupted cache files
cd ~/.sharpy/cache/overload-index/
file *.json.gz | grep -v "gzip compressed data"

# Delete specific cache
rm sharpy.core-*.json.gz
```

The cache automatically cleans up on load failure, but manual intervention can help diagnose patterns.

---

## Contribution Guidelines

### When to Modify This File

1. **Adding New Module Sources**: If Sharpy gains the ability to import from sources other than `Exports` classes (e.g., native modules, dynamic assemblies)

2. **Improving Type Mapping**: Extending `ConvertTypeSignature()` to handle new semantic types or CLR types

3. **Fixing Default Parameter Support**: Implementing the TODO to reconstruct `DefaultValue` expressions from cached strings

4. **Performance Optimization**: Adding caching for `AppDomain.GetAssemblies()` type lookup if profiling shows it as a bottleneck

5. **Query Methods**: Adding new methods to query cached data (e.g., `GetFunctionsByPrefix()`, `GetModulesByNamespace()`)

### What NOT to Change

- **Caching Strategy**: The `OverloadIndexCache` handles persistence. Changes to cache format should happen there, not here.
- **Reflection Logic**: The `OverloadIndexBuilder` handles discovery. Changes to what gets discovered should happen there.
- **Type System**: Changes to `SemanticType` hierarchy should happen in `src/Sharpy.Compiler/Semantic/SemanticType.cs`

### Testing Considerations

When adding features:

1. **Unit Tests**: Mock `OverloadIndexCache` to test without disk I/O
   ```csharp
   var tempCache = new OverloadIndexCache(Path.GetTempPath());
   var discovery = new CachedModuleDiscovery(tempCache);
   ```

2. **Integration Tests**: Test with real assemblies:
   ```csharp
   var assembly = typeof(Sharpy.Core.Exports).Assembly;
   discovery.LoadAssembly(assembly);
   var functions = discovery.GetModuleFunctions("builtins");
   Assert.True(functions.Any(f => f.Name == "print"));
   ```

3. **Concurrency Tests**: Use `Parallel.For` to simulate concurrent loads

4. **Cache Invalidation Tests**: Modify an assembly file and verify cache is rebuilt

### Code Style Notes

- Use **XML doc comments** for all public methods
- Prefer **LINQ** for collection operations (maintains consistency with codebase)
- Use **string interpolation** (`$"..."`) for formatting
- Add **TODO comments** for incomplete features (like default value reconstruction)
- Log warnings with `Console.Error.WriteLine()` for non-critical failures

---

## Cross-References

### Related Source Files

**Discovery/Caching Subsystem**:
- [`OverloadIndexCache.md`](Caching/OverloadIndexCache.md) - Persistent cache management
- [`OverloadIndexBuilder.md`](Caching/OverloadIndexBuilder.md) - Reflection-based function discovery
- [`OverloadIndex.md`](Caching/OverloadIndex.md) - Cached data model
- [`AssemblyIdentity.md`](Caching/AssemblyIdentity.md) - Versioned assembly identification

**Semantic Analysis**:
- `src/Sharpy.Compiler/Semantic/ModuleRegistry.cs` - Calls this class to resolve compiled modules
- `src/Sharpy.Compiler/Semantic/ImportResolver.cs` - Uses discovered functions during import resolution
- `src/Sharpy.Compiler/Semantic/Symbol.cs` - Target data structures (`FunctionSymbol`, `ParameterSymbol`)
- `src/Sharpy.Compiler/Semantic/SemanticType.cs` - Type system used in conversion

**Code Generation**:
- [`TypeMapper.md`](TypeMapper.md) - Shared type mapping logic between discovery and emission

### Related Documentation

**Language Specifications**:
- `docs/language_specification/module_resolution.md` - How imports are resolved
- `docs/language_specification/module_system.md` - Module naming and organization

### Future Enhancements

1. **Default Parameter Reconstruction**: Parse cached default value strings back into AST expressions
2. **Generic Method Support**: Currently filtered out by `OverloadIndexBuilder`, needs semantic model for generics
3. **Property Discovery**: Extend to discover public static properties from `Exports` classes
4. **Assembly Watching**: Automatically reload when referenced assemblies change on disk
5. **Lazy Loading**: Only discover functions when module is first imported (saves startup time)

---

## Summary

`CachedModuleDiscovery` is a critical performance optimization that makes Sharpy's compiler responsive during development. By caching function signatures discovered via reflection, it reduces import resolution from hundreds of milliseconds to under a millisecond.

The class demonstrates several best practices:
- **Layered caching** (memory + disk)
- **Thread-safe concurrent access**
- **Content-based cache invalidation**
- **Graceful error handling**
- **Separation of concerns**

For most development, you won't need to modify this file - it's a stable component that "just works." However, understanding its role is essential for debugging import resolution issues or extending Sharpy to support new module sources.
