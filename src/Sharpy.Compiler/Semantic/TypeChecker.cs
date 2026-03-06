using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic.Validation;
using Sharpy.Compiler.Services;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Type checks expressions and statements
/// </summary>
internal partial class TypeChecker
{
    private readonly SymbolTable _symbolTable;
    private readonly SemanticInfo _semanticInfo;
    private readonly TypeResolver _typeResolver;

    private readonly ICompilerLogger _logger;
    private readonly DiagnosticBag _diagnostics = new();

    // Validation pipeline - always enabled (default created if not provided)
    private readonly ValidationPipeline _validationPipeline;

    // Type inference service - extracted from validators for clean separation
    private readonly TypeInferenceService _typeInference;

    // Generic type argument inference service - for inferring type arguments from function call arguments
    private readonly GenericTypeInferenceService _genericInference;

    // Track current function return type for return statement checking
    private SemanticType? _currentFunctionReturnType = null;

    // Expected type for constructor inference (Some/None()/Ok/Err)
    // Set temporarily when checking initializers, return values, and arguments
    private SemanticType? _expectedType = null;

    // Track current class being checked (for self parameter typing)
    private TypeSymbol? _currentClass = null;

    // Track type narrowing in conditional contexts with proper scope isolation
    private readonly TypeNarrowingContext _narrowingContext = new();

    // Track whether we're inside an except block (for bare raise validation)
    private bool _inExceptBlock = false;

    // Track current method context for super() validation
    private string? _currentMethodName = null;
    private bool _currentMethodIsOverride = false;
    private bool _currentMethodIsDunder = false;
    private bool _currentFunctionIsGenerator = false;
    private bool _currentFunctionIsAsync = false;
    private int _controlFlowDepth = 0;
    private bool _superInitCalled = false;  // Track if super().__init__() was called

    // Cancellation token for long-running analysis
    private CancellationToken _cancellationToken = default;

    // Counter for periodic cancellation checks in loops
    private int _cancellationCheckCounter;
    private const int CancellationCheckInterval = 100;

    // Counter for error recovery marks — used by CheckExpression to detect when
    // a sub-expression was marked as error recovery during the current evaluation,
    // enabling transitive propagation of error recovery status to parent expressions.
    private int _errorRecoveryMarkCount;

    // Configuration
    public bool ContinueAfterError { get; set; } = true;
    public int MaxErrors { get; set; } = 100;
    private bool _maxErrorsReported = false;

    // Whether the current module is an entry point file
    private bool _isEntryPoint = false;

    // Current file path for diagnostic location
    private string? _currentFilePath = null;

    // Optional CompilerServices for centralized access
    private readonly CompilerServices? _services;

    /// <summary>
    /// SemanticBinding for storing semantic data (CodeGenInfo, VariableType, inheritance).
    /// Writes go exclusively to SemanticBinding; Symbol properties are populated by materialization at freeze points.
    /// </summary>
    public SemanticBinding SemanticBinding { get; set; } = new();

    /// <summary>
    /// Per-validator timing data from the last validation pipeline run.
    /// Each key is a validator name, and each value is the time spent in that validator.
    /// This is populated after <see cref="CheckModule"/> completes.
    /// </summary>
    public IReadOnlyDictionary<string, TimeSpan>? ValidatorTimes { get; private set; }

    public TypeChecker(
        SymbolTable symbolTable,
        SemanticInfo semanticInfo,
        TypeResolver typeResolver,
        ICompilerLogger? logger = null,
        ValidationPipeline? validationPipeline = null)
    {
        _symbolTable = symbolTable;
        _semanticInfo = semanticInfo;
        _typeResolver = typeResolver;
        _logger = logger ?? NullLogger.Instance;
        _validationPipeline = validationPipeline ?? ValidationPipelineFactory.CreateDefault(logger);

        // Create shared CLR member cache for efficient reflection caching
        var sharedClrCache = new ClrMemberCache();

        // Initialize type inference service for inferring result types during type checking
        _typeInference = new TypeInferenceService(_symbolTable, sharedClrCache);

        // Initialize generic type argument inference service
        _genericInference = new GenericTypeInferenceService(_symbolTable);
    }

    /// <summary>
    /// Create TypeChecker with CompilerServices for centralized service access.
    /// Preferred constructor for new code.
    /// </summary>
    public TypeChecker(CompilerServices services, ValidationPipeline? validationPipeline = null)
        : this(
            services.SymbolTable,
            services.SemanticInfo,
            ((TypeResolverAdapter)services.TypeResolver).UnderlyingResolver,
            services.Logger,
            validationPipeline)
    {
        _services = services;
    }

    /// <summary>
    /// Creates a SemanticContext for use with the validation pipeline.
    /// </summary>
    public SemanticContext CreateSemanticContext()
    {
        SemanticContext context;

        // Prefer using CompilerServices if available
        if (_services != null)
        {
            context = new SemanticContext(_services);
        }
        else
        {
            context = new SemanticContext(_symbolTable, _semanticInfo, _typeResolver, _logger);
        }

        // Set entry point flag for module-level validation
        context.IsEntryPoint = _isEntryPoint;
        // Set file path for diagnostic location (if not already set by CompilerServices)
        if (context.CurrentFilePath == null && _currentFilePath != null)
            context.CurrentFilePath = _currentFilePath;
        // Thread SemanticBinding so validators can read from it
        context.SemanticBinding = SemanticBinding;
        return context;
    }

    /// <summary>
    /// Gets the base type for a TypeSymbol from SemanticBinding.
    /// Falls back to symbol.BaseType for symbols not tracked by this binding (e.g., CLR types).
    /// </summary>
    private TypeSymbol? GetBaseType(TypeSymbol symbol)
        => SemanticBinding.GetBaseType(symbol) ?? symbol.BaseType;

    /// <summary>
    /// Gets the interfaces for a TypeSymbol from SemanticBinding.
    /// Falls back to symbol.Interfaces for symbols not tracked by this binding (e.g., CLR types).
    /// </summary>
    private IReadOnlyList<TypeSymbol> GetInterfaces(TypeSymbol symbol)
        => TypeSymbol.GetAllInterfaces(symbol, SemanticBinding);

    /// <summary>
    /// Gets the type for a VariableSymbol from SemanticBinding.
    /// Falls back to symbol.Type for symbols not tracked by this binding.
    /// </summary>
    private SemanticType GetVariableType(VariableSymbol symbol)
    {
        var bindingType = SemanticBinding.GetVariableType(symbol);
        return bindingType != SemanticType.Unknown ? bindingType : symbol.Type;
    }

    /// <summary>
    /// Gets diagnostics from type checking, type resolution, and validation pipeline.
    /// </summary>
    public DiagnosticBag Diagnostics => _diagnostics;

    /// <summary>
    /// Imports root cause identifiers from another diagnostic bag.
    /// This allows TypeChecker to suppress cascading errors for identifiers
    /// that were already reported as root causes (e.g., from failed imports).
    /// </summary>
    /// <param name="sourceBag">The diagnostic bag containing root causes to import</param>
    public void ImportRootCauses(DiagnosticBag sourceBag)
    {
        foreach (var identifier in sourceBag.GetRootCauses())
        {
            _diagnostics.MarkAsRootCause(identifier);
        }
    }

    /// <summary>
    /// Current file path for diagnostic location. Set by the compiler before calling CheckModule.
    /// </summary>
    public string? CurrentFilePath
    {
        get => _currentFilePath;
        set => _currentFilePath = value;
    }

    /// <summary>
    /// Check for cancellation periodically in tight loops.
    /// Checking every iteration would be expensive, so we check every N iterations.
    /// </summary>
    private void CheckCancellation()
    {
        if (++_cancellationCheckCounter >= CancellationCheckInterval)
        {
            _cancellationCheckCounter = 0;
            _cancellationToken.ThrowIfCancellationRequested();
        }
    }

    /// <summary>
    /// Type check all statements in a module
    /// </summary>
    /// <param name="module">The module to check</param>
    /// <param name="computeCodeGenInfo">
    /// If true, compute CodeGenInfo for all symbols after type checking.
    /// This is required for code generation to work correctly.
    /// </param>
    /// <param name="isEntryPoint">
    /// If true, this module is the entry point (main executable file).
    /// Entry point files require a main() function.
    /// </param>
    /// <param name="cancellationToken">Optional cancellation token for LSP/IDE scenarios</param>
    public void CheckModule(Module module, bool computeCodeGenInfo = false, bool isEntryPoint = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInfo("Starting type checking");
        _isEntryPoint = isEntryPoint;
        _cancellationToken = cancellationToken;
        _cancellationCheckCounter = 0;

        // Propagate SemanticBinding to sub-services
        _genericInference.SemanticBinding = SemanticBinding;

        foreach (var statement in module.Body)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            CheckStatement(statement);
        }

        // Run pipeline validators (always enabled)
        var context = CreateSemanticContext();
        context.Diagnostics.Merge(_diagnostics);
        context.Diagnostics.Merge(_typeResolver.Diagnostics);

        _validationPipeline.Validate(module, context, out var validatorTimes);
        ValidatorTimes = validatorTimes;

        // Merge TypeResolver diagnostics
        _diagnostics.Merge(_typeResolver.Diagnostics);

        // Merge pipeline-added diagnostics. Validators are responsible for checking
        // whether an error already exists at a given position before adding new ones
        // (see OperatorValidator.HasErrorAtPosition). This prevents duplicate reporting
        // where TypeChecker (SPY0222) and OperatorValidator (SPY0402) both flag the
        // same operator issue.
        var diagnosticCountBeforeMerge = _diagnostics.GetAll().Count;
        var existingExact = new HashSet<(int?, int?, string)>(
            _diagnostics.GetAll().Select(e => (e.Line, e.Column, e.Message)));
        foreach (var diag in context.Diagnostics.GetAll())
        {
            // Skip diagnostics that were merged into the context at the start
            // (they are already in _diagnostics) — only add truly new ones.
            if (existingExact.Contains((diag.Line, diag.Column, diag.Message)))
                continue;
            _diagnostics.Add(diag);
        }

        // Compute CodeGenInfo for all symbols if enabled
        if (computeCodeGenInfo)
        {
            var codeGenInfoComputer = new CodeGenInfoComputer(_symbolTable, SemanticBinding, _diagnostics);
            codeGenInfoComputer.ComputeForModule(module);
        }

        _logger.LogInfo($"Completed type checking ({module.Body.Length} statements, {_diagnostics.ErrorCount} errors)");
    }

    private void CheckStatement(Statement statement)
    {
        switch (statement)
        {
            case FunctionDef functionDef:
                CheckFunction(functionDef);
                break;

            case ClassDef classDef:
                CheckClass(classDef);
                break;

            case StructDef structDef:
                CheckStruct(structDef);
                break;

            case InterfaceDef interfaceDef:
                CheckInterface(interfaceDef);
                break;

            case EnumDef enumDef:
                CheckEnum(enumDef);
                break;

            case UnionDef unionDef:
                CheckUnion(unionDef);
                break;

            case DelegateDef delegateDef:
                CheckDelegate(delegateDef);
                break;

            case Assignment assignment:
                CheckAssignment(assignment);
                break;

            case VariableDeclaration varDecl:
                CheckVariableDeclaration(varDecl);
                break;

            case ReturnStatement returnStmt:
                CheckReturn(returnStmt);
                break;

            case YieldStatement yieldStmt:
                CheckYield(yieldStmt);
                break;

            case IfStatement ifStmt:
                CheckIf(ifStmt);
                break;

            case WhileStatement whileStmt:
                CheckWhile(whileStmt);
                break;

            case ForStatement forStmt:
                CheckFor(forStmt);
                break;

            case RaiseStatement raiseStmt:
                CheckRaise(raiseStmt);
                break;

            case TryStatement tryStmt:
                CheckTry(tryStmt);
                break;

            case WithStatement withStmt:
                CheckWith(withStmt);
                break;

            case AssertStatement assertStmt:
                CheckAssert(assertStmt);
                break;

            case ExpressionStatement exprStmt:
                CheckExpression(exprStmt.Expression);
                break;

            case PassStatement:
            case BreakStatement:
            case ContinueStatement:
                // No type checking needed
                break;

            case ImportStatement:
            case FromImportStatement:
                // Import validation handled elsewhere
                break;

            case TypeAlias:
                // Type aliases are compile-time only, no type checking needed
                break;

            case PropertyDef:
                // Property validation handled elsewhere (property-specific validation)
                break;

            case EventDef eventDef:
                CheckEvent(eventDef);
                break;

            case MatchStatement matchStmt:
                CheckMatch(matchStmt);
                break;

            default:
                _logger.LogWarning($"Unhandled statement type: {statement.GetType().Name}", 0, 0);
                AddError(
                    $"Internal: unrecognized statement type '{statement.GetType().Name}'. This is a compiler bug — please report it.",
                    statement.LineStart,
                    statement.ColumnStart,
                    DiagnosticCodes.Semantic.UnrecognizedStatementType,
                    statement.Span);
                break;
        }
    }
}
