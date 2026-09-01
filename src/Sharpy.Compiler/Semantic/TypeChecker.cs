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
    //
    // The two sites that set this are CONDITIONAL: when the expression is not a type test they push
    // the field's current value, so an enclosing operand survives rather than being cleared. That is
    // a SAFETY PROPERTY, not an observable behavior, and no test can currently distinguish it —
    // the three read sites either freeze their ReferenceEquals decision before descending into
    // subexpressions (CheckMemberAccess, CheckIndexAccess) or are leaves (the identifier read), and a
    // scope restores the enclosing value on exit either way, so a divergence could only be observed
    // by a node that is its own descendant. If that read-before-descend ordering ever changes, the
    // conditional becomes load-bearing and nothing in the suite will tell you (#1218).
    private Expression? _typeTestOperand;

    // The TYPE argument of the type test currently being checked (`T` in `isinstance(x, T)`). It names
    // a type rather than denoting a value, so the value-position reference rules (SPY0337, #1170) skip
    // exactly this node — otherwise a union-variant type test would be reported as a variant
    // constructor used as a value. Scoped alongside _typeTestOperand, with the conjunction: the second
    // argument only names a type when there IS a second argument.
    private Expression? _typeTestTypeArgument;

    // The SUBJECT expression of the match statement or match expression currently being checked
    // (parentheses unwrapped, since the read lowering lands on the inner node). A match subject that
    // carries an isinstance narrowing records its narrowed TYPE as usual — #1299's pattern filling
    // reads it — but suppresses the Cast read lowering; see RecordNarrowedReadLowering for why
    // (#1370). Set only around the subject's own CheckExpression, so an arm body's reads and a nested
    // match's subject see their own state.
    private Expression? _matchSubjectOperand;

    // Whether the function body currently being checked is @test-decorated (or nested inside one,
    // matching how the emitter's _isInTestFunction propagates).
    private bool _inTestFunction;

    // The callee expression of the FunctionCall currently being checked (`call.Function`). A
    // generic function reference with explicit type args (`identity[int]`) is an internal carrier,
    // legal *only* as the immediate callee of a call. CheckExpression errors (SPY0335) whenever a
    // GenericFunctionType surfaces on any node that is not this callee, so uncalled references never
    // escape into a value context (#1138). Scoped around the callee check via ScopedValue.Push (see
    // Semantic/ScopedValue.cs, #1218); stored as a node reference so nested calls restore correctly.
    private Expression? _currentCallCallee;

    // What the CLR member-type resolver answered for each member access it was asked about, keyed by
    // node identity. Two consumers read it: the call seam, which turns a CALLEE-position property or
    // field into the call's type plus the zero-arg collapse (`s.count()`), and the permissive
    // channel, whose reason string names the resolver's own verdict rather than the whole seam
    // (#1640). Not SemanticInfo: nothing downstream of type checking reads it — the FACTS codegen
    // needs (the resolved CLR name, the property-call lowering) are materialized separately.
    private readonly Dictionary<MemberAccess, Discovery.ClrMemberResolution> _clrMemberResolutions =
        new(ReferenceEqualityComparer.Instance);

    // The qualifier expression of the MemberAccess currently being checked (`memberAccess.Object`).
    // A generic TYPE reference is legal there — `Box[int].of(42)`, `Comparer[int].create(f)` name the
    // type a static member is reached through, they do not use it as a value — so the SPY0339
    // uncalled-type-reference rule skips exactly this node (#1192). Scoped around the qualifier check
    // in CheckMemberAccessCore via ScopedValue.Push (see Semantic/ScopedValue.cs, #1218).
    private Expression? _currentMemberAccessQualifier;

    // The direct argument expressions (positional and keyword, unwrapped through parentheses) of the
    // FunctionCall currently having its arguments checked. A builtin type NAME in one of these
    // positions is established, working behavior — map(int, xs), sorted(xs, key=int),
    // defaultdict(list), isinstance(x, int) — because a C# target type exists there and the legacy
    // synthesized-signature typing feeds generic inference. The constructor-reference rules skip
    // exactly those nodes so #1182 cannot over-fire on them (the failure mode that reverted the
    // SPY0337 extension, #1170). Scoped once around the whole argument-checking block in
    // CheckFunctionCall via ScopedValue.Push (see Semantic/ScopedValue.cs, #1218), so every internal
    // argument path is covered and nested calls restore the enclosing set.
    private HashSet<Expression>? _currentCallArguments;

    // The one call argument whose PARAMETER type `_expectedType` currently holds — null whenever
    // `_expectedType` came from somewhere else.
    //
    // `_expectedType` is a general "what does this position want" channel, and the argument loop
    // only OVERWRITES it when a parameter type is actually in hand; otherwise the enclosing
    // context's expectation stays visible. So while `list` is checked in
    // `d: defaultdict[str, list[int]] = defaultdict(list)`, `_expectedType` is the ASSIGNMENT's
    // declared type, and while `str` is checked in `zs: list[str] = sorted(xs, key=str)` it is
    // `list[str]` — neither is the parameter. A rule that reads `_expectedType` as "this argument's
    // parameter type" without checking this field refuses working calls against a parameter they
    // were never passed to; both of those are shipped fixtures that went red exactly that way
    // (#1490).
    //
    // Compared by REFERENCE against the argument being checked, so a stale value can only cause a
    // miss, never a false hit.
    private Expression? _parameterTypedArgument;

    // The iterator expression of the for statement or comprehension for-clause currently being
    // checked (`for c in Color`, `[c.name for c in Color]`). An ENUM name is a legitimate iterable
    // there — it denotes the member set, and both sites rescue it into a UserDefinedType right after
    // CheckExpression returns Unknown for it. That makes the position a use of the type name, not of
    // a value, so the non-constructible refusal skips exactly these nodes (#1250); refusing here
    // broke five shipped enum-iteration fixtures, which is the #1170 over-fire in its enum flavor.
    // Scoped around the iterator check via ScopedValue.Push (see Semantic/ScopedValue.cs, #1218).
    private Expression? _currentIterationSource;

    // The index expression of the IndexAccess currently being checked, and the elements of a
    // multi-argument index (`Outer.Inner[int]`, `Dict[str, int]`). Some type references reach the
    // value-indexing path rather than the generic-reference resolver — the nested spelling does —
    // and their index names a TYPE ARGUMENT there, never a value. A builtin type name is only ever a
    // type argument in an index position (nothing in Sharpy is indexed BY a type), so the
    // constructor-reference rules skip exactly these nodes (#1182, #1192). Scoped around the index
    // check via ScopedValue.Push (see Semantic/ScopedValue.cs, #1218).
    private HashSet<Expression>? _currentIndexArguments;

    // _currentBindingValue lived here until #1248. It existed to recognise the ONE position that
    // could mint a call-only alias — an assignment's right-hand side or a declaration's initializer —
    // and the alias is retired, so nothing asks the question any more. Deleted rather than left
    // write-only.

    // Per-compilation memo for BCL generic instance methods resolved by CLR reflection fallback
    // (TryResolveGenericInstanceMethod, #1136). Raw BCL TypeSymbols built by
    // ModuleRegistry.CreateTypeSymbolFromClrType carry a ClrType but no Methods, so an explicit-
    // type-argument reference like `lst.convert_all[str](...)` can only be resolved by reflecting the
    // owning ClrType. Keyed on (owning TypeSymbol, Sharpy member name); a null value memoizes a
    // negative result so repeated misses do not re-reflect. TypeSymbols from CreateTypeSymbolFromClrType
    // are per-compilation, so this never mutates shared state (StaticStateConformance) and TypeSymbol.Methods
    // is left untouched. Lazily reflects on the constructed receiver so class-level type params are closed.
    private readonly Dictionary<(TypeSymbol, string), FunctionSymbol?> _bclGenericMethodMemo = new();

    // Companion memo for the BCL member-absence proof (#1141): when the reflection fallback above
    // finds no generic method, this records whether reflection can affirmatively prove the member does
    // not exist AT ALL on the receiver's ClrType (no member by any mangling candidate, no reachable
    // extension method) together with the closest member name to suggest. Keyed identically to
    // _bclGenericMethodMemo so both negative results are computed once per (type, member).
    private readonly Dictionary<(TypeSymbol, string), (bool Absent, string? Suggestion)> _bclMemberAbsenceMemo = new();

    // Bridges reflected CLR parameter/return types to SemanticTypes when materializing a reflected BCL
    // generic method (#1136). Conservative by design: anything unmappable collapses to object, since the
    // emitted C# uses explicit type args + the verbatim CLR name and Roslyn performs the authoritative bind.
    private readonly Discovery.ClrTypeBridge _bclGenericMethodBridge = new();

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

    // How each tracked list[T] symbol is backed in the emitted C#, which decides #1052 fast-path
    // eligibility (only ListBackingKind.SharpyList exposes GetItemUnchecked). Populated at the same
    // binding sites the old _sharpyListBackedSymbols set was, but now records the negative facts
    // explicitly: SharpyList for non-variadic list parameters and explicitly-annotated list locals,
    // ClrArray for *args variadic list parameters. Symbols absent from the map default to Unknown
    // (interop returns, inferred locals, narrowed values), which stays conservative. Uses reference
    // equality (Symbol overrides it).
    private readonly Dictionary<Symbol, ListBackingKind> _listBackingKinds = new();

    // Pending overload selections for callable references whose arity-divergent overloads cannot be
    // resolved because the expected type still contains unsolved type parameters (#1589). Populated
    // during argument checking and drained — resolved or refused — before the recording call's check
    // returns. One list serves every call in flight: the entries at or past `_pendingOverloadWatermark`
    // belong to the call currently being checked, the ones below it to enclosing calls (a nested call
    // pushes its own watermark and must not drain its parent's entries with its own substitutions).
    private readonly List<PendingOverloadSelection> _pendingOverloadSelections = new();

    // Index into `_pendingOverloadSelections` where the current call's entries start, or -1 when no
    // call is being checked. Deferral is only offered inside a call's argument scope: elsewhere there
    // is no inference to come back with a concrete target, so the reference is refused on the spot.
    private int _pendingOverloadWatermark = -1;

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

    /// <summary>
    /// The project's assembly references, forwarded to validators. Empty unless a project
    /// compilation supplies one (#1492).
    /// </summary>
    internal Discovery.ReferenceClosure ReferenceClosure { get; set; } = Discovery.ReferenceClosure.Empty;

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
        _typeInference.OperandAssignability = (source, target) => IsArgumentAssignable(source, target);

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
        // Thread the project's references so the SPY0495 absence proof can consult them (#1492).
        context.ReferenceClosure = ReferenceClosure;
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
    /// The type a variable was DECLARED with — its root binding's type — which is what a STORE into
    /// it is checked against. A plain rebinding versions the binding by the assigned value's type so
    /// that reads narrow (`x: str | None = None; x = "a"` reads `str`), but the emitted C# local keeps
    /// its declared type, so the next store (`x = None`, `x = 2` into an `object`) is a write into the
    /// declared slot, never into the previous value's type (#1706).
    /// </summary>
    private SemanticType DeclaredBindingType(VariableSymbol symbol)
        => GetVariableType(_semanticInfo.GetRootBinding(symbol));

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
    /// <remarks>
    /// This is the SYMBOL-IDENTITY axis: it stamps <c>DeclaringFilePath</c> on the symbols this
    /// pass creates, and the single-file analyze path deliberately nulls it so the entry file's
    /// symbols read as "the current document" (#1087). For what the file is CALLED — which is a
    /// different question, and one an editor buffer can always answer — see
    /// <see cref="ModuleIdentityFilePath"/>.
    /// </remarks>
    public string? CurrentFilePath
    {
        get => _currentFilePath;
        set => _currentFilePath = value;
    }

    /// <summary>
    /// The file's NAME, used to derive the module class name (and thus to detect the SPY0523
    /// function/module-class collision). Falls back to <see cref="CurrentFilePath"/> when unset.
    /// </summary>
    /// <remarks>
    /// Split out from <see cref="CurrentFilePath"/> for #1433. The two axes were one field, so
    /// nulling the entry file's SYMBOL paths (the #1087 contract, which an editor genuinely needs)
    /// also erased the file's NAME — and SPY0523 is derived entirely from the name. The result was
    /// that a module-level <c>def foo</c> in <c>foo.spy</c> produced no squiggle in the editor and
    /// then failed the build. Naming the file costs nothing to symbol identity: only
    /// <see cref="NameMangler.ComputeModuleClassName"/> reads this.
    /// </remarks>
    public string? ModuleIdentityFilePath { get; set; }

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

        // Pre-pass: fold constant values for module-level consts (#1601). Fixed-point
        // iteration handles forward references and chains (BETA = ALPHA, ALPHA = 200):
        // each pass folds consts whose dependencies were resolved on a prior pass.
        bool foldedAny;
        do
        {
            foldedAny = false;
            foreach (var statement in module.Body)
            {
                _cancellationToken.ThrowIfCancellationRequested();
                if (statement is Parser.Ast.VariableDeclaration { IsConst: true } constDecl
                    && constDecl.InitialValue != null)
                {
                    var sym = _symbolTable.Lookup(constDecl.Name) as VariableSymbol;
                    if (sym is { IsConstant: true, ConstantValue: null })
                    {
                        var declaredType = _typeResolver.ResolveTypeAnnotation(constDecl.Type);
                        TryFoldConstantValue(sym, declaredType, constDecl.InitialValue);
                        if (sym.ConstantValue != null)
                            foldedAny = true;
                    }
                }
            }
        } while (foldedAny);

        foreach (var statement in module.Body)
        {
            if (statement is Parser.Ast.VariableDeclaration { IsConst: true } unfoldedDecl
                && unfoldedDecl.InitialValue != null
                && _symbolTable.Lookup(unfoldedDecl.Name) is VariableSymbol { IsConstant: true, ConstantValue: null })
            {
                var unfoldedType = _typeResolver.ResolveTypeAnnotation(unfoldedDecl.Type);
                if (Registry.PrimitiveCatalog.GetPrimitiveInfo(unfoldedType)?.Kind
                    is not (Registry.PrimitiveCatalog.NumericKind.SignedInteger
                        or Registry.PrimitiveCatalog.NumericKind.UnsignedInteger))
                    continue;

                if (ReferencesUnfoldedConst(unfoldedDecl.InitialValue))
                {
                    AddError(
                        $"Circular constant reference: '{unfoldedDecl.Name}' depends on a constant that references it back",
                        unfoldedDecl.LineStart, unfoldedDecl.ColumnStart,
                        code: DiagnosticCodes.Semantic.CircularConstantReference,
                        span: unfoldedDecl.Span);
                }
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
            var codeGenInfoComputer = new CodeGenInfoComputer(_symbolTable, SemanticBinding, _diagnostics, _semanticInfo);
            // The file's NAME, not its symbol identity (#1433): module-class derivation must keep
            // working for an entry file whose symbol paths are nulled for the #1087 contract.
            codeGenInfoComputer.ComputeForModule(module, ModuleIdentityFilePath ?? _currentFilePath);
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
    /// Returns true when <paramref name="expr"/> references a constant whose value
    /// has not yet been folded (ConstantValue is null). Used to raise SPY0278
    /// (CircularConstantReference) for circular constant references. The default
    /// (false) is conservative: an expression kind not enumerated here is treated as
    /// not referencing an unfolded constant, which means a cycle through that kind
    /// produces no SPY0278 — the wrong-diagnostic shape, not silent-wrong-output.
    /// Pinned by ConstantCycleDetectionTests.
    /// </summary>
    private bool ReferencesUnfoldedConst(Expression expr)
    {
        switch (expr)
        {
            case Identifier id:
                return _symbolTable.Lookup(id.Name) is VariableSymbol { IsConstant: true, ConstantValue: null };
            case UnaryOp unary:
                return ReferencesUnfoldedConst(unary.Operand);
            case BinaryOp binary:
                return ReferencesUnfoldedConst(binary.Left) || ReferencesUnfoldedConst(binary.Right);
            case Parenthesized paren:
                return ReferencesUnfoldedConst(paren.Expression);
            default:
                return false;
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
                {
                    var exprType = CheckExpression(exprStmt.Expression);
                    var unwrapped = Shared.AstHelper.UnwrapParenthesized(exprStmt.Expression);

                    // Classify every expression statement so the emitter never pattern-matches
                    // AST shape to pick plain/discard/elide (#1622).
                    //
                    // Refusals first: a Discard lowers to `_ = expr;`, which C# accepts only for an
                    // expression with a value of a known type. The shapes that reach the Discard arm
                    // with no such value are refused HERE (SPY0603) instead of surfacing as a C#
                    // error behind SPY0908 — CPython evaluates each as a no-op, so every one of
                    // these was an ICE before, never a program that ran. The elided shapes (`None`,
                    // method groups) and the type-name statement (SPY0342) are handled elsewhere.
                    // A refused statement records no lowering; the emitter throws on an absent fact.
                    if (unwrapped is LambdaExpression lambdaStmt)
                    {
                        // A `_` placeholder call (`f(_)`) is desugared by the parser into a lambda
                        // whose parameters carry the `__placeholder_` prefix (the parser's own
                        // marker, Parser.Expressions.cs); as a statement that partial application
                        // is built and dropped. Name the placeholder so the message points at the
                        // source spelling, not the desugaring.
                        var isPlaceholderPartial = lambdaStmt.Parameters.Length > 0
                            && lambdaStmt.Parameters[0].Name.StartsWith("__placeholder_", StringComparison.Ordinal);
                        AddError(
                            isPlaceholderPartial
                                ? "a '_' placeholder partial application cannot be an expression statement; its result is a function: call it or bind it to a name"
                                : "a lambda cannot be an expression statement; call it or bind it to a name",
                            exprStmt.LineStart, exprStmt.ColumnStart,
                            code: DiagnosticCodes.SemanticOverflow.ExpressionStatementNotDiscardable,
                            span: exprStmt.Expression.Span);
                        break;
                    }
                    if (exprType is ModuleType)
                    {
                        AddError(
                            "a module reference cannot be an expression statement",
                            exprStmt.LineStart, exprStmt.ColumnStart,
                            code: DiagnosticCodes.SemanticOverflow.ExpressionStatementNotDiscardable,
                            span: exprStmt.Expression.Span);
                        break;
                    }
                    // `...` types as Void but never reaches the discard: the emitter lowers an
                    // ellipsis statement to `throw new NotImplementedException()` (a transform
                    // applied before the kind switch), so it is excluded here.
                    if (exprType is VoidType
                        && unwrapped is not (FunctionCall or Parser.Ast.AwaitExpression or NoneLiteral or EllipsisLiteral))
                    {
                        AddError(
                            "expression statement of type 'None' must be a call; write the branch as an if statement",
                            exprStmt.LineStart, exprStmt.ColumnStart,
                            code: DiagnosticCodes.SemanticOverflow.ExpressionStatementNotDiscardable,
                            span: exprStmt.Expression.Span);
                        break;
                    }

                    if (unwrapped is NoneLiteral)
                    {
                        _semanticInfo.SetStatementLowering(exprStmt,
                            new StatementLowering(StatementLoweringKind.ElideNoneLiteral));
                    }
                    else if (exprType is FunctionType
                        && (unwrapped is MemberAccess
                            || (unwrapped is Identifier id
                                && _semanticInfo.GetIdentifierSymbol(id) is FunctionSymbol)))
                    {
                        _semanticInfo.SetStatementLowering(exprStmt,
                            new StatementLowering(StatementLoweringKind.ElideMethodGroupStatement));
                    }
                    else if (unwrapped is FunctionCall or Parser.Ast.AwaitExpression)
                    {
                        _semanticInfo.SetStatementLowering(exprStmt,
                            new StatementLowering(StatementLoweringKind.PlainStatement));
                    }
                    else
                    {
                        _semanticInfo.SetStatementLowering(exprStmt,
                            new StatementLowering(StatementLoweringKind.Discard));
                    }
                    break;
                }

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
