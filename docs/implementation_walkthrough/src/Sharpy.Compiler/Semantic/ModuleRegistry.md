# Walkthrough: ModuleRegistry.cs

**Source File**: `src/Sharpy.Compiler/Semantic/ModuleRegistry.cs`

---

## Overview

`ModuleRegistry` is the central registry for managing third-party .NET assemblies and their exported functions in the Sharpy compiler. It serves as a bridge between Sharpy's module system and the .NET ecosystem, enabling Sharpy code to import and use external .NET libraries.

**Primary responsibilities:**
- Loading and caching .NET assemblies
- Discovering exported functions from assemblies (via reflection)
- Resolving .NET types from Sharpy module names (e.g., `system` → `System`)
- Managing module search paths for assembly resolution
- Converting CLR types to Sharpy type symbols
- Thread-safe concurrent access to shared state

**Pipeline position:**
- **Upstream**: Parser (AST) - receives import statements
- **Downstream**: Semantic Analyzer, Code Generator - provides type information and function symbols
- **Role**: Type resolution and external dependency management

---

## Class Structure

### Main Class: `ModuleRegistry`

A thread-safe registry that manages .NET assembly loading and function discovery.

#### Key Fields

```csharp
private readonly CachedModuleDiscovery _discovery;
private readonly ICompilerLogger _logger;
private readonly ConcurrentDictionary<string, Assembly> _loadedAssemblies;
private readonly ConcurrentBag<string> _modulePaths;
private readonly ConcurrentBag<SemanticError> _errors;
```

**Thread-safety design:**
- Uses `ConcurrentDictionary` for loaded assemblies (atomic operations)
- Uses `ConcurrentBag` for module paths (lock-free, allows duplicates)
- Uses `ConcurrentBag` for error collection (thread-safe accumulation)

#### Constructor

```csharp
public ModuleRegistry(ICompilerLogger? logger = null, OverloadIndexCache? cache = null)
```

**Design decision:** Optional parameters with null-coalescing defaults enable flexible instantiation:
- `logger ?? NullLogger.Instance` - avoids null checks throughout the code
- Custom cache support enables testing and cache sharing across compilations

---

## Key Methods

### 1. Assembly Loading: `LoadReference(string assemblyPath)`

The primary entry point for loading external .NET assemblies.

```csharp
public bool LoadReference(string assemblyPath)
```

**Algorithm flow:**
1. **Path resolution** - Calls `ResolveAssemblyPath()` to find the actual file
2. **Assembly loading** - Uses `Assembly.LoadFrom()` to load the DLL
3. **Deduplication** - Uses `TryAdd()` to prevent double-loading
4. **Function discovery** - Delegates to `_discovery.LoadAssembly()` for reflection
5. **Error handling** - Catches `IOException`, `BadImageFormatException`, `UnauthorizedAccessException`

**Key implementation details:**
- Returns `true` even if assembly was already loaded (idempotent behavior)
- Uses assembly name (not path) as the dictionary key
- Logs at different levels: Debug for duplicates, Info for new loads, Error for failures

**Thread-safety:** `TryAdd()` ensures atomic check-and-add operation - multiple threads can safely call `LoadReference()` for the same assembly without race conditions.

---

### 2. Path Resolution: `ResolveAssemblyPath(string assemblyPath)`

Implements a multi-tier search strategy to locate assembly files.

```csharp
private string? ResolveAssemblyPath(string assemblyPath)
```

**Search order:**
1. **Absolute/relative path** - Checks if `assemblyPath` exists as-is
2. **Current directory** - Checks `Path.Combine(Directory.GetCurrentDirectory(), assemblyPath)`
3. **Module search paths** - Iterates through `_modulePaths` checking each directory
4. **Auto .dll extension** - If path doesn't end with `.dll`, tries adding it

**TOCTOU (Time-of-Check-Time-of-Use) consideration:**
The code comment explicitly acknowledges the race condition between checking file existence and loading it. This is acceptable because:
- File system changes are rare during compilation
- `LoadReference()` catches any exceptions from `Assembly.LoadFrom()`
- The alternative (no existence check) would provide worse error messages

**Extension handling:**
```csharp
if (!assemblyPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
{
    var dllPath = Path.Combine(searchPath, assemblyPath + ".dll");
    if (File.Exists(dllPath))
        return Path.GetFullPath(dllPath);
}
```
This allows users to write `LoadReference("MyLibrary")` instead of `LoadReference("MyLibrary.dll")`.

---

### 3. Module Path Management: `AddModulePath(string path)`

Adds directories to the assembly search path.

```csharp
public void AddModulePath(string path)
```

**Design decision - Allowing duplicates:**
The code comment notes that `ConcurrentBag` allows duplicates, but this is acceptable because:
- `ResolveAssemblyPath()` returns on first match
- Avoiding duplicates would require locking (performance cost)
- Duplicates cause no correctness issues (just redundant checks)

**Validation:**
- Checks if directory exists before adding
- Logs warning (not error) if directory doesn't exist
- Does not throw exceptions (fail-soft behavior)

---

### 4. Type Resolution: `TryResolveNetType(string moduleName, string typeName)`

Maps Sharpy module names to .NET types.

```csharp
public Type? TryResolveNetType(string moduleName, string typeName)
```

**Example mapping:**
```
Sharpy: from system import Exception
  ↓
moduleName = "system"
typeName = "Exception"
  ↓
MapModuleToNamespace("system") → "System"
  ↓
fullTypeName = "System.Exception"
  ↓
Type.GetType("System.Exception") → System.Exception (CLR type)
```

**Type search strategy:**
1. **Try `Type.GetType()`** - Checks mscorlib and System.* assemblies
2. **Search all loaded assemblies** - Iterates `AppDomain.CurrentDomain.GetAssemblies()`

**Why two-tier search?**
- `Type.GetType()` is fast but limited to core assemblies
- AppDomain search is slower but comprehensive (finds types from loaded assemblies)

---

### 5. Namespace Mapping: `MapModuleToNamespace(string moduleName)`

Converts Python-style lowercase module names to .NET PascalCase namespaces.

```csharp
private string? MapModuleToNamespace(string moduleName)
{
    return moduleName.ToLowerInvariant() switch
    {
        "system" => "System",
        "system.collections" => "System.Collections",
        "system.collections.generic" => "System.Collections.Generic",
        "system.io" => "System.IO",
        "system.text" => "System.Text",
        "system.linq" => "System.Linq",
        "system.threading" => "System.Threading",
        "system.threading.tasks" => "System.Threading.Tasks",
        "system.net" => "System.Net",
        "system.net.http" => "System.Net.Http",
        _ => null
    };
}
```

**Design decision - Explicit mapping:**
Uses a hardcoded switch statement rather than automatic transformation (`ToPascalCase()`). This provides:
- **Predictability** - Only known namespaces are supported
- **Error detection** - Returns `null` for unsupported namespaces
- **Special cases** - Handles `IO` vs `Io` correctly

**Extensibility consideration:**
Adding new namespaces requires updating this method. Alternative designs (regex, config file) were likely rejected for simplicity.

---

### 6. CLR Type Conversion: `CreateTypeSymbolFromClrType(Type clrType)`

Converts a .NET `Type` into a Sharpy `TypeSymbol`.

```csharp
public TypeSymbol? CreateTypeSymbolFromClrType(Type clrType)
```

**Type kind mapping:**
```csharp
var typeKind = clrType.IsInterface ? TypeKind.Interface
             : clrType.IsEnum ? TypeKind.Enum
             : clrType.IsValueType ? TypeKind.Struct
             : TypeKind.Class;
```

**Key transformations:**
1. **Base type handling** - Recursively creates `TypeSymbol` for base classes, excludes `System.Object`
2. **Interface filtering** - Only includes directly implemented interfaces (not inherited ones)
3. **Constructor mapping** - Converts .NET constructors to `__init__` methods (Python convention)

**Inheritance filtering logic:**
```csharp
// Only add directly implemented interfaces (not inherited ones)
if (clrType.BaseType != null && clrType.BaseType.GetInterfaces().Contains(iface))
    continue;
```
This prevents duplicate interface entries when base class already implements an interface.

**Base type handling:**
```csharp
// Set base type for classes (except System.Object)
if (clrType.BaseType != null && clrType.BaseType != typeof(object))
{
    var baseTypeSymbol = CreateTypeSymbolFromClrType(clrType.BaseType);
    if (baseTypeSymbol != null)
    {
        typeSymbol.BaseType = baseTypeSymbol;
    }
}
```

---

### 7. Constructor Conversion: `CreateConstructorSymbol(ConstructorInfo ctor, Type declaringType)`

Maps .NET constructors to Sharpy `__init__` methods.

```csharp
private FunctionSymbol? CreateConstructorSymbol(ConstructorInfo ctor, Type declaringType)
```

**The `self` parameter puzzle:**

The code comment explains a subtle design decision:
```csharp
// Note: We DO include 'self' as the first parameter to match Sharpy conventions.
// The type checker uses .Skip(1) when building FunctionType from constructors,
// so we need the 'self' parameter for the skip to work correctly.
```

**Why this matters:**
- Sharpy methods always have `self` as first parameter (Python convention)
- Type checker expects to skip `self` when building function types
- If `self` is missing, `.Skip(1)` would skip the first real parameter!

**Parameter mapping:**
```csharp
// Add 'self' parameter first (Sharpy convention - will be skipped by type checker)
parameters.Add(new ParameterSymbol
{
    Name = "self",
    Type = new UserDefinedType { Name = declaringType.Name }
});

// Add constructor parameters
foreach (var param in ctor.GetParameters())
{
    var paramType = typeMapper.MapClrTypeToSemanticType(param.ParameterType);
    parameters.Add(new ParameterSymbol
    {
        Name = param.Name ?? $"arg{param.Position}",
        Type = paramType,
        HasDefault = param.HasDefaultValue
    });
}
```

**Null safety:** Uses null-coalescing for parameter names (`param.Name ?? $"arg{param.Position}"`) because .NET reflection can return null for parameter names in some cases.

**Return type:** Constructor symbols always return `SemanticType.Void` (constructors don't return values in .NET).

---

### 8. Function Discovery: `GetModuleFunctions(string moduleName)`

Retrieves all exported functions from a loaded module.

```csharp
public List<FunctionSymbol> GetModuleFunctions(string moduleName)
```

**Error handling strategy:**
- Catches `KeyNotFoundException` - Module not loaded
- Catches `InvalidOperationException` - Discovery errors
- Returns empty list instead of throwing - fail-soft behavior
- Logs warnings for debugging

**Why catch and return empty list?**
- Enables defensive programming in calling code
- Allows compilation to continue (collect multiple errors)
- Caller can check `Errors` property for diagnostic information

---

### 9. Namespace Type Discovery: `GetNamespaceTypes(string moduleName)`

Gets all public types from a .NET namespace.

```csharp
public List<TypeSymbol> GetNamespaceTypes(string moduleName)
```

**Algorithm:**
1. Map Sharpy module name to .NET namespace using `MapModuleToNamespace()`
2. Iterate through all loaded assemblies in the AppDomain
3. For each assembly, get all types matching the namespace
4. Filter to public types only
5. Convert each CLR type to a `TypeSymbol`

**Assembly enumeration:**
```csharp
foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
{
    try
    {
        var namespaceTypes = assembly.GetTypes()
            .Where(t => t.IsPublic && t.Namespace == netNamespace);

        foreach (var clrType in namespaceTypes)
        {
            var typeSymbol = CreateTypeSymbolFromClrType(clrType);
            if (typeSymbol != null)
            {
                types.Add(typeSymbol);
            }
        }
    }
    catch (ReflectionTypeLoadException)
    {
        // Skip assemblies that can't be fully loaded
    }
}
```

**Why catch `ReflectionTypeLoadException`?**
- Some assemblies fail to load all types (missing dependencies)
- Better to skip problematic assemblies than fail entire compilation
- Types from working assemblies are still discovered

**Performance consideration:**
This method calls `GetTypes()` on every loaded assembly, which can be slow. Caching could improve performance if this becomes a bottleneck.

---

### 10. Module Queries: `GetLoadedModules()` & `IsModuleLoaded(string moduleName)`

```csharp
public IEnumerable<string> GetLoadedModules()
public bool IsModuleLoaded(string moduleName)
```

**What they do:** Query methods for introspection and validation.

**`GetLoadedModules()` implementation:**
- Delegates to `_discovery.GetLoadedModules()`
- Returns distinct module names from all loaded assemblies

**`IsModuleLoaded()` implementation:**
```csharp
public bool IsModuleLoaded(string moduleName)
{
    return GetLoadedModules().Any(m =>
        m.Equals(moduleName, StringComparison.OrdinalIgnoreCase));
}
```

**Case-insensitive comparison:** Matches .NET's assembly naming conventions.

**Use cases:**
- **Error Messages**: "Module 'Foo' not loaded. Did you forget to import it?"
- **Debugging**: Print all loaded modules when compiler runs with `--verbose`
- **Optimization**: Skip loading if already present

---

### 11. Namespace Checks: `IsNetNamespace(string moduleName)`

```csharp
public bool IsNetNamespace(string moduleName)
{
    return MapModuleToNamespace(moduleName) != null;
}
```

**What it does:** Checks if a module name maps to a .NET namespace.

**Use case:** Distinguishing between Sharpy modules (`.spy` files) and .NET namespace imports:
```python
from system import Console  # .NET namespace → IsNetNamespace("system") = true
from mymodule import MyClass  # Sharpy module → IsNetNamespace("mymodule") = false
```

---

### 12. Cache Management: `ClearCache()`

```csharp
public void ClearCache()
{
    _discovery.ClearCache();
    _logger.LogInfo("Cleared module discovery cache");
}
```

**What it does**: Deletes cached function signature data, forcing a full reflection scan on next load.

**When to use:**
- Development: After recompiling a C# library that Sharpy imports
- CI/CD: Ensuring clean builds
- Debugging: When cached data might be stale or corrupted

---

### 13. Error Collection: `AddError(string message)`

```csharp
private void AddError(string message)
{
    _errors.Add(new SemanticError(message, null, null));
}
```

**What it does:** Thread-safe way to record an error without throwing an exception.

**Design decision:** Errors are collected rather than thrown immediately, allowing the compiler to report multiple module loading failures at once instead of stopping at the first error.

**Public access:** Errors are exposed via the `Errors` property:
```csharp
public IReadOnlyList<SemanticError> Errors => _errors.ToList();
```

---

## Dependencies

### Internal Dependencies

#### `CachedModuleDiscovery` (Discovery namespace)
- **Purpose**: Handles actual reflection and caching of function signatures
- **Why separate?** Separation of concerns - discovery logic is complex enough to warrant its own class
- **Caching strategy**: Uses `OverloadIndexCache` to persist discovered functions between compilations

#### `ICompilerLogger` (Logging namespace)
- **Purpose**: Diagnostic logging throughout the registry
- **Levels used**: Debug (verbose), Info (normal), Warning (non-fatal issues)
- **Null pattern**: `NullLogger.Instance` avoids null checks

#### `TypeMapper` (Discovery namespace)
- **Purpose**: Maps .NET types to Sharpy semantic types
- **Example**: `System.Int32` → `SemanticType.Int`
- **Usage**: In `CreateConstructorSymbol()` to map parameter types

### Symbol Types (Semantic namespace)
- `FunctionSymbol` - Represents a function/method
- `TypeSymbol` - Represents a type (class, struct, interface, enum)
- `ParameterSymbol` - Represents a function parameter
- `SemanticError` - Error reporting

### External Dependencies

#### `System.Reflection`
- **Usage**: Loading assemblies, discovering types and methods
- **Key types**: `Assembly`, `Type`, `ConstructorInfo`, `MethodInfo`

#### `System.Collections.Concurrent`
- **Usage**: Thread-safe collections
- **Key types**: `ConcurrentDictionary`, `ConcurrentBag`

---

## Patterns and Design Decisions

### 1. Thread-Safety Pattern

**Concurrent collections throughout:**
```csharp
private readonly ConcurrentDictionary<string, Assembly> _loadedAssemblies;
private readonly ConcurrentBag<string> _modulePaths;
private readonly ConcurrentBag<SemanticError> _errors;
```

**Why thread-safe?**
- Enables parallel compilation of multiple files
- Allows concurrent loading of multiple assemblies
- Future-proofs for multi-threaded compiler pipeline

**Trade-off:** Slight performance overhead for single-threaded use, but negligible compared to reflection costs.

---

### 2. Null Object Pattern

```csharp
_logger = logger ?? NullLogger.Instance;
```

**Benefits:**
- No `if (_logger != null)` checks needed
- Clean call sites: `_logger.LogInfo(...)` always works
- Follows "Tell, Don't Ask" principle

---

### 3. Fail-Soft Error Handling

**Philosophy:** Collect errors and continue, rather than fail fast.

**Example:**
```csharp
public List<FunctionSymbol> GetModuleFunctions(string moduleName)
{
    try
    {
        return _discovery.GetModuleFunctions(moduleName);
    }
    catch (KeyNotFoundException ex)
    {
        _logger.LogWarning($"Module '{moduleName}' not found: {ex.Message}", 0, 0);
        return new List<FunctionSymbol>();  // Empty list, not exception
    }
}
```

**Rationale:**
- Enables reporting multiple errors in one compilation
- Allows partial compilation success
- Caller can check `Errors` property for diagnostics

---

### 4. Delegation Pattern

`ModuleRegistry` delegates actual function discovery to `CachedModuleDiscovery`:

```csharp
private readonly CachedModuleDiscovery _discovery;

public void LoadAssembly(Assembly assembly)
{
    _discovery.LoadAssembly(assembly);
}
```

**Why delegate?**
- **Single Responsibility** - `ModuleRegistry` manages loading, `CachedModuleDiscovery` handles reflection
- **Caching abstraction** - Registry doesn't need to know about cache implementation
- **Testability** - Can mock discovery for unit tests

---

### 5. Idempotent Operations

**Assembly loading is idempotent:**
```csharp
if (!_loadedAssemblies.TryAdd(assemblyName, assembly))
{
    _logger.LogDebug($"Assembly '{assemblyName}' already loaded");
    return true;  // Returns true even if already loaded
}
```

**Benefits:**
- Safe to call `LoadReference()` multiple times
- Simplifies calling code (no need to track loaded assemblies)
- Enables "load on first use" pattern

---

### 6. Search Path Fallback Chain

**Tiered resolution strategy** in `ResolveAssemblyPath()`:

```
1. Direct path
   ↓ (not found)
2. Current directory
   ↓ (not found)
3. Module search paths
   ↓ (not found)
4. Try adding .dll extension
   ↓ (not found)
return null
```

**Design rationale:**
- **Least surprising** - Check direct path first
- **Convenience** - Allow relative paths from current directory
- **Flexibility** - Support custom search paths
- **User-friendly** - Auto-append `.dll` extension

---

### 7. Recursive Type Symbol Creation

**Pattern:** When converting CLR types to type symbols, recursively process:
- Base types (inheritance chains)
- Implemented interfaces
- Generic type arguments (handled by TypeMapper)

**Example:**
```csharp
public class MyList : List<string>, IEnumerable<string>
```

Becomes:
```
TypeSymbol { Name = "MyList" }
  ↓ BaseType
TypeSymbol { Name = "List", TypeArguments = [string] }
  ↓ Interfaces (direct only)
TypeSymbol { Name = "IEnumerable", TypeArguments = [string] }
```

**Note:** The interface filtering logic ensures we don't duplicate interfaces already implemented by the base class.

---

## Integration with Compiler Pipeline

### How ModuleRegistry Fits In

```
Parser (import statements)
    ↓
Semantic Analyzer / ImportResolver
    ↓
ModuleRegistry.IsNetNamespace("system")
    ↓
ModuleRegistry.TryResolveNetType("system", "Console")
    ↓
CreateTypeSymbolFromClrType(typeof(System.Console))
    ↓
TypeSymbol created with constructors and methods
    ↓
Semantic Analyzer (type checking)
    ↓
Code Generator (emits C# using symbols)
```

### Example: Processing an Import Statement

**Sharpy code:**
```python
from system import Console

Console.WriteLine("Hello")
```

**Compiler steps:**
1. **Parser**: Creates `ImportStatement` AST node
2. **ImportResolver**: Calls `ModuleRegistry.IsNetNamespace("system")` → `true`
3. **Type Resolution**: Calls `ModuleRegistry.TryResolveNetType("system", "Console")` → `System.Console` (CLR type)
4. **Symbol Creation**: Calls `ModuleRegistry.CreateTypeSymbolFromClrType(typeof(System.Console))` → `TypeSymbol`
5. **Type Checking**: Validates `Console.WriteLine` method exists and accepts `string`
6. **Code Generation**: Emits `System.Console.WriteLine("Hello")`

### Example: Loading a Custom Assembly

**Sharpy code:**
```python
# Assume MyLibrary.dll exports class Exports with static methods
from MyLibrary import calculate

result = calculate(42)
```

**Compiler steps:**
1. **CLI/Build System**: Calls `registry.AddModulePath("./libs")`
2. **ImportResolver**: Sees import, calls `registry.LoadReference("MyLibrary.dll")`
3. **ModuleRegistry**: Resolves path to `./libs/MyLibrary.dll`
4. **Assembly.LoadFrom**: Loads the assembly
5. **CachedModuleDiscovery**: Scans for `Exports` class, discovers `calculate` method
6. **Type Checking**: Validates `calculate(42)` call matches discovered signature
7. **Code Generation**: Emits `MyLibrary.Exports.calculate(42)`

---

## Debugging Tips

### 1. Enable Verbose Logging

Set a logger that outputs Debug-level messages to see:
- Which assemblies are being loaded
- Path resolution attempts
- Type resolution steps

```csharp
var logger = new ConsoleLogger(LogLevel.Debug);
var registry = new ModuleRegistry(logger);
```

### 2. Check the Errors Collection

```csharp
registry.LoadReference("MyLibrary.dll");
if (registry.Errors.Any())
{
    foreach (var error in registry.Errors)
        Console.WriteLine(error.Message);
}
```

### 3. Common Issues and Fixes

#### Issue: "Assembly not found"
**Likely cause:** Assembly not in search path

**Fix:** Call `AddModulePath()` with directory containing the DLL

**Debug:**
```csharp
// Add breakpoint in ResolveAssemblyPath to inspect search order
var resolvedPath = ResolveAssemblyPath(assemblyPath);
Console.WriteLine($"Resolved: {resolvedPath}");
```

#### Issue: "Module 'foo' not found" when calling `GetModuleFunctions()`
**Likely cause:** Assembly hasn't been loaded yet or module name mismatch

**Fix:**
- Call `LoadReference()` before `GetModuleFunctions()`
- Check module name case sensitivity

**Debug:**
```csharp
var loadedModules = registry.GetLoadedModules().ToList();
Console.WriteLine($"Loaded modules: {string.Join(", ", loadedModules)}");
```

#### Issue: Constructor methods have wrong parameters
**Likely cause:** Type checker not skipping `self` parameter

**Fix:** Verify `CreateConstructorSymbol()` includes `self` as first parameter

**Debug:** Inspect the `ParameterSymbol` list for constructor functions

#### Issue: Thread-safety violations (rare)
**Symptom:** Random failures in multi-threaded scenarios

**Debug:** Check that all collection access uses concurrent collections

**Fix:** Never cache references to collection contents (always call methods)

**Log thread info:**
```csharp
_logger.LogDebug($"[Thread {Thread.CurrentThread.ManagedThreadId}] Loading {assemblyPath}");
```

### 4. Reflection Performance

**Symptom:** Slow compilation when loading many assemblies

**Cause:** `GetTypes()` and constructor reflection are expensive

**Mitigation:** Check if `CachedModuleDiscovery` cache is working (should be fast on subsequent runs)

**Debug:** Add timing logs:
```csharp
var sw = Stopwatch.StartNew();
_discovery.LoadAssembly(assembly);
_logger.LogInfo($"Loaded assembly in {sw.ElapsedMilliseconds}ms");
```

### 5. Breakpoint Locations

**Key breakpoints for debugging:**
- `LoadReference:54` - Start of assembly loading
- `ResolveAssemblyPath:373` - Path resolution logic
- `CreateTypeSymbolFromClrType:240` - Type conversion
- `MapModuleToNamespace:345` - Namespace mapping
- `CreateConstructorSymbol:306` - Constructor conversion

---

## Contribution Guidelines

### When to Modify This File

**Add new .NET namespace mapping:**
1. Add entry to `MapModuleToNamespace()` switch statement (line 348)
2. Follow lowercase → PascalCase convention
3. Update tests to verify new namespace

**Change assembly loading behavior:**
- Ensure thread-safety is preserved
- Update error handling to maintain fail-soft behavior
- Add tests for new scenarios

**Add new type conversion logic:**
- Modify `CreateTypeSymbolFromClrType()` (line 240)
- Ensure generic types are handled correctly
- Test with various .NET types (class, struct, interface, enum)

**Extend constructor conversion:**
- Modify `CreateConstructorSymbol()` (line 306)
- Ensure `self` parameter handling is preserved (CRITICAL!)
- Coordinate with type checker team if changing parameter order

### What NOT to Modify

**Don't change:**
- Thread-safety model (concurrent collections) without careful analysis
- Error handling strategy (fail-soft) without discussion
- `self` parameter handling in `CreateConstructorSymbol()` (breaks type checker)
- Assembly deduplication logic in `TryAdd()` (could cause memory leaks)

### Testing Checklist

When modifying this file:
- [ ] Test with concurrent assembly loading (multi-threaded test)
- [ ] Test assembly not found scenarios
- [ ] Test duplicate assembly loading (idempotency)
- [ ] Test namespace resolution for common namespaces (`system`, `system.io`, etc.)
- [ ] Test type conversion for classes, structs, interfaces, enums
- [ ] Test constructor conversion (verify `self` parameter)
- [ ] Verify logging output at Debug/Info/Warning levels
- [ ] Check error collection and reporting
- [ ] Test with .NET types that have complex inheritance hierarchies
- [ ] Test with generic types and nested types

### Coding Conventions

**Follow existing patterns:**
- Use concurrent collections for shared state
- Use null-coalescing for optional parameters (`?? DefaultValue`)
- Return empty collections instead of null
- Log before throwing or returning errors
- Catch specific exceptions (not `catch (Exception)`)

**Naming conventions:**
- Private fields: `_camelCase`
- Public methods: `PascalCase`
- Local variables: `camelCase`
- Parameters: `camelCase`

---

## Cross-References

### Related Files

**Discovery Layer:**
- `src/Sharpy.Compiler/Discovery/CachedModuleDiscovery.cs` - Handles reflection and caching
- `src/Sharpy.Compiler/Discovery/OverloadIndexBuilder.cs` - Builds function signature indices
- `src/Sharpy.Compiler/Discovery/TypeMapper.cs` - Maps CLR types to Sharpy types

**Semantic Analysis:**
- `src/Sharpy.Compiler/Semantic/Symbol.cs` - Defines `FunctionSymbol`, `TypeSymbol`, `ParameterSymbol`
- `src/Sharpy.Compiler/Semantic/SemanticError.cs` - Error representation
- `src/Sharpy.Compiler/Semantic/ImportResolver.cs` - Processes import statements, calls `ModuleRegistry`
- `src/Sharpy.Compiler/Semantic/TypeChecker.cs` - Uses type symbols for type checking

**Logging:**
- `src/Sharpy.Compiler/Logging/ICompilerLogger.cs` - Logger interface
- `src/Sharpy.Compiler/Logging/NullLogger.cs` - Null object pattern implementation

### Related Documentation

**Language Specifications:**
- `docs/language_specification/module_system.md` - Sharpy module and import semantics
- `docs/language_specification/module_resolution.md` - Module resolution algorithm

**Implementation Walkthroughs:**
- `docs/implementation_walkthrough/src/Sharpy.Compiler/Semantic/ImportResolver.md` - Import resolution logic
- `docs/implementation_walkthrough/src/Sharpy.Compiler/Semantic/Symbol.md` - Symbol types documentation
- `docs/implementation_walkthrough/src/Sharpy.Compiler/Semantic/TypeChecker.md` - Type checking that uses symbols

---

## Summary

`ModuleRegistry` is the gateway between Sharpy's Python-inspired module system and the .NET ecosystem. It provides:

✅ **Thread-safe assembly loading** with deduplication
✅ **Flexible path resolution** with fallback chain
✅ **Python-to-.NET namespace mapping** (`system` → `System`)
✅ **CLR type conversion** to Sharpy symbols
✅ **Constructor mapping** to `__init__` methods (with critical `self` parameter)
✅ **Fail-soft error handling** for robust compilation
✅ **Namespace type discovery** for .NET type imports

**Key architectural decisions:**
1. **Delegation to CachedModuleDiscovery** - Separates assembly loading from function discovery
2. **Explicit namespace mapping** - Only supported .NET namespaces are recognized
3. **Self parameter inclusion** - Critical for type checker compatibility
4. **Interface filtering** - Only direct interfaces to avoid duplicates
5. **Fail-soft error collection** - Enables reporting multiple errors

**Key takeaway:** When debugging import/module issues, this is the first place to look. Enable verbose logging and check the `Errors` collection to understand what's happening during assembly discovery. Pay special attention to the `self` parameter in constructor conversion - it's a critical design decision that enables proper type checking.
