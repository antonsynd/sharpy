using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Services;

/// <summary>
/// Central services container used throughout compilation.
/// Provides caching, logging, and common operations.
///
/// Design notes:
/// - Thread-safe for future parallel compilation
/// - Services are lazily initialized where possible
/// - Configuration is immutable after construction
/// </summary>
public class CompilerServices
{
    private readonly CompilerServicesConfiguration _config;

    // Logging
    public ICompilerLogger Logger { get; }

    // Diagnostics
    public IDiagnosticReporter DiagnosticReporter { get; }

    // Type services
    public ITypeResolver TypeResolver { get; }

    // Symbol services
    public ISymbolLookup SymbolLookup { get; }

    // CLR interop
    public IClrTypeMapper ClrMapper { get; }

    // Underlying infrastructure (for components that need direct access during migration)
    public SymbolTable SymbolTable { get; }
    public SemanticInfo SemanticInfo { get; }

    // Configuration
    internal CompilerServicesConfiguration Configuration => _config;

    /// <summary>
    /// Optional SemanticBinding for reading inheritance data.
    /// When set, helpers prefer this over direct symbol property access.
    /// </summary>
    public SemanticBinding SemanticBinding { get; internal set; } = new();

    /// <summary>
    /// Current file path being processed. Can be updated as compilation progresses.
    /// </summary>
    public string? CurrentFilePath
    {
        get => DiagnosticReporter.CurrentFilePath;
        set => DiagnosticReporter.CurrentFilePath = value;
    }

    internal CompilerServices(
        CompilerServicesConfiguration config,
        ICompilerLogger logger,
        SymbolTable symbolTable,
        SemanticInfo semanticInfo,
        ITypeResolver typeResolver,
        ISymbolLookup symbolLookup,
        IClrTypeMapper clrMapper,
        IDiagnosticReporter diagnosticReporter)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        SymbolTable = symbolTable ?? throw new ArgumentNullException(nameof(symbolTable));
        SemanticInfo = semanticInfo ?? throw new ArgumentNullException(nameof(semanticInfo));
        TypeResolver = typeResolver ?? throw new ArgumentNullException(nameof(typeResolver));
        SymbolLookup = symbolLookup ?? throw new ArgumentNullException(nameof(symbolLookup));
        ClrMapper = clrMapper ?? throw new ArgumentNullException(nameof(clrMapper));
        DiagnosticReporter = diagnosticReporter ?? throw new ArgumentNullException(nameof(diagnosticReporter));

        // Apply initial configuration
        if (config.InitialFilePath != null)
        {
            CurrentFilePath = config.InitialFilePath;
        }
    }

    // =========================================================================
    // Convenience methods for common operations
    // =========================================================================

    /// <summary>
    /// Resolve a type annotation to its semantic type.
    /// Convenience wrapper around TypeResolver.ResolveTypeAnnotation.
    /// </summary>
    public SemanticType ResolveType(TypeAnnotation? annotation)
    {
        return TypeResolver.ResolveTypeAnnotation(annotation);
    }

    /// <summary>
    /// Look up a symbol by name in the current scope and parent scopes.
    /// Convenience wrapper around SymbolLookup.Lookup.
    /// </summary>
    public Symbol? LookupSymbol(string name)
    {
        return SymbolLookup.Lookup(name);
    }

    /// <summary>
    /// Check if one type is assignable to another.
    /// </summary>
    public bool CanAssign(SemanticType from, SemanticType to)
    {
        if (from == null || to == null)
            return false;
        if (from == SemanticType.Unknown || to == SemanticType.Unknown)
            return true;
        if (from.Equals(to))
            return true;

        // Handle nullable assignment (T can be assigned to T?)
        if (to is NullableType nullableTo)
        {
            return CanAssign(from, nullableTo.UnderlyingType);
        }

        // Handle None to nullable
        if (from == SemanticType.Void && to is NullableType)
        {
            return true;
        }

        // Handle inheritance
        if (from is UserDefinedType fromUdt && to is UserDefinedType toUdt)
        {
            return IsSubtypeOf(fromUdt.Symbol, toUdt.Symbol);
        }

        // Handle numeric widening (int -> long, float32 -> float64)
        if (IsNumericWidening(from, to))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Report an error with source location.
    /// Convenience wrapper around DiagnosticReporter.ReportError.
    /// </summary>
    public void ReportError(string message, Node location)
    {
        DiagnosticReporter.ReportError(message, location);
    }

    /// <summary>
    /// Report an error with optional line/column.
    /// </summary>
    public void ReportError(string message, int? line = null, int? column = null)
    {
        DiagnosticReporter.ReportError(message, line, column);
    }

    /// <summary>
    /// Check if we should continue processing (based on error count).
    /// </summary>
    public bool ShouldContinue()
    {
        if (!_config.ContinueAfterErrors && DiagnosticReporter.HasErrors)
            return false;
        if (DiagnosticReporter.Diagnostics.ErrorCount >= _config.MaxErrors)
            return false;
        return true;
    }

    // =========================================================================
    // Private helper methods
    // =========================================================================

    private TypeSymbol? GetBaseType(TypeSymbol symbol)
        => SemanticBinding.GetBaseType(symbol) ?? symbol.BaseType;

    private IReadOnlyList<TypeSymbol> GetInterfaces(TypeSymbol symbol)
        => SemanticBinding.GetInterfaces(symbol) ?? (IReadOnlyList<TypeSymbol>)symbol.Interfaces;

    private bool IsSubtypeOf(TypeSymbol? derived, TypeSymbol? baseType)
    {
        if (derived == null || baseType == null)
            return false;
        if (derived == baseType)
            return true;

        // Check direct base type
        var derivedBase = GetBaseType(derived);
        if (derivedBase != null && IsSubtypeOf(derivedBase, baseType))
            return true;

        // Check interfaces
        foreach (var iface in GetInterfaces(derived))
        {
            if (IsSubtypeOf(iface, baseType))
                return true;
        }

        return false;
    }

    private bool IsNumericWidening(SemanticType from, SemanticType to)
    {
        // int -> long
        if (from == SemanticType.Int && to == SemanticType.Long)
            return true;
        // float32 -> float64/double
        if (from == SemanticType.Float32 && (to == SemanticType.Double || to == SemanticType.Float))
            return true;
        // int -> float64/double
        if (from == SemanticType.Int && (to == SemanticType.Double || to == SemanticType.Float))
            return true;

        return false;
    }
}
