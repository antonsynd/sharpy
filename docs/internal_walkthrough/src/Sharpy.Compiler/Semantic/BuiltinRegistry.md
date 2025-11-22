# Walkthrough: BuiltinRegistry.cs

**Source File**: `src/Sharpy.Compiler/Semantic/BuiltinRegistry.cs`

---

## Overview

`BuiltinRegistry` is the **central registry** for all built-in types and functions that are available by default in every Sharpy program. Think of it as the "standard library catalog" that the compiler consults whenever it encounters code like `print("hello")`, `len(my_list)`, or type annotations like `int`, `str`, `list[T]`.

**Key Responsibilities:**
- Register Python-like primitive types (`int`, `str`, `bool`, `float`, etc.) and map them to their .NET equivalents
- Register generic collection types (`list[T]`, `dict[K,V]`, `set[T]`)
- Automatically discover and register all builtin functions from `Sharpy.Core` assembly using reflection
- Provide fast lookup for types and functions during semantic analysis and type checking

**When It's Used:**
- **During compiler initialization** - Created once at the start of compilation
- **During type checking** - When the `TypeChecker` needs to verify if `int`, `str`, or `list[T]` is a valid type
- **During name resolution** - When the `NameResolver` encounters calls to builtin functions like `print()`, `len()`, or `range()`
- **During overload resolution** - When the `TypeChecker` needs to find the correct overload of a builtin function

**Performance Note:**
The registry uses the `CachedModuleDiscovery` system, which provides **4-7x speedup** by caching function signatures to disk. The first compilation of a project may take a bit longer (milliseconds) as it builds the cache, but subsequent compilations are much faster.

---

## Class Structure

### Main Class: `BuiltinRegistry`

```csharp
public class BuiltinRegistry
{
    private readonly Dictionary<string, TypeSymbol> _types = new();
    private readonly Dictionary<string, List<FunctionSymbol>> _functions = new();
    private readonly CachedModuleDiscovery _discovery;
}
```

**Field Breakdown:**

| Field | Type | Purpose |
|-------|------|---------|
| `_types` | `Dictionary<string, TypeSymbol>` | Maps Sharpy type names (`"int"`, `"str"`, `"list"`) to their type symbols |
| `_functions` | `Dictionary<string, List<FunctionSymbol>>` | Maps function names to **lists** of function symbols (supports overloading) |
| `_discovery` | `CachedModuleDiscovery` | Handles reflection-based discovery of functions from `Sharpy.Core` with caching |

**Why lists for functions?**
Many builtin functions have multiple overloads. For example, `range()` has three overloads:
- `range(stop: int)` - e.g., `range(10)`
- `range(start: int, stop: int)` - e.g., `range(5, 10)`
- `range(start: int, stop: int, step: int)` - e.g., `range(0, 10, 2)`

The `_functions` dictionary stores all overloads in a list, and the `TypeChecker` later selects the correct one based on argument types.

---

## Key Methods

### 1. Constructor

```csharp
public BuiltinRegistry()
{
    _discovery = new CachedModuleDiscovery();
    LoadBuiltins();
}
```

**What it does:**
1. Creates a `CachedModuleDiscovery` instance (using default cache directory `~/.sharpy/cache/overload-index/`)
2. Calls `LoadBuiltins()` to populate the registry

**When it's called:**
- Once per compilation, typically in `Compiler.cs` or test setup code
- Example: `var builtinRegistry = new BuiltinRegistry();`

**Important:** This constructor does I/O (disk cache reads/writes) and reflection, so it takes a few milliseconds on first run. Subsequent runs are much faster due to caching.

---

### 2. LoadBuiltins - The Initialization Pipeline

```csharp
private void LoadBuiltins()
{
    // Numeric types
    RegisterType("int", typeof(int), TypeKind.Struct);
    RegisterType("long", typeof(long), TypeKind.Struct);
    RegisterType("float", typeof(float), TypeKind.Struct);
    RegisterType("double", typeof(double), TypeKind.Struct);
    RegisterType("decimal", typeof(decimal), TypeKind.Struct);

    // Boolean
    RegisterType("bool", typeof(bool), TypeKind.Struct);

    // String
    RegisterType("str", typeof(string), TypeKind.Class);

    // Collections (generic)
    RegisterType("list", typeof(System.Collections.Generic.List<>), TypeKind.Class, 
                 isGeneric: true, typeParamCount: 1);
    RegisterType("dict", typeof(System.Collections.Generic.Dictionary<,>), TypeKind.Class, 
                 isGeneric: true, typeParamCount: 2);
    RegisterType("set", typeof(System.Collections.Generic.HashSet<>), TypeKind.Class, 
                 isGeneric: true, typeParamCount: 1);

    // Special
    RegisterType("object", typeof(object), TypeKind.Class);
    RegisterType("None", typeof(void), TypeKind.Struct); // void for return type

    // Load builtin functions using reflection-based discovery
    LoadBuiltinFunctions();
}
```

**What it does:**
This is a two-phase initialization:

**Phase 1: Type Registration** (lines 24-43)
- Registers all primitive types that Sharpy programs can use
- Maps Sharpy names (`"int"`, `"str"`, `"list"`) to .NET types (`typeof(int)`, `typeof(string)`, etc.)
- Handles generic types specially (see the `isGeneric: true` parameter)

**Phase 2: Function Discovery** (line 46)
- Delegates to `LoadBuiltinFunctions()` which uses reflection to find all functions in `Sharpy.Core`

**Key Design Decisions:**

1. **Types are manually registered** - This is intentional! Types are a small, stable set, so manual registration is fine and gives us precise control over naming.

2. **Functions are auto-discovered** - There are many builtin functions (`print()`, `len()`, `range()`, `enumerate()`, `filter()`, `map()`, etc.) and they have multiple overloads. Auto-discovery via reflection is much more maintainable than manual registration.

3. **Generic types need special handling** - Notice how `list`, `dict`, and `set` have `isGeneric: true` and `typeParamCount`. This tells the compiler that `list` needs a type argument (`list[int]`) and won't work by itself.

**Naming Convention:**
- `"None"` maps to `typeof(void)` - This is for return types like `def greet(name: str) -> None:`
- Python-style names (`"str"`, `"int"`) map to C# types (`string`, `int`)
- This allows Sharpy code to use familiar Python syntax while compiling to .NET

---

### 3. LoadBuiltinFunctions - Auto-Discovery via Reflection

```csharp
private void LoadBuiltinFunctions()
{
    // Load Sharpy.Core assembly and discover all builtin functions automatically
    var sharpyCoreAssembly = typeof(Sharpy.Core.Exports).Assembly;
    _discovery.LoadAssembly(sharpyCoreAssembly);

    // Get all functions from the "builtins" module
    var builtinFunctions = _discovery.GetModuleFunctions("builtins");

    // Register them in our internal dictionary
    // Note: This is called during construction, so no concurrent access is expected here
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
1. Gets the `Sharpy.Core` assembly (which contains all builtin functions)
2. Passes it to `CachedModuleDiscovery.LoadAssembly()` which either:
   - Loads cached function signatures from disk (fast path, 4-7x faster)
   - Uses reflection to discover functions and caches them (slow path, first run only)
3. Retrieves all functions from the `"builtins"` module
4. Groups them by name in `_functions` dictionary

**How the "builtins" module works:**
In `Sharpy.Core`, there's a special `Exports` class marked with a `[Module("builtins")]` attribute:

```csharp
// In Sharpy.Core/Builtins/Exports.cs
namespace Sharpy.Core;

public static partial class Exports
{
    public static void Print(params object?[] values) { ... }
    public static int Len(object obj) { ... }
    // ... more builtin functions
}
```

The `CachedModuleDiscovery` uses reflection to find all public static methods in classes marked with `[Module("builtins")]` and converts them to `FunctionSymbol` objects.

**Why this approach?**
- **Maintainability**: Adding a new builtin function to `Sharpy.Core` automatically makes it available in the compiler
- **Performance**: The cache avoids expensive reflection on every compilation
- **Type Safety**: The compiler sees the exact signatures with all parameter types and return types
- **Overload Support**: Multiple methods with the same name (like `range(int)`, `range(int, int)`, `range(int, int, int)`) are all discovered and stored

---

### 4. RegisterType - Type Symbol Creation

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

**What it does:**
Creates a `TypeSymbol` (defined in `Symbol.cs`) and stores it in the `_types` dictionary.

**Parameters:**
- `sharpyName` - The name used in Sharpy code (e.g., `"int"`, `"list"`)
- `clrType` - The corresponding .NET type (e.g., `typeof(int)`, `typeof(List<>)`)
- `kind` - Either `TypeKind.Class` or `TypeKind.Struct` (determines if it's passed by value or reference)
- `isGeneric` - Whether the type needs type parameters
- `typeParamCount` - How many type parameters (1 for `list`, 2 for `dict`)

**Generic Type Parameters:**
For generic types like `list`, the compiler generates placeholder names `["T0"]`, and for `dict` it generates `["T0", "T1"]`. These are later replaced with actual types during type checking.

**Example:**
```csharp
RegisterType("list", typeof(List<>), TypeKind.Class, isGeneric: true, typeParamCount: 1);
// Creates a TypeSymbol with TypeParameters = ["T0"]
```

When you write `list[int]` in Sharpy code, the type checker:
1. Looks up `"list"` in the registry → gets `TypeSymbol` with `TypeParameters = ["T0"]`
2. Substitutes `T0` with `int`
3. Creates a concrete type `List<int>`

---

### 5. GetType - Type Lookup

```csharp
public TypeSymbol? GetType(string name) => _types.GetValueOrDefault(name);
```

**What it does:**
Simple dictionary lookup. Returns `null` if the type doesn't exist.

**Used by:**
- `TypeChecker` - When validating type annotations like `x: int`
- `TypeResolver` - When resolving type references in function signatures
- `CodeGenContext` - When mapping Sharpy types to C# types

**Example:**
```csharp
var intType = builtins.GetType("int");      // Returns TypeSymbol for int
var fooType = builtins.GetType("Foo");      // Returns null (not a builtin)
```

---

### 6. GetFunction - Single Function Lookup

```csharp
/// <summary>
/// Returns the first function symbol with the given name.
/// For functions with multiple overloads, use GetFunctionOverloads instead.
/// </summary>
public FunctionSymbol? GetFunction(string name) 
    => _functions.GetValueOrDefault(name)?.FirstOrDefault();
```

**What it does:**
Returns the **first** function with the given name. If there are multiple overloads, only the first is returned.

**When to use:**
- When you know there's only one overload
- Legacy code that hasn't been updated for overload support
- Quick checks like "does this function exist?"

**When NOT to use:**
- For functions with multiple overloads (like `range()`, `print()`, etc.)
- During overload resolution in the type checker

**Warning:** This method is somewhat deprecated in favor of `GetFunctionOverloads()`.

---

### 7. GetFunctionOverloads - Proper Overload Lookup

```csharp
/// <summary>
/// Returns all function overloads with the given name, or null if no function with that name exists.
/// </summary>
public List<FunctionSymbol>? GetFunctionOverloads(string name) 
    => _functions.GetValueOrDefault(name);
```

**What it does:**
Returns **all** overloads of a function, or `null` if the function doesn't exist.

**Used by:**
- `TypeChecker.CheckCall()` - When resolving which overload to use for a function call
- Overload resolution algorithms

**Example:**
```csharp
var rangeOverloads = builtins.GetFunctionOverloads("range");
// Returns List<FunctionSymbol> with 3 items:
// - range(stop: int)
// - range(start: int, stop: int)
// - range(start: int, stop: int, step: int)

// The TypeChecker then picks the right one based on arguments:
// range(10)           → picks first overload
// range(5, 10)        → picks second overload
// range(0, 10, 2)     → picks third overload
```

**Why this is important:**
The Sharpy compiler supports function overloading (multiple functions with the same name but different parameters). This is common in builtin functions:
- `print()` can take any number of arguments
- `len()` works on different types (strings, lists, dicts, sets)
- `range()` has 3 different parameter combinations

---

### 8. GetAllTypes - Enumeration for Debugging

```csharp
public IEnumerable<(string Name, TypeSymbol Type)> GetAllTypes() 
    => _types.Select(kv => (kv.Key, kv.Value));
```

**What it does:**
Returns all registered types as tuples of `(Name, TypeSymbol)`.

**Used for:**
- Debugging and diagnostics
- Compiler error messages that list available types
- Tool support (LSP, IDE autocompletion)

**Example:**
```csharp
foreach (var (name, type) in builtins.GetAllTypes())
{
    Console.WriteLine($"{name} -> {type.ClrType.Name}");
}
// Output:
// int -> Int32
// str -> String
// list -> List`1
// dict -> Dictionary`2
// ...
```

---

### 9. GetAllFunctions - Enumeration for Debugging

```csharp
public IEnumerable<(string Name, FunctionSymbol Function)> GetAllFunctions() =>
    _functions.SelectMany(kv => kv.Value.Select(f => (kv.Key, f)));
```

**What it does:**
Returns all registered functions, **flattening overloads** into individual tuples.

**Used for:**
- Debugging and diagnostics
- Listing available builtin functions in error messages
- Tool support (LSP, IDE autocompletion)

**Example:**
```csharp
foreach (var (name, function) in builtins.GetAllFunctions())
{
    var paramStr = string.Join(", ", function.Parameters.Select(p => $"{p.Name}: {p.Type}"));
    Console.WriteLine($"{name}({paramStr}) -> {function.ReturnType}");
}
// Output:
// print(values: object?[]) -> None
// len(obj: object) -> int
// range(stop: int) -> range
// range(start: int, stop: int) -> range
// range(start: int, stop: int, step: int) -> range
// ...
```

**Note:** This flattens overloads, so `range` appears 3 times (once for each overload).

---

## Dependencies

`BuiltinRegistry` depends on several other components:

### 1. `CachedModuleDiscovery` (Discovery namespace)
**Purpose:** Discovers functions from assemblies using reflection and caching
**Relationship:** `BuiltinRegistry` creates and uses a `CachedModuleDiscovery` instance to load `Sharpy.Core`

**Why it's important:** Without this, the registry would need to manually list every builtin function, which would be error-prone and hard to maintain.

### 2. `TypeSymbol` and `FunctionSymbol` (Symbol.cs)
**Purpose:** Represent types and functions in the semantic analyzer
**Relationship:** `BuiltinRegistry` creates and stores these symbols

**Key fields:**
- `TypeSymbol`: `Name`, `ClrType`, `TypeKind`, `TypeParameters`, `IsGeneric`
- `FunctionSymbol`: `Name`, `Parameters`, `ReturnType`, `ClrMethod` (for .NET interop)

### 3. `Sharpy.Core.Exports` Assembly
**Purpose:** The standard library containing all builtin functions
**Relationship:** `BuiltinRegistry` loads this assembly and discovers its exported functions

**Discovery process:**
1. Get assembly: `typeof(Sharpy.Core.Exports).Assembly`
2. Load it: `_discovery.LoadAssembly(sharpyCoreAssembly)`
3. Query functions: `_discovery.GetModuleFunctions("builtins")`

### 4. `SymbolTable` (Semantic namespace)
**Purpose:** Stores all symbols (variables, functions, types) in the current compilation
**Relationship:** `SymbolTable` **owns** a `BuiltinRegistry` instance and provides access to it

**Usage:**
```csharp
public class SymbolTable
{
    private readonly BuiltinRegistry _builtins;

    public SymbolTable(BuiltinRegistry builtins)
    {
        _builtins = builtins;
    }

    public BuiltinRegistry BuiltinRegistry => _builtins;
}
```

**Design Pattern:** This is **dependency injection** - `SymbolTable` receives a `BuiltinRegistry` rather than creating it. This makes testing easier and allows sharing the same registry across multiple symbol tables.

---

## Patterns and Design Decisions

### 1. Separation of Concerns

**Types vs Functions:**
- **Types** are manually registered - small, stable set
- **Functions** are auto-discovered - large, growing set with overloads

This hybrid approach balances:
- **Control** (manual type registration gives precise naming)
- **Maintainability** (auto-discovery means less boilerplate)
- **Performance** (caching makes auto-discovery fast)

### 2. Lazy Discovery with Caching

The registry uses a **"discover once, cache forever"** approach:

**First compilation:**
1. Use reflection on `Sharpy.Core` assembly
2. Build function index
3. Save to disk (`~/.sharpy/cache/overload-index/Sharpy.Core-1.0.0.bin`)

**Subsequent compilations:**
1. Check cache for `Sharpy.Core-1.0.0.bin`
2. Load cached index (much faster than reflection)
3. Use cached function signatures

**Result:** 4-7x speedup on subsequent compilations.

### 3. Overload Support

Notice how `_functions` is a `Dictionary<string, List<FunctionSymbol>>` rather than `Dictionary<string, FunctionSymbol>`.

**Why?** Many builtin functions have multiple overloads:
- `range(stop)`, `range(start, stop)`, `range(start, stop, step)`
- `print(*values)` (variadic)
- `len(str)`, `len(list)`, `len(dict)`, etc. (different parameter types)

The list stores all overloads, and the `TypeChecker` selects the correct one based on the actual argument types in the call.

### 4. Pythonic Naming with .NET Types

The registry provides a **naming bridge** between Python and .NET:

| Sharpy Name | .NET Type | TypeKind |
|-------------|-----------|----------|
| `int` | `System.Int32` | Struct |
| `str` | `System.String` | Class |
| `list` | `System.Collections.Generic.List<>` | Class |
| `dict` | `System.Collections.Generic.Dictionary<,>` | Class |
| `None` | `System.Void` | Struct |

This allows Sharpy code to feel like Python while compiling to efficient .NET IL.

### 5. Generic Type Placeholder Naming

For generic types, the registry generates simple placeholder names:
- `list` → `TypeParameters = ["T0"]`
- `dict` → `TypeParameters = ["T0", "T1"]`
- `set` → `TypeParameters = ["T0"]`

These are later substituted during type checking:
- `list[int]` → `List<Int32>`
- `dict[str, int]` → `Dictionary<String, Int32>`

**Why `T0`, `T1` and not `T`, `K`, `V`?**
- Simple, predictable pattern
- Avoids naming conflicts
- Easy to generate programmatically

### 6. Thread-Safety

The registry itself is **not thread-safe** because it's designed to be:
1. Created once during compiler initialization
2. Read-only after construction
3. Shared across the compilation pipeline

However, the underlying `CachedModuleDiscovery` **is thread-safe**, so multiple compilers can run concurrently without corrupting the cache.

---

## How It Fits into the Compiler Pipeline

```
Source Code: "print(len([1, 2, 3]))"
    ↓
Lexer → Tokens
    ↓
Parser → AST
    ↓
┌─────────────────────────────────────┐
│     Semantic Analysis Phase         │
│                                     │
│  NameResolver:                      │
│    - Sees "print" → lookup in       │
│      BuiltinRegistry.GetFunction()  │ ← Uses BuiltinRegistry
│    - Sees "len" → lookup in         │
│      BuiltinRegistry.GetFunction()  │
│                                     │
│  TypeChecker:                       │
│    - Sees "[1, 2, 3]" → infers      │
│      type "list[int]" using         │ ← Uses BuiltinRegistry
│      BuiltinRegistry.GetType("list")│
│    - Checks "len([1,2,3])" call →   │
│      verifies len() accepts list    │
│    - Checks "print(...)" call →     │
│      verifies print() exists        │
└─────────────────────────────────────┘
    ↓
Code Generator
    ↓
C# Code: "Sharpy.Core.Exports.Print(Sharpy.Core.Exports.Len(new List<int> {1, 2, 3}))"
```

### Initialization Flow

```
Compiler.Compile():
    ↓
Create BuiltinRegistry()
    ↓
    RegisterType("int", typeof(int), ...)
    RegisterType("str", typeof(string), ...)
    RegisterType("list", typeof(List<>), ...)
    ...
    ↓
    LoadBuiltinFunctions()
        ↓
        CachedModuleDiscovery.LoadAssembly(Sharpy.Core)
            ↓
            Try load from cache
            ↓ (cache miss)
            OverloadIndexBuilder.BuildFromAssembly()
                ↓ (reflection)
                Discover all [Module("builtins")] classes
                Find all public static methods
                Convert to FunctionSignature objects
            ↓
            OverloadIndexCache.Save()
        ↓
        GetModuleFunctions("builtins")
            ↓
            Returns List<FunctionSymbol>
        ↓
        Add to _functions dictionary
    ↓
Registry ready
    ↓
Pass to SymbolTable(builtinRegistry)
    ↓
Pass to TypeChecker, NameResolver, etc.
```

---

## Debugging Tips

### 1. Function Not Found

**Symptom:** Compiler error: `Undefined name: some_function`

**Possible causes:**
- Function not in `Sharpy.Core`
- Function not in `Exports` class
- Function not public static
- Function's class not in `"builtins"` module

**How to debug:**
```csharp
var registry = new BuiltinRegistry();
var allFunctions = registry.GetAllFunctions().Select(f => f.Name).Distinct();
Console.WriteLine("Available functions: " + string.Join(", ", allFunctions));

// Check if specific function exists
var myFunc = registry.GetFunction("some_function");
if (myFunc == null)
{
    Console.WriteLine("Function not registered!");
    // Check Sharpy.Core/Builtins/Exports.cs
}
```

### 2. Type Not Found

**Symptom:** Compiler error: `Unknown type: some_type`

**Possible causes:**
- Type not registered in `LoadBuiltins()`
- Typo in type name

**How to debug:**
```csharp
var registry = new BuiltinRegistry();
var allTypes = registry.GetAllTypes();
foreach (var (name, type) in allTypes)
{
    Console.WriteLine($"{name} -> {type.ClrType.FullName}");
}

// Check if specific type exists
var myType = registry.GetType("some_type");
if (myType == null)
{
    Console.WriteLine("Type not registered!");
    // Add it to LoadBuiltins() if needed
}
```

### 3. Wrong Overload Selected

**Symptom:** Type checker selects wrong overload, or reports "no matching overload"

**Possible causes:**
- Overload missing from `Sharpy.Core`
- Type mapper converting types incorrectly
- Parameter type mismatch

**How to debug:**
```csharp
var registry = new BuiltinRegistry();
var overloads = registry.GetFunctionOverloads("problematic_function");

if (overloads == null)
{
    Console.WriteLine("No overloads found!");
}
else
{
    Console.WriteLine($"Found {overloads.Count} overload(s):");
    foreach (var overload in overloads)
    {
        var paramStr = string.Join(", ", 
            overload.Parameters.Select(p => $"{p.Name}: {p.Type}"));
        Console.WriteLine($"  - {overload.Name}({paramStr}) -> {overload.ReturnType}");
    }
}
```

### 4. Cache Issues

**Symptom:** New functions added to `Sharpy.Core` but not appearing in registry

**Cause:** Stale cache

**How to fix:**
```bash
# Clear the cache
rm -rf ~/.sharpy/cache/overload-index/

# Or in code:
var discovery = new CachedModuleDiscovery();
discovery.ClearCache();
```

### 5. Performance Problems

**Symptom:** Compilation is slow on every run

**Possible causes:**
- Cache not being used
- Cache directory permission issues
- Cache disabled in tests

**How to debug:**
```csharp
// Enable verbose logging
var cache = new OverloadIndexCache();
var discovery = new CachedModuleDiscovery(cache);

// Check if cache is being used
var assembly = typeof(Sharpy.Core.Exports).Assembly;
var identity = AssemblyIdentity.FromAssembly(assembly);

var cached = cache.TryLoad(identity);
if (cached != null)
{
    Console.WriteLine("Cache hit!");
}
else
{
    Console.WriteLine("Cache miss - using reflection");
}
```

### 6. Generic Type Issues

**Symptom:** `list`, `dict`, or `set` not working correctly

**Check:**
```csharp
var registry = new BuiltinRegistry();
var listType = registry.GetType("list");

Console.WriteLine($"IsGeneric: {listType?.IsGeneric}");
Console.WriteLine($"Type parameters: [{string.Join(", ", listType?.TypeParameters ?? [])}]");
Console.WriteLine($"CLR type: {listType?.ClrType.FullName}");

// Should print:
// IsGeneric: True
// Type parameters: [T0]
// CLR type: System.Collections.Generic.List`1
```

---

## Contribution Guidelines

### Adding a New Builtin Type

If you need to add a new primitive type (uncommon):

1. **Add to `LoadBuiltins()` method:**
   ```csharp
   // In LoadBuiltins()
   RegisterType("byte", typeof(byte), TypeKind.Struct);
   ```

2. **Update tests:**
   ```csharp
   [Fact]
   public void TestByteType()
   {
       var registry = new BuiltinRegistry();
       var byteType = registry.GetType("byte");
       Assert.NotNull(byteType);
       Assert.Equal(typeof(byte), byteType.ClrType);
   }
   ```

3. **Consider:** Do you really need a new type? Most .NET types can be imported directly.

### Adding a New Builtin Function

**Good news:** You don't need to modify `BuiltinRegistry.cs` at all!

1. **Add function to `Sharpy.Core/Builtins/Exports.cs`:**
   ```csharp
   namespace Sharpy.Core;

   public static partial class Exports
   {
       /// <summary>
       /// Your new builtin function
       /// </summary>
       public static int MyNewFunction(string arg1, int arg2)
       {
           // Implementation
           return 42;
       }
   }
   ```

2. **Clear cache** (first time):
   ```bash
   rm -rf ~/.sharpy/cache/overload-index/
   ```

3. **Rebuild and test:**
   ```bash
   dotnet build src/Sharpy.Core
   dotnet test src/Sharpy.Compiler.Tests --filter "FullyQualifiedName~BuiltinRegistry"
   ```

4. **Verify discovery:**
   ```csharp
   [Fact]
   public void TestMyNewFunctionDiscovered()
   {
       var registry = new BuiltinRegistry();
       var func = registry.GetFunction("MyNewFunction");
       Assert.NotNull(func);
       Assert.Equal(2, func.Parameters.Count);
   }
   ```

The function will be **automatically discovered** and added to the registry!

### Adding Function Overloads

Just add another method with the same name in `Sharpy.Core`:

```csharp
public static partial class Exports
{
    // Existing overload
    public static int MyFunc(int x) => x * 2;

    // New overload - automatically discovered!
    public static int MyFunc(int x, int y) => x + y;
}
```

Both will be stored in `_functions["MyFunc"]` as a list.

### Modifying the Discovery System

If you need to change **how** functions are discovered (rare):

1. **Modify `CachedModuleDiscovery`** - This handles the reflection logic
2. **Update cache format** - Increment cache version to force rebuild
3. **Test thoroughly** - Discovery affects all builtin functions
4. **Document changes** - Update architecture docs

### Common Mistakes to Avoid

❌ **DON'T manually register functions** in `LoadBuiltinFunctions()`:
```csharp
// BAD - don't do this!
_functions["print"] = new List<FunctionSymbol> { ... };
```

✅ **DO add them to `Sharpy.Core/Builtins/Exports.cs`:**
```csharp
// GOOD - let discovery find them
public static partial class Exports
{
    public static void Print(params object?[] values) { ... }
}
```

❌ **DON'T cache the BuiltinRegistry** across compilations:
```csharp
// BAD - creates concurrency issues
private static BuiltinRegistry _sharedRegistry = new();
```

✅ **DO create a new instance per compilation:**
```csharp
// GOOD - each compilation gets its own instance
var registry = new BuiltinRegistry();
```

❌ **DON'T forget to handle overloads:**
```csharp
// BAD - only gets first overload
var func = registry.GetFunction("range");
```

✅ **DO use `GetFunctionOverloads()` when you need all overloads:**
```csharp
// GOOD - gets all overloads for proper resolution
var overloads = registry.GetFunctionOverloads("range");
```

### Performance Considerations

1. **Create once per compilation** - Don't create multiple `BuiltinRegistry` instances
2. **Share across components** - Pass the same instance to `SymbolTable`, `TypeChecker`, etc.
3. **Cache is automatic** - Don't try to "optimize" the discovery process
4. **Generic types are fast** - No special handling needed

### Testing Changes

Always run the full test suite after modifying `BuiltinRegistry`:

```bash
# Test the registry itself
dotnet test --filter "FullyQualifiedName~BuiltinRegistry"

# Test semantic analyzer (heavy user of registry)
dotnet test --filter "FullyQualifiedName~TypeChecker"
dotnet test --filter "FullyQualifiedName~NameResolver"

# Test integration (end-to-end)
dotnet test --filter "FullyQualifiedName~Integration"
```

---

## Summary

`BuiltinRegistry` is the **foundation** of the Sharpy type system. It:

1. **Maps Python-like types to .NET types** - `int`, `str`, `list[T]`, etc.
2. **Automatically discovers builtin functions** - Uses reflection + caching for performance
3. **Supports function overloading** - Stores all overloads and lets `TypeChecker` pick the right one
4. **Provides fast lookup** - Simple dictionary access for types and functions
5. **Enables .NET interop** - Each symbol has a `ClrType` or `ClrMethod` for code generation

**Key Takeaway:** To add a new builtin function, you **don't touch this file**. Just add it to `Sharpy.Core/Builtins/Exports.cs` and it will be discovered automatically. This makes the compiler highly extensible while maintaining good performance through caching.
