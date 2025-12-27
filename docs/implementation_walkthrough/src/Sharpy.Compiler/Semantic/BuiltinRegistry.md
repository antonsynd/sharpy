# Walkthrough: BuiltinRegistry.cs

**Source File**: `src/Sharpy.Compiler/Semantic/BuiltinRegistry.cs`

---

## 1. Overview

`BuiltinRegistry` is the **central registry for all builtin types and functions** available in Sharpy programs. Think of it as the compiler's "address book" for everything that's automatically available without an import statement.

**What it does:**
- Registers primitive types (`int`, `float`, `str`, `bool`, etc.)
- Registers collection types (`list[T]`, `dict[K,V]`, `set[T]`)
- Discovers and registers all builtin functions from `Sharpy.Core` (like `print()`, `len()`, `range()`)
- Provides lookup methods for types and functions during semantic analysis

**Role in the compiler pipeline:**
```
Source Code → Lexer → Parser → [Semantic Analysis] → Code Generation
                                        ↑
                                 BuiltinRegistry
                                 (provides type/function info)
```

The `BuiltinRegistry` is created early in the compilation process and is passed to the `SymbolTable`, which uses it to populate the global scope with builtin symbols that every Sharpy program can use.

---

## 2. Class Structure

### Core Fields

```csharp
private readonly Dictionary<string, TypeSymbol> _types = new();
private readonly Dictionary<string, List<FunctionSymbol>> _functions = new();
private readonly CachedModuleDiscovery _discovery;
```

**Field breakdown:**

| Field | Type | Purpose |
|-------|------|---------|
| `_types` | `Dictionary<string, TypeSymbol>` | Maps Sharpy type names (e.g., `"int"`, `"list"`) to their `TypeSymbol` metadata |
| `_functions` | `Dictionary<string, List<FunctionSymbol>>` | Maps function names (e.g., `"print"`, `"len"`) to all their overloads. **Note the `List`** - this supports function overloading! |
| `_discovery` | `CachedModuleDiscovery` | Reflection-based discovery system that scans `Sharpy.Core.dll` to find builtin functions automatically |

### Static Configuration

```csharp
private static readonly HashSet<string> RegisteredPrimitiveNames = new()
{
    "int", "long", "float", "double", "decimal", "bool", "str"
};
```

This whitelist defines which primitives from `PrimitiveCatalog` get registered. The `PrimitiveCatalog` knows about *all* .NET primitives (including `sbyte`, `ushort`, etc.), but we only expose the most commonly used ones to Sharpy programmers.

---

## 3. Key Methods

### 3.1 Constructor: `BuiltinRegistry()`

```csharp
public BuiltinRegistry()
{
    _discovery = new CachedModuleDiscovery();
    LoadBuiltins();
}
```

**What it does:**
- Creates the discovery system (which uses caching to avoid expensive reflection on every compilation)
- Calls `LoadBuiltins()` to populate the registry

**When it's called:** Once per compilation, typically in `Compiler.cs` or test setup.

---

### 3.2 Private Method: `LoadBuiltins()`

This is the **heart of the initialization process**. It runs in three phases:

#### Phase 1: Register Primitives

```csharp
foreach (var (name, info) in PrimitiveCatalog.GetAllPrimitives())
{
    if (!RegisteredPrimitiveNames.Contains(name)) continue;
    if (info.ClrType == typeof(void)) continue;  // void handled specially below
    
    var kind = info.ClrType.IsValueType ? TypeKind.Struct : TypeKind.Class;
    RegisterType(info.SharpyName, info.ClrType, kind);
}
```

**Key points:**
- Uses `PrimitiveCatalog` (a comprehensive registry of *all* .NET primitives) as the source
- Filters to only the primitives in `RegisteredPrimitiveNames`
- Automatically determines if the type is a struct or class based on `ClrType.IsValueType`
- Maps Sharpy names (`"str"`) to CLR types (`typeof(string)`)

#### Phase 2: Register Collections

```csharp
RegisterType("list", typeof(Sharpy.Core.List<>), TypeKind.Class, isGeneric: true, typeParamCount: 1);
RegisterType("dict", typeof(Sharpy.Core.Dict<,>), TypeKind.Class, isGeneric: true, typeParamCount: 2);
RegisterType("set", typeof(Sharpy.Core.Set<>), TypeKind.Class, isGeneric: true, typeParamCount: 1);
```

**Important:** These are **Sharpy's custom collection types** from `Sharpy.Core`, NOT the standard .NET `System.Collections.Generic` types! This is crucial for providing Python-like semantics (negative indexing, slicing, etc.).

**Why explicit registration?** Collections are generic and need special handling. Notice:
- `List<>` has 1 type parameter (`T`)
- `Dict<,>` has 2 type parameters (`K` and `V`)

#### Phase 3: Register Special Types & Load Functions

```csharp
RegisterType("object", typeof(object), TypeKind.Class);
RegisterType("None", typeof(void), TypeKind.Struct);
LoadBuiltinFunctions();
```

- `object` is the root of the type hierarchy
- `None` (Sharpy's equivalent of Python's `None` or C#'s `void`) maps to `typeof(void)` for return types
- Builtin functions are loaded via reflection

---

### 3.3 Private Method: `LoadBuiltinFunctions()`

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

**How it works:**
1. Gets a reference to the `Sharpy.Core.dll` assembly
2. Uses `CachedModuleDiscovery` to scan for functions (with caching for performance)
3. Retrieves all functions marked as "builtins" (e.g., `print()`, `len()`, `range()`)
4. Groups them by name, supporting **multiple overloads** per function name

**Why the `List<FunctionSymbol>`?** Many builtin functions have multiple overloads. For example, `range()` has:
- `range(stop)` - single argument
- `range(start, stop)` - two arguments
- `range(start, stop, step)` - three arguments

All three overloads are stored under the key `"range"`.

---

### 3.4 Private Method: `RegisterType()`

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
            ? Enumerable.Range(0, typeParamCount).Select(i => $"T{i}").ToList()
            : new List<string>(),
        AccessLevel = AccessLevel.Public
    };
    
    _types[sharpyName] = typeSymbol;
}
```

**What it does:** Creates a `TypeSymbol` with all the necessary metadata and adds it to the registry.

**Generic type parameter handling:**
- For non-generic types: empty `TypeParameters` list
- For generic types: synthesizes parameter names (`"T0"`, `"T1"`, etc.)
  - Example: `dict` gets `["T0", "T1"]` for its key and value types

**Why synthesized names?** At this stage, we're registering the *definition* of generic types. Actual type arguments (like `list[int]`) are resolved later during semantic analysis.

---

### 3.5 Public Lookup Methods

#### `GetType(string name)`

```csharp
public TypeSymbol? GetType(string name) => _types.GetValueOrDefault(name);
```

Simple dictionary lookup. Returns `null` if the type isn't registered.

**Usage example:**
```csharp
var intType = builtinRegistry.GetType("int");  // Returns TypeSymbol for int
var customType = builtinRegistry.GetType("MyClass");  // Returns null (not a builtin)
```

#### `GetFunction(string name)`

```csharp
public FunctionSymbol? GetFunction(string name) => 
    _functions.GetValueOrDefault(name)?.FirstOrDefault();
```

Returns the **first overload** of a function. Use this when you just need to check if a function exists.

**⚠️ Important:** For proper overload resolution, use `GetFunctionOverloads()` instead!

#### `GetFunctionOverloads(string name)`

```csharp
public List<FunctionSymbol>? GetFunctionOverloads(string name) => 
    _functions.GetValueOrDefault(name);
```

Returns **all overloads** of a function, or `null` if the function doesn't exist.

**This is the method used by `TypeChecker`** for resolving function calls. The type checker compares argument types against all overloads to find the best match.

---

### 3.6 Enumeration Methods

```csharp
public IEnumerable<(string Name, TypeSymbol Type)> GetAllTypes() => 
    _types.Select(kv => (kv.Key, kv.Value));

public IEnumerable<(string Name, FunctionSymbol Function)> GetAllFunctions() =>
    _functions.SelectMany(kv => kv.Value.Select(f => (kv.Key, f)));
```

**Usage:** Populating the global scope in `SymbolTable`. These methods let the symbol table iterate over all builtins and add them to the global scope so they're available everywhere.

**Note:** `GetAllFunctions()` flattens the overloads - each overload becomes a separate tuple in the result.

---

## 4. Dependencies

### Direct Dependencies

1. **`CachedModuleDiscovery`** (`Discovery/CachedModuleDiscovery.cs`)
   - Reflection-based function discovery with caching
   - Avoids expensive reflection on every compilation
   - Thread-safe for concurrent compilations

2. **`PrimitiveCatalog`** (`Semantic/PrimitiveCatalog.cs`)
   - Comprehensive registry of all .NET primitive types
   - Provides type promotion rules and conversion checking
   - Single source of truth for primitive type metadata

3. **`Symbol` types** (`Semantic/Symbol.cs`)
   - `TypeSymbol`: Represents a type (class, struct, etc.)
   - `FunctionSymbol`: Represents a function/method
   - `ParameterSymbol`: Represents function parameters
   - `SemanticType`: Abstract type representation used in analysis

4. **`Sharpy.Core.Exports`**
   - The actual implementation of builtin functions
   - Uses `partial class Exports` pattern across multiple files
   - Assembly is scanned to discover builtin functions

### Where It's Used

```
BuiltinRegistry
    ↓
SymbolTable (holds a reference, populates global scope)
    ↓
NameResolver, TypeResolver, TypeChecker (access via SymbolTable.BuiltinRegistry)
    ↓
CodeGenContext (passed through for code generation)
```

**Key consumers:**
- **`SymbolTable`**: Stores the registry and populates the global scope
- **`TypeChecker`**: Uses `GetFunctionOverloads()` for overload resolution
- **`Compiler`**: Creates the initial `BuiltinRegistry` instance
- **Test suites**: Create registries for isolated testing

---

## 5. Design Patterns and Decisions

### Pattern 1: Registry Pattern

`BuiltinRegistry` implements the **Registry Pattern** - a centralized location to register and look up related objects. This provides:
- **Single source of truth** for builtin types/functions
- **Lazy initialization** (everything loaded in constructor)
- **Fast lookups** (dictionary-based)

### Pattern 2: Builder Pattern (Implicit)

The `LoadBuiltins()` → `RegisterType()` → `LoadBuiltinFunctions()` flow is a form of the **Builder Pattern**, constructing the registry in discrete, ordered steps.

### Pattern 3: Overload Resolution via Lists

Storing `List<FunctionSymbol>` instead of single `FunctionSymbol` enables **function overloading**:

```csharp
// In Python/Sharpy:
range(5)          # range(stop)
range(1, 5)       # range(start, stop)
range(1, 10, 2)   # range(start, stop, step)
```

All three are registered under the same name `"range"`, and the type checker selects the right one based on argument count and types.

### Decision: Reflection with Caching

**Problem:** Scanning assemblies with reflection is slow.

**Solution:** Use `CachedModuleDiscovery` which:
- Uses reflection on first compilation
- Caches results to disk
- Loads from cache on subsequent compilations
- Dramatically speeds up repeated compilations

### Decision: Whitelist of Primitives

**Why not register all primitives?** 
- Sharpy is designed to be **Pythonic and .NET-first**
- Exposing *all* .NET primitives (`sbyte`, `ushort`, `nint`, etc.) would overwhelm users
- The whitelist (`RegisteredPrimitiveNames`) exposes only commonly-used types
- Advanced users can still access .NET types via interop

### Decision: Sharpy.Core Collections, Not .NET Collections

```csharp
RegisterType("list", typeof(Sharpy.Core.List<>), ...);  // ✅ Sharpy's list
// NOT: typeof(System.Collections.Generic.List<>)        // ❌ .NET's List
```

**Why?** Sharpy's collections provide Python-like semantics:
- Negative indexing: `lst[-1]` (last element)
- Slicing: `lst[1:5]`, `lst[::2]`
- Python-style methods: `append()`, `extend()`, `pop()`, etc.

.NET's collections don't support these features natively.

---

## 6. Debugging Tips

### Tip 1: Verify Builtin Registration

If a type or function isn't being recognized:

```csharp
var builtinRegistry = new BuiltinRegistry();
var type = builtinRegistry.GetType("your_type_name");
if (type == null)
{
    // Type not registered - check:
    // 1. Is it in RegisteredPrimitiveNames?
    // 2. Is it manually registered in LoadBuiltins()?
    // 3. Is it spelled correctly?
}
```

### Tip 2: Inspect Function Overloads

If function overload resolution is failing:

```csharp
var overloads = builtinRegistry.GetFunctionOverloads("function_name");
if (overloads == null)
{
    Console.WriteLine("Function not found!");
}
else
{
    Console.WriteLine($"Found {overloads.Count} overload(s):");
    foreach (var overload in overloads)
    {
        var paramTypes = string.Join(", ", overload.Parameters.Select(p => p.Type));
        Console.WriteLine($"  {overload.Name}({paramTypes}) -> {overload.ReturnType}");
    }
}
```

### Tip 3: Check Sharpy.Core Assembly Loading

If builtin functions aren't being discovered:

```csharp
// In LoadBuiltinFunctions():
var sharpyCoreAssembly = typeof(Sharpy.Core.Exports).Assembly;
Console.WriteLine($"Loaded assembly: {sharpyCoreAssembly.FullName}");

var builtinFunctions = _discovery.GetModuleFunctions("builtins");
Console.WriteLine($"Discovered {builtinFunctions.Count} builtin functions");
```

Common issues:
- `Sharpy.Core.dll` not found (check build output)
- Functions not marked correctly in `Sharpy.Core.Exports`
- Cache is stale (delete cache files in `~/.sharpy/cache/`)

### Tip 4: Trace Symbol Table Population

If builtins aren't available in global scope:

```csharp
// In SymbolTable.PopulateBuiltins():
foreach (var (name, typeSymbol) in _builtins.GetAllTypes())
{
    Console.WriteLine($"Registering type: {name}");
    _globalScope.Define(typeSymbol);
}
```

Verify that:
1. `BuiltinRegistry` was passed to `SymbolTable` constructor
2. `PopulateBuiltins()` was called
3. Symbols are actually being added to `_globalScope`

### Tip 5: PrimitiveCatalog Mismatch

If a primitive type has weird behavior:

```csharp
// Check the PrimitiveCatalog entry:
var primitiveInfo = PrimitiveCatalog.GetByName("int");
Console.WriteLine($"Sharpy name: {primitiveInfo?.SharpyName}");
Console.WriteLine($"C# name: {primitiveInfo?.CSharpName}");
Console.WriteLine($"CLR type: {primitiveInfo?.ClrType}");
```

Ensure the `PrimitiveCatalog` and `BuiltinRegistry` agree on the mapping.

---

## 7. Contribution Guidelines

### Adding a New Primitive Type

**Example:** Adding `decimal` support

1. **Check `PrimitiveCatalog`** - Is it already registered there?
2. **Add to whitelist:**
   ```csharp
   private static readonly HashSet<string> RegisteredPrimitiveNames = new()
   {
       "int", "long", "float", "double", "decimal", "bool", "str", "decimal" // Add here
   };
   ```
3. **Test it:**
   ```csharp
   [Fact]
   public void TestDecimalType()
   {
       var registry = new BuiltinRegistry();
       var decimalType = registry.GetType("decimal");
       Assert.NotNull(decimalType);
       Assert.Equal(typeof(decimal), decimalType.ClrType);
   }
   ```

### Adding a New Collection Type

**Example:** Adding `tuple[T1, T2, ...]` support

1. **Implement in `Sharpy.Core`:**
   ```csharp
   // src/Sharpy.Core/Tuple.cs
   public class Tuple<T1, T2> { /* ... */ }
   ```

2. **Register in `LoadBuiltins()`:**
   ```csharp
   RegisterType("tuple", typeof(Sharpy.Core.Tuple<,>), TypeKind.Class, 
                isGeneric: true, typeParamCount: 2);
   ```

3. **Add tests** in `Sharpy.Compiler.Tests`:
   ```csharp
   [Fact]
   public void TestTupleType()
   {
       var registry = new BuiltinRegistry();
       var tupleType = registry.GetType("tuple");
       Assert.NotNull(tupleType);
       Assert.True(tupleType.IsGeneric);
       Assert.Equal(2, tupleType.TypeParameters.Count);
   }
   ```

### Adding a New Builtin Function

**Good news:** You don't need to modify `BuiltinRegistry`!

1. **Add the function to `Sharpy.Core`:**
   ```csharp
   // src/Sharpy.Core/NewFunction.cs
   public static partial class Exports
   {
       public static void MyNewFunction(string message)
       {
           Console.WriteLine($"New function: {message}");
       }
   }
   ```

2. **Mark it as a builtin** (if needed by discovery mechanism)

3. **Clear the cache:**
   ```bash
   rm -rf ~/.sharpy/cache/
   ```

4. **Rebuild and test:**
   ```bash
   dotnet build
   dotnet test --filter "FullyQualifiedName~Integration"
   ```

The `CachedModuleDiscovery` will automatically find your new function!

### Modifying Discovery Behavior

If you need to change **how** functions are discovered:

1. **Don't modify `BuiltinRegistry`** - it just uses `CachedModuleDiscovery`
2. **Modify `CachedModuleDiscovery`** in `src/Sharpy.Compiler/Discovery/`
3. Consider:
   - Attribute-based filtering
   - Different module namespaces
   - Custom reflection logic

### Performance Improvements

If registry initialization is slow:

1. **Profile with BenchmarkDotNet:**
   ```csharp
   [Benchmark]
   public void CreateRegistry()
   {
       var registry = new BuiltinRegistry();
   }
   ```

2. **Optimize hotspots:**
   - Cache more aggressively
   - Use `FrozenDictionary` for immutable registries
   - Lazy-load function overloads

3. **Measure impact:**
   - Run full compiler test suite
   - Check integration test timings

### Testing Best Practices

**Always test:**
- ✅ Type registration (both generic and non-generic)
- ✅ Function registration (including overloads)
- ✅ Lookup methods (both found and not-found cases)
- ✅ Integration with `SymbolTable`

**Example test structure:**
```csharp
public class BuiltinRegistryTests
{
    [Fact]
    public void RegistersPrimitiveTypes() { /* ... */ }
    
    [Fact]
    public void RegistersCollectionTypes() { /* ... */ }
    
    [Fact]
    public void RegistersBuiltinFunctions() { /* ... */ }
    
    [Fact]
    public void SupportsFunctionOverloads() { /* ... */ }
}
```

---

## 8. Common Pitfalls

### Pitfall 1: Using `GetFunction()` Instead of `GetFunctionOverloads()`

**Wrong:**
```csharp
var rangeFunc = builtinRegistry.GetFunction("range");
// Only gets FIRST overload - wrong!
```

**Right:**
```csharp
var rangeOverloads = builtinRegistry.GetFunctionOverloads("range");
// Gets ALL overloads - correct for type checking
```

### Pitfall 2: Forgetting to Clear Cache

After modifying `Sharpy.Core` builtins, the discovery cache might be stale:

```bash
# Clear the cache:
rm -rf ~/.sharpy/cache/
# Or wherever CachedModuleDiscovery stores its cache
```

### Pitfall 3: Confusing Sharpy.Core vs .NET Types

```csharp
// ❌ WRONG - .NET's List
typeof(System.Collections.Generic.List<>)

// ✅ CORRECT - Sharpy's List
typeof(Sharpy.Core.List<>)
```

Always use Sharpy's types for collections to get Python-like semantics!

### Pitfall 4: Modifying the Registry After Construction

`BuiltinRegistry` is designed to be **immutable after construction**. Don't add types/functions dynamically:

```csharp
// ❌ NO - dictionaries are private
builtinRegistry._types["new_type"] = ...;  // Won't compile

// ✅ YES - modify LoadBuiltins() and reconstruct
// Make changes in LoadBuiltins() method
var newRegistry = new BuiltinRegistry();
```

---

## 9. Related Files

| File | Relationship |
|------|--------------|
| `SymbolTable.cs` | Owns a `BuiltinRegistry` instance; populates global scope with builtins |
| `PrimitiveCatalog.cs` | Source of primitive type metadata; used by `LoadBuiltins()` |
| `CachedModuleDiscovery.cs` | Discovery system for finding builtin functions via reflection |
| `Symbol.cs` | Defines `TypeSymbol` and `FunctionSymbol` used in the registry |
| `TypeChecker.cs` | Uses `GetFunctionOverloads()` for overload resolution |
| `CodeGenContext.cs` | Receives registry for code generation context |
| `src/Sharpy.Core/` | Implementation of all builtin functions and types |

---

## 10. Further Reading

- **PrimitiveCatalog**: See `docs/internal_walkthrough/src/Sharpy.Compiler/Semantic/PrimitiveCatalog.md` (if exists)
- **SymbolTable**: Understand how builtins are used in scoping
- **CachedModuleDiscovery**: Deep dive into the reflection-based discovery system
- **Sharpy.Core**: Browse the standard library to see what's available as builtins

---

## Quick Reference

```csharp
// Create a registry
var registry = new BuiltinRegistry();

// Look up a type
var intType = registry.GetType("int");

// Look up a function (single overload)
var printFunc = registry.GetFunction("print");

// Look up all function overloads
var rangeOverloads = registry.GetFunctionOverloads("range");

// Enumerate all types
foreach (var (name, typeSymbol) in registry.GetAllTypes())
{
    Console.WriteLine($"Type: {name}");
}

// Enumerate all functions (flattened)
foreach (var (name, funcSymbol) in registry.GetAllFunctions())
{
    Console.WriteLine($"Function: {name}");
}
```

---

**Welcome to the team! If you have questions about `BuiltinRegistry`, check the tests in `Sharpy.Compiler.Tests` or ask a senior team member.**
