using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;

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
public class SemanticContext
{
    // Core semantic data
    public SymbolTable SymbolTable { get; }
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

    // State tracking for validators
    public TypeSymbol? CurrentClass { get; set; }
    public FunctionSymbol? CurrentFunction { get; set; }
    public bool InLoop { get; set; }
    public int LoopDepth { get; set; }

    public SemanticContext(
        SymbolTable symbolTable,
        SemanticInfo semanticInfo,
        TypeResolver typeResolver,
        ICompilerLogger? logger = null)
    {
        SymbolTable = symbolTable;
        SemanticInfo = semanticInfo;
        TypeResolver = typeResolver;
        Logger = logger ?? NullLogger.Instance;

        Diagnostics = new DiagnosticBag();
        ClrCache = new ClrMemberCache();
    }

    /// <summary>
    /// Create a context with shared infrastructure but fresh diagnostics.
    /// Useful for validating individual files in a project.
    /// </summary>
    public SemanticContext CreateForFile(string filePath)
    {
        return new SemanticContext(SymbolTable, SemanticInfo, TypeResolver, Logger)
        {
            CurrentFilePath = filePath,
            ContinueAfterErrors = ContinueAfterErrors,
            MaxErrors = MaxErrors,
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

    /// <summary>
    /// Merge diagnostics from a legacy validator's error list.
    /// Use during migration period.
    /// </summary>
    public void MergeFromLegacyErrors(IEnumerable<SemanticError> errors)
    {
        foreach (var error in errors)
        {
            // Extract message without the "Semantic error at line X:" prefix
            var message = error.Message;
            if (message.StartsWith("Semantic error"))
            {
                var colonIdx = message.IndexOf(": ");
                if (colonIdx >= 0)
                    message = message.Substring(colonIdx + 2);
            }
            Diagnostics.AddError(message, error.Line, error.Column, CurrentFilePath);
        }
    }
}
