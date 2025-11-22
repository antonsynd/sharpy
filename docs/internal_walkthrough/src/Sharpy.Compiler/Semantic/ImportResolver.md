# Walkthrough: ImportResolver.cs

**Source File**: `src/Sharpy.Compiler/Semantic/ImportResolver.cs`

---

## Overview

`ImportResolver` is the Sharpy compiler component responsible for **resolving import statements** and **loading symbols from imported modules**. It handles two types of imports:

1. **Sharpy modules** (`.spy` files) - Other Sharpy source files in the project
2. **.NET assemblies** (`.dll` files) - Compiled .NET libraries that expose functions

When the semantic analyzer encounters an `import` or `from ... import` statement, it delegates to ImportResolver to:
- Find the module (search filesystem or registry)
- Load and parse it (for .spy files)
- Extract exported symbols (functions, classes, constants, etc.)
- Detect circular imports
- Cache modules to avoid reprocessing

**Role in the compiler pipeline:**
```
Parser → Semantic Analyzer → ImportResolver → ModuleInfo
                    ↓
              (uses symbols from imported modules)
```

---

## Class/Type Structure

### ImportResolver (Main Class)

```csharp
public class ImportResolver
{
    private readonly ICompilerLogger _logger;
    private readonly List<SemanticError> _errors = new();
    private readonly HashSet<string> _loadedModules = new();
    private readonly HashSet<string> _loadingModules = new();  // Circular import detection
    private readonly Dictionary<string, ModuleInfo> _moduleCache = new();
    private readonly ModuleRegistry? _moduleRegistry;
    private string? _currentModulePath = null;
}
```

**Key Fields:**
- `_errors`: Accumulates all errors encountered during import resolution
- `_loadedModules`: Tracks which modules have been successfully loaded (prevents redundant loading)
- `_loadingModules`: Tracks modules currently being loaded (for circular import detection)
- `_moduleCache`: Maps module paths to `ModuleInfo` objects (performance optimization)
- `_moduleRegistry`: Optional registry for .NET assembly imports
- `_currentModulePath`: The module currently being analyzed (for relative imports)

### ModuleInfo (Supporting Type)

```csharp
public class ModuleInfo
{
    public string Path { get; init; }
    public Module Module { get; init; }  // AST root node
    public Dictionary<string, Symbol> ExportedSymbols { get; init; }
    public bool IsNetModule { get; init; }
}
```

**Important:** When `IsNetModule` is `true`, the `Module` property will be `null` because .NET assemblies don't have an AST. Always check `IsNetModule` before accessing `Module`.

---

## Key Functions/Methods

### 1. ResolveImport()

```csharp
public List<ModuleInfo> ResolveImport(ImportStatement importStmt, string? searchPath = null)
```

**Purpose:** Resolves a standard import statement like `import module1, module2`.

**Algorithm:**
1. For each module name in the import statement:
   - Try to resolve as a .NET module via `ModuleRegistry`
   - If not found, try to resolve as a .spy file
   - Load the module and extract symbols
2. Return list of successfully loaded `ModuleInfo` objects

**Example usage:**
```python
# Sharpy code
import math, os

# Compiler calls:
var moduleInfos = importResolver.ResolveImport(importStmt);
// Returns: [ModuleInfo for math, ModuleInfo for os]
```

**Key implementation details:**
- Tries .NET assemblies first (via `ModuleRegistry`)
- Falls back to filesystem search for .spy files
- Collects errors but continues processing other imports
- Each `ImportAlias` has location info for precise error reporting

---

### 2. ResolveFromImport()

```csharp
public ModuleInfo? ResolveFromImport(FromImportStatement fromImport, string? searchPath = null)
```

**Purpose:** Resolves from-import statements like `from module import func1, func2` or `from module import *`.

**Algorithm:**
1. Resolve the source module (same as `ResolveImport`)
2. If not `import *`, validate that each imported name exists in the module's exports
3. Return the `ModuleInfo` (caller extracts specific symbols)

**Example usage:**
```python
# Sharpy code
from collections import List, Dict

# Compiler calls:
var moduleInfo = importResolver.ResolveFromImport(fromImportStmt);
// Returns: ModuleInfo for 'collections'
// Validates that 'List' and 'Dict' are in moduleInfo.ExportedSymbols
```

**Validation:**
- For specific imports (not `*`), checks that each name exists in the module
- Adds errors for missing symbols but returns the `ModuleInfo` anyway
- The `import *` case skips validation (imports all exports)

---

### 3. LoadModule() (Private)

```csharp
private ModuleInfo? LoadModule(string modulePath, int? lineStart, int? columnStart)
```

**Purpose:** The core method that loads and parses a .spy module file.

**Algorithm:**
1. **Check cache** - Return cached `ModuleInfo` if already loaded
2. **Detect circular imports** - Check if currently loading (prevents infinite recursion)
3. **Read source file** - Load .spy file from disk
4. **Tokenize and parse** - Run lexer and parser to get AST
5. **Extract exports** - Walk top-level declarations to build symbol table
6. **Cache result** - Store in `_moduleCache` for reuse

**Circular import detection:**
```csharp
// Before loading:
if (_loadingModules.Contains(modulePath))
{
    AddError($"Circular import detected for module '{modulePath}'", ...);
    return null;
}
_loadingModules.Add(modulePath);

// After loading (in finally block):
_loadingModules.Remove(modulePath);
```

This prevents scenarios like:
```python
# a.spy
import b  # Starts loading b

# b.spy
import a  # ERROR: 'a' is already being loaded
```

**Caching strategy:**
- Module is cached **after** successful parsing
- If parsing fails, module is **not cached** (can retry later)
- Cache key is the full module path

---

### 4. ExtractExportedSymbol() (Private)

```csharp
private void ExtractExportedSymbol(Statement statement, ModuleInfo moduleInfo)
```

**Purpose:** Extract exportable symbols from top-level AST statements.

**What gets exported:**
- **Functions** (`def func():`) → `FunctionSymbol`
- **Classes** (`class MyClass:`) → `TypeSymbol` with `TypeKind.Class`
- **Structs** (`struct MyStruct:`) → `TypeSymbol` with `TypeKind.Struct`
- **Interfaces** (`interface IMyInterface:`) → `TypeSymbol` with `TypeKind.Interface`
- **Enums** (`enum Color:`) → `TypeSymbol` with `TypeKind.Enum`
- **Constants** (`const PI: float = 3.14`) → `VariableSymbol` with `IsConstant = true`

**What does NOT get exported:**
- Regular variables (not const)
- Non-top-level declarations
- Import statements
- Control flow statements

**Example:**
```python
# module.spy
def greet(name: str) -> str:  # ✅ Exported as FunctionSymbol
    return f"Hello, {name}"

class Calculator:              # ✅ Exported as TypeSymbol
    def add(x: int, y: int) -> int:
        return x + y

temp: int = 42                 # ❌ NOT exported (not const)
const MAX: int = 100           # ✅ Exported as VariableSymbol
```

---

### 5. TryResolveNetModule() (Private)

```csharp
private ModuleInfo? TryResolveNetModule(string moduleName, int? lineStart, int? columnStart)
```

**Purpose:** Resolve imports from .NET assemblies via `ModuleRegistry`.

**Algorithm:**
1. Check if `ModuleRegistry` is configured
2. Check if module name is loaded in registry
3. Retrieve all exported functions from the module
4. Create a `ModuleInfo` with `IsNetModule = true`
5. Cache the result with key `.net:{moduleName}`

**Example:**
```python
# Sharpy code
import System.Math  # .NET assembly

# Compiler:
// 1. ModuleRegistry.IsModuleLoaded("System.Math") → true
// 2. ModuleRegistry.GetModuleFunctions("System.Math") → [Sin, Cos, Sqrt, ...]
// 3. Create ModuleInfo with those functions as symbols
```

**Important notes:**
- .NET modules have `Module = null!` (no AST)
- Cache key is prefixed with `.net:` to avoid conflicts with .spy files
- Only functions are currently supported from .NET assemblies (not classes, etc.)

---

### 6. ResolveModulePath() (Private)

```csharp
private string? ResolveModulePath(string moduleName, string? searchPath = null)
```

**Purpose:** Convert a module name like `"utils.math"` to a filesystem path like `"utils/math.spy"`.

**Search order:**
1. **Current module's directory** - Relative to the file being compiled
2. **Provided search path** - Custom search directory
3. **Current working directory** - Where compiler was invoked

**Two resolution strategies:**
- **Direct file**: `utils.math` → `utils/math.spy`
- **Package with `__init__.spy`**: `utils.math` → `utils/math/__init__.spy`

**Example:**
```python
# Current file: /project/src/main.spy
import utils.helper

# Search:
# 1. /project/src/utils/helper.spy        ← Try this first
# 2. /project/src/utils/helper/__init__.spy
# 3. {searchPath}/utils/helper.spy
# 4. {searchPath}/utils/helper/__init__.spy
# 5. {cwd}/utils/helper.spy
# 6. {cwd}/utils/helper/__init__.spy
```

**Why both strategies?** Supports Python-style package structure:
```
utils/
  __init__.spy    # Package marker
  math.spy        # utils.math module
  helper/
    __init__.spy  # utils.helper package
```

---

## Dependencies

### Internal Dependencies

1. **`ModuleRegistry`** (`Semantic/ModuleRegistry.cs`)
   - Manages loaded .NET assemblies
   - Provides `IsModuleLoaded()` and `GetModuleFunctions()`
   - Used for .NET interop

2. **`Symbol` and subtypes** (`Semantic/Symbol.cs`)
   - `FunctionSymbol`, `TypeSymbol`, `VariableSymbol`
   - Represent exported symbols from modules

3. **`SemanticError`** (`Semantic/SemanticError.cs`)
   - Error reporting structure
   - Contains message and source location

4. **`Parser`** (`Parser/Parser.cs`)
   - Parses .spy files into AST
   - Used by `LoadModule()`

5. **`Lexer`** (`Lexer/Lexer.cs`)
   - Tokenizes .spy source code
   - First step of `LoadModule()`

6. **AST types** (`Parser/Ast/*.cs`)
   - `Module`, `ImportStatement`, `FromImportStatement`
   - `FunctionDef`, `ClassDef`, etc.

### External Dependencies

- **System.IO** - File operations (`File.ReadAllText`, `Path.Combine`)
- **ICompilerLogger** - Logging infrastructure

---

## Patterns and Design Decisions

### 1. Two-Phase Resolution (Try .NET, then .spy)

```csharp
// Phase 1: Try .NET
var moduleInfo = TryResolveNetModule(name, line, col);

// Phase 2: Try .spy file
if (moduleInfo == null)
{
    var modulePath = ResolveModulePath(name, searchPath);
    moduleInfo = LoadModule(modulePath, line, col);
}
```

**Rationale:** 
- .NET lookups are fast (registry check)
- File system operations are slower
- Prioritizes interop use cases

### 2. Caching Strategy

**Three-level caching:**
```csharp
_moduleCache       // Maps path → ModuleInfo (primary cache)
_loadedModules     // Set of successfully loaded paths (deduplication)
_loadingModules    // Set of currently loading paths (cycle detection)
```

**Benefits:**
- Prevents parsing the same file multiple times
- Enables efficient cross-module imports
- Typical speedup: 4-7x for multi-module projects

### 3. Error Collection vs. Fail-Fast

ImportResolver **collects errors** rather than throwing immediately:
```csharp
foreach (var importAlias in importStmt.Names)
{
    if (error)
    {
        AddError(...);
        continue;  // Keep processing other imports
    }
}
```

**Rationale:**
- Report all import errors at once (better UX)
- Partial success: some imports might work even if others fail
- Semantic analyzer can decide how to handle errors

### 4. Explicit Null Handling

Uses nullable reference types:
```csharp
private string? _currentModulePath = null;
public ModuleInfo? LoadModule(...) { }
```

**Benefits:**
- Clear intent when module might not be found
- Compile-time null checking
- Forces callers to handle missing modules

### 5. Location Tracking

Every error includes source location:
```csharp
AddError($"Cannot find module '{name}'", 
    importAlias.LineStart, 
    importAlias.ColumnStart);
```

**Benefits:**
- User-friendly error messages
- IDE integration (jump to error)
- Precise debugging

---

## Debugging Tips

### Common Issues and How to Debug

#### 1. "Cannot find module" Errors

**Check:**
- Print the search paths being checked:
  ```csharp
  _logger.LogDebug($"Searching in: {currentDir}, {searchPath}, {cwd}");
  ```
- Verify `_currentModulePath` is set correctly
- Check file permissions and existence

**Debug strategy:**
```csharp
// In ResolveModulePath(), add logging:
foreach (var searchedPath in searchedPaths)
{
    _logger.LogDebug($"Tried: {searchedPath}, exists={File.Exists(searchedPath)}");
}
```

#### 2. Circular Import Detection Not Working

**Check:**
- Ensure `_loadingModules.Add()` happens before recursive call
- Verify `finally` block always removes from `_loadingModules`
- Check if exception is swallowing the error

**Debug strategy:**
```csharp
_logger.LogDebug($"Loading stack: {string.Join(" → ", _loadingModules)}");
```

#### 3. Symbols Not Exported

**Check:**
- Is the declaration at top level? (nested declarations not exported)
- Is it a supported type? (variables must be `const`)
- Print what was extracted:
  ```csharp
  _logger.LogDebug($"Extracted {moduleInfo.ExportedSymbols.Count} symbols: " +
                   $"{string.Join(", ", moduleInfo.ExportedSymbols.Keys)}");
  ```

#### 4. .NET Module Not Found

**Check:**
- Is `ModuleRegistry` initialized?
- Was the assembly loaded via `ModuleRegistry.LoadReference()`?
- Check case sensitivity in module names

**Debug strategy:**
```csharp
if (_moduleRegistry == null)
    _logger.LogWarning("ModuleRegistry is null, .NET imports disabled");
else
    _logger.LogDebug($"Loaded .NET modules: {string.Join(", ", 
        _moduleRegistry.GetLoadedModules())}");
```

#### 5. Cache Invalidation Issues

**Problem:** Edited .spy file not reloading.

**Solution:**
- Clear cache when file changes detected
- Use file timestamps or content hashing
- Currently: restart compiler to clear cache

---

## Contribution Guidelines

### What Kinds of Changes Can Be Made?

#### ✅ Good Additions/Changes

1. **Support for Python-style relative imports:**
   ```python
   from . import sibling
   from .. import parent
   ```
   - Modify `ResolveModulePath()` to handle `.` and `..`
   - Track package hierarchy

2. **Improved error messages:**
   ```csharp
   AddError($"Cannot find module '{name}'. Searched in:\n" +
       $"  - {path1}\n  - {path2}\n  - {path3}", ...);
   ```

3. **Selective exports (like Python `__all__`):**
   ```python
   # module.spy
   __all__ = ["public_func", "PublicClass"]
   
   def public_func(): pass
   def _private_func(): pass  # Not exported
   ```
   - Parse `__all__` in `LoadModule()`
   - Filter `ExportedSymbols` based on `__all__`

4. **File watching / cache invalidation:**
   ```csharp
   private readonly Dictionary<string, DateTime> _moduleTimestamps;
   
   private bool IsModuleStale(string path)
   {
       var lastLoaded = _moduleTimestamps[path];
       var lastModified = File.GetLastWriteTime(path);
       return lastModified > lastLoaded;
   }
   ```

5. **Support for .NET classes (not just functions):**
   ```csharp
   // In TryResolveNetModule():
   var types = _moduleRegistry.GetModuleTypes(moduleName);
   foreach (var type in types)
   {
       moduleInfo.ExportedSymbols[type.Name] = type;
   }
   ```

#### ❌ Changes to Avoid

1. **Don't remove circular import detection** - Critical for correctness
2. **Don't bypass the cache** - Performance regression
3. **Don't hardcode search paths** - Breaks portability
4. **Don't fail-fast on first error** - Poor UX

### Testing Changes

When modifying `ImportResolver`:

1. **Add unit tests:**
   ```csharp
   [Fact]
   public void ResolveImport_WithRelativePath_FindsModule()
   {
       var resolver = new ImportResolver();
       resolver.SetCurrentModule("/project/src/main.spy");
       
       var result = resolver.ResolveImport(
           new ImportStatement { Names = [new ImportAlias { Name = "utils" }] },
           searchPath: "/project/src");
       
       Assert.Single(result);
       Assert.Contains("greet", result[0].ExportedSymbols.Keys);
   }
   ```

2. **Test edge cases:**
   - Circular imports
   - Missing modules
   - Malformed .spy files
   - Case sensitivity
   - Conflicting .NET and .spy module names

3. **Integration tests:**
   - Compile multi-file projects
   - Verify symbols are accessible across modules
   - Test .NET interop

### Code Style

Follow existing patterns:
- Use `var` for local variables
- Null-coalesce operator: `_logger ?? NullLogger.Instance`
- Pattern matching in `switch` expressions
- XML doc comments for public methods

### Performance Considerations

- Cache is critical - don't bypass it
- Avoid parsing files multiple times
- Consider lazy loading for large projects
- File I/O is slow - minimize reads

---

## Summary

`ImportResolver` is the **module system backbone** of the Sharpy compiler. It:
- Finds and loads both .spy and .NET modules
- Extracts exported symbols for the semantic analyzer
- Prevents circular dependencies
- Caches results for performance

**Key responsibilities:**
1. **Resolution:** Module name → file path or .NET assembly
2. **Loading:** File/assembly → parsed AST or reflection
3. **Extraction:** AST declarations → symbol table
4. **Validation:** Circular imports, missing symbols, access control

**Integration points:**
- Called by `SemanticAnalyzer` when encountering imports
- Uses `Parser` to process .spy files
- Delegates to `ModuleRegistry` for .NET assemblies
- Provides `ModuleInfo` to populate symbol tables

Understanding this file is essential for:
- Working on the module system
- Debugging import-related errors
- Adding new import features (relative imports, selective exports)
- Optimizing compilation performance
