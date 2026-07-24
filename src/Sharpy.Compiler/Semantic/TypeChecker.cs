using Sharpy.Compiler.Analysis.ControlFlow;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic.Registry;
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

    // Shared, per-compilation control flow graph cache. Owned here so the same instance is
    // reachable from the TypeChecker side (for future narrowing dataflow) and threaded onto the
    // SemanticContext so the CFG-consuming validators reuse graphs instead of rebuilding them.
    private readonly ControlFlowGraphCache _controlFlowGraphs = new();

    // Track current function return type for return statement checking
    private SemanticType? _currentFunctionReturnType = null;

    // Expected type for constructor inference (Some/None()/Ok/Err)
    // Set temporarily when checking initializers, return values, and arguments
    private SemanticType? _expectedType = null;

    // Track current class being checked (for self parameter typing)
    private TypeSymbol? _currentClass = null;

    // Track type narrowing in conditional contexts with proper scope isolation.
    // After #1042 (P5.3) this carries ONLY expression-level narrowings that the statement-level
    // CFG dataflow cannot model — the `and` right-hand side and match-arm/pattern scopes. All
    // control-flow (if/elif/else/while/assert/early-exit) narrowing now flows through
    // <see cref="_narrowingFlow"/>.
    private readonly TypeNarrowingContext _narrowingContext = new();

    // Statement-level type-narrowing facts for the current function/module body, computed once at
    // body entry by NarrowingFlowAnalysis over the CFG (#1042). Null outside any analysed body.
    private Analysis.ControlFlow.NarrowingFlowResult? _narrowingFlow;

    // The narrowing facts in effect at the statement (or branch condition) currently being checked.
    // Read sites resolve these against live types. Threaded by CheckStatement / the compound-statement
    // condition checks; reset to empty when crossing into a nested body (function/lambda) that has its
    // own CFG.
    private IReadOnlyCollection<Analysis.ControlFlow.NarrowingFact> _currentFacts =
        System.Array.Empty<Analysis.ControlFlow.NarrowingFact>();

    // The expression currently being read as the operand of a type test (`x is (not) None`,
    // `isinstance(x, T)`). Type-test operands observe the honest, un-narrowed value: the read
    // sites skip narrowing for exactly this node, so a redundant re-test of an already-narrowed
    // value neither acquires an accessor (which would degenerate to `x.Value != null` — CS0472)
    // nor presupposes the very fact the test is checking (an isinstance operand cast).
    private Expression? _typeTestOperand;

    // Track whether we're inside an except block (for bare raise validation)
    private bool _inExceptBlock = false;

    // Track whether we're inside a finally block (for ? operator validation)
    private bool _inFinally = false;

    // Track whether we're inside an except* block (PEP 654 — restricts break/continue/return)
    private bool _inExceptStarBlock = false;

    // Track current method context for super() validation
    private string? _currentMethodName = null;
    private bool _currentMethodIsOverride = false;
    private bool _currentMethodIsDunder = false;
    private bool _currentFunctionIsGenerator = false;
    private bool _currentFunctionIsAsync = false;
    private int _controlFlowDepth = 0;
    private bool _superInitCalled = false;  // Track if super().__init__() was called

    // Symbols of range(...)-loop induction variables currently in scope that are provably >= 0
    // (single-arg range, or non-negative-literal start with positive step) AND not reassigned in
    // the loop body. Used to prove non-negative list indices for IndexAccessLowering.NativeUnchecked
    // (#1052). Uses reference equality (Symbol overrides it), so nested loops with distinct symbols
    // never collide even when they share a variable name.
    private readonly HashSet<Symbol> _nonNegativeInductionVars = new();

    // Symbols whose C# emission is guaranteed to be a concrete Sharpy.List<T> (so it has the
    // GetItemUnchecked accessor): non-variadic list-typed parameters and locals declared with an
    // explicit list[T] annotation. This excludes list[T] values that are actually backed by a CLR
    // array (e.g. *args, or interop returns) or narrowed to Sharpy.IList — those emit without a
    // GetItemUnchecked method, so tagging them NativeUnchecked would not compile (#1052).
    private readonly HashSet<Symbol> _sharpyListBackedSymbols = new();

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

    /// <summary>
    /// Enabled experimental feature flags for this compilation. Semantic/codegen-scoped
    /// features consult this via <see cref="Shared.FeatureFlags.IsEnabled"/> to alter
    /// analysis. Compilation-wide flags flow in here from <see cref="CompilerOptions.Features"/>;
    /// per-file <c>from __future__ import</c> flags are unioned in during import resolution.
    /// </summary>
    public Shared.FeatureFlags Features { get; set; } = Shared.FeatureFlags.None;

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
    /// Symbol names imported from deferred circular imports (stubs).
    /// Passed through to <see cref="SemanticContext"/> for the validation pipeline.
    /// </summary>
    public IReadOnlySet<string>? DeferredCycleSymbols { get; set; }

    /// <summary>
    /// File paths involved in deferred circular import cycles.
    /// </summary>
    public IReadOnlySet<string>? DeferredCycleFiles { get; set; }

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
        // Route Engine B (operator dunders, __getitem__) through the shared deterministic
        // betterness core so overload resolution is order-independent (#975).
        _typeInference.DeterministicBinaryOverloadResolver = ResolveDunderOverload;

        // Initialize generic type argument inference service
        _genericInference = new GenericTypeInferenceService(_symbolTable, typeResolver);
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
        // Share the inference service so validators resolve operators with identical rules
        context.TypeInference = _typeInference;
        // Share the CFG cache so validators reuse graphs (and P5.2 can reach the same instance)
        context.ControlFlowGraphs = _controlFlowGraphs;
        context.DeferredCycleSymbols = DeferredCycleSymbols;
        context.DeferredCycleFiles = DeferredCycleFiles;
        // Thread the effective feature flags so validators can gate feature-conditional
        // diagnostics. No validator consults this today (the SPY0479 to→as?/as! hint went
        // unconditional when failable_cast graduated (#1096) and was then removed along with
        // the `to` operator, #1127); retained deliberately for future feature-conditional
        // diagnostics — see SemanticContext.Features.
        context.Features = Features;
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
    private IEnumerable<TypeSymbol> GetInterfaces(TypeSymbol symbol)
        => TypeHierarchyService.GetAllInterfaces(symbol, SemanticBinding);

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
    /// Optional module registry for inline CLR namespace resolution (e.g., `System`.Console).
    /// When set, backtick-escaped identifiers that fail symbol-table lookup are resolved against
    /// the registry's known .NET namespaces lazily, without requiring an explicit import.
    /// </summary>
    internal ModuleRegistry? ModuleRegistry { get; set; }

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

        // Pre-pass: resolve return types and parameter types for all module-level functions
        // so that forward references from class methods have resolved type information.
        // The NameResolver pre-pass registers function names, but types remain Unknown
        // until the TypeChecker processes each function. Without this pre-pass, a class
        // method calling a function defined later in the file would see Unknown types.
        foreach (var statement in module.Body)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (statement is FunctionDef functionDef)
            {
                ResolveModuleFunctionSignature(functionDef);
            }
        }

        // Compute statement-level narrowing facts for the module body (#1042). Module-level code is
        // its own narrowing scope; nested functions/lambdas get their own flow when checked.
        var previousFlow = _narrowingFlow;
        var previousFacts = _currentFacts;
        _narrowingFlow = ComputeNarrowingFlow(module.Body);
        _currentFacts = System.Array.Empty<Analysis.ControlFlow.NarrowingFact>();

        foreach (var statement in module.Body)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            CheckStatement(statement);
        }

        _narrowingFlow = previousFlow;
        _currentFacts = previousFacts;

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

        // Apply scoped @suppress (#1024): drop suppressible diagnostics that fall inside a
        // suppressor's region, then flag suppressors that silenced nothing. Runs here, after
        // the validation merge, because only now do warnings from every phase live in one bag.
        ApplyScopedSuppression(module);

        // Compute CodeGenInfo for all symbols if enabled
        if (computeCodeGenInfo)
        {
            var codeGenInfoComputer = new CodeGenInfoComputer(_symbolTable, SemanticBinding, _diagnostics);
            codeGenInfoComputer.ComputeForModule(module, _currentFilePath);
        }

        _logger.LogInfo($"Completed type checking ({module.Body.Length} statements, {_diagnostics.ErrorCount} errors)");
    }

    /// <summary>
    /// Applies scoped <c>@suppress</c> suppression (#1024) over the per-file diagnostic bag:
    /// removes every suppressible diagnostic (Warning/Hint/Info — including a warning promoted to
    /// an error under <c>-Werror</c>) whose code is named by an enclosing suppressor and whose
    /// location lies within that suppressor's region, then emits SPY0481 for suppressors that
    /// silenced nothing. Errors are never removed. SPY0481 is skipped when the file still has
    /// errors, so a broken file does not also nag about ineffective suppressions.
    /// </summary>
    private void ApplyScopedSuppression(Module module)
    {
        var regions = SuppressionCollector.Collect(module);
        if (regions.Count == 0)
            return;

        _diagnostics.RemoveWhere(diagnostic =>
        {
            if (string.IsNullOrEmpty(diagnostic.Code) || !IsSuppressibleSeverity(diagnostic))
                return false;

            foreach (var region in regions)
            {
                if (region.Codes.Contains(diagnostic.Code) && region.Contains(diagnostic))
                {
                    region.DroppedAny = true;
                    return true;
                }
            }

            return false;
        });

        if (_diagnostics.ErrorCount != 0)
            return;

        foreach (var region in regions)
        {
            if (region.DroppedAny)
                continue;

            _diagnostics.AddWarning(
                "suppression has no effect: no matching diagnostic was reported in this scope",
                region.DecoratorSpan,
                region.DecoratorLine,
                region.DecoratorColumn,
                _currentFilePath,
                DiagnosticCodes.Validation.UnusedSuppression,
                CompilerPhase.Validation);
        }
    }

    /// <summary>
    /// True when <paramref name="diagnostic"/> may be silenced by <c>@suppress</c>: any
    /// non-error diagnostic, plus a warning that <c>-Werror</c> promoted to an error (recognized
    /// via the original severity stamped into <see cref="DiagnosticBag.OriginalSeverityDataKey"/>).
    /// A genuine error is never suppressible.
    /// </summary>
    private static bool IsSuppressibleSeverity(CompilerDiagnostic diagnostic)
    {
        if (!diagnostic.IsError)
            return true;

        return diagnostic.Data != null
            && diagnostic.Data.TryGetValue(DiagnosticBag.OriginalSeverityDataKey, out var original)
            && original == nameof(CompilerDiagnosticSeverity.Warning);
    }

    private void CheckStatement(Statement statement)
    {
        // Thread the statement-level narrowing facts (#1042). Simple statements are tracked by the
        // flow analysis; compound statements (if/while/for/…) are not, so they inherit the enclosing
        // facts and their header expressions override via FactsBeforeBranch (see CheckIf/CheckWhile/
        // CheckFor). Save/restore keeps nested statement checks from leaking facts back to the parent.
        var savedFacts = _currentFacts;
        if (_narrowingFlow != null && _narrowingFlow.IsTracked(statement))
            _currentFacts = _narrowingFlow.FactsBefore(statement);

        try
        {
            CheckStatementCore(statement);
        }
        finally
        {
            _currentFacts = savedFacts;
        }
    }

    private void CheckStatementCore(Statement statement)
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

            case DeferStatement deferStmt:
                CheckDefer(deferStmt);
                break;

            case AssertStatement assertStmt:
                CheckAssert(assertStmt);
                break;

            case ExpressionStatement exprStmt:
                CheckExpression(exprStmt.Expression);
                break;

            case DecoratedStatement decorated:
                // @suppress wrapper (#1024): decorators are compile-time-only; check the inner statement.
                CheckStatement(decorated.Statement);
                break;

            case PassStatement:
                // No type checking needed
                break;

            case BreakStatement breakStmt:
                if (_inExceptStarBlock)
                {
                    AddError("'break' is not allowed inside 'except*' handler",
                        breakStmt.LineStart, breakStmt.ColumnStart,
                        code: DiagnosticCodes.Semantic.BreakInExceptStar,
                        span: breakStmt.Span);
                }
                break;

            case ContinueStatement continueStmt:
                if (_inExceptStarBlock)
                {
                    AddError("'continue' is not allowed inside 'except*' handler",
                        continueStmt.LineStart, continueStmt.ColumnStart,
                        code: DiagnosticCodes.Semantic.ContinueInExceptStar,
                        span: continueStmt.Span);
                }
                break;

            case ImportStatement:
            case FromImportStatement:
                // Import validation handled elsewhere
                break;

            case TypeAlias typeAlias:
                // Register function-scoped type aliases so they're visible
                // to subsequent statements in the same function body
                RegisterScopedTypeAlias(typeAlias);
                break;

            case PropertyDef propDef when _currentClass == null:
                // Module-level property: resolve its type and check the accessor
                // body / default value (#844)
                CheckModuleProperty(propDef);
                break;

            case PropertyDef propDef when _currentClass != null:
                // Class/struct/interface property: declared types are resolved via
                // ResolvePropertyTypes; check the accessor body / default value (#849)
                CheckClassProperty(propDef);
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
