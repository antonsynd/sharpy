# Walkthrough: OverloadIndex.cs

**Source File**: `src/Sharpy.Compiler/Discovery/Caching/OverloadIndex.cs`

---

## Overview

`OverloadIndex.cs` defines the **data structures for caching function overload information** from discovered .NET assemblies. This file is a critical component of Sharpy's **module discovery caching system**, which dramatically improves compilation performance by avoiding expensive reflection operations on previously-analyzed assemblies.

### Role in the Project

When the Sharpy compiler imports .NET assemblies (e.g., `from System.Collections.Generic import List`), it needs to discover what types and methods are available. Reflection is expensive, especially for large assemblies. The overload index allows the compiler to:

1. **Serialize** discovered function signatures to disk
2. **Reuse** cached metadata across compilation sessions
3. **Avoid** redundant reflection on unchanged assemblies
4. **Accelerate** import resolution by 4-7x (as noted in performance tests)

This file contains **pure data structures** with no business logic—it's designed for JSON serialization via System.Text.Json.

---

## Class/Type Structure

The file defines a hierarchical data model with four main classes, organized from top-level to detailed:

```
OverloadIndex                    (Assembly-level)
  └─ ModuleOverloads             (Module-level)
      └─ FunctionSignature       (Function-level)
          ├─ ParameterSignature  (Parameter-level)
          └─ TypeSignature       (Type-level)
```

### 1. `OverloadIndex` (Root Container)

**Purpose**: Top-level cache entry representing all discovered overloads from a single .NET assembly.

```csharp
public class OverloadIndex
{
    public AssemblyIdentity Identity { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int CacheFormatVersion { get; set; } = 1;
    public Dictionary<string, ModuleOverloads> Modules { get; set; } = new();
}
```

**Key Properties**:
- **`Identity`**: Identifies which assembly this cache represents (name, version, public key token)
- **`CreatedAt`**: Timestamp for cache invalidation (detect if assembly has been updated)
- **`CacheFormatVersion`**: Schema version for backward compatibility when cache format evolves
- **`Modules`**: Dictionary mapping module names (e.g., `"System.Collections.Generic"`) to their overloads

**Design Note**: This is the serialization root—when saving to disk, an `OverloadIndex` becomes a single `.json` file.

---

### 2. `ModuleOverloads` (Module/Namespace Container)

**Purpose**: Groups all function overloads within a single module or namespace.

```csharp
public class ModuleOverloads
{
    public string ModuleName { get; set; } = string.Empty;
    public Dictionary<string, List<FunctionSignature>> Functions { get; set; } = new();
}
```

**Key Properties**:
- **`ModuleName`**: The fully-qualified namespace (e.g., `"System.Linq"`)
- **`Functions`**: Dictionary where:
  - **Key**: Function name (e.g., `"Select"`)
  - **Value**: List of overloads for that function (supporting overload resolution)

**Why `List<FunctionSignature>`?**  
.NET and Sharpy support function overloading—multiple methods can share the same name but differ in parameters. For example, `Console.WriteLine` has 18+ overloads. The list stores all variants.

---

### 3. `FunctionSignature` (Individual Overload)

**Purpose**: Represents a single callable overload of a function.

```csharp
public class FunctionSignature
{
    public string Name { get; set; } = string.Empty;
    public List<ParameterSignature> Parameters { get; set; } = new();
    public TypeSignature ReturnType { get; set; } = new();
    public string MethodToken { get; set; } = string.Empty;
}
```

**Key Properties**:
- **`Name`**: Method name (e.g., `"WriteLine"`)
- **`Parameters`**: Ordered list of parameter signatures
- **`ReturnType`**: What the function returns
- **`MethodToken`**: **Critical rehydration key** (see below)

#### Understanding `MethodToken`

This is the **most important property** for performance. The token is a string that uniquely identifies the method for later lookup, formatted as:

```
AssemblyName|TypeName|MethodName|ParamCount
```

**Example**:
```
mscorlib|System.Console|WriteLine|1
```

**Why it matters**:  
When the compiler needs to actually *call* a cached method during code generation, it uses this token to quickly locate the exact `MethodInfo` via reflection, without re-scanning the entire assembly.

**Rehydration workflow**:
1. Parse token → `["mscorlib", "System.Console", "WriteLine", "1"]`
2. Load assembly `mscorlib`
3. Get type `System.Console`
4. Find method `WriteLine` with 1 parameter
5. Use for code generation

---

### 4. `ParameterSignature` (Function Parameter)

**Purpose**: Describes a single parameter of a function.

```csharp
public class ParameterSignature
{
    public string Name { get; set; } = string.Empty;
    public TypeSignature Type { get; set; } = new();
    public bool HasDefault { get; set; }
    public string? DefaultValue { get; set; }
    public bool IsVariadic { get; set; }
}
```

**Key Properties**:
- **`Name`**: Parameter name (e.g., `"value"`)
- **`Type`**: Type signature (see below)
- **`HasDefault`**: Whether parameter has a default value (e.g., `x: int = 0`)
- **`DefaultValue`**: Serialized default value (nullable—only set if `HasDefault` is true)
- **`IsVariadic`**: Whether this is a `params` parameter (`*args` in Python, `params T[]` in C#)

**Design Decision**: Storing default values as strings requires later parsing but keeps serialization simple. Alternative approaches (storing typed values) complicate JSON schema.

---

### 5. `TypeSignature` (Type Description)

**Purpose**: Describes a type, including generics support.

```csharp
public class TypeSignature
{
    public string Name { get; set; } = string.Empty;
    public bool IsGeneric { get; set; }
    public List<TypeSignature> TypeArguments { get; set; } = new();
    public string ClrTypeName { get; set; } = string.Empty;
}
```

**Key Properties**:
- **`Name`**: User-facing type name (e.g., `"List"`, `"int"`)
- **`IsGeneric`**: Whether this type has type parameters
- **`TypeArguments`**: **Recursive structure** for generic type arguments
- **`ClrTypeName`**: Full CLR type name for runtime mapping (e.g., `"System.Collections.Generic.List`1"`)

#### Generics Example

For `List<Dictionary<string, int>>`:

```csharp
new TypeSignature
{
    Name = "List",
    IsGeneric = true,
    ClrTypeName = "System.Collections.Generic.List`1",
    TypeArguments = new List<TypeSignature>
    {
        new TypeSignature
        {
            Name = "Dictionary",
            IsGeneric = true,
            ClrTypeName = "System.Collections.Generic.Dictionary`2",
            TypeArguments = new List<TypeSignature>
            {
                new TypeSignature { Name = "string", ClrTypeName = "System.String" },
                new TypeSignature { Name = "int", ClrTypeName = "System.Int32" }
            }
        }
    }
}
```

**Recursive Design**: `TypeArguments` is itself a `List<TypeSignature>`, enabling arbitrarily nested generics.

---

## Key Functions/Methods

**This file contains NO methods**—it's pure data structures (POCOs - Plain Old CLR Objects). All properties use C# auto-properties with default initialization.

### Default Initialization Pattern

Every property uses initializers to avoid null reference warnings:

```csharp
public string Name { get; set; } = string.Empty;  // Never null
public List<TypeSignature> TypeArguments { get; set; } = new();  // Never null
```

**Why?** Enables nullable reference types (`#nullable enable`) without constant null checks. JSON deserializer will replace these defaults, but code that creates instances manually gets safe defaults.

---

## Dependencies

### Direct Dependencies

1. **`System.Text.Json.Serialization`** (imported but unused in this file)
   - Implicitly used by consumers for JSON serialization
   - Future: May add `[JsonPropertyName]` attributes for custom naming

2. **`AssemblyIdentity`** (referenced but not shown in this file)
   - Likely defined in same namespace
   - Contains assembly name, version, culture, public key token

### Consumers (Who Uses This File?)

1. **`CachedModuleDiscovery`** or similar discovery components
   - Build `OverloadIndex` from reflection
   - Serialize to disk as JSON

2. **Module resolution logic**
   - Deserialize cached index
   - Lookup function overloads without reflection

3. **Code generator**
   - Use `MethodToken` to rehydrate `MethodInfo`
   - Generate correct .NET calls

---

## Patterns and Design Decisions

### 1. **Data Transfer Object (DTO) Pattern**

All classes are DTOs with:
- Public properties
- No methods
- No validation
- Default constructors
- Serializable structure

**Rationale**: Separation of concerns. Validation and logic live elsewhere; this file is pure data.

### 2. **Explicit Cache Versioning**

```csharp
public int CacheFormatVersion { get; set; } = 1;
```

**Why?** As the compiler evolves, the cache format may change:
- Adding new properties (e.g., `IsAsync` for async methods)
- Changing structure (e.g., splitting `MethodToken` into separate fields)
- Improving performance (e.g., binary format instead of JSON)

The version allows graceful migration: if cache version doesn't match, invalidate and rebuild.

### 3. **Hierarchical Dictionary Structure**

```
OverloadIndex
  → Dictionary<string, ModuleOverloads>
      → Dictionary<string, List<FunctionSignature>>
```

**Benefits**:
- **Fast lookup**: O(1) module lookup, O(1) function lookup
- **Natural grouping**: Mirrors how developers think about namespaces
- **Serialization-friendly**: JSON naturally represents dictionaries as objects

### 4. **String-Based Tokens vs. Complex Objects**

`MethodToken` uses a pipe-delimited string instead of a structured object:

```csharp
// Current approach
public string MethodToken { get; set; } = string.Empty;

// Alternative (not used)
public class MethodReference
{
    public string AssemblyName { get; set; }
    public string TypeName { get; set; }
    public string MethodName { get; set; }
    public int ParamCount { get; set; }
}
```

**Trade-offs**:
- ✅ **Simpler serialization** (single string vs. nested object)
- ✅ **Compact representation** (saves disk space)
- ❌ **Parsing overhead** (must split string when rehydrating)
- ❌ **Less type-safe** (no compile-time validation of format)

**Current choice**: Simplicity wins for this use case.

### 5. **Nullable vs. Non-Nullable Properties**

Most properties are non-nullable with safe defaults:

```csharp
public string Name { get; set; } = string.Empty;  // Non-nullable
public string? DefaultValue { get; set; }         // Nullable
```

**Decision criteria**:
- Non-nullable: Properties that always have a value (e.g., every function has a name)
- Nullable: Properties that may be absent (e.g., not all parameters have defaults)

---

## Debugging Tips

### 1. **Inspecting Cached Files**

Cache files are JSON—you can inspect them manually:

```bash
# Find cache directory (likely in compiler temp or user cache)
find ~/.sharpy/cache -name "*.json"

# Pretty-print a cache file
cat ~/.sharpy/cache/System.Collections.json | jq .
```

**Look for**:
- `CreatedAt`: Is cache stale?
- `CacheFormatVersion`: Does it match current version?
- `Modules`: Are expected namespaces present?

### 2. **Deserialization Failures**

If cache loading fails:

```csharp
// Add try-catch to see deserialization errors
try
{
    var index = JsonSerializer.Deserialize<OverloadIndex>(json);
}
catch (JsonException ex)
{
    Console.WriteLine($"Cache corrupted: {ex.Message}");
    // Fall back to reflection
}
```

Common issues:
- Cache format version mismatch
- Incomplete write (file corrupted)
- Manual edits breaking JSON syntax

### 3. **Overload Resolution Problems**

If the wrong overload is selected:

1. Check `FunctionSignature.Parameters` count matches call site
2. Verify `TypeSignature` matches expected types
3. Ensure `MethodToken` points to correct method
4. Test with reflection to compare cached vs. actual method

### 4. **Generic Type Issues**

For generics problems:

```csharp
// Log type signature structure
void DumpTypeSignature(TypeSignature sig, int indent = 0)
{
    Console.WriteLine($"{new string(' ', indent)}{sig.Name} ({sig.ClrTypeName})");
    if (sig.IsGeneric)
    {
        foreach (var arg in sig.TypeArguments)
            DumpTypeSignature(arg, indent + 2);
    }
}
```

**Check**:
- `IsGeneric` flag set correctly
- `TypeArguments` count matches CLR type's arity (e.g., `Dictionary`2` has 2)
- Nested generics fully expanded

### 5. **Performance Profiling**

To verify caching helps:

```csharp
var sw = Stopwatch.StartNew();

// Without cache (uses reflection)
var overloads1 = DiscoverOverloadsViaReflection(assembly);
var time1 = sw.ElapsedMilliseconds;

sw.Restart();

// With cache
var overloads2 = LoadOverloadIndexFromCache(assembly);
var time2 = sw.ElapsedMilliseconds;

Console.WriteLine($"Reflection: {time1}ms, Cache: {time2}ms, Speedup: {time1/time2}x");
```

Expected: 4-7x improvement (per tests in `Sharpy.Compiler.Tests/Performance/CachingTests.cs`).

---

## Contribution Guidelines

### Types of Changes You Might Make

#### 1. **Adding New Metadata**

If the compiler needs to track additional method information:

```csharp
public class FunctionSignature
{
    // Existing properties...
    
    // NEW: Track async methods
    public bool IsAsync { get; set; }
    
    // NEW: Track extension methods
    public bool IsExtension { get; set; }
}
```

**Don't forget**:
- Increment `CacheFormatVersion`
- Update cache invalidation logic
- Add migration for old caches

#### 2. **Optimizing Serialization**

Current format is JSON for human readability. For performance:

```csharp
// Consider binary serialization
// - MessagePack
// - Protocol Buffers
// - Custom binary format
```

**Trade-offs**: Binary is faster but harder to debug.

#### 3. **Improving MethodToken**

Replace string token with structured reference:

```csharp
public class MethodReference
{
    public string AssemblyName { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string MethodName { get; set; } = string.Empty;
    public int ParameterCount { get; set; }
    
    public string ToToken() => $"{AssemblyName}|{TypeName}|{MethodName}|{ParameterCount}";
    public static MethodReference FromToken(string token) { /* parse */ }
}
```

**Benefits**: Type safety, easier to extend, better IntelliSense.

#### 4. **Adding Validation**

Currently, no validation in data classes. Could add:

```csharp
public class OverloadIndex
{
    private int _cacheFormatVersion = 1;
    
    public int CacheFormatVersion
    {
        get => _cacheFormatVersion;
        set
        {
            if (value < 1)
                throw new ArgumentException("Version must be >= 1");
            _cacheFormatVersion = value;
        }
    }
}
```

**Or** use validation library (FluentValidation, DataAnnotations).

#### 5. **Supporting Attributes**

Track C# attributes on methods:

```csharp
public class FunctionSignature
{
    // Existing...
    
    public List<AttributeSignature> Attributes { get; set; } = new();
}

public class AttributeSignature
{
    public string AttributeTypeName { get; set; } = string.Empty;
    public Dictionary<string, object> Arguments { get; set; } = new();
}
```

**Use case**: Sharpy decorators that map to .NET attributes.

---

### Testing Your Changes

When modifying this file:

1. **Unit tests** (in `Sharpy.Compiler.Tests/Discovery/`):
   - Serialize and deserialize round-trip
   - Verify all properties preserved
   - Test edge cases (empty collections, nulls, deeply nested generics)

2. **Integration tests**:
   - Compile project with caching enabled
   - Verify correct method resolution
   - Compare output with/without cache

3. **Performance tests** (in `Sharpy.Compiler.Tests/Performance/CachingTests.cs`):
   - Measure serialization time
   - Measure deserialization time
   - Verify speedup maintained

---

### Code Quality Standards

Follow existing patterns:

```csharp
// ✅ GOOD: Default initialization, non-nullable
public List<TypeSignature> TypeArguments { get; set; } = new();

// ❌ BAD: Nullable when unnecessary
public List<TypeSignature>? TypeArguments { get; set; }

// ✅ GOOD: Descriptive XML comments
/// <summary>
/// Signature of a single function overload.
/// </summary>

// ❌ BAD: No documentation
// Just some class
```

**Before submitting PR**:
- Run `dotnet format`
- Ensure `dotnet build` succeeds
- Run all tests: `dotnet test`
- Update `CacheFormatVersion` if format changed
- Add migration logic if needed

---

## Related Files

To fully understand the caching system, also review:

1. **`AssemblyIdentity.cs`**: How assemblies are identified for caching
2. **`CachedModuleDiscovery.cs`**: Logic that builds and uses these structures
3. **`ModuleCache.cs`**: Cache storage and retrieval
4. **`Sharpy.Compiler.Tests/Performance/CachingTests.cs`**: Validation that caching works

---

## Summary

`OverloadIndex.cs` is a **data model file** that defines the schema for caching .NET method metadata. Its hierarchical structure (`OverloadIndex` → `ModuleOverloads` → `FunctionSignature` → `ParameterSignature`/`TypeSignature`) mirrors how methods are organized in assemblies.

**Key takeaways**:
- 📦 **Pure data structures** with no logic
- 🚀 **Enables 4-7x faster** compilation via caching
- 🔄 **Designed for JSON serialization** and deserialization
- 🎯 **MethodToken is critical** for rehydrating actual MethodInfo objects
- 🔢 **Versioning built-in** for schema evolution

**Mental model**: Think of this as the "save game format" for the compiler's knowledge about .NET assemblies—instead of re-learning what methods exist every time, it loads a pre-computed index.

When debugging cache issues, start here and trace how these structures are populated (discovery) and consumed (code generation).
