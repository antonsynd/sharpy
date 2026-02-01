using Sharpy.Compiler.Diagnostics;
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

    // Whether the current module is an entry point file
    private bool _isEntryPoint = false;

    // Current file path for diagnostic location
    private string? _currentFilePath = null;

    // Optional CompilerServices for centralized access
    private readonly CompilerServices? _services;

    /// <summary>
    /// Optional SemanticBinding for dual-write of CodeGenInfo during computation.
    /// </summary>
    public SemanticBinding SemanticBinding { get; set; } = new();

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
    /// Gets the base type for a TypeSymbol, preferring SemanticBinding when available.
    /// Falls back to symbol.BaseType for backward compatibility during migration.
    /// </summary>
    private TypeSymbol? GetBaseType(TypeSymbol symbol)
        => SemanticBinding.GetBaseType(symbol) ?? symbol.BaseType;

    /// <summary>
    /// Gets the interfaces for a TypeSymbol, preferring SemanticBinding when available.
    /// Falls back to symbol.Interfaces for backward compatibility during migration.
    /// </summary>
    private IReadOnlyList<TypeSymbol> GetInterfaces(TypeSymbol symbol)
        => SemanticBinding.GetInterfaces(symbol) ?? (IReadOnlyList<TypeSymbol>)symbol.Interfaces;

    /// <summary>
    /// Gets the type for a VariableSymbol, preferring SemanticBinding when available.
    /// Falls back to symbol.Type for backward compatibility during migration.
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
    /// Current file path for diagnostic location. Set by the compiler before calling CheckModule.
    /// </summary>
    public string? CurrentFilePath
    {
        get => _currentFilePath;
        set => _currentFilePath = value;
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
    public void CheckModule(Module module, bool computeCodeGenInfo = false, bool isEntryPoint = false)
    {
        _logger.LogInfo("Type checking module");
        _isEntryPoint = isEntryPoint;

        // Propagate SemanticBinding to sub-services
        _genericInference.SemanticBinding = SemanticBinding;

        foreach (var statement in module.Body)
        {
            CheckStatement(statement);
        }

        // Run pipeline validators (always enabled)
        var context = CreateSemanticContext();
        context.Diagnostics.Merge(_diagnostics);
        context.Diagnostics.Merge(_typeResolver.Diagnostics);

        var diagnosticCountBeforePipeline = context.Diagnostics.GetAll().Count;

        _validationPipeline.Validate(module, context);

        // Merge TypeResolver diagnostics
        _diagnostics.Merge(_typeResolver.Diagnostics);

        // Merge only pipeline-added diagnostics (those added after the snapshot).
        // Dedup against existing errors to avoid duplicates where both TypeChecker
        // (via type inference) and OperatorValidatorV2 report the same issue.
        var existingErrors = _diagnostics.GetErrors();
        var exactErrors = new HashSet<(int?, int?, string)>(
            existingErrors.Select(e => (e.Line, e.Column, e.Message)));

        // Track positions of operator errors by diagnostic code (not message content)
        // to match near-duplicates where TypeChecker (SHP0222) and OperatorValidatorV2
        // (SHP0402) report the same operator issue with slightly different wording.
        var operatorErrorPositions = new HashSet<(int?, int?)>(
            existingErrors
                .Where(e => e.Code is DiagnosticCodes.Semantic.InvalidBinaryOperation
                                   or DiagnosticCodes.Validation.UnsupportedOperator)
                .Select(e => (e.Line, e.Column)));

        var allDiagnostics = context.Diagnostics.GetAll();
        for (int i = diagnosticCountBeforePipeline; i < allDiagnostics.Count; i++)
        {
            var diag = allDiagnostics[i];
            if (diag.IsError)
            {
                // Skip exact duplicates (same position and message)
                if (exactErrors.Contains((diag.Line, diag.Column, diag.Message)))
                    continue;
                // Skip near-duplicate operator errors at the same position
                if (diag.Code is DiagnosticCodes.Semantic.InvalidBinaryOperation
                              or DiagnosticCodes.Validation.UnsupportedOperator
                    && operatorErrorPositions.Contains((diag.Line, diag.Column)))
                    continue;
            }
            _diagnostics.Add(diag);
        }

        // Compute CodeGenInfo for all symbols if enabled
        if (computeCodeGenInfo)
        {
            var codeGenInfoComputer = new CodeGenInfoComputer(_symbolTable, SemanticBinding);
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
