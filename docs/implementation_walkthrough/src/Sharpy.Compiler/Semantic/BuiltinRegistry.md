# Walkthrough: BuiltinRegistry.cs

**Source File**: `src/Sharpy.Compiler/Semantic/BuiltinRegistry.cs`

---

## 1. Overview

**What is BuiltinRegistry?**

`BuiltinRegistry` is the centralized catalog of all built-in types and functions available in Sharpy programs. It acts as the bridge between Sharpy's Python-like syntax and the underlying .NET runtime types from the `Sharpy.Core` standard library.

**Role in the Compiler Pipeline:**

```
Lexer → Parser → [NameResolver → TypeResolver → TypeChecker] → CodeGen
                       ↑              ↑              ↑
                  BuiltinRegistry (via SymbolTable)
```

The BuiltinRegistry is used indirectly through the `SymbolTable`:
- **Initialization**: Created once per compilation and used to populate the `SymbolTable`'s global scope
- **SymbolTable.PopulateBuiltins()**: Pulls all types and functions from the registry into the global scope
- **Name Resolution**: When code references `int`, `str`, `print()`, `len()`, etc., they resolve to symbols that originated here

**Key Responsibilities:**
- Registers primitive types (`int`, `str`, `bool`, `float`, etc.) from `PrimitiveCatalog`
- Registers generic collection types (`list`, `dict`, `set`)
- Discovers and registers all builtin functions from `Sharpy.Core` via reflection
- Provides lookup methods for types and function overloads
- Maps Sharpy type names to CLR (Common Language Runtime) types

---

## 2. Class Structure

### Main Class: `BuiltinRegistry`

```csharp
public class BuiltinRegistry
{
    private readonly Dictionary<string, TypeSymbol> _types = new();
    private readonly Dictionary<string, List<FunctionSymbol>> _functions = new();
    private readonly CachedModuleDiscovery _discovery;

    private static readonly HashSet<string> RegisteredPrimitiveNames = new()
    {
        "int", "long", "float", "double", "decimal", "bool", "str"
    };
}
```

**Key Data Structures:**

1. **`_types`**: Maps Sharpy type names (`string`) to `TypeSymbol` objects
   - Keys: `"int"`, `"str"`, `"list"`, `"dict"`, etc.
   - Values: Full `TypeSymbol` with metadata (CLR type, kind, generic parameters)

2. **`_functions`**: Maps function names (`string`) to lists of `FunctionSymbol` (for overloads)
   - Keys: `"print"`, `"len"`, `"range"`, etc.
   - Values: `List<FunctionSymbol>` to support function overloading
   - Example: `len()` might have overloads for `len(str)` and `len(list[T])`

3. **`_discovery`**: Instance of `CachedModuleDiscovery`
   - Uses reflection to discover functions from the `Sharpy.Core` assembly
   - Caches results to avoid expensive reflection on subsequent compilations
   - Thread-safe for concurrent use

4. **`RegisteredPrimitiveNames`**: Whitelist of primitive types to register
   - Maintains backward compatibility with original hard-coded type list
   - Not all primitives from `PrimitiveCatalog` are registered (e.g., `int8`, `uint16` are omitted)
   - This controls which types appear in Sharpy's global namespace by default

---

## 3. Key Methods Walkthrough

### 3.1 Constructor: `BuiltinRegistry()`

```csharp
public BuiltinRegistry()
{
    _discovery = new CachedModuleDiscovery();
    LoadBuiltins();
}
```

**What it does:**
- Creates a new `CachedModuleDiscovery` instance for function discovery
- Immediately calls `LoadBuiltins()` to populate all types and functions

**When it's called:**
Created once during compiler initialization in `Compiler.cs`:

```csharp
var builtinRegistry = new BuiltinRegistry();
var symbolTable = new SymbolTable(builtinRegistry);
```

**Performance Note:**
The first time this runs, it performs reflection over `Sharpy.Core`. Subsequent compilations use cached data, making startup much faster.

---

### 3.2 `LoadBuiltins()`

```csharp
private void LoadBuiltins()
{
    // Step 1: Register primitive types from PrimitiveCatalog
    foreach (var (name, info) in PrimitiveCatalog.GetAllPrimitives())
    {
        if (!RegisteredPrimitiveNames.Contains(name)) continue;
        if (info.ClrType == typeof(void)) continue;

        var kind = info.ClrType.IsValueType ? TypeKind.Struct : TypeKind.Class;
        RegisterType(info.SharpyName, info.ClrType, kind);
    }

    // Step 2: Register generic collection types
    RegisterType("list", typeof(Sharpy.Core.List<>), TypeKind.Class, isGeneric: true, typeParamCount: 1);
    RegisterType("dict", typeof(Sharpy.Core.Dict<,>), TypeKind.Class, isGeneric: true, typeParamCount: 2);
    RegisterType("set", typeof(Sharpy.Core.Set<>), TypeKind.Class, isGeneric: true, typeParamCount: 1);

    // Step 3: Register special types
    RegisterType("object", typeof(object), TypeKind.Class);
    RegisterType("None", typeof(void), TypeKind.Struct); // for return types

    // Step 4: Load builtin functions via reflection
    LoadBuiltinFunctions();
}
```

**What it does:**
This is the main initialization logic that populates the registry in four distinct steps:

**Step 1: Primitive Types**
- Iterates through `PrimitiveCatalog.GetAllPrimitives()`
- Filters to only include types in `RegisteredPrimitiveNames`
- Skips `void` (registered separately as `"None"`)
- Determines `TypeKind` (Struct vs Class) based on whether it's a value type
- Registers: `int`, `long`, `float`, `double`, `decimal`, `bool`, `str`

**Step 2: Generic Collections**
- Manually registers `list<T>`, `dict<K,V>`, `set<T>`
- IMPORTANT: These are `Sharpy.Core.List<>`, NOT `System.Collections.Generic.List<>`!
- `typeParamCount` tells the compiler how many type arguments are required

**Step 3: Special Types**
- `object`: Base type for all reference types
- `None`: Sharpy's equivalent of Python's `None` type, mapped to C#'s `void`

**Step 4: Builtin Functions**
- Delegates to `LoadBuiltinFunctions()` for reflection-based discovery

**Design Decision:**
Why not register all primitives from `PrimitiveCatalog`? The whitelist approach (`RegisteredPrimitiveNames`) ensures backward compatibility and a curated set of types in the global namespace. Types like `int8`, `uint16`, etc., are available but not automatically imported.

---

### 3.3 `LoadBuiltinFunctions()`

```csharp
private void LoadBuiltinFunctions()
{
    // Load Sharpy.Core assembly and discover all builtin functions
    var sharpyCoreAssembly = typeof(Sharpy.Core.Exports).Assembly;
    _discovery.LoadAssembly(sharpyCoreAssembly);

    // Get all functions from the "builtins" module
    var builtinFunctions = _discovery.GetModuleFunctions("builtins");

    // Register them in our internal dictionary
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

**What it does:**
1. Gets the `Sharpy.Core` assembly by referencing the `Exports` type
2. Uses `CachedModuleDiscovery` to reflect over the assembly and build a function index
3. Retrieves all functions from the `"builtins"` module
4. Stores them in `_functions` dictionary, grouping overloads by name

**How it discovers functions:**
The `CachedModuleDiscovery` class:
- Scans for classes marked with `[SharpyModule("builtins")]` attribute
- Finds public static methods marked with `[SharpyFunction]` attribute
- Builds `FunctionSymbol` objects with parameter types and return types
- Caches results to disk for faster subsequent loads

**Example of what gets loaded:**
```csharp
// From Sharpy.Core:
[SharpyModule("builtins")]
public static class BuiltinFunctions
{
    [SharpyFunction("print")]
    public static void Print(object value) { ... }

    [SharpyFunction("len")]
    public static int Len(string s) { ... }

    [SharpyFunction("len")]
    public static int Len<T>(List<T> list) { ... }  // Overload!
}
```

**Concurrency Note:**
The comment on line 68 mentions "no concurrent access is expected here" because this method is only called from the constructor, before the `BuiltinRegistry` is exposed to other threads.

---

### 3.4 `RegisterType()`

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
            ? Enumerable.Range(0, typeParamCount).Select(i => new TypeParameterDef { Name = $"T{i}" }).ToList()
            : new List<TypeParameterDef>(),
        AccessLevel = AccessLevel.Public
    };

    _types[sharpyName] = typeSymbol;
}
```

**What it does:**
Creates a `TypeSymbol` from a CLR type and registers it under a Sharpy name.

**Parameters:**
- `sharpyName`: The name used in Sharpy code (e.g., `"str"`, `"list"`)
- `clrType`: The .NET type (e.g., `typeof(string)`, `typeof(Sharpy.Core.List<>)`)
- `kind`: Whether it's a `Class` or `Struct` (`TypeKind` enum)
- `isGeneric`: Whether this is a generic type
- `typeParamCount`: How many type parameters (e.g., 1 for `List<T>`, 2 for `Dict<K,V>`)

**Type Parameters:**
For generic types, it generates synthetic type parameter names:
- 1 parameter: `["T0"]` → `list[T0]`
- 2 parameters: `["T0", "T1"]` → `dict[T0, T1]`

These names are placeholders; the actual type arguments come from usage sites in source code.

**Example Usage:**
```csharp
RegisterType("list", typeof(Sharpy.Core.List<>), TypeKind.Class, isGeneric: true, typeParamCount: 1);
// Creates TypeSymbol:
//   Name: "list"
//   ClrType: Sharpy.Core.List<>
//   TypeParameters: [{ Name: "T0" }]
```

---

### 3.5 `GetType()`, `GetFunction()`, `GetFunctionOverloads()`

```csharp
public TypeSymbol? GetType(string name)
    => _types.GetValueOrDefault(name);

public FunctionSymbol? GetFunction(string name)
    => _functions.GetValueOrDefault(name)?.FirstOrDefault();

public List<FunctionSymbol>? GetFunctionOverloads(string name)
    => _functions.GetValueOrDefault(name);
```

**What they do:**
Simple lookup methods used by `SymbolTable` to populate the global scope.

**`GetType(string name)`:**
- Returns the `TypeSymbol` for a given Sharpy type name, or `null` if not found
- Example: `GetType("int")` returns the `TypeSymbol` with `ClrType = typeof(int)`

**`GetFunction(string name)`:**
- Returns the **first** overload of a function with the given name
- Convenient for functions with no overloads (most builtins)
- For overloaded functions, the returned overload is arbitrary (first in list)

**`GetFunctionOverloads(string name)`:**
- Returns **all** overloads of a function
- Used during function call resolution to find the correct overload
- Returns `null` if no function with that name exists
- Example: `GetFunctionOverloads("len")` might return 2+ `FunctionSymbol` objects

**Usage Pattern:**
```csharp
// In SymbolTable.PopulateBuiltins():
foreach (var (name, typeSymbol) in _builtins.GetAllTypes())
{
    Define(typeSymbol); // Add to global scope
}

foreach (var (name, functionSymbol) in _builtins.GetAllFunctions())
{
    Define(functionSymbol); // Add to global scope
}
```

---

### 3.6 `GetAllTypes()`, `GetAllFunctions()`

```csharp
public IEnumerable<(string Name, TypeSymbol Type)> GetAllTypes()
    => _types.Select(kv => (kv.Key, kv.Value));

public IEnumerable<(string Name, FunctionSymbol Function)> GetAllFunctions()
    => _functions.SelectMany(kv => kv.Value.Select(f => (kv.Key, f)));
```

**What they do:**
Iterator methods that return all registered types and functions.

**`GetAllTypes()`:**
- Returns tuples of `(Name, TypeSymbol)` for all registered types
- Used by `SymbolTable.PopulateBuiltins()` to add types to global scope

**`GetAllFunctions()`:**
- Returns tuples of `(Name, FunctionSymbol)` for **every function overload**
- Uses `SelectMany` to flatten the `Dictionary<string, List<FunctionSymbol>>` into individual tuples
- If `len` has 3 overloads, this yields 3 separate `("len", FunctionSymbol)` tuples

**Why return tuples?**
This API is more convenient for the caller (`SymbolTable`) because it provides both the name and the symbol together, avoiding redundant lookups.

---

## 4. Dependencies

### Internal Dependencies

**`PrimitiveCatalog`** (`Sharpy.Compiler.Semantic`)
- Provides exhaustive catalog of all primitive types supported by Sharpy
- Used to register types like `int`, `str`, `bool`, `float`, etc.
- See: [`PrimitiveCatalog.md`](PrimitiveCatalog.md)

**`CachedModuleDiscovery`** (`Sharpy.Compiler.Discovery`)
- Reflection-based discovery of functions from .NET assemblies
- Caches results to avoid expensive reflection on subsequent runs
- Thread-safe and uses frozen dictionaries for performance

**`Symbol` types** (`Sharpy.Compiler.Semantic`)
- `TypeSymbol`: Represents a type (class, struct, interface, enum)
- `FunctionSymbol`: Represents a function or method
- `ParameterSymbol`: Represents a function parameter
- See: [`Symbol.md`](Symbol.md)

**AST Types** (`Sharpy.Compiler.Parser.Ast`)
- `TypeParameterDef`: Represents a generic type parameter (e.g., `T` in `List<T>`)
- Used when constructing `TypeSymbol` objects for generic types

### External Dependencies

**`Sharpy.Core` Assembly**
- The standard library that provides implementations of builtin types and functions
- Contains types like `Sharpy.Core.List<T>`, `Sharpy.Core.Dict<K,V>`
- Contains builtin functions marked with `[SharpyFunction]` attributes
- This assembly is **not** part of the compiler; it's a runtime dependency

---

## 5. Patterns and Design Decisions

### 5.1 Separation of Type Catalog and Symbol Registry

**Pattern:**
`PrimitiveCatalog` is a static registry of type **information**, while `BuiltinRegistry` creates **symbols** from that information.

**Why?**
- `PrimitiveCatalog` is used by multiple components (TypeChecker, CodeGen, etc.) for type queries
- `BuiltinRegistry` is specific to the semantic analysis phase
- This separation allows other parts of the compiler to reason about primitives without needing symbols

### 5.2 Reflection-Based Function Discovery

**Pattern:**
Instead of hard-coding every builtin function, the registry uses reflection over `Sharpy.Core` to discover them automatically.

**Benefits:**
- **Extensibility**: New builtins can be added to `Sharpy.Core` without modifying the compiler
- **Single Source of Truth**: Function signatures come directly from the implementation
- **Reduced Maintenance**: No need to keep compiler and runtime in sync manually

**Trade-off:**
- Initial startup cost for reflection (mitigated by caching)
- Requires attributes (`[SharpyModule]`, `[SharpyFunction]`) in `Sharpy.Core`

### 5.3 Function Overload Storage

**Pattern:**
Functions are stored as `Dictionary<string, List<FunctionSymbol>>` rather than `Dictionary<string, FunctionSymbol>`.

**Why?**
- Supports function overloading (multiple functions with the same name but different signatures)
- Example: `len(str)` vs `len(list[T])` vs `len(dict[K,V])`
- During function call resolution, the type checker picks the best overload based on argument types

**Implementation Detail:**
The `GetFunction()` method returns `FirstOrDefault()` for convenience, but serious overload resolution happens in the type checker using `GetFunctionOverloads()`.

### 5.4 Curated Primitive Whitelist

**Pattern:**
`RegisteredPrimitiveNames` is a hardcoded set rather than automatically registering all primitives from `PrimitiveCatalog`.

**Why?**
- **Backward Compatibility**: Preserves the original set of builtins from early Sharpy versions
- **Namespace Control**: Prevents global namespace pollution with rarely-used types
- **Opt-in Complexity**: Types like `int8`, `uint16` are available but require explicit import

**Future Evolution:**
This could evolve to support a `from sharpy import int8, uint16` pattern for accessing the full primitive catalog.

### 5.5 Generic Type Parameter Naming

**Pattern:**
Generic types use synthetic names `T0`, `T1`, etc., rather than meaningful names like `TKey`, `TValue`.

**Current State:**
This is a simplification; the actual type parameter names from the CLR types are ignored.

**Impact:**
- Error messages will show `dict[T0, T1]` instead of `dict[TKey, TValue]`
- This is acceptable for now but could be improved by reading actual CLR type parameter names via reflection

---

## 6. Debugging Tips

### 6.1 "Type 'X' not found" Errors

**Symptom:**
Compiler reports that a type like `int` or `str` is not found.

**Likely Causes:**
1. `BuiltinRegistry` wasn't initialized properly
2. `SymbolTable.PopulateBuiltins()` wasn't called
3. Type name is misspelled in `RegisteredPrimitiveNames`

**How to Debug:**
- Set a breakpoint in `BuiltinRegistry()` constructor
- Step through `LoadBuiltins()` and verify that `_types` dictionary is populated
- Check that `SymbolTable.PopulateBuiltins()` is called after creating the symbol table
- Use debugger watch on `_types.Count` (should be ~10-15 types)

### 6.2 "Function 'X' not found" Errors

**Symptom:**
Compiler reports that a builtin function like `print` or `len` is not found.

**Likely Causes:**
1. Function isn't marked with `[SharpyFunction]` in `Sharpy.Core`
2. Function is in a different module than `"builtins"`
3. Reflection-based discovery failed
4. Cache is stale and needs to be cleared

**How to Debug:**
- Set a breakpoint in `LoadBuiltinFunctions()`
- Check that `builtinFunctions` list is populated (should be ~20+ functions)
- Verify that `_functions` dictionary contains the expected function names
- Try deleting the cache directory and recompiling (cache location is platform-specific)
- Inspect `Sharpy.Core` source to verify function has correct attributes

### 6.3 Generic Type Instantiation Issues

**Symptom:**
Code like `list[int]` fails to compile or generates incorrect C# code.

**Likely Causes:**
1. `typeParamCount` is incorrect in `RegisterType()` call
2. Type resolver isn't correctly handling generic type arguments

**How to Debug:**
- Verify that `_types["list"].TypeParameters.Count == 1`
- Check that `ClrType` is the open generic type (`Sharpy.Core.List<>`)
- Set breakpoints in `TypeResolver` to see how `list[int]` is being processed

### 6.4 Inspecting Loaded Builtins

**Useful Debugging Snippet:**
```csharp
// Add this at the end of LoadBuiltins() for debugging
Console.WriteLine($"Loaded {_types.Count} types:");
foreach (var (name, typeSymbol) in _types)
{
    Console.WriteLine($"  {name} -> {typeSymbol.ClrType?.FullName}");
}

Console.WriteLine($"Loaded {_functions.Count} function names:");
foreach (var (name, overloads) in _functions)
{
    Console.WriteLine($"  {name} ({overloads.Count} overload(s))");
}
```

---

## 7. Contribution Guidelines

### 7.1 Adding New Primitive Types

**When to add a type to `RegisteredPrimitiveNames`:**
- The type should be commonly used in Sharpy programs
- It should be available in the global namespace without explicit import
- It should have a clear use case that isn't covered by existing types

**Steps:**
1. Add the type name to `RegisteredPrimitiveNames` set
2. Ensure the type exists in `PrimitiveCatalog`
3. Add tests for the new type in `SemanticAnalyzerTests`
4. Document the type in the language specification

**Example:**
```csharp
private static readonly HashSet<string> RegisteredPrimitiveNames = new()
{
    "int", "long", "float", "double", "decimal", "bool", "str",
    "byte" // NEW: Add byte type to global namespace
};
```

### 7.2 Adding New Builtin Functions

**Steps:**
1. Add the function to `Sharpy.Core` with proper attributes:
   ```csharp
   [SharpyModule("builtins")]
   public static class BuiltinFunctions
   {
       [SharpyFunction("my_func")]
       public static int MyFunc(string arg) { ... }
   }
   ```
2. No changes to `BuiltinRegistry.cs` are needed! Reflection handles it automatically.
3. Clear the cache (or increment version) to force re-discovery
4. Add tests for the new function in `SemanticAnalyzerTests`
5. Document the function in the language specification

### 7.3 Adding New Collection Types

**When to add a collection:**
- It provides significant value beyond `list`, `dict`, `set`
- Examples: `tuple`, `array`, `frozenset`

**Steps:**
1. Implement the type in `Sharpy.Core` (e.g., `Sharpy.Core.Tuple<T1, T2>`)
2. Add a `RegisterType()` call in `LoadBuiltins()`:
   ```csharp
   RegisterType("tuple", typeof(Sharpy.Core.Tuple<,>), TypeKind.Struct,
                isGeneric: true, typeParamCount: 2);
   ```
3. Add tests for type checking, generic instantiation, and code generation
4. Document the type in the language specification

### 7.4 Modifying Reflection-Based Discovery

**When to modify:**
- Changing how functions are discovered from assemblies
- Adding support for new function metadata (e.g., deprecation warnings)
- Optimizing cache storage or lookup

**Files to modify:**
- `CachedModuleDiscovery.cs` - Main discovery logic
- `OverloadIndexBuilder.cs` - Builds function index from reflection
- `OverloadIndexCache.cs` - Serialization and caching
- This file (`BuiltinRegistry.cs`) only if changing the API

**Be careful with:**
- Cache invalidation: Increment version number if changing the cache format
- Thread safety: Discovery is used concurrently in multi-project compilations

---

## 8. Cross-References

### Related Documentation

**Companion Files:**
- [`PrimitiveCatalog.md`](PrimitiveCatalog.md) - Exhaustive primitive type registry
- [`SymbolTable.md`](SymbolTable.md) - Uses BuiltinRegistry to populate global scope
- [`Symbol.md`](Symbol.md) - Definitions of TypeSymbol, FunctionSymbol, etc.

**Discovery Infrastructure:**
- `CachedModuleDiscovery.cs` - Reflection-based function discovery
- `OverloadIndexBuilder.cs` - Builds function index from assemblies
- `TypeMapper.cs` - Maps CLR types to Sharpy semantic types

**Downstream Consumers:**
- `NameResolver.cs` - Resolves names to symbols from SymbolTable (populated by BuiltinRegistry)
- `TypeResolver.cs` - Resolves type annotations to SemanticType objects
- `TypeChecker.cs` - Validates expressions against types from SymbolTable

**Upstream Providers:**
- `Sharpy.Core` assembly - Contains all builtin type implementations and functions
- `PrimitiveCatalog.cs` - Static catalog of primitive type metadata

---

## 9. Historical Context

### Evolution of Builtin Discovery

**Original Implementation (pre-reflection):**
All builtins were hard-coded in `BuiltinRegistry`:
```csharp
RegisterFunction("print", ...);
RegisterFunction("len", ...);
RegisterFunction("range", ...);
// ... 50+ more lines
```

**Problems:**
- High maintenance burden: Any change to `Sharpy.Core` required updating the compiler
- Inconsistencies: Function signatures could get out of sync
- No overload support: Each overload had to be manually registered

**Current Implementation (reflection-based):**
Functions are discovered automatically from `Sharpy.Core` assembly via reflection and cached for performance.

**Benefits:**
- Single source of truth: `Sharpy.Core` is authoritative
- Automatic overload support
- Easy to extend: Just add functions to `Sharpy.Core`

### Why Keep RegisteredPrimitiveNames?

This whitelist approach was retained for backward compatibility. In the future, Sharpy may adopt a more flexible system where:
- All primitives from `PrimitiveCatalog` are available via qualified names
- A default set is imported into the global namespace
- Users can customize which types are auto-imported

---

## 10. Future Improvements

### 10.1 Better Generic Type Parameter Names

**Current State:**
Generic types use synthetic names like `T0`, `T1`.

**Proposed:**
Extract actual type parameter names from CLR types via reflection:
```csharp
var genericArgs = clrType.GetGenericArguments();
TypeParameters = genericArgs.Select(arg => new TypeParameterDef { Name = arg.Name }).ToList();
```

**Benefits:**
- Better error messages: `dict[TKey, TValue]` instead of `dict[T0, T1]`
- More intuitive for users coming from C# or other statically-typed languages

### 10.2 Lazy Loading of Builtins

**Current State:**
All builtins are loaded eagerly during `BuiltinRegistry()` construction.

**Proposed:**
Load types/functions on-demand as they are referenced in code:
```csharp
public TypeSymbol? GetType(string name)
{
    if (_types.TryGetValue(name, out var symbol))
        return symbol;

    // Try lazy load from PrimitiveCatalog or Sharpy.Core
    return TryLazyLoadType(name);
}
```

**Benefits:**
- Faster compiler startup for programs that don't use all builtins
- Reduced memory footprint

**Challenges:**
- More complex cache invalidation
- Thread-safety concerns for lazy initialization

### 10.3 Module-Scoped Builtins

**Current State:**
All builtins are in the global `"builtins"` module.

**Proposed:**
Support multiple builtin modules like Python:
- `builtins` - Core functions (`print`, `len`, etc.)
- `sys` - System functions
- `os` - Operating system interface
- `math` - Mathematical functions

**Implementation:**
Modify `LoadBuiltinFunctions()` to load multiple modules:
```csharp
foreach (var moduleName in new[] { "builtins", "sys", "os", "math" })
{
    var functions = _discovery.GetModuleFunctions(moduleName);
    // Register in module-specific namespace
}
```

### 10.4 User-Defined Builtins

**Use Case:**
Allow users to extend the global namespace with custom builtins for their domain.

**Example:**
```csharp
var registry = new BuiltinRegistry();
registry.RegisterCustomType("MyCustomType", typeof(MyNamespace.MyCustomType));
registry.RegisterCustomFunction("my_func", typeof(MyNamespace.MyFunctions).GetMethod("MyFunc"));
```

**Benefits:**
- DSL support: Create domain-specific languages on top of Sharpy
- Compiler plugins: Third-party libraries can extend Sharpy's type system

---

**End of Walkthrough**

For questions or improvements to this documentation, please open an issue or submit a pull request to the Sharpy repository.
