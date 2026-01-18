# Walkthrough: ModuleResolver.cs

**Source File**: `src/Sharpy.Compiler/Semantic/ModuleResolver.cs`

---

## Overview

The `ModuleResolver` is a critical component in the **Semantic Analysis** phase of the Sharpy compiler pipeline. Its primary responsibility is to **resolve module import statements to actual source files** on disk.

When the Parser encounters an `import` or `from ... import` statement, it creates AST nodes representing those imports. Later, during semantic analysis, the `ModuleResolver` takes module names (like `"utils.helpers"` or `".sibling"`) and locates the corresponding `.spy` files in the filesystem.

**Key Responsibilities:**
- Convert dotted module names to file paths (e.g., `utils.helpers` → `utils/helpers.spy`)
- Support relative imports (e.g., `.sibling`, `..parent`)
- Search multiple locations (current directory, project paths, stdlib paths)
- Handle Python-style package directories with `__init__.spy` files
- Compute canonical module names for proper deduplication

**Pipeline Position:**
```
Parser (AST) → [ModuleResolver] → Semantic Analysis → RoslynEmitter
```

The resolver is typically invoked during semantic analysis when the compiler needs to:
1. Load imported modules for type checking
2. Build a dependency graph
3. Ensure all imports can be resolved before code generation

---

## Class/Type Structure

### 1. `ModuleResolver` (Main Class)

The main workhorse class with these key fields:

```csharp
private readonly ICompilerLogger _logger;        // For debug logging
private readonly List<string> _searchPaths;      // Project search paths
private string? _currentModulePath;              // Context for relative imports
```

**State Management:**
- `_searchPaths`: A list of directories to search for modules (e.g., project root, stdlib path)
- `_currentModulePath`: The absolute path to the currently-being-compiled module. This is crucial for resolving relative imports like `from .sibling import foo`

### 2. `ModuleResolutionResult` (Return Type)

A data class representing a successful resolution:

```csharp
public class ModuleResolutionResult
{
    string FullPath              // Absolute path: /path/to/project/utils/helpers.spy
    string ModuleName            // Original import: "utils.helpers" or ".helpers"
    string? CanonicalModuleName  // Canonical form: "mypackage.utils.helpers"
    ModuleResolutionKind Kind    // How it was found
    string? SearchPath           // Which search path matched
}
```

**Why CanonicalModuleName?**
When you import `.helpers` from `mypackage/__init__.spy`, the canonical name is `mypackage.helpers`. This ensures the compiler doesn't load the same module twice under different names.

### 3. `ModuleResolutionKind` (Enum)

Indicates **how** a module was resolved:

```csharp
public enum ModuleResolutionKind
{
    RelativeToCurrentModule,      // Found via relative import
    ProjectSearchPath,            // Found in configured search path
    StandardLibrary,              // (Future) Found in stdlib
    ExternalPackage,              // (Future) Found in package manager
    CurrentWorkingDirectory       // Fallback: found in CWD
}
```

This metadata is useful for debugging import issues and understanding module provenance.

---

## Key Functions/Methods

### 1. `Resolve(string moduleName)` - Main Entry Point

**Location**: Lines 52-127

**What it does:**
The primary public API for resolving a module name to a file path.

```csharp
public ModuleResolutionResult? Resolve(string moduleName)
```

**Algorithm:**
1. **Validate input** - return null if empty
2. **Check if relative import** (starts with `.`) → delegate to `ResolveRelativeImport`
3. **Convert dotted name to path**: `"utils.helpers"` → `"utils/helpers.spy"`
4. **Search strategy** (in order):
   - Try relative to current module's directory (if `_currentModulePath` is set)
   - Try each configured search path in `_searchPaths`
   - Try current working directory (fallback)
5. **Return result or null** (with debug logging of all searched paths)

**Example:**
```csharp
// Resolving "utils.helpers"
var resolver = new ModuleResolver(logger);
resolver.AddSearchPath("/myproject/src");
resolver.SetCurrentModulePath("/myproject/src/main.spy");

var result = resolver.Resolve("utils.helpers");
// Tries:
// 1. /myproject/src/utils/helpers.spy
// 2. /myproject/src/utils/__init__.spy (if helpers is a package)
// 3. (search paths...)
```

**Key Implementation Details:**
- Uses `TryResolveInDirectory` helper for each search location
- Tracks all searched paths for error reporting
- Returns null (not exception) if module not found

---

### 2. `ResolveRelativeImport(string moduleName, List<string> searchedPaths)` - Relative Import Handler

**Location**: Lines 135-240

**What it does:**
Handles Python-style relative imports like `.sibling`, `..parent`, `...grandparent`.

**Algorithm:**
1. **Validate current module context** - relative imports require knowing where "we" are
2. **Count leading dots** to determine how many levels to go up:
   - `.foo` → same directory
   - `..foo` → parent directory
   - `...foo` → grandparent directory
3. **Navigate up directory tree** by dot count
4. **Handle special case**: Just dots (e.g., `..`) imports the parent package's `__init__.spy`
5. **Try resolution**:
   - As a file: `baseDir/remaining/path.spy`
   - As a package: `baseDir/remaining/path/__init__.spy`
6. **Compute canonical name** to prevent duplicate imports

**Example:**
```
File structure:
  mypackage/
    __init__.spy
    subpkg/
      __init__.spy
      module_a.spy
      module_b.spy

From module_a.spy:
  from .module_b import X       # → mypackage/subpkg/module_b.spy
  from ..othermod import Y      # → mypackage/othermod.spy
  from . import module_b        # → mypackage/subpkg/module_b.spy
```

**Important Edge Cases:**
- `from .. import foo` → imports from parent package's `__init__.spy`
- `from . import foo` → imports from current package's `__init__.spy` or `foo.spy` sibling
- Going up too many levels (e.g., `....foo` from shallow directory) returns null

---

### 3. `ComputeCanonicalModuleName(string fullPath)` - Canonical Name Calculator

**Location**: Lines 246-294

**What it does:**
Converts an absolute file path back to a canonical dotted module name by walking up the package hierarchy.

**Algorithm:**
1. **Normalize path** to absolute form
2. **Walk up directory tree** from the file's directory
3. **For each directory**, check if it contains `__init__.spy` (= is a package)
4. **Collect package names** until we hit a non-package directory (the source root)
5. **Build dotted name** from collected parts
6. **Special case**: `__init__.spy` files don't add their own name

**Example:**
```
File: /project/src/mylib/utils/helpers.spy

Directory structure:
  /project/src/mylib/__init__.spy     ← package
  /project/src/mylib/utils/__init__.spy ← package
  /project/src/mylib/utils/helpers.spy

Walk up:
  - Start at helpers.spy
  - utils/ has __init__.spy → add "utils"
  - mylib/ has __init__.spy → add "mylib"
  - src/ has NO __init__.spy → stop (source root)

Canonical name: "mylib.utils.helpers"
```

**Why this matters:**
Without canonical names, the compiler might load the same module twice:
- Once as `"utils.helpers"` (absolute import)
- Once as `".helpers"` (relative import from utils/__init__.spy)

The canonical name ensures both resolve to `"mylib.utils.helpers"`.

---

### 4. `TryResolveInDirectory(...)` - Directory Search Helper

**Location**: Lines 299-342

**What it does:**
A helper method that tries to resolve a module within a specific base directory.

**Parameters:**
- `baseDir`: The directory to search in
- `relativePath`: The file path to try (e.g., `"utils/helpers.spy"`)
- `packagePath`: The package path to try (e.g., `"utils/helpers"`)
- `moduleName`: Original module name (for logging)
- `kind`: Resolution kind to record
- `searchedPaths`: List to track what was tried
- `searchPath`: The search path being used (for result metadata)

**Algorithm:**
1. **Try direct file**: `baseDir/relativePath` (e.g., `src/utils/helpers.spy`)
2. **Try package directory**: `baseDir/packagePath/__init__.spy` (e.g., `src/utils/helpers/__init__.spy`)
3. **Return result or null**

**Why two attempts?**
Python-style module systems allow both:
- **File modules**: `helpers.spy` (single file)
- **Package modules**: `helpers/__init__.spy` (directory with initialization)

---

### 5. `SetCurrentModulePath(string modulePath)` - Context Setter

**Location**: Lines 31-34

**What it does:**
Sets the current module path to provide context for relative imports.

**Usage Pattern:**
```csharp
// When compiling module_a.spy:
resolver.SetCurrentModulePath("/project/src/mylib/module_a.spy");

// Now relative imports in module_a work correctly
var result = resolver.Resolve(".sibling");
// → Looks for /project/src/mylib/sibling.spy
```

**Important:**
This must be called **before** processing imports for each module. The Semantic Analyzer typically calls this when starting analysis of each source file.

---

### 6. `AddSearchPath(string path)` - Search Path Configuration

**Location**: Lines 39-45

**What it does:**
Adds a directory to the list of search paths for absolute imports.

**Usage Pattern:**
```csharp
var resolver = new ModuleResolver(logger);

// Add project source directories
resolver.AddSearchPath("/myproject/src");
resolver.AddSearchPath("/myproject/lib");

// Add standard library
resolver.AddSearchPath("/usr/local/sharpy/stdlib");
```

**Order matters:**
Search paths are tried in the order they're added. Earlier paths take precedence.

---

## Dependencies

### Internal Dependencies

**Sharpy.Compiler.Logging**
- `ICompilerLogger`: Interface for logging debug messages
- `NullLogger`: No-op logger for when logging is disabled

The resolver logs extensively at debug level:
- Resolution attempts
- Successful resolutions with file paths
- Failed resolutions with all searched paths

This is invaluable for debugging import issues.

### External Dependencies

**.NET Framework Classes:**
- `System.IO.Path`: Path manipulation (combining, normalization, separators)
- `System.IO.File`: File existence checks
- `System.IO.Directory`: Directory operations

**Platform Considerations:**
- Uses `Path.DirectorySeparatorChar` for cross-platform compatibility
- Uses `Path.GetFullPath()` to normalize paths (handles `..`, `.`, etc.)

---

## Patterns and Design Decisions

### 1. **Python-Inspired Module System**

The resolver closely mirrors Python's import system:
- Dotted module names (`utils.helpers`)
- Relative imports (`.sibling`, `..parent`)
- Package directories with `__init__.spy`
- Search path resolution order

**Why?**
Python's module system is well-understood and battle-tested. Sharpy developers can apply their Python intuition.

### 2. **Separation of Concerns**

The resolver **only** finds files. It doesn't:
- Parse the files
- Load ASTs
- Resolve symbols within modules
- Handle circular dependencies

These are responsibilities of other semantic analysis components.

### 3. **Null Return on Failure (Not Exceptions)**

When a module isn't found, `Resolve()` returns `null` rather than throwing an exception.

**Rationale:**
- The caller (semantic analyzer) can provide better error messages with context
- Allows batching of import errors
- Non-exceptional control flow

### 4. **Stateful vs Stateless Design**

The resolver is **stateful** (`_currentModulePath`, `_searchPaths`), but operations are deterministic:
- Same inputs → same outputs (for a given state)
- State is typically set once per module compilation

**Alternative design:**
A stateless design would pass search paths and current module path as parameters to every `Resolve()` call. The current design reduces parameter passing at the cost of requiring careful state management.

### 5. **Canonical Name Computation**

The `CanonicalModuleName` field enables **module deduplication**.

**Problem without it:**
```csharp
// In pkg/__init__.spy
from .submodule import X  // → resolves to pkg/submodule.spy

// In main.spy
from pkg.submodule import Y  // → also resolves to pkg/submodule.spy
```

Without canonical names, the compiler might analyze `pkg/submodule.spy` twice. With canonical names, both imports map to `"pkg.submodule"`.

---

## Debugging Tips

### 1. **Enable Debug Logging**

The resolver logs extensively. When debugging import issues:

```csharp
var logger = new ConsoleCompilerLogger(LogLevel.Debug);
var resolver = new ModuleResolver(logger);
// Now you'll see all resolution attempts
```

Look for:
- `"Resolved module 'X' to Y (kind)"` → success
- `"Module 'X' not found. Searched: ..."` → failure with details

### 2. **Common Import Failures**

**"Module not found"**
- Check `_currentModulePath` is set correctly
- Verify search paths are added in the right order
- Check for typos in module names
- Verify `.spy` files exist (not just directories)

**"Relative import without context"**
- `SetCurrentModulePath()` wasn't called
- Current module path is null

**"Cannot go up N levels"**
- Too many dots in relative import (e.g., `....foo` from shallow directory)

### 3. **Testing Strategies**

When adding tests for the resolver:

```csharp
// Create temp directory structure
var tempDir = CreateTempPackage(
    "pkg/__init__.spy",
    "pkg/mod_a.spy",
    "pkg/subpkg/__init__.spy",
    "pkg/subpkg/mod_b.spy"
);

var resolver = new ModuleResolver(logger);
resolver.AddSearchPath(tempDir);
resolver.SetCurrentModulePath($"{tempDir}/pkg/mod_a.spy");

// Test absolute import
var result = resolver.Resolve("pkg.subpkg.mod_b");
Assert.NotNull(result);

// Test relative import
var relResult = resolver.Resolve(".subpkg.mod_b");
Assert.NotNull(relResult);
Assert.Equal(result.FullPath, relResult.FullPath);
Assert.Equal("pkg.subpkg.mod_b", relResult.CanonicalModuleName);
```

### 4. **Path Normalization Issues**

Be careful with:
- Mixed path separators (`/` vs `\` on Windows)
- Trailing slashes in search paths
- Relative vs absolute paths

The resolver uses `Path.GetFullPath()` to normalize, but input validation helps.

### 5. **Breakpoint Suggestions**

Key places to set breakpoints:
- **Line 52** (`Resolve` entry) - see what's being resolved
- **Line 65** (relative import check) - trace relative import handling
- **Line 312** (`File.Exists` check) - see what file paths are tried
- **Line 246** (`ComputeCanonicalModuleName`) - understand canonical name calculation

---

## Contribution Guidelines

### Types of Changes

**1. Adding New Resolution Strategies**

If adding support for standard library or package managers:

1. Add new `ModuleResolutionKind` enum value
2. Add resolution logic in `Resolve()` method (in priority order)
3. Update tests to cover new resolution paths
4. Update documentation

**2. Improving Error Messages**

The resolver tracks all searched paths. Consider:
- Adding suggestions for common typos
- Detecting case-sensitivity issues
- Suggesting package installation for missing external packages

**3. Performance Optimizations**

Current design does file existence checks for each search path. Potential optimizations:
- Cache resolution results (beware of file system changes!)
- Index available modules at startup
- Parallelize file existence checks (probably not worth it)

**4. Enhanced Logging**

Consider adding:
- Performance metrics (time spent resolving)
- Statistics (cache hit rates, most-used modules)
- Warnings for ambiguous imports

### Testing Requirements

When modifying the resolver:

1. **Unit tests** for each resolution strategy
2. **Integration tests** with real file structures
3. **Edge case tests**:
   - Empty module names
   - Very long relative imports (`........`)
   - Non-existent paths
   - Circular package structures
4. **Cross-platform tests** (Windows vs Unix path separators)

### Code Style Conventions

Follow existing patterns:
- Use nullable reference types (`string?` for optional fields)
- Log at debug level generously
- Return null for failures, not exceptions
- Use `Path.Combine()` for path building (never string concatenation)
- Initialize collections in field declarations (`= new()`)

---

## Cross-References

### Related Semantic Analysis Components

- **SymbolTable**: Uses resolved modules to build symbol tables
- **TypeChecker**: Needs resolved modules to check imported types
- **Import Statement Handling**: The semantic analyzer's import processing logic that calls `ModuleResolver`

### Related Documentation

- `docs/language_specification/module_resolution.md`: Formal specification for module resolution
- `docs/language_specification/module_system.md`: Overall module and package system design

### Related Source Files

While `ModuleResolver` is not a partial class, it works closely with:

- **`Semantic/SymbolTable.cs`**: Stores resolved module information
- **`Semantic/SemanticAnalyzer.cs`**: The component that invokes the resolver during analysis
- **`Parser/Ast/Statement.cs`**: Defines `ImportStatement` and `ImportFromStatement` AST nodes
- **`Compiler.cs`**: Sets up search paths and initializes the resolver

### Future Extensions

The `ModuleResolutionKind` enum includes placeholder values for future features:
- `StandardLibrary`: For built-in Sharpy standard library modules
- `ExternalPackage`: For third-party packages from a package manager

When implementing these, you'll need to:
1. Implement resolution logic in `Resolve()`
2. Add configuration for stdlib/package paths
3. Consider version resolution for packages
4. Handle package metadata files (`pyproject.toml` equivalent)

---

## Summary

The `ModuleResolver` is a focused, well-designed component that:
- Converts import statements into file paths
- Supports Python-style relative imports and package directories
- Provides detailed logging for debugging
- Computes canonical names to prevent duplicate module loading
- Returns rich metadata about how each module was resolved

When working with imports in Sharpy, this is your starting point. Understanding how module names map to files is crucial for debugging import errors and extending the module system.

**Key Takeaway**: The resolver is **just** a file finder. It doesn't parse, analyze, or validate. It takes a string (module name) and returns a path (or null). Simple, focused, and reliable.
