# Walkthrough: ModuleRegistry.cs

**Source File**: `src/Sharpy.Compiler/Semantic/ModuleRegistry.cs`

---

## 1. Overview

`ModuleRegistry` is the **gateway for loading and managing external .NET assemblies** in the Sharpy compiler. When Sharpy code imports a .NET library (like `import System.Math` or a custom C# assembly), this class is responsible for:

- **Locating** the assembly file (searching multiple paths)
- **Loading** it into the compiler's runtime using .NET reflection
- **Discovering** all exported functions from classes named `Exports`
- **Providing** those function signatures to the semantic analyzer for type checking

Think of it as a **module loader and function catalog** that bridges Sharpy's Python-like imports with .NET's assembly system.

### Role in the Compiler Pipeline

```
Sharpy Source (.spy)
    ↓
Parser → Semantic Analyzer
              ↓
       (Sees import statement)
              ↓
       ModuleRegistry.LoadReference("MyLibrary.dll")
              ↓
       Assembly.LoadFrom() + Function Discovery
              ↓
       Returns FunctionSymbol list to semantic analyzer
              ↓
       Type checking proceeds with imported functions
```

---

## 2. Class/Type Structure

### Main Class: `ModuleRegistry`

```csharp
public class ModuleRegistry
```

**Thread-Safety**: This class is designed for **concurrent use**. Multiple threads can safely load modules or query functions simultaneously thanks to `ConcurrentDictionary` and `ConcurrentBag` data structures.

### Key Fields

| Field | Type | Purpose |
|-------|------|---------|
| `_discovery` | `CachedModuleDiscovery` | Handles reflection-based discovery of functions from assemblies. Uses caching to avoid re-scanning assemblies. |
| `_logger` | `ICompilerLogger` | Logs debug/info/warning messages for diagnostics |
| `_loadedAssemblies` | `ConcurrentDictionary<string, Assembly>` | Maps assembly names → loaded `Assembly` objects. Prevents duplicate loads. |
| `_modulePaths` | `ConcurrentBag<string>` | Additional directories to search when resolving assembly paths |
| `_errors` | `ConcurrentBag<SemanticError>` | Collects errors during module loading (thread-safe) |

### Public Properties

```csharp
public IReadOnlyList<SemanticError> Errors => _errors.ToList();
```

Exposes all errors encountered during module loading as an immutable list.

---

## 3. Key Functions/Methods

### Constructor: `ModuleRegistry(ICompilerLogger?, OverloadIndexCache?)`

```csharp
public ModuleRegistry(ICompilerLogger? logger = null, OverloadIndexCache? cache = null)
{
    _discovery = new CachedModuleDiscovery(cache);
    _logger = logger ?? NullLogger.Instance;
}
```

**What it does**: Initializes the registry with optional logging and caching infrastructure.

**Key Parameters**:
- `logger`: For debug/info/warning output. Defaults to `NullLogger` (no-op) if not provided.
- `cache`: An `OverloadIndexCache` instance for persisting discovered function signatures. If `null`, uses default cache directory.

**Design Decision**: Optional parameters with sensible defaults make the class easy to use in tests (no logger needed) while still supporting production diagnostics.

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

**What it does**: Registers an additional directory to search when resolving assembly paths.

**Use Case**: When compiling a Sharpy project with custom dependencies:
```bash
# Sharpy code might do: import MyCustomLib
# Compiler would call: registry.AddModulePath("./libs")
# So it can find "./libs/MyCustomLib.dll"
```

**Note**: The code allows duplicate paths in `_modulePaths` (it's a `ConcurrentBag`). This is intentional and acceptable because `ResolveAssemblyPath` will find the assembly on the first match anyway.

---

### `LoadReference(string assemblyPath)` ⭐ Core Method

```csharp
public bool LoadReference(string assemblyPath)
```

**What it does**: The **main entry point** for loading a .NET assembly into the compiler's module system.

**Algorithm**:
1. **Resolve Path**: Call `ResolveAssemblyPath()` to find the actual `.dll` file
2. **Load Assembly**: Use `Assembly.LoadFrom()` to load it into the runtime
3. **De-duplicate**: Use `TryAdd` to ensure we don't load the same assembly twice
4. **Discover Functions**: Delegate to `_discovery.LoadAssembly()` to scan for exported functions
5. **Log Success/Failure**: Record info or add errors

**Return Value**: `true` if loaded successfully, `false` if errors occurred

**Exception Handling**:
```csharp
catch (IOException ex)               // File not found, locked, etc.
catch (BadImageFormatException ex)   // Not a valid .NET assembly
catch (UnauthorizedAccessException ex) // Permission denied
```

All exceptions are caught and converted to `SemanticError` objects, making the compiler robust against invalid module references.

**Thread-Safety Note**: `TryAdd` is atomic. If two threads try to load the same assembly simultaneously, only one will succeed and the other will see "already loaded".

---

### `ResolveAssemblyPath(string assemblyPath)` 🔍 Path Resolution Logic

```csharp
private string? ResolveAssemblyPath(string assemblyPath)
```

**What it does**: Searches for an assembly file using a **fallback chain**:

1. **Absolute/Relative Path**: If `assemblyPath` is already a valid path → use it
2. **Current Directory**: Try `./assemblyPath`
3. **Module Search Paths**: For each path in `_modulePaths`:
   - Try `<searchPath>/assemblyPath`
   - If no `.dll` extension, try `<searchPath>/assemblyPath.dll`

**Return Value**: Full absolute path to the assembly, or `null` if not found

**Important Comment**:
```csharp
// TOCTOU (Time-of-Check-Time-of-Use) race condition is acceptable here
```

**What this means**: There's a small window between checking if a file exists (`File.Exists`) and actually loading it (`Assembly.LoadFrom`). The file could be deleted or moved in between. However, this is acceptable because `LoadReference()` catches all exceptions from the load attempt.

**Example Flow**:
```
Input: "MyLib"
1. Check: "./MyLib" → Not found
2. Check: "/current/dir/MyLib" → Not found
3. Check: "/module/path/1/MyLib" → Not found
4. Check: "/module/path/1/MyLib.dll" → Found!
   Return: "/absolute/path/to/module/path/1/MyLib.dll"
```

---

### `GetModuleFunctions(string moduleName)`

```csharp
public List<FunctionSymbol> GetModuleFunctions(string moduleName)
```

**What it does**: Retrieves all exported functions from a loaded module.

**Key Parameter**: `moduleName` should match the assembly/namespace name (e.g., `"System.Math"`)

**Return Value**: List of `FunctionSymbol` objects containing:
- Function name
- Parameter types and names
- Return type
- Access modifiers

**Exception Handling**: Catches `KeyNotFoundException` (module not found) and `InvalidOperationException` (discovery errors), returning an empty list instead of crashing.

**Use Case**: During semantic analysis, when the compiler sees:
```python
import MyModule
result = MyModule.some_function(42)
```

The semantic analyzer calls `GetModuleFunctions("MyModule")` to validate that `some_function` exists and check its signature.

---

### `GetLoadedModules()` & `IsModuleLoaded(string moduleName)`

```csharp
public IEnumerable<string> GetLoadedModules()
public bool IsModuleLoaded(string moduleName)
```

**What they do**: Query methods for introspection and validation.

**Use Cases**:
- **Error Messages**: "Module 'Foo' not loaded. Did you forget to import it?"
- **Debugging**: Print all loaded modules when compiler runs with `--verbose`
- **Optimization**: Skip loading if already present

**Implementation Note**: `IsModuleLoaded` uses case-insensitive comparison to match .NET's assembly naming conventions.

---

### `ClearCache()`

```csharp
public void ClearCache()
{
    _discovery.ClearCache();
    _logger.LogInfo("Cleared module discovery cache");
}
```

**What it does**: Deletes cached function signature data, forcing a full reflection scan on next load.

**When to Use**:
- Development: After recompiling a C# library that Sharpy imports
- CI/CD: Ensuring clean builds
- Debugging: When cached data might be stale or corrupted

---

### `AddError(string message)` - Private Helper

```csharp
private void AddError(string message)
{
    _errors.Add(new SemanticError(message, null, null));
}
```

**What it does**: Thread-safe way to record an error without throwing an exception.

**Design Decision**: Errors are collected rather than thrown immediately, allowing the compiler to report multiple module loading failures at once instead of stopping at the first error.

---

## 4. Dependencies

### Internal Dependencies

| Namespace | Purpose |
|-----------|---------|
| `Sharpy.Compiler.Discovery` | Contains `CachedModuleDiscovery` for reflection-based function discovery |
| `Sharpy.Compiler.Discovery.Caching` | Provides `OverloadIndexCache` for persisting discovered function signatures |
| `Sharpy.Compiler.Logging` | Interface for compiler diagnostics (`ICompilerLogger`, `NullLogger`) |
| `Sharpy.Compiler.Semantic` | Symbol types (`FunctionSymbol`, `SemanticError`) |

### External Dependencies

| .NET Type | Purpose |
|-----------|---------|
| `System.Reflection.Assembly` | Core .NET API for loading and inspecting assemblies |
| `System.Collections.Concurrent.*` | Thread-safe collections for concurrent module loading |
| `System.IO.*` | File system operations for path resolution |

### Key Related Files

- **`CachedModuleDiscovery.cs`**: Handles the actual reflection logic to find `Exports` classes and extract function signatures
- **`Symbol.cs`**: Defines `FunctionSymbol` and related symbol types
- **`SemanticError.cs`**: Error type used throughout the semantic analysis phase

---

## 5. Patterns and Design Decisions

### 1. **Facade Pattern**
`ModuleRegistry` acts as a simplified interface to the complex subsystem of assembly loading + caching + function discovery. Clients don't need to understand `CachedModuleDiscovery`, `OverloadIndexBuilder`, or cache invalidation—they just call `LoadReference()`.

### 2. **Thread-Safety Without Locks**
Uses lock-free concurrent collections (`ConcurrentDictionary`, `ConcurrentBag`) for performance:
- `TryAdd` for atomic "check if exists, then insert"
- `GetOrAdd` in `CachedModuleDiscovery` for lazy loading with deduplication
- No explicit `lock` statements → avoids contention

**Trade-off**: `ConcurrentBag` allows duplicates in `_modulePaths`, but this is acceptable for the use case.

### 3. **Defensive Error Handling**
Every external dependency (file system, reflection API) is wrapped in try-catch blocks. This prevents a single bad module reference from crashing the entire compilation.

### 4. **Separation of Concerns**
- **ModuleRegistry**: High-level orchestration (loading, path resolution, error tracking)
- **CachedModuleDiscovery**: Low-level reflection and caching
- **OverloadIndexCache**: Persistence layer

Each class has a single, clear responsibility.

### 5. **Null Object Pattern**
```csharp
_logger = logger ?? NullLogger.Instance;
```
Uses `NullLogger` (a no-op logger) instead of null checks everywhere. Makes code cleaner and avoids `NullReferenceException`.

### 6. **Caching Strategy**
Function discovery is expensive (requires reflection on every type and method in an assembly). The caching layer:
- Stores discovered functions as serialized `OverloadIndex` objects
- Uses assembly identity (name + version + hash) as cache key
- Invalidates cache if assembly changes

**Performance Impact**: First load is slow (~100ms for large assemblies), subsequent loads are fast (~1ms).

---

## 6. Debugging Tips

### Problem: "Assembly not found" Error

**Check**:
1. Print resolved paths: Add a breakpoint in `ResolveAssemblyPath` and inspect the search order
2. Verify `_modulePaths` contents: Ensure paths were added correctly
3. Check file extensions: Assembly might be named `MyLib.dll` but code passes `MyLib`

**Quick Debug Code**:
```csharp
// Add this before LoadReference returns false
Console.WriteLine($"Searched paths: {string.Join(", ", _modulePaths)}");
Console.WriteLine($"Resolved path: {ResolveAssemblyPath(assemblyPath)}");
```

---

### Problem: Functions Not Found After Loading

**Check**:
1. Does the assembly have an `Exports` class? `CachedModuleDiscovery` only scans classes named `Exports`
2. Are functions public and static? Non-public or instance methods are ignored
3. Check the cache: Run `registry.ClearCache()` and try again

**How to Inspect**:
```csharp
var functions = registry.GetModuleFunctions("MyModule");
Console.WriteLine($"Found {functions.Count} functions:");
foreach (var fn in functions) {
    Console.WriteLine($"  {fn.Name}({string.Join(", ", fn.Parameters.Select(p => p.Type))}) -> {fn.ReturnType}");
}
```

---

### Problem: Slow Compilation on Module Import

**Likely Cause**: Cache miss → full reflection scan

**Check Cache Status**:
- Look for cache directory (default: `~/.sharpy/cache/` or similar)
- Check if assembly version changed (cache uses assembly hash, so any recompilation invalidates it)

**Solution**: Cache warming in CI:
```bash
# Pre-load common modules during build
sharpy-cli load-module System.dll
sharpy-cli load-module MyCommonLib.dll
```

---

### Problem: Thread-Safety Issues (Race Conditions)

**Symptoms**: Intermittent crashes, duplicate loads, missing functions

**Check**:
- Is `_loadedAssemblies.TryAdd()` returning false unexpectedly?
- Are multiple threads calling `LoadReference` simultaneously with the same assembly?

**Debug with Logging**:
```csharp
_logger.LogDebug($"[Thread {Thread.CurrentThread.ManagedThreadId}] Loading {assemblyPath}");
```

---

### Problem: Permission Denied / UnauthorizedAccessException

**Common Causes**:
- Assembly locked by another process (e.g., running application)
- Incorrect file permissions
- Anti-virus software blocking .NET assembly loads

**Workaround**: Copy assembly to temp directory before loading:
```csharp
var tempPath = Path.Combine(Path.GetTempPath(), Path.GetFileName(resolvedPath));
File.Copy(resolvedPath, tempPath, overwrite: true);
var assembly = Assembly.LoadFrom(tempPath);
```

---

## 7. Contribution Guidelines

### Adding Features

#### 1. **Support Non-`Exports` Class Discovery**

**Current Limitation**: Only scans classes named `Exports`.

**Proposal**: Add configuration to specify class name patterns:
```csharp
registry.ConfigureDiscovery(classPattern: "*Exports");
```

**Files to Modify**:
- `ModuleRegistry.cs`: Add configuration property
- `CachedModuleDiscovery.cs`: Pass pattern to `OverloadIndexBuilder`
- `OverloadIndexBuilder.cs`: Use pattern in reflection scan

---

#### 2. **Assembly Version Conflict Resolution**

**Current Behavior**: If two assemblies reference different versions of a dependency, behavior is undefined.

**Proposal**: Add version resolution strategy:
```csharp
registry.SetVersionResolution(VersionResolution.Newest);
```

**Files to Modify**:
- `ModuleRegistry.cs`: Add version policy enum and configuration
- Add new class `AssemblyVersionResolver.cs`

---

#### 3. **Hot Reload / Watch Mode**

**Use Case**: During development, automatically reload assemblies when they change.

**Proposal**:
```csharp
registry.EnableFileWatcher("/path/to/modules");
```

**Implementation**:
- Use `FileSystemWatcher` to detect `.dll` changes
- Call `ClearCache()` and `LoadReference()` on change
- Emit event for compiler to re-analyze code

**Files to Modify**:
- `ModuleRegistry.cs`: Add `FileSystemWatcher` field and event
- Consider race conditions (file still being written)

---

### Bug Fixes

#### Common Issues to Look For

1. **Path Separator Issues**: Windows (`\`) vs Unix (`/`)
   - Check: `Path.Combine` is used everywhere (not string concatenation)

2. **Case Sensitivity**: Module names might not match on case-sensitive file systems
   - Check: `StringComparison.OrdinalIgnoreCase` is used for comparisons

3. **Assembly Unloading**: .NET Framework doesn't support unloading, .NET Core does
   - Check: Are assemblies ever unloaded? Could cause memory leaks in long-running processes

4. **Cache Invalidation**: Cache key might not detect all assembly changes
   - Check: Does `AssemblyIdentity` hash include all relevant metadata?

---

### Testing Additions

#### Current Test Gaps

1. **Concurrent Loading**: No tests for multiple threads loading the same assembly
   ```csharp
   [Fact]
   public void TestConcurrentLoadReference()
   {
       var tasks = Enumerable.Range(0, 10)
           .Select(_ => Task.Run(() => registry.LoadReference("System.dll")))
           .ToArray();
       Task.WaitAll(tasks);
       // Verify: Only one assembly instance in _loadedAssemblies
   }
   ```

2. **Path Resolution Edge Cases**: Symlinks, UNC paths, relative paths with `..`
3. **Error Recovery**: Can the registry recover after a failed load?

#### Where to Add Tests

- **Unit Tests**: `src/Sharpy.Compiler.Tests/Semantic/ModuleRegistryTests.cs`
- **Integration Tests**: `src/Sharpy.Compiler.Tests/Integration/ModuleLoadingTests.cs`

---

### Code Quality Improvements

#### 1. **Extract Path Resolution to Separate Class**

**Current**: `ResolveAssemblyPath` is private method (~35 lines)

**Proposal**: Create `AssemblyPathResolver` class for easier testing and reuse:
```csharp
public class AssemblyPathResolver
{
    public string? Resolve(string assemblyPath, IEnumerable<string> searchPaths);
}
```

---

#### 2. **Add Metrics/Telemetry**

**Use Case**: Track how long module loading takes, how often cache hits occur

**Proposal**:
```csharp
public class ModuleRegistryMetrics
{
    public int CacheHits { get; }
    public int CacheMisses { get; }
    public TimeSpan TotalLoadTime { get; }
}

public ModuleRegistryMetrics GetMetrics();
```

---

#### 3. **Better Error Messages**

**Current**: Generic messages like "Assembly not found"

**Improvement**: Include search details:
```
Assembly 'Foo.dll' not found.
Searched paths:
  - /current/dir/Foo.dll
  - /module/path/1/Foo.dll
  - /module/path/2/Foo.dll
Did you forget to call AddModulePath()?
```

---

### Documentation Additions

- **Add XML doc comments** for internal methods like `ResolveAssemblyPath`
- **Create architecture diagram** showing `ModuleRegistry` → `CachedModuleDiscovery` → `OverloadIndexBuilder` flow
- **Document thread-safety guarantees** more explicitly in class-level comments

---

## Summary Checklist for Contributors

When modifying `ModuleRegistry.cs`:

- [ ] Maintain thread-safety (use concurrent collections)
- [ ] Add defensive error handling (catch reflection/IO exceptions)
- [ ] Update cache invalidation logic if changing discovery
- [ ] Add logging statements for debugging
- [ ] Update tests (both unit and integration)
- [ ] Verify path resolution works on Windows and Unix
- [ ] Check performance impact on large projects (100+ modules)
- [ ] Update this walkthrough document if adding public APIs

---

## Further Reading

- **Related Walkthroughs**:
  - `CachedModuleDiscovery.md` - Deep dive into function discovery and caching
  - `SymbolTable.md` - How discovered functions integrate into semantic analysis

- **Sharpy Documentation**:
  - `docs/specs/interop.md` - .NET interop specification
  - `.github/instructions/Sharpy.Compiler/HOW_TO_CONTRIBUTE.instructions.md` - Compiler contribution guide

- **.NET Documentation**:
  - [Assembly.LoadFrom() Best Practices](https://docs.microsoft.com/en-us/dotnet/framework/app-domains/best-practices-for-assembly-loading)
  - [ConcurrentDictionary<TKey,TValue>](https://docs.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2)
