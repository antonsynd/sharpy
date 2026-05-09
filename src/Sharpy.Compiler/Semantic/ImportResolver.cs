using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Services;
using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Utilities;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Resolves imports and loads symbols from imported modules (both .spy files and .NET assemblies).
/// Delegates module loading/caching/symbol-extraction to <see cref="ModuleLoader"/>.
/// Sub-partials: ModuleLoading, Symbols, Helpers
/// </summary>
internal partial class ImportResolver
{
    private readonly ICompilerLogger _logger;
    private readonly DiagnosticBag _diagnostics = new();
    private readonly ModuleLoader _moduleLoader;
    private readonly ModuleRegistry? _moduleRegistry;
    private readonly ModuleResolver _moduleResolver;

    /// <summary>
    /// All loaded .spy modules (excludes .NET modules).
    /// Key is the full file path, value is the ModuleInfo.
    /// </summary>
    public IReadOnlyDictionary<string, ModuleInfo> LoadedSpyModules => _moduleLoader.LoadedSpyModules;
    private IDependencyRecorder? _dependencyRecorder;
    private SemanticBinding _semanticBinding = new();

    private string? _currentModulePath = null;

    public ImportResolver(ICompilerLogger? logger = null, ModuleRegistry? moduleRegistry = null,
        ModuleResolver? moduleResolver = null,
        SemanticBinding? semanticBinding = null, IDependencyRecorder? dependencyRecorder = null)
        : this(new ModuleLoader(logger), logger, moduleRegistry, moduleResolver,
            semanticBinding, dependencyRecorder)
    {
    }

    public ImportResolver(ModuleLoader moduleLoader, ICompilerLogger? logger = null,
        ModuleRegistry? moduleRegistry = null, ModuleResolver? moduleResolver = null,
        SemanticBinding? semanticBinding = null, IDependencyRecorder? dependencyRecorder = null)
    {
        _moduleLoader = moduleLoader;
        _logger = logger ?? NullLogger.Instance;
        _moduleRegistry = moduleRegistry;
        _moduleResolver = moduleResolver ?? new ModuleResolver(logger);
        if (semanticBinding != null)
            _semanticBinding = semanticBinding;
        _dependencyRecorder = dependencyRecorder;
    }

    public DiagnosticBag Diagnostics => _diagnostics;

    private CancellationToken _cancellationToken;

    /// <summary>
    /// Updates the current module path on all internal components.
    /// </summary>
    private void UpdateCurrentModule(string modulePath)
    {
        _currentModulePath = modulePath;
        _moduleLoader.CurrentModulePath = modulePath;
        _moduleResolver.SetCurrentModulePath(modulePath);
    }

    private void AddError(string message, int? line, int? column, string? code = null,
        Text.TextSpan? span = null)
    {
        var errorMessage = _currentModulePath != null
            ? $"{message} (in {Path.GetFileName(_currentModulePath)})"
            : message;
        _diagnostics.AddPhaseError(errorMessage, CompilerPhase.ImportResolution,
            span: span, line: line, column: column, filePath: _currentModulePath, code: code);
    }

    /// <summary>
    /// Checks whether a from-import symbol name was already imported from a different module.
    /// Returns the name of the existing source module if a duplicate is detected, or null otherwise.
    /// Same-module re-imports (idempotent) return null (not a conflict).
    /// Shared between <see cref="ImportResolver"/> (single-file) and ProjectCompiler (multi-file).
    /// </summary>
    internal static string? FindDuplicateFromImportSource(
        string registerName,
        string sourceModule,
        Dictionary<string, string> importedSources)
    {
        if (importedSources.TryGetValue(registerName, out var existingModule)
            && !string.Equals(existingModule, sourceModule, StringComparison.Ordinal))
        {
            return existingModule;
        }

        return null;
    }
}
