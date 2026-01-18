# Walkthrough: PackageResolver.cs

**Source File**: `src/Sharpy.Compiler/Semantic/PackageResolver.cs`

---

## Overview

`PackageResolver` is responsible for resolving **Python-style packages** in Sharpy, which are directories containing an `__init__.spy` file. Its primary job is to:

1. Parse the `__init__.spy` file to extract directly defined symbols (functions, classes, etc.)
2. Process import statements to identify **re-exported symbols** from submodules
3. Build a complete `PackageInfo` object containing all symbols visible when importing the package

This is a critical component of Sharpy's module system, enabling package-level imports like `from utils import helper_function`, where `helper_function` might be defined in `utils/helpers.spy` but re-exported through `utils/__init__.spy`.

**Pipeline Position**: Semantic Analysis phase, works alongside `ImportResolver` and `ModuleResolver` to build a complete symbol table for the project.

---

## Architecture Context

### The Package Concept

In Python (and Sharpy), a **package** is a directory containing an `__init__.py` (or `__init__.spy`) file:

```
my_project/
├── utils/                  # This is a package
│   ├── __init__.spy       # Makes 'utils' a package
│   ├── helpers.spy        # Submodule
│   └── math.spy           # Submodule
```

The `__init__.spy` file serves two purposes:
1. **Defines symbols directly** (functions, classes, constants)
2. **Re-exports symbols** from submodules (via `from .helpers import some_function`)

### How It Fits in the Pipeline

```
Source Code (.spy files)
    ↓
Lexer → Tokens
    ↓
Parser → AST
    ↓
Semantic Analysis
    ├── ModuleResolver     # Resolves individual .spy files
    ├── ImportResolver     # Resolves import statements
    └── PackageResolver    # Resolves packages (__init__.spy) ← YOU ARE HERE
    ↓
CodeGen (RoslynEmitter)
```

**Upstream**: Receives paths to `__init__.spy` files from `ImportResolver` or project discovery
**Downstream**: Provides `PackageInfo` with symbols to `ImportResolver` for import resolution

---

## Class Structure

### Main Class: `PackageResolver`

```csharp
public class PackageResolver
{
    private readonly ICompilerLogger _logger;
    private readonly ImportResolver _importResolver;
    private readonly Dictionary<string, PackageInfo> _packageCache;
}
```

**Key Fields**:
- `_logger`: For debug/error logging during package resolution
- `_importResolver`: Used to resolve `from X import Y` statements within `__init__.spy`
- `_packageCache`: Caches resolved packages to avoid re-parsing the same `__init__.spy` multiple times

**Design Pattern**: Uses **caching** heavily to avoid redundant file I/O and parsing.

---

### Data Class: `PackageInfo`

```csharp
public class PackageInfo
{
    public string Name { get; init; }                          // "utils.math"
    public string InitPath { get; init; }                      // "/path/to/utils/math/__init__.spy"
    public Module Module { get; init; }                        // Parsed AST
    public Dictionary<string, Symbol> ExportedSymbols { get; init; }  // All visible symbols
}
```

This is the **output** of package resolution. `ExportedSymbols` contains:
- Directly defined symbols (functions, classes in `__init__.spy`)
- Re-exported symbols from `from X import Y` statements

---

## Key Methods

### 1. `ResolvePackage(string packageName, string initPath)` (Lines 32-114)

**Purpose**: The main entry point. Resolves a package and returns all its exported symbols.

**Algorithm**:
```
1. Check cache - return cached PackageInfo if available
2. Parse __init__.spy file (Lexer → Parser → AST)
3. Extract directly defined symbols (functions, classes, etc.)
4. Process import statements to find re-exported symbols
5. Cache and return PackageInfo
```

**Key Implementation Details**:

```csharp
// Step 1: Parse the __init__.spy file
var source = File.ReadAllText(initPath);
var lexer = new Lexer.Lexer(source, _logger);
var tokens = lexer.TokenizeAll();
var parser = new Parser.Parser(tokens, _logger);
var module = parser.ParseModule();
```

This creates a mini-compilation pipeline just for the `__init__.spy` file. Note that this is **synchronous** file I/O - potential area for optimization in large projects.

```csharp
// Step 2: Extract top-level symbols
foreach (var statement in module.Body)
{
    ExtractSymbolFromStatement(statement, moduleInfo);
}
```

This picks up functions, classes, structs, interfaces, enums, and constants defined directly in `__init__.spy`.

```csharp
// Step 3: Process imports for re-exports
var packageDir = Path.GetDirectoryName(initPath);
var searchPath = packageDir != null ? Path.GetDirectoryName(packageDir) : null;

foreach (var statement in moduleInfo.Module.Body)
{
    switch (statement)
    {
        case FromImportStatement fromImport:
            ProcessFromImport(fromImport, packageInfo, searchPath);
            break;
        case ImportStatement import:
            ProcessImport(import, packageInfo);
            break;
    }
}
```

**Critical Path Detail**: The `searchPath` is set to the **parent of the package directory**. This allows sibling packages to be found. For example:

```
project/
├── utils/
│   ├── __init__.spy
│   └── helpers.spy
└── models/
    └── __init__.spy
```

When resolving `utils/__init__.spy`, `searchPath` points to `project/`, allowing `from models import X` to work.

---

### 2. `ProcessFromImport(FromImportStatement, PackageInfo, string?)` (Lines 120-156)

**Purpose**: Handles `from X import Y` statements to re-export symbols from submodules.

**Two Modes**:

#### Mode 1: Import All (`from X import *`)
```csharp
if (fromImport.ImportAll)
{
    var publicSymbols = _importResolver.GetImportAllSymbols(importedModule);
    foreach (var (name, symbol) in publicSymbols)
    {
        if (!packageInfo.ExportedSymbols.ContainsKey(name))
        {
            packageInfo.ExportedSymbols[name] = symbol;
        }
    }
}
```

**Behavior**: Copies all **public** symbols from the imported module. Uses `GetImportAllSymbols` which filters out private/protected symbols.

**Collision Handling**: If a symbol with the same name is already defined directly in `__init__.spy`, it's **not overwritten**. Direct definitions take precedence.

#### Mode 2: Specific Imports (`from X import Y, Z as W`)
```csharp
foreach (var importAlias in fromImport.Names)
{
    var sourceName = importAlias.Name;
    var exportName = importAlias.AsName ?? sourceName;

    if (importedModule.ExportedSymbols.TryGetValue(sourceName, out var symbol))
    {
        packageInfo.ExportedSymbols[exportName] = symbol;
    }
}
```

**Alias Support**: Handles `from helpers import function as func`, where `function` is imported and re-exported as `func`.

**Example**:
```python
# utils/__init__.spy
from .helpers import do_something, do_another as helper

# Result: packageInfo.ExportedSymbols contains:
# - "do_something" → FunctionSymbol
# - "helper" → FunctionSymbol (aliased from do_another)
```

---

### 3. `ProcessImport(ImportStatement, PackageInfo)` (Lines 162-167)

**Purpose**: Handles regular `import X` statements.

**Current Behavior**: Does nothing!

```csharp
// Regular imports (import X) don't automatically re-export
// They're used within __init__.spy but not exposed at package level
```

**Why?**: In Python, `import os` inside `__init__.py` makes `os` available for use within that file, but doesn't make `os` visible to importers of the package. This matches that behavior.

**Future Feature**: The comment mentions `__all__` support. In Python, you can define `__all__ = ["foo", "bar"]` to explicitly control what `from package import *` exports. This is not yet implemented.

---

### 4. `ExtractSymbolFromStatement(Statement, ModuleInfo)` (Lines 173-260)

**Purpose**: Extract a symbol from a top-level statement in `__init__.spy`.

**Supported Statement Types**:
1. `FunctionDef` → `FunctionSymbol`
2. `ClassDef` → `TypeSymbol` (TypeKind.Class)
3. `StructDef` → `TypeSymbol` (TypeKind.Struct)
4. `InterfaceDef` → `TypeSymbol` (TypeKind.Interface)
5. `EnumDef` → `TypeSymbol` (TypeKind.Enum)
6. `VariableDeclaration` (constants only) → `VariableSymbol`

**Pattern**: Each case follows the same structure:
1. Determine access level from naming convention
2. Create appropriate symbol type
3. Populate metadata (name, line/column for diagnostics)
4. Add to `moduleInfo.ExportedSymbols`

**Access Level Example** (Function):
```csharp
case FunctionDef functionDef:
    var funcAccessLevel = GetAccessLevel(functionDef.Name);
    var funcSymbol = new FunctionSymbol
    {
        Name = functionDef.Name,
        Kind = SymbolKind.Function,
        AccessLevel = funcAccessLevel,
        DeclarationLine = functionDef.LineStart,
        DeclarationColumn = functionDef.ColumnStart
    };
    moduleInfo.ExportedSymbols[functionDef.Name] = funcSymbol;
    break;
```

**Note on Variables**: Only **constants** (`const foo = 42`) are exported. Regular variables are not considered package-level exports. This prevents mutable state from leaking out of the package initialization.

---

### 5. `GetAccessLevel(string name)` (Lines 265-272)

**Purpose**: Determines visibility based on Python naming conventions.

```csharp
private AccessLevel GetAccessLevel(string name)
{
    if (name.StartsWith("__"))
        return AccessLevel.Private;      // __private_function
    if (name.StartsWith("_"))
        return AccessLevel.Protected;    // _internal_helper
    return AccessLevel.Public;           // public_api
}
```

**Python Convention Mapping**:
- `__name`: Private (name mangling in Python, private in Sharpy)
- `_name`: Protected/internal (convention: "internal use")
- `name`: Public (part of the public API)

This affects which symbols are included in `from package import *` (only public symbols).

---

### 6. `ClearCache()` (Lines 277-280)

**Purpose**: Clears the package cache.

**Use Cases**:
- Unit tests that need a fresh state
- Hot-reload scenarios (future feature)
- After file system changes invalidate the cache

---

## Dependencies and Cross-References

### Internal Dependencies

1. **ImportResolver** ([ImportResolver.md](./ImportResolver.md))
   - Used to resolve `from X import Y` statements within `__init__.spy`
   - Provides `ResolveFromImport()` and `GetImportAllSymbols()` methods
   - **Key Relationship**: PackageResolver is a specialized consumer of ImportResolver

2. **Symbol Classes** ([Symbol.md](./Symbol.md))
   - `FunctionSymbol`: Represents functions
   - `TypeSymbol`: Represents classes, structs, interfaces, enums
   - `VariableSymbol`: Represents constants
   - All inherit from base `Symbol` class with `Name`, `Kind`, `AccessLevel`

3. **ModuleResolver** ([ModuleResolver.md](./ModuleResolver.md))
   - Sibling component that resolves individual `.spy` files (not packages)
   - PackageResolver focuses on directories with `__init__.spy`

4. **AST Types** (Parser namespace)
   - `Module`, `FromImportStatement`, `ImportStatement`
   - `FunctionDef`, `ClassDef`, `StructDef`, `InterfaceDef`, `EnumDef`
   - `VariableDeclaration`

### External Dependencies

- **System.IO**: File system access (`File.ReadAllText`, path manipulation)
- **Lexer & Parser**: Creates mini-compilation pipeline to parse `__init__.spy`
- **ICompilerLogger**: Logging interface from `Sharpy.Compiler.Logging`

---

## Design Patterns and Decisions

### 1. Caching Strategy

```csharp
if (_packageCache.TryGetValue(packageName, out var cached))
    return cached;
```

**Rationale**: Parsing files is expensive. If multiple files import the same package, we only parse `__init__.spy` once.

**Cache Key**: Package name (dotted notation like "utils.math")

**Invalidation**: Manual via `ClearCache()`. No automatic file watching (yet).

### 2. Dual-Source Symbol Collection

Symbols come from two sources:
1. **Direct definitions**: `def foo():` in `__init__.spy`
2. **Re-exports**: `from .helpers import foo` in `__init__.spy`

This mirrors Python's package initialization semantics exactly.

### 3. Precedence: Direct > Re-exported

```csharp
if (!packageInfo.ExportedSymbols.ContainsKey(name))
{
    packageInfo.ExportedSymbols[name] = symbol;
}
```

If `__init__.spy` defines `foo()` and also does `from .helpers import foo`, the local definition wins. This prevents accidental shadowing.

### 4. Synchronous File I/O

```csharp
var source = File.ReadAllText(initPath);
```

**Trade-off**: Simpler code, but could block on slow I/O. For large projects with many packages, this could be a bottleneck. Consider async refactoring if performance becomes an issue.

### 5. Error Handling Philosophy

```csharp
catch (Exception ex)
{
    _logger.LogError($"Error parsing package {packageName}: {ex.Message}", 0, 0);
    return null;
}
```

**Approach**: Fail gracefully. If a package can't be resolved, log the error and return `null`. The caller must handle missing packages.

**Implication**: Compilation can continue even if some packages fail to resolve. This allows partial builds but may lead to cascading errors.

---

## Debugging Tips

### 1. Enable Debug Logging

Set a logger to see detailed resolution steps:
```csharp
var logger = new ConsoleLogger(LogLevel.Debug);
var resolver = new PackageResolver(logger);
```

You'll see output like:
```
Resolving package: utils from /path/to/utils/__init__.spy
  Re-exporting helper from utils.helpers
```

### 2. Common Issues: Cache Staleness

**Problem**: You modify `__init__.spy` but the resolver returns old symbols.

**Solution**: The cache doesn't auto-invalidate. Call `ClearCache()` after file changes.

**Future Improvement**: Track file modification times and auto-invalidate.

### 3. Common Issues: Import Resolution Failures

**Problem**: `from .helpers import foo` doesn't re-export `foo`.

**Debugging**:
1. Check if `_importResolver.ResolveFromImport()` returns `null` (check logs)
2. Verify `searchPath` is correct (should be parent of package directory)
3. Ensure `helpers.spy` exists and exports `foo`

**Tip**: Add a breakpoint in `ProcessFromImport` at line 122 to inspect `importedModule`.

### 4. Common Issues: Wrong Access Level

**Problem**: Private symbols are being exported.

**Debugging**:
1. Check the symbol name - does it start with `_` or `__`?
2. `GetAccessLevel()` determines visibility (line 265)
3. `from X import *` should filter out non-public symbols via `GetImportAllSymbols()`

### 5. Inspect PackageInfo Contents

After resolution, inspect the returned `PackageInfo`:
```csharp
var packageInfo = resolver.ResolvePackage("utils", "/path/to/utils/__init__.spy");
foreach (var (name, symbol) in packageInfo.ExportedSymbols)
{
    Console.WriteLine($"{name}: {symbol.Kind} ({symbol.AccessLevel})");
}
```

This shows exactly what symbols are visible from the package.

### 6. Distinguish Direct vs. Re-exported Symbols

Add temporary logging in `ExtractSymbolFromStatement` (line 173) and `ProcessFromImport` (line 120) to see which symbols come from where.

---

## Contribution Guidelines

### What Changes Might Be Made Here?

1. **Add `__all__` Support**
   - Parse `__all__ = ["foo", "bar"]` from `__init__.spy`
   - Use it to filter `from package import *` exports
   - Update `ProcessImport()` to check `__all__`

2. **Async File I/O**
   - Change `File.ReadAllText` to `File.ReadAllTextAsync`
   - Make `ResolvePackage` async
   - Improve performance for large projects

3. **Cache Invalidation**
   - Track file modification times
   - Auto-invalidate cache when `__init__.spy` changes
   - Consider using `FileSystemWatcher` for hot-reload

4. **Circular Dependency Detection**
   - Track resolution stack
   - Detect if Package A → Package B → Package A
   - Emit clear error instead of stack overflow

5. **Enhanced Symbol Metadata**
   - Store docstrings from `__init__.spy`
   - Track which symbols are re-exported vs. direct
   - Add source location for better error messages

6. **Support for Relative Imports**
   - Currently relies on `ImportResolver` for relative imports
   - May need special handling for `from .. import X` (parent package imports)

7. **Performance Optimizations**
   - Lazy parsing (only parse `__init__.spy` when needed)
   - Parallel package resolution
   - Incremental updates instead of full re-parsing

### Code Style Conventions

- Use **init-only properties** (`{ get; init; }`) for data classes like `PackageInfo`
- Prefer **null-conditional operators** (`?.`) for path manipulation
- Use **pattern matching** in switch statements for AST traversal
- Follow **Python semantics** closely - this helps developers familiar with Python understand Sharpy packages

### Testing Considerations

When modifying this code, ensure tests cover:
- Empty `__init__.spy` files (valid but export nothing)
- `from .sub import *` (wildcard re-exports)
- `from .sub import foo as bar` (aliased re-exports)
- Packages with deeply nested structures (`a.b.c.d`)
- Name collisions between direct definitions and re-exports
- Private symbols (should not appear in re-exports)
- Missing/invalid `__init__.spy` files (error handling)

---

## Example Usage

### Scenario: Resolving a Utility Package

```
project/
└── utils/
    ├── __init__.spy       # Package initialization
    ├── helpers.spy        # Helper functions
    └── math.spy           # Math utilities
```

**utils/__init__.spy**:
```python
from .helpers import clean_string, format_text
from .math import *

def local_util():
    pass
```

**Resolution Process**:

```csharp
var resolver = new PackageResolver(logger);
var packageInfo = resolver.ResolvePackage("utils", "/project/utils/__init__.spy");

// packageInfo.ExportedSymbols now contains:
// - "clean_string" (from helpers.spy)
// - "format_text" (from helpers.spy)
// - All public symbols from math.spy (via import *)
// - "local_util" (defined directly)
```

**Result**: Anyone importing `utils` sees all these symbols as if they were defined in `utils` itself.

---

## Summary

`PackageResolver` is the **package-level symbol aggregator** in Sharpy's semantic analysis. It:

✅ Parses `__init__.spy` files to extract package structure
✅ Combines direct definitions with re-exported symbols
✅ Caches results for performance
✅ Respects Python's naming conventions for visibility
✅ Integrates tightly with `ImportResolver` for cross-module symbol resolution

**Key Insight**: This class enables **package-level abstraction**. Users can organize code into submodules (`helpers.spy`, `math.spy`) while presenting a clean package-level API through `__init__.spy` re-exports.

**Next Steps**: To understand the full module resolution story, read:
1. [ImportResolver.md](./ImportResolver.md) - How individual imports are resolved
2. [ModuleResolver.md](./ModuleResolver.md) - How individual `.spy` files are resolved
3. [Symbol.md](./Symbol.md) - The symbol types used throughout
