# Task List: CompilationUnit Model Implementation (Rec #1)

**Goal:** Create a unified `CompilationUnit` and `ProjectModel` abstraction to replace the scattered state management in `ProjectCompiler`.

**Estimated Total Effort:** 8-12 hours (across multiple sessions)

**Key Design Decisions:**
- **Two-way door**: Implementation is additive; existing APIs remain functional
- **Future-proof**: Designed for incremental compilation, parallel builds, LSP integration
- **Testable**: Each phase has tests that validate without breaking existing tests

---

## Prerequisites

Before starting, ensure all existing tests pass:

```bash
cd /Users/anton/Documents/github/sharpy
dotnet test src/Sharpy.Compiler.Tests/Sharpy.Compiler.Tests.csproj --filter "Category!=Performance"
```

If tests fail, do not proceed until they pass.

---

## Phase 1: Create CompilationUnit Class (Additive)

**Goal:** Create the `CompilationUnit` class that represents a single Sharpy source file and all its compilation artifacts.

### Task 1.1: Create the CompilationUnit.cs file

- [x] Create `/src/Sharpy.Compiler/Model/CompilationUnit.cs`

```csharp
using System.Collections.Immutable;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Model;

/// <summary>
/// Represents a single Sharpy source file and all its compilation artifacts.
/// This is the fundamental unit of compilation.
/// </summary>
/// <remarks>
/// <para>
/// CompilationUnit is designed for:
/// - **Incremental compilation**: Track content hash for staleness detection
/// - **Parallel compilation**: Immutable after construction (except GeneratedCSharp)
/// - **LSP support**: Store tokens for hover/completion, diagnostics with file context
/// - **Debugging**: Preserve source mapping information
/// </para>
/// </remarks>
public class CompilationUnit
{
    /// <summary>
    /// Creates a new CompilationUnit for a source file.
    /// </summary>
    /// <param name="filePath">Full path to the source file.</param>
    /// <param name="modulePath">Dotted module path (e.g., "mypackage.helpers").</param>
    /// <param name="sourceText">The raw source code text.</param>
    public CompilationUnit(string filePath, string modulePath, string sourceText)
    {
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        ModulePath = modulePath ?? throw new ArgumentNullException(nameof(modulePath));
        SourceText = sourceText ?? throw new ArgumentNullException(nameof(sourceText));
        ContentHash = ComputeHash(sourceText);
        Diagnostics = new DiagnosticBag();
    }

    #region Source Information

    /// <summary>
    /// Full path to the source file.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Dotted module path derived from the file's location relative to project root.
    /// Example: "mypackage.helpers" for src/mypackage/helpers.spy
    /// </summary>
    public string ModulePath { get; }

    /// <summary>
    /// The raw source code text.
    /// </summary>
    public string SourceText { get; }

    /// <summary>
    /// SHA-256 hash of source content for change detection in incremental compilation.
    /// </summary>
    public string ContentHash { get; }

    #endregion

    #region Parsing Artifacts

    /// <summary>
    /// The tokenized representation of the source file.
    /// Null until lexical analysis completes.
    /// </summary>
    /// <remarks>
    /// Stored for future LSP support (hover, completion, syntax highlighting).
    /// </remarks>
    public IReadOnlyList<Token>? Tokens { get; internal set; }

    /// <summary>
    /// The abstract syntax tree for this file.
    /// Null until parsing completes.
    /// </summary>
    public Module? Ast { get; internal set; }

    #endregion

    #region Semantic Artifacts

    /// <summary>
    /// The module-level scope containing all declarations from this file.
    /// Null until name resolution completes.
    /// </summary>
    public Scope? ModuleScope { get; internal set; }

    /// <summary>
    /// Type symbols declared in this file (classes, structs, interfaces, enums).
    /// Empty until name resolution completes.
    /// </summary>
    public IReadOnlyList<TypeSymbol> DeclaredTypes { get; internal set; } = Array.Empty<TypeSymbol>();

    /// <summary>
    /// Function symbols declared at module level in this file.
    /// Empty until name resolution completes.
    /// </summary>
    public IReadOnlyList<FunctionSymbol> DeclaredFunctions { get; internal set; } = Array.Empty<FunctionSymbol>();

    /// <summary>
    /// Import statements in this file.
    /// Empty until parsing completes.
    /// </summary>
    public IReadOnlyList<ImportStatement> Imports { get; internal set; } = Array.Empty<ImportStatement>();

    /// <summary>
    /// FromImport statements in this file.
    /// Empty until parsing completes.
    /// </summary>
    public IReadOnlyList<FromImportStatement> FromImports { get; internal set; } = Array.Empty<FromImportStatement>();

    #endregion

    #region Dependencies

    /// <summary>
    /// File paths that this unit directly depends on (imports).
    /// Populated during import resolution.
    /// </summary>
    public ImmutableHashSet<string> DirectDependencies { get; internal set; } = ImmutableHashSet<string>.Empty;

    #endregion

    #region Code Generation

    /// <summary>
    /// The generated C# source code for this file.
    /// Null until code generation completes.
    /// </summary>
    public string? GeneratedCSharp { get; set; }

    #endregion

    #region Diagnostics

    /// <summary>
    /// Diagnostics (errors/warnings) specific to this file.
    /// Thread-safe for potential future parallel compilation.
    /// </summary>
    public DiagnosticBag Diagnostics { get; }

    /// <summary>
    /// Indicates whether this unit has any errors.
    /// </summary>
    public bool HasErrors => Diagnostics.HasErrors;

    #endregion

    #region Compilation State

    /// <summary>
    /// The current compilation phase of this unit.
    /// </summary>
    public CompilationPhase Phase { get; internal set; } = CompilationPhase.Created;

    #endregion

    #region Helper Methods

    /// <summary>
    /// Checks if this unit's content has changed compared to a cached hash.
    /// </summary>
    /// <param name="cachedHash">The previously stored content hash.</param>
    /// <returns>True if the content has changed (is stale), false otherwise.</returns>
    public bool IsStale(string? cachedHash)
    {
        if (string.IsNullOrEmpty(cachedHash))
            return true;
        return !string.Equals(ContentHash, cachedHash, StringComparison.Ordinal);
    }

    private static string ComputeHash(string content)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var hashBytes = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hashBytes);
    }

    #endregion
}

/// <summary>
/// Represents the compilation phase of a CompilationUnit.
/// </summary>
public enum CompilationPhase
{
    /// <summary>Unit created but not yet processed.</summary>
    Created,

    /// <summary>Lexical analysis (tokenization) completed.</summary>
    Lexed,

    /// <summary>Parsing completed, AST available.</summary>
    Parsed,

    /// <summary>Name resolution completed, symbols declared.</summary>
    NamesResolved,

    /// <summary>Type checking and semantic analysis completed.</summary>
    TypeChecked,

    /// <summary>Code generation completed, C# output available.</summary>
    CodeGenerated,

    /// <summary>Compilation failed with errors.</summary>
    Failed
}
```

### Task 1.2: Create the Model directory and add to project

- [x] Create the directory `/src/Sharpy.Compiler/Model/` (if it doesn't exist)
- [x] Verify the file compiles: `dotnet build src/Sharpy.Compiler/Sharpy.Compiler.csproj`

### Task 1.3: Create unit tests for CompilationUnit

- [x] Create `/src/Sharpy.Compiler.Tests/Model/CompilationUnitTests.cs`

```csharp
using Sharpy.Compiler.Model;
using Xunit;

namespace Sharpy.Compiler.Tests.Model;

public class CompilationUnitTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_ValidInputs_SetsProperties()
    {
        var unit = new CompilationUnit(
            "/path/to/file.spy",
            "mypackage.mymodule",
            "x: int = 42");

        Assert.Equal("/path/to/file.spy", unit.FilePath);
        Assert.Equal("mypackage.mymodule", unit.ModulePath);
        Assert.Equal("x: int = 42", unit.SourceText);
        Assert.NotEmpty(unit.ContentHash);
        Assert.Equal(CompilationPhase.Created, unit.Phase);
    }

    [Fact]
    public void Constructor_NullFilePath_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CompilationUnit(null!, "module", "source"));
    }

    [Fact]
    public void Constructor_NullModulePath_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CompilationUnit("/path/file.spy", null!, "source"));
    }

    [Fact]
    public void Constructor_NullSourceText_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CompilationUnit("/path/file.spy", "module", null!));
    }

    #endregion

    #region ContentHash Tests

    [Fact]
    public void ContentHash_SameContent_SameHash()
    {
        var unit1 = new CompilationUnit("/path/a.spy", "module.a", "x = 1");
        var unit2 = new CompilationUnit("/path/b.spy", "module.b", "x = 1");

        Assert.Equal(unit1.ContentHash, unit2.ContentHash);
    }

    [Fact]
    public void ContentHash_DifferentContent_DifferentHash()
    {
        var unit1 = new CompilationUnit("/path/a.spy", "module.a", "x = 1");
        var unit2 = new CompilationUnit("/path/a.spy", "module.a", "x = 2");

        Assert.NotEqual(unit1.ContentHash, unit2.ContentHash);
    }

    [Fact]
    public void ContentHash_IsDeterministic()
    {
        var content = "def foo() -> int:\n    return 42";
        var unit1 = new CompilationUnit("/path/a.spy", "module.a", content);
        var unit2 = new CompilationUnit("/path/b.spy", "module.b", content);

        Assert.Equal(unit1.ContentHash, unit2.ContentHash);
    }

    #endregion

    #region IsStale Tests

    [Fact]
    public void IsStale_NullCachedHash_ReturnsTrue()
    {
        var unit = new CompilationUnit("/path/a.spy", "module.a", "x = 1");

        Assert.True(unit.IsStale(null));
    }

    [Fact]
    public void IsStale_EmptyCachedHash_ReturnsTrue()
    {
        var unit = new CompilationUnit("/path/a.spy", "module.a", "x = 1");

        Assert.True(unit.IsStale(""));
    }

    [Fact]
    public void IsStale_MatchingHash_ReturnsFalse()
    {
        var unit = new CompilationUnit("/path/a.spy", "module.a", "x = 1");

        Assert.False(unit.IsStale(unit.ContentHash));
    }

    [Fact]
    public void IsStale_DifferentHash_ReturnsTrue()
    {
        var unit = new CompilationUnit("/path/a.spy", "module.a", "x = 1");

        Assert.True(unit.IsStale("different_hash"));
    }

    #endregion

    #region Default State Tests

    [Fact]
    public void DefaultState_CollectionsAreEmpty()
    {
        var unit = new CompilationUnit("/path/a.spy", "module.a", "x = 1");

        Assert.Empty(unit.DeclaredTypes);
        Assert.Empty(unit.DeclaredFunctions);
        Assert.Empty(unit.Imports);
        Assert.Empty(unit.FromImports);
        Assert.Empty(unit.DirectDependencies);
    }

    [Fact]
    public void DefaultState_ArtifactsAreNull()
    {
        var unit = new CompilationUnit("/path/a.spy", "module.a", "x = 1");

        Assert.Null(unit.Tokens);
        Assert.Null(unit.Ast);
        Assert.Null(unit.ModuleScope);
        Assert.Null(unit.GeneratedCSharp);
    }

    [Fact]
    public void DefaultState_HasNoErrors()
    {
        var unit = new CompilationUnit("/path/a.spy", "module.a", "x = 1");

        Assert.False(unit.HasErrors);
    }

    #endregion

    #region Diagnostics Tests

    [Fact]
    public void Diagnostics_AddError_HasErrorsBecomesTrue()
    {
        var unit = new CompilationUnit("/path/a.spy", "module.a", "x = 1");

        unit.Diagnostics.AddError("Test error", 1, 1);

        Assert.True(unit.HasErrors);
    }

    [Fact]
    public void Diagnostics_IsThreadSafe()
    {
        var unit = new CompilationUnit("/path/a.spy", "module.a", "x = 1");

        // Simulate concurrent access
        Parallel.For(0, 100, i =>
        {
            unit.Diagnostics.AddError($"Error {i}", i, 1);
        });

        Assert.Equal(100, unit.Diagnostics.ErrorCount);
    }

    #endregion
}
```

### Task 1.4: Verify tests pass

- [x] Run the new tests:
  ```bash
  dotnet test src/Sharpy.Compiler.Tests/Sharpy.Compiler.Tests.csproj --filter "FullyQualifiedName~CompilationUnitTests"
  ```
- [x] Run all existing tests to ensure nothing broke:
  ```bash
  dotnet test src/Sharpy.Compiler.Tests/Sharpy.Compiler.Tests.csproj --filter "Category!=Performance"
  ```

### Task 1.5: Commit Phase 1

- [x] Commit changes:
  ```bash
  git add .
  git commit -m "Add CompilationUnit class for unified file representation

  - Create Model/CompilationUnit.cs with file artifacts, semantic info, diagnostics
  - Add CompilationPhase enum to track compilation progress
  - Include ContentHash for incremental compilation support
  - Add comprehensive unit tests for CompilationUnit

  Part of architecture Rec #1: CompilationUnit Model"
  ```

---

## Phase 2: Create ProjectModel Class (Additive)

**Goal:** Create the `ProjectModel` class that represents a complete Sharpy project being compiled.

### Task 2.1: Create ProjectModel.cs

- [ ] Create `/src/Sharpy.Compiler/Model/ProjectModel.cs`

```csharp
using System.Collections.Immutable;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Model;

/// <summary>
/// Represents a complete Sharpy project being compiled.
/// Aggregates all CompilationUnits and provides project-level services.
/// </summary>
/// <remarks>
/// <para>
/// ProjectModel serves as the central data structure for:
/// - **Multi-file compilation**: Manages all source files as CompilationUnits
/// - **Dependency tracking**: Integrates with DependencyGraph for build ordering
/// - **Cross-file resolution**: Global symbol table for cross-module references
/// - **Incremental compilation**: Track which files need recompilation
/// </para>
/// </remarks>
public class ProjectModel
{
    private readonly Dictionary<string, CompilationUnit> _units;
    private IReadOnlyList<string>? _cachedBuildOrder;

    /// <summary>
    /// Creates a new ProjectModel with the given configuration.
    /// </summary>
    /// <param name="config">The project configuration.</param>
    public ProjectModel(ProjectConfig config)
    {
        Config = config ?? throw new ArgumentNullException(nameof(config));
        _units = new Dictionary<string, CompilationUnit>(StringComparer.OrdinalIgnoreCase);
        GlobalDiagnostics = new DiagnosticBag();
    }

    #region Configuration

    /// <summary>
    /// The project configuration.
    /// </summary>
    public ProjectConfig Config { get; }

    #endregion

    #region Compilation Units

    /// <summary>
    /// All compilation units in the project, keyed by file path.
    /// </summary>
    public IReadOnlyDictionary<string, CompilationUnit> Units => _units;

    /// <summary>
    /// Gets a compilation unit by file path.
    /// </summary>
    /// <param name="filePath">The file path to look up.</param>
    /// <returns>The CompilationUnit, or null if not found.</returns>
    public CompilationUnit? GetUnit(string filePath)
    {
        var normalized = NormalizePath(filePath);
        return _units.TryGetValue(normalized, out var unit) ? unit : null;
    }

    /// <summary>
    /// Adds a compilation unit to the project.
    /// </summary>
    /// <param name="unit">The compilation unit to add.</param>
    /// <exception cref="ArgumentException">Thrown if a unit for this file already exists.</exception>
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

    /// <summary>
    /// Creates and adds a compilation unit for a source file.
    /// </summary>
    /// <param name="filePath">Full path to the source file.</param>
    /// <param name="modulePath">Dotted module path.</param>
    /// <param name="sourceText">The source code.</param>
    /// <returns>The created CompilationUnit.</returns>
    public CompilationUnit CreateUnit(string filePath, string modulePath, string sourceText)
    {
        var unit = new CompilationUnit(filePath, modulePath, sourceText);
        AddUnit(unit);
        return unit;
    }

    /// <summary>
    /// Gets the number of compilation units in the project.
    /// </summary>
    public int UnitCount => _units.Count;

    #endregion

    #region Global State

    /// <summary>
    /// The global symbol table containing symbols from all files.
    /// Null until name resolution begins.
    /// </summary>
    public SymbolTable? GlobalSymbols { get; internal set; }

    /// <summary>
    /// The semantic info shared across all files.
    /// Null until semantic analysis begins.
    /// </summary>
    public SemanticInfo? SemanticInfo { get; internal set; }

    #endregion

    #region Dependencies

    /// <summary>
    /// The project-wide dependency graph.
    /// Null until import resolution completes.
    /// </summary>
    public DependencyGraph? DependencyGraph { get; internal set; }

    /// <summary>
    /// Gets the build order for compilation (dependencies before dependents).
    /// Returns null if dependency graph is not yet available.
    /// </summary>
    /// <remarks>
    /// Build order is cached and invalidated when units are added/removed.
    /// </remarks>
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

    /// <summary>
    /// Gets compilation units in build order.
    /// </summary>
    /// <returns>
    /// Units ordered by dependencies (dependencies first), or all units in arbitrary order
    /// if dependency graph is not available.
    /// </returns>
    public IEnumerable<CompilationUnit> GetUnitsInBuildOrder()
    {
        var buildOrder = GetBuildOrder();
        if (buildOrder == null)
        {
            return _units.Values;
        }

        return buildOrder
            .Select(path => GetUnit(path))
            .Where(unit => unit != null)
            .Cast<CompilationUnit>();
    }

    /// <summary>
    /// Gets groups of files that can be compiled in parallel.
    /// </summary>
    /// <returns>
    /// Groups of CompilationUnits, where each group can be compiled concurrently,
    /// or null if dependency graph is not available.
    /// </returns>
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

    #endregion

    #region Diagnostics

    /// <summary>
    /// Project-level diagnostics (not specific to any file).
    /// </summary>
    public DiagnosticBag GlobalDiagnostics { get; }

    /// <summary>
    /// Gets all diagnostics from all compilation units and project-level diagnostics.
    /// </summary>
    /// <returns>Combined list of all diagnostics.</returns>
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

    /// <summary>
    /// Indicates whether the project has any errors.
    /// </summary>
    public bool HasErrors =>
        GlobalDiagnostics.HasErrors || _units.Values.Any(u => u.HasErrors);

    /// <summary>
    /// Gets total error count across all units and project-level diagnostics.
    /// </summary>
    public int TotalErrorCount =>
        GlobalDiagnostics.ErrorCount + _units.Values.Sum(u => u.Diagnostics.ErrorCount);

    #endregion

    #region Incremental Compilation Support

    /// <summary>
    /// Determines which files are affected by changes to the given files.
    /// </summary>
    /// <param name="changedFiles">Files that have changed.</param>
    /// <returns>
    /// Set of file paths that need recompilation (including the changed files),
    /// or null if dependency graph is not available.
    /// </returns>
    public ImmutableHashSet<string>? GetAffectedFiles(IEnumerable<string> changedFiles)
    {
        return DependencyGraph?.GetAffectedFiles(changedFiles);
    }

    /// <summary>
    /// Checks if a file is stale compared to a hash cache.
    /// </summary>
    /// <param name="filePath">The file to check.</param>
    /// <param name="cachedHash">The cached hash for this file.</param>
    /// <returns>True if the file content has changed, false otherwise.</returns>
    public bool IsFileStale(string filePath, string? cachedHash)
    {
        var unit = GetUnit(filePath);
        return unit?.IsStale(cachedHash) ?? true;
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Normalizes a file path for consistent comparison.
    /// </summary>
    private static string NormalizePath(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (!OperatingSystem.IsLinux())
        {
            normalized = normalized.ToLowerInvariant();
        }
        return normalized;
    }

    /// <summary>
    /// Invalidates the cached build order.
    /// </summary>
    private void InvalidateBuildOrder()
    {
        _cachedBuildOrder = null;
    }

    #endregion
}
```

### Task 2.2: Create unit tests for ProjectModel

- [ ] Create `/src/Sharpy.Compiler.Tests/Model/ProjectModelTests.cs`

```csharp
using Sharpy.Compiler.Model;
using Sharpy.Compiler.Project;
using Xunit;

namespace Sharpy.Compiler.Tests.Model;

public class ProjectModelTests
{
    private static ProjectConfig CreateTestConfig()
    {
        return new ProjectConfig
        {
            RootNamespace = "TestProject",
            ProjectDirectory = "/test/project",
            SourceFiles = new List<string>()
        };
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_ValidConfig_SetsProperties()
    {
        var config = CreateTestConfig();

        var model = new ProjectModel(config);

        Assert.Equal(config, model.Config);
        Assert.Empty(model.Units);
        Assert.Null(model.GlobalSymbols);
        Assert.Null(model.DependencyGraph);
    }

    [Fact]
    public void Constructor_NullConfig_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ProjectModel(null!));
    }

    #endregion

    #region Unit Management Tests

    [Fact]
    public void AddUnit_ValidUnit_AddsToUnits()
    {
        var model = new ProjectModel(CreateTestConfig());
        var unit = new CompilationUnit("/test/a.spy", "test.a", "x = 1");

        model.AddUnit(unit);

        Assert.Equal(1, model.UnitCount);
        Assert.Same(unit, model.GetUnit("/test/a.spy"));
    }

    [Fact]
    public void AddUnit_DuplicatePath_Throws()
    {
        var model = new ProjectModel(CreateTestConfig());
        var unit1 = new CompilationUnit("/test/a.spy", "test.a", "x = 1");
        var unit2 = new CompilationUnit("/test/a.spy", "test.a", "x = 2");

        model.AddUnit(unit1);

        Assert.Throws<ArgumentException>(() => model.AddUnit(unit2));
    }

    [Fact]
    public void CreateUnit_CreatesAndAdds()
    {
        var model = new ProjectModel(CreateTestConfig());

        var unit = model.CreateUnit("/test/a.spy", "test.a", "x = 1");

        Assert.Equal(1, model.UnitCount);
        Assert.Same(unit, model.GetUnit("/test/a.spy"));
    }

    [Fact]
    public void GetUnit_PathNormalization_FindsUnit()
    {
        var model = new ProjectModel(CreateTestConfig());
        model.CreateUnit("/test/src/module.spy", "test.module", "x = 1");

        // Should find with backslashes on non-Linux
        var unit = model.GetUnit("/test/src/module.spy");
        Assert.NotNull(unit);

        // Should find with different path separator
        var unit2 = model.GetUnit("\\test\\src\\module.spy");
        Assert.NotNull(unit2);
    }

    [Fact]
    public void GetUnit_NotFound_ReturnsNull()
    {
        var model = new ProjectModel(CreateTestConfig());

        Assert.Null(model.GetUnit("/nonexistent.spy"));
    }

    #endregion

    #region Diagnostics Tests

    [Fact]
    public void HasErrors_NoErrors_ReturnsFalse()
    {
        var model = new ProjectModel(CreateTestConfig());
        model.CreateUnit("/test/a.spy", "test.a", "x = 1");

        Assert.False(model.HasErrors);
    }

    [Fact]
    public void HasErrors_GlobalError_ReturnsTrue()
    {
        var model = new ProjectModel(CreateTestConfig());
        model.GlobalDiagnostics.AddError("Project error");

        Assert.True(model.HasErrors);
    }

    [Fact]
    public void HasErrors_UnitError_ReturnsTrue()
    {
        var model = new ProjectModel(CreateTestConfig());
        var unit = model.CreateUnit("/test/a.spy", "test.a", "x = 1");
        unit.Diagnostics.AddError("File error", 1, 1);

        Assert.True(model.HasErrors);
    }

    [Fact]
    public void TotalErrorCount_AggregatesAllErrors()
    {
        var model = new ProjectModel(CreateTestConfig());
        model.GlobalDiagnostics.AddError("Error 1");
        model.GlobalDiagnostics.AddError("Error 2");

        var unit1 = model.CreateUnit("/test/a.spy", "test.a", "x = 1");
        unit1.Diagnostics.AddError("Error 3", 1, 1);

        var unit2 = model.CreateUnit("/test/b.spy", "test.b", "y = 2");
        unit2.Diagnostics.AddError("Error 4", 1, 1);
        unit2.Diagnostics.AddError("Error 5", 2, 1);

        Assert.Equal(5, model.TotalErrorCount);
    }

    [Fact]
    public void GetAllDiagnostics_CombinesAll()
    {
        var model = new ProjectModel(CreateTestConfig());
        model.GlobalDiagnostics.AddError("Global error");
        model.GlobalDiagnostics.AddWarning("Global warning");

        var unit = model.CreateUnit("/test/a.spy", "test.a", "x = 1");
        unit.Diagnostics.AddError("File error", 1, 1);

        var allDiagnostics = model.GetAllDiagnostics();

        Assert.Equal(3, allDiagnostics.Count);
    }

    #endregion

    #region Build Order Tests

    [Fact]
    public void GetBuildOrder_NoDependencyGraph_ReturnsNull()
    {
        var model = new ProjectModel(CreateTestConfig());
        model.CreateUnit("/test/a.spy", "test.a", "x = 1");

        Assert.Null(model.GetBuildOrder());
    }

    [Fact]
    public void GetUnitsInBuildOrder_NoDependencyGraph_ReturnsAllUnits()
    {
        var model = new ProjectModel(CreateTestConfig());
        model.CreateUnit("/test/a.spy", "test.a", "x = 1");
        model.CreateUnit("/test/b.spy", "test.b", "y = 2");

        var units = model.GetUnitsInBuildOrder().ToList();

        Assert.Equal(2, units.Count);
    }

    #endregion

    #region Incremental Compilation Tests

    [Fact]
    public void IsFileStale_UnitExists_DelegatesToUnit()
    {
        var model = new ProjectModel(CreateTestConfig());
        var unit = model.CreateUnit("/test/a.spy", "test.a", "x = 1");

        Assert.False(model.IsFileStale("/test/a.spy", unit.ContentHash));
        Assert.True(model.IsFileStale("/test/a.spy", "different_hash"));
    }

    [Fact]
    public void IsFileStale_UnitNotFound_ReturnsTrue()
    {
        var model = new ProjectModel(CreateTestConfig());

        Assert.True(model.IsFileStale("/nonexistent.spy", "any_hash"));
    }

    #endregion
}
```

### Task 2.3: Verify tests pass

- [ ] Run the new tests:
  ```bash
  dotnet test src/Sharpy.Compiler.Tests/Sharpy.Compiler.Tests.csproj --filter "FullyQualifiedName~ProjectModelTests"
  ```
- [ ] Run all tests:
  ```bash
  dotnet test src/Sharpy.Compiler.Tests/Sharpy.Compiler.Tests.csproj --filter "Category!=Performance"
  ```

### Task 2.4: Commit Phase 2

- [ ] Commit changes:
  ```bash
  git add .
  git commit -m "Add ProjectModel class for unified project representation

  - Create Model/ProjectModel.cs with unit management, diagnostics aggregation
  - Support for build ordering via DependencyGraph integration
  - Incremental compilation support with IsFileStale checks
  - Parallel compilation group support via GetParallelizableGroups
  - Add comprehensive unit tests for ProjectModel

  Part of architecture Rec #1: CompilationUnit Model"
  ```

---

## Phase 3: Create CompilationUnitFactory (Helper)

**Goal:** Create factory methods to populate CompilationUnits from compilation results.

### Task 3.1: Create CompilationUnitFactory.cs

- [ ] Create `/src/Sharpy.Compiler/Model/CompilationUnitFactory.cs`

```csharp
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Parser;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;
using System.Collections.Immutable;

namespace Sharpy.Compiler.Model;

/// <summary>
/// Factory methods for creating and populating CompilationUnits.
/// </summary>
public static class CompilationUnitFactory
{
    /// <summary>
    /// Computes the module path from a file path and project root.
    /// </summary>
    /// <param name="filePath">Full path to the source file.</param>
    /// <param name="projectRoot">Path to the project root directory.</param>
    /// <returns>Dotted module path (e.g., "mypackage.mymodule").</returns>
    public static string ComputeModulePath(string filePath, string projectRoot)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(projectRoot);

        var relativePath = Path.GetRelativePath(projectRoot, filePath);
        var withoutExtension = Path.ChangeExtension(relativePath, null);

        // Replace directory separators with dots
        var modulePath = withoutExtension
            .Replace(Path.DirectorySeparatorChar, '.')
            .Replace(Path.AltDirectorySeparatorChar, '.');

        // Remove leading dots if present
        while (modulePath.StartsWith('.'))
        {
            modulePath = modulePath.Substring(1);
        }

        return modulePath;
    }

    /// <summary>
    /// Creates a CompilationUnit from a source file.
    /// </summary>
    /// <param name="filePath">Full path to the source file.</param>
    /// <param name="projectRoot">Path to the project root directory.</param>
    /// <returns>A new CompilationUnit with source text loaded.</returns>
    public static CompilationUnit CreateFromFile(string filePath, string projectRoot)
    {
        var sourceText = File.ReadAllText(filePath);
        var modulePath = ComputeModulePath(filePath, projectRoot);
        return new CompilationUnit(filePath, modulePath, sourceText);
    }

    /// <summary>
    /// Performs lexical analysis on a CompilationUnit.
    /// </summary>
    /// <param name="unit">The CompilationUnit to tokenize.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <returns>True if lexing succeeded, false if there were errors.</returns>
    public static bool Lex(CompilationUnit unit, ICompilerLogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(unit);

        try
        {
            var lexer = new Lexer.Lexer(unit.SourceText, logger ?? NullLogger.Instance);
            var tokens = lexer.TokenizeAll();
            unit.Tokens = tokens;
            unit.Phase = CompilationPhase.Lexed;
            return true;
        }
        catch (LexerError ex)
        {
            unit.Diagnostics.AddError(ex.Message, ex.Line, ex.Column, unit.FilePath);
            unit.Phase = CompilationPhase.Failed;
            return false;
        }
    }

    /// <summary>
    /// Performs parsing on a CompilationUnit.
    /// Requires Lex() to have been called first.
    /// </summary>
    /// <param name="unit">The CompilationUnit to parse.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <returns>True if parsing succeeded, false if there were errors.</returns>
    public static bool Parse(CompilationUnit unit, ICompilerLogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(unit);

        if (unit.Tokens == null)
        {
            throw new InvalidOperationException("Cannot parse without tokens. Call Lex() first.");
        }

        try
        {
            var parser = new Parser.Parser(unit.Tokens.ToList(), logger ?? NullLogger.Instance);
            var ast = parser.ParseModule();
            unit.Ast = ast;

            // Extract import statements from AST
            var imports = new List<ImportStatement>();
            var fromImports = new List<FromImportStatement>();

            foreach (var statement in ast.Body)
            {
                if (statement is ImportStatement import)
                {
                    imports.Add(import);
                }
                else if (statement is FromImportStatement fromImport)
                {
                    fromImports.Add(fromImport);
                }
            }

            unit.Imports = imports;
            unit.FromImports = fromImports;
            unit.Phase = CompilationPhase.Parsed;
            return true;
        }
        catch (ParserError ex)
        {
            unit.Diagnostics.AddError(ex.Message, ex.Line, ex.Column, unit.FilePath);
            unit.Phase = CompilationPhase.Failed;
            return false;
        }
    }

    /// <summary>
    /// Performs lexing and parsing in one call.
    /// </summary>
    /// <param name="unit">The CompilationUnit to process.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <returns>True if both lexing and parsing succeeded.</returns>
    public static bool LexAndParse(CompilationUnit unit, ICompilerLogger? logger = null)
    {
        return Lex(unit, logger) && Parse(unit, logger);
    }

    /// <summary>
    /// Sets the direct dependencies for a CompilationUnit.
    /// </summary>
    /// <param name="unit">The CompilationUnit to update.</param>
    /// <param name="dependencies">The file paths this unit depends on.</param>
    public static void SetDependencies(CompilationUnit unit, IEnumerable<string> dependencies)
    {
        ArgumentNullException.ThrowIfNull(unit);
        ArgumentNullException.ThrowIfNull(dependencies);

        unit.DirectDependencies = dependencies.ToImmutableHashSet();
    }
}
```

### Task 3.2: Create unit tests for CompilationUnitFactory

- [ ] Create `/src/Sharpy.Compiler.Tests/Model/CompilationUnitFactoryTests.cs`

```csharp
using Sharpy.Compiler.Model;
using Xunit;

namespace Sharpy.Compiler.Tests.Model;

public class CompilationUnitFactoryTests
{
    #region ComputeModulePath Tests

    [Theory]
    [InlineData("/project/src/main.spy", "/project", "src.main")]
    [InlineData("/project/main.spy", "/project", "main")]
    [InlineData("/project/utils/helpers.spy", "/project", "utils.helpers")]
    [InlineData("/project/src/lib/math/vector.spy", "/project", "src.lib.math.vector")]
    public void ComputeModulePath_ReturnsCorrectPath(string filePath, string projectRoot, string expected)
    {
        var result = CompilationUnitFactory.ComputeModulePath(filePath, projectRoot);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ComputeModulePath_NullFilePath_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            CompilationUnitFactory.ComputeModulePath(null!, "/project"));
    }

    [Fact]
    public void ComputeModulePath_NullProjectRoot_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            CompilationUnitFactory.ComputeModulePath("/project/main.spy", null!));
    }

    #endregion

    #region Lex Tests

    [Fact]
    public void Lex_ValidSource_SetsTokensAndPhase()
    {
        var unit = new CompilationUnit("/test/a.spy", "test.a", "x: int = 42");

        var result = CompilationUnitFactory.Lex(unit);

        Assert.True(result);
        Assert.NotNull(unit.Tokens);
        Assert.True(unit.Tokens.Count > 0);
        Assert.Equal(CompilationPhase.Lexed, unit.Phase);
    }

    [Fact]
    public void Lex_InvalidSource_AddsDiagnosticAndFails()
    {
        var unit = new CompilationUnit("/test/a.spy", "test.a", "x = @@@invalid");

        var result = CompilationUnitFactory.Lex(unit);

        Assert.False(result);
        Assert.True(unit.HasErrors);
        Assert.Equal(CompilationPhase.Failed, unit.Phase);
    }

    [Fact]
    public void Lex_NullUnit_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            CompilationUnitFactory.Lex(null!));
    }

    #endregion

    #region Parse Tests

    [Fact]
    public void Parse_ValidTokens_SetsAstAndPhase()
    {
        var unit = new CompilationUnit("/test/a.spy", "test.a", "x: int = 42");
        CompilationUnitFactory.Lex(unit);

        var result = CompilationUnitFactory.Parse(unit);

        Assert.True(result);
        Assert.NotNull(unit.Ast);
        Assert.Equal(CompilationPhase.Parsed, unit.Phase);
    }

    [Fact]
    public void Parse_WithImports_ExtractsImportStatements()
    {
        var source = @"import math
from utils import helper
x = 1";
        var unit = new CompilationUnit("/test/a.spy", "test.a", source);
        CompilationUnitFactory.Lex(unit);

        var result = CompilationUnitFactory.Parse(unit);

        Assert.True(result);
        Assert.Single(unit.Imports);
        Assert.Single(unit.FromImports);
    }

    [Fact]
    public void Parse_WithoutTokens_Throws()
    {
        var unit = new CompilationUnit("/test/a.spy", "test.a", "x = 1");
        // Don't call Lex()

        Assert.Throws<InvalidOperationException>(() =>
            CompilationUnitFactory.Parse(unit));
    }

    [Fact]
    public void Parse_InvalidSyntax_AddsDiagnosticAndFails()
    {
        var unit = new CompilationUnit("/test/a.spy", "test.a", "def foo( -> int:");
        CompilationUnitFactory.Lex(unit);

        var result = CompilationUnitFactory.Parse(unit);

        Assert.False(result);
        Assert.True(unit.HasErrors);
        Assert.Equal(CompilationPhase.Failed, unit.Phase);
    }

    #endregion

    #region LexAndParse Tests

    [Fact]
    public void LexAndParse_ValidSource_Succeeds()
    {
        var unit = new CompilationUnit("/test/a.spy", "test.a", "x: int = 42\ny: str = \"hello\"");

        var result = CompilationUnitFactory.LexAndParse(unit);

        Assert.True(result);
        Assert.NotNull(unit.Tokens);
        Assert.NotNull(unit.Ast);
        Assert.Equal(CompilationPhase.Parsed, unit.Phase);
    }

    [Fact]
    public void LexAndParse_LexError_FailsEarly()
    {
        var unit = new CompilationUnit("/test/a.spy", "test.a", "@@@");

        var result = CompilationUnitFactory.LexAndParse(unit);

        Assert.False(result);
        Assert.True(unit.HasErrors);
        Assert.Null(unit.Ast);
    }

    #endregion

    #region SetDependencies Tests

    [Fact]
    public void SetDependencies_SetsDependencies()
    {
        var unit = new CompilationUnit("/test/a.spy", "test.a", "x = 1");
        var deps = new[] { "/test/b.spy", "/test/c.spy" };

        CompilationUnitFactory.SetDependencies(unit, deps);

        Assert.Equal(2, unit.DirectDependencies.Count);
        Assert.Contains("/test/b.spy", unit.DirectDependencies);
        Assert.Contains("/test/c.spy", unit.DirectDependencies);
    }

    #endregion
}
```

### Task 3.3: Verify tests pass

- [ ] Run the new tests:
  ```bash
  dotnet test src/Sharpy.Compiler.Tests/Sharpy.Compiler.Tests.csproj --filter "FullyQualifiedName~CompilationUnitFactoryTests"
  ```
- [ ] Run all tests:
  ```bash
  dotnet test src/Sharpy.Compiler.Tests/Sharpy.Compiler.Tests.csproj --filter "Category!=Performance"
  ```

### Task 3.4: Commit Phase 3

- [ ] Commit changes:
  ```bash
  git add .
  git commit -m "Add CompilationUnitFactory for creating and processing units

  - Create Model/CompilationUnitFactory.cs with factory methods
  - Implement Lex(), Parse(), LexAndParse() for phased compilation
  - Add ComputeModulePath() for file-to-module path conversion
  - Handle errors by adding diagnostics to the unit
  - Add comprehensive unit tests

  Part of architecture Rec #1: CompilationUnit Model"
  ```

---

## Phase 4: Integration with ProjectCompiler (Refactor)

**Goal:** Refactor `ProjectCompiler` to use `ProjectModel` internally while maintaining the existing API.

### Task 4.1: Add ProjectModel field to ProjectCompiler

- [ ] Edit `/src/Sharpy.Compiler/Project/ProjectCompiler.cs`

Add the following field and modify existing code:

```csharp
// Add at the top with other fields:
private ProjectModel? _projectModel;

// In Compile() method, after initializing _projectMetrics, add:
_projectModel = new ProjectModel(config);

// IMPORTANT: Keep all existing code working - this is additive!
```

### Task 4.2: Populate CompilationUnits during parsing phase

Modify the `ParseAllFiles` method to create CompilationUnits:

```csharp
private bool ParseAllFiles(ProjectConfig config)
{
    _logger.LogInfo($"Phase 1: Parsing {config.SourceFiles.Count} source files");

    foreach (var sourceFile in config.SourceFiles)
    {
        var fileMetrics = new CompilationMetrics(
            fileName: Path.GetRelativePath(config.ProjectDirectory, sourceFile),
            projectName: config.RootNamespace,
            configuration: config.Configuration);

        try
        {
            var source = File.ReadAllText(sourceFile);

            // Create CompilationUnit (NEW)
            var modulePath = CompilationUnitFactory.ComputeModulePath(sourceFile, config.ProjectDirectory);
            var compilationUnit = _projectModel!.CreateUnit(sourceFile, modulePath, source);

            fileMetrics.StartPhase("Lexical Analysis");
            var lexer = new Lexer.Lexer(source, _logger);
            var tokens = lexer.TokenizeAll();
            fileMetrics.EndPhase();

            // Store tokens in CompilationUnit (NEW)
            compilationUnit.Tokens = tokens;
            compilationUnit.Phase = CompilationPhase.Lexed;

            fileMetrics.StartPhase("Syntax Analysis");
            var parser = new Parser.Parser(tokens, _logger);
            var module = parser.ParseModule();
            fileMetrics.EndPhase();

            // Store AST in CompilationUnit (NEW)
            compilationUnit.Ast = module;
            compilationUnit.Phase = CompilationPhase.Parsed;

            // Extract imports (NEW)
            var imports = new List<ImportStatement>();
            var fromImports = new List<FromImportStatement>();
            foreach (var stmt in module.Body)
            {
                if (stmt is ImportStatement import) imports.Add(import);
                else if (stmt is FromImportStatement fromImport) fromImports.Add(fromImport);
            }
            compilationUnit.Imports = imports;
            compilationUnit.FromImports = fromImports;

            _parsedModules[sourceFile] = module;
            _fileMetrics[sourceFile] = fileMetrics;

            // ... rest of existing code ...
        }
        catch (LexerError ex)
        {
            // Add to CompilationUnit diagnostics if available
            var unit = _projectModel!.GetUnit(sourceFile);
            unit?.Diagnostics.AddError(ex.Message, ex.Line, ex.Column, sourceFile);

            _errors.Add($"{sourceFile}({ex.Line},{ex.Column}): error: {ex.Message}");
            _projectMetrics.AddFileMetrics(fileMetrics);
        }
        // ... rest of existing catch blocks with similar changes ...
    }

    return !_errors.Any();
}
```

### Task 4.3: Store DependencyGraph in ProjectModel

Modify `ResolveImports` to store the dependency graph:

```csharp
private bool ResolveImports(ProjectConfig config)
{
    // ... existing code ...

    // Build the dependency graph after all imports are resolved
    _dependencyGraph = _graphBuilder.Build();

    // Store in ProjectModel (NEW)
    _projectModel!.DependencyGraph = _dependencyGraph;

    // ... rest of existing code ...
}
```

### Task 4.4: Store symbols in ProjectModel

Modify `InitializeSharedState`:

```csharp
private void InitializeSharedState()
{
    var builtinRegistry = new BuiltinRegistry();
    _symbolTable = new SymbolTable(builtinRegistry);
    _semanticInfo = new SemanticInfo();
    _importResolver = new ImportResolver(_logger, _moduleRegistry);

    // Store in ProjectModel (NEW)
    _projectModel!.GlobalSymbols = _symbolTable;
    _projectModel!.SemanticInfo = _semanticInfo;

    // ... rest of existing code ...
}
```

### Task 4.5: Update semantic analysis to update CompilationUnit phase

Modify `PerformSemanticAnalysis`:

```csharp
private bool PerformSemanticAnalysis(ProjectConfig config)
{
    // ... existing code ...

    foreach (var sourceFile in modulesToProcess)
    {
        var module = _parsedModules[sourceFile];
        var unit = _projectModel!.GetUnit(sourceFile); // NEW

        // ... existing analysis code ...

        if (typeChecker.Errors.Any())
        {
            // Add to unit diagnostics (NEW)
            foreach (var error in typeChecker.Errors)
            {
                unit?.Diagnostics.AddError(error.Message, error.Line, error.Column, sourceFile);
            }
            unit!.Phase = CompilationPhase.Failed; // NEW

            _errors.AddRange(typeChecker.Errors.Select(e =>
                $"{sourceFile}({e.Line},{e.Column}): error: {e.Message}"));
        }
        else
        {
            unit!.Phase = CompilationPhase.TypeChecked; // NEW
        }

        // ... rest of existing code ...
    }

    return !_errors.Any();
}
```

### Task 4.6: Store generated C# in CompilationUnit

Modify `GenerateCode`:

```csharp
private Dictionary<string, string> GenerateCode(ProjectConfig config)
{
    // ... existing code ...

    foreach (var (sourceFile, module) in _parsedModules)
    {
        var unit = _projectModel!.GetUnit(sourceFile); // NEW

        // ... existing code generation ...

        // Check for code generation errors
        if (codeGenContext.HasErrors)
        {
            foreach (var error in codeGenContext.Errors)
            {
                _errors.Add($"{sourceFile}: error: {error}");
                unit?.Diagnostics.AddError(error, null, null, sourceFile); // NEW
            }
            unit!.Phase = CompilationPhase.Failed; // NEW
            continue;
        }

        // Store generated C# in CompilationUnit (NEW)
        unit!.GeneratedCSharp = csharpCode;
        unit.Phase = CompilationPhase.CodeGenerated;

        // ... rest of existing code ...
    }

    return generatedCSharp;
}
```

### Task 4.7: Add ProjectModel to ProjectCompilationResult (optional, for future use)

- [ ] Update `ProjectCompilationResult` in `Compiler.cs`:

```csharp
public class ProjectCompilationResult
{
    // ... existing properties ...

    /// <summary>
    /// The ProjectModel containing all CompilationUnits.
    /// Available for tooling and analysis.
    /// </summary>
    public Model.ProjectModel? ProjectModel { get; init; }
}
```

- [ ] Update `CreateFailureResult` and successful result returns to include `ProjectModel = _projectModel`

### Task 4.8: Create integration tests

- [ ] Create `/src/Sharpy.Compiler.Tests/Model/ProjectCompilerIntegrationTests.cs`

```csharp
using Sharpy.Compiler.Model;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Tests.Helpers;
using Xunit;

namespace Sharpy.Compiler.Tests.Model;

public class ProjectCompilerModelIntegrationTests
{
    [Fact]
    public void Compile_PopulatesProjectModel()
    {
        // Create a simple test project
        using var tempDir = new TempDirectory();
        var mainSpy = tempDir.CreateFile("main.spy", "x: int = 42");

        var config = new ProjectConfig
        {
            RootNamespace = "TestProject",
            ProjectDirectory = tempDir.Path,
            SourceFiles = new List<string> { mainSpy }
        };

        var compiler = new ProjectCompiler();
        var result = compiler.Compile(config);

        // Verify ProjectModel is populated
        Assert.NotNull(result.ProjectModel);
        Assert.Equal(1, result.ProjectModel.UnitCount);

        var unit = result.ProjectModel.GetUnit(mainSpy);
        Assert.NotNull(unit);
        Assert.Equal("main", unit.ModulePath);
        Assert.NotNull(unit.Ast);
        Assert.NotNull(unit.Tokens);
        Assert.NotEmpty(unit.ContentHash);

        if (result.Success)
        {
            Assert.Equal(CompilationPhase.CodeGenerated, unit.Phase);
            Assert.NotNull(unit.GeneratedCSharp);
        }
    }

    [Fact]
    public void Compile_MultiFile_AllUnitsPopulated()
    {
        using var tempDir = new TempDirectory();
        var mainSpy = tempDir.CreateFile("main.spy", @"from utils import helper
x = helper()");
        var utilsSpy = tempDir.CreateFile("utils.spy", @"def helper() -> int:
    return 42");

        var config = new ProjectConfig
        {
            RootNamespace = "TestProject",
            ProjectDirectory = tempDir.Path,
            SourceFiles = new List<string> { mainSpy, utilsSpy }
        };

        var compiler = new ProjectCompiler();
        var result = compiler.Compile(config);

        Assert.NotNull(result.ProjectModel);
        Assert.Equal(2, result.ProjectModel.UnitCount);

        Assert.NotNull(result.ProjectModel.GetUnit(mainSpy));
        Assert.NotNull(result.ProjectModel.GetUnit(utilsSpy));
    }

    [Fact]
    public void Compile_Error_UnitHasDiagnostics()
    {
        using var tempDir = new TempDirectory();
        var mainSpy = tempDir.CreateFile("main.spy", "x: int = \"not an int\"");

        var config = new ProjectConfig
        {
            RootNamespace = "TestProject",
            ProjectDirectory = tempDir.Path,
            SourceFiles = new List<string> { mainSpy }
        };

        var compiler = new ProjectCompiler();
        var result = compiler.Compile(config);

        Assert.False(result.Success);
        Assert.NotNull(result.ProjectModel);

        var unit = result.ProjectModel.GetUnit(mainSpy);
        Assert.NotNull(unit);
        Assert.True(unit.HasErrors);
    }
}

/// <summary>
/// Helper class for creating temporary directories in tests.
/// </summary>
internal class TempDirectory : IDisposable
{
    public string Path { get; }

    public TempDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"sharpy_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    public string CreateFile(string relativePath, string content)
    {
        var fullPath = System.IO.Path.Combine(Path, relativePath);
        var dir = System.IO.Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
    }
}
```

### Task 4.9: Verify all tests pass

- [ ] Run integration tests:
  ```bash
  dotnet test src/Sharpy.Compiler.Tests/Sharpy.Compiler.Tests.csproj --filter "FullyQualifiedName~ProjectCompilerModelIntegrationTests"
  ```
- [ ] Run ALL tests to ensure no regressions:
  ```bash
  dotnet test src/Sharpy.Compiler.Tests/Sharpy.Compiler.Tests.csproj
  ```

### Task 4.10: Commit Phase 4

- [ ] Commit changes:
  ```bash
  git add .
  git commit -m "Integrate ProjectModel into ProjectCompiler

  - Add ProjectModel as internal state in ProjectCompiler
  - Populate CompilationUnits during parsing phase
  - Store tokens, AST, imports in CompilationUnits
  - Update compilation phases as processing progresses
  - Store DependencyGraph in ProjectModel
  - Add diagnostics to CompilationUnits alongside existing error lists
  - Expose ProjectModel in ProjectCompilationResult
  - Add integration tests verifying model population

  Part of architecture Rec #1: CompilationUnit Model
  BREAKING: None - existing APIs unchanged"
  ```

---

## Phase 5: Documentation and Cleanup

### Task 5.1: Add XML documentation

- [ ] Ensure all public members have XML documentation
- [ ] Add `<example>` sections to key classes

### Task 5.2: Create README for Model namespace

- [ ] Create `/src/Sharpy.Compiler/Model/README.md`

```markdown
# Sharpy.Compiler.Model Namespace

This namespace contains the core data model classes for the Sharpy compiler.

## Key Classes

### CompilationUnit

Represents a single Sharpy source file and all its compilation artifacts:
- Source text and content hash (for incremental compilation)
- Tokens (for LSP hover/completion)
- AST (Module)
- Semantic artifacts (declared types, functions, scope)
- Dependencies (direct imports)
- Generated C# code
- Diagnostics (errors/warnings specific to this file)

### ProjectModel

Represents a complete Sharpy project being compiled:
- Collection of CompilationUnits
- Global symbol table
- Dependency graph
- Build ordering

### CompilationUnitFactory

Factory methods for creating and processing CompilationUnits:
- `CreateFromFile()` - Load source from file
- `Lex()` / `Parse()` / `LexAndParse()` - Process compilation phases

## Usage

```csharp
// Create a project model
var model = new ProjectModel(config);

// Add compilation units
var unit = model.CreateUnit(filePath, modulePath, sourceText);

// Process the unit
CompilationUnitFactory.LexAndParse(unit, logger);

// Access results
var ast = unit.Ast;
var tokens = unit.Tokens;
var errors = unit.Diagnostics.GetAll();
```

## Design Principles

1. **Immutability where practical**: Use `init` properties, but allow
   internal setters for compilation pipeline flexibility.

2. **Thread-safety**: DiagnosticBag is thread-safe for future parallel
   compilation support.

3. **Incremental compilation ready**: Content hashing and dependency
   tracking support future incremental compilation.

4. **LSP ready**: Token storage and source spans enable future IDE
   integration.
```

### Task 5.3: Final test run

- [ ] Run ALL tests:
  ```bash
  dotnet test src/Sharpy.Compiler.Tests/Sharpy.Compiler.Tests.csproj
  ```
- [ ] Run performance tests to ensure no regression:
  ```bash
  dotnet test src/Sharpy.Compiler.Tests/Sharpy.Compiler.Tests.csproj --filter "Category=Performance"
  ```

### Task 5.4: Final commit

- [ ] Commit documentation:
  ```bash
  git add .
  git commit -m "Add documentation for Model namespace

  - Add README.md explaining Model namespace usage
  - Ensure all public members have XML documentation

  Completes architecture Rec #1: CompilationUnit Model"
  ```

---

## Summary

### Files Created
1. `/src/Sharpy.Compiler/Model/CompilationUnit.cs`
2. `/src/Sharpy.Compiler/Model/ProjectModel.cs`
3. `/src/Sharpy.Compiler/Model/CompilationUnitFactory.cs`
4. `/src/Sharpy.Compiler/Model/README.md`
5. `/src/Sharpy.Compiler.Tests/Model/CompilationUnitTests.cs`
6. `/src/Sharpy.Compiler.Tests/Model/ProjectModelTests.cs`
7. `/src/Sharpy.Compiler.Tests/Model/CompilationUnitFactoryTests.cs`
8. `/src/Sharpy.Compiler.Tests/Model/ProjectCompilerModelIntegrationTests.cs`

### Files Modified
1. `/src/Sharpy.Compiler/Project/ProjectCompiler.cs` - Add ProjectModel integration
2. `/src/Sharpy.Compiler/Compiler.cs` - Add ProjectModel to result

### Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| Mutable internal setters | Compilation pipeline needs to update state; can be tightened in future |
| Thread-safe DiagnosticBag | Prepares for parallel compilation in v0.2.x |
| Content hashing | Enables incremental compilation without file system checks |
| Token storage | Required for LSP hover/completion |
| Path normalization | Consistent cross-platform behavior |
| Additive implementation | Existing APIs unchanged; easy rollback |

### Future Work (Not Part of This Task)

- [ ] **Phase 2 (Rec #8)**: Dependency Graph enhancements for type-level dependencies
- [ ] **Phase 3 (Rec #7)**: Migrate to immutable AST
- [ ] **Phase 4**: Deprecate `_parsedModules` dictionary in favor of `ProjectModel.Units`
- [ ] **Phase 5**: Add caching layer using `ContentHash` for incremental builds
