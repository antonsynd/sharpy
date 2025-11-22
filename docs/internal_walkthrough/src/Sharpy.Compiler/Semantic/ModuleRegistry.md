# Walkthrough: ModuleRegistry.cs

**Source File**: `src/Sharpy.Compiler/Semantic/ModuleRegistry.cs`

---

## Overview

`ModuleRegistry` is a **thread-safe registry** for managing third-party .NET assemblies that can be imported into Sharpy programs. It acts as a bridge between the Sharpy compiler and external .NET libraries, enabling Sharpy code to call into C# libraries and the broader .NET ecosystem.

### Core Responsibilities

1. **Assembly Loading**: Load .NET DLL files from disk into the runtime
2. **Path Resolution**: Search multiple directories to locate assembly files
3. **Function Discovery**: Extract public static methods from loaded assemblies
4. **Module Querying**: Provide access to functions exported by loaded modules
5. **Error Management**: Track and report loading errors without crashing compilation
6. **Thread Safety**: Support concurrent access from multiple compiler threads

### Position in the Compiler Pipeline

```
Sharpy Source Code
    ↓
Parser (generates import statements)
    ↓
ImportResolver (resolves imports) ──→ ModuleRegistry (loads .NET assemblies)
    ↓                                        ↓
SemanticAnalyzer                    CachedModuleDiscovery (discovers functions)
    ↓
Code Generator (references loaded types/functions)
```

The `ModuleRegistry` is primarily used by the `ImportResolver` during semantic analysis to resolve `import` statements that reference .NET assemblies.

---

## Class Structure

### Main Class: `ModuleRegistry`

```csharp
public class ModuleRegistry
{
    private readonly CachedModuleDiscovery _discovery;
    private readonly ICompilerLogger _logger;
    private readonly ConcurrentDictionary<string, Assembly> _loadedAssemblies;
    private readonly ConcurrentBag<string> _modulePaths;
    private readonly ConcurrentBag<SemanticError> _errors;
}
```

### Field Breakdown

| Field | Type | Purpose |
|-------|------|---------|
| `_discovery` | `CachedModuleDiscovery` | Performs reflection-based function discovery with caching |
| `_logger` | `ICompilerLogger` | Logs debug/info/warning messages during assembly loading |
| `_loadedAssemblies` | `ConcurrentDictionary<string, Assembly>` | Tracks loaded assemblies by name (prevents duplicate loading) |
| `_modulePaths` | `ConcurrentBag<string>` | Directories to search when resolving assembly paths |
| `_errors` | `ConcurrentBag<SemanticError>` | Accumulates loading errors for later reporting |

### Why Concurrent Collections?

The use of `ConcurrentDictionary` and `ConcurrentBag` makes this class **thread-safe**, which is important for:
- Parallel compilation of multiple Sharpy files
- Future multi-threaded semantic analysis
- Plugin systems that might load modules concurrently

---

## Key Methods

### Constructor

```csharp
public ModuleRegistry(ICompilerLogger? logger = null, OverloadIndexCache? cache = null)
{
    _discovery = new CachedModuleDiscovery(cache);
    _logger = logger ?? NullLogger.Instance;
}
```

**Purpose**: Initialize the registry with optional logging and caching support.

**Parameters**:
- `logger`: For debugging and diagnostics (defaults to silent logger)
- `cache`: Performance optimization—reuse previously discovered function signatures

**Design Note**: Both parameters are optional, making the class easy to instantiate for testing or simple use cases.

---

### `AddModulePath(string path)`

```csharp
public void AddModulePath(string path)
{
    if (!Directory.Exists(path))
    {
        _logger.LogWarning($"Module path does not exist: {path}", 0, 0);
        return;
    }

    _modulePaths.Add(path);
    _logger.LogDebug($"Added module search path: {path}");
}
```

**Purpose**: Register additional directories where assembly DLLs might be located.

**Example Use Case**:
```csharp
registry.AddModulePath("/usr/local/lib/sharpy-modules");
registry.AddModulePath("./external_libs");
registry.LoadReference("MyCustomLibrary.dll");  // Searches these paths
```

**Important Detail**: The code includes a comment noting that `ConcurrentBag` allows duplicates, but this is acceptable since path resolution stops at the first match. This is a pragmatic trade-off for thread-safety without needing locks.

---

### `LoadReference(string assemblyPath)` - The Core Method

This is the **most important method** in the class. Let's break it down step by step:

#### Step 1: Path Resolution

```csharp
var resolvedPath = ResolveAssemblyPath(assemblyPath);
if (resolvedPath == null)
{
    AddError($"Assembly not found: {assemblyPath}");
    return false;
}
```

The registry tries to find the assembly file by searching:
1. As an absolute path
2. Relative to the current directory
3. In all registered module search paths
4. Optionally with `.dll` extension added

#### Step 2: Load the Assembly

```csharp
var assembly = Assembly.LoadFrom(resolvedPath);
var assemblyName = assembly.GetName().Name ?? Path.GetFileNameWithoutExtension(resolvedPath);
```

Uses .NET's reflection API to load the DLL into memory. The assembly name is extracted from metadata (preferred) or from the filename as a fallback.

#### Step 3: Prevent Duplicate Loading

```csharp
if (!_loadedAssemblies.TryAdd(assemblyName, assembly))
{
    _logger.LogDebug($"Assembly '{assemblyName}' already loaded");
    return true;
}
```

**Thread-safe check-and-add**: `TryAdd` atomically checks if the assembly is already loaded and adds it if not. This prevents:
- Wasting time re-scanning the same assembly
- Duplicate function symbols in the symbol table
- Potential memory leaks from multiple Assembly instances

#### Step 4: Discover Functions

```csharp
_discovery.LoadAssembly(assembly);
```

Delegates to `CachedModuleDiscovery` which:
1. Scans all types in the assembly
2. Finds public static methods in classes named "Exports" (by convention)
3. Converts them to `FunctionSymbol` objects
4. Caches the results for faster subsequent loads

#### Step 5: Error Handling

```csharp
catch (IOException ex)
{
    AddError($"Failed to load assembly '{assemblyPath}': {ex.Message}");
    return false;
}
catch (BadImageFormatException ex)
{
    AddError($"Invalid assembly format '{assemblyPath}': {ex.Message}");
    return false;
}
catch (UnauthorizedAccessException ex)
{
    AddError($"Access denied loading assembly '{assemblyPath}': {ex.Message}");
    return false;
}
```

**Graceful degradation**: Errors are collected but don't crash the compiler. This allows:
- Compilation to continue for other modules
- All errors to be reported together at the end
- Better user experience (see all problems at once)

**Common Error Scenarios**:
- `IOException`: File not found, disk error, network share unavailable
- `BadImageFormatException`: Not a valid .NET assembly (wrong architecture, corrupted file)
- `UnauthorizedAccessException`: Permission denied on protected system directories

---

### `GetModuleFunctions(string moduleName)`

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
        return new List<FunctionSymbol>();
    }
    catch (InvalidOperationException ex)
    {
        _logger.LogWarning($"Error getting functions for module '{moduleName}': {ex.Message}", 0, 0);
        return new List<FunctionSymbol>();
    }
}
```

**Purpose**: Retrieve all functions exported by a specific module for use in semantic analysis.

**Why Return Empty List on Error?**: This allows the semantic analyzer to gracefully handle missing modules without crashing. It will still generate appropriate "undefined symbol" errors when the code tries to use functions from the missing module.

**FunctionSymbol Structure**:
Each `FunctionSymbol` contains:
- Function name
- Return type (as `SemanticType`)
- Parameters with types and default values
- Access level (public/private)
- Reference to the underlying .NET `MethodInfo` (for code generation)

---

### `ResolveAssemblyPath(string assemblyPath)` - Path Resolution Algorithm

```csharp
private string? ResolveAssemblyPath(string assemblyPath)
{
    // 1. Try as absolute or relative path first
    if (File.Exists(assemblyPath))
    {
        return Path.GetFullPath(assemblyPath);
    }

    // 2. Try in current directory
    var currentDirPath = Path.Combine(Directory.GetCurrentDirectory(), assemblyPath);
    if (File.Exists(currentDirPath))
    {
        return Path.GetFullPath(currentDirPath);
    }

    // 3. Try in module search paths
    foreach (var searchPath in _modulePaths)
    {
        var fullPath = Path.Combine(searchPath, assemblyPath);
        if (File.Exists(fullPath))
        {
            return Path.GetFullPath(fullPath);
        }

        // Also try with .dll extension if not present
        if (!assemblyPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            var dllPath = Path.Combine(searchPath, assemblyPath + ".dll");
            if (File.Exists(dllPath))
            {
                return Path.GetFullPath(dllPath);
            }
        }
    }

    return null;
}
```

**Search Order Priority**:
1. **Absolute/Relative Paths**: `/usr/lib/MyLib.dll` or `../libs/MyLib.dll`
2. **Current Directory**: `./MyLib.dll`
3. **Registered Module Paths**: Each path in `_modulePaths`
4. **Extension Auto-completion**: Tries adding `.dll` if not present

**TOCTOU Race Condition Note**: The comment in the code mentions "Time-of-Check-Time-of-Use" race condition. This means:
- File might exist when checked here
- But could be deleted before `Assembly.LoadFrom()` is called
- This is acceptable because the exceptions in `LoadReference()` handle this

**Why It's Safe**: The race condition is benign because:
1. It's rare in practice (files don't usually disappear mid-compilation)
2. The exception handling catches any issues
3. Alternative (file locking) would be more complex and slower

---

### Helper Methods

#### `GetLoadedModules()`

```csharp
public IEnumerable<string> GetLoadedModules()
{
    return _discovery.GetLoadedModules();
}
```

Returns all module names that have been successfully loaded. Useful for debugging and diagnostics.

#### `IsModuleLoaded(string moduleName)`

```csharp
public bool IsModuleLoaded(string moduleName)
{
    return GetLoadedModules().Any(m =>
        m.Equals(moduleName, StringComparison.OrdinalIgnoreCase));
}
```

**Case-insensitive check** for module presence. This matches .NET's convention where assembly names are case-insensitive on Windows.

#### `ClearCache()`

```csharp
public void ClearCache()
{
    _discovery.ClearCache();
    _logger.LogInfo("Cleared module discovery cache");
}
```

Forces re-discovery of functions on next load. Useful for:
- Development scenarios where assemblies are rebuilt
- Testing
- Clearing stale cached data

---

## Dependencies

### Internal Dependencies

1. **`CachedModuleDiscovery`** (`Discovery/CachedModuleDiscovery.cs`)
   - Performs reflection on assemblies to find functions
   - Implements caching to avoid repeated reflection (4-7x speedup)
   - Converts .NET types to Sharpy's `SemanticType` system

2. **`FunctionSymbol`** (`Semantic/Symbol.cs`)
   - Record type representing a function in the symbol table
   - Contains signature information (name, parameters, return type)
   - Links to .NET `MethodInfo` for code generation

3. **`SemanticError`** (`Semantic/SemanticError.cs`)
   - Exception type for semantic analysis errors
   - Includes line/column information when available
   - Formatted for user-friendly error messages

4. **`ICompilerLogger`** (`Logging/ICompilerLogger.cs`)
   - Interface for logging compilation events
   - Supports Debug, Info, Warning levels
   - `NullLogger` implementation for silent operation

### External Dependencies

1. **`System.Reflection.Assembly`**
   - .NET's reflection API for loading assemblies
   - Core to the entire module loading mechanism

2. **`System.Collections.Concurrent`**
   - `ConcurrentDictionary` and `ConcurrentBag` for thread-safety
   - No locks needed—better performance

3. **`System.IO`**
   - File system operations for path resolution
   - Directory existence checks

---

## Patterns and Design Decisions

### 1. **Separation of Concerns**

The `ModuleRegistry` focuses on **assembly management**, while `CachedModuleDiscovery` handles **function discovery**. This separation:
- Makes each class easier to understand and test
- Allows discovery caching to be swapped out independently
- Follows Single Responsibility Principle

### 2. **Null Object Pattern**

```csharp
_logger = logger ?? NullLogger.Instance;
```

Using `NullLogger.Instance` instead of null checks everywhere:
- Eliminates `if (logger != null)` clutter
- Makes logging calls unconditional and cleaner
- No performance penalty (NullLogger methods are no-ops)

### 3. **Fail-Soft Error Handling**

Rather than throwing exceptions on assembly load failures, errors are:
- Collected in `_errors` bag
- Logged as warnings
- Methods return `false` or empty collections

**Why?**: Allows compilation to continue and report all errors at once, rather than failing fast on the first broken import.

### 4. **Thread-Safe Lazy Loading**

```csharp
if (!_loadedAssemblies.TryAdd(assemblyName, assembly))
{
    return true;  // Already loaded
}
```

Using `TryAdd` provides:
- Atomic check-and-insert operation
- No explicit locks needed
- Protection against duplicate loading in parallel scenarios

### 5. **Convention Over Configuration**

The system looks for classes named `"Exports"` in assemblies by convention. This means:
- .NET libraries can opt-in to Sharpy interop by following a naming pattern
- No attributes or special compilation needed
- Simple for C# library authors to understand

---

## Debugging Tips

### 1. **Enable Verbose Logging**

```csharp
var logger = new ConsoleLogger(LogLevel.Debug);
var registry = new ModuleRegistry(logger);
```

This will show:
- Which paths are searched for assemblies
- When assemblies are loaded
- When duplicates are skipped
- All warnings about missing paths

### 2. **Check Loaded Assemblies**

```csharp
foreach (var module in registry.GetLoadedModules())
{
    Console.WriteLine($"Loaded: {module}");
}
```

Verify that your expected modules are actually loaded.

### 3. **Inspect Errors**

```csharp
foreach (var error in registry.Errors)
{
    Console.WriteLine($"Error: {error.Message}");
}
```

Check what went wrong during assembly loading.

### 4. **Verify Function Discovery**

```csharp
var functions = registry.GetModuleFunctions("MyModule");
foreach (var func in functions)
{
    Console.WriteLine($"  {func.Name}({string.Join(", ", func.Parameters.Select(p => $"{p.Name}: {p.Type}"))}) -> {func.ReturnType}");
}
```

Ensure functions are being discovered correctly from your assembly.

### 5. **Common Issues**

| Problem | Cause | Solution |
|---------|-------|----------|
| "Assembly not found" | Wrong path | Check `_modulePaths`, verify file location |
| "Invalid assembly format" | Wrong architecture (x86/x64) or corrupted DLL | Rebuild assembly for correct target |
| No functions found | No "Exports" class or methods not public static | Add/rename class to "Exports" |
| Functions missing | Method signatures incompatible | Check parameter/return types are Sharpy-compatible |

### 6. **Cache Issues**

If you're modifying an assembly and changes aren't reflected:

```csharp
registry.ClearCache();  // Force re-discovery
```

The cache is persistent across compiler invocations, so updated assemblies might not be re-scanned without clearing.

---

## How It Fits in the Broader Codebase

### Usage in `Compiler.cs`

```csharp
private readonly ModuleRegistry? _moduleRegistry;

public Compiler(CompilerOptions? options = null, ICompilerLogger? logger = null)
{
    _moduleRegistry = new ModuleRegistry(_logger);
    // ...
}
```

The main `Compiler` class creates a `ModuleRegistry` instance and passes it to:

### Usage in `ImportResolver.cs`

```csharp
private readonly ModuleRegistry? _moduleRegistry;

public ImportResolver(ICompilerLogger? logger = null, ModuleRegistry? moduleRegistry = null)
{
    _moduleRegistry = moduleRegistry;
}
```

When resolving imports, `ImportResolver`:
1. Checks if an import is a .NET assembly
2. Calls `_moduleRegistry.LoadReference(assemblyPath)`
3. Retrieves functions via `_moduleRegistry.GetModuleFunctions(moduleName)`
4. Adds those functions to the symbol table

### Example Flow

```python
# Sharpy code
import System.Math

result = System.Math.Sqrt(16.0)
```

1. **Parser**: Creates `ImportFrom` AST node for `System.Math`
2. **ImportResolver**: 
   - Asks `ModuleRegistry` to load `System.Math.dll`
   - Registry uses reflection to find `System.Math` type
   - Discovers `Sqrt` method
   - Returns `FunctionSymbol` for `Sqrt`
3. **SemanticAnalyzer**: Adds `Sqrt` to symbol table
4. **CodeGenerator**: Emits C# call to `System.Math.Sqrt()`

---

## Contribution Guidelines

### When to Modify This File

1. **Adding New Discovery Strategies**
   - Currently looks for "Exports" classes only
   - Could extend to support attributes, interfaces, etc.

2. **Improving Path Resolution**
   - Add support for environment variables
   - Implement NuGet package resolution
   - Support assembly version resolution

3. **Enhanced Error Reporting**
   - Provide suggestions when assemblies aren't found
   - Better diagnostics for incompatible assemblies
   - Line/column info for import statements

4. **Performance Optimizations**
   - Parallel assembly loading for large projects
   - Lazy loading (defer discovery until functions are used)
   - Incremental caching improvements

### Suggested Improvements

#### 1. **Add Assembly Version Support**

Currently, the registry doesn't handle versioned assemblies. You could add:

```csharp
public bool LoadReference(string assemblyPath, Version? requiredVersion = null)
{
    // Check assembly version matches requirement
    if (requiredVersion != null && assembly.GetName().Version != requiredVersion)
    {
        AddError($"Version mismatch: expected {requiredVersion}, got {assembly.GetName().Version}");
        return false;
    }
    // ...
}
```

#### 2. **Support NuGet Packages**

```csharp
public void AddNuGetSource(string sourceUrl)
{
    // Integrate with NuGet API to resolve package names to DLL paths
}
```

#### 3. **Better Error Context**

Pass the `ImportFrom` AST node to provide source location:

```csharp
public bool LoadReference(string assemblyPath, ImportFrom? importNode = null)
{
    if (resolvedPath == null)
    {
        AddError($"Assembly not found: {assemblyPath}", 
                 importNode?.Line, 
                 importNode?.Column);
        // ...
    }
}
```

#### 4. **Discovery Extension Points**

Allow custom discovery strategies:

```csharp
public interface IModuleDiscoveryStrategy
{
    List<FunctionSymbol> DiscoverFunctions(Assembly assembly, string moduleName);
}

public void RegisterDiscoveryStrategy(IModuleDiscoveryStrategy strategy)
{
    // ...
}
```

### Testing Considerations

When modifying this file, ensure:

1. **Thread-safety is maintained**
   - All public methods must be safe for concurrent access
   - Use concurrent collections or proper locking

2. **Error handling is comprehensive**
   - Don't let exceptions escape (except truly fatal ones)
   - Provide useful error messages

3. **Caching behavior is tested**
   - Verify cache hits/misses
   - Test cache clearing
   - Check performance improvements

4. **Path resolution covers all cases**
   - Absolute paths
   - Relative paths
   - Search paths
   - Extension variants (.dll, no extension)

### Code Style Guidelines

- **Use `nullable` references**: The code already uses `?` for nullability
- **Log important events**: Use appropriate log levels (Debug for verbose, Info for key events, Warning for issues)
- **Document thread-safety**: Comments like the TOCTOU note help future maintainers
- **Prefer fail-soft**: Return false/empty rather than throwing, accumulate errors

---

## Summary

`ModuleRegistry` is a critical component for **Sharpy's .NET interoperability**. It:

- Loads external .NET assemblies into the compiler
- Discovers exported functions through reflection and caching
- Provides thread-safe access for concurrent compilation
- Gracefully handles errors without crashing compilation
- Integrates with the broader semantic analysis pipeline

Understanding this class is key to:
- Extending Sharpy's ability to call .NET libraries
- Debugging import resolution issues
- Improving compilation performance through better caching
- Contributing to the compiler's module system

The design prioritizes **robustness** (error accumulation), **performance** (caching, concurrent collections), and **usability** (flexible path resolution, verbose logging).
