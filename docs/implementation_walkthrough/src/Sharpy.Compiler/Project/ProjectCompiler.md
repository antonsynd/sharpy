# Walkthrough: ProjectCompiler.cs

**Source File**: `src/Sharpy.Compiler/Project/ProjectCompiler.cs`

---

## Overview

`ProjectCompiler` is the orchestrator for **multi-file Sharpy project compilation**. It coordinates the entire compilation pipeline from parsing multiple `.spy` files through semantic analysis and C# code generation, finally producing a .NET assembly. This is distinct from the single-file `Compiler` class—`ProjectCompiler` handles cross-file dependencies, import resolution, and proper build ordering.

**Key Responsibilities**:
- Manage compilation phases across multiple source files
- Handle cross-file type visibility through two-phase name resolution
- Resolve imports and build dependency graphs
- Orchestrate semantic analysis in dependency order
- Generate C# code and compile to a .NET assembly
- Track per-file and project-wide metrics/diagnostics

---

## Architecture Position

```
CLI (sharpyc) → Compiler.CompileProject() → ProjectCompiler.Compile()
                                              ↓
                       [Multi-file Pipeline - 7 Phases]
                                              ↓
                                     ProjectCompilationResult
                                     (assembly + diagnostics)
```

The `ProjectCompiler` sits between the high-level `Compiler` facade and the individual compilation components (Lexer, Parser, SemanticAnalyzer, etc.).

---

## Class Structure

### Main Class: `ProjectCompiler`

```csharp
public class ProjectCompiler
{
    private readonly ICompilerLogger _logger;
    private readonly ModuleRegistry? _moduleRegistry;

    // Shared across all files
    private SymbolTable _symbolTable;
    private SemanticInfo _semanticInfo;
    private ImportResolver _importResolver;

    // Error tracking
    private List<string> _errors;
    private List<string> _warnings;

    // Metrics and dependency analysis
    private ProjectCompilationMetrics _projectMetrics;
    private DependencyGraphBuilder _graphBuilder;
    private DependencyGraph? _dependencyGraph;

    // Unified project model (central data structure)
    private ProjectModel? _projectModel;
}
```

**Design Highlights**:
- **Shared State**: A single `SymbolTable` and `SemanticInfo` are used across all files, enabling cross-file symbol resolution
- **ProjectModel**: Central data structure containing all `CompilationUnit` instances, each representing one `.spy` file with its tokens, AST, diagnostics, and generated C#
- **Dependency Graph**: Tracks import relationships for proper build ordering and incremental compilation support

---

## The 7-Phase Compilation Pipeline

The `Compile(ProjectConfig config)` method orchestrates seven sequential phases:

### Phase 1: Parse All Files

**Method**: `ParseAllFiles(ProjectConfig config)` (lines 108-211)

**What it does**:
1. Iterates over all source files in `config.SourceFiles`
2. For each file:
   - Creates a `CompilationUnit` via `ProjectModel.CreateUnit()`
   - Lexes source → stores tokens in unit
   - Parses tokens → stores AST in unit
   - Extracts import statements (for later dependency resolution)
   - Creates per-file `CompilationMetrics`
3. Catches and records `LexerError` / `ParserError` exceptions per file

**Key Details**:
- Each file gets its own `CompilationUnit` stored in `_projectModel.Units`
- Errors in one file don't prevent parsing others (fail-fast is deferred)
- Import statements are extracted early to prepare for Phase 4

**Error Handling**:
```csharp
catch (LexerError ex)
{
    var unit = _projectModel.GetUnit(sourceFile);
    unit.Diagnostics.AddError(ex.Message, ex.Line, ex.Column, sourceFile);
    unit.Phase = CompilationPhase.Failed;
    _errors.Add($"{sourceFile}({ex.Line},{ex.Column}): error: {ex.Message}");
}
```

---

### Phase 2: Initialize Shared State

**Method**: `InitializeSharedState()` (lines 213-243)

**What it does**:
1. Creates the global `SymbolTable` with builtin types (int, str, list, etc.)
2. Initializes `SemanticInfo` (stores type annotations separate from AST)
3. Creates `ImportResolver` with the optional `ModuleRegistry`
4. Initializes `DependencyGraphBuilder` and connects it to the import resolver
5. Registers all parsed files in the dependency graph

**Why this matters**:
- **Single Symbol Table**: Enables cross-file type references (e.g., `from module_a import MyClass`)
- **SemanticBinding**: Maintains immutable AST principle—semantic data goes in `SemanticBinding`, not AST nodes

```csharp
var builtinRegistry = new BuiltinRegistry();
_symbolTable = new SymbolTable(builtinRegistry);
_semanticInfo = new SemanticInfo();
_importResolver = new ImportResolver(_logger, _moduleRegistry);

// Create SemanticBinding for storing semantic data separate from AST
var semanticBinding = new SemanticBinding();
_projectModel!.SemanticBinding = semanticBinding;

// Initialize dependency graph builder
_graphBuilder = new DependencyGraphBuilder();
_importResolver.SetDependencyGraphBuilder(_graphBuilder);
```

---

### Phase 3: Collect Type Declarations

**Method**: `CollectTypeDeclarations(ProjectConfig config)` (lines 245-298)

**Critical Design Decision**: This is a **two-phase process** to enable cross-file inheritance:

#### Phase 3a: Collect Type Shells (lines 259-280)
- Creates a **single** `NameResolver` instance for all files (not one per file!)
- Calls `NameResolver.ResolveDeclarations(unit.Ast)` for each file
- This registers type **names** (classes, structs, interfaces) in the symbol table WITHOUT resolving their base classes or members yet

#### Phase 3b: Resolve Inheritance (lines 282-286)
- Calls `NameResolver.ResolveInheritance()` once after all types are declared
- Now that all types from all files are in the symbol table, cross-file inheritance can be resolved

**Why a single NameResolver?**
```csharp
// IMPORTANT: We use a SINGLE NameResolver instance across all files so that the
// _classDefs, _structDefs, and _interfaceDefs lists are populated with ALL type
// definitions before resolving inheritance. This is critical for cross-module
// inheritance to work correctly.
var nameResolver = new NameResolver(_symbolTable, _logger);

// Phase 3a: Collect all type declarations (shells only)
foreach (var (_, unit) in _projectModel!.Units)
{
    nameResolver.SetCurrentFilePath(unit.FilePath);
    nameResolver.ResolveDeclarations(unit.Ast);
    unit.Phase = CompilationPhase.NamesResolved;
}

// Phase 3b: Resolve inheritance (using the SAME NameResolver instance)
nameResolver.ResolveInheritance();
```

**Example Scenario**:
```python
# file_a.spy
class Animal:
    pass

# file_b.spy
from file_a import Animal

class Dog(Animal):  # Needs Animal to already exist in symbol table
    pass
```

Without the two-phase approach, `Dog` might be processed before `Animal` is declared, causing a "base class not found" error.

---

### Phase 4: Resolve Imports

**Method**: `ResolveImports(ProjectConfig config)` (lines 300-455)

**What it does**:
1. For each file's import statements:
   - Calls `ImportResolver.ResolveImport()` or `ResolveFromImport()`
   - Adds imported symbols to the global symbol table
   - Records dependencies in `DependencyGraphBuilder`
2. Builds the final `DependencyGraph` from collected dependencies
3. Detects circular dependencies (e.g., `a.spy` imports `b.spy`, `b.spy` imports `a.spy`)

**Import Handling Details**:

- **Module Imports** (`import lib.math`): Creates nested `ModuleSymbol` structure (lines 343-382)
  ```csharp
  // For "import lib.math", creates:
  // lib (ModuleSymbol) -> math (ModuleSymbol) -> (exports from lib/math.spy)
  var parts = importAlias.Name.Split('.');
  var leafModule = new ModuleSymbol { Name = parts[^1], ... };

  // Build nested structure from inside out
  ModuleSymbol currentModule = leafModule;
  for (int j = parts.Length - 2; j >= 0; j--)
  {
      var parentModule = new ModuleSymbol {
          Name = parts[j],
          Exports = new Dictionary<string, Symbol> { { currentModule.Name, currentModule } }
      };
      currentModule = parentModule;
  }
  ```

- **From Imports** (`from lib.math import sqrt`): Directly imports symbols into current scope (lines 384-417)
  ```csharp
  // Adds sqrt directly to symbol table (no lib.math prefix needed)
  var reExportedSymbols = _projectModel.SemanticBinding?.GetReExportedSymbols(fromImport)
                          ?? fromImport.ReExportedSymbols;
  foreach (var (name, symbol) in reExportedSymbols)
  {
      _symbolTable.TryDefine(symbol);
  }
  ```

- **Aliased Imports** (`import numpy as np`): Symbol gets the alias name (lines 328-341)
  ```csharp
  if (importAlias.AsName != null)
  {
      var aliasedModule = new ModuleSymbol {
          Name = importAlias.AsName,
          FilePath = moduleInfo.Path,
          Exports = new Dictionary<string, Symbol>(moduleInfo.ExportedSymbols)
      };
      _symbolTable.TryDefine(aliasedModule);
  }
  ```

**Circular Dependency Detection** (lines 427-442):
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
    return false;  // Stop compilation
}
```

---

### Phase 5: Semantic Analysis

**Method**: `PerformSemanticAnalysis(ProjectConfig config)` (lines 457-536)

**What it does**:
1. Processes files in **dependency order** (dependencies before dependents)
2. For each file:
   - Creates `TypeResolver` (resolves type expressions like `list[int]`)
   - Creates `TypeChecker` with validation pipeline
   - Calls `TypeChecker.CheckModule()` to type-check all statements/expressions
3. Records type errors in per-file diagnostics

**Build Order Example**:
```
If file_c.spy imports file_b.spy, and file_b.spy imports file_a.spy:
Build order: [file_a.spy, file_b.spy, file_c.spy]
```

This ensures that when type-checking `file_c`, all symbols from `file_a` and `file_b` are already resolved.

**Implementation** (lines 464-533):
```csharp
// Process modules in dependency order (dependencies before dependents)
IEnumerable<string> modulesToProcess;
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

foreach (var sourceFile in modulesToProcess)
{
    var unit = _projectModel!.GetUnit(sourceFile);

    // Type resolution
    var typeResolver = new TypeResolver(_symbolTable, _semanticInfo, _logger);

    // Type checking
    var pipeline = ValidationPipelineFactory.CreateDefault(_logger);
    var typeChecker = new TypeChecker(_symbolTable, _semanticInfo, typeResolver, _logger, pipeline);
    typeChecker.CheckModule(unit.Ast, computeCodeGenInfo: config.UsePrecomputedCodeGenInfo);

    if (typeChecker.Errors.Any())
    {
        unit.Phase = CompilationPhase.Failed;
        _errors.AddRange(typeChecker.Errors);
    }
    else
    {
        unit.Phase = CompilationPhase.TypeChecked;
    }
}
```

**CodeGenInfo Precomputation**:
- If `config.UsePrecomputedCodeGenInfo = true`, type checker annotates symbols with C# code generation metadata (method version numbers, mangled names, etc.)
- This is required for code generation to work (legacy approach removed)

---

### Phase 6: Code Generation

**Method**: `GenerateCode(ProjectConfig config)` (lines 538-627)

**What it does**:
1. For each successfully type-checked file:
   - Creates `CodeGenContext` with symbol table and project namespace
   - Instantiates `RoslynEmitter` (uses Roslyn's `SyntaxFactory` to build C# AST)
   - Calls `GenerateCompilationUnit()` to produce C# code
   - Stores generated C# in `unit.GeneratedCSharp`
2. Returns a dictionary mapping file paths to C# code

**Entry Point Detection** (lines 564-581):
```csharp
// Determine if this file is the entry point
var isEntryPoint = IsEntryPointFile(sourceFile, config);

bool IsEntryPointFile(string file, ProjectConfig cfg)
{
    var fileName = Path.GetFileName(file);

    // If EntryPoint is specified in config, check against it
    if (!string.IsNullOrWhiteSpace(cfg.EntryPoint))
    {
        return fileName.Equals(cfg.EntryPoint, StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals(Path.GetFileName(cfg.EntryPoint), StringComparison.OrdinalIgnoreCase);
    }

    // Otherwise, default to main.spy for executable projects
    var fileNameNoExt = Path.GetFileNameWithoutExtension(file);
    return fileNameNoExt.Equals("main", StringComparison.OrdinalIgnoreCase);
}
```

The entry point file gets special treatment—the emitter generates a `Main()` method for executable projects.

**CodeGenContext Setup** (lines 583-591):
```csharp
var codeGenContext = new CodeGenContext(_symbolTable, builtinRegistry)
{
    SourceFilePath = sourceFile,
    ProjectNamespace = config.RootNamespace,  // e.g., "MyProject"
    ProjectRootPath = ComputeSourceRootPath(config),
    IsEntryPoint = isEntryPoint,
    Logger = _logger,
    SemanticBinding = _projectModel.SemanticBinding  // For retrieving semantic data
};

var emitter = new RoslynEmitter(codeGenContext);
var roslynCompilationUnit = emitter.GenerateCompilationUnit(unit.Ast);
var csharpCode = roslynCompilationUnit.ToFullString();
```

---

### Phase 7: Assembly Compilation

**Method**: `CompileAssembly(ProjectConfig config, Dictionary<string, string> generatedCSharp)` (lines 629-675)

**What it does**:
1. Delegates to `AssemblyCompiler.CompileToAssembly()`
2. The assembly compiler:
   - Uses Roslyn's `CSharpCompilation` API
   - References required .NET assemblies (Sharpy.Core, System.Runtime, etc.)
   - Emits a `.dll` or `.exe` to `bin/{Configuration}/{TargetFramework}/`
3. Returns `ProjectCompilationResult` with success/failure and diagnostics

**Result Construction** (lines 665-674):
```csharp
return new ProjectCompilationResult
{
    Success = true,
    OutputAssemblyPath = assemblyResult.OutputAssemblyPath,  // e.g., bin/Debug/net8.0/MyProject.dll
    Warnings = _warnings,
    GeneratedCSharpFiles = generatedCSharp,  // For debugging/inspection
    Metrics = _projectMetrics,  // Timing data for each phase
    DependencyGraph = _dependencyGraph,  // For tooling (LSP, incremental builds)
    ProjectModel = _projectModel  // All CompilationUnits with full state
};
```

---

## Key Data Structures

### 1. `ProjectModel` (src/Sharpy.Compiler/Model/ProjectModel.cs)

Central container for all compilation state:

```csharp
public class ProjectModel
{
    // All source files being compiled
    public IReadOnlyDictionary<string, CompilationUnit> Units { get; }

    // Shared symbol table across all files
    public SymbolTable? GlobalSymbols { get; internal set; }

    // Shared semantic info (type annotations)
    public SemanticInfo? SemanticInfo { get; internal set; }

    // Immutable AST annotations
    public SemanticBinding? SemanticBinding { get; internal set; }

    // Import dependency graph
    public DependencyGraph? DependencyGraph { get; internal set; }

    // Project-level diagnostics (e.g., circular dependency errors)
    public DiagnosticBag GlobalDiagnostics { get; }
}
```

**Key Methods**:
- `GetBuildOrder()`: Returns files in dependency order
- `GetParallelizableGroups()`: Groups files that can be compiled concurrently (future optimization)
- `GetAllDiagnostics()`: Combines global + per-file errors/warnings

### 2. `CompilationUnit` (src/Sharpy.Compiler/Model/CompilationUnit.cs)

Represents a single `.spy` file's compilation artifacts:

```csharp
public class CompilationUnit
{
    // Source information
    public string FilePath { get; }           // e.g., /path/to/myapp/src/utils.spy
    public string ModulePath { get; }         // e.g., "myapp.utils"
    public string SourceText { get; }         // Raw source code
    public string ContentHash { get; }        // SHA-256 (for incremental compilation)

    // Compilation artifacts
    public IReadOnlyList<Token>? Tokens { get; }
    public Module? Ast { get; }
    public string? GeneratedCSharp { get; set; }

    // Semantic data
    public IReadOnlyList<ImportStatement> Imports { get; }
    public IReadOnlyList<FromImportStatement> FromImports { get; }

    // Diagnostics and metrics
    public DiagnosticBag Diagnostics { get; }
    public CompilationMetrics? Metrics { get; }
    public CompilationPhase Phase { get; internal set; }  // Created → Lexed → Parsed → ...
}
```

**Compilation Phases**:
```csharp
public enum CompilationPhase
{
    Created,          // Unit created, not processed
    Lexed,            // Tokenization complete
    Parsed,           // AST built
    NamesResolved,    // Type declarations registered
    TypeChecked,      // Semantic analysis complete
    CodeGenerated,    // C# code emitted
    Failed            // Compilation error
}
```

### 3. `ProjectConfig` (src/Sharpy.Compiler/ProjectConfig.cs)

Loaded from `.spyproj` files (XML format similar to MSBuild):

```csharp
public class ProjectConfig
{
    public string RootNamespace { get; }       // e.g., "MyGame"
    public string OutputType { get; }          // "exe" or "library"
    public string TargetFramework { get; }     // e.g., "net8.0"
    public string? EntryPoint { get; }         // e.g., "main.spy"
    public List<string> SourceFiles { get; }   // Resolved from glob patterns
    public List<string> References { get; }    // .NET assembly references
    public List<string> ModulePaths { get; }   // Paths to search for .spy modules
    public string Configuration { get; }       // "Debug" or "Release"
}
```

**Example .spyproj**:
```xml
<Project>
  <PropertyGroup>
    <RootNamespace>MyGame</RootNamespace>
    <OutputType>exe</OutputType>
    <EntryPoint>main.spy</EntryPoint>
  </PropertyGroup>
  <ItemGroup>
    <SpyFile Include="**/*.spy" Exclude="tests/**/*.spy" />
    <Reference Include="UnityEngine.dll" />
  </ItemGroup>
</Project>
```

---

## Important Design Patterns

### 1. **Fail-Slow Error Accumulation**

Unlike single-file compilation (which stops at first error), `ProjectCompiler` tries to compile as many files as possible:

```csharp
// Errors don't throw exceptions - they're accumulated in lists
foreach (var sourceFile in config.SourceFiles)
{
    try { /* parse */ }
    catch (ParserError ex)
    {
        _errors.Add(ex.Message);
        // Continue to next file
    }
}

// Check errors at phase boundaries
if (_errors.Any())
    return CreateFailureResult();
```

**Why?** Better developer experience—see all errors at once instead of fixing one at a time.

### 2. **Single Shared Symbol Table**

One `SymbolTable` across all files, unlike some compilers that use per-file scopes:

```csharp
// Phase 2: Create ONCE
_symbolTable = new SymbolTable(builtinRegistry);

// Phase 3: ALL files add to it
foreach (var unit in _projectModel.Units)
{
    nameResolver.ResolveDeclarations(unit.Ast);  // Mutates shared _symbolTable
}

// Phase 5: ALL files read from it
foreach (var unit in buildOrder)
{
    var typeChecker = new TypeChecker(_symbolTable, ...);  // Reads symbols from other files
}
```

**Trade-off**: Simpler cross-file resolution, but requires careful ordering (hence dependency graph).

### 3. **Immutable AST + Separate Semantic Data**

AST nodes (from `Parser.Ast` namespace) are immutable. Semantic annotations go in `SemanticBinding`:

```csharp
// BAD: Modifying AST node (violates immutability)
myFunctionDef.ResolvedType = intType;

// GOOD: Store in SemanticBinding
_projectModel.SemanticBinding.SetResolvedType(myFunctionDef, intType);
```

**Why?** Enables parallel compilation (future), easier caching for LSP, cleaner separation of concerns.

### 4. **Path Normalization**

Cross-platform path handling (lines 767-775):

```csharp
private static string NormalizePath(string path)
{
    var normalized = path.Replace('\\', '/');  // Unix-style separators
    if (!OperatingSystem.IsLinux())
    {
        normalized = normalized.ToLowerInvariant();  // Case-insensitive on Windows/macOS
    }
    return normalized;
}
```

Used when comparing file paths in dictionaries (e.g., `ProjectModel.Units` keys).

---

## Helper Methods

### `ComputeSourceRootPath(ProjectConfig config)` (lines 693-735)

Finds the common directory prefix of all source files:

```csharp
// If all files are in /path/to/project/src/, returns "/path/to/project/src"
// Used for computing relative module paths in generated C#
```

### `GetLongestCommonPath(string path1, string path2)` (lines 737-761)

Compares two paths component-by-component:

```csharp
// "/a/b/c" and "/a/b/d" → "/a/b"
```

### `MergeModuleExports(ModuleSymbol target, ModuleSymbol source)` (lines 777-799)

Handles multiple imports of the same root module:

```csharp
// import lib.math
// import lib.random
// → Merges lib.math and lib.random into single "lib" module
```

---

## Dependencies on Other Components

### Upstream (Inputs)
- **`ProjectConfig`**: Defines what to compile (source files, references, namespaces)
- **`ModuleRegistry`**: Resolves imports to external .NET assemblies (optional)

### Internal (Used During Compilation)
- **`Lexer`**: Tokenizes source code (Phase 1)
- **`Parser`**: Builds AST from tokens (Phase 1)
- **`NameResolver`**: Collects type declarations (Phase 3)
- **`ImportResolver`**: Resolves import statements to modules (Phase 4)
- **`TypeChecker`**: Semantic analysis (Phase 5)
- **`RoslynEmitter`**: Generates C# code (Phase 6)
- **`AssemblyCompiler`**: Compiles C# to .NET assembly (Phase 7)

### Downstream (Outputs)
- **`ProjectCompilationResult`**: Contains success/failure, assembly path, diagnostics, and introspection data (`ProjectModel`, `DependencyGraph`)

---

## Debugging Tips

### 1. **Enable Debug Logging**

Per-file metrics are logged at `Debug` level:

```csharp
if (_logger.IsEnabled(CompilerLogLevel.Debug))
{
    _logger.LogDebug($"Parsed {Path.GetFileName(sourceFile)}: {fileMetrics.TotalDuration.TotalMilliseconds:F2} ms");
}
```

**How to use**:
```bash
# Set logger level to Debug when creating Compiler
var logger = new ConsoleLogger(CompilerLogLevel.Debug);
var compiler = new Compiler(logger: logger);
```

### 2. **Inspect CompilationUnits**

After compilation, examine `ProjectModel`:

```csharp
var result = projectCompiler.Compile(config);
foreach (var unit in result.ProjectModel.Units.Values)
{
    Console.WriteLine($"{unit.FilePath}: Phase={unit.Phase}, Errors={unit.Diagnostics.ErrorCount}");

    // Inspect generated C#
    if (unit.GeneratedCSharp != null)
    {
        Console.WriteLine(unit.GeneratedCSharp);
    }
}
```

### 3. **Visualize Dependency Graph**

```csharp
var graph = result.DependencyGraph;
var buildOrder = graph.GetBuildOrder();
Console.WriteLine("Build order: " + string.Join(" → ", buildOrder.Select(Path.GetFileName)));

// Check for circular dependencies
var cycles = graph.DetectCycles();
if (cycles.Any())
{
    foreach (var cycle in cycles)
    {
        Console.WriteLine("Cycle: " + string.Join(" → ", cycle));
    }
}
```

### 4. **Common Failure Points**

| Phase | Common Issue | How to Debug |
|-------|-------------|--------------|
| **Phase 1** | `ParserError` | Check `unit.Diagnostics` for syntax errors; inspect `unit.Tokens` |
| **Phase 3** | Cross-file inheritance fails | Verify `NameResolver.ResolveInheritance()` is called AFTER all files' declarations are collected |
| **Phase 4** | `ImportError` "module not found" | Check `ImportResolver.Errors`; verify `ModulePaths` in config |
| **Phase 4** | Circular dependencies | Inspect `DependencyGraph.DetectCycles()` output |
| **Phase 5** | `TypeError` in cross-file reference | Ensure dependency order is correct (`GetBuildOrder()`); check if imported type was actually exported |
| **Phase 7** | C# compilation errors | Inspect `generatedCSharp` dictionary; look for malformed C# syntax |

### 5. **Use CLI Emit Commands**

```bash
# See generated C# for a specific file
dotnet run --project src/Sharpy.Cli -- emit csharp myproject/src/utils.spy

# See AST structure
dotnet run --project src/Sharpy.Cli -- emit ast myproject/src/utils.spy
```

---

## Contribution Guidelines

### When You Might Modify This File

1. **Adding New Compilation Phases**:
   - Example: Add "Phase 3.5: Constant Folding"
   - Insert between existing phases in `Compile()` method
   - Update `CompilationPhase` enum in `CompilationUnit.cs`

2. **Changing Import Resolution**:
   - Modify `ResolveImports()` method
   - May need to update `ImportResolver` class (separate file)
   - Test with circular dependencies and cross-file type references

3. **Optimizing Build Performance**:
   - Investigate `GetParallelizableGroups()` for parallel type-checking
   - Currently sequential—could parallelize independent files
   - Requires thread-safe `SymbolTable` modifications

4. **Incremental Compilation Support**:
   - Use `CompilationUnit.ContentHash` to detect stale files
   - Use `DependencyGraph.GetAffectedFiles()` to minimize recompilation
   - Cache type-checked units between builds

### Critical Rules

- **Never break two-phase name resolution**: Types must be declared (Phase 3a) before inheritance is resolved (Phase 3b)
- **Preserve immutable AST**: Use `SemanticBinding` for annotations, never mutate AST nodes
- **Maintain dependency order**: Semantic analysis MUST use `GetBuildOrder()` to avoid "symbol not found" errors
- **Handle errors gracefully**: Accumulate errors across files; don't stop at first failure

### Testing

When modifying `ProjectCompiler`, test with:

```bash
# Multi-file test fixtures
dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"

# Specific cross-file scenarios
dotnet test --filter "DisplayName~cross_module_inheritance"
dotnet test --filter "DisplayName~circular_import"
```

---

## Cross-References

### Related Documentation
- **[Compiler.md](../Compiler.md)**: High-level single-file compiler (delegates to `ProjectCompiler` for projects)
- **[ProjectModel.md](../Model/ProjectModel.md)**: Central data structure holding all `CompilationUnit` instances
- **[CompilationUnit.md](../Model/CompilationUnit.md)**: Per-file compilation state
- **[DependencyGraph.md](./DependencyGraph.md)**: Import dependency analysis and build ordering
- **[DependencyGraphBuilder.md](./DependencyGraphBuilder.md)**: Builder for constructing dependency graphs
- **[AssemblyCompiler.md](../AssemblyCompiler.md)**: Roslyn-based C# → .NET assembly compiler

### Related Source Files
- `src/Sharpy.Compiler/Compiler.cs`: Entry point that calls `ProjectCompiler`
- `src/Sharpy.Compiler/Model/ProjectModel.cs`: Container for all `CompilationUnit` instances
- `src/Sharpy.Compiler/Model/CompilationUnit.cs`: Per-file state machine
- `src/Sharpy.Compiler/ProjectConfig.cs`: .spyproj file parser and configuration
- `src/Sharpy.Compiler/Project/DependencyGraph.cs`: Build order and cycle detection
- `src/Sharpy.Compiler/Project/AssemblyCompiler.cs`: Roslyn-based C# → .NET assembly compiler

---

## Summary for Newcomers

**TL;DR**: `ProjectCompiler` is the "build system" for Sharpy projects. It:

1. Parses all `.spy` files in parallel (conceptually—currently sequential)
2. Builds a shared symbol table so types can reference each other across files
3. Resolves imports and detects circular dependencies
4. Type-checks files in dependency order (dependencies first)
5. Generates C# code for each file
6. Compiles all C# into a single .NET assembly

**Mental Model**: Think of it like MSBuild for Sharpy—it's responsible for the whole-project view, while individual compiler components (Lexer, Parser, TypeChecker) operate on single files.

**Most Important Insight**: The two-phase name resolution (Phase 3a + 3b) is critical for cross-file type references. Without it, you'd hit "type not found" errors when file B imports a type from file A that hasn't been processed yet.
