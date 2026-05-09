using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Services;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Shared context for all semantic validators.
/// Contains symbols, types, caches, and diagnostics.
///
/// Design notes for future features:
/// - LSP: Context can be snapshotted and compared for incremental validation
/// - Parallel: Context is designed to be shared across validators (caches are thread-safe)
/// - Incremental: Context can track which parts have changed since last validation
/// </summary>
internal class SemanticContext
{
    // Core semantic data — exposed via the read-only IGlobalSymbolTable interface
    // because validators only read symbols. Write access is performed by NameResolver,
    // ImportResolver, and TypeChecker via the concrete SymbolTable.
    public IGlobalSymbolTable SymbolTable { get; }
    public BuiltinRegistry Builtins { get; }
    public SemanticInfo SemanticInfo { get; }
    public TypeResolver TypeResolver { get; }

    // Shared caches (avoid duplicate work across validators)
    public ClrMemberCache ClrCache { get; }

    // Diagnostics collection
    public DiagnosticBag Diagnostics { get; }

    // Logging
    public ICompilerLogger Logger { get; }

    // File context (for multi-file compilation)
    public string? CurrentFilePath { get; set; }

    // Configuration
    public bool ContinueAfterErrors { get; set; } = true;
    public int MaxErrors { get; set; } = 100;

    /// <summary>
    /// Whether this file is the entry point (main executable file).
    /// Entry point files require a main() function.
    /// </summary>
    public bool IsEntryPoint { get; set; } = false;

    /// <summary>
    /// Optional CompilerServices for centralized service access.
    /// When set, provides access to all compiler services.
    /// </summary>
    public CompilerServices? Services { get; }

    /// <summary>
    /// Optional SemanticBinding for reading inheritance data.
    /// When set, validators should prefer this over direct symbol property access.
    /// </summary>
    public SemanticBinding SemanticBinding { get; set; } = new();

    // Centralized AST traversal state (recommended for new validators)
    /// <summary>
    /// Centralized AST traversal state for validators.
    /// Use this instead of the individual state properties for stack-based scope tracking.
    /// </summary>
    public AstTraversalContext Traversal { get; } = new();

    public SemanticContext(
        SymbolTable symbolTable,
        SemanticInfo semanticInfo,
        TypeResolver typeResolver,
        ICompilerLogger? logger = null)
        : this((IGlobalSymbolTable)symbolTable, symbolTable.BuiltinRegistry, semanticInfo, typeResolver, logger)
    {
    }

    /// <summary>
    /// Internal constructor used when only the read-only symbol-table view is available.
    /// Callers must provide the BuiltinRegistry separately because it is not exposed on
    /// <see cref="IGlobalSymbolTable"/>.
    /// </summary>
    internal SemanticContext(
        IGlobalSymbolTable symbolTable,
        BuiltinRegistry builtins,
        SemanticInfo semanticInfo,
        TypeResolver typeResolver,
        ICompilerLogger? logger = null)
    {
        SymbolTable = symbolTable;
        Builtins = builtins;
        SemanticInfo = semanticInfo;
        TypeResolver = typeResolver;
        Logger = logger ?? NullLogger.Instance;

        Diagnostics = new DiagnosticBag();
        ClrCache = new ClrMemberCache();
    }

    /// <summary>
    /// Create a context backed by CompilerServices.
    /// Preferred constructor for new code.
    /// </summary>
    public SemanticContext(CompilerServices services)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
        SymbolTable = services.SymbolTable;
        Builtins = services.SymbolTable.BuiltinRegistry;
        SemanticInfo = services.SemanticInfo;
        TypeResolver = (services.TypeResolver as TypeResolverAdapter)?.UnderlyingResolver
            ?? throw new InvalidOperationException("TypeResolver must be a TypeResolverAdapter");
        Logger = services.Logger;
        Diagnostics = services.DiagnosticReporter.Diagnostics;
        ClrCache = (services.ClrMapper as ClrTypeMapperAdapter)?.UnderlyingCache ?? new ClrMemberCache();
        CurrentFilePath = services.CurrentFilePath;

        // Propagate configuration from CompilerServices
        ContinueAfterErrors = services.Configuration.ContinueAfterErrors;
        MaxErrors = services.Configuration.MaxErrors;
    }

    /// <summary>
    /// Create a context with shared infrastructure but fresh diagnostics.
    /// Useful for validating individual files in a project.
    /// </summary>
    public SemanticContext CreateForFile(string filePath)
    {
        return new SemanticContext(SymbolTable, Builtins, SemanticInfo, TypeResolver, Logger)
        {
            CurrentFilePath = filePath,
            ContinueAfterErrors = ContinueAfterErrors,
            MaxErrors = MaxErrors,
            SemanticBinding = SemanticBinding,
            // Share the ClrCache across files for efficiency
        };
    }

    /// <summary>
    /// Check if we should continue validation (based on error count and configuration).
    /// </summary>
    public bool ShouldContinue()
    {
        if (!ContinueAfterErrors && Diagnostics.HasErrors)
            return false;
        if (Diagnostics.ErrorCount >= MaxErrors)
            return false;
        return true;
    }

}
