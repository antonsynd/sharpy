# Walkthrough: BuiltinRegistry.cs

**Source File**: `src/Sharpy.Compiler/Semantic/BuiltinRegistry.cs`

---

## Overview

`BuiltinRegistry` is the central registry for all builtin types and functions that are available globally in Sharpy programs without requiring imports. It serves as the bridge between the Sharpy type system and the .NET runtime library (`Sharpy.Core`).

**Key Responsibilities:**
- Register primitive types (int, float, bool, str, etc.) from `PrimitiveCatalog`
- Register collection types (list, dict, set) from `Sharpy.Core`
- Auto-discover builtin functions from `Sharpy.Core.Exports` using reflection
- Provide lookup methods for types and functions during semantic analysis

**Position in Compiler Pipeline:**
```
Parser (AST) → BuiltinRegistry ← TypeChecker → RoslynEmitter
                      ↓
                SymbolTable (populated with builtins)
```

The registry is created during compiler initialization and used by `TypeChecker` and `NameResolver` to resolve builtin type and function references.

---

## Class/Type Structure

### Main Class: `BuiltinRegistry`

```csharp
public class BuiltinRegistry
{
    private readonly Dictionary<string, TypeSymbol> _types;
    private readonly Dictionary<string, List<FunctionSymbol>> _functions;
    private readonly CachedModuleDiscovery _discovery;

    // ... methods
}
```

**Internal State:**
- `_types`: Maps Sharpy type names (e.g., "int", "list") to their `TypeSymbol` definitions
- `_functions`: Maps function names to lists of `FunctionSymbol` (to support overloading)
- `_discovery`: Reflection-based discovery system with caching for performance

**Registered Primitive Names:**
A whitelist of primitive types to register from `PrimitiveCatalog`:
```csharp
private static readonly HashSet<string> RegisteredPrimitiveNames = new()
{
    "int", "long", "float", "double", "decimal", "bool", "str"
};
```

This maintains backward compatibility by only exposing a subset of all available primitives (excludes int8, int16, uint*, etc. from global scope).

---

## Key Functions/Methods

### Constructor: `BuiltinRegistry()`

**Purpose:** Initialize the registry and populate it with all builtin types and functions.

```csharp
public BuiltinRegistry()
{
    _discovery = new CachedModuleDiscovery();
    LoadBuiltins();
}
```

**What It Does:**
1. Creates a `CachedModuleDiscovery` instance (uses disk cache to avoid repeated reflection)
2. Calls `LoadBuiltins()` to populate the registry

**Performance Note:** The first run performs reflection on `Sharpy.Core.dll`, which is then cached to disk. Subsequent runs load from cache, making initialization fast.

---

### Private Method: `LoadBuiltins()`

**Purpose:** Register all primitive types, collection types, and discover builtin functions.

```csharp
private void LoadBuiltins()
{
    // 1. Register primitives from PrimitiveCatalog
    foreach (var (name, info) in PrimitiveCatalog.GetAllPrimitives())
    {
        if (!RegisteredPrimitiveNames.Contains(name))
            continue;
        if (info.ClrType == typeof(void))
            continue;

        var kind = info.ClrType.IsValueType ? TypeKind.Struct : TypeKind.Class;
        RegisterType(info.SharpyName, info.ClrType, kind);
    }

    // 2. Register collection types (generic)
    RegisterType("list", typeof(Sharpy.Core.List<>), TypeKind.Class,
                 isGeneric: true, typeParamCount: 1);
    RegisterType("dict", typeof(Sharpy.Core.Dict<,>), TypeKind.Class,
                 isGeneric: true, typeParamCount: 2);
    RegisterType("set", typeof(Sharpy.Core.Set<>), TypeKind.Class,
                 isGeneric: true, typeParamCount: 1);

    // 3. Register special types
    RegisterType("object", typeof(object), TypeKind.Class);
    RegisterType("None", typeof(void), TypeKind.Struct);

    // 4. Discover builtin functions
    LoadBuiltinFunctions();
}
```

**Key Design Decisions:**

1. **Primitive Filtering**: Only registers primitives in `RegisteredPrimitiveNames`, not all primitives from `PrimitiveCatalog`. This prevents cluttering the global namespace with types like `int8`, `uint16`, etc.

2. **Void Handling**: Skips `typeof(void)` in the primitive loop but registers "None" separately. This maps Python's `None` to C#'s `void` for return types.

3. **Collection Types**: These are registered manually because they're not primitives (they're generic classes). **Important:** Uses `Sharpy.Core.List<>`, NOT `System.Collections.Generic.List<>`!

4. **Generic Type Registration**: Generic types are registered with their CLR open generic type (e.g., `List<>`) and a count of type parameters. This allows the type system to later construct closed generic types (e.g., `list[int]`).

---

### Private Method: `LoadBuiltinFunctions()`

**Purpose:** Use reflection to discover all builtin functions from `Sharpy.Core.Exports`.

```csharp
private void LoadBuiltinFunctions()
{
    var sharpyCoreAssembly = typeof(Sharpy.Core.Exports).Assembly;
    _discovery.LoadAssembly(sharpyCoreAssembly);

    var builtinFunctions = _discovery.GetModuleFunctions("builtins");

    foreach (var function in builtinFunctions)
    {
        if (!_functions.ContainsKey(function.Name))
        {
            _functions[function.Name] = new List<FunctionSymbol>();
        }
        _functions[function.Name].Add(function);
    }
}
```

**How It Works:**

1. **Assembly Loading**: Gets the `Sharpy.Core` assembly and passes it to `CachedModuleDiscovery`
2. **Module Discovery**: Requests all functions from the "builtins" module
3. **Overload Support**: Stores functions in a `List<FunctionSymbol>` to support multiple overloads of the same function (e.g., `print(str)` and `print(int)`)

**Upstream Connection to Discovery System:**
- `CachedModuleDiscovery` scans `Sharpy.Core.Exports` using reflection
- It looks for methods decorated with `[SharpyBuiltin]` or similar attributes
- Caches the results to `~/.sharpy/cache/overload_index/` to avoid repeated reflection

**Performance Impact:** First-time discovery can take ~100ms, but cached loads are <10ms.

---

### Private Method: `RegisterType(...)`

**Purpose:** Create and register a `TypeSymbol` for a builtin type.

```csharp
private void RegisterType(string sharpyName, Type clrType, TypeKind kind,
                          bool isGeneric = false, int typeParamCount = 0)
{
    var typeSymbol = new TypeSymbol
    {
        Name = sharpyName,
        Kind = SymbolKind.Type,
        TypeKind = kind,
        ClrType = clrType,
        TypeParameters = isGeneric
            ? Enumerable.Range(0, typeParamCount)
                .Select(i => new TypeParameterDef { Name = $"T{i}" })
                .ToList()
            : new List<TypeParameterDef>(),
        AccessLevel = AccessLevel.Public
    };

    _types[sharpyName] = typeSymbol;
}
```

**Parameters:**
- `sharpyName`: The name used in Sharpy source code (e.g., "int", "list")
- `clrType`: The corresponding .NET type (e.g., `typeof(int)`, `typeof(List<>)`)
- `kind`: `TypeKind.Class` or `TypeKind.Struct`
- `isGeneric`: Whether this is a generic type
- `typeParamCount`: Number of type parameters (e.g., 1 for `list[T]`, 2 for `dict[K,V]`)

**Type Parameter Naming:**
Generic type parameters are named `T0`, `T1`, etc. (e.g., `dict` has `T0` for key, `T1` for value). This is an internal representation—the compiler doesn't expose these names to users.

---

### Public Method: `GetType(string name)`

```csharp
public TypeSymbol? GetType(string name) => _types.GetValueOrDefault(name);
```

**Usage:** Called by `TypeChecker` and `NameResolver` to resolve type names in source code.

**Example:**
```python
x: int = 42  # TypeChecker calls GetType("int") → returns TypeSymbol for int32
```

---

### Public Method: `GetFunction(string name)`

```csharp
public FunctionSymbol? GetFunction(string name)
    => _functions.GetValueOrDefault(name)?.FirstOrDefault();
```

**Purpose:** Returns the first overload of a builtin function.

**When to Use:** For quick lookups when you know there's only one overload or you just need to check if a function exists.

**Example:**
```python
len([1, 2, 3])  # NameResolver calls GetFunction("len") to verify it exists
```

---

### Public Method: `GetFunctionOverloads(string name)`

```csharp
public List<FunctionSymbol>? GetFunctionOverloads(string name)
    => _functions.GetValueOrDefault(name);
```

**Purpose:** Returns ALL overloads of a builtin function for overload resolution.

**When to Use:** During type checking when you need to select the correct overload based on argument types.

**Example:**
```python
# Assume print has overloads: print(str), print(int), print(object)
print(42)  # TypeChecker calls GetFunctionOverloads("print") and selects print(int)
```

**Overload Resolution:** The caller (usually `TypeChecker`) is responsible for selecting the best overload based on argument types.

---

### Public Methods: `GetAllTypes()` and `GetAllFunctions()`

```csharp
public IEnumerable<(string Name, TypeSymbol Type)> GetAllTypes()
    => _types.Select(kv => (kv.Key, kv.Value));

public IEnumerable<(string Name, FunctionSymbol Function)> GetAllFunctions()
    => _functions.SelectMany(kv => kv.Value.Select(f => (kv.Key, f)));
```

**Purpose:** Enumerate all registered builtins for debugging, tooling, or symbol table initialization.

**Usage Example:**
```csharp
// Populate global scope with all builtin types
foreach (var (name, typeSymbol) in builtinRegistry.GetAllTypes())
{
    globalScope.Define(typeSymbol);
}
```

---

## Dependencies

### Internal Dependencies

**`PrimitiveCatalog`** (`src/Sharpy.Compiler/Semantic/PrimitiveCatalog.cs`)
- Provides exhaustive catalog of primitive types with metadata
- Used to register primitives like `int`, `float`, `bool`, `str`
- Provides numeric promotion rules (not used by BuiltinRegistry, but by TypeChecker)

**`CachedModuleDiscovery`** (`src/Sharpy.Compiler/Discovery/CachedModuleDiscovery.cs`)
- Reflection-based discovery of builtin functions from assemblies
- Uses disk cache to avoid repeated reflection overhead
- Converts reflected method signatures to `FunctionSymbol` instances

**Symbol Types** (`src/Sharpy.Compiler/Semantic/Symbol.cs`)
- `TypeSymbol`: Represents a type definition (class, struct, interface)
- `FunctionSymbol`: Represents a function with parameters and return type
- `ParameterSymbol`: Represents a function parameter

**SemanticType** (`src/Sharpy.Compiler/Semantic/SemanticType.cs`)
- Used by `FunctionSymbol.ReturnType` and `ParameterSymbol.Type`
- Represents resolved types during semantic analysis

### External Dependencies

**`Sharpy.Core`** (Standard Library)
- Source of all builtin functions (discovered via reflection)
- Provides collection types: `List<T>`, `Dict<K,V>`, `Set<T>`
- Located in `src/Sharpy.Core/` directory

**AST Types** (`Sharpy.Compiler.Parser.Ast`)
- Used by `ParameterSymbol.DefaultValue` (stores AST expression for default values)
- Used by `TypeSymbol.TypeParameters` (stores `TypeParameterDef` from AST)

---

## Patterns and Design Decisions

### 1. **Two-Phase Initialization**

The registry is fully populated during construction (eager loading), not lazily. This ensures:
- All builtins are available immediately
- No thread-safety concerns during lookup
- Predictable initialization cost

**Trade-off:** Slightly slower startup time (~100ms first run, <10ms cached) vs. simpler code and no lazy-loading bugs.

### 2. **Overload Support via `List<FunctionSymbol>`**

Functions are stored as `Dictionary<string, List<FunctionSymbol>>` to support multiple overloads:
```csharp
_functions["print"] = [ print(str), print(int), print(object) ]
```

**Why Not a Single FunctionSymbol?**
- Python/Sharpy allows function overloading (unlike Python's single-dispatch runtime)
- Enables better .NET interop (C# methods often have multiple overloads)
- Type checker can select the best match during compile time

### 3. **Separate Type and Function Registries**

Types and functions are stored separately, not in a unified symbol table. This:
- Simplifies lookup (no need to filter by `SymbolKind`)
- Reflects the fact that types and functions live in different namespaces
- Matches how Python resolves names (types and functions can have the same name)

### 4. **Reflection-Based Discovery with Caching**

Instead of hardcoding function signatures, the registry uses reflection to discover them from `Sharpy.Core`:

**Advantages:**
- Single source of truth (function signatures live in C# code, not duplicated)
- Adding new builtin functions requires no compiler changes
- Type-safe (CLR types are used directly, no string parsing)

**Caching Strategy:**
- First run: Reflect on assembly and cache to disk
- Subsequent runs: Load from cache (10x faster)
- Cache invalidated if assembly version changes

### 5. **Immutable Symbols**

`TypeSymbol` and `FunctionSymbol` are C# records (immutable by default). Once created, they never change. This:
- Enables safe sharing across threads
- Prevents accidental mutation during type checking
- Makes debugging easier (symbols don't change unexpectedly)

### 6. **Whitelist for Primitive Types**

Not all primitives from `PrimitiveCatalog` are registered globally:
```csharp
RegisteredPrimitiveNames = { "int", "long", "float", "double", "decimal", "bool", "str" }
```

**Why?**
- Keeps global namespace clean
- Avoids exposing low-level types like `int8`, `uint16` to beginners
- Advanced users can still import them explicitly (future feature)

**What's Excluded:**
- `int8`, `int16`, `int32`, `int64` (use `int` instead)
- `uint`, `uint8`, `uint16`, `uint32`, `uint64` (unsigned types)
- `float32`, `float64` (use `float` instead)
- `byte`, `sbyte`, `short`, `ushort`, `char` (low-level types)

---

## Debugging Tips

### 1. **"Type 'X' not found" Errors**

Check if the type is in `RegisteredPrimitiveNames`:
```csharp
// Add breakpoint in LoadBuiltins() and inspect _types after loading
var registeredTypes = _types.Keys.ToList();  // Should contain "int", "str", "list", etc.
```

**Common Issue:** Trying to use `int32` instead of `int`. Solution: Use `int` or add `int32` to `RegisteredPrimitiveNames`.

### 2. **"Function 'X' not found" Errors**

Check if the function is discovered from `Sharpy.Core`:
```csharp
// Add breakpoint after LoadBuiltinFunctions()
var registeredFunctions = _functions.Keys.ToList();  // Should contain "print", "len", etc.
```

**Common Issue:** Function exists in `Sharpy.Core` but isn't decorated with the discovery attribute. Solution: Add `[SharpyBuiltin]` attribute to the method.

### 3. **Overload Resolution Failures**

If the type checker can't resolve an overload:
```csharp
var overloads = builtinRegistry.GetFunctionOverloads("functionName");
foreach (var overload in overloads)
{
    Console.WriteLine($"{overload.Name}({string.Join(", ", overload.Parameters.Select(p => p.Type.GetDisplayName()))}) -> {overload.ReturnType.GetDisplayName()}");
}
```

This shows all available overloads and their signatures.

### 4. **Cache Issues**

If function signatures are outdated after changing `Sharpy.Core`:
```bash
# Clear the cache
rm -rf ~/.sharpy/cache/overload_index/

# Or disable caching temporarily
var discovery = new CachedModuleDiscovery(cache: null);  // Null cache = no caching
```

### 5. **Generic Type Issues**

If generic types (list, dict, set) aren't working:
```csharp
var listSymbol = builtinRegistry.GetType("list");
Console.WriteLine($"IsGeneric: {listSymbol.IsGeneric}");  // Should be true
Console.WriteLine($"TypeParams: {listSymbol.TypeParameters.Count}");  // Should be 1
Console.WriteLine($"ClrType: {listSymbol.ClrType}");  // Should be Sharpy.Core.List`1[T]
```

---

## Contribution Guidelines

### When to Modify This File

**DO modify when:**
1. Adding a new primitive type to the global namespace (add to `RegisteredPrimitiveNames`)
2. Adding a new builtin collection type (add `RegisterType` call in `LoadBuiltins`)
3. Changing how builtin functions are discovered (modify `LoadBuiltinFunctions`)
4. Adding special types like "None", "object" (add `RegisterType` call)

**DON'T modify when:**
1. Adding new builtin functions → Add to `Sharpy.Core/Exports.cs` instead
2. Adding new primitive types to PrimitiveCatalog → Modify `PrimitiveCatalog.cs` instead
3. Changing type checking logic → Modify `TypeChecker.cs` instead
4. Changing symbol table behavior → Modify `SymbolTable.cs` or `Scope.cs` instead

### Adding a New Primitive Type

1. Ensure it's registered in `PrimitiveCatalog.cs`
2. Add it to `RegisteredPrimitiveNames` in this file
3. Rebuild and test

Example:
```csharp
private static readonly HashSet<string> RegisteredPrimitiveNames = new()
{
    "int", "long", "float", "double", "decimal", "bool", "str",
    "char"  // ← New primitive type
};
```

### Adding a New Collection Type

Add a `RegisterType` call in `LoadBuiltins()`:
```csharp
// Example: Adding a "tuple" type
RegisterType("tuple", typeof(Sharpy.Core.Tuple<>), TypeKind.Struct,
             isGeneric: true, typeParamCount: 1);
```

### Adding a New Builtin Function

**Don't modify this file!** Instead:
1. Add the function to `src/Sharpy.Core/Exports.cs`
2. Decorate it with `[SharpyBuiltin("builtins")]`
3. Rebuild `Sharpy.Core`
4. Clear the cache: `rm -rf ~/.sharpy/cache/overload_index/`
5. The function will be auto-discovered on next compile

### Testing Changes

After modifying this file:
```bash
# Run semantic tests
dotnet test --filter "FullyQualifiedName~Semantic"

# Run integration tests
dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"

# Test specific builtin
dotnet run --project src/Sharpy.Cli -- emit csharp test.spy
```

### Code Style

- Keep `RegisteredPrimitiveNames` alphabetically sorted
- Keep `RegisterType` calls grouped by category (primitives, collections, special)
- Add comments for non-obvious type mappings (e.g., `None` → `void`)
- Use consistent formatting for generic type registration

---

## Cross-References

### Related Files

**Semantic Analysis:**
- [`Symbol.md`](./Symbol.md) - Defines `TypeSymbol`, `FunctionSymbol`, `ParameterSymbol`
- [`SemanticType.md`](./SemanticType.md) - Type representations during semantic analysis
- [`PrimitiveCatalog.md`](./PrimitiveCatalog.md) - Exhaustive primitive type registry
- [`TypeChecker.md`](./TypeChecker.md) - Uses BuiltinRegistry for type resolution

**Discovery System:**
- `CachedModuleDiscovery.cs` - Reflection-based function discovery with caching
- `OverloadIndexBuilder.cs` - Builds function overload index from assemblies

**Standard Library:**
- `src/Sharpy.Core/Exports.cs` - Defines builtin functions discovered by this registry
- `src/Sharpy.Core/List.cs`, `Dict.cs`, `Set.cs` - Collection implementations

**Usage Examples:**
- [`NameResolver.md`](./NameResolver.md) - Resolves names to symbols using BuiltinRegistry
- [`TypeResolver.md`](./TypeResolver.md) - Resolves type annotations using BuiltinRegistry
- [`ModuleRegistry.md`](./ModuleRegistry.md) - Coordinates builtin and user-defined symbols

---

## Summary

`BuiltinRegistry` is a foundational component that bridges Sharpy's type system and the .NET runtime library. It:
- Registers primitive types, collections, and special types
- Auto-discovers builtin functions via reflection (with caching)
- Provides fast lookup for type checking and name resolution
- Supports function overloading for better .NET interop

**Key Insight:** This is a "configuration" class, not a "logic" class. Most changes should happen in `Sharpy.Core` (for functions) or `PrimitiveCatalog` (for primitives), not here.

**Performance:** Fast after first run (disk cache). Initialization takes <10ms on cached runs, ~100ms on first run.

**Thread Safety:** Safe for concurrent reads after construction (immutable symbols, readonly dictionaries).
