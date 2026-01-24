# Walkthrough: ImportResolver.cs

**Source File**: `src/Sharpy.Compiler/Semantic/ImportResolver.cs`

---

## Overview

The `ImportResolver` class is the cornerstone of Sharpy's module system, responsible for resolving import statements and loading symbols from both `.spy` files and .NET assemblies. It sits in the **Semantic Analysis** phase of the compiler pipeline, operating after the Parser produces an AST and before code generation via RoslynEmitter.

**Key Responsibilities:**
- Resolve `import` and `from ... import` statements to actual modules
- Load and parse `.spy` files transitively
- Integrate .NET namespaces and assemblies through the ModuleRegistry
- Extract exported symbols (functions, classes, structs, interfaces, enums, variables)
- Handle re-exports (`from .submodule import func`)
- Detect circular import dependencies with detailed error reporting
- Track file dependencies for incremental compilation
- Enforce Python-like visibility rules (public, `_protected`, `__private`)

**Pipeline Position:**
```
Source (.spy) → Lexer → Parser (AST) → [ImportResolver] → SymbolTable/TypeChecker → RoslynEmitter → C#
```

The ImportResolver populates `ModuleInfo` objects containing exported symbols, which downstream components use for name resolution and type checking.

---

## Class/Type Structure

### Main Class: `ImportResolver`

The resolver maintains several pieces of state to track module loading:

```csharp
public class ImportResolver
{
    private readonly ICompilerLogger _logger;
    private readonly List<SemanticError> _errors;                 // Collects errors during resolution
    private readonly HashSet<string> _loadedModules;              // Prevents reloading
    private readonly Stack<ImportChainEntry> _importChain;        // For circular detection
    private readonly Dictionary<string, ModuleInfo> _moduleCache; // Caches loaded modules
    private readonly ModuleRegistry? _moduleRegistry;             // .NET interop
    private readonly ModuleResolver _moduleResolver;              // Path resolution
    private DependencyGraphBuilder? _graphBuilder;                // Build system integration
    private SemanticBinding? _semanticBinding;                    // Immutable AST pattern
    private string? _currentModulePath;                           // Context for relative imports
}
```

**Design Pattern: Immutable AST with SemanticBinding**

Notice the `_semanticBinding` field. Sharpy is migrating toward an immutable AST architecture:
- **Old approach**: Store semantic data directly on AST nodes (e.g., `fromImport.ResolvedModulePath`)
- **New approach**: Store semantic data in `SemanticBinding` separate from AST
- The code uses a **dual-write pattern** for backward compatibility during migration

Throughout the code, you'll see patterns like:
```csharp
if (_semanticBinding != null)
{
    _semanticBinding.SetResolvedModulePath(fromImport, resolvedPath);
}
else
{
    // Legacy fallback: store directly on AST (will be removed in future)
    fromImport.ResolvedModulePath = resolvedPath;
}
```

### Supporting Types

#### `ImportChainEntry`
```csharp
internal record ImportChainEntry(
    string ModulePath,
    int? LineStart,
    int? ColumnStart,
    string? ImportingModule
);
```

Used for detailed circular import error reporting. Each entry tracks a step in the import chain, enabling user-friendly error messages showing the exact cycle path.

#### `ModuleInfo`
```csharp
public class ModuleInfo
{
    public string Path { get; init; }                          // File path or ".net:modulename"
    public Module Module { get; init; }                        // Parsed AST (null for .NET modules!)
    public Dictionary<string, Symbol> ExportedSymbols { get; init; }
    public bool IsNetModule { get; init; }                     // True for .NET assemblies
}
```

**Critical Warning**: Always check `IsNetModule` before accessing `Module` property. .NET modules don't have an AST representation, so `Module` will be null and accessing it will cause a NullReferenceException.

---

## Key Functions/Methods

### Entry Points: ResolveImport and ResolveFromImport

These are the main public methods called by the semantic analyzer:

#### `ResolveImport(ImportStatement importStmt, string? searchPath)`

Handles `import math` or `import utils.helpers as helpers`.

**Algorithm:**
1. For each imported name in the statement:
   - Try `TryResolveNetModule()` first (checks ModuleRegistry for .NET assemblies)
   - If not found, use `ResolveModulePath()` to find a `.spy` file
   - Track file dependency via `DependencyGraphBuilder`
   - Call `LoadModule()` to parse and extract symbols
2. Return list of `ModuleInfo` objects

**Example flow:**
```python
import math, utils.helpers
```
→ Resolves "math" as .NET module, "utils.helpers" as .spy file

**Key Code (lines 78-117):**
```csharp
public List<ModuleInfo> ResolveImport(ImportStatement importStmt, string? searchPath = null)
{
    var result = new List<ModuleInfo>();

    foreach (var importAlias in importStmt.Names)
    {
        // First, try to resolve as .NET assembly module through ModuleRegistry
        var moduleInfo = TryResolveNetModule(importAlias.Name, importAlias.LineStart, importAlias.ColumnStart);

        // If not found in .NET assemblies, try .spy file
        if (moduleInfo == null)
        {
            var modulePath = ResolveModulePath(importAlias.Name, searchPath);
            if (modulePath == null)
            {
                AddError($"Cannot find module '{importAlias.Name}'", ...);
                continue;
            }

            // Track the dependency (current module depends on imported module)
            if (_graphBuilder != null && _currentModulePath != null)
            {
                _graphBuilder.AddDependency(_currentModulePath, modulePath);
            }

            moduleInfo = LoadModule(modulePath, importAlias.LineStart, importAlias.ColumnStart);
        }

        if (moduleInfo != null)
        {
            result.Add(moduleInfo);
        }
    }

    return result;
}
```

#### `ResolveFromImport(FromImportStatement fromImport, string? searchPath)`

Handles `from math import sqrt` or `from .submodule import *`.

**Algorithm:**
1. Resolve the source module (same .NET-first logic as above)
2. **Store resolved module path** (for relative imports like `.helpers` → canonical name `mypackage.helpers`)
3. **Validate imported names:**
   - For `import *`: Filter to public symbols only (no `_` prefix)
   - For explicit names: Check symbol exists and is not `__private`
4. **Build re-export dictionary** for code generation:
   - Creates `Symbol` copies with `IsReExport = true`
   - Preserves type information, parameters, etc.
   - Handles aliasing (`import sqrt as square_root`)
5. Store re-export info in SemanticBinding or AST (dual-write)

**Key Insight**: Re-exports enable patterns like:
```python
# utils/__init__.spy
from .helpers import format_string  # format_string becomes an export of utils

# main.spy
from utils import format_string     # Works because of re-export
```

**Important Code (lines 122-233):**
The method has two distinct phases:
1. **Module resolution** (lines 126-161): Resolves the source module and stores the canonical path
2. **Symbol validation and re-export** (lines 163-230): Validates imported names and populates re-export information

---

### Module Loading: LoadModule

```csharp
private ModuleInfo? LoadModule(string modulePath, int? lineStart, int? columnStart)
```

The core module loading logic. This is where `.spy` files are read, lexed, parsed, and analyzed.

**Algorithm (lines 238-330):**

1. **Check cache**: Return cached `ModuleInfo` if already loaded (lines 241-242)

2. **Circular import detection**: Check if `modulePath` is in `_importChain` stack (lines 245-250)
   - If circular, format detailed error showing the cycle path

3. **Check loaded set**: Quick check to avoid re-loading (lines 253-254)

4. **File I/O**: Read source file (lines 269-273)

5. **Lex and Parse**: Create Lexer → tokenize → Parser → AST (lines 275-281)

6. **Push to import chain** (lines 258-264) - for nested import tracking

7. **Extract symbols** (lines 291-313):
   - **Critical**: Set `_currentModulePath` (needed for relative imports in re-exports)
   - Call `ExtractExportedSymbol()` for each top-level statement
   - Recursively resolve imports via `ResolveModuleImports()`
   - **Restore** `_currentModulePath` in finally block

8. **Cache and return**: Store in `_moduleCache` and `_loadedModules` (lines 316-317)

9. **Pop from chain** in finally block (line 328)

**Critical Detail**: The `_currentModulePath` context switch (lines 292-313) enables nested relative imports:
```csharp
var previousModulePath = _currentModulePath;
_currentModulePath = modulePath;
_moduleResolver.SetCurrentModulePath(modulePath);

try
{
    // Extract symbols - if this module has "from .math import sqrt",
    // the resolver needs to know we're in this module's context
    foreach (var statement in module.Body)
    {
        ExtractExportedSymbol(statement, moduleInfo);
    }

    // Recursively resolve imports within this module
    ResolveModuleImports(module, Path.GetDirectoryName(modulePath));
}
finally
{
    _currentModulePath = previousModulePath;
    if (previousModulePath != null)
    {
        _moduleResolver.SetCurrentModulePath(previousModulePath);
    }
}
```

**Circular Import Detection:**
```
FormatCircularImportChain() produces:
Circular import detected:
  -> module_a.spy
  -> module_b.spy
  -> module_a.spy (cycle)
```

---

### Symbol Extraction: ExtractExportedSymbol

```csharp
private void ExtractExportedSymbol(Statement statement, ModuleInfo moduleInfo)
```

Traverses top-level statements and extracts symbols to `moduleInfo.ExportedSymbols` (lines 355-448).

**Supported Statement Types:**

| Statement | Symbol Created | Extraction Method | Key Details |
|-----------|----------------|-------------------|-------------|
| `FunctionDef` | `FunctionSymbol` | Inline (lines 359-385) | Converts parameters/return type annotations |
| `ClassDef` | `TypeSymbol` (TypeKind.Class) | `ExtractFullClassSymbol()` (line 389) | Full class info including fields, methods, constructors |
| `StructDef` | `TypeSymbol` (TypeKind.Struct) | `ExtractFullStructSymbol()` (line 395) | Similar to class but value type |
| `InterfaceDef` | `TypeSymbol` (TypeKind.Interface) | `ExtractFullInterfaceSymbol()` (line 401) | Methods only, abstract detection |
| `EnumDef` | `TypeSymbol` (TypeKind.Enum) | Inline (lines 406-418) | Simple enum symbol |
| `VariableDeclaration` | `VariableSymbol` | Inline (lines 420-435) | Module-level constants/variables |
| `FromImportStatement` | Multiple symbols | `ExtractReExportedSymbols()` (line 441) | Re-export handling |

**Visibility Enforcement (lines 592-599)**: All symbols are tracked (even `__private`), but visibility is checked at **import time**:
```csharp
private AccessLevel GetAccessLevel(string name)
{
    if (name.StartsWith("__"))
        return AccessLevel.Private;
    if (name.StartsWith("_"))
        return AccessLevel.Protected;
    return AccessLevel.Public;
}
```

**Type Annotation Conversion (lines 606-636)**: Uses `ConvertTypeAnnotationToSemanticType()` to map primitive types:
```csharp
"int" → SemanticType.Int
"str" → SemanticType.Str
"bool" → SemanticType.Bool
"float32" → SemanticType.Float32
// Complex types → SemanticType.Unknown (resolved later by TypeChecker)
```

This provides early type information before full type checking.

---

### Type Symbol Extraction

Three specialized methods extract complete type information:

#### `ExtractFullClassSymbol(ClassDef classDef)` (lines 644-697)
- Extracts fields from `VariableDeclaration` statements in class body
- Extracts methods via `ExtractMethodSymbol()`
- Identifies constructors (`__init__`)
- Detects `@abstract` decorator
- Copies type parameters from ClassDef
- **Note**: Base class resolution happens later in `NameResolver.ResolveInheritance()` - not here!

#### `ExtractFullStructSymbol(StructDef structDef)` (lines 703-754)
- Similar to class, but `TypeKind.Struct`
- Structs are value types in C# (compiled to `readonly record struct`)

#### `ExtractFullInterfaceSymbol(InterfaceDef interfaceDef)` (lines 760-797)
- Extracts methods (no fields in interfaces)
- Detects abstract methods (methods with ellipsis body `...`)
- **Special logic (lines 782-791)**: Interface methods are implicitly abstract unless they have an implementation (default interface methods)
  ```csharp
  bool hasEllipsisBody = method.Body.Length == 1
      && method.Body[0] is ExpressionStatement { Expression: EllipsisLiteral };
  if (hasEllipsisBody)
  {
      methodSymbol = methodSymbol with { IsAbstract = true };
  }
  ```

#### `ExtractMethodSymbol(FunctionDef method)` (lines 802-836)
- Detects `@static`, `@abstract`, `@virtual`, `@override` decorators
- **Infers `IsStatic` from absence of `self` parameter** (lines 806-810)
  ```csharp
  bool hasSelfParameter = method.Parameters.Any(p =>
      string.Equals(p.Name, "self", StringComparison.OrdinalIgnoreCase));
  bool hasStaticDecorator = method.Decorators.Any(d =>
      d.Name == "static" || d.Name == "staticmethod");
  bool isStatic = hasStaticDecorator || !hasSelfParameter;
  ```
- Preserves type parameters for generic methods
- Extracts variadic parameters (`*args`)

**Code Generation Metadata**: These symbols include everything the RoslynEmitter needs:
- Access levels (public/protected/private)
- Method modifiers (static/virtual/override)
- Parameter defaults
- Type parameters for generics

---

### Re-Export Handling

#### `ExtractReExportedSymbols(FromImportStatement, ModuleInfo)` (lines 454-520)

Handles module-level re-exports like:
```python
# utils/__init__.spy
from .helpers import format_string
```

**Algorithm:**
1. Resolve source module path (line 457)
2. Load source module via `LoadModule()` - **recursive!** (line 465)
3. For `import *`: Copy all public symbols (lines 474-486)
4. For explicit imports: Copy specific symbols with optional aliasing (lines 488-504)
5. Create re-export symbols via `CreateReExportSymbol()` (lines 482, 499)
6. Add to current module's `ExportedSymbols` (lines 483, 500)
7. Store in SemanticBinding for code generation (lines 507-519)

**Why This Matters**: Re-exports enable clean package APIs:
```python
# mypackage/__init__.spy
from .helpers import format_string
from .math import sqrt, pi

# External code can now do:
from mypackage import format_string, sqrt, pi
# Instead of:
from mypackage.helpers import format_string
from mypackage.math import sqrt, pi
```

#### `CreateReExportSymbol(Symbol, FromImportStatement, string?)` (lines 525-575)

Clones a symbol with re-export metadata. Supports cloning:
- `FunctionSymbol`: Preserves parameters, return type (lines 530-541)
- `TypeSymbol`: Preserves fields, methods, type parameters (lines 542-560)
- `VariableSymbol`: Preserves type, const-ness (lines 561-572)

**Key metadata changes:**
```csharp
Name = newName ?? func.Name,              // Handle aliasing
IsReExport = true,                        // Mark as re-export
OriginalModule = fromImport.Module,       // e.g., ".helpers"
DeclarationLine = fromImport.LineStart,   // Points to import, not original
DefiningModule = GetResolvedModulePath(fromImport) ?? fromImport.Module  // For TypeSymbol
```

**Why Clone?**: The cloned symbol lives in the importing module's symbol table but references the original module for code generation (RoslynEmitter emits `using static OriginalModule;`).

---

### .NET Module Resolution

#### `TryResolveNetModule(string moduleName, ...)` (lines 842-893)

Resolves .NET modules through the `ModuleRegistry`:

**Two resolution paths:**
1. **Standard .NET namespaces** (line 853): `"system"` → `System` namespace
   - Calls `ResolveNetNamespaceModule()` (line 855)
   - Returns types from the namespace (e.g., `Console`, `String`)

2. **Sharpy Exports classes**: `"math"` → `Sharpy.Core.Math.Exports`
   - Checks `IsModuleLoaded()` (line 859)
   - Returns static functions from the Exports class (lines 865-892)

**Cache key format**: `.net:modulename` (line 848) distinguishes .NET modules from file paths.

**Example:**
```python
import system  # Resolves to System namespace
from system import Console

import math    # Resolves to Sharpy.Core.Math.Exports
from math import sqrt
```

**Important**: Creates `ModuleInfo` with `IsNetModule = true` and `Module = null!` (lines 873-879)

#### `ResolveNetNamespaceModule(...)` (lines 898-934)

Creates `ModuleInfo` for .NET namespaces:
- Calls `ModuleRegistry.GetNamespaceTypes()` for all types (line 912)
- Optionally includes `Exports` class functions (lines 918-926)
- Sets `IsNetModule = true`, `Module = null`

---

### Module Path Resolution

#### `ResolveModulePath(string moduleName, string? searchPath)` (lines 939-942)

Thin wrapper around `ModuleResolver.Resolve()` that returns just the file path.

#### `ResolveModuleWithResult(...)` (lines 947-957)

Returns full `ModuleResolutionResult` which includes:
- `FullPath`: Absolute file path
- `ModuleName`: Original dotted name
- `CanonicalModuleName`: Resolved canonical name (for relative imports)

**Example:**
```python
# In file: mypackage/utils/helpers.spy
from .math import sqrt  # moduleName = ".math"
```
→ Resolves to:
- `FullPath` = `/path/to/mypackage/utils/math.spy`
- `CanonicalModuleName` = `mypackage.utils.math`

The canonical name is stored for code generation (lines 142-151).

---

### Visibility and Access Control

#### `IsDirectlyImportable(string symbolName)` (lines 962-966)

Returns `false` for `__private` symbols. Enforced during `from module import name`.

#### `IsExportedByImportAll(string)` & `GetImportAllSymbols(ModuleInfo)` (lines 971-985)

Implements the filtering logic for `from module import *` statements:
```csharp
private bool IsExportedByImportAll(string symbolName)
{
    return !symbolName.StartsWith("_");  // Only public symbols (no underscore prefix)
}

public Dictionary<string, Symbol> GetImportAllSymbols(ModuleInfo moduleInfo)
{
    return moduleInfo.ExportedSymbols
        .Where(kvp => IsExportedByImportAll(kvp.Key))
        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
}
```

**Design Rationale**: Matches Python's import semantics:
- `from module import __private` → Error (not directly importable)
- `from module import _protected` → Allowed (explicit import)
- `from module import *` → Only public symbols (no underscore)

**Visibility Summary:**

| Symbol Name | Direct Import? | Wildcard Import? | Access Level |
|-------------|----------------|------------------|--------------|
| `public` | ✅ Yes | ✅ Yes | Public |
| `_protected` | ✅ Yes | ❌ No | Protected |
| `__private` | ❌ No | ❌ No | Private |

---

## Dependencies

### Internal Dependencies

| Dependency | Purpose | File Location |
|------------|---------|---------------|
| `ModuleResolver` | Resolves dotted module names to file paths, handles relative imports | `Semantic/ModuleResolver.cs` |
| `ModuleRegistry` | Provides .NET namespace/assembly integration | `Project/ModuleRegistry.cs` |
| `SemanticBinding` | Stores semantic data separately from AST (immutable AST pattern) | `Semantic/SemanticBinding.cs` |
| `DependencyGraphBuilder` | Tracks file dependencies for incremental builds | `Project/DependencyGraphBuilder.cs` |
| `Lexer.Lexer` | Tokenizes `.spy` source files | `Lexer/Lexer.cs` |
| `Parser.Parser` | Parses tokens into AST | `Parser/Parser.cs` |

### Symbol Types Created

- `FunctionSymbol`: Functions and methods (lines 373-384, 821-835)
- `TypeSymbol`: Classes, structs, interfaces, enums (lines 389-417, 644-797)
- `VariableSymbol`: Module-level variables and fields (lines 423-434, 666-676)
- `ParameterSymbol`: Function parameters (lines 364-371, 812-819)

---

## Patterns and Design Decisions

### 1. Dual-Write Pattern for Migration

Throughout the code, you'll see patterns like (lines 143-151, 220-228, 510-518):
```csharp
if (_semanticBinding != null)
{
    _semanticBinding.SetResolvedModulePath(fromImport, resolvedPath);
}
else
{
    // Legacy fallback: store directly on AST (will be removed in future)
    fromImport.ResolvedModulePath = resolvedPath;
}
```

**Rationale**: Sharpy is migrating from mutable AST to immutable AST + SemanticBinding. During the transition, both approaches are supported to avoid breaking downstream code.

### 2. .NET-First Resolution

`TryResolveNetModule()` is always called before file resolution (lines 86-92, 126-130):
```csharp
var moduleInfo = TryResolveNetModule(importAlias.Name, ...);
if (moduleInfo == null) {
    // Try .spy file
    var modulePath = ResolveModulePath(...);
}
```

**Rationale**: .NET modules like `system` take precedence over user files named `system.spy`. Matches Python's behavior (stdlib before local modules).

### 3. Stack-Based Circular Detection

The `_importChain` stack tracks the current import path (lines 258-264, 328):
```
Push → LoadModule → ResolveImports (may push more) → Pop
```

When a module path appears in the stack, it's a circular import. The stack enables detailed error messages showing the exact cycle (lines 996-1019).

### 4. Context-Aware Resolution

The `_currentModulePath` is set/restored around module loading (lines 292-313):
```csharp
var previousModulePath = _currentModulePath;
_currentModulePath = modulePath;
try {
    // Extract symbols, resolve imports
} finally {
    _currentModulePath = previousModulePath;
}
```

**Why**: Enables correct relative import resolution in nested modules. When loading `a.spy` which imports `b.spy` which has `from .c import x`, the resolver needs to know "we're in b.spy's context" to resolve `.c`.

### 5. Cache-First Loading

The `_moduleCache` ensures each module is loaded exactly once (lines 241-242):
```csharp
if (_moduleCache.TryGetValue(modulePath, out var cached))
    return cached;
```

**Performance**: Prevents re-parsing the same file multiple times in complex import graphs.

### 6. Symbol Cloning for Re-Exports

Re-exported symbols are cloned rather than shared (lines 525-575):
```csharp
return originalSymbol switch {
    FunctionSymbol func => new FunctionSymbol {
        Name = newName ?? func.Name,
        IsReExport = true,
        OriginalModule = fromImport.Module,
        // ... copy all fields
    }
}
```

**Why**: The cloned symbol needs different metadata:
- `IsReExport = true` for code generation
- `DeclarationLine/Column` points to the import statement, not original definition
- `Name` may differ if aliased
- `DefiningModule` tracks the original source for type resolution

### 7. Lazy Symbol Table Population

The ImportResolver only extracts top-level symbol *metadata* (names, access levels, type annotations). It does **not**:
- Perform full type checking
- Resolve type references
- Analyze function bodies

**Rationale**: This keeps import resolution fast and prevents circular dependencies during semantic analysis. The TypeChecker handles deep analysis later.

---

## Debugging Tips

### 1. Tracing Import Resolution

Enable debug logging to see resolution flow:
```csharp
var logger = new ConsoleLogger(LogLevel.Debug);
var resolver = new ImportResolver(logger);
```

You'll see (lines 80, 124, 256, 862, 890, 900, 931):
```
Resolving import: math, utils.helpers
Resolving .NET module: math
Loading module: /path/to/utils/helpers.spy
Loaded .NET module 'math' with 15 functions
```

### 2. Circular Import Errors

If you hit a circular import error, the stack trace shows the chain (lines 998-1019):
```
Circular import detected:
  -> a.spy
  -> b.spy
  -> c.spy
  -> a.spy (cycle)
```

**Fix**: Restructure to use forward references (type annotations only) or move shared code to a third module. See `docs/language_specification/module_system.md` for circular import handling rules.

### 3. Module Not Found

Check `_searchPaths` in debugger. The resolver searches:
1. Relative to current module directory
2. Each path in `_searchPaths`
3. Package directories (`module/` vs `module.spy`)

**Common issue**: Forgetting to set search paths via `ModuleResolver.AddSearchPath()`.

### 4. Symbol Not Found in Module

If `from module import name` fails (lines 194-199):
1. Load module manually: `var info = resolver.LoadModule(path)`
2. Inspect `info.ExportedSymbols.Keys`
3. Check visibility: Is it `__private`?
4. Set breakpoint in `ExtractExportedSymbol()` to see what symbols are being extracted

### 5. .NET Module Not Resolving

Check `ModuleRegistry.IsModuleLoaded()` and `GetModuleFunctions()`. The registry is populated by scanning assemblies for `Exports` classes.

**Debug flow** (lines 842-893):
1. Is `_moduleRegistry` null? (optional dependency)
2. Is it a namespace? (`IsNetNamespace()`)
3. Is the module loaded? (`IsModuleLoaded()`)
4. Are there functions? (`GetModuleFunctions()`)

### 6. Re-Export Not Working

Set breakpoint in `ExtractReExportedSymbols()` (line 454). Verify:
- Source module loaded successfully (line 465)
- Symbols exist in source module (lines 477, 496)
- Re-export dictionary populated (lines 472-505)
- Stored in SemanticBinding or AST (lines 507-519)

### 7. Inspecting Loaded Modules

Use the `_moduleCache` dictionary in debugger to see all loaded modules and their symbols.

### 8. Type Information Missing

If parameters or return types show as `Unknown`:
- **Expected**: `ConvertTypeAnnotationToSemanticType()` only handles primitive types (lines 606-636)
- Complex types (generics, user-defined classes) are resolved later by TypeChecker
- If primitive types are showing as `Unknown`, check the type annotation conversion logic

### Breakpoint Recommendations

Key methods to set breakpoints in:
1. **ResolveImport** / **ResolveFromImport** (lines 78, 122): Entry points for debugging import resolution
2. **LoadModule** (line 238): See when modules are loaded and in what order
3. **IsModuleInChain** (line 990): Catch circular imports as they're detected
4. **ExtractExportedSymbol** (line 355): Understand what symbols are being extracted
5. **TryResolveNetModule** (line 842): Debug .NET assembly resolution
6. **FormatCircularImportChain** (line 998): See circular import error formatting

---

## Contribution Guidelines

### What Changes Might Be Made to This File?

1. **New Import Syntax**: If Sharpy adds syntax like `from __future__ import annotations`, update `ResolveFromImport()`.

2. **Module Attributes**: Python's `__all__` controls `import *` exports. If added to Sharpy, modify `GetImportAllSymbols()`.

3. **Better Error Messages**: Enhanced diagnostics for common import mistakes (e.g., "did you mean?" suggestions).

4. **Performance Optimization**: Caching, lazy loading, or parallel module loading.
   - **Be careful**: Parallel loading requires thread-safety for `_importChain` and `_currentModulePath`

5. **Completing SemanticBinding Migration**: Remove legacy AST property writes (the `else` branches in dual-write patterns).

6. **New Symbol Types**: If Sharpy adds new top-level constructs (e.g., type aliases), add cases to `ExtractExportedSymbol()`.

### Code Conventions

- **Error Reporting**: Always use `AddError()` with line/column for user-friendly diagnostics (lines 1021-1027)
- **Caching**: Check cache before expensive operations (file I/O, parsing)
- **Context Management**: Save/restore `_currentModulePath` in try/finally
- **Null Safety**: Check `IsNetModule` before accessing `ModuleInfo.Module`
- **Immutability**: Prefer SemanticBinding over mutating AST/symbols
- **Early Returns**: Avoid deep nesting for error cases

### Testing

When modifying ImportResolver, ensure you test:
- Simple imports (`import module`)
- From-imports (`from module import name`)
- Wildcard imports (`from module import *`)
- Aliased imports (`import module as alias`, `from module import name as alias`)
- Relative imports (`.module`, `..parent`)
- Re-exports via `__init__.spy`
- Circular import detection
- Visibility rule enforcement (public, protected, private)
- .NET module imports (if applicable)
- Error messages for various failure scenarios

**Integration Tests**: See `src/Sharpy.Compiler.Tests/Integration/TestFixtures/` for file-based tests.

### Related Files to Consider

When changing ImportResolver, you may also need to update:

- `ModuleResolver.cs` (src:Sharpy.Compiler/Semantic): Module path resolution logic
- `NameResolver.cs` (src:Sharpy.Compiler/Semantic): Uses ImportResolver during name binding
- `RoslynEmitter.cs` (src:Sharpy.Compiler/CodeGen): Code generation for imports (`using` directives)
- Language specs in `docs/language_specification/`

---

## Cross-References

### Related Documentation

- **Semantic Analysis**:
  - [`Symbol.md`](./Symbol.md) - Symbol types and metadata
  - [`SemanticBinding.md`](./SemanticBinding.md) - Immutable AST pattern
  - [`ModuleResolver.md`](./ModuleResolver.md) - Path resolution logic
  - [`NameResolver.md`](./NameResolver.md) - Uses ImportResolver for name binding
  - [`TypeChecker.md`](./TypeChecker.md) - Type checking after import resolution
  - [`SymbolTable.md`](./SymbolTable.md) - Symbol table that consumes ImportResolver's output

- **Project System**:
  - [`ModuleRegistry.md`](./ModuleRegistry.md) - .NET module integration
  - `DependencyGraphBuilder.cs` - Build dependency tracking

- **Parser**:
  - `src/Sharpy.Compiler/Parser/Ast/Statement.cs` - ImportStatement, FromImportStatement

### Language Specification

- [`docs/language_specification/import_statements.md`](../../../../language_specification/import_statements.md) - Import syntax
- [`docs/language_specification/module_resolution.md`](../../../../language_specification/module_resolution.md) - Module path resolution rules
- [`docs/language_specification/module_system.md`](../../../../language_specification/module_system.md) - Package structure, circular imports

### Upstream Components

- **Parser** (`src/Sharpy.Compiler/Parser/Parser.cs`) - Produces the AST with ImportStatement and FromImportStatement nodes
- **Lexer** (`src/Sharpy.Compiler/Lexer/Lexer.cs`) - Used to tokenize imported source files

### Downstream Components

- **TypeChecker** (`src/Sharpy.Compiler/Semantic/TypeChecker.cs`) - Uses resolved symbols for type checking
- **NameResolver** (`src/Sharpy.Compiler/Semantic/NameResolver.cs`) - Resolves names to symbols using import information
- **RoslynEmitter** (`src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`) - Generates C# `using` statements from resolved imports

---

## Summary

The `ImportResolver` is a foundational component that makes Sharpy's module system work. It handles the complex task of resolving Python-style imports to actual modules, extracting symbol information, and enforcing visibility rules—all while detecting circular dependencies and supporting both .spy files and .NET assemblies.

**Key Takeaways:**
- **Dual nature**: Handles both `.spy` files and .NET assemblies seamlessly
- **Recursive**: Loading a module may load its imports transitively
- **Cached**: Each module loaded once, even in complex import graphs
- **Context-aware**: Maintains `_currentModulePath` for relative imports
- **Visibility enforcement**: Implements Python's public/protected/private rules
- **Immutable AST**: Uses SemanticBinding for semantic data storage (migration in progress)
- **Error-resilient**: Collects errors rather than throwing exceptions
- **.NET-first**: Prioritizes .NET modules over .spy files

Understanding ImportResolver is crucial for working on:
- Module system features
- Import error diagnostics
- .NET interop expansion
- Build system integration
- LSP/IDE tooling (import auto-complete)
