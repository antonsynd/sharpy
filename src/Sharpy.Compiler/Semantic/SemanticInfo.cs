using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Services;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Maps AST nodes to their semantic information.
/// Provides a way to annotate the AST without modifying it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Threading:</b> All mutable annotation fields use <see cref="ConcurrentDictionary{TKey,TValue}"/>
/// for thread safety, with <c>_symbolReferences</c> using a <see cref="ConcurrentBag{T}"/>
/// per symbol so concurrent writers can record references without locking. The
/// <c>_symbolTable</c> backing field and <c>CurrentFilePath</c> are intended to be set
/// once per instance during initialization and read concurrently afterward.
/// </para>
/// </remarks>
[NotThreadSafe(Reason = "All annotation dictionaries are concurrent, but _symbolTable/CurrentFilePath are set-once initialization fields. Treat the instance as read-mostly after type checking completes.")]
public class SemanticInfo : ISemanticQuery
{
    // Use ReferenceEqualityComparer because AST nodes are records with value-based equality,
    // but we need to distinguish between different instances (e.g., two super().__init__() calls
    // in different files should be cached separately even if they have the same structure)

    // Map expressions to their resolved types
    private readonly ConcurrentDictionary<Expression, SemanticType> _expressionTypes =
        new(ReferenceEqualityComparer.Instance);

    // Map identifiers to their symbols
    private readonly ConcurrentDictionary<Identifier, Symbol> _identifierSymbols =
        new(ReferenceEqualityComparer.Instance);

    // Map variable declarations to the symbol they bind, recorded where the checker binds it.
    // Distinct from _identifierSymbols, which is populated at *references*: a declaration nobody
    // reads has no identifier to key on, and before this table the only way to resolve one was a
    // name-and-position scan that could not see a function-local binding at all (#1222).
    private readonly ConcurrentDictionary<VariableDeclaration, Symbol> _declarationSymbols =
        new(ReferenceEqualityComparer.Instance);

    // Map function calls to resolved function symbols
    private readonly ConcurrentDictionary<FunctionCall, FunctionSymbol> _callTargets =
        new(ReferenceEqualityComparer.Instance);

    // Map type annotations to resolved semantic types
    private readonly ConcurrentDictionary<TypeAnnotation, SemanticType> _typeAnnotations =
        new(ReferenceEqualityComparer.Instance);

    // Map expressions to their narrowed types (for type narrowing after is not None / isinstance checks)
    // This captures the narrowed type at each specific usage of an identifier within a narrowing context
    private readonly ConcurrentDictionary<Expression, SemanticType> _narrowedExpressionTypes =
        new(ReferenceEqualityComparer.Instance);

    // Map narrowed read expressions (Identifier / MemberAccess / IndexAccess) to the exact accessor
    // codegen must apply at that read site (.Unwrap() / .Value / ! / cast). Recorded by the TypeChecker
    // per read node so the emitter is a dumb applier and never re-derives narrowing flow (#1081,
    // Critical Rule 2 pattern (b)). Match-arm reads never appear here — they narrow via redefined
    // symbols, so no accessor is needed.
    private readonly ConcurrentDictionary<Expression, NarrowedReadLowering> _narrowedReadLowerings =
        new(ReferenceEqualityComparer.Instance);

    // Map expressions whose SEMANTIC type is a Sharpy collection but whose EMITTED type is the CLR
    // sequence the bridge mapped it from, to the Sharpy collection they must be materialized into
    // (#1251, Critical Rule 2 pattern (b)). A BCL extension call typed `list[str]` emits as
    // `IEnumerable<string>`; without the materialization the two disagree, and the disagreement is
    // not merely an ICE — `print(xs)` prints a LINQ iterator's type name and `xs.append(v)` binds
    // LINQ's pure Enumerable.Append and silently discards the result. The value recorded is the
    // target collection type; the emitter wraps the generated expression in its constructor and
    // decides nothing itself.
    private readonly ConcurrentDictionary<Expression, SemanticType> _sequenceMaterializations =
        new(ReferenceEqualityComparer.Instance);

    // Map a TYPE OPERAND node to the type test codegen must emit. Keyed on the operand node so the
    // emitter and both narrowing resolvers read ONE decided type and none of them re-derives what the
    // operand's syntax denotes (#1207, #1213, Critical Rule 2 pattern (b)).
    //
    // The key is `Node`, not `Expression`, because a type operand is written two ways: as an
    // expression (`isinstance(x, T)`'s second argument, the same node an IsType narrowing fact
    // retains) and as a TypeAnnotation (`x is T`, `x as? T`, `case T():`, `except T:`). Both derive
    // from Node, so one channel serves all four sites (#1235) — see TypeAnnotation's remarks.
    //
    // Absent ⇒ the operand is not a classified type test: either the callee is a shadowed
    // `isinstance`, or the shape is one the classifier left to the ordinary runtime-call path.
    private readonly ConcurrentDictionary<Node, TypeTestLowering> _typeTestLowerings =
        new(ReferenceEqualityComparer.Instance);

    // Map generic function calls to their inferred type arguments
    // Used by codegen to emit explicit type arguments in generated C#
    private readonly ConcurrentDictionary<FunctionCall, List<SemanticType>> _inferredTypeArguments =
        new(ReferenceEqualityComparer.Instance);

    // Map member access expressions to their resolved symbols (type owner + member).
    // Used to communicate TypeChecker's resolution to codegen so it doesn't re-resolve.
    // Covers: ClassName.FIELD (static/const), ClassName.method (static), self.static_field.
    private readonly ConcurrentDictionary<MemberAccess, (TypeSymbol Owner, Symbol Member)> _memberAccessResolutions =
        new(ReferenceEqualityComparer.Instance);

    // Track functions that contain yield statements (generators)
    //
    // TRANSPORT (E2 #1056): the CODEGEN routing consumers now read this fact from the lowering IR
    // (IrCompilation.IsGenerator); the emitter never reads this dict. Full plan-letter deletion
    // (Option B) would repoint the two pre-lowering validators (ControlFlowValidator,
    // GeneratorValidator) to the symbol-keyed FunctionSymbol.IsGenerator, but resolving
    // FunctionDef→FunctionSymbol for those raw class/struct/module-body FunctionDefs (incl. nested and
    // decorated defs) has no clean validator-side channel — the team lead's pre-authorized Option-A
    // fallback. So the dict is retained as the validators' + lowering pass's input; physical deletion
    // + its MergeFrom line are deferred to the guardrail-retirement step (lowering-ir.md §6.4).
    private readonly ConcurrentDictionary<FunctionDef, byte> _generatorFunctions = new(ReferenceEqualityComparer.Instance);

    // Track member access expressions that resolve to events (for codegen to emit +=/-= correctly)
    private readonly ConcurrentDictionary<Expression, byte> _eventAccessNodes = new(ReferenceEqualityComparer.Instance);

    // Track expressions that denote a type rather than a value (e.g., a module-qualified
    // reference to an exported TypeSymbol). Used to accept such expressions for parameters
    // backed by CLR System.Type (e.g., assert_raises(zoneinfo.ZoneInfoNotFoundError)).
    private readonly ConcurrentDictionary<Expression, byte> _typeReferenceNodes = new(ReferenceEqualityComparer.Instance);

    // Track arguments that name a type used as a zero-argument factory callable — Python's
    // defaultdict(list) convention, where the argument is a type name, not a value. Whether an
    // identifier denotes such a factory is a semantic question (it may resolve as a TypeSymbol, as a
    // builtin collection function, or through the wrapper-collection special cases), so the answer is
    // decided once here; codegen only wraps the marked argument in `() => new TValue()` (#1175).
    private readonly ConcurrentDictionary<Expression, byte> _typeFactoryArguments = new(ReferenceEqualityComparer.Instance);

    // Map patterns to their resolved union case type symbols
    // Used when a PositionalPattern or MemberAccessPattern matches a union case
    private readonly ConcurrentDictionary<Pattern, TypeSymbol> _patternUnionCases =
        new(ReferenceEqualityComparer.Instance);

    // Map BindingPatterns to constant VariableSymbols when the identifier resolves
    // to a module-level Final/const variable (RFC 3535 — constants in match patterns)
    private readonly ConcurrentDictionary<Pattern, VariableSymbol> _patternConstants =
        new(ReferenceEqualityComparer.Instance);

    // Map TypePatterns to their fully-resolved SemanticType when the TypeChecker
    // computed a type that differs from a naive resolution of the AST type node
    // (e.g., unparameterized collection patterns like `case list()` against an
    // `object` scrutinee get default `object` type arguments filled in). CodeGen
    // prefers this over re-resolving the AST type node.
    private readonly ConcurrentDictionary<Pattern, SemanticType> _patternTypes =
        new(ReferenceEqualityComparer.Instance);

    // Track expressions whose type was set to UnknownType due to a user error
    // (i.e., a diagnostic was already emitted for the node). This distinguishes
    // expected error-recovery Unknown types from unexpected ones (compiler bugs).
    private readonly ConcurrentDictionary<Expression, byte> _errorRecoveryNodes =
        new(ReferenceEqualityComparer.Instance);

    // Map with-item context expressions to their context manager kind
    // (Disposable, DunderProtocol, or AsyncDisposable/AsyncDunderProtocol)
    //
    // TRANSPORT (E2 #1056): this fact now flows to codegen through the lowering IR
    // (IrWithItem.Kind); the emitter reads the IR, never this dict. The dict is retained only as the
    // lowering pass's input (the kind is resolved via CLR IDisposable/dunder-protocol inspection and
    // cannot be recomputed post-type-check). Physical deletion + its MergeFrom line are deferred to
    // the guardrail-retirement step (lowering-ir.md §6.4, post-E2).
    private readonly ConcurrentDictionary<Expression, ContextManagerKind> _contextManagerKinds =
        new(ReferenceEqualityComparer.Instance);

    // Map with-item nodes to their 'as' variable symbols
    // Needed because the with-scope is exited after type checking, making SymbolTable lookup impossible
    private readonly ConcurrentDictionary<WithItem, VariableSymbol> _withItemSymbols =
        new(ReferenceEqualityComparer.Instance);

    // Map except-handler nodes to their 'as' variable symbols. The except scope is exited after type
    // checking, and a handler variable nothing reads is in no reference collection either, so this
    // is the only way back to the symbol from the declaration node (#1232). Mirrors
    // _withItemSymbols, whose 'as' variable has the same shape and the same problem.
    private readonly ConcurrentDictionary<ExceptHandler, VariableSymbol> _exceptHandlerSymbols =
        new(ReferenceEqualityComparer.Instance);

    // Map function definitions to the symbol they declare, recorded where the checker resolves it.
    // Module-level functions are findable by other means; a *nested* def that nothing calls is not —
    // it is in no reference collection and not in module scope (#1232). Keyed per node so an
    // overload set stays distinguishable.
    private readonly ConcurrentDictionary<FunctionDef, FunctionSymbol> _functionDeclarationSymbols =
        new(ReferenceEqualityComparer.Instance);

    // Map binary-op expressions (==/!=) to the strategy codegen must use to emit them.
    // Only present when the strategy differs from the default native operator — e.g.
    // tuple equality and CLR types that implement Equals/IEquatable but define no
    // op_Equality must lower to an Equals call instead of a C# `==`. Keyed by node identity.
    //
    // TRANSPORT (E2 #1056): this fact now flows to codegen through the lowering IR
    // (IrEqualityComparison.Strategy); the emitter reads the IR, never this dict. The dict is
    // retained only as the lowering pass's input (the strategy is CLR-reflection-derived and cannot
    // be recomputed post-type-check). Physical deletion + its MergeFrom line are deferred to the
    // guardrail-retirement step (lowering-ir.md §6.4, post-E2).
    private readonly ConcurrentDictionary<Expression, BinaryOpLowering> _binaryOpLowerings =
        new(ReferenceEqualityComparer.Instance);

    // Map index-access expressions to the strategy codegen must use to emit them. Only present when
    // the strategy differs from the default native element access (e.g. string/array helper calls,
    // params-indexer spreads, tuple .ItemN access). Keyed by node identity.
    //
    // TRANSPORT (E2 #1056): this fact now flows to codegen through the lowering IR
    // (IrIndexAccess.Strategy); the emitter reads the IR, never this dict. The dict is retained only
    // as the lowering pass's input — the NativeUnchecked strategy depends on transient TypeChecker
    // traversal state (_nonNegativeInductionVars / _listBackingKinds) that is gone by lowering
    // time, so it cannot be recomputed. Physical deletion + its MergeFrom line are deferred to the
    // guardrail-retirement step (lowering-ir.md §6.4, post-E2).
    private readonly ConcurrentDictionary<Expression, IndexAccessLowering> _indexAccessLowerings =
        new(ReferenceEqualityComparer.Instance);

    // Map a generic-reference index access (callee[T, ...]) to the normalized GenericReference fact the
    // GenericReferenceResolver produced: the callee kind, its target symbol / receiver type, the
    // resolved type arguments, and (for arity-selected builtins) the selected overload. This is the
    // single lowering face for generic references — the emitter switches on Kind alone rather than
    // re-deriving the callee shape per helper (Critical Rule 2 pattern (b); #1143). The parallel
    // GenericFunctionType / GenericType expression-type recording stays as the type-system face.
    private readonly ConcurrentDictionary<Expression, GenericReference> _genericReferences =
        new(ReferenceEqualityComparer.Instance);

    // Map safe-cast expressions (value to T? / value as? T) to the shape codegen must emit. Only present
    // when the source and stripped target are both plain numeric primitives (int/long/float32/double) —
    // the TypeChecker classifies widening/identity vs narrowing here so the emitter never inspects the
    // operand types to pick a lowering (#1110, Critical Rule 2 pattern (b)). Absent ⇒ the default
    // type-pattern lowering (which is the only correct shape for object/reference/optional sources).
    private readonly ConcurrentDictionary<Expression, TypeCoercionLowering> _typeCoercionLowerings =
        new(ReferenceEqualityComparer.Instance);

    // Map a bare builtin type-constructor reference used as a value (int, str, dict, list) to the C#
    // shape codegen must emit for it and the signature the TypeChecker pinned it to — the conversion
    // families' Builtins.X method group, or the collection families' constructor lambda (#1182).
    // Present only where a signature was available (an annotated target, a declared return type, a
    // parameter): an unpinned reference is refused in semantic analysis (SPY0342, or SPY0346 for a
    // type with no construction at all) and never reaches codegen through this dictionary. The
    // emitter switches on the recorded Family and never inspects the builtin (Critical Rule 2
    // pattern (b)). Keyed by node identity.
    private readonly ConcurrentDictionary<Expression, ConstructorReferenceLowering> _constructorReferenceLowerings =
        new(ReferenceEqualityComparer.Instance);

    // Map builtin-call argument expressions sitting in an ITERABLE position to how that argument
    // binds there: the element type it iterates as, plus the projection codegen must apply before
    // passing it (sorted(x), list(x), max(x), zip(x, …), filter(f, x), ", ".join(x), …). Present only
    // for arguments the TypeChecker decided are acceptable as an iterable in that position, which is
    // also what makes them acceptable — one record carries both halves so acceptance can never drift
    // ahead of lowering (#1154, #1159, #1198, #1199). The emitter switches on the tag and never
    // re-inspects types (repo rule 2). Absent ⇒ not an iterable position, or a source the ring does
    // not accept there (e.g. `for k in d`, dict(d) copy, user-function params). Keyed by node
    // identity.
    private readonly ConcurrentDictionary<Expression, IterableArgumentProjection> _iterableProjections =
        new(ReferenceEqualityComparer.Instance);

    // Map member-access expressions on CLR-backed receivers to the original CLR method name
    // (e.g. is_os_platform -> IsOSPlatform), resolved by reflection during type checking so codegen
    // never reflects (#974). Only present when a directly-imported CLR method's acronym casing
    // must be preserved. Keyed by node identity.
    //
    // TRANSPORT (E2 #1056): this fact now flows to codegen through the lowering IR
    // (IrMemberAccess.ResolvedClrMemberName); the emitter reads the IR, never this dict. The dict is
    // retained only as the lowering pass's input (the name is resolved via CLR reflection and cannot
    // be recomputed post-type-check without reflecting in Lowering). Physical deletion + its MergeFrom
    // line are deferred to the guardrail-retirement step (lowering-ir.md §6.4, post-E2).
    private readonly ConcurrentDictionary<Expression, string> _resolvedClrMemberNames =
        new(ReferenceEqualityComparer.Instance);

    // Map method-call member-access expressions to a static-extension dispatch decision, so codegen
    // emits `global::Ext.Method(receiver, args...)` instead of the instance form `receiver.Method(args)`.
    // C# binds instance methods before extension methods, so an instance-style call to a str method
    // whose semantics live in Sharpy.StringExtensions silently binds a shadowing System.String BCL
    // method (e.g. Replace(old,new,count==0→StringComparison), Split→string[]). The TypeChecker makes
    // the dispatch decision here so the emitter never re-derives it. Keyed by node identity (#1071, #1072, #1085).
    //
    // TRANSPORT (E2 #1056): this fact now flows to codegen through the lowering IR
    // (IrMemberAccess.ExtensionDispatch); the emitter reads the IR, never this dict. The dict is
    // retained only as the lowering pass's input (the dispatch is resolved via the builtin registry /
    // str-method lookup and cannot be recomputed post-type-check). Physical deletion + its MergeFrom
    // line are deferred to the guardrail-retirement step (lowering-ir.md §6.4, post-E2).
    private readonly ConcurrentDictionary<Expression, StaticExtensionDispatch> _staticExtensionDispatches =
        new(ReferenceEqualityComparer.Instance);

    // Map function calls by Identifier callee to whether the call resolves to a builtin or a user
    // symbol. Recorded by the TypeChecker while scopes are live so the emitter (scope-collapsed at
    // codegen time) applies the routing verbatim instead of re-deriving it from a lookup that can
    // only see the global builtin (#1326, Critical Rule 2 pattern (b)).
    private readonly ConcurrentDictionary<FunctionCall, CalleeRouting> _calleeRoutings =
        new(ReferenceEqualityComparer.Instance);

    // Map declarations to their source generator bindings (bracket attributes that resolve to SourceGenerator subclasses)
    private readonly ConcurrentDictionary<Statement, List<GeneratorBinding>> _generatorBindings =
        new(ReferenceEqualityComparer.Instance);

    // Track statements that were produced by a source generator.
    // Value is the generator name (e.g., "GenerateEquals"). Used by LSP to display
    // "Generated by @[X]" on hover (Phase 7).
    private readonly ConcurrentDictionary<Statement, string> _generatedStatements =
        new(ReferenceEqualityComparer.Instance);

    // Track all reference locations for each symbol (for find-references and rename).
    // Key is Symbol (reference-equality), value is a thread-safe bag of references.
    // The FilePath may be null for the main file in single-file compilation.
    // THREADING: ConcurrentDictionary + ConcurrentBag allow lock-free concurrent writes
    // during type checking. Read order is unspecified, which is acceptable because
    // consumers (find-references / rename) sort or treat results as a set.
    private readonly ConcurrentDictionary<Symbol, ConcurrentBag<SymbolReference>> _symbolReferences = new();

    private SymbolTable? _symbolTable;

    /// <summary>
    /// The file path of the current compilation unit, used to tag symbol references.
    /// </summary>
    public string? CurrentFilePath { get; internal set; }

    public void SetSymbolTable(SymbolTable symbolTable)
    {
        _symbolTable = symbolTable;
    }

    public void SetExpressionType(Expression expr, SemanticType type)
    {
        _expressionTypes[expr] = type;
    }

    public SemanticType? GetExpressionType(Expression expr)
    {
        return _expressionTypes.TryGetValue(expr, out var type) ? type : null;
    }

    public void SetIdentifierSymbol(Identifier id, Symbol symbol)
    {
        _identifierSymbols[id] = symbol;
        RecordReference(symbol, id);
    }

    public Symbol? GetIdentifierSymbol(Identifier id)
    {
        return _identifierSymbols.TryGetValue(id, out var symbol) ? symbol : null;
    }

    /// <summary>
    /// Records the symbol a variable declaration binds. Called where the checker creates or
    /// updates that symbol, so the binding is known whether or not anything reads the variable.
    /// </summary>
    public void SetDeclarationSymbol(VariableDeclaration declaration, Symbol symbol)
    {
        _declarationSymbols[declaration] = symbol;
    }

    /// <inheritdoc/>
    public Symbol? GetDeclarationSymbol(VariableDeclaration declaration)
    {
        return _declarationSymbols.TryGetValue(declaration, out var symbol) ? symbol : null;
    }

    public void SetCallTarget(FunctionCall call, FunctionSymbol target)
    {
        _callTargets[call] = target;
    }

    public FunctionSymbol? GetCallTarget(FunctionCall call)
    {
        return _callTargets.TryGetValue(call, out var target) ? target : null;
    }

    public void SetCalleeRouting(FunctionCall call, CalleeRouting routing)
    {
        _calleeRoutings[call] = routing;
    }

    public CalleeRouting? GetCalleeRouting(FunctionCall call)
    {
        return _calleeRoutings.TryGetValue(call, out var routing) ? routing : null;
    }

    public void SetTypeAnnotation(TypeAnnotation annotation, SemanticType type)
    {
        _typeAnnotations[annotation] = type;
    }

    public SemanticType? GetTypeAnnotation(TypeAnnotation annotation)
    {
        return _typeAnnotations.TryGetValue(annotation, out var type) ? type : null;
    }

    /// <summary>
    /// Sets a narrowed type for an expression (typically an Identifier) within a narrowing context.
    /// Used for type narrowing after `is not None` or `isinstance()` checks.
    /// </summary>
    public void SetNarrowedType(Expression expr, SemanticType narrowedType)
    {
        _narrowedExpressionTypes[expr] = narrowedType;
    }

    /// <summary>
    /// Gets the narrowed type for an expression, if one was recorded.
    /// Returns null if the expression wasn't in a narrowing context.
    /// </summary>
    public SemanticType? GetNarrowedType(Expression expr)
    {
        return _narrowedExpressionTypes.TryGetValue(expr, out var type) ? type : null;
    }

    /// <summary>
    /// Records the accessor codegen must apply at a narrowed read site (the Identifier, MemberAccess,
    /// or IndexAccess node being read). The TypeChecker sets this alongside <see cref="SetNarrowedType"/>
    /// whenever a narrowing origin implies an accessor; the emitter reads it and applies the accessor
    /// verbatim (#1081).
    /// </summary>
    public void SetNarrowedReadLowering(Expression expr, NarrowedReadLowering lowering)
    {
        _narrowedReadLowerings[expr] = lowering;
    }

    /// <summary>
    /// Gets the accessor lowering recorded for a narrowed read site, or <c>null</c> when the read needs
    /// no accessor (e.g. a match-arm binding, or an unnarrowed read).
    /// </summary>
    public NarrowedReadLowering? GetNarrowedReadLowering(Expression expr)
    {
        return _narrowedReadLowerings.TryGetValue(expr, out var lowering) ? lowering : null;
    }

    /// <summary>
    /// Records that <paramref name="expr"/> produces a CLR sequence which must be materialized into
    /// <paramref name="targetCollection"/> — a Sharpy <c>list</c>/<c>set</c>/<c>dict</c> — before it can
    /// be used as the value its semantic type already claims it is (#1251). Copy semantics are
    /// deliberate and are exactly Python's <c>list(...)</c>.
    /// </summary>
    public void SetSequenceMaterialization(Expression expr, SemanticType targetCollection)
    {
        _sequenceMaterializations[expr] = targetCollection;
    }

    /// <summary>
    /// The Sharpy collection an expression must be materialized into, or <c>null</c> when its emitted
    /// type already matches its semantic type — which is every expression that did not come from a
    /// CLR sequence.
    /// </summary>
    public SemanticType? GetSequenceMaterialization(Expression expr)
    {
        return _sequenceMaterializations.TryGetValue(expr, out var target) ? target : null;
    }

    /// <summary>
    /// Records the type test codegen must emit for a type operand — an <c>isinstance</c> argument or
    /// the <see cref="TypeAnnotation"/> of an <c>is</c>/<c>as?</c>/<c>as!</c>, a match class pattern
    /// or an <c>except</c> clause. Set by the TypeChecker's type-operand classifier, which is the
    /// single authority on what the operand denotes; the emitter switches on
    /// <see cref="TypeTestLowering.Kind"/> and the narrowing resolvers read
    /// <see cref="TypeTestLowering.TestType"/>, so the emitted test and the narrowed type agree by
    /// construction (#1207, #1213, #1235).
    /// </summary>
    public void SetTypeTestLowering(Node typeOperand, TypeTestLowering lowering)
    {
        _typeTestLowerings[typeOperand] = lowering;
    }

    /// <summary>
    /// Gets the type test recorded for a type operand, or <c>null</c> when the operand was not
    /// classified as one (shadowed <c>isinstance</c>, a synthesized node the checker never saw, or a
    /// shape the classifier leaves to the ordinary runtime-call path).
    /// </summary>
    public TypeTestLowering? GetTypeTestLowering(Node typeOperand)
    {
        return _typeTestLowerings.TryGetValue(typeOperand, out var lowering) ? lowering : null;
    }

    /// <summary>
    /// Records the emission shape codegen must apply for a numeric safe cast (<c>value to T?</c> /
    /// <c>value as? T</c>). Set by the TypeChecker only when the numeric applicability guard holds; the
    /// emitter applies it verbatim and never re-derives the shape from operand types (#1110).
    /// </summary>
    public void SetTypeCoercionLowering(Expression coercion, TypeCoercionLowering lowering)
    {
        _typeCoercionLowerings[coercion] = lowering;
    }

    /// <summary>
    /// Gets the numeric safe-cast lowering recorded for a coercion, or <c>null</c> when none was recorded
    /// (the emitter then falls back to the default type-pattern lowering).
    /// </summary>
    public TypeCoercionLowering? GetTypeCoercionLowering(Expression coercion)
    {
        return _typeCoercionLowerings.TryGetValue(coercion, out var lowering) ? lowering : null;
    }

    /// <summary>
    /// Records the emission shape for a builtin constructor reference the TypeChecker pinned to a
    /// concrete signature (<c>g: (str) -&gt; int = int</c>). Set only where a signature was available;
    /// the emitter applies the recorded family verbatim and never inspects the builtin (#1182).
    /// </summary>
    public void SetConstructorReferenceLowering(Expression reference, ConstructorReferenceLowering lowering)
    {
        _constructorReferenceLowerings[reference] = lowering;
    }

    /// <summary>
    /// Gets the pinned-constructor-reference lowering for an expression, or <c>null</c> when the node
    /// is not a pinned builtin constructor reference.
    /// </summary>
    public ConstructorReferenceLowering? GetConstructorReferenceLowering(Expression reference)
    {
        return _constructorReferenceLowerings.TryGetValue(reference, out var lowering) ? lowering : null;
    }

    /// <summary>
    /// Records how a builtin-call argument binds in an iterable position: its element type and the
    /// projection codegen must apply before passing it. The TypeChecker sets this at the one
    /// builtin-call recording choke point; the emitter reads it in its single argument-generation
    /// funnel and applies the projection verbatim (#1154, #1198).
    /// </summary>
    public void SetIterableProjection(Expression argument, IterableArgumentProjection projection)
    {
        _iterableProjections[argument] = projection;
    }

    /// <summary>
    /// Gets the iterable-argument binding recorded for a builtin-call argument, or <c>null</c> when
    /// the argument does not sit in an iterable position the ring accepts.
    /// </summary>
    public IterableArgumentProjection? GetIterableProjection(Expression argument)
    {
        return _iterableProjections.TryGetValue(argument, out var projection) ? projection : null;
    }

    /// <summary>
    /// Gets the effective type of an expression, considering type narrowing.
    /// Returns the narrowed type if one was recorded, otherwise returns the expression type.
    /// This is the primary method for LSP hover and other tooling that needs the "best known" type.
    /// </summary>
    /// <param name="expr">The expression to get the type for.</param>
    /// <returns>The narrowed type if available, otherwise the expression type, or null if unknown.</returns>
    public SemanticType? GetEffectiveType(Expression expr)
    {
        return GetNarrowedType(expr) ?? GetExpressionType(expr);
    }

    /// <summary>
    /// Sets the inferred type arguments for a generic function call.
    /// Used when calling a generic function without explicit type arguments (e.g., identity(42) -> T=int).
    /// </summary>
    public void SetInferredTypeArguments(FunctionCall call, List<SemanticType> typeArguments)
    {
        _inferredTypeArguments[call] = typeArguments;
    }

    /// <summary>
    /// Gets the inferred type arguments for a generic function call.
    /// Returns null if no type arguments were inferred (explicit call or non-generic function).
    /// </summary>
    public List<SemanticType>? GetInferredTypeArguments(FunctionCall call)
    {
        return _inferredTypeArguments.TryGetValue(call, out var types) ? types : null;
    }

    /// <summary>
    /// Records that a MemberAccess was resolved to a specific member symbol owned by a type.
    /// Used for static/const field access via type name (ClassName.FIELD) and
    /// static method access via type name (ClassName.method).
    /// Allows codegen to skip re-resolving the symbol table lookup.
    /// </summary>
    public void SetMemberAccessResolution(MemberAccess memberAccess, TypeSymbol owner, Symbol member)
    {
        _memberAccessResolutions[memberAccess] = (owner, member);
    }

    /// <summary>
    /// Gets the resolved member access symbol, if the TypeChecker recorded one.
    /// Returns null if this MemberAccess was not resolved via type name access.
    /// </summary>
    public (TypeSymbol Owner, Symbol Member)? GetMemberAccessResolution(MemberAccess memberAccess)
    {
        return _memberAccessResolutions.TryGetValue(memberAccess, out var resolution) ? resolution : null;
    }

    /// <summary>
    /// Records that a pattern was resolved to a specific union case type symbol.
    /// Used for PositionalPattern and MemberAccessPattern matching union cases.
    /// </summary>
    public void SetPatternUnionCase(Pattern pattern, TypeSymbol caseSymbol)
    {
        _patternUnionCases[pattern] = caseSymbol;
    }

    /// <summary>
    /// Gets the resolved union case symbol for a pattern, if one was recorded.
    /// Returns null if the pattern was not resolved as a union case.
    /// </summary>
    public TypeSymbol? GetPatternUnionCase(Pattern pattern)
    {
        return _patternUnionCases.TryGetValue(pattern, out var symbol) ? symbol : null;
    }

    /// <summary>
    /// Records that a BindingPattern resolved to a module-level constant (RFC 3535).
    /// </summary>
    public void SetPatternConstantSymbol(Pattern pattern, VariableSymbol constantSymbol)
    {
        _patternConstants[pattern] = constantSymbol;
    }

    /// <summary>
    /// Gets the constant variable symbol for a pattern, if one was recorded.
    /// Returns null if the pattern is a normal capture binding.
    /// </summary>
    public VariableSymbol? GetPatternConstantSymbol(Pattern pattern)
    {
        return _patternConstants.TryGetValue(pattern, out var symbol) ? symbol : null;
    }

    /// <summary>
    /// Records the fully-resolved SemanticType the TypeChecker computed for a pattern.
    /// Used for type patterns where the resolved type differs from a naive resolution
    /// of the AST type node (e.g., default `object` type arguments filled in for
    /// unparameterized collection patterns).
    /// </summary>
    public void SetPatternType(Pattern pattern, SemanticType type)
    {
        _patternTypes[pattern] = type;
    }

    /// <summary>
    /// Gets the fully-resolved SemanticType recorded for a pattern, if one was recorded.
    /// Returns null if the TypeChecker did not record a specialized type.
    /// </summary>
    public SemanticType? GetPatternType(Pattern pattern)
    {
        return _patternTypes.TryGetValue(pattern, out var type) ? type : null;
    }

    /// <summary>
    /// Marks an expression as having UnknownType due to error recovery.
    /// Call this when the type is set to UnknownType because a user-facing diagnostic
    /// was already emitted. This allows the invariant checker to distinguish expected
    /// Unknown types (error recovery) from unexpected ones (compiler bugs).
    /// </summary>
    public void MarkErrorRecovery(Expression expr)
    {
        _errorRecoveryNodes.TryAdd(expr, 0);
    }

    /// <summary>
    /// Returns true if the given expression was marked as error recovery,
    /// meaning its UnknownType is expected (a diagnostic was emitted).
    /// </summary>
    public bool IsErrorRecoveryType(Expression expr)
    {
        return _errorRecoveryNodes.ContainsKey(expr);
    }

    /// <summary>
    /// Marks a function as a generator (contains yield statements).
    /// </summary>
    public void MarkAsGenerator(FunctionDef funcDef) => _generatorFunctions.TryAdd(funcDef, 0);

    /// <summary>
    /// Returns true if the function has been marked as a generator.
    /// </summary>
    public bool IsGenerator(FunctionDef funcDef) => _generatorFunctions.ContainsKey(funcDef);

    /// <summary>
    /// Marks an expression as an event access (for codegen to emit event += / -= correctly).
    /// </summary>
    public void MarkAsEventAccess(Expression expr) => _eventAccessNodes.TryAdd(expr, 0);

    /// <summary>
    /// Returns true if the expression has been marked as an event access.
    /// </summary>
    public bool IsEventAccess(Expression expr) => _eventAccessNodes.ContainsKey(expr);

    /// <summary>
    /// Marks an expression as denoting a type reference (rather than a value), e.g., a
    /// module-qualified reference to an exported TypeSymbol.
    /// </summary>
    public void MarkTypeReference(Expression expr) => _typeReferenceNodes.TryAdd(expr, 0);

    /// <summary>
    /// Returns true if the expression has been marked as a type reference.
    /// </summary>
    public bool IsTypeReference(Expression expr) => _typeReferenceNodes.ContainsKey(expr);

    /// <summary>
    /// Marks a call argument as naming a type used as a zero-argument factory callable — the
    /// <c>defaultdict(list)</c> convention. Codegen wraps a marked argument in
    /// <c>() =&gt; new TValue()</c> rather than passing the name through (#1175).
    /// </summary>
    public void MarkTypeFactoryArgument(Expression expr) => _typeFactoryArguments.TryAdd(expr, 0);

    /// <summary>
    /// Returns true if the argument names a type used as a zero-argument factory callable.
    /// </summary>
    public bool IsTypeFactoryArgument(Expression expr) => _typeFactoryArguments.ContainsKey(expr);

    public void AddGeneratorBinding(Statement declaration, TypeSymbol generatorType, Decorator trigger)
    {
        var binding = new GeneratorBinding(generatorType, trigger);
        _generatorBindings.AddOrUpdate(
            declaration,
            _ => new List<GeneratorBinding> { binding },
            (_, list) => { list.Add(binding); return list; });
    }

    public IReadOnlyList<GeneratorBinding> GetGeneratorBindings(Statement declaration)
    {
        return _generatorBindings.TryGetValue(declaration, out var bindings)
            ? bindings
            : Array.Empty<GeneratorBinding>();
    }

    public IEnumerable<(Statement Declaration, IReadOnlyList<GeneratorBinding> Bindings)> GetAllGeneratorBindings()
    {
        foreach (var kvp in _generatorBindings)
            yield return (kvp.Key, kvp.Value);
    }

    /// <summary>
    /// Marks a statement as having been produced by a source generator.
    /// Used by LSP to display "Generated by @[X]" on hover.
    /// </summary>
    /// <param name="statement">The generated statement.</param>
    /// <param name="generatorName">The name of the generator that produced it.</param>
    public void MarkAsGenerated(Statement statement, string generatorName)
    {
        _generatedStatements[statement] = generatorName;
    }

    /// <summary>
    /// Returns true if the given statement was produced by a source generator.
    /// </summary>
    public bool IsGenerated(Statement statement) => _generatedStatements.ContainsKey(statement);

    /// <summary>
    /// Gets the name of the generator that produced the given statement, or null if
    /// the statement was not produced by a generator.
    /// </summary>
    public string? GetGeneratorName(Statement statement)
    {
        return _generatedStatements.TryGetValue(statement, out var name) ? name : null;
    }

    /// <summary>
    /// Returns true if any expression type in the semantic info is UnknownType.
    /// Used by tests to verify the invariant: if no semantic errors, no types should be unknown.
    /// </summary>
    public bool HasUnknownExpressionTypes()
    {
        return _expressionTypes.Values.Any(t => t is UnknownType);
    }

    /// <summary>
    /// Returns expressions that have UnknownType but are NOT in the error recovery set.
    /// These represent potential compiler bugs where type inference failed silently.
    /// </summary>
    public IReadOnlyList<Expression> GetUnexpectedUnknownExpressions()
    {
        return _expressionTypes
            .Where(kvp => kvp.Value is UnknownType && !_errorRecoveryNodes.ContainsKey(kvp.Key))
            .Select(kvp => kvp.Key)
            .ToList();
    }

    /// <summary>
    /// Returns the total number of expression types recorded.
    /// Used for consistency assertions and diagnostics.
    /// </summary>
    public int ExpressionTypeCount => _expressionTypes.Count;

    /// <summary>
    /// Returns the total number of identifier-to-symbol mappings.
    /// </summary>
    public int IdentifierSymbolCount => _identifierSymbols.Count;

    /// <summary>
    /// Records how a with-item's context expression should be handled at codegen time.
    /// Keyed on the context expression (each with-item has a unique expression reference).
    /// </summary>
    public void SetContextManagerKind(Expression contextExpr, ContextManagerKind kind)
    {
        _contextManagerKinds[contextExpr] = kind;
    }

    /// <summary>
    /// Gets the context manager kind for a with-item's context expression.
    /// Returns null if not recorded (defaults to Disposable in codegen).
    /// </summary>
    /// <remarks>
    /// Lowering-input only (E2 #1056): the lowering pass reads this to build
    /// <c>IrWithItem.Kind</c>; code generation reads the IR, never this accessor. Renamed with the
    /// <c>ForIr</c> suffix so nothing in <c>CodeGen/</c> can bind it.
    /// </remarks>
    public ContextManagerKind? GetContextManagerKindForIr(Expression contextExpr)
    {
        return _contextManagerKinds.TryGetValue(contextExpr, out var kind) ? kind : null;
    }

    /// <summary>
    /// Records the variable symbol for a with-item's <c>as</c> variable.
    /// Called during type checking so the symbol is retrievable after the with-scope is exited.
    /// </summary>
    public void SetWithItemSymbol(WithItem item, VariableSymbol symbol)
    {
        _withItemSymbols[item] = symbol;
    }

    /// <summary>
    /// Gets the variable symbol for a with-item's <c>as</c> variable.
    /// Returns null if no symbol was recorded (e.g., no <c>as</c> clause).
    /// </summary>
    public VariableSymbol? GetWithItemSymbol(WithItem item)
    {
        return _withItemSymbols.TryGetValue(item, out var symbol) ? symbol : null;
    }

    /// <summary>
    /// Records the variable symbol an except handler's <c>as</c> clause binds. Called where the
    /// checker binds it, so the binding is known whether or not the handler body reads it.
    /// </summary>
    public void SetExceptHandlerSymbol(ExceptHandler handler, VariableSymbol symbol)
    {
        _exceptHandlerSymbols[handler] = symbol;
    }

    /// <inheritdoc/>
    public VariableSymbol? GetExceptHandlerSymbol(ExceptHandler handler)
    {
        return _exceptHandlerSymbols.TryGetValue(handler, out var symbol) ? symbol : null;
    }

    /// <summary>
    /// Records the symbol a function definition declares. Called where the checker resolves it, so
    /// a nested definition nothing calls is still reachable from its declaration node.
    /// </summary>
    public void SetFunctionDeclarationSymbol(FunctionDef definition, FunctionSymbol symbol)
    {
        _functionDeclarationSymbols[definition] = symbol;
    }

    /// <inheritdoc/>
    public FunctionSymbol? GetFunctionDeclarationSymbol(FunctionDef definition)
    {
        return _functionDeclarationSymbols.TryGetValue(definition, out var symbol) ? symbol : null;
    }

    /// <summary>
    /// Records how an equality binary operation (<c>==</c>/<c>!=</c>) should be lowered by codegen.
    /// Only set when the strategy is not the default <see cref="BinaryOpLowering.NativeOperator"/>;
    /// the absence of an entry means codegen should emit a native C# operator.
    /// </summary>
    public void SetBinaryOpLowering(Expression binaryOp, BinaryOpLowering lowering)
    {
        _binaryOpLowerings[binaryOp] = lowering;
    }

    /// <summary>
    /// Gets the lowering strategy for an equality binary operation.
    /// Returns <see cref="BinaryOpLowering.NativeOperator"/> when no override was recorded.
    /// </summary>
    /// <remarks>
    /// Lowering-input only (E2 #1056): the lowering pass reads this to build
    /// <c>IrEqualityComparison.Strategy</c>; code generation reads the IR, never this accessor.
    /// Renamed with the <c>ForIr</c> suffix so nothing in <c>CodeGen/</c> can bind it.
    /// </remarks>
    public BinaryOpLowering GetBinaryOpLoweringForIr(Expression binaryOp)
    {
        return _binaryOpLowerings.TryGetValue(binaryOp, out var lowering)
            ? lowering
            : BinaryOpLowering.NativeOperator;
    }

    /// <summary>
    /// Records how an index access (<c>obj[index]</c>) should be lowered by codegen.
    /// Only set when the strategy is not the default <see cref="IndexAccessLowering.Native"/>;
    /// the absence of an entry means codegen should emit a native C# element access.
    /// </summary>
    public void SetIndexAccessLowering(Expression indexAccess, IndexAccessLowering lowering)
    {
        _indexAccessLowerings[indexAccess] = lowering;
    }

    /// <summary>
    /// Gets the lowering strategy for an index access.
    /// Returns <see cref="IndexAccessLowering.Native"/> when no override was recorded.
    /// </summary>
    /// <remarks>
    /// Lowering-input only (E2 #1056): the lowering pass reads this to build
    /// <c>IrIndexAccess.Strategy</c>; code generation reads the IR, never this accessor.
    /// Renamed with the <c>ForIr</c> suffix so nothing in <c>CodeGen/</c> can bind it.
    /// </remarks>
    public IndexAccessLowering GetIndexAccessLoweringForIr(Expression indexAccess)
    {
        return _indexAccessLowerings.TryGetValue(indexAccess, out var lowering)
            ? lowering
            : IndexAccessLowering.Native;
    }

    /// <summary>
    /// Records the normalized <see cref="GenericReference"/> fact for a generic-reference index access
    /// (<c>callee[T, ...]</c>), produced by the GenericReferenceResolver. The emitter reads this to
    /// lower the reference by <see cref="GenericReference.Kind"/> without re-deriving the callee shape
    /// (Critical Rule 2 pattern (b); #1143).
    /// </summary>
    public void SetGenericReference(Expression indexAccess, GenericReference reference)
    {
        _genericReferences[indexAccess] = reference;
    }

    /// <summary>
    /// Gets the normalized <see cref="GenericReference"/> fact for an index access, or <c>null</c> when
    /// the node is not a resolved generic reference (ordinary value indexing).
    /// </summary>
    public GenericReference? GetGenericReference(Expression indexAccess)
    {
        return _genericReferences.TryGetValue(indexAccess, out var reference) ? reference : null;
    }

    /// <summary>
    /// Records the original CLR method name resolved for a member access on a CLR-backed receiver,
    /// so codegen can preserve acronym casing without reflecting. Only set when a non-trivial CLR
    /// name was resolved.
    /// </summary>
    public void SetResolvedClrMemberName(Expression memberAccess, string clrName)
    {
        _resolvedClrMemberNames[memberAccess] = clrName;
    }

    /// <summary>
    /// Gets the original CLR method name resolved for a member access, or <c>null</c> when none was
    /// recorded (codegen then applies normal name mangling).
    /// </summary>
    /// <remarks>
    /// Lowering-input only (E2 #1056): the lowering pass reads this to build
    /// <c>IrMemberAccess.ResolvedClrMemberName</c>; code generation reads the IR, never this accessor.
    /// Renamed with the <c>ForIr</c> suffix so nothing in <c>CodeGen/</c> can bind it.
    /// </remarks>
    public string? GetResolvedClrMemberNameForIr(Expression memberAccess)
    {
        return _resolvedClrMemberNames.TryGetValue(memberAccess, out var name) ? name : null;
    }

    /// <summary>
    /// Records that a method-call member access must be emitted as a static extension-method call
    /// (<c>global::Ext.Method(receiver, args...)</c>) rather than the instance form. Set by the
    /// TypeChecker when a call resolves to a Sharpy extension method that a shadowing BCL instance
    /// method would otherwise capture (#1071, #1072, #1085).
    /// </summary>
    public void SetStaticExtensionDispatch(Expression memberAccess, StaticExtensionDispatch dispatch)
    {
        _staticExtensionDispatches[memberAccess] = dispatch;
    }

    /// <summary>
    /// Gets the static-extension dispatch decision for a method-call member access, or <c>null</c>
    /// when the call should emit as an ordinary instance-method invocation.
    /// </summary>
    /// <remarks>
    /// Lowering-input only (E2 #1056): the lowering pass reads this to build
    /// <c>IrMemberAccess.ExtensionDispatch</c>; code generation reads the IR, never this accessor.
    /// Renamed with the <c>ForIr</c> suffix so nothing in <c>CodeGen/</c> can bind it.
    /// </remarks>
    public StaticExtensionDispatch? GetStaticExtensionDispatchForIr(Expression memberAccess)
    {
        return _staticExtensionDispatches.TryGetValue(memberAccess, out var dispatch) ? dispatch : null;
    }

    /// <summary>
    /// Merges all entries from another SemanticInfo into this instance.
    /// Used to combine per-file SemanticInfo back into a project-level instance.
    /// </summary>
    public void MergeFrom(SemanticInfo other)
    {
        foreach (var kvp in other._expressionTypes)
            _expressionTypes.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._identifierSymbols)
            _identifierSymbols.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._declarationSymbols)
            _declarationSymbols.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._callTargets)
            _callTargets.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._typeAnnotations)
            _typeAnnotations.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._narrowedExpressionTypes)
            _narrowedExpressionTypes.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._narrowedReadLowerings)
            _narrowedReadLowerings.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._sequenceMaterializations)
            _sequenceMaterializations.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._typeTestLowerings)
            _typeTestLowerings.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._inferredTypeArguments)
            _inferredTypeArguments.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._memberAccessResolutions)
            _memberAccessResolutions.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._generatorFunctions)
            _generatorFunctions.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._eventAccessNodes)
            _eventAccessNodes.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._typeReferenceNodes)
            _typeReferenceNodes.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._typeFactoryArguments)
            _typeFactoryArguments.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._patternUnionCases)
            _patternUnionCases.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._patternConstants)
            _patternConstants.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._patternTypes)
            _patternTypes.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._errorRecoveryNodes)
            _errorRecoveryNodes.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._contextManagerKinds)
            _contextManagerKinds.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._withItemSymbols)
            _withItemSymbols.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._exceptHandlerSymbols)
            _exceptHandlerSymbols.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._functionDeclarationSymbols)
            _functionDeclarationSymbols.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._binaryOpLowerings)
            _binaryOpLowerings.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._indexAccessLowerings)
            _indexAccessLowerings.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._genericReferences)
            _genericReferences.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._constructorReferenceLowerings)
            _constructorReferenceLowerings.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._typeCoercionLowerings)
            _typeCoercionLowerings.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._iterableProjections)
            _iterableProjections.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._resolvedClrMemberNames)
            _resolvedClrMemberNames.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._staticExtensionDispatches)
            _staticExtensionDispatches.TryAdd(kvp.Key, kvp.Value);

        // Generator bindings are populated per-file during type checking (TypeChecker.Definitions)
        // but read from the merged project SemanticInfo by the generator sub-pipeline
        // (ProjectCompiler.Generators). Without this merge they are silently dropped and no source
        // generator runs (#1042).
        foreach (var kvp in other._generatorBindings)
            _generatorBindings.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._generatedStatements)
            _generatedStatements.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._calleeRoutings)
            _calleeRoutings.TryAdd(kvp.Key, kvp.Value);

        foreach (var (symbol, refs) in other._symbolReferences)
        {
            var bag = _symbolReferences.GetOrAdd(symbol, static _ => new ConcurrentBag<SymbolReference>());
            foreach (var reference in refs)
            {
                bag.Add(reference);
            }
        }
    }

    // === Symbol Reference Tracking ===

    private void RecordReference(Symbol symbol, Node node)
    {
        if (node.Span == null)
            return;

        var reference = new SymbolReference(CurrentFilePath, node.Span.Value, node.LineStart, node.ColumnStart);
        var bag = _symbolReferences.GetOrAdd(symbol, static _ => new ConcurrentBag<SymbolReference>());
        bag.Add(reference);
    }

    /// <summary>
    /// Gets all recorded reference locations for a symbol.
    /// Returns an empty list if no references have been recorded.
    /// </summary>
    public IReadOnlyList<SymbolReference> GetReferences(Symbol symbol)
    {
        return _symbolReferences.TryGetValue(symbol, out var bag)
            ? bag.ToArray()
            : Array.Empty<SymbolReference>();
    }

    /// <inheritdoc/>
    public IReadOnlyList<SymbolReference> FindReferencesBySymbolIdentity(string symbolName, string? declaringFilePath)
    {
        foreach (var (symbol, refs) in _symbolReferences)
        {
            if (symbol.Name == symbolName &&
                string.Equals(symbol.DeclaringFilePath, declaringFilePath, StringComparison.Ordinal))
            {
                return refs.ToArray();
            }
        }
        return Array.Empty<SymbolReference>();
    }

    /// <inheritdoc/>
    public Symbol? FindSymbolByDeclaration(string name, int line, int column)
    {
        foreach (var symbol in _symbolReferences.Keys)
        {
            if (symbol.Name == name
                && symbol.DeclarationLine == line
                && symbol.DeclarationColumn == column)
            {
                return symbol;
            }
        }

        foreach (var symbol in _identifierSymbols.Values)
        {
            if (symbol.Name == name
                && symbol.DeclarationLine == line
                && symbol.DeclarationColumn == column)
            {
                return symbol;
            }
        }

        if (_symbolTable != null)
        {
            foreach (var symbol in _symbolTable.GetAllModuleScopeSymbols())
            {
                if (symbol.Name == name
                    && symbol.DeclarationLine == line
                    && symbol.DeclarationColumn == column)
                {
                    return symbol;
                }
            }
        }

        return null;
    }
}

/// <summary>
/// Records a single location where a symbol is referenced.
/// </summary>
public record SymbolReference(string? FilePath, Text.TextSpan Span, int Line, int Column);

/// <summary>
/// Describes how a with-item's context expression implements the context manager protocol.
/// Used by codegen to decide between C# using statements and explicit Enter/Exit calls.
/// </summary>
public enum ContextManagerKind
{
    /// <summary>Implements IDisposable — use C# using statement.</summary>
    Disposable,

    /// <summary>Implements __enter__/__exit__ dunder protocol — emit Enter()/Exit() calls.</summary>
    DunderProtocol,

    /// <summary>Implements IAsyncDisposable — use C# await using statement.</summary>
    AsyncDisposable,

    /// <summary>Implements __aenter__/__aexit__ async dunder protocol — emit AenterAsync()/AexitAsync() calls.</summary>
    AsyncDunderProtocol
}

/// <summary>
/// The accessor codegen applies when reading a narrowed value at a specific read site. Each kind maps
/// to one emitter transformation, so the emitter switches on the tag alone and never re-derives
/// narrowing flow (#1081, Critical Rule 2 pattern (b)).
/// </summary>
public enum NarrowedReadKind
{
    /// <summary>Sharpy <c>T?</c> (<see cref="OptionalType"/>) narrowed to <c>T</c> — emit <c>.Unwrap()</c>.</summary>
    UnwrapOptional,

    /// <summary>Value-type <c>T | None</c> (<see cref="NullableType"/> <c>{IsValueType: true}</c>) — emit <c>.Value</c>.</summary>
    NullableValue,

    /// <summary>Reference-type <c>T | None</c> (<see cref="NullableType"/>) — emit the null-forgiving <c>!</c>.</summary>
    NullForgiving,

    /// <summary>isinstance narrowing — emit a parenthesized cast to <see cref="NarrowedReadLowering.CastTarget"/>.</summary>
    Cast
}

/// <summary>
/// The exact accessor codegen must apply at a narrowed read site, materialized per read node by the
/// TypeChecker (#1081). Node-keyed <see cref="SemanticInfo"/> record (Critical Rule 2 pattern (b)); the
/// emitter looks it up and applies the accessor without re-deriving narrowing origins.
/// </summary>
/// <param name="Kind">Which accessor to apply.</param>
/// <param name="CastTarget">
/// The target type for a <see cref="NarrowedReadKind.Cast"/> lowering (the emitter maps it to syntax and
/// routes builtin collections through the non-generic-interface rule, #912); <c>null</c> for all other kinds.
/// </param>
public sealed record NarrowedReadLowering(NarrowedReadKind Kind, SemanticType? CastTarget = null);

/// <summary>
/// How codegen emits the type test for a classified <c>isinstance</c> type operand. The TypeChecker's
/// classifier decides which applies, so the emitter switches on the tag alone and never inspects the
/// operand expression's shape (#1207, #1213, Critical Rule 2 pattern (b)).
/// </summary>
public enum TypeTestLoweringKind
{
    /// <summary>
    /// The operand denotes exactly one closed type. Emit <c>expr is T</c> against
    /// <see cref="TypeTestLowering.TestType"/>.
    /// </summary>
    ClosedType,

    /// <summary>
    /// The operand named an unparameterized builtin collection (<c>list</c>/<c>set</c>/<c>dict</c>),
    /// whose element types the test cannot know. Emit the test against the non-generic
    /// <c>Sharpy.IList</c>/<c>ISet</c>/<c>IDict</c> protocol interface — implemented by every closed
    /// instantiation via boxing adapters — rather than against the default-argument instantiation
    /// carried in <see cref="TypeTestLowering.TestType"/>, which would only match that one
    /// instantiation (#912). The parameterized spelling <c>list[int]</c> is
    /// <see cref="ClosedType"/>: it names the instantiation, so the test can be exact.
    /// </summary>
    ErasedBuiltinCollection,

    /// <summary>
    /// <b><c>except</c> clauses only.</b> The operand is a tuple of exception types with an <c>as</c>
    /// binding — <c>except (A, B) as e:</c>. C# has no multi-type catch, so the clause binds at the
    /// common base carried in <see cref="TypeTestLowering.TestType"/> and discriminates with a filter
    /// over <see cref="TypeTestLowering.Alternatives"/>:
    /// <c>catch (Base e) when (e is A || e is B)</c> (#1235).
    /// <para>
    /// This is not the isinstance tuple, which stays refused (SPY0344). The refusal exists because a
    /// successful <c>isinstance</c> must narrow to one type and a union of types is not spellable;
    /// an <c>except</c> binding has a principled type to bind at — the same common base
    /// <c>try[A | B]</c> already uses for its Result error type — so the form is supported here.
    /// </para>
    /// </summary>
    ExceptionAlternation
}

/// <summary>
/// The type test codegen must emit for an <c>isinstance</c> type operand, materialized per operand
/// node by the TypeChecker's classifier (#1207, #1213). Node-keyed <see cref="SemanticInfo"/> record
/// (Critical Rule 2 pattern (b)).
/// <para>
/// The classifier is the single authority on what the operand denotes: shapes it cannot lower to one
/// closed type are rejected at semantic time (SPY0344, SPY0345), so no un-lowerable operand reaches
/// codegen. <see cref="TestType"/> is also what the narrowing resolvers narrow to, which is what makes
/// the emitted test and the narrowed type agree by construction rather than by two parallel
/// derivations.
/// </para>
/// </summary>
/// <param name="Kind">Which emission shape to apply.</param>
/// <param name="TestType">The resolved closed type the operand denotes — also the narrowing target.</param>
/// <param name="Alternatives">
/// The individual exception types of an <see cref="TypeTestLoweringKind.ExceptionAlternation"/>,
/// in written order. <c>null</c> for every other kind — only the <c>except (A, B) as e</c> lowering
/// tests more than one type, and it still binds at the single <paramref name="TestType"/>.
/// </param>
public sealed record TypeTestLowering(
    TypeTestLoweringKind Kind,
    SemanticType TestType,
    IReadOnlyList<SemanticType>? Alternatives = null);

/// <summary>
/// The emission shape codegen applies for a safe cast (<c>value to T?</c> / <c>value as? T</c>) whose
/// source and stripped target are both plain numeric primitives. The TypeChecker classifies the pair
/// during type checking so the emitter switches on the tag alone and never inspects operand types
/// (#1110, Critical Rule 2 pattern (b)).
/// </summary>
public enum TypeCoercionLoweringKind
{
    /// <summary>
    /// Widening or identity (int→long/float32/double, long→float32/double, float32→double,
    /// double→float32, and same-type). Emit <c>Optional&lt;T&gt;.Some((T)value)</c> unconditionally — the
    /// value always fits (double→float32 maps overflow to ±∞ and preserves NaN, both representable). Never
    /// routed through the type pattern, which would raise a CS8794-class "expression is always
    /// true/false" warning on an identity source (spy-test C# compiles under TreatWarningsAsErrors).
    /// </summary>
    NumericAlwaysFits,

    /// <summary>
    /// Narrowing (double→int, double→long, float32→int, float32→long, long→int). Emit
    /// <c>global::Sharpy.NumericSafeCast.{SafeCastMethod}(value)</c>, which range-checks and returns
    /// <c>None</c> for out-of-range, NaN, or ±∞.
    /// </summary>
    NumericRangeChecked
}

/// <summary>
/// A materialized numeric safe-cast lowering decision, keyed per coercion node (Critical Rule 2 pattern
/// (b), #1110). Absent from <see cref="SemanticInfo"/> ⇒ the emitter uses its default type-pattern
/// lowering (correct for object/reference/optional/non-numeric sources).
/// </summary>
/// <param name="Kind">Which emission shape to apply.</param>
/// <param name="SafeCastMethod">
/// For <see cref="TypeCoercionLoweringKind.NumericRangeChecked"/>, the <c>Sharpy.NumericSafeCast</c> method
/// to invoke (<c>ToIntOrNone</c> / <c>ToLongOrNone</c>); <c>null</c> for
/// <see cref="TypeCoercionLoweringKind.NumericAlwaysFits"/>.
/// </param>
public sealed record TypeCoercionLowering(TypeCoercionLoweringKind Kind, string? SafeCastMethod = null);

/// <summary>
/// How codegen must project a builtin-call argument before passing it. The TypeChecker records this
/// per argument node at the single builtin-call recording choke point so the emitter switches on the
/// tag alone and never re-inspects operand types (#1154, Critical Rule 2 pattern (b)).
/// </summary>
public enum IterableProjectionKind
{
    /// <summary>
    /// The source already presents itself as <c>IEnumerable&lt;element&gt;</c> in C# (a list, set,
    /// frozenset, dict view, range/iterator, or any CLR-backed type implementing the interface) —
    /// pass it unchanged. The mark still matters: it is what makes the argument ACCEPTABLE as an
    /// iterable in this position (#1198), and the position tables are the only thing that grants it,
    /// so a user-declared <c>list[int]</c> parameter stays strict.
    /// </summary>
    Direct,

    /// <summary>
    /// Bare (or <c>| None</c>-wrapped) dict in a builtin's iterable-of-keys position — project to
    /// <c>arg.Keys()</c> (<c>DictKeyView&lt;K,V&gt; : IEnumerable&lt;K&gt;</c>). Python iterates a dict's
    /// keys, but the dict's generic enumerable surface is
    /// <c>IEnumerable&lt;KeyValuePair&lt;K,V&gt;&gt;</c>, so an unprojected dict either fails to compile
    /// (CS1503) or binds the builtin's element type to the wrong <c>KeyValuePair</c> (silent-wrong
    /// iteration / runtime crash).
    /// </summary>
    DictKeys,

    /// <summary>
    /// Tuple in an iterable position — spread to a typed array,
    /// <c>new element[] { t.Item1, …, t.ItemN }</c>. <c>System.ValueTuple</c> implements no
    /// <c>IEnumerable&lt;T&gt;</c> at all, so an unprojected tuple that the checker accepts cannot be
    /// lowered (CS1503/CS0411) — the drift #1198's planning found in <c>sorted(t)</c>,
    /// <c>min(t)</c>, <c>max(t)</c>. Recorded only when every element type unifies, so the array is
    /// always well-typed.
    /// </summary>
    TupleToArray,

    /// <summary>
    /// <c>str</c> in a builtin's iterable position — project to
    /// <c>Builtins.ListFromStr(arg)</c>, whose <c>List&lt;string&gt;</c> iterates one-character
    /// STRINGS as Python does. <c>System.String</c> is <c>IEnumerable&lt;char&gt;</c>, not
    /// <c>IEnumerable&lt;string&gt;</c>, so an unprojected str either fails to compile
    /// (<c>sorted(s)</c>, <c>set(s)</c> — CS1503) or binds C#'s <c>T</c> to <c>char</c> while Sharpy
    /// types the element <c>str</c> (<c>enumerate(s)</c>, <c>min(s)</c>, <c>max(s)</c>). The second
    /// is the worse half: it compiles, and <c>print</c> accepts both, so the divergence only
    /// surfaces when something distinguishes them — <c>min(s).upper()</c> is CS1503 and
    /// <c>len(min(s))</c> is CS1061 (#1209).
    /// </summary>
    StrToList
}

/// <summary>
/// How an argument in a builtin's iterable position binds there: the element type it iterates as and
/// the projection codegen applies to make the C# argument an <c>IEnumerable&lt;element&gt;</c>
/// (#1198). Acceptance and lowering are one record on purpose — the TypeChecker records it only for
/// sources it will also lower, so neither half can drift ahead of the other.
/// </summary>
/// <param name="Kind">The projection the emitter applies verbatim.</param>
/// <param name="ElementType">
/// The element type the source iterates as — <c>list[ElementType]</c> is the type the argument binds
/// through in the checker, and the array element type for
/// <see cref="IterableProjectionKind.TupleToArray"/>.
/// </param>
/// <param name="TupleArity">
/// For <see cref="IterableProjectionKind.TupleToArray"/>, the number of <c>.ItemN</c> members to
/// spread; 0 otherwise.
/// </param>
public sealed record IterableArgumentProjection(
    IterableProjectionKind Kind,
    SemanticType ElementType,
    int TupleArity = 0);

public sealed record GeneratorBinding(TypeSymbol GeneratorType, Decorator Trigger);

/// <summary>
/// A materialized decision that a method-call member access dispatches to a static extension method.
/// Codegen emits <c>global::{ExtensionTypeName}.{MethodName}(receiver, args...)</c> so the extension
/// is bound explicitly, never shadowed by a same-named BCL instance method (C# resolves instance
/// methods before extensions). Used for <c>str</c> methods backed by <c>Sharpy.StringExtensions</c>
/// (#1071, #1072, #1085).
/// </summary>
/// <param name="ExtensionTypeName">Fully-qualified extension class name, e.g. <c>Sharpy.StringExtensions</c>.</param>
/// <param name="MethodName">The C# method name to invoke on the extension class, e.g. <c>Split</c>.</param>
public sealed record StaticExtensionDispatch(string ExtensionTypeName, string MethodName);

/// <summary>
/// How codegen should emit an equality binary operation (<c>==</c>/<c>!=</c>).
/// The TypeChecker records this during inference because the emitter cannot re-derive
/// it from the operand types alone (it needs the same operator-resolution rules).
/// </summary>
public enum BinaryOpLowering
{
    /// <summary>Emit a native C# operator (<c>left == right</c> / <c>left != right</c>). Default.</summary>
    NativeOperator,

    /// <summary>
    /// Lower to an instance <c>Equals</c> call: <c>left.Equals(right)</c> (<c>!=</c> wraps in <c>!(...)</c>).
    /// Used for tuples and CLR value types, where the instance call avoids boxing. The
    /// instance-vs-static choice is decided here during inference so the emitter never re-derives it.
    /// </summary>
    EqualsCallInstance,

    /// <summary>
    /// Lower to a static null-safe <c>object.Equals(left, right)</c> call (<c>!=</c> wraps in <c>!(...)</c>).
    /// Used for reference types that implement <c>Equals</c>/<c>IEquatable</c> but define no
    /// <c>op_Equality</c>, where a native C# <c>==</c> would be reference equality (wrong); the static
    /// form preserves null-safety.
    /// </summary>
    EqualsCallStatic,

    /// <summary>
    /// Lower to a C# null pattern check: <c>operand is null</c> (<c>==</c>) / <c>operand is not null</c>
    /// (<c>!=</c>), where <c>operand</c> is the non-None side. Used for <c>x == None</c>/<c>x != None</c>
    /// on reference-semantics types — this bypasses any overloaded <c>op_Equality</c> and matches Python's
    /// identity fallback (a live object <c>== None</c> is <c>False</c>). Operand order is irrelevant (#901).
    /// </summary>
    NoneCheck
}

/// <summary>
/// How codegen should emit an index access (<c>obj[index]</c>). The TypeChecker records this during
/// inference so the emitter switches on the tag alone and never re-inspects operand types (or reflects
/// over CLR indexers) to pick a strategy.
/// </summary>
public enum IndexAccessLowering
{
    /// <summary>Emit a native C# element access (<c>obj[index]</c>). Default.</summary>
    Native,

    /// <summary>
    /// String indexing: <c>global::Sharpy.StringHelpers.GetItem(obj, index)</c> — returns a
    /// single-character string (not a C# <c>char</c>) and supports Python negative indexing.
    /// </summary>
    String,

    /// <summary>
    /// Array indexing: <c>global::Sharpy.ArrayHelpers.GetItem(obj, index)</c> — supports Python
    /// negative indexing over a C# array.
    /// </summary>
    Array,

    /// <summary>
    /// Multi-axis indexing into a CLR <c>params</c> indexer (e.g. numpy's <c>NdArray</c>): a
    /// <c>TupleLiteral</c> index <c>a[1, 2]</c> is spread into separate element-access arguments
    /// <c>a[1, 2]</c> rather than passed as a single tuple (#956).
    /// </summary>
    ParamsSpread,

    /// <summary>
    /// Tuple positional access: <c>t[k]</c> lowers to <c>t.Item(k+1)</c> because C# ValueTuples have
    /// no runtime indexer. The constant index is re-read from the (validated) literal by the emitter.
    /// </summary>
    TupleItem,

    /// <summary>
    /// Provably non-negative <c>list[T]</c> access: <c>xs[i]</c> lowers to
    /// <c>xs.GetItemUnchecked(i)</c>, which skips the negative-index normalization the ordinary
    /// indexer performs (bounds are still enforced, raising <c>IndexError</c>). The TypeChecker only
    /// records this when the index is provably &#8805; 0 — a non-negative int literal, or a
    /// <c>range(...)</c>-loop induction variable that is not reassigned in the loop body (#1052).
    /// </summary>
    NativeUnchecked
}

/// <summary>
/// Whether a function call by Identifier resolves to a builtin or a user-defined symbol.
/// Recorded by the TypeChecker (scope live) so the emitter (scope collapsed) applies it
/// verbatim (#1326, Critical Rule 2 pattern (b)).
/// </summary>
public enum CalleeRouting
{
    Builtin,
    UserSymbol
}
