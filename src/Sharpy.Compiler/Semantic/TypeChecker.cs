using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic.Validation;
using Sharpy.Compiler.Services;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Type checks expressions and statements
/// </summary>
public partial class TypeChecker
{
    private readonly SymbolTable _symbolTable;
    private readonly SemanticInfo _semanticInfo;
    private readonly TypeResolver _typeResolver;

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

    // Optional CompilerServices for centralized access
    private readonly CompilerServices? _services;

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
        // Prefer using CompilerServices if available
        if (_services != null)
        {
            return new SemanticContext(_services);
        }
        return new SemanticContext(_symbolTable, _semanticInfo, _typeResolver, _logger);
    }

    /// <summary>
    /// Gets all semantic errors from type checking and validation.
    /// </summary>
    /// <remarks>
    /// Errors come from:
    /// 1. Direct TypeChecker errors (_errors) - includes type mismatch, undefined symbols
    /// 2. TypeResolver errors (unresolved types) - merged in CheckModule
    /// 3. V2 validators via ValidationPipeline (control flow, access, operators, protocols) - merged in CheckModule
    ///
    /// Legacy validators are still instantiated but their errors are no longer collected here.
    /// They remain for backward compatibility with code that calls their validation methods directly.
    /// Error reporting is now handled by V2 validators and direct TypeChecker error reporting.
    /// </remarks>
    public IReadOnlyList<SemanticError> Errors => _errors;

    /// <summary>
    /// Type check all statements in a module
    /// </summary>
    /// <param name="module">The module to check</param>
    /// <param name="computeCodeGenInfo">
    /// If true, compute CodeGenInfo for all symbols after type checking.
    /// This is required for code generation to work correctly.
    /// </param>
    public void CheckModule(Module module, bool computeCodeGenInfo = false)
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

        // Merge TypeResolver errors into _errors (they are not covered by V2 validators)
        foreach (var error in _typeResolver.Errors)
        {
            bool isDuplicate = _errors.Any(e =>
                e.Line == error.Line && e.Message == error.Message);
            if (!isDuplicate)
            {
                _errors.Add(error);
            }
        }

        // Merge pipeline diagnostics back to _errors
        // Note: SemanticError.Message is formatted with "Semantic error at line X:" prefix,
        // while CompilerDiagnostic.Message is raw. We need to check if the raw message
        // is contained in the formatted message for proper deduplication.
        foreach (var error in context.Diagnostics.GetErrors())
        {
            bool isDuplicate = _errors.Any(e =>
                e.Line == error.Line &&
                (e.Message == error.Message ||
                 e.Message.EndsWith(": " + error.Message) ||
                 // Handle similar operator error messages (e.g., "with operand" vs "with right operand")
                 (e.Message.Contains("does not support operator") &&
                  error.Message.Contains("does not support operator"))));
            if (!isDuplicate)
            {
                _errors.Add(new SemanticError(error.Message, error.Line, error.Column));
            }
        }

        // Compute CodeGenInfo for all symbols if enabled
        if (computeCodeGenInfo)
        {
            var codeGenInfoComputer = new CodeGenInfoComputer(_symbolTable);
            codeGenInfoComputer.ComputeForModule(module);
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
