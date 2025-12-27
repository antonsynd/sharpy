# Walkthrough: ImportResolver.cs

**Source File**: `src/Sharpy.Compiler/Semantic/ImportResolver.cs`

---

## 1. Overview

`ImportResolver` is the cornerstone of Sharpy's module system. It handles the discovery, loading, and validation of both Sharpy source modules (`.spy` files) and .NET assembly modules. Think of it as the "librarian" that finds the books (modules) you request via `import` and `from...import` statements.

**Key Responsibilities:**
- Resolve module names to file paths (e.g., `my.module` → `my/module.spy`)
- Load and parse `.spy` source files into AST modules
- Interface with `ModuleRegistry` to access .NET assemblies
- Extract exported symbols from loaded modules (functions, classes, constants, etc.)
- Detect circular imports
- Cache loaded modules for performance
- Validate that imported symbols actually exist in the target module

**Position in the Compiler Pipeline:**
```
Parser → ImportResolver (during NameResolver) → TypeResolver → TypeChecker → CodeGen
```

Import resolution happens during the semantic analysis phase, specifically during name resolution when the compiler encounters `import` or `from...import` statements.

---

## 2. Class Structure

### Main Class: `ImportResolver`

```csharp
public class ImportResolver
{
    private readonly ICompilerLogger _logger;
    private readonly List<SemanticError> _errors;
    private readonly HashSet<string> _loadedModules;
    private readonly HashSet<string> _loadingModules;  // Circular import detection
    private readonly Dictionary<string, ModuleInfo> _moduleCache;
    private readonly ModuleRegistry? _moduleRegistry;
    private string? _currentModulePath;
}
```

**Field Breakdown:**

- **`_logger`**: For diagnostic logging (debug, info, warnings)
- **`_errors`**: Accumulates semantic errors encountered during import resolution
- **`_loadedModules`**: Tracks successfully loaded module paths to avoid redundant work
- **`_loadingModules`**: Temporary set used to detect circular dependencies (e.g., `A imports B imports A`)
- **`_moduleCache`**: Maps module paths to `ModuleInfo` objects for fast lookup
- **`_moduleRegistry`**: Optional registry for accessing preloaded .NET assemblies
- **`_currentModulePath`**: Context for resolving relative imports (set via `SetCurrentModule`)

### Supporting Class: `ModuleInfo`

```csharp
public class ModuleInfo
{
    public string Path { get; init; }
    public Module Module { get; init; }  // AST representation (null for .NET modules)
    public Dictionary<string, Symbol> ExportedSymbols { get; init; }
    public bool IsNetModule { get; init; }
}
```

This is the data structure returned after successfully resolving an import. It contains:
- The module's file path (or `.net:ModuleName` for .NET assemblies)
- The parsed AST `Module` (null for .NET assemblies since they don't have Sharpy source)
- A dictionary of all symbols this module exports
- A flag indicating whether it's a .NET module

**⚠️ Important**: Always check `IsNetModule` before accessing `Module` property to avoid null references!

---

## 3. Key Methods

### 3.1 `ResolveImport` - Handle `import` Statements

```csharp
public List<ModuleInfo> ResolveImport(ImportStatement importStmt, string? searchPath = null)
```

**Purpose**: Resolves `import module1, module2 as alias` statements.

**Algorithm:**
1. Iterate through each module name in the import statement
2. First attempt: Try to resolve as a .NET module via `ModuleRegistry`
3. Fallback: If not a .NET module, resolve as a `.spy` file
4. Collect all successfully resolved modules
5. Record errors for any modules that couldn't be found

**Example Input:**
```python
import math, my_utils, System.Collections
```

**Returns**: A list of `ModuleInfo` objects, one per successfully resolved module.

**Error Handling**: If a module can't be found, an error is added to `_errors` but the method continues processing remaining modules (fail-soft approach).

---

### 3.2 `ResolveFromImport` - Handle `from...import` Statements

```csharp
public ModuleInfo? ResolveFromImport(FromImportStatement fromImport, string? searchPath = null)
```

**Purpose**: Resolves `from module import symbol1, symbol2` statements.

**Algorithm:**
1. Resolve the source module (same logic as `ResolveImport`: .NET first, then `.spy`)
2. If `from module import *`, skip validation (all symbols will be imported)
3. Otherwise, validate that each imported symbol exists in the module's `ExportedSymbols`
4. Record errors for missing symbols

**Example Input:**
```python
from my_math import add, subtract, nonexistent_func  # Error on nonexistent_func
```

**Returns**: The `ModuleInfo` for the source module, or `null` if the module couldn't be found.

**Key Difference from `ResolveImport`**: This method validates individual symbol existence, whereas `ResolveImport` only validates module existence.

---

### 3.3 `LoadModule` - Load and Parse Sharpy Source Files

```csharp
private ModuleInfo? LoadModule(string modulePath, int? lineStart, int? columnStart)
```

**Purpose**: The workhorse method that actually loads a `.spy` file from disk, parses it, and extracts exported symbols.

**Algorithm:**
1. **Check cache**: Return immediately if already loaded
2. **Circular import detection**: Check if currently being loaded (in `_loadingModules`)
3. **Mark as loading**: Add to `_loadingModules` to catch circular dependencies
4. **Read source**: Load file contents from disk
5. **Tokenize**: Run the lexer to produce tokens
6. **Parse**: Run the parser to produce an AST `Module`
7. **Extract exports**: Walk the module's top-level statements to collect exported symbols
8. **Cache and mark loaded**: Store in `_moduleCache` and add to `_loadedModules`
9. **Cleanup**: Remove from `_loadingModules` in a `finally` block

**Circular Import Detection Example:**
```
Module A loads → calls LoadModule("B")
  Module B loads → calls LoadModule("A")
    Module A already in _loadingModules → Error!
```

**Error Recovery**: Uses try-catch to handle file I/O errors, parse errors, etc. Returns `null` on failure.

---

### 3.4 `ExtractExportedSymbol` - Collect Module Exports

```csharp
private void ExtractExportedSymbol(Statement statement, ModuleInfo moduleInfo)
```

**Purpose**: Examines a top-level statement and adds exportable symbols to `moduleInfo.ExportedSymbols`.

**Exported Statement Types:**

| Statement Type | Symbol Created | Notes |
|---------------|----------------|-------|
| `FunctionDef` | `FunctionSymbol` | All top-level functions are exported |
| `ClassDef` | `TypeSymbol` (Class) | All top-level classes are exported |
| `StructDef` | `TypeSymbol` (Struct) | All top-level structs are exported |
| `InterfaceDef` | `TypeSymbol` (Interface) | All top-level interfaces are exported |
| `EnumDef` | `TypeSymbol` (Enum) | All top-level enums are exported |
| `VariableDeclaration` (const) | `VariableSymbol` | Only constants are exported |

**Design Decision**: Sharpy follows Python's convention where all top-level declarations are implicitly public and exported. There's no `export` keyword or `__all__` list (though this might be added in the future).

**Non-exported Items:**
- Regular variable declarations (non-const)
- Nested functions/classes (defined inside other functions/classes)
- Expressions, control flow statements, etc.

---

### 3.5 `TryResolveNetModule` - Load .NET Assembly Modules

```csharp
private ModuleInfo? TryResolveNetModule(string moduleName, int? lineStart, int? columnStart)
```

**Purpose**: Attempts to resolve a module name as a .NET assembly that's been registered with the `ModuleRegistry`.

**Algorithm:**
1. Check if `ModuleRegistry` is available (it's optional)
2. Ask the registry if this module is loaded
3. Check cache (keyed by `.net:ModuleName`)
4. Retrieve all exported functions from the registry
5. Create a `ModuleInfo` with `IsNetModule = true` and `Module = null!`
6. Populate `ExportedSymbols` with function symbols

**Example:**
```python
# If System.Math.dll is loaded in ModuleRegistry
import System.Math
# TryResolveNetModule finds it and returns ModuleInfo with exported functions
```

**Why Module is null**: .NET assemblies don't have Sharpy AST representations. They're already compiled IL. The `null!` (null-forgiving operator) documents this is intentional.

---

### 3.6 `ResolveModulePath` - Find `.spy` Files

```csharp
private string? ResolveModulePath(string moduleName, string? searchPath = null)
```

**Purpose**: Converts a Python-style dotted module name to a file system path.

**Search Strategy** (in order):

1. **Current module's directory** (if `_currentModulePath` is set)
   - Try `current/dir/module/submodule.spy`
   - Try `current/dir/module/submodule/__init__.spy` (package)

2. **Provided search path**
   - Try `searchPath/module/submodule.spy`
   - Try `searchPath/module/submodule/__init__.spy`

3. **Current working directory**
   - Try `cwd/module/submodule.spy`
   - Try `cwd/module/submodule/__init__.spy`

**Module Name Transformation:**
```python
# Input: "my.nested.module"
# Transforms to: "my/nested/module.spy" (on Unix) or "my\nested\module.spy" (on Windows)
```

**Package Support**: If `module/__init__.spy` exists, the module is treated as a package. This matches Python's package system.

**Returns**: Full absolute path to the module file, or `null` if not found.

**Debugging Tip**: On failure, logs all searched paths via `_logger.LogDebug()` to help diagnose why a module wasn't found.

---

### 3.7 `AddError` - Error Recording Helper

```csharp
private void AddError(string message, int? line, int? column)
```

**Purpose**: Centralized error recording that adds context (current module name) to error messages.

**Behavior**: If `_currentModulePath` is set, appends `(in filename.spy)` to the error message for better diagnostics.

---

## 4. Dependencies

### Internal Dependencies

- **`Parser.Ast`**: Needs AST node types (`ImportStatement`, `FromImportStatement`, `FunctionDef`, `ClassDef`, etc.)
- **`Lexer`**: Uses `Lexer.Lexer` to tokenize source files
- **`Parser`**: Uses `Parser.Parser` to parse tokens into AST
- **`Symbol`**: Creates various symbol types (`FunctionSymbol`, `TypeSymbol`, `VariableSymbol`)
- **`ModuleRegistry`**: Optional dependency for .NET assembly support
- **`ICompilerLogger`**: For logging and diagnostics
- **`SemanticError`**: For error reporting

### External Dependencies

- **`System.IO.File`**: Reading `.spy` files from disk
- **`System.IO.Path`**: Path manipulation and normalization

### Related Components in the Pipeline

**Upstream:**
- **Parser**: Produces `ImportStatement` and `FromImportStatement` nodes that this class processes

**Downstream:**
- **NameResolver**: Calls `ImportResolver` to populate the symbol table with imported symbols
- **TypeResolver**: Uses the symbols loaded by `ImportResolver` to resolve type references

---

## 5. Design Patterns and Decisions

### 5.1 Dual-Source Module Resolution

**Pattern**: Strategy pattern with fallback chain

The resolver tries .NET assemblies first, then falls back to Sharpy source files. This allows seamless interop:

```python
import System.Collections  # .NET assembly
import my_sharpy_module    # .spy file
```

**Rationale**: .NET modules are checked first because they're expected to be more common in production code and they're faster to validate (no parsing needed).

### 5.2 Caching Strategy

**Pattern**: Memoization with multi-key cache

The `_moduleCache` stores loaded modules by path. For .NET modules, the key is prefixed with `.net:` to avoid collisions with file paths.

**Benefits**:
- Prevents re-parsing the same `.spy` file multiple times
- Avoids duplicate symbol extraction
- Speeds up compilation when a module is imported from multiple places

**Cache Key Examples:**
```
/full/path/to/my_module.spy
.net:System.Collections
.net:MyCustomAssembly
```

### 5.3 Circular Import Detection

**Pattern**: Temporary marker set

Using `_loadingModules` as a stack-like structure to track the current loading chain. When `LoadModule` is called recursively, it checks this set before proceeding.

**Why Not Track the Full Chain?**: Only need to detect cycles, not report the full dependency path. A simple set membership check suffices.

**Cleanup**: The `finally` block ensures `_loadingModules` is cleaned up even if loading fails.

### 5.4 Fail-Soft Error Handling

**Pattern**: Error accumulation

Rather than throwing exceptions on first error, `ImportResolver` collects errors in `_errors` list and continues processing. This provides better developer experience by showing all import errors at once.

**Example:**
```python
import module1, missing_module, module3
# Shows errors for missing_module but still processes module1 and module3
```

### 5.5 Implicit Export Model

**Design Decision**: All top-level declarations are public/exported by default.

This differs from some languages (like TypeScript) that require explicit `export` keywords. The rationale:
- Matches Python's behavior (Sharpy is Pythonic)
- Simpler for beginners
- Can add explicit export control later if needed (via `__all__` or similar)

### 5.6 Null Safety for .NET Modules

**Pattern**: Nullable reference types with documentation

The `ModuleInfo.Module` property is marked as `null!` for .NET modules, and the class has XML comments warning consumers to check `IsNetModule` first.

**Alternative Considered**: Using a discriminated union or separate classes for `.spy` vs .NET modules. Rejected for simplicity - a flag is more straightforward.

---

## 6. Debugging Tips

### 6.1 Enable Debug Logging

When imports fail mysteriously, turn on debug logging to see the search paths:

```csharp
// In compiler setup
var logger = new ConsoleLogger(LogLevel.Debug);
var resolver = new ImportResolver(logger);
```

You'll see output like:
```
Module 'my_module' not found. Searched: [/path1/my_module.spy, /path2/my_module.spy, ...]
```

### 6.2 Check Module Cache State

If modules aren't reloading as expected, inspect the cache:

```csharp
// In debugger or diagnostic code
Console.WriteLine($"Loaded modules: {string.Join(", ", resolver._loadedModules)}");
Console.WriteLine($"Cache size: {resolver._moduleCache.Count}");
```

### 6.3 Circular Import Detection

If you get "Circular import detected", trace through `_loadingModules`:

**Debugging Strategy:**
1. Set breakpoint in `LoadModule` before circular check
2. Inspect `_loadingModules.Contains(modulePath)` 
3. Look at call stack to see the import chain

**Common Cause**: Two modules importing each other at the top level. Solution: Move one import inside a function or refactor to break the cycle.

### 6.4 Symbol Not Found Errors

When `from module import symbol` fails with "has no exported symbol":

**Checklist:**
1. Is the symbol spelled correctly? (case-sensitive!)
2. Is it defined at the top level of the module?
3. For variables, is it marked `const`? (non-const variables aren't exported)
4. For nested items, they're not exported (by design)

**Debugging:**
```csharp
// In debugger after module load
var symbols = moduleInfo.ExportedSymbols.Keys;
// Inspect to see what's actually exported
```

### 6.5 .NET Module vs Source Module Confusion

If you're getting `NullReferenceException` on `moduleInfo.Module`:

**Root Cause**: Trying to access `Module` property on a .NET module.

**Fix**: Always check `IsNetModule` first:
```csharp
if (!moduleInfo.IsNetModule)
{
    var astModule = moduleInfo.Module;  // Safe
    // Process AST...
}
```

### 6.6 Path Resolution Issues

Module not found but file exists?

**Common Issues:**
- Using wrong path separator (`\` vs `/`) - `Path.DirectorySeparatorChar` handles this
- Relative vs absolute paths - use `Path.GetFullPath()` to normalize
- Case sensitivity on Unix vs Windows
- `__init__.spy` missing from package directories

**Debug Technique:**
1. Add breakpoint in `ResolveModulePath`
2. Step through each search location
3. Manually check if file exists at each path
4. Look for typos in directory names

---

## 7. Contribution Guidelines

### 7.1 Adding Support for New Statement Types

If you add a new top-level declaration type (e.g., `TypeAliasDef`):

1. **Add case to `ExtractExportedSymbol`**:
   ```csharp
   case TypeAliasDef typeAlias:
       var symbol = new TypeSymbol { ... };
       moduleInfo.ExportedSymbols[typeAlias.Name] = symbol;
       break;
   ```

2. **Add corresponding `Symbol` subclass if needed** (in `Symbol.cs`)

3. **Write tests** in `ImportResolverTests.cs`:
   ```csharp
   [Fact]
   public void TestImportTypeAlias() { ... }
   ```

### 7.2 Enhancing Module Search Logic

Want to add support for `sys.path`-style search directories?

**Steps:**
1. Add `List<string> _searchPaths` field to `ImportResolver`
2. Add `AddSearchPath(string path)` method
3. Modify `ResolveModulePath` to iterate through `_searchPaths`
4. Consider order: current dir first, then search paths, then cwd

**Design Consideration**: Should search paths be global or per-resolver instance? Currently instance-level for thread safety.

### 7.3 Implementing Conditional Imports

For features like:
```python
if platform == "windows":
    import windows_module
```

**Current Limitation**: `ImportResolver` processes all imports unconditionally at compile time.

**Enhancement Path:**
1. Add conditional flag to `ImportStatement` AST node (in `Parser`)
2. Modify `ResolveImport` to check condition at compile time
3. Consider warning for unresolved conditional imports

### 7.4 Supporting Import Aliases

Currently aliases are in the AST but not fully utilized:

```python
import module as alias  # Alias stored but not validated
```

**Enhancement**: Store alias mapping in `ModuleInfo` or symbol table for name mangling in code generation phase.

### 7.5 Adding `__all__` Export Lists

Python allows explicit export control:
```python
__all__ = ["public_func", "PublicClass"]
```

**Implementation Plan:**
1. Detect `__all__` assignment in module's top-level statements
2. Parse the list of exported names
3. Filter `ExportedSymbols` to only include names in `__all__`
4. Consider backward compatibility (default to current behavior if `__all__` is absent)

### 7.6 Improving Error Messages

Current errors are basic. Consider adding:
- **Suggestions**: "Did you mean 'my_modul'?" (Levenshtein distance)
- **Context**: Show the import statement that failed
- **Fix hints**: "Module 'X' found but has no `__init__.spy`"

**Implementation**: Enhance `AddError` to build richer error objects.

### 7.7 Performance Optimization

For large projects with many imports:

**Profiling Targets:**
1. **File I/O**: Consider async/await for parallel module loading
2. **Parsing**: Cache parse trees across compilation runs (persistent cache)
3. **Symbol extraction**: Use lazy evaluation - only extract symbols when accessed

**Benchmark First**: Use `BenchmarkDotNet` to measure before optimizing.

### 7.8 Testing Best Practices

When adding tests for `ImportResolver`:

**Test Categories:**
- **Unit tests**: Mock file system, test logic in isolation
- **Integration tests**: Use real `.spy` files in `test_modules/` directory
- **Error cases**: Circular imports, missing modules, missing symbols
- **.NET interop**: Test with actual .NET assemblies

**Example Test Structure:**
```csharp
[Fact]
public void TestResolveImport_CircularDependency()
{
    // Setup: Create module A that imports B, and B that imports A
    // Act: Call ResolveImport
    // Assert: Check for circular import error
}
```

---

## Summary

`ImportResolver` is a critical component that bridges the gap between Sharpy's module system and both Sharpy source files and .NET assemblies. Key takeaways:

- **Dual-source**: Handles both `.spy` files and .NET assemblies seamlessly
- **Caching**: Avoids redundant parsing and loading
- **Safety**: Detects circular imports and validates symbol existence
- **Pythonic**: Follows Python's implicit export model
- **Error-tolerant**: Collects errors without stopping on first failure

When working with imports in Sharpy, `ImportResolver` is your first debugging stop. Check its error list, enable debug logging, and trace through the module path resolution logic to diagnose issues.

For contributions, focus on enhancing error messages, adding new export patterns, or optimizing performance for large projects. Always maintain backward compatibility with existing `.spy` code.
