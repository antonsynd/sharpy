# Walkthrough: OverloadIndex.cs

**Source File**: `src/Sharpy.Compiler/Discovery/Caching/OverloadIndex.cs`

---

## Overview

`OverloadIndex.cs` defines the **serializable data structures** for caching function overload information discovered from .NET assemblies. Think of it as the "schema" for the compiler's assembly cache system.

### What Problem Does This Solve?

When Sharpy code imports .NET assemblies (like `Sharpy.Core` for builtins), the compiler needs to:
1. Discover which functions are available
2. Understand their signatures (parameters, return types)
3. Do this **quickly** on subsequent compilations

Reflecting over assemblies is expensive (100-500ms for large assemblies). This caching system reduces that to <5ms by:
- Building an index once via reflection
- Serializing it to disk (`~/.sharpy/cache/overload-index/`)
- Loading the cached index on subsequent compilations

### Role in the Compiler Pipeline

```
.NET Assembly (DLL)
    ↓
[OverloadIndexBuilder] ← Reflects over assembly
    ↓
[OverloadIndex] ← In-memory data structure (this file)
    ↓
[OverloadIndexCache] ← Serializes to disk (JSON + GZIP)
    ↓
~/.sharpy/cache/overload-index/sharpy.core-1.0.0-a1b2c3d4.json.gz
    ↓
[CachedModuleDiscovery] ← Uses cached data during type checking
```

---

## Class/Type Structure

This file defines **four main data classes** that form a hierarchy:

```
OverloadIndex
├── Identity: AssemblyIdentity (which assembly this is)
├── Modules: Dictionary<string, ModuleOverloads> (e.g., "builtins", "System.Collections")
    └── ModuleOverloads
        ├── ModuleName: string
        └── Functions: Dictionary<string, List<FunctionSignature>> (e.g., "print", "len")
            └── FunctionSignature (one per overload)
                ├── Name: string (snake_case like "split_lines")
                ├── Parameters: List<ParameterSignature>
                │   └── ParameterSignature
                │       ├── Name: string
                │       ├── Type: TypeSignature
                │       ├── HasDefault: bool
                │       └── IsVariadic: bool
                ├── ReturnType: TypeSignature
                └── MethodToken: string (for rehydration)
```

### 1. `OverloadIndex` - Top-Level Container

```csharp
public class OverloadIndex
{
    public AssemblyIdentity Identity { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int CacheFormatVersion { get; set; } = 1;
    public Dictionary<string, ModuleOverloads> Modules { get; set; } = new();
}
```

**Purpose**: Root object that represents a complete cached index for one assembly.

**Key Properties**:
- `Identity`: Uniquely identifies the assembly (name + version + content hash)
- `CreatedAt`: Timestamp for cache invalidation/debugging
- `CacheFormatVersion`: Allows future schema migrations (currently `1`)
- `Modules`: All modules discovered in the assembly

**Design Note**: Each assembly gets its own cache file. The filename is derived from `Identity.ToCacheKey()`.

---

### 2. `ModuleOverloads` - Functions Grouped by Module

```csharp
public class ModuleOverloads
{
    public string ModuleName { get; set; } = string.Empty;
    public Dictionary<string, List<FunctionSignature>> Functions { get; set; } = new();
}
```

**Purpose**: Groups functions by their module/namespace.

**Module Naming Convention**:
- `Sharpy.Core.Exports` → `"builtins"` (special case)
- Other namespaces → snake_case conversion (e.g., `System.Collections` → `"system_collections"`)

**Why `List<FunctionSignature>` for each function?**
- Supports **function overloading** - multiple methods with the same name but different signatures
- Example: `print()`, `print(str)`, `print(str, str)` would all be in the list for key `"print"`

---

### 3. `FunctionSignature` - Complete Overload Information

```csharp
public class FunctionSignature
{
    public string Name { get; set; } = string.Empty;
    public List<ParameterSignature> Parameters { get; set; } = new();
    public TypeSignature ReturnType { get; set; } = new();
    public string MethodToken { get; set; } = string.Empty;
}
```

**Purpose**: Captures everything needed to understand and call one function overload.

**Key Properties**:
- `Name`: Snake_case function name (e.g., `"split_lines"` from C# `SplitLines`)
- `Parameters`: Ordered list of parameters
- `ReturnType`: What the function returns
- `MethodToken`: **Critical for rehydration** - lets us find the actual `MethodInfo` later

**MethodToken Format**: `AssemblyName|TypeName|MethodName|ParamCount`
- Example: `"Sharpy.Core|Sharpy.Core.Exports|SplitLines|2"`
- Used by `CachedModuleDiscovery` to resolve the actual method during compilation

---

### 4. `ParameterSignature` - Individual Parameter Details

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

**Purpose**: Complete parameter information for type checking and error messages.

**Key Properties**:
- `Name`: Parameter name (used in error messages and named arguments)
- `Type`: Sharpy type signature
- `HasDefault`: Whether parameter is optional (e.g., `def foo(x: int = 5)`)
- `DefaultValue`: String representation of default (e.g., `"5"`, `"\"hello\""`, `"true"`)
- `IsVariadic`: Whether it's a `*args` parameter (from C# `params`)

**Default Value Serialization**:
```csharp
// From OverloadIndexBuilder.ConvertDefaultValue()
string s => $"\"{s}\""        // Strings: "hello" → "\"hello\""
bool b => b.ToString().ToLowerInvariant()  // Booleans: True → "true"
int/long/... => value.ToString()           // Numbers: 42 → "42"
float f => f.ToString("G9")                // Floats: precise formatting
```

---

### 5. `TypeSignature` - Type Information

```csharp
public class TypeSignature
{
    public string Name { get; set; } = string.Empty;
    public bool IsGeneric { get; set; }
    public List<TypeSignature> TypeArguments { get; set; } = new();
    public string ClrTypeName { get; set; } = string.Empty;
}
```

**Purpose**: Represents Sharpy types with support for generics.

**Key Properties**:
- `Name`: Human-readable Sharpy type name (e.g., `"list[int]"`, `"str"`, `"int?"`)
- `IsGeneric`: True for generic types like `list[T]`, `dict[K,V]`
- `TypeArguments`: Nested `TypeSignature` objects for generic arguments
- `ClrTypeName`: Full CLR type name for mapping back (e.g., `"System.Int32"`)

**Example - Generic Type**:
```csharp
// For List<int> in C#:
{
    Name: "list[int]",
    IsGeneric: true,
    TypeArguments: [
        { Name: "int", ClrTypeName: "System.Int32" }
    ],
    ClrTypeName: "Sharpy.Core.List`1"
}
```

---

## Key Functions/Methods

**Note**: This file contains **only data classes** - no methods beyond property getters/setters. The logic that populates and uses these structures lives in:
- `OverloadIndexBuilder.cs` - Creates the index from reflection
- `OverloadIndexCache.cs` - Serializes/deserializes to disk
- `CachedModuleDiscovery.cs` - Uses the cached index during compilation

---

## Dependencies

### Internal Dependencies

**Direct dependencies** (imported types):
- `AssemblyIdentity` (same namespace) - Identifies which assembly this index is for

**Consumer dependencies** (who uses this file):
- `OverloadIndexBuilder` - Builds instances of these classes via reflection
- `OverloadIndexCache` - Serializes/deserializes using `System.Text.Json`
- `CachedModuleDiscovery` - Loads cached indexes and queries function information

### External Dependencies

- `System.Text.Json.Serialization` - For JSON serialization attributes (implied by usage in `OverloadIndexCache`)
- No direct .NET dependencies in this file (pure data classes)

---

## Patterns and Design Decisions

### 1. **Plain Data Objects (POCOs)**

All classes are simple property bags with no logic. This is intentional:
- ✅ Easy to serialize/deserialize
- ✅ Easy to test
- ✅ Clear separation of concerns (data vs. logic)
- ✅ Can evolve independently of business logic

### 2. **Immutability Not Enforced**

Unlike AST nodes which use C# records, these are mutable classes:
- **Why?** JSON deserialization requires mutable objects (or special constructors)
- **Trade-off**: Slightly less safe, but more compatible with System.Text.Json

### 3. **Hierarchical Structure Mirrors Reality**

The nesting mirrors how .NET assemblies are organized:
```
Assembly → Modules/Namespaces → Functions → Overloads → Parameters → Types
```

This makes it intuitive to navigate and query.

### 4. **String-Based MethodToken for Rehydration**

Instead of serializing full `MethodInfo` objects (impossible), we serialize enough metadata to reconstruct them:

```csharp
// MethodToken format: "AssemblyName|TypeName|MethodName|ParamCount"
"Sharpy.Core|Sharpy.Core.Exports|Print|1"

// Later, in CachedModuleDiscovery:
var assembly = Assembly.Load(assemblyName);
var type = assembly.GetType(typeName);
var method = type.GetMethods()
    .First(m => m.Name == methodName && m.GetParameters().Length == paramCount);
```

**Why include ParamCount?**
- Disambiguates overloads with the same name
- Faster lookup than comparing full parameter lists

### 5. **CacheFormatVersion for Schema Evolution**

The `CacheFormatVersion` field allows for future changes:
```csharp
public int CacheFormatVersion { get; set; } = 1;
```

If we need to change the schema (e.g., add new fields), we can:
1. Increment the version to `2`
2. In `OverloadIndexCache.TryLoad()`, check the version
3. Reject old caches or migrate them

### 6. **Default Property Initialization**

All properties have default values:
```csharp
public string Name { get; set; } = string.Empty;
public List<ParameterSignature> Parameters { get; set; } = new();
```

**Benefits**:
- Prevents `NullReferenceException` if deserialization partially fails
- Makes objects safe to use even if not fully populated
- Simplifies testing (can create empty objects)

---

## Debugging Tips

### 1. **Inspecting Cache Files**

Cache files are stored in `~/.sharpy/cache/overload-index/` as compressed JSON:

```bash
# Find cache files
ls -lh ~/.sharpy/cache/overload-index/

# Inspect a cache file (decompress and pretty-print)
gunzip -c ~/.sharpy/cache/overload-index/sharpy.core-1.0.0-*.json.gz | jq .

# Check specific function
gunzip -c ~/.sharpy/cache/overload-index/sharpy.core-*.json.gz | jq '.modules.builtins.functions.print'
```

### 2. **Cache Invalidation Issues**

If the compiler isn't picking up new functions after rebuilding an assembly:

```bash
# Clear the cache
rm -rf ~/.sharpy/cache/overload-index/

# Or just for a specific assembly
rm ~/.sharpy/cache/overload-index/sharpy.core-*.json.gz
```

**Common causes**:
- Assembly content changed but version/name didn't → `AssemblyIdentity.ContentHash` should catch this
- Cache file corrupted → `OverloadIndexCache` will delete and rebuild
- Wrong assembly loaded → Check `OverloadIndex.Identity.FilePath`

### 3. **Missing Functions in Cache**

If a function exists in the assembly but isn't in the cache:

**Check `OverloadIndexBuilder` filtering rules** (lines 69-76):
```csharp
var eligibleMethods = methods
    .Where(m => !m.Name.StartsWith("_"))           // No private methods
    .Where(m => !m.Name.StartsWith("get_"))        // No property getters
    .Where(m => !m.Name.StartsWith("set_"))        // No property setters
    .Where(m => !m.IsSpecialName)                  // No operators, etc.
    .Where(m => !m.IsGenericMethodDefinition)      // No generic methods (not yet supported)
    .Where(m => !IsTypeConstructor(m))             // No type constructors
```

Your function might be filtered out!

### 4. **Type Mapping Issues**

If a function's types look wrong in the cache:

**Check `TypeMapper.MapClrTypeToSemanticType()`** in `Discovery/TypeMapper.cs`:
- This converts `System.Int32` → `"int"`, `List<T>` → `"list[T]"`, etc.
- Add debugging in `OverloadIndexBuilder.CreateTypeSignature()` (line 171)

### 5. **Debugging MethodToken Resolution**

If function calls fail at runtime with "method not found":

**Verify MethodToken format**:
```csharp
// Should be: AssemblyName|TypeName|MethodName|ParamCount
"Sharpy.Core|Sharpy.Core.Exports|Print|1"
```

**Check that the assembly is actually loaded**:
```csharp
// In CachedModuleDiscovery
var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
```

---

## Contribution Guidelines

### When to Modify This File

**Add new properties** when you need to cache additional metadata:

```csharp
// Example: Add support for caching generic method constraints
public class FunctionSignature
{
    // ... existing properties ...
    
    // NEW: Generic type constraints
    public List<TypeConstraint> GenericConstraints { get; set; } = new();
}
```

**When adding properties**:
1. ✅ Use default initialization (`= new()`, `= string.Empty`)
2. ✅ Increment `CacheFormatVersion` in `OverloadIndex`
3. ✅ Update `OverloadIndexBuilder` to populate the new property
4. ✅ Consider backward compatibility (can old caches be read?)
5. ✅ Update tests in `Sharpy.Compiler.Tests/Discovery/`

### Common Modification Scenarios

#### 1. **Adding Parameter Metadata**

If you need to cache whether a parameter is `out`, `ref`, or has attributes:

```csharp
public class ParameterSignature
{
    // ... existing ...
    public bool IsOut { get; set; }
    public bool IsRef { get; set; }
    public List<string> Attributes { get; set; } = new();
}
```

Then update `OverloadIndexBuilder.CreateParameterSignature()`:
```csharp
return new ParameterSignature
{
    // ... existing ...
    IsOut = param.IsOut,
    IsRef = param.ParameterType.IsByRef && !param.IsOut,
    Attributes = param.GetCustomAttributes().Select(a => a.GetType().Name).ToList()
};
```

#### 2. **Adding Function Metadata**

To cache whether a function is async, deprecated, or has other attributes:

```csharp
public class FunctionSignature
{
    // ... existing ...
    public bool IsAsync { get; set; }
    public bool IsDeprecated { get; set; }
    public string? DeprecationMessage { get; set; }
}
```

Update `OverloadIndexBuilder.CreateFunctionSignature()`:
```csharp
var deprecatedAttr = method.GetCustomAttribute<ObsoleteAttribute>();
signature.IsDeprecated = deprecatedAttr != null;
signature.DeprecationMessage = deprecatedAttr?.Message;
```

#### 3. **Improving Type Representation**

If you need better generic type support:

```csharp
public class TypeSignature
{
    // ... existing ...
    
    // Support for generic type constraints
    public List<string> Constraints { get; set; } = new(); // "new()", "struct", "class"
    
    // Support for nullable reference types
    public NullabilityState Nullability { get; set; }
}
```

### Testing Your Changes

After modifying these data structures:

1. **Clear the cache** (force rebuild):
   ```bash
   rm -rf ~/.sharpy/cache/overload-index/
   ```

2. **Run discovery tests**:
   ```bash
   dotnet test --filter "FullyQualifiedName~Discovery"
   ```

3. **Verify serialization roundtrip**:
   ```bash
   dotnet test --filter "FullyQualifiedName~OverloadIndexCache"
   ```

4. **Test with a real assembly**:
   ```bash
   dotnet run --project src/Sharpy.Cli -- build samples/type_system_showcase.spy
   ```

5. **Inspect the cache**:
   ```bash
   gunzip -c ~/.sharpy/cache/overload-index/sharpy.core-*.json.gz | jq . | less
   ```

### Style Guidelines

**Follow existing patterns**:
- ✅ Use `{ get; set; }` properties
- ✅ Initialize collections: `= new()`
- ✅ Initialize strings: `= string.Empty`
- ✅ Use XML doc comments for public types
- ✅ Keep data classes in this file, logic elsewhere

**Naming conventions**:
- Classes: `PascalCase` (e.g., `FunctionSignature`)
- Properties: `PascalCase` (e.g., `ReturnType`)
- Collections: Plural names (e.g., `Parameters`, `Modules`)

---

## Related Files

### Must-Read Related Files

| File | Purpose | Relationship |
|------|---------|--------------|
| `AssemblyIdentity.cs` | Identifies assemblies for caching | Used by `OverloadIndex.Identity` |
| `OverloadIndexBuilder.cs` | Populates these data structures | **Creates** instances of these classes |
| `OverloadIndexCache.cs` | Persists to disk | **Serializes/deserializes** these classes |
| `CachedModuleDiscovery.cs` | Uses cached data during compilation | **Consumes** these classes |

### Broader Context

| Area | Files | Purpose |
|------|-------|---------|
| **Discovery** | `Discovery/*.cs` | Module/assembly discovery system |
| **Semantic** | `Semantic/TypeResolver.cs` | Uses function signatures for type checking |
| **Type Mapping** | `Discovery/TypeMapper.cs` | Converts CLR types ↔ Sharpy types |

---

## Example: Full Cache Structure

Here's what a complete cached index looks like for a simple assembly:

```json
{
  "identity": {
    "name": "Sharpy.Core",
    "version": "1.0.0",
    "contentHash": "a1b2c3d4e5f6...",
    "filePath": "/path/to/Sharpy.Core.dll"
  },
  "createdAt": "2025-12-27T00:00:00Z",
  "cacheFormatVersion": 1,
  "modules": {
    "builtins": {
      "moduleName": "builtins",
      "functions": {
        "print": [
          {
            "name": "print",
            "parameters": [
              {
                "name": "value",
                "type": {
                  "name": "object",
                  "isGeneric": false,
                  "typeArguments": [],
                  "clrTypeName": "System.Object"
                },
                "hasDefault": false,
                "defaultValue": null,
                "isVariadic": false
              }
            ],
            "returnType": {
              "name": "None",
              "isGeneric": false,
              "typeArguments": [],
              "clrTypeName": "System.Void"
            },
            "methodToken": "Sharpy.Core|Sharpy.Core.Exports|Print|1"
          }
        ],
        "len": [
          {
            "name": "len",
            "parameters": [
              {
                "name": "obj",
                "type": {
                  "name": "list[T]",
                  "isGeneric": true,
                  "typeArguments": [
                    {
                      "name": "T",
                      "isGeneric": false,
                      "typeArguments": [],
                      "clrTypeName": ""
                    }
                  ],
                  "clrTypeName": "Sharpy.Core.List`1"
                },
                "hasDefault": false,
                "defaultValue": null,
                "isVariadic": false
              }
            ],
            "returnType": {
              "name": "int",
              "isGeneric": false,
              "typeArguments": [],
              "clrTypeName": "System.Int32"
            },
            "methodToken": "Sharpy.Core|Sharpy.Core.List`1|get_Count|0"
          }
        ]
      }
    }
  }
}
```

---

## Summary

`OverloadIndex.cs` is the **data schema** for Sharpy's assembly cache system. It defines pure data classes that:
- Capture all metadata needed to understand .NET function signatures
- Serialize efficiently to JSON + GZIP
- Enable fast compiler startup by avoiding expensive reflection

**Key Takeaways**:
1. **Four main types**: `OverloadIndex` → `ModuleOverloads` → `FunctionSignature` → `ParameterSignature` + `TypeSignature`
2. **No logic here**: All business logic is in `Builder`, `Cache`, and `Discovery` classes
3. **MethodToken is crucial**: Links cached data back to actual `MethodInfo` at runtime
4. **Designed for JSON**: Mutable properties with default initialization
5. **Schema versioning**: `CacheFormatVersion` allows future evolution

When debugging, remember: **The cache is an optimization**. If you delete it, everything still works (just slower). This makes it safe to clear when troubleshooting!
