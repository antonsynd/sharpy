using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic.Validation;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Type checks expressions and statements
/// </summary>
public partial class TypeChecker
{
    private readonly SymbolTable _symbolTable;
    private readonly SemanticInfo _semanticInfo;
    private readonly TypeResolver _typeResolver;
    private readonly ControlFlowValidator _controlFlowValidator;
    private readonly AccessValidator _accessValidator;
    private readonly OperatorValidator _operatorValidator;
    // Used for protocol validation (iterability, membership, indexing, len)
    private readonly ProtocolValidator _protocolValidator;
    // Used for validating default parameter values
    private readonly DefaultParameterValidator _defaultParameterValidator;
    private readonly ICompilerLogger _logger;
    private readonly List<SemanticError> _errors = new();

    // Validation pipeline support
    private readonly ValidationPipeline? _validationPipeline;
    private readonly bool _usePipeline;

    // Track current function return type for return statement checking
    private SemanticType? _currentFunctionReturnType = null;

    // Track current class being checked (for self parameter typing)
    private TypeSymbol? _currentClass = null;

    // Track type narrowing in conditional contexts
    private Dictionary<string, SemanticType> _narrowedTypes = new();

    // Track whether we're inside an except block (for bare raise validation)
    private bool _inExceptBlock = false;

    // Track current method context for super() validation
    private string? _currentMethodName = null;
    private bool _currentMethodIsOverride = false;
    private bool _currentMethodIsDunder = false;
    private int _controlFlowDepth = 0;
    private bool _superInitCalled = false;  // Track if super().__init__() was called

    // Configuration
    public bool ContinueAfterError { get; set; } = true;
    public int MaxErrors { get; set; } = 100;

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
        _validationPipeline = validationPipeline;
        _usePipeline = validationPipeline != null;

        _controlFlowValidator = new ControlFlowValidator(_logger);
        _accessValidator = new AccessValidator(_symbolTable, _semanticInfo, _logger);

        // Create shared CLR member cache for efficient reflection caching across validators
        var sharedClrCache = new ClrMemberCache();

        _protocolValidator = new ProtocolValidator(_symbolTable, _logger, sharedClrCache);
        // Pass ProtocolValidator to OperatorValidator for 'in' operator membership checking
        _operatorValidator = new OperatorValidator(_symbolTable, _logger, _protocolValidator, sharedClrCache);
        _defaultParameterValidator = new DefaultParameterValidator(_symbolTable, _typeResolver, _logger);
    }

    /// <summary>
    /// Creates a SemanticContext for use with the validation pipeline.
    /// </summary>
    public SemanticContext CreateSemanticContext()
    {
        return new SemanticContext(_symbolTable, _semanticInfo, _typeResolver, _logger);
    }

    /// <summary>
    /// Gets all semantic errors from type checking.
    /// When using the validation pipeline, errors from V2 validators are automatically merged.
    /// When not using the pipeline (legacy mode), errors are collected from individual validators.
    /// </summary>
    /// <remarks>
    /// MIGRATION NOTE: Legacy mode (collecting errors from individual validators) is deprecated.
    /// Use ValidationPipelineFactory.CreateDefault() to enable pipeline mode.
    /// All validators should be migrated to the ISemanticValidator interface.
    /// See ControlFlowValidatorV2 as the reference implementation.
    /// </remarks>
    public IReadOnlyList<SemanticError> Errors
    {
        get
        {
            // Combine errors from type checker, type resolver, and validators.
            var allErrors = new List<SemanticError>(_errors);
            allErrors.AddRange(_typeResolver.Errors);

            if (!_usePipeline)
            {
                // DEPRECATED: Legacy behavior - collect from individual validators.
                // This path will be removed once all validators are migrated to the pipeline.
                // To use the new pipeline, pass a ValidationPipeline to the TypeChecker constructor.
                allErrors.AddRange(_controlFlowValidator.Errors);
                // AccessValidator errors now reported via AccessValidatorV2 in pipeline
                allErrors.AddRange(_operatorValidator.Errors);
                allErrors.AddRange(_protocolValidator.Errors);
                allErrors.AddRange(_defaultParameterValidator.Errors);
            }
            // When using pipeline, errors are merged in CheckModule

            return allErrors;
        }
    }

    /// <summary>
    /// Type check all statements in a module
    /// </summary>
    public void CheckModule(Module module)
    {
        _logger.LogInfo("Type checking module");

        foreach (var statement in module.Body)
        {
            CheckStatement(statement);
        }

        // If pipeline is configured, run additional validators
        if (_usePipeline && _validationPipeline != null)
        {
            var context = CreateSemanticContext();
            context.MergeFromLegacyErrors(_errors);
            context.MergeFromLegacyErrors(_typeResolver.Errors);

            _validationPipeline.Validate(module, context);

            // Merge pipeline diagnostics back to legacy error list
            foreach (var error in context.Diagnostics.GetErrors())
            {
                if (!_errors.Any(e => e.Message == error.Message && e.Line == error.Line))
                {
                    _errors.Add(new SemanticError(error.Message, error.Line, error.Column));
                }
            }
        }
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

            case Assignment assignment:
                CheckAssignment(assignment);
                break;

            case VariableDeclaration varDecl:
                CheckVariableDeclaration(varDecl);
                break;

            case ReturnStatement returnStmt:
                CheckReturn(returnStmt);
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

            // TODO: When 'with' statement is implemented, ensure it creates its own scope
            // similar to try/except/finally blocks. The context manager's __enter__ and
            // __exit__ should be called, and the body should be in its own scope.

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

            default:
                _logger.LogWarning($"Unhandled statement type: {statement.GetType().Name}", 0, 0);
                break;
        }
    }
}
