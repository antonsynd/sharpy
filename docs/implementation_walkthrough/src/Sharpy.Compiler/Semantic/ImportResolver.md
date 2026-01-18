# Walkthrough: ImportResolver.cs

**Source File**: `src/Sharpy.Compiler/Semantic/ImportResolver.cs`

---

## Overview

The **ImportResolver** is a critical component in Sharpy's semantic analysis phase that handles all aspects of import statement resolution. It bridges the gap between Python-style import syntax and the underlying module system, supporting both:

1. **Sharpy modules** (`.spy` files) - Source files written in Sharpy
2. **.NET assemblies** - Compiled .NET libraries loaded through the ModuleRegistry

The ImportResolver is responsible for:
- Resolving module names to actual file paths (via ModuleResolver)
- Loading and parsing imported modules
- Extracting exported symbols from modules
- Detecting circular import dependencies
- Validating import visibility rules (public/protected/private)
- Supporting re-export patterns (`from .submodule import func`)
- Populating symbol information for code generation

**Pipeline Position**: This component sits early in the semantic analysis phase, after the Parser produces the AST but before type checking and name resolution can proceed. It's essential because downstream components need symbol information from imported modules.

---

## Class/Type Structure

### ImportChainEntry (Record)
```csharp
internal record ImportChainEntry(
    string ModulePath,
    int? LineStart,
    int? ColumnStart,
    string? ImportingModule
);
```

A lightweight record used for circular import detection. Each entry represents one step in the import chain, storing:
- **ModulePath**: The absolute path to the module being imported
- **LineStart/ColumnStart**: Source location where the import occurs (for error reporting)
- **ImportingModule**: The module that triggered this import (parent in the chain)

### ImportResolver (Main Class)

The main class that orchestrates import resolution. Key fields:

```csharp
private readonly ICompilerLogger _logger;
private readonly List<SemanticError> _errors = new();
private readonly HashSet<string> _loadedModules = new();
private readonly Stack<ImportChainEntry> _importChain = new();
private readonly Dictionary<string, ModuleInfo> _moduleCache = new();
private readonly ModuleRegistry? _moduleRegistry;
private readonly ModuleResolver _moduleResolver;
private string? _currentModulePath = null;
```

**Field Breakdown**:
- **_errors**: Collects semantic errors during import resolution (available via `Errors` property)
- **_loadedModules**: Prevents duplicate loading of the same module
- **_importChain**: Stack-based tracking for detecting circular imports with detailed error reporting
- **_moduleCache**: Caches parsed modules to avoid re-parsing (keyed by absolute path or `.net:moduleName`)
- **_moduleRegistry**: Optional registry for .NET assembly interop
- **_moduleResolver**: Handles the low-level path resolution logic (relative imports, search paths, etc.)
- **_currentModulePath**: Tracks which module is currently being processed (for relative import resolution)

### ModuleInfo (Data Class)

```csharp
public class ModuleInfo
{
    public string Path { get; init; }
    public Module Module { get; init; }
    public Dictionary<string, Symbol> ExportedSymbols { get; init; }
    public bool IsNetModule { get; init; }
}
```

Represents a loaded module with all its exported symbols. Important notes:
- **Path**: For .spy files, this is the absolute file path; for .NET modules, it's `.net:moduleName`
- **Module**: The parsed AST (null for .NET modules - always check `IsNetModule` before accessing!)
- **ExportedSymbols**: All symbols that can potentially be imported (visibility is enforced separately)
- **IsNetModule**: Flag indicating this module comes from a .NET assembly, not a .spy file

---

## Key Functions/Methods

### Public API Methods

#### ResolveImport(ImportStatement, string?)
```csharp
public List<ModuleInfo> ResolveImport(ImportStatement importStmt, string? searchPath = null)
```

**Purpose**: Resolves `import` statements like `import math` or `import utils.helpers as h`.

**Algorithm**:
1. Iterate through each import name in the statement (can be comma-separated)
2. Try to resolve as .NET module first (via `TryResolveNetModule`)
3. If not a .NET module, resolve as .spy file (via `ResolveModulePath`)
4. Load the module with `LoadModule` (which handles parsing, caching, circular detection)
5. Collect all successfully loaded modules

**Returns**: List of `ModuleInfo` objects, one per successfully resolved import

**Error Handling**: Adds errors for modules that can't be found, but continues processing remaining imports

#### ResolveFromImport(FromImportStatement, string?)
```csharp
public ModuleInfo? ResolveFromImport(FromImportStatement fromImport, string? searchPath = null)
```

**Purpose**: Resolves `from ... import ...` statements like `from math import sqrt, pi` or `from .helpers import *`.

**Key Responsibilities**:
1. Resolve the source module (same logic as `ResolveImport`)
2. Store the canonical module path in `fromImport.ResolvedModulePath` for code generation
3. Validate that imported names actually exist in the module
4. Enforce visibility rules:
   - Direct imports: Can't import `__private` symbols (double underscore)
   - Wildcard imports (`import *`): Only imports public symbols (no leading underscore)
5. Populate `fromImport.ReExportedSymbols` for code generation

**Special Handling for `import *`**:
```csharp
if (fromImport.ImportAll)
{
    // Populate re-export symbols for code generation
    foreach (var (name, symbol) in moduleInfo.ExportedSymbols)
    {
        if (!name.StartsWith("_"))  // Only public symbols
        {
            var reExportSymbol = CreateReExportSymbol(symbol, fromImport);
            fromImport.ReExportedSymbols[name] = reExportSymbol;
        }
    }
}
```

**Returns**: The `ModuleInfo` for the source module, or null if resolution fails

### Core Import Resolution Methods

#### LoadModule(string, int?, int?)
```csharp
private ModuleInfo? LoadModule(string modulePath, int? lineStart, int? columnStart)
```

**Purpose**: The workhorse method that loads, parses, and analyzes a .spy module.

**Critical Algorithm Steps**:

1. **Cache Check**: Return cached module if already loaded
   ```csharp
   if (_moduleCache.TryGetValue(modulePath, out var cached))
       return cached;
   ```

2. **Circular Import Detection**: Check if module is already in the current import chain
   ```csharp
   if (IsModuleInChain(modulePath))
   {
       var chainMessage = FormatCircularImportChain(modulePath);
       AddError(chainMessage, lineStart, columnStart);
       return null;
   }
   ```

3. **Push to Import Chain**: Track this import for circular detection
   ```csharp
   _importChain.Push(new ImportChainEntry(
       modulePath, lineStart, columnStart, _currentModulePath
   ));
   ```

4. **Parse the Module**: Read source, tokenize, parse
   ```csharp
   var source = File.ReadAllText(modulePath);
   var lexer = new Lexer.Lexer(source, _logger);
   var tokens = lexer.TokenizeAll();
   var parser = new Parser.Parser(tokens, _logger);
   var module = parser.ParseModule();
   ```

5. **Extract Exported Symbols**: Walk the AST and collect top-level declarations
   ```csharp
   foreach (var statement in module.Body)
   {
       ExtractExportedSymbol(statement, moduleInfo);
   }
   ```

6. **Recursive Import Resolution**: Process imports within the loaded module (detects transitive circular dependencies)
   ```csharp
   ResolveModuleImports(module, Path.GetDirectoryName(modulePath));
   ```

7. **Cache and Return**: Store in both `_moduleCache` and `_loadedModules`

**Important Context Switching**:
The method temporarily changes `_currentModulePath` to the module being loaded. This is crucial for resolving relative imports correctly within the loaded module:
```csharp
var previousModulePath = _currentModulePath;
_currentModulePath = modulePath;
try {
    // Extract symbols and resolve imports
} finally {
    _currentModulePath = previousModulePath;
}
```

#### ExtractExportedSymbol(Statement, ModuleInfo)
```csharp
private void ExtractExportedSymbol(Statement statement, ModuleInfo moduleInfo)
```

**Purpose**: Extracts symbol information from top-level AST statements and adds them to the module's exported symbols.

**Handled Statement Types**:
- **FunctionDef**: Creates `FunctionSymbol` with parameters and return type
- **ClassDef**: Creates `TypeSymbol` with `TypeKind.Class`
- **StructDef**: Creates `TypeSymbol` with `TypeKind.Struct`
- **InterfaceDef**: Creates `TypeSymbol` with `TypeKind.Interface`
- **EnumDef**: Creates `TypeSymbol` with `TypeKind.Enum`
- **VariableDeclaration**: Creates `VariableSymbol` (constants or regular variables)
- **FromImportStatement**: Handles re-exports via `ExtractReExportedSymbols`

**Access Level Determination**:
Uses naming conventions to determine visibility:
```csharp
private AccessLevel GetAccessLevel(string name)
{
    if (name.StartsWith("__")) return AccessLevel.Private;
    if (name.StartsWith("_")) return AccessLevel.Protected;
    return AccessLevel.Public;
}
```

**Type Annotation Conversion**:
Converts parser-level type annotations to semantic types for primitive types:
```csharp
private SemanticType ConvertTypeAnnotationToSemanticType(TypeAnnotation? typeAnnotation)
{
    // Maps "int" -> SemanticType.Int, "str" -> SemanticType.Str, etc.
    // Returns SemanticType.Unknown for complex types (resolved later)
}
```

#### ExtractReExportedSymbols(FromImportStatement, ModuleInfo)
```csharp
private void ExtractReExportedSymbols(FromImportStatement fromImport, ModuleInfo moduleInfo)
```

**Purpose**: Handles the re-export pattern where a module imports symbols from another module and makes them available to its own importers.

**Example Use Case**:
```python
# utils/__init__.spy
from .helpers import format_string, parse_input
from .math import sqrt

# Now other modules can do: from utils import format_string
```

**Algorithm**:
1. Resolve the source module path
2. Load the source module to get its symbols
3. For `import *`: Re-export all public symbols (no leading underscore)
4. For specific imports: Re-export only the named symbols (respecting aliases)
5. Add re-exported symbols to both:
   - `moduleInfo.ExportedSymbols` (makes them available for import)
   - `fromImport.ReExportedSymbols` (needed for code generation)

**Symbol Cloning**:
Creates new symbol instances with updated metadata:
```csharp
private Symbol CreateReExportSymbol(Symbol originalSymbol, FromImportStatement fromImport, string? newName = null)
{
    // Clones the symbol with:
    // - New name (if aliased)
    // - IsReExport = true
    // - OriginalModule = fromImport.Module
    // - DeclarationLine/Column = fromImport location
}
```

### .NET Interop Methods

#### TryResolveNetModule(string, int?, int?)
```csharp
private ModuleInfo? TryResolveNetModule(string moduleName, int? lineStart, int? columnStart)
```

**Purpose**: Attempts to resolve a module name as a .NET assembly loaded in the `ModuleRegistry`.

**Process**:
1. Check if `ModuleRegistry` is available (it's optional)
2. Query registry to see if module is loaded
3. Check cache with key `.net:moduleName`
4. Retrieve function symbols from registry
5. Create a `ModuleInfo` with `IsNetModule = true` and `Module = null!`

**Important Note**: .NET modules don't have an AST (`Module` property is null), so consumers must check `IsNetModule` before accessing the `Module` property.

### Utility Methods

#### IsDirectlyImportable(string)
```csharp
private bool IsDirectlyImportable(string symbolName)
{
    return !symbolName.StartsWith("__");  // Private symbols can't be directly imported
}
```

Enforces Python-style visibility rules for direct imports (`from module import symbol`).

#### IsExportedByImportAll(string) & GetImportAllSymbols(ModuleInfo)
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

Implements the filtering logic for `from module import *` statements.

#### FormatCircularImportChain(string)
```csharp
private string FormatCircularImportChain(string cycleStartModule)
```

**Purpose**: Creates a user-friendly error message showing the exact circular import chain.

**Output Example**:
```
Circular import detected:
  -> module_a.spy
  -> module_b.spy
  -> module_c.spy
  -> module_a.spy (cycle)
```

The method walks the `_importChain` stack in reverse order, finds where the cycle starts, and formats only the relevant portion of the chain.

---

## Dependencies

### Internal Sharpy Dependencies

1. **ModuleResolver** (`Semantic/ModuleResolver.cs`)
   - Handles low-level path resolution
   - Supports relative imports (`.module`, `..parent`)
   - Manages search paths for module discovery
   - Returns `ModuleResolutionResult` with canonical module names

2. **ModuleRegistry** (`Semantic/ModuleRegistry.cs`)
   - Optional component for .NET assembly interop
   - Provides access to compiled .NET functions
   - Used for importing .NET libraries into Sharpy code

3. **Symbol Types** (`Semantic/Symbol.cs`)
   - `Symbol`, `FunctionSymbol`, `TypeSymbol`, `VariableSymbol`, `ParameterSymbol`
   - Represents exported symbols from modules
   - Includes re-export metadata (`IsReExport`, `OriginalModule`)

4. **SemanticType** (`Semantic/SemanticType.cs`)
   - Type system primitives (`Int`, `Str`, `Bool`, etc.)
   - Used for early type annotation conversion
   - Full type resolution happens later in the pipeline

5. **Parser/AST** (`Parser/Ast/`)
   - `Module`, `ImportStatement`, `FromImportStatement`
   - `FunctionDef`, `ClassDef`, `StructDef`, `InterfaceDef`, `EnumDef`
   - `VariableDeclaration`, `TypeAnnotation`, `Expression`

6. **Lexer and Parser** (`Lexer/`, `Parser/`)
   - Used to tokenize and parse imported `.spy` files
   - Each imported module is fully parsed into an AST

### External Dependencies

- **System.IO**: File operations for reading source files
- **LINQ**: Extensive use for filtering and transforming collections

---

## Patterns and Design Decisions

### 1. **Two-Phase Resolution Strategy**

The ImportResolver tries .NET modules first, then falls back to .spy files:
```csharp
var moduleInfo = TryResolveNetModule(importAlias.Name, lineStart, columnStart);
if (moduleInfo == null) {
    var modulePath = ResolveModulePath(importAlias.Name, searchPath);
    moduleInfo = LoadModule(modulePath, lineStart, columnStart);
}
```

**Rationale**: This allows .NET assemblies to shadow .spy modules with the same name, which is useful for providing optimized native implementations.

### 2. **Lazy Symbol Table Population**

The ImportResolver only extracts top-level symbol *metadata* (names, access levels, type annotations). It does **not**:
- Perform full type checking
- Resolve type references
- Analyze function bodies

**Rationale**: This keeps import resolution fast and prevents circular dependencies during semantic analysis. The TypeChecker handles deep analysis later.

### 3. **Context-Aware Module Loading**

The `_currentModulePath` field is carefully maintained during recursive imports:
```csharp
var previousModulePath = _currentModulePath;
_currentModulePath = modulePath;
try {
    // Process imports within the loaded module
} finally {
    _currentModulePath = previousModulePath;
}
```

**Rationale**: Relative imports (`.submodule`) must be resolved relative to the module being processed, not the original entry point.

### 4. **Import Chain for Detailed Error Reporting**

Rather than just detecting "circular import exists", the resolver maintains a full stack trace:
```csharp
_importChain.Push(new ImportChainEntry(modulePath, lineStart, columnStart, _currentModulePath));
```

**Rationale**: Provides developers with exact import chains for debugging, showing how the circular dependency was created.

### 5. **Re-Export Symbol Cloning**

When symbols are re-exported, they're cloned with new metadata:
```csharp
IsReExport = true,
OriginalModule = fromImport.Module,
DeclarationLine = fromImport.LineStart  // Points to the re-export, not the original
```

**Rationale**: This allows the code generator to properly resolve symbol references and generate correct `using` statements for the original module.

### 6. **Visibility Rules Enforcement**

Three levels of visibility:
- **Public** (no underscore): Importable by anyone, included in `import *`
- **Protected** (single underscore `_foo`): Importable directly, excluded from `import *`
- **Private** (double underscore `__foo`): Not directly importable at all

**Rationale**: Matches Python conventions while providing stronger encapsulation.

### 7. **Module Caching Strategy**

Modules are cached by absolute path (or `.net:moduleName` for .NET modules):
```csharp
_moduleCache[modulePath] = moduleInfo;
_loadedModules.Add(modulePath);
```

**Rationale**: Prevents re-parsing the same module multiple times when it's imported from different locations.

---

## Debugging Tips

### Common Issues and How to Diagnose

#### 1. **Circular Import Errors**

**Symptom**: "Circular import detected" error with a chain of modules

**Debug Strategy**:
- Examine the import chain in the error message
- Look for modules that import each other directly or indirectly
- Check if you can restructure to move shared types to a separate module
- Remember: Circular imports are allowed for type annotations (forward references), but not for base classes

**Code to Check**: `IsModuleInChain()` and `FormatCircularImportChain()`

#### 2. **Module Not Found**

**Symptom**: "Cannot find module 'xyz'" error

**Debug Strategy**:
- Check if the module name uses correct dot notation (`utils.helpers`, not `utils/helpers`)
- For relative imports, verify the dots match the directory structure (`.helpers` = same directory, `..parent` = parent directory)
- Add logging to `ModuleResolver.Resolve()` to see which paths are being searched
- Verify search paths are configured correctly (check `_moduleResolver._searchPaths`)

**Code to Check**: `ResolveModulePath()` and `ModuleResolver.Resolve()`

#### 3. **Symbol Not Found in Module**

**Symptom**: "Module 'xyz' has no exported symbol 'abc'" error

**Debug Strategy**:
- Check if the symbol name is spelled correctly (case-sensitive!)
- Verify the symbol is at the top level of the module (not nested in a class/function)
- Check if the symbol is private (`__private`) and you're trying to import it directly
- Add logging to `ExtractExportedSymbol()` to see what symbols are being extracted

**Code to Check**: `ExtractExportedSymbol()` and symbol visibility checks

#### 4. **Re-Export Not Working**

**Symptom**: Symbol imported in `__init__.spy` isn't available to external modules

**Debug Strategy**:
- Verify `ExtractReExportedSymbols()` is being called (it should happen during `ExtractExportedSymbol` for `FromImportStatement`)
- Check that the re-exported symbols are added to `moduleInfo.ExportedSymbols`
- Ensure the source module is being loaded successfully

**Code to Check**: `ExtractReExportedSymbols()` and `CreateReExportSymbol()`

#### 5. **Type Information Missing**

**Symptom**: Parameters or return types show as `Unknown` in symbol table

**Debug Strategy**:
- Remember: `ConvertTypeAnnotationToSemanticType()` only handles primitive types
- Complex types (generics, user-defined classes) are resolved later by the TypeChecker
- This is expected behavior during import resolution
- If primitive types are showing as `Unknown`, check the type annotation conversion logic

**Code to Check**: `ConvertTypeAnnotationToSemanticType()`

### Logging and Diagnostics

Enable debug logging to trace import resolution:
```csharp
_logger.LogDebug($"Resolving import: {string.Join(", ", importStmt.Names.Select(n => n.Name))}");
_logger.LogInfo($"Loading module: {modulePath}");
```

The logger tracks:
- Which imports are being resolved
- Which modules are being loaded
- .NET module function counts

### Breakpoint Recommendations

Key methods to set breakpoints in:
1. **ResolveImport** / **ResolveFromImport**: Entry points for debugging import resolution
2. **LoadModule**: See when modules are loaded and in what order
3. **IsModuleInChain**: Catch circular imports as they're detected
4. **ExtractExportedSymbol**: Understand what symbols are being extracted
5. **TryResolveNetModule**: Debug .NET assembly resolution

---

## Contribution Guidelines

### When You Might Modify This File

1. **Adding New Import Syntax**
   - Example: Supporting `from module import (name1, name2, name3)` multi-line imports
   - Modify `ResolveFromImport` to handle new AST node structure
   - Add corresponding tests

2. **Changing Visibility Rules**
   - Example: Adding a `@public` decorator to make `_protected` symbols fully public
   - Modify `GetAccessLevel`, `IsDirectlyImportable`, and `IsExportedByImportAll`
   - Update language specification documentation

3. **Improving Error Messages**
   - Example: Adding suggestions for common typos in module names
   - Modify `AddError` calls to include hints
   - Consider adding a "did you mean?" feature

4. **Supporting New Symbol Types**
   - Example: Adding support for type aliases as top-level exports
   - Add new case to `ExtractExportedSymbol` switch statement
   - Create appropriate symbol type (likely extending `Symbol`)
   - Update `CreateReExportSymbol` to handle cloning

5. **Optimizing Module Loading**
   - Example: Parallel loading of independent modules
   - Be extremely careful with `_importChain` and `_currentModulePath` state
   - Consider thread-safety implications

### Testing Considerations

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

### Code Style Notes

- Prefer early returns for error cases to avoid deep nesting
- Use pattern matching switches for AST node type checking
- Maintain the context switching pattern (save/restore `_currentModulePath`)
- Always pop from `_importChain` in a `finally` block
- Add debug logging for important decision points
- Include XML doc comments for public methods

---

## Cross-References

### Related Implementation Files

- **[ModuleResolver.md](ModuleResolver.md)** - Low-level module path resolution
- **[Symbol.md](Symbol.md)** - Symbol type definitions and metadata
- **[SymbolTable.md](SymbolTable.md)** - Symbol table that consumes ImportResolver's output
- **[TypeChecker.md](TypeChecker.md)** - Performs full type checking after import resolution
- **[ModuleRegistry.md](ModuleRegistry.md)** - .NET assembly interop

### Related Specification Documents

- **[Import Statements](../../../../language_specification/import_statements.md)** - Language spec for import syntax
- **[Module System](../../../../language_specification/module_system.md)** - Module resolution and circular import handling
- **[Module Resolution](../../../../language_specification/module_resolution.md)** - Resolution algorithm details (if exists)

### Upstream Components

- **Parser** (`src/Sharpy.Compiler/Parser/Parser.cs`) - Produces the AST with ImportStatement and FromImportStatement nodes
- **Lexer** (`src/Sharpy.Compiler/Lexer/Lexer.cs`) - Used to tokenize imported source files

### Downstream Components

- **TypeChecker** (`src/Sharpy.Compiler/Semantic/TypeChecker.cs`) - Uses resolved symbols for type checking
- **NameResolver** (`src/Sharpy.Compiler/Semantic/NameResolver.cs`) - Resolves names to symbols using import information
- **RoslynEmitter** (`src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`) - Generates C# `using` statements from resolved imports

---

## Summary

The ImportResolver is a foundational component that makes Sharpy's module system work. It handles the complex task of resolving Python-style imports to actual modules, extracting symbol information, and enforcing visibility rules—all while detecting circular dependencies and supporting both .spy files and .NET assemblies.

Key takeaways for newcomers:
- **Resolution happens in two phases**: .NET modules first, then .spy files
- **Symbols are extracted lazily**: Only metadata, not full type checking
- **Context matters**: `_currentModulePath` enables correct relative import resolution
- **Circular imports are tracked carefully**: Full chain available for debugging
- **Re-exports enable clean package APIs**: `__init__.spy` can aggregate submodule exports
- **The resolver is stateful**: It maintains caches, import chains, and current context

Understanding this file is essential for working on:
- Module system improvements
- Import statement features
- .NET interop
- Symbol resolution debugging
