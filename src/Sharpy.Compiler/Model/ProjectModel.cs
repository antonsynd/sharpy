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
/// - <b>Multi-file compilation</b>: Manages all source files as CompilationUnits
/// - <b>Dependency tracking</b>: Integrates with DependencyGraph for build ordering
/// - <b>Cross-file resolution</b>: Global symbol table for cross-module references
/// - <b>Incremental compilation</b>: Track which files need recompilation
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

    /// <summary>
    /// Gets all compilation units that failed during compilation.
    /// </summary>
    /// <returns>List of units with Failed phase.</returns>
    public IReadOnlyList<CompilationUnit> GetFailedUnits()
    {
        return _units.Values.Where(u => u.Phase == CompilationPhase.Failed).ToList();
    }

    /// <summary>
    /// Gets all compilation units that completed code generation successfully.
    /// </summary>
    /// <returns>List of units with CodeGenerated phase.</returns>
    public IReadOnlyList<CompilationUnit> GetSuccessfulUnits()
    {
        return _units.Values.Where(u => u.Phase == CompilationPhase.CodeGenerated).ToList();
    }

    /// <summary>
    /// Gets all compilation units with the specified phase.
    /// </summary>
    /// <param name="phase">The compilation phase to filter by.</param>
    /// <returns>List of units at the specified phase.</returns>
    public IReadOnlyList<CompilationUnit> GetUnitsAtPhase(CompilationPhase phase)
    {
        return _units.Values.Where(u => u.Phase == phase).ToList();
    }

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

    /// <summary>
    /// The semantic binding storing semantic data separate from AST nodes.
    /// This enables immutable AST while allowing semantic annotations.
    /// Null until semantic analysis begins.
    /// </summary>
    public SemanticBinding SemanticBinding { get; internal set; } = new();

    #endregion

    #region Dependencies

    /// <summary>
    /// The project-wide dependency graph.
    /// Null until import resolution completes.
    /// </summary>
    internal DependencyGraph? DependencyGraph { get; set; }

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

    /// <summary>
    /// Gets all error messages formatted as strings for reporting.
    /// </summary>
    /// <returns>List of formatted error messages with file paths and locations.</returns>
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
