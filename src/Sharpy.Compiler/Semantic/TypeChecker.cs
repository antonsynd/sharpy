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

    // Validation pipeline - always enabled (default created if not provided)
    private readonly ValidationPipeline _validationPipeline;

    // Type inference service - extracted from validators for clean separation
    private readonly TypeInferenceService _typeInference;

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
        _validationPipeline = validationPipeline ?? ValidationPipelineFactory.CreateDefault(logger);

        _controlFlowValidator = new ControlFlowValidator(_logger);
        _accessValidator = new AccessValidator(_symbolTable, _semanticInfo, _logger);

        // Create shared CLR member cache for efficient reflection caching across validators
        var sharedClrCache = new ClrMemberCache();

        _protocolValidator = new ProtocolValidator(_symbolTable, _logger, sharedClrCache);
        // Pass ProtocolValidator to OperatorValidator for 'in' operator membership checking
        _operatorValidator = new OperatorValidator(_symbolTable, _logger, _protocolValidator, sharedClrCache);
        _defaultParameterValidator = new DefaultParameterValidator(_symbolTable, _typeResolver, _logger);

        // Initialize type inference service (uses same CLR cache)
        _typeInference = new TypeInferenceService(_symbolTable, sharedClrCache);
    }

    /// <summary>
    /// Creates a SemanticContext for use with the validation pipeline.
    /// </summary>
    public SemanticContext CreateSemanticContext()
    {
        return new SemanticContext(_symbolTable, _semanticInfo, _typeResolver, _logger);
    }

    /// <summary>
    /// Gets all semantic errors from type checking and validation.
    /// Errors are collected from multiple sources:
    /// 1. Direct TypeChecker errors (_errors)
    /// 2. TypeResolver errors
    /// 3. Legacy validators (still used for type inference)
    /// 4. V2 pipeline validators (merged during CheckModule)
    ///
    /// Duplicate errors are automatically filtered out.
    /// </summary>
    /// <remarks>
    /// MIGRATION NOTE: As validators are migrated to the V2 pipeline pattern,
    /// legacy validator errors will be phased out. See ControlFlowValidatorV2
    /// as the reference implementation for the new pattern.
    /// </remarks>
    public IReadOnlyList<SemanticError> Errors
    {
        get
        {
            // Combine errors from type checker and legacy validators.
            // When the pipeline runs (CheckModule), it merges _errors, _typeResolver.Errors,
            // and V2 validator errors back into _errors, so we start with those.
            // Legacy validators are still called during type checking for type inference,
            // so we also collect their errors with deduplication.
            var allErrors = new List<SemanticError>(_errors);

            // Collect errors from type resolver and legacy validators.
            // These may be duplicated if the pipeline ran (since CheckModule merges them),
            // so we deduplicate when adding.
            var legacyErrors = new List<SemanticError>();
            legacyErrors.AddRange(_typeResolver.Errors);
            legacyErrors.AddRange(_controlFlowValidator.Errors);
            legacyErrors.AddRange(_accessValidator.Errors);
            legacyErrors.AddRange(_operatorValidator.Errors);
            legacyErrors.AddRange(_protocolValidator.Errors);
            legacyErrors.AddRange(_defaultParameterValidator.Errors);

            // Deduplicate: only add legacy errors that aren't already in allErrors.
            // Compare by line number and raw message content (accounting for format differences).
            foreach (var legacyError in legacyErrors)
            {
                bool isDuplicate = allErrors.Any(e =>
                    e.Line == legacyError.Line &&
                    (e.Message == legacyError.Message ||
                     // Handle case where one message contains the other (format differences)
                     e.Message.Contains(ExtractRawMessage(legacyError.Message)) ||
                     legacyError.Message.Contains(ExtractRawMessage(e.Message))));
                if (!isDuplicate)
                {
                    allErrors.Add(legacyError);
                }
            }

            return allErrors;
        }
    }

    /// <summary>
    /// Extracts the raw error message from a formatted SemanticError message.
    /// Removes prefixes like "Semantic error at line X, column Y: ".
    /// </summary>
    private static string ExtractRawMessage(string message)
    {
        // Pattern: "Semantic error at line X, column Y: " or "Semantic error at line X: " or "Semantic error: "
        if (message.StartsWith("Semantic error"))
        {
            var colonIdx = message.IndexOf(": ");
            if (colonIdx >= 0 && colonIdx < message.Length - 2)
                return message.Substring(colonIdx + 2);
        }
        return message;
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

        // Run pipeline validators (always enabled)
        var context = CreateSemanticContext();
        context.MergeFromLegacyErrors(_errors);
        context.MergeFromLegacyErrors(_typeResolver.Errors);

        _validationPipeline.Validate(module, context);

        // Merge pipeline diagnostics back to legacy error list
        // Note: SemanticError.Message is formatted with "Semantic error at line X:" prefix,
        // while CompilerDiagnostic.Message is raw. We need to check if the raw message
        // is contained in the formatted message for proper deduplication.
        foreach (var error in context.Diagnostics.GetErrors())
        {
            bool isDuplicate = _errors.Any(e =>
                e.Line == error.Line &&
                (e.Message == error.Message || e.Message.EndsWith(": " + error.Message)));
            if (!isDuplicate)
            {
                _errors.Add(new SemanticError(error.Message, error.Line, error.Column));
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
