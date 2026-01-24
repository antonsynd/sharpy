# Walkthrough: ProjectCompiler.cs

**Source File**: `src/Sharpy.Compiler/Project/ProjectCompiler.cs`

---

## Overview

`ProjectCompiler.cs` is the **orchestrator for multi-file Sharpy projects**. While the single-file `Compiler` class handles individual `.spy` files, `ProjectCompiler` manages the complexities of compiling entire projects with multiple source files, cross-file dependencies, and proper type visibility across modules.

**In the compilation flow:**
```
Multiple .spy files → ProjectCompiler → Dependency Resolution → Unified Compilation → .NET Assembly
```

This class is responsible for:
- **Multi-file parsing**: Lexing and parsing all `.spy` files in a project
- **Two-phase type resolution**: Collecting type declarations before resolving inheritance (enables cross-file type references)
- **Import resolution**: Handling `import` and `from...import` statements across modules
- **Dependency management**: Building dependency graphs and detecting circular imports
- **Build ordering**: Processing files in dependency order to ensure symbols are available
- **Unified semantic analysis**: Sharing a single symbol table and semantic info across all files
- **Project-wide code generation**: Generating C# for all modules with proper namespace structuring
- **Assembly compilation**: Delegating to `AssemblyCompiler` for final .NET output

Think of it as the **project-level build system** that coordinates all compilation phases across multiple source files.

---

## Class/Type Structure

### Main Class: `ProjectCompiler`

```csharp
public class ProjectCompiler
{
    private readonly ICompilerLogger _logger;
    private readonly ModuleRegistry? _moduleRegistry;

    // Shared compilation state
    private SymbolTable _symbolTable;
    private SemanticInfo _semanticInfo;
    private ImportResolver _importResolver;
    private DependencyGraphBuilder _graphBuilder;
    private DependencyGraph? _dependencyGraph;
    private ProjectModel? _projectModel;

    // Error/warning tracking
    private List<string> _errors;
    private List<string> _warnings;
    private ProjectCompilationMetrics _projectMetrics;
}
```

**Key design decisions:**

1. **Shared State Architecture**: Unlike single-file compilation, all files share:
   - `SymbolTable`: Global symbol registry for cross-file type lookups
   - `SemanticInfo`: Type annotations and semantic metadata
   - `ProjectModel`: Unified model containing all `CompilationUnit` instances

2. **Dependency injection**: Accepts optional `ICompilerLogger` and `ModuleRegistry` for testability

3. **Phase-based compilation**: 7 distinct phases ensure proper ordering of operations

4. **Incremental tracking**: `ProjectModel` stores per-file state (`CompilationUnit`) with phase markers

---

## Compilation Pipeline (7 Phases)

The `Compile()` method orchestrates these phases in strict order:

### Phase 1: Parse All Files (Lines 108-211)
```csharp
private bool ParseAllFiles(ProjectConfig config)
```

**What it does:**
- Iterates through all `.spy` files in `config.SourceFiles`
- Creates a `CompilationUnit` for each file to track its compilation state
- Runs **Lexer** → **Parser** on each file to produce AST modules
- Extracts import statements (`import` and `from...import`) for later resolution
- Tracks per-file metrics (timing for lexing/parsing phases)

**Key implementation details:**
- **Module path computation**: Uses `CompilationUnitFactory.ComputeModulePath()` to determine the module's qualified name from file path (line 124)
- **Error isolation**: Catches `LexerError`, `ParserError`, and generic exceptions per-file to avoid failing the entire build on one bad file
- **Diagnostic tracking**: Stores errors in both:
  - `CompilationUnit.Diagnostics` (per-file structured diagnostics)
  - `_errors` list (flat error messages for final result)
- **Phase markers**: Sets `unit.Phase = CompilationPhase.Parsed` on success or `CompilationPhase.Failed` on error

**Lexing and Parsing Loop (lines 120-144):**
```csharp
var source = File.ReadAllText(sourceFile);

// Create CompilationUnit for this file
var modulePath = CompilationUnitFactory.ComputeModulePath(sourceFile, config.ProjectDirectory);
var compilationUnit = _projectModel!.CreateUnit(sourceFile, modulePath, source);

fileMetrics.StartPhase("Lexical Analysis");
var lexer = new Lexer.Lexer(source, _logger);
var tokens = lexer.TokenizeAll();
fileMetrics.EndPhase();

// Store tokens in CompilationUnit
compilationUnit.Tokens = tokens;
compilationUnit.Phase = CompilationPhase.Lexed;

fileMetrics.StartPhase("Syntax Analysis");
var parser = new Parser.Parser(tokens, _logger);
var module = parser.ParseModule();
fileMetrics.EndPhase();

// Store AST in CompilationUnit
compilationUnit.Ast = module;
compilationUnit.Phase = CompilationPhase.Parsed;
```

**Import extraction (lines 145-156):**
```csharp
// Extract imports from AST
var imports = new List<ImportStatement>();
var fromImports = new List<FromImportStatement>();
foreach (var stmt in module.Body)
{
    if (stmt is ImportStatement import)
        imports.Add(import);
    else if (stmt is FromImportStatement fromImport)
        fromImports.Add(fromImport);
}
compilationUnit.Imports = imports;
compilationUnit.FromImports = fromImports;
```

**Why separate phase:** Parsing all files first allows later phases to see the complete project structure.

---

### Phase 2: Initialize Shared State (Lines 216-243)
```csharp
private void InitializeSharedState()
```

**What it does:**
- Creates the **single shared `SymbolTable`** with builtin types registered
- Initializes **shared `SemanticInfo`** for storing type annotations
- Creates `ImportResolver` for handling cross-module imports
- Sets up `DependencyGraphBuilder` to track import relationships
- Creates `SemanticBinding` for storing semantic data separate from AST
- Registers all parsed files in the dependency graph

**Critical design:** This is where the **shared compilation context** is established. All subsequent phases operate on these shared data structures.

**Implementation (lines 218-242):**
```csharp
var builtinRegistry = new BuiltinRegistry();
_symbolTable = new SymbolTable(builtinRegistry);
_semanticInfo = new SemanticInfo();
_importResolver = new ImportResolver(_logger, _moduleRegistry);

// Create SemanticBinding for storing semantic data separate from AST
var semanticBinding = new SemanticBinding();

// Store in ProjectModel
_projectModel!.GlobalSymbols = _symbolTable;
_projectModel.SemanticInfo = _semanticInfo;
_projectModel.SemanticBinding = semanticBinding;

// Initialize dependency graph builder and connect to import resolver
_graphBuilder = new DependencyGraphBuilder();
_importResolver.SetDependencyGraphBuilder(_graphBuilder);

// Connect SemanticBinding to import resolver for storing import data
_importResolver.SetSemanticBinding(semanticBinding);

// Register all parsed files in the dependency graph
foreach (var sourceFile in _projectModel!.Units.Keys)
{
    _graphBuilder.AddFile(sourceFile);
}
```

**Why it matters:**
This ensures types defined in `file_a.spy` can be referenced in `file_b.spy`. The shared symbol table is the foundation of cross-file type visibility.

---

### Phase 3: Collect Type Declarations (Lines 255-298)

**Two sub-phases:**

#### Phase 3a: Collect Type Shells (Lines 264-280)
```csharp
private void CollectTypeDeclarations(ProjectConfig config)
{
    var nameResolver = new NameResolver(_symbolTable, _logger);

    foreach (var (_, unit) in _projectModel!.Units)
    {
        if (unit.Phase == CompilationPhase.Failed || unit.Ast == null)
            continue;

        nameResolver.SetCurrentFilePath(unit.FilePath);
        nameResolver.ResolveDeclarations(unit.Ast);  // Registers type names
        unit.Phase = CompilationPhase.NamesResolved;
    }
```

**What it does:**
- Creates a **SINGLE `NameResolver` instance** used across ALL files (critical!)
- Calls `ResolveDeclarations()` on each AST to register type names (classes, structs, interfaces)
- Does NOT resolve inheritance yet—just creates "type shells" in the symbol table

**Why a single NameResolver?**
```
// From comments (lines 250-254):
// IMPORTANT: We use a SINGLE NameResolver instance across all files so that the
// _classDefs, _structDefs, and _interfaceDefs lists are populated with ALL
// type definitions before resolving inheritance. This is critical for
// cross-module inheritance to work correctly.
```

Example scenario:
```python
# file_a.spy
class Base:
    pass

# file_b.spy
from file_a import Base
class Derived(Base):  # Needs Base to be registered first!
    pass
```

#### Phase 3b: Resolve Inheritance (Lines 285-287)
```csharp
nameResolver.ResolveInheritance();
```

**What it does:**
- NOW resolves base classes/interfaces using the SAME `NameResolver` instance
- At this point, all types from all files are registered, so cross-module inheritance works
- Collects and reports any inheritance errors (e.g., undefined base class)

**Error collection (lines 289-297):**
```csharp
if (nameResolver.Errors.Any())
{
    foreach (var error in nameResolver.Errors)
    {
        var errorMsg = $"({error.Line},{error.Column}): error: {error.Message}";
        _projectModel!.GlobalDiagnostics.AddError(error.Message, error.Line, error.Column);
        _errors.Add(errorMsg);
    }
}
```

**Phase marker:** `CompilationPhase.NamesResolved`

---

### Phase 4: Resolve Imports (Lines 303-455)
```csharp
private bool ResolveImports(ProjectConfig config)
```

**What it does:**
- Processes `import` and `from...import` statements for each module
- Resolves module paths (relative imports, absolute imports, stdlib modules)
- Populates the symbol table with imported symbols
- Builds the dependency graph by tracking import relationships
- Detects **circular dependencies** (import cycles)

**Key algorithms:**

#### 4.1: Import Statement Resolution (Lines 318-382)

**Handling aliased imports (lines 328-341):**
```csharp
if (importAlias.AsName != null)
{
    // Create a single ModuleSymbol with the alias name
    var aliasedModule = new ModuleSymbol
    {
        Name = importAlias.AsName,
        Kind = SymbolKind.Module,
        FilePath = moduleInfo.Path,
        Exports = new Dictionary<string, Symbol>(moduleInfo.ExportedSymbols)
    };
    _symbolTable.TryDefine(aliasedModule);
    continue;
}
```

**Handling dotted imports (lines 343-381):**
For `import lib.math`, creates nested module structure:
```csharp
// Handle non-aliased imports by building nested module structure
// For "import lib.math", we need lib -> math -> (exports)
var parts = importAlias.Name.Split('.');

// Create the leaf module with actual exports
var leafModule = new ModuleSymbol
{
    Name = parts[^1], // Last part (e.g., "math")
    Kind = SymbolKind.Module,
    FilePath = moduleInfo.Path,
    Exports = new Dictionary<string, Symbol>(moduleInfo.ExportedSymbols)
};

// Build nested structure from inside out
ModuleSymbol currentModule = leafModule;
for (int j = parts.Length - 2; j >= 0; j--)
{
    var parentModule = new ModuleSymbol
    {
        Name = parts[j],
        Kind = SymbolKind.Module,
        FilePath = "", // Parent modules don't have their own file
        Exports = new Dictionary<string, Symbol> { { currentModule.Name, currentModule } }
    };
    currentModule = parentModule;
}
```

**Module merging (lines 370-381):**
If `lib.foo` and `lib.bar` are imported separately, the `lib` module is merged:
```csharp
var existingSymbol = _symbolTable.Lookup(rootName, searchParents: false);
if (existingSymbol is ModuleSymbol existingModule)
{
    // Merge: add the new nested exports to the existing module
    MergeModuleExports(existingModule, currentModule);
}
else
{
    _symbolTable.TryDefine(currentModule);
}
```

#### 4.2: From-Import Statement Resolution (Lines 384-417)
```csharp
else if (statement is FromImportStatement fromImport)
{
    var moduleInfo = _importResolver.ResolveFromImport(fromImport, config.ProjectDirectory);
    if (moduleInfo != null)
    {
        // Use ReExportedSymbols which have DefiningModule set for cross-module type references
        var reExportedSymbols = _projectModel!.SemanticBinding?.GetReExportedSymbols(fromImport)
                                ?? fromImport.ReExportedSymbols;
        var symbolsToImport = reExportedSymbols ?? moduleInfo.ExportedSymbols;

        if (fromImport.ImportAll)
        {
            foreach (var (name, symbol) in symbolsToImport)
            {
                _symbolTable.TryDefine(symbol);
            }
        }
        else
        {
            foreach (var importAlias in fromImport.Names)
            {
                var symbolName = importAlias.AsName ?? importAlias.Name;
                if (symbolsToImport.TryGetValue(symbolName, out var symbol))
                {
                    _symbolTable.TryDefine(symbol);
                }
            }
        }
    }
}
```

**Critical detail (lines 389-394):**
Uses `ReExportedSymbols` instead of `ExportedSymbols` because they have `DefiningModule` set. This metadata enables cross-module type references during code generation.

#### 4.3: Building the Dependency Graph (lines 420-424)
```csharp
// Build the dependency graph after all imports are resolved
_dependencyGraph = _graphBuilder.Build();

// Store in ProjectModel
_projectModel!.DependencyGraph = _dependencyGraph;
```

#### 4.4: Circular Dependency Detection (Lines 426-442)
```csharp
var cycles = _dependencyGraph.DetectCycles();
if (cycles.Count > 0)
{
    foreach (var cycle in cycles)
    {
        var cycleFiles = cycle.Select(Path.GetFileName).ToList();
        var cycleDescription = string.Join(" → ", cycleFiles);
        var errorMsg = $"Circular dependency detected: {cycleDescription}";
        _projectModel!.GlobalDiagnostics.AddError(errorMsg);
        _errors.Add(errorMsg);
    }
    // Don't add import resolver errors when we have circular dependencies
    // as they would be redundant/confusing
    return false;
}
```

**Design decision:** Circular dependency errors take precedence over generic import errors (lines 438-441), because circular imports cause "module not found" errors that are misleading.

**Example output:**
```
Circular dependency detected: module_a.spy → module_b.spy → module_c.spy → module_a.spy
```

---

### Phase 5: Semantic Analysis (Lines 460-539)
```csharp
private bool PerformSemanticAnalysis(ProjectConfig config)
```

**What it does:**
- Runs **type resolution** and **type checking** on all modules
- **Processes files in dependency order** (dependencies before dependents)
- Performs module-level validation (e.g., entry point must have `main()`)

**Key implementation details:**

#### 5.1: Dependency-Ordered Processing (Lines 466-486)
```csharp
if (_dependencyGraph != null)
{
    // Build a mapping from normalized paths to original paths
    var normalizedToOriginal = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var path in _projectModel!.Units.Keys)
    {
        var normalized = NormalizePath(path);
        normalizedToOriginal[normalized] = path;
    }

    // Get build order and map back to original paths
    var buildOrder = _dependencyGraph.GetBuildOrder();
    modulesToProcess = buildOrder
        .Select(normalized => normalizedToOriginal.TryGetValue(normalized, out var original) ? original : null)
        .Where(path => path != null)!;
}
else
{
    modulesToProcess = _projectModel!.Units.Keys;
}
```

**Why dependency order matters:**
- Ensures imported types are fully analyzed before files that import them
- Prevents "type not found" errors when types are defined in different files

**Path normalization (lines 470-475):**
Maps normalized paths (used by `DependencyGraph`) back to original paths (used by `ProjectModel.Units` keys).

#### 5.2: Type Checking Per Module (Lines 488-536)
```csharp
foreach (var sourceFile in modulesToProcess)
{
    var unit = _projectModel!.GetUnit(sourceFile);
    if (unit == null || unit.Phase == CompilationPhase.Failed || unit.Ast == null)
        continue;

    // Type resolution
    fileMetrics.StartPhase("Type Resolution");
    var typeResolver = new TypeResolver(_symbolTable, _semanticInfo, _logger);
    fileMetrics.EndPhase();

    // Type checking
    fileMetrics.StartPhase("Type Checking");
    var pipeline = ValidationPipelineFactory.CreateDefault(_logger);
    var typeChecker = new TypeChecker(_symbolTable, _semanticInfo, typeResolver, _logger, pipeline);

    // Determine if this file is the entry point for module-level validation
    var isEntryPoint = IsEntryPointFileForTypeCheck(sourceFile, config);
    typeChecker.CheckModule(unit.Ast, computeCodeGenInfo: config.UsePrecomputedCodeGenInfo, isEntryPoint: isEntryPoint);
    fileMetrics.EndPhase();
```

**Entry point special handling (lines 510, 666-680):**
Only the entry point file (default: `main.spy`) is validated for having a `main()` function:
```csharp
private static bool IsEntryPointFileForTypeCheck(string file, ProjectConfig config)
{
    var fileName = Path.GetFileName(file);

    // If EntryPoint is specified in config, check against it
    if (!string.IsNullOrWhiteSpace(config.EntryPoint))
    {
        return fileName.Equals(config.EntryPoint, StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals(Path.GetFileName(config.EntryPoint), StringComparison.OrdinalIgnoreCase);
    }

    // Otherwise, default to main.spy for executable projects
    var fileNameNoExt = Path.GetFileNameWithoutExtension(file);
    return fileNameNoExt.Equals("main", StringComparison.OrdinalIgnoreCase);
}
```

**Error handling (lines 514-529):**
```csharp
if (typeChecker.Errors.Any())
{
    // Add to unit diagnostics
    foreach (var error in typeChecker.Errors)
    {
        unit.Diagnostics.AddError(error.Message, error.Line, error.Column, unit.FilePath);
    }
    unit.Phase = CompilationPhase.Failed;

    _errors.AddRange(typeChecker.Errors.Select(e =>
        $"{unit.FilePath}({e.Line},{e.Column}): error: {e.Message}"));
}
else
{
    unit.Phase = CompilationPhase.TypeChecked;
}
```

**Phase marker:** `CompilationPhase.TypeChecked` on success, `CompilationPhase.Failed` on error

---

### Phase 6: Code Generation (Lines 544-612)
```csharp
private Dictionary<string, string> GenerateCode(ProjectConfig config)
```

**What it does:**
- Generates C# code for all successfully type-checked modules
- Uses `RoslynEmitter` to produce Roslyn `CompilationUnitSyntax` trees
- Returns a dictionary: `relative/path.cs` → `generated C# code`

**Key implementation details:**

#### 6.1: Code Generation Context (Lines 568-576)
```csharp
var codeGenContext = new CodeGenContext(_symbolTable, builtinRegistry)
{
    SourceFilePath = sourceFile,
    ProjectNamespace = config.RootNamespace,
    ProjectRootPath = ComputeSourceRootPath(config),
    IsEntryPoint = isEntryPoint,
    Logger = _logger,
    SemanticBinding = _projectModel.SemanticBinding
};
```

**Important fields:**
- `ProjectNamespace`: Root namespace for all generated C# (e.g., `MyProject`)
- `ProjectRootPath`: Common directory prefix for all sources (used for relative path calculations)
- `SemanticBinding`: Provides import metadata needed for cross-module references

#### 6.2: Emitting C# Code (lines 578-580)
```csharp
var emitter = new RoslynEmitter(codeGenContext);
var roslynCompilationUnit = emitter.GenerateCompilationUnit(unit.Ast);
var csharpCode = roslynCompilationUnit.ToFullString();
```

#### 6.3: Error Handling (lines 585-594)
```csharp
// Check for code generation errors
if (codeGenContext.HasErrors)
{
    foreach (var error in codeGenContext.Errors)
    {
        unit.Diagnostics.AddError(error, filePath: sourceFile);
        _errors.Add($"{sourceFile}: error: {error}");
    }
    unit.Phase = CompilationPhase.Failed;
    continue;
}
```

#### 6.4: Storing Generated Code (lines 596-608)
```csharp
// Store generated C# in CompilationUnit
unit.GeneratedCSharp = csharpCode;
unit.Phase = CompilationPhase.CodeGenerated;

// Use relative path for C# file name
var csharpFileName = Path.ChangeExtension(relativePath, ".cs");
generatedCSharp[csharpFileName] = csharpCode;
```

**Preserves directory structure:**
```
src/lib/math.spy → lib/math.cs
src/utils/helpers.spy → utils/helpers.cs
```

**Phase marker:** `CompilationPhase.CodeGenerated`

---

### Phase 7: Assembly Compilation (Lines 617-660)
```csharp
private ProjectCompilationResult CompileAssembly(ProjectConfig config, Dictionary<string, string> generatedCSharp)
```

**What it does:**
- Delegates to `AssemblyCompiler.CompileToAssembly()` to turn C# into a .NET assembly
- Aggregates metrics from assembly compilation into project metrics
- Produces final `ProjectCompilationResult`

**Implementation (lines 619-647):**
```csharp
var assemblyCompiler = new AssemblyCompiler(_logger);
var assemblyResult = assemblyCompiler.CompileToAssembly(generatedCSharp, config);

// Add assembly metrics to project metrics
if (assemblyResult.Metrics != null)
{
    _projectMetrics.SetAssemblyMetrics(assemblyResult.Metrics);
}

if (!assemblyResult.Success)
{
    // Add assembly errors to global diagnostics
    foreach (var error in assemblyResult.Errors)
    {
        _projectModel!.GlobalDiagnostics.AddError(error);
    }
    _errors.AddRange(assemblyResult.Errors);
    return new ProjectCompilationResult
    {
        Success = false,
        Errors = _errors,
        Warnings = assemblyResult.Warnings,
        Metrics = _projectMetrics,
        DependencyGraph = _dependencyGraph,
        ProjectModel = _projectModel
    };
}

_warnings.AddRange(assemblyResult.Warnings);
```

**Final result structure (lines 650-659):**
```csharp
return new ProjectCompilationResult
{
    Success = true,
    OutputAssemblyPath = assemblyResult.OutputAssemblyPath,
    Warnings = _warnings,
    GeneratedCSharpFiles = generatedCSharp,
    Metrics = _projectMetrics,
    DependencyGraph = _dependencyGraph,
    ProjectModel = _projectModel
};
```

---

## Helper Methods

### `ComputeSourceRootPath(ProjectConfig config)` (lines 701-740)

**What it does:**
Finds the **longest common directory prefix** of all source files.

**Example:**
```
Source files:
  /project/src/lib/math.spy
  /project/src/lib/string.spy
  /project/src/utils/helpers.spy

Common prefix: /project/src/
```

**Why it matters:**
Relative paths from this root determine the C# namespace structure:
```
/project/src/lib/math.spy → namespace MyProject.lib { ... }
```

**Implementation (lines 708-737):**
```csharp
// Find the common directory prefix of all source files
var directories = config.SourceFiles
    .Select(f => Path.GetDirectoryName(Path.GetFullPath(f)))
    .Where(d => d != null)
    .Select(d => d!)
    .Distinct()
    .ToList();

if (directories.Count == 0)
    return config.ProjectDirectory;

if (directories.Count == 1)
    // All files are in the same directory
    return directories[0];

// Find the longest common prefix path
var commonPath = directories[0];
foreach (var dir in directories.Skip(1))
{
    commonPath = GetLongestCommonPath(commonPath, dir);
    if (string.IsNullOrEmpty(commonPath))
        return config.ProjectDirectory;
}

return commonPath;
```

---

### `GetLongestCommonPath(string path1, string path2)` (lines 745-766)

**What it does:**
Compares two paths component-by-component to find common prefix.

**Example:**
```
/a/b/c and /a/b/d → /a/b
```

**Implementation:**
```csharp
var parts1 = path1.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
var parts2 = path2.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

var commonParts = new List<string>();
var minLength = System.Math.Min(parts1.Length, parts2.Length);

for (int i = 0; i < minLength; i++)
{
    if (string.Equals(parts1[i], parts2[i], StringComparison.OrdinalIgnoreCase))
    {
        commonParts.Add(parts1[i]);
    }
    else
    {
        break;
    }
}

return string.Join(Path.DirectorySeparatorChar.ToString(), commonParts);
```

---

### `NormalizePath(string path)` (lines 772-780)

**What it does:**
Normalizes file paths for consistent cross-platform comparison.

**Implementation:**
```csharp
private static string NormalizePath(string path)
{
    var normalized = path.Replace('\\', '/');
    if (!OperatingSystem.IsLinux())
    {
        normalized = normalized.ToLowerInvariant();
    }
    return normalized;
}
```

**Why it matters:**
- Windows uses `\`, Unix uses `/`
- Windows/macOS are case-insensitive, Linux is case-sensitive
- `DependencyGraph` uses normalized paths, `ProjectModel.Units` uses original paths

---

### `MergeModuleExports(ModuleSymbol target, ModuleSymbol source)` (lines 786-804)

**What it does:**
Handles multiple imports of the same root module.

**Example:**
```python
import lib.math
import lib.random
# → Merges lib.math and lib.random into single "lib" module
```

**Implementation:**
```csharp
private void MergeModuleExports(ModuleSymbol target, ModuleSymbol source)
{
    foreach (var (name, symbol) in source.Exports)
    {
        if (target.Exports.TryGetValue(name, out var existing))
        {
            // If both are modules, merge recursively
            if (existing is ModuleSymbol existingModule && symbol is ModuleSymbol sourceModule)
            {
                MergeModuleExports(existingModule, sourceModule);
            }
            // Otherwise, the existing symbol takes precedence (first import wins)
        }
        else
        {
            target.Exports[name] = symbol;
        }
    }
}
```

---

## Key Data Structures

### 1. `ProjectModel` (lines 39, 57)
```csharp
private ProjectModel? _projectModel;
_projectModel = new ProjectModel(config);
```

**What it stores:**
- `Units`: Dictionary of `filePath → CompilationUnit`
- `GlobalSymbols`: Shared `SymbolTable`
- `SemanticInfo`: Shared type annotations
- `SemanticBinding`: Import metadata and re-exported symbols
- `DependencyGraph`: Build order and cycle detection
- `GlobalDiagnostics`: Project-level errors (e.g., circular imports)

**Why it matters:**
Provides a unified view of the entire project's compilation state.

**Cross-reference:** See [ProjectModel.md](../Model/ProjectModel.md) for details.

---

### 2. `CompilationUnit` (created in line 125)
```csharp
var compilationUnit = _projectModel!.CreateUnit(sourceFile, modulePath, source);
```

**Tracks per-file state:**
- `FilePath`: Original source file path
- `ModulePath`: Dotted module name (e.g., `lib.math`)
- `SourceCode`: Original `.spy` source
- `Tokens`: Lexer output
- `Ast`: Parser output (`Module` AST node)
- `GeneratedCSharp`: Generated C# code
- `Phase`: Current compilation phase (Lexed → Parsed → NamesResolved → TypeChecked → CodeGenerated)
- `Diagnostics`: Errors/warnings for this file
- `Metrics`: Per-file timing metrics

**Cross-reference:** See [CompilationUnit.md](../Model/CompilationUnit.md) for details.

---

### 3. `DependencyGraph` (lines 36, 421)
```csharp
_dependencyGraph = _graphBuilder.Build();
```

**Provides:**
- `GetBuildOrder()`: Returns files in dependency order
- `DetectCycles()`: Finds circular import chains
- Import relationship tracking

**Cross-reference:** See [DependencyGraph.md](DependencyGraph.md) for details.

---

## Dependencies on Other Components

### Upstream (Consumed by ProjectCompiler)

1. **`Lexer`** (lines 128-129): Tokenizes source code
   - Input: Raw `.spy` source string
   - Output: `List<Token>`

2. **`Parser`** (lines 137-138): Parses tokens into AST
   - Input: `List<Token>`
   - Output: `Module` AST node

3. **`NameResolver`** (line 261): Collects type declarations and resolves inheritance
   - Input: `Module` AST
   - Effect: Populates `SymbolTable` with types

4. **`ImportResolver`** (lines 221, 320, 386): Resolves import statements
   - Input: `ImportStatement` or `FromImportStatement`
   - Output: `ModuleInfo` with exported symbols

5. **`TypeChecker`** (line 507): Performs semantic analysis
   - Input: `Module` AST, `SymbolTable`, `SemanticInfo`
   - Output: Annotated AST with type information

6. **`RoslynEmitter`** (line 578): Generates C# code
   - Input: `Module` AST, `CodeGenContext`
   - Output: `CompilationUnitSyntax` (Roslyn AST)

7. **`AssemblyCompiler`** (lines 620-621): Compiles C# to .NET assembly
   - Input: Generated C# code, `ProjectConfig`
   - Output: `.dll` or `.exe` file

### Downstream (Consumers of ProjectCompiler)

1. **`Sharpy.Cli`**: Command-line interface
   - Calls `ProjectCompiler.Compile()` for multi-file projects
   - Uses `ProjectCompilationResult` to report errors/success

---

## Patterns and Design Decisions

### 1. **Two-Phase Type Declaration** (Phase 3a + 3b)

**Problem:** Cross-module inheritance requires all types to be visible before resolving base classes.

**Solution:**
- **Phase 3a**: Register all type names (create "shells")
- **Phase 3b**: Resolve inheritance relationships

**Why it works:**
```python
# module_a.spy
class Base:
    pass

# module_b.spy
from module_a import Base
class Derived(Base):  # Needs Base to exist in symbol table
    pass
```

If we tried to resolve `Derived`'s base class before registering `Base`, we'd get "Base not found" errors.

---

### 2. **Shared Symbol Table Architecture**

**Problem:** Types defined in one file must be accessible in other files.

**Solution:** Single `SymbolTable` shared across all files (line 219).

**Key insight:** The symbol table is initialized ONCE in Phase 2 and populated incrementally:
- Phase 3: Type declarations added
- Phase 4: Imported symbols added
- Phase 5: Type checking validates references

---

### 3. **Dependency-Ordered Compilation** (Phase 5)

**Problem:** Type checking `file_b.spy` might need types from `file_a.spy` to be fully analyzed.

**Solution:** Build a dependency graph and process files in topological order.

**Example:**
```
file_a.spy  (no imports)
file_b.spy  (imports file_a)
file_c.spy  (imports file_b)

Build order: file_a → file_b → file_c
```

**Implementation (lines 478-481):**
```csharp
var buildOrder = _dependencyGraph.GetBuildOrder();
modulesToProcess = buildOrder.Select(/* normalize paths */);
```

---

### 4. **Error Isolation Per File**

**Problem:** One bad file shouldn't prevent compilation of other files (for better diagnostics).

**Solution:** Try-catch per file with phase markers.

**Example (lines 169-194):**
```csharp
try {
    var lexer = new Lexer(source, _logger);
    var parser = new Parser(tokens, _logger);
    compilationUnit.Phase = CompilationPhase.Parsed;
} catch (ParserError ex) {
    unit.Diagnostics.AddError(ex.Message, ex.Line, ex.Column, sourceFile);
    unit.Phase = CompilationPhase.Failed;
    _errors.Add($"{sourceFile}({ex.Line},{ex.Column}): error: {ex.Message}");
}
```

**Benefits:**
- User sees errors from ALL files, not just the first failure
- `ProjectModel` tracks which files succeeded vs. failed

---

### 5. **Circular Dependency Prioritization** (lines 426-442)

**Design decision:** Report circular imports as the primary error, suppressing misleading "module not found" errors.

**Rationale:**
```python
# a.spy
from b import foo  # Error: module b not found (misleading)

# b.spy
from a import bar  # Error: module a not found (misleading)

# Better error:
Circular dependency detected: a.spy → b.spy → a.spy
```

**Implementation:**
```csharp
if (cycles.Count > 0) {
    // Report cycles
    return false;  // Don't add ImportResolver errors
}
```

---

### 6. **Immutable AST + Separate Semantic Data**

**Problem:** Mutating AST nodes violates immutability principle and makes caching difficult.

**Solution:** Store semantic annotations in `SemanticBinding`, not in AST nodes.

**Example:**
```csharp
// BAD: Modifying AST node
fromImport.ReExportedSymbols = symbols;

// GOOD: Store in SemanticBinding
_projectModel.SemanticBinding.SetReExportedSymbols(fromImport, symbols);
```

**Why it matters:**
- Enables parallel compilation (future)
- Easier caching for LSP
- Cleaner separation of concerns

---

## Debugging Tips

### 1. **Enable Debug Logging**

Per-file metrics are logged at `Debug` level:
```csharp
if (_logger.IsEnabled(CompilerLogLevel.Debug)) {
    _logger.LogDebug($"Parsed {Path.GetFileName(sourceFile)}: {fileMetrics.TotalDuration.TotalMilliseconds:F2} ms");
}
```

**How to enable:** Pass a logger with `Debug` level enabled to `ProjectCompiler` constructor.

---

### 2. **Inspect ProjectModel After Each Phase**

The `ProjectModel` is updated after each phase and returned in `ProjectCompilationResult.ProjectModel`.

**Useful for debugging:**
- Check `unit.Phase` to see where compilation stopped
- Inspect `unit.Diagnostics` for file-specific errors
- Review `GlobalDiagnostics` for project-level errors (circular imports, name resolution)

---

### 3. **Trace Import Resolution**

Import errors can be subtle (wrong paths, typos, circular dependencies).

**Debug checklist:**
1. Check `DependencyGraph` for import relationships
2. Look for cycles using `DetectCycles()`
3. Verify `ImportResolver.Errors` for unresolved imports
4. Inspect `SemanticBinding.GetReExportedSymbols()` for from-import metadata

---

### 4. **Path Normalization Issues**

The code normalizes paths for case-insensitive comparisons (lines 772-780).

**Problem:** `ProjectModel.Units` uses original paths, but `DependencyGraph` uses normalized paths.

**Solution:** Lines 470-481 map normalized paths back to original paths when getting build order.

**Watch for:** Path comparison bugs on Windows vs. Linux.

---

### 5. **Common Failure Scenarios**

| Symptom | Likely Cause | Where to Look |
|---------|-------------|---------------|
| "Type X not found" in multi-file project | Name resolution ran before imports | Check Phase 3 vs. Phase 4 ordering |
| "Circular dependency" false positive | Path normalization mismatch | Check `NormalizePath()` logic |
| Symbols from `from X import Y` not found | `ReExportedSymbols` not set | Check `SemanticBinding.GetReExportedSymbols()` |
| Wrong build order | Dependency graph construction error | Inspect `DependencyGraphBuilder` traces |
| Entry point validation failing for wrong file | `IsEntryPointFileForTypeCheck()` logic | Lines 666-680 |

---

## Contribution Guidelines

### When to Modify This File

1. **Adding new compilation phases**: Update `Compile()` and create new phase methods
2. **Changing import resolution**: Modify Phase 4 (`ResolveImports`)
3. **Adjusting build order logic**: Update Phase 5 (`PerformSemanticAnalysis`)
4. **Adding new validation**: Integrate into Phase 5 via `ValidationPipeline`
5. **Improving error reporting**: Enhance diagnostic collection in each phase

---

### Testing Changes

**Key test files:**
- `src/Sharpy.Compiler.Tests/Model/ProjectCompilerModelIntegrationTests.cs`
- Integration tests in `src/Sharpy.Compiler.Tests/Integration/`

**Test multi-file scenarios:**
- Cross-module imports
- Circular dependencies
- Inheritance across files
- Aliased imports (`import x as y`)
- Star imports (`from x import *`)

---

### Maintaining Phase Independence

**Critical rule:** Each phase should only depend on COMPLETED phases.

**Example violations to avoid:**
```csharp
// BAD: Type checking before import resolution
PerformSemanticAnalysis(config);  // Phase 5
ResolveImports(config);           // Phase 4

// GOOD: Proper phase ordering
ResolveImports(config);           // Phase 4 first
PerformSemanticAnalysis(config);  // Phase 5 after
```

---

### Adding New ProjectConfig Options

If adding new build settings:
1. Add field to `ProjectConfig`
2. Pass through `CodeGenContext` in Phase 6 (line 568)
3. Document in [ProjectConfig.md](../ProjectConfig.md)

---

## Cross-References

### Related Files
- [ProjectModel.md](../Model/ProjectModel.md) - Unified project compilation state
- [CompilationUnit.md](../Model/CompilationUnit.md) - Per-file compilation tracking
- [DependencyGraph.md](DependencyGraph.md) - Import dependency management
- [AssemblyCompiler.md](../AssemblyCompiler.md) - Final assembly generation (Phase 7)
- [Compiler.md](../Compiler.md) - Single-file compilation (compare with multi-file)

### Semantic Analysis Components
- [NameResolver.md](../Semantic/NameResolver.md) - Type declaration collection (Phase 3)
- [ImportResolver.md](../Semantic/ImportResolver.md) - Import resolution (Phase 4)
- [TypeChecker.md](../Semantic/TypeChecker.md) - Type checking (Phase 5)

### Code Generation
- [RoslynEmitter.md](../CodeGen/RoslynEmitter.md) - C# code generation (Phase 6)

---

## Summary

`ProjectCompiler` is the **multi-file orchestrator** that:
1. Parses all `.spy` files into ASTs
2. Builds a shared symbol table with all types visible
3. Resolves imports and builds dependency graphs
4. Type-checks files in dependency order
5. Generates C# code for all modules
6. Compiles to a .NET assembly

**Key innovation:** Two-phase type resolution + shared symbol table enables seamless cross-module type references, making multi-file Sharpy projects feel like a unified compilation unit rather than isolated files.
