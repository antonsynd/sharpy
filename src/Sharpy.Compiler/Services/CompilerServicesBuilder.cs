using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Services;

/// <summary>
/// Builder for constructing CompilerServices with proper initialization.
/// Ensures all required components are available.
/// </summary>
public class CompilerServicesBuilder
{
    private CompilerServicesConfiguration _config = CompilerServicesConfiguration.Default;
    private ICompilerLogger? _logger;
    private SymbolTable? _symbolTable;
    private SemanticInfo? _semanticInfo;
    private TypeResolver? _typeResolver;
    private ClrMemberCache? _clrCache;

    /// <summary>
    /// Set the configuration.
    /// </summary>
    public CompilerServicesBuilder WithConfiguration(CompilerServicesConfiguration config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        return this;
    }

    /// <summary>
    /// Set the logger.
    /// </summary>
    public CompilerServicesBuilder WithLogger(ICompilerLogger? logger)
    {
        _logger = logger;
        return this;
    }

    /// <summary>
    /// Set the symbol table (required).
    /// </summary>
    public CompilerServicesBuilder WithSymbolTable(SymbolTable symbolTable)
    {
        _symbolTable = symbolTable ?? throw new ArgumentNullException(nameof(symbolTable));
        return this;
    }

    /// <summary>
    /// Set the semantic info (required).
    /// </summary>
    public CompilerServicesBuilder WithSemanticInfo(SemanticInfo semanticInfo)
    {
        _semanticInfo = semanticInfo ?? throw new ArgumentNullException(nameof(semanticInfo));
        return this;
    }

    /// <summary>
    /// Set an existing TypeResolver (optional - will be created if not provided).
    /// </summary>
    public CompilerServicesBuilder WithTypeResolver(TypeResolver typeResolver)
    {
        _typeResolver = typeResolver;
        return this;
    }

    /// <summary>
    /// Set an existing ClrMemberCache (optional - will be created if not provided).
    /// </summary>
    public CompilerServicesBuilder WithClrCache(ClrMemberCache clrCache)
    {
        _clrCache = clrCache;
        return this;
    }

    /// <summary>
    /// Build the CompilerServices instance.
    /// </summary>
    /// <exception cref="InvalidOperationException">If required components are missing.</exception>
    public CompilerServices Build()
    {
        // Validate required components
        if (_symbolTable == null)
            throw new InvalidOperationException("SymbolTable is required. Call WithSymbolTable() before Build().");
        if (_semanticInfo == null)
            throw new InvalidOperationException("SemanticInfo is required. Call WithSemanticInfo() before Build().");

        // Use defaults for optional components
        var logger = _logger ?? NullLogger.Instance;
        var clrCache = _clrCache ?? new ClrMemberCache();
        var typeResolver = _typeResolver ?? new TypeResolver(_symbolTable, _semanticInfo, logger);

        // Create adapters
        var typeResolverAdapter = new TypeResolverAdapter(typeResolver);
        var symbolLookupAdapter = new SymbolLookupAdapter(_symbolTable);
        var clrMapperAdapter = new ClrTypeMapperAdapter(clrCache);
        var diagnosticReporter = new DiagnosticReporter(logger);

        return new CompilerServices(
            _config,
            logger,
            _symbolTable,
            _semanticInfo,
            typeResolverAdapter,
            symbolLookupAdapter,
            clrMapperAdapter,
            diagnosticReporter);
    }

    /// <summary>
    /// Create a minimal CompilerServices for testing.
    /// Creates fresh SymbolTable and SemanticInfo.
    /// </summary>
    public static CompilerServices CreateForTesting(ICompilerLogger? logger = null)
    {
        var builtinRegistry = new BuiltinRegistry(logger);
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();

        return new CompilerServicesBuilder()
            .WithLogger(logger)
            .WithSymbolTable(symbolTable)
            .WithSemanticInfo(semanticInfo)
            .Build();
    }
}
