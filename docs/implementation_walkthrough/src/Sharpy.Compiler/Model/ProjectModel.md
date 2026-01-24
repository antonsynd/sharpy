# Walkthrough: ProjectModel.cs

**Source File**: `src/Sharpy.Compiler/Model/ProjectModel.cs`

---

## 1. Overview

`ProjectModel` is the **central orchestrator** for multi-file Sharpy compilation. It serves as a container and coordinator for all `CompilationUnit` instances (individual source files) in a project, managing their relationships, dependencies, and compilation lifecycle.

**Role in the Compiler Pipeline:**

```
Source Files (.spy) 
    ↓
ProjectModel.CreateUnit() → CompilationUnit instances
    ↓
ProjectModel tracks: Dependencies, Build Order, Global State
    ↓
Feeds units to AssemblyCompiler in correct order
    ↓
C# Generation → .NET IL
```

**Key Responsibilities:**
- **Multi-file management**: Aggregates all source files as `CompilationUnit` objects
- **Dependency tracking**: Integrates with `DependencyGraph` to determine build order
- **Global state coordination**: Maintains shared symbol tables and semantic info across files
- **Diagnostics aggregation**: Collects errors/warnings from all units
- **Incremental compilation**: Identifies which files need recompilation when sources change

---

## 2. Class Structure

### Core Properties

```csharp
public class ProjectModel
{
    // Configuration
    public ProjectConfig Config { get; }
    
    // Compilation units (keyed by normalized file path)
    private readonly Dictionary<string, CompilationUnit> _units;
    public IReadOnlyDictionary<string, CompilationUnit> Units => _units;
    
    // Global semantic state (null until semantic analysis)
    public SymbolTable? GlobalSymbols { get; internal set; }
    public SemanticInfo? SemanticInfo { get; internal set; }
    public SemanticBinding? SemanticBinding { get; internal set; }
    
    // Dependency tracking (null until import resolution)
    public DependencyGraph? DependencyGraph { get; internal set; }
    
    // Diagnostics
    public DiagnosticBag GlobalDiagnostics { get; }
}
```

### State Evolution

The `ProjectModel` evolves through compilation stages:

| Stage | What Gets Populated |
|-------|---------------------|
| **Initialization** | `Config`, `_units`, `GlobalDiagnostics` |
| **Unit Creation** | `_units` dictionary filled with `CompilationUnit` instances |
| **Import Resolution** | `DependencyGraph` constructed from import statements |
| **Name Resolution** | `GlobalSymbols` created with all module-level declarations |
| **Semantic Analysis** | `SemanticInfo` and `SemanticBinding` populated with types |

---

## 3. Key Methods

### 3.1 Construction and Unit Management

#### `ProjectModel(ProjectConfig config)`

**Purpose**: Initialize a new project with configuration.

```csharp
public ProjectModel(ProjectConfig config)
{
    Config = config ?? throw new ArgumentNullException(nameof(config));
    _units = new Dictionary<string, CompilationUnit>(StringComparer.OrdinalIgnoreCase);
    GlobalDiagnostics = new DiagnosticBag();
}
```

**Key Details:**
- Uses **case-insensitive** string comparison for file paths (except on Linux)
- Creates an empty diagnostics bag for project-level errors
- Configuration is immutable after construction

---

#### `CreateUnit(string filePath, string modulePath, string sourceText)`

**Purpose**: Factory method to create and register a compilation unit.

```csharp
public CompilationUnit CreateUnit(string filePath, string modulePath, string sourceText)
{
    var unit = new CompilationUnit(filePath, modulePath, sourceText);
    AddUnit(unit);
    return unit;
}
```

**Parameters:**
- `filePath`: Absolute path to `.spy` file (e.g., `/project/src/main.spy`)
- `modulePath`: Python-style dotted module path (e.g., `mypackage.main`)
- `sourceText`: Raw source code content

**Usage Pattern:**
```csharp
var project = new ProjectModel(config);
project.CreateUnit("/project/src/utils/math.spy", "utils.math", sourceCode);
```

**Important**: This invalidates the cached build order since dependencies may change.

---

#### `AddUnit(CompilationUnit unit)`

**Purpose**: Register an already-created compilation unit.

```csharp
public void AddUnit(CompilationUnit unit)
{
    ArgumentNullException.ThrowIfNull(unit);
    var normalized = NormalizePath(unit.FilePath);
    if (!_units.TryAdd(normalized, unit))
    {
        throw new ArgumentException($"A compilation unit for '{unit.FilePath}' already exists.", nameof(unit));
    }
    InvalidateBuildOrder();
}
```

**Key Behaviors:**
- **Path normalization**: Converts `\` to `/`, lowercases on Windows/macOS
- **Duplicate detection**: Throws if unit for this file already exists
- **Cache invalidation**: Clears `_cachedBuildOrder` to force recalculation

**Why Normalization?** Ensures consistent lookups regardless of path separators or casing:
```csharp
// These should refer to the same file on Windows
"C:\Project\Main.spy"
"c:/project/main.spy"
```

---

#### `GetUnit(string filePath)`

**Purpose**: Retrieve a compilation unit by file path.

```csharp
public CompilationUnit? GetUnit(string filePath)
{
    var normalized = NormalizePath(filePath);
    return _units.TryGetValue(normalized, out var unit) ? unit : null;
}
```

**Returns**: `CompilationUnit` if found, `null` otherwise.

**Usage Example:**
```csharp
var mainUnit = project.GetUnit("/project/src/main.spy");
if (mainUnit?.HasErrors == true)
{
    Console.WriteLine("Main module has errors!");
}
```

---

### 3.2 Dependency and Build Order

#### `GetBuildOrder()`

**Purpose**: Get the dependency-sorted order for compilation.

```csharp
public IReadOnlyList<string>? GetBuildOrder()
{
    if (DependencyGraph == null)
        return null;

    if (_cachedBuildOrder == null)
    {
        _cachedBuildOrder = DependencyGraph.GetBuildOrder();
    }
    return _cachedBuildOrder;
}
```

**Why This Matters:**
Sharpy files must be compiled in dependency order. If `main.spy` imports `utils.spy`, then `utils.spy` must be compiled first so its symbols are available.

**Example Dependency Chain:**
```
utils/math.spy  (no dependencies)
    ↓
utils/geometry.spy  (imports math)
    ↓
main.spy  (imports geometry)
```

**Build Order:** `["utils/math.spy", "utils/geometry.spy", "main.spy"]`

**Caching Strategy:**
- First call delegates to `DependencyGraph.GetBuildOrder()`
- Result cached in `_cachedBuildOrder`
- Cache invalidated when units are added/removed via `InvalidateBuildOrder()`

---

#### `GetUnitsInBuildOrder()`

**Purpose**: Get `CompilationUnit` objects in dependency order.

```csharp
public IEnumerable<CompilationUnit> GetUnitsInBuildOrder()
{
    var buildOrder = GetBuildOrder();
    if (buildOrder == null)
    {
        return _units.Values;  // Fallback: arbitrary order
    }

    return buildOrder
        .Select(path => GetUnit(path))
        .Where(unit => unit != null)
        .Cast<CompilationUnit>();
}
```

**Fallback Behavior**: If `DependencyGraph` is not yet available, returns units in arbitrary order (safe for single-file compilation or when dependencies don't matter yet).

**Usage in Compilation:**
```csharp
foreach (var unit in project.GetUnitsInBuildOrder())
{
    compiler.CompileUnit(unit);
}
```

---

#### `GetParallelizableGroups()`

**Purpose**: Identify independent groups that can be compiled concurrently.

```csharp
public IReadOnlyList<IReadOnlyList<CompilationUnit>>? GetParallelizableGroups()
{
    if (DependencyGraph == null)
        return null;

    var groups = DependencyGraph.GetParallelizableGroups();
    return groups.Select(group =>
        group.Select(path => GetUnit(path))
             .Where(unit => unit != null)
             .Cast<CompilationUnit>()
             .ToList() as IReadOnlyList<CompilationUnit>
    ).ToList();
}
```

**Parallelization Strategy:**

```
Group 1 (no dependencies):
  - utils/math.spy
  - utils/string.spy
  ↓ (can compile in parallel)
Group 2 (depends on Group 1):
  - utils/geometry.spy  (imports math)
  - utils/parser.spy    (imports string)
  ↓ (can compile in parallel)
Group 3 (depends on Group 2):
  - main.spy (imports geometry, parser)
```

**Future Optimization**: Currently sequential, but this enables future parallel compilation within groups.

---

### 3.3 Phase Tracking

#### `GetUnitsAtPhase(CompilationPhase phase)`

**Purpose**: Filter units by compilation stage.

```csharp
public IReadOnlyList<CompilationUnit> GetUnitsAtPhase(CompilationPhase phase)
{
    return _units.Values.Where(u => u.Phase == phase).ToList();
}
```

**CompilationPhase Enum** (from `CompilationUnit.cs`):
- `Created` → Unit initialized but not processed
- `Lexed` → Tokenization complete
- `Parsed` → AST generated
- `NamesResolved` → Symbols declared
- `TypeChecked` → Semantic analysis complete
- `CodeGenerated` → C# emitted
- `Failed` → Compilation error occurred

**Usage Example:**
```csharp
// Check if any units are still parsing
var parsingUnits = project.GetUnitsAtPhase(CompilationPhase.Parsed);
if (parsingUnits.Count > 0)
{
    Console.WriteLine($"{parsingUnits.Count} units still in parsing phase");
}
```

---

#### `GetFailedUnits()` and `GetSuccessfulUnits()`

**Purpose**: Quickly identify compilation outcomes.

```csharp
public IReadOnlyList<CompilationUnit> GetFailedUnits()
    => _units.Values.Where(u => u.Phase == CompilationPhase.Failed).ToList();

public IReadOnlyList<CompilationUnit> GetSuccessfulUnits()
    => _units.Values.Where(u => u.Phase == CompilationPhase.CodeGenerated).ToList();
```

**Typical Post-Compilation Check:**
```csharp
var failed = project.GetFailedUnits();
if (failed.Count > 0)
{
    foreach (var unit in failed)
    {
        Console.WriteLine($"Failed: {unit.FilePath}");
        foreach (var error in unit.Diagnostics.GetErrors())
        {
            Console.WriteLine($"  {error.Message}");
        }
    }
    return false;
}
```

---

### 3.4 Diagnostics

#### `GetAllDiagnostics()`

**Purpose**: Aggregate diagnostics from all sources.

```csharp
public IReadOnlyList<CompilerDiagnostic> GetAllDiagnostics()
{
    var all = new List<CompilerDiagnostic>();
    all.AddRange(GlobalDiagnostics.GetAll());
    foreach (var unit in _units.Values)
    {
        all.AddRange(unit.Diagnostics.GetAll());
    }
    return all;
}
```

**Two Types of Diagnostics:**
1. **Global Diagnostics**: Project-level issues (e.g., circular dependencies, missing configuration)
2. **Unit Diagnostics**: File-specific issues (e.g., syntax errors, type mismatches)

---

#### `GetAllErrorMessages()`

**Purpose**: Format all errors for user-friendly reporting.

```csharp
public IReadOnlyList<string> GetAllErrorMessages()
{
    var messages = new List<string>();

    // Add global errors (no file context)
    foreach (var error in GlobalDiagnostics.GetErrors())
    {
        if (!string.IsNullOrEmpty(error.FilePath))
        {
            messages.Add($"{error.FilePath}({error.Line},{error.Column}): error: {error.Message}");
        }
        else
        {
            messages.Add($"error: {error.Message}");
        }
    }

    // Add per-file errors
    foreach (var unit in _units.Values)
    {
        foreach (var error in unit.Diagnostics.GetErrors())
        {
            var filePath = error.FilePath ?? unit.FilePath;
            messages.Add($"{filePath}({error.Line},{error.Column}): error: {error.Message}");
        }
    }

    return messages;
}
```

**Output Format** (MSBuild-compatible):
```
/project/src/main.spy(12,5): error: Undefined variable 'x'
/project/src/utils.spy(8,10): error: Type mismatch: expected 'int', got 'str'
error: Circular dependency detected: main -> utils -> main
```

**Why MSBuild Format?** IDEs can parse this format and jump to error locations.

---

#### `HasErrors` and `TotalErrorCount`

**Purpose**: Quick error status checks.

```csharp
public bool HasErrors =>
    GlobalDiagnostics.HasErrors || _units.Values.Any(u => u.HasErrors);

public int TotalErrorCount =>
    GlobalDiagnostics.ErrorCount + _units.Values.Sum(u => u.Diagnostics.ErrorCount);
```

**Usage:**
```csharp
if (project.HasErrors)
{
    Console.WriteLine($"Compilation failed with {project.TotalErrorCount} errors");
    Console.WriteLine(string.Join("\n", project.GetAllErrorMessages()));
    return 1;
}
```

---

### 3.5 Incremental Compilation

#### `GetAffectedFiles(IEnumerable<string> changedFiles)`

**Purpose**: Determine which files need recompilation after changes.

```csharp
public ImmutableHashSet<string>? GetAffectedFiles(IEnumerable<string> changedFiles)
{
    return DependencyGraph?.GetAffectedFiles(changedFiles);
}
```

**How It Works:**

If `utils/math.spy` changes, we need to recompile:
1. `utils/math.spy` itself
2. `utils/geometry.spy` (imports math)
3. `main.spy` (imports geometry, which depends on math)

**Algorithm** (in `DependencyGraph`):
- Start with changed files
- Recursively add all files that depend on them (transitive closure)
- Return the complete set of affected files

**Example:**
```csharp
var changed = new[] { "/project/src/utils/math.spy" };
var affected = project.GetAffectedFiles(changed);
// Returns: [math.spy, geometry.spy, main.spy]
```

---

#### `IsFileStale(string filePath, string? cachedHash)`

**Purpose**: Check if a file's content has changed.

```csharp
public bool IsFileStale(string filePath, string? cachedHash)
{
    var unit = GetUnit(filePath);
    return unit?.IsStale(cachedHash) ?? true;
}
```

**How Hashing Works:**
- Each `CompilationUnit` stores a SHA-256 `ContentHash` of its source
- Compare with cached hash from previous compilation
- If hashes differ (or unit not found), file is stale

**Incremental Build Flow:**
```csharp
var cache = LoadCompilationCache();
var staleFiles = project.Units.Keys
    .Where(path => project.IsFileStale(path, cache.GetHash(path)))
    .ToList();

var affected = project.GetAffectedFiles(staleFiles);
// Only recompile affected files
```

---

### 3.6 Helper Methods

#### `NormalizePath(string path)`

**Purpose**: Ensure consistent path representation across platforms.

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

**Platform Differences:**
- **Windows/macOS**: File systems are case-insensitive → normalize to lowercase
- **Linux**: File systems are case-sensitive → preserve original casing

**Example Normalization:**
```csharp
// Windows:
NormalizePath("C:\\Project\\Main.spy") → "c:/project/main.spy"

// Linux:
NormalizePath("/Project/Main.spy") → "/Project/Main.spy"  // Case preserved
```

---

#### `InvalidateBuildOrder()`

**Purpose**: Clear cached build order when project structure changes.

```csharp
private void InvalidateBuildOrder()
{
    _cachedBuildOrder = null;
}
```

**When Called:**
- `AddUnit()` → New file added, dependencies changed
- Potentially when imports are modified (handled by `DependencyGraph`)

**Why Cache?** Topological sort is O(V + E), but results don't change unless units/dependencies change.

---

## 4. Dependencies

### Internal Sharpy Dependencies

**`Sharpy.Compiler.Diagnostics`**
- `DiagnosticBag`: Thread-safe collection of errors/warnings
- `CompilerDiagnostic`: Individual diagnostic messages with location

**`Sharpy.Compiler.Project`**
- `ProjectConfig`: Configuration (root namespace, output type, etc.)
- `DependencyGraph`: Topological sort and affected file calculation

**`Sharpy.Compiler.Semantic`**
- `SymbolTable`: Global registry of symbols (classes, functions, variables)
- `SemanticInfo`: Type annotations for AST nodes
- `SemanticBinding`: Maps AST nodes to their semantic information

### Related Model Classes

**`CompilationUnit`** (sibling in `Model/`)
- Represents a single source file
- Contains tokens, AST, diagnostics, generated C#
- Tracks compilation phase (Created → Lexed → Parsed → ... → CodeGenerated)

---

## 5. Patterns and Design Decisions

### 5.1 Immutable AST, Mutable Project State

**Design Philosophy:**
- AST nodes (from Parser) are **immutable records**
- `ProjectModel` and `CompilationUnit` are **mutable classes**
- Semantic information stored separately in `SemanticBinding` (not in AST)

**Rationale:**
- Immutable ASTs are thread-safe and cacheable
- Mutable project state allows incremental compilation updates
- Separation of concerns: parsing vs. semantic analysis

---

### 5.2 Progressive Population Pattern

Properties start as `null` and get populated as compilation advances:

```csharp
public SymbolTable? GlobalSymbols { get; internal set; }        // Null until name resolution
public SemanticInfo? SemanticInfo { get; internal set; }        // Null until semantic analysis
public DependencyGraph? DependencyGraph { get; internal set; }  // Null until import resolution
```

**Why `internal set`?**
- Only compiler internals (`AssemblyCompiler`, `NameResolver`, etc.) populate these
- External consumers get read-only access via public getter
- Prevents accidental corruption of compiler state

---

### 5.3 Case-Insensitive Path Storage

```csharp
_units = new Dictionary<string, CompilationUnit>(StringComparer.OrdinalIgnoreCase);
```

**Cross-Platform Consistency:**
- Windows: `C:\Foo\Bar.spy` and `c:\foo\bar.spy` refer to the same file
- macOS: Similar case-insensitivity
- Linux: Case-sensitive, but normalization still converts separators

**Why Not Use Platform-Specific Logic?**
Path normalization (`NormalizePath`) handles platform differences explicitly for clarity.

---

### 5.4 Lazy Build Order Computation

Build order is computed on first access and cached:

```csharp
if (_cachedBuildOrder == null)
{
    _cachedBuildOrder = DependencyGraph.GetBuildOrder();
}
```

**Performance Tradeoff:**
- **Cost of Sort**: O(V + E) for topological sort (V = files, E = imports)
- **Frequency**: Rarely changes during compilation
- **Solution**: Compute once, cache until invalidated

---

## 6. Debugging Tips

### 6.1 Inspecting Project State

**At any point during compilation:**
```csharp
// Check unit count
Console.WriteLine($"Project has {project.UnitCount} files");

// Check compilation progress
foreach (var phase in Enum.GetValues<CompilationPhase>())
{
    var count = project.GetUnitsAtPhase(phase).Count;
    Console.WriteLine($"{phase}: {count} units");
}

// Check dependencies
if (project.DependencyGraph != null)
{
    var order = project.GetBuildOrder();
    Console.WriteLine($"Build order: {string.Join(" -> ", order)}");
}
```

---

### 6.2 Diagnosing Build Order Issues

**Problem**: Files compile in wrong order, causing "undefined symbol" errors.

**Debug Steps:**
```csharp
// 1. Check if dependency graph exists
if (project.DependencyGraph == null)
{
    Console.WriteLine("ERROR: DependencyGraph not built yet!");
    return;
}

// 2. Print build order
var order = project.GetBuildOrder();
foreach (var path in order)
{
    var unit = project.GetUnit(path);
    Console.WriteLine($"{path} depends on: {string.Join(", ", unit.DirectDependencies)}");
}

// 3. Check for circular dependencies
var groups = project.GetParallelizableGroups();
if (groups == null || groups.Count == 0)
{
    Console.WriteLine("ERROR: Circular dependency detected!");
}
```

---

### 6.3 Tracking Down Errors

**Problem**: Compilation fails but error messages are unclear.

**Solution:**
```csharp
// Get all diagnostics with full context
var errors = project.GetAllDiagnostics()
    .Where(d => d.Severity == DiagnosticSeverity.Error);

foreach (var error in errors)
{
    var unit = project.GetUnit(error.FilePath);
    Console.WriteLine($"Error in {unit?.ModulePath ?? error.FilePath}:");
    Console.WriteLine($"  Phase: {unit?.Phase}");
    Console.WriteLine($"  Location: Line {error.Line}, Column {error.Column}");
    Console.WriteLine($"  Message: {error.Message}");
    
    // Print source context if available
    if (unit != null && unit.SourceText != null)
    {
        var lines = unit.SourceText.Split('\n');
        if (error.Line > 0 && error.Line <= lines.Length)
        {
            Console.WriteLine($"  Source: {lines[error.Line - 1]}");
        }
    }
}
```

---

### 6.4 Debugging Path Normalization Issues

**Problem**: `GetUnit()` returns null even though file exists.

**Debug:**
```csharp
// Check what paths are registered
Console.WriteLine("Registered paths:");
foreach (var path in project.Units.Keys)
{
    Console.WriteLine($"  '{path}'");
}

// Check normalization
var lookupPath = "/Project/Main.spy";
var normalized = ProjectModel.NormalizePath(lookupPath);
Console.WriteLine($"Looking up: '{lookupPath}'");
Console.WriteLine($"Normalized: '{normalized}'");
Console.WriteLine($"Found: {project.GetUnit(lookupPath) != null}");
```

---

## 7. Contribution Guidelines

### 7.1 When to Modify ProjectModel

**Add properties** for new project-wide state:
```csharp
// Example: Add support for type stubs
public Dictionary<string, TypeStubInfo>? TypeStubs { get; internal set; }
```

**Add methods** for new query patterns:
```csharp
// Example: Get all units that import a specific module
public IReadOnlyList<CompilationUnit> GetUnitsImporting(string modulePath)
{
    return _units.Values
        .Where(u => u.Imports.Any(i => i.ModulePath == modulePath))
        .ToList();
}
```

---

### 7.2 What NOT to Change

**❌ Don't make AST mutable:**
```csharp
// BAD: Don't add mutable state to AST nodes
public class Module {
    public List<Symbol> ResolvedSymbols { get; set; }  // ❌ Violates immutability
}
```

**✅ Use SemanticBinding instead:**
```csharp
// GOOD: Store semantic info separately
project.SemanticBinding?.SetSymbol(moduleNode, symbolTable);
```

---

### 7.3 Adding New Compilation Phases

If adding a new phase (e.g., optimization pass):

1. **Add to `CompilationPhase` enum** in `CompilationUnit.cs`:
```csharp
public enum CompilationPhase
{
    // ... existing phases ...
    TypeChecked,
    Optimized,      // NEW
    CodeGenerated,
    Failed
}
```

2. **Add query method** in `ProjectModel.cs`:
```csharp
public IReadOnlyList<CompilationUnit> GetOptimizedUnits()
    => GetUnitsAtPhase(CompilationPhase.Optimized);
```

3. **Update `AssemblyCompiler`** to advance units to new phase.

---

### 7.4 Testing Changes

**Unit tests for ProjectModel** (`Sharpy.Compiler.Tests/Model/ProjectModelTests.cs`):

```csharp
[Fact]
public void AddUnit_DuplicatePath_ThrowsArgumentException()
{
    var project = new ProjectModel(new ProjectConfig());
    var unit1 = new CompilationUnit("/test.spy", "test", "code");
    var unit2 = new CompilationUnit("/test.spy", "test", "code");
    
    project.AddUnit(unit1);
    
    Assert.Throws<ArgumentException>(() => project.AddUnit(unit2));
}

[Fact]
public void GetBuildOrder_NoDependencyGraph_ReturnsNull()
{
    var project = new ProjectModel(new ProjectConfig());
    
    Assert.Null(project.GetBuildOrder());
}
```

---

### 7.5 Common Pitfalls

**Pitfall 1: Forgetting to invalidate cache**
```csharp
// BAD: Modifying state without invalidating cache
public void RemoveUnit(string filePath)
{
    _units.Remove(filePath);
    // ❌ Forgot to call InvalidateBuildOrder()!
}

// GOOD:
public void RemoveUnit(string filePath)
{
    _units.Remove(filePath);
    InvalidateBuildOrder();  // ✅
}
```

**Pitfall 2: Not handling null states**
```csharp
// BAD: Assuming properties are populated
var buildOrder = project.GetBuildOrder();
foreach (var path in buildOrder)  // ❌ NullReferenceException if DependencyGraph not built
{
    // ...
}

// GOOD:
var buildOrder = project.GetBuildOrder();
if (buildOrder == null)
{
    Console.WriteLine("Dependency graph not available yet");
    return;
}
foreach (var path in buildOrder)
{
    // ...
}
```

**Pitfall 3: Path normalization inconsistency**
```csharp
// BAD: Bypassing normalization
_units.Add(filePath, unit);  // ❌ Might not match lookups

// GOOD: Always use methods that normalize
AddUnit(unit);  // ✅ Normalizes internally
```

---

## 8. Cross-References

### Related Documentation

- **[CompilationUnit.md](CompilationUnit.md)** - Individual source file representation (sibling class)
- **[../Project/DependencyGraph.md](../Project/DependencyGraph.md)** - Dependency tracking and build order
- **[../Diagnostics/DiagnosticBag.md](../Diagnostics/DiagnosticBag.md)** - Error/warning collection
- **[../Semantic/SymbolTable.md](../Semantic/SymbolTable.md)** - Global symbol registry
- **[../AssemblyCompiler.md](../AssemblyCompiler.md)** - Multi-file compilation orchestrator

### Usage Context

**Where ProjectModel is Created:**
- `AssemblyCompiler.Compile()` - Main entry point for multi-file compilation
- `ProjectCompiler.CompileProject()` - CLI project compilation

**Where ProjectModel is Consumed:**
- `AssemblyCompiler` - Iterates units in build order
- `NameResolver` - Populates `GlobalSymbols`
- `TypeChecker` - Accesses `SemanticInfo` and `SemanticBinding`
- `RoslynEmitter` - Generates C# for successful units

---

## Summary

`ProjectModel` is the **central nervous system** of multi-file Sharpy compilation. It:

✅ **Aggregates** all source files as `CompilationUnit` objects  
✅ **Coordinates** dependency-based build ordering  
✅ **Maintains** global semantic state (symbols, types)  
✅ **Collects** diagnostics from all compilation stages  
✅ **Enables** incremental compilation via change tracking  

**Mental Model:**
- Think of `ProjectModel` as a **database** of compilation state
- Each `CompilationUnit` is a **row** (one source file)
- Queries like `GetBuildOrder()` and `GetFailedUnits()` provide **views**
- Semantic state (`GlobalSymbols`, etc.) are **indexes** built progressively

**For Newcomers:**
Start by exploring `CompilationUnit.md` to understand individual file representation, then return here to see how `ProjectModel` orchestrates the big picture.
