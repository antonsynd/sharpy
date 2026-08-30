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

    // Bare declarations (`x: int`, no initializer) that the definite-assignment analysis proved are
    // assigned on ALL paths before any read. The emitter adds `= default` so the C# compiler's own
    // DA is satisfied even when Sharpy's DA is strictly stronger (e.g. for-else, while-else) (#1656).
    private readonly ConcurrentDictionary<VariableDeclaration, byte> _definitelyAssignedBareLocals =
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

    // Map expressions whose EMITTED C# type is char-based to the conversion that turns them into the
    // Sharpy `str` their semantic type says they are (#1291, Critical Rule 2 pattern (b)). Sharpy has
    // no char, so a reflected `char` is a one-character `str` at the surface; recording the conversion
    // on the PRODUCING expression means the value is a string from that point on, and every position
    // downstream — indexing, slicing, iteration, `list()`, an annotation, an argument — is ordinary
    // str handling with nothing further to know. Absent for every expression that never touched a CLR
    // char, which is all of them but these, so the default path is byte-identical.
    private readonly ConcurrentDictionary<Expression, CharMaterializationKind> _charMaterializations =
        new(ReferenceEqualityComparer.Instance);

    // Map a bare `None` node to the OPTIONAL type it must materialize as (#1478). Node-keyed
    // (Critical Rule 2 pattern (b)) because the fact belongs to the literal and there is no symbol
    // to hang it on: the SAME `None` token means C# `null` for a `T | None` destination and
    // `Optional<T>.None` for a `T?` one, and only the checker knows which destination it landed in.
    //
    // The emitter used to answer this from its own ambient target-type context, which it could only
    // do at the DIRECT value sites (initializers, returns, assignments) — the site's comment says
    // reading the ambient context in an argument would fire wrongly. So the argument position got no
    // conversion at all and `takes(None)` for `x: int?` emitted a bare `null` into a
    // `Sharpy.Optional<int>` slot: CS1503 behind SPY0908, because that Optional is a STRUCT with no
    // conversion from null. A reference payload survived only because `Optional<string>` accepts it.
    private readonly ConcurrentDictionary<Expression, OptionalType> _optionalNoneMaterializations =
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

    // #1428: augmented assignment mutation lowering — when the inplace_augassign feature is
    // enabled and the assignment matches the Classify table, the emitter emits a mutation call
    // (e.g. xs.Extend(ys)) instead of the default rebind. Keyed on the Assignment node; value
    // is the CLR method name on the Sharpy.Core collection.
    private readonly ConcurrentDictionary<Assignment, string> _augmentedAssignMutations =
        new(ReferenceEqualityComparer.Instance);

    // #1519: default-interface dispatch — when a call dispatches to a default method on an
    // interface the class doesn't override, the emitter must cast through that interface.
    // Keyed on the FunctionCall node; value is the interface name (C# spelling, post-mangling).
    private readonly ConcurrentDictionary<FunctionCall, string> _defaultInterfaceDispatches =
        new(ReferenceEqualityComparer.Instance);

    // #1519: CLR-property-call lowering — when a zero-arg call's member resolves to a CLR
    // property with no same-named method, the emitter emits property access without parens (#555).
    private readonly ConcurrentDictionary<FunctionCall, bool> _clrPropertyCallLowerings =
        new(ReferenceEqualityComparer.Instance);

    // #1672: callable-object dispatch — when an obj(args) call resolves through __call__,
    // the emitter emits obj.Invoke(args) from this recorded lowering.
    private readonly ConcurrentDictionary<FunctionCall, CallableObjectDispatch> _callableObjectDispatches =
        new(ReferenceEqualityComparer.Instance);

    // #1520: functools.partial spec — the target symbol and fixed-arg metadata resolved during
    // type checking, so the emitter reads the spec instead of re-deriving the target (#1520).
    private readonly ConcurrentDictionary<FunctionCall, FunctoolsPartialSpec> _functoolsPartialSpecs =
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

    // Map an f-string interpolation OPERAND to the runtime conversion codegen must wrap it in
    // before interpolating. Present only when the default `$"{x}"` rendering — which is
    // x.ToString() — is not what Python prints for that operand's type. Today the only recorded
    // case is an exception-typed operand: .NET's Exception.ToString() prints the type name, the
    // message AND a stack trace carrying an absolute build path, where CPython's f"{e}" is just
    // str(e) (#1480). Deciding this is a semantic question (it needs the operand's resolved type
    // and the Exception hierarchy), so it is decided once here and codegen applies it verbatim —
    // Critical Rule 2 pattern (b). Named for the mechanism rather than for exceptions so the
    // bare-CLR-sequence display case (#1453's f-string half) can join without a second dictionary.
    private readonly ConcurrentDictionary<Expression, InterpolationStrWrapping> _interpolationStrWrappings =
        new(ReferenceEqualityComparer.Instance);

    // Map patterns to their resolved union case type symbols
    // Used when a PositionalPattern or MemberAccessPattern matches a union case
    private readonly ConcurrentDictionary<Pattern, TypeSymbol> _patternUnionCases =
        new(ReferenceEqualityComparer.Instance);

    // Map MemberAccessPatterns to their resolved type symbol + the index of the type
    // segment in the Parts array. Recorded by CheckMemberAccessPattern so
    // GenerateMemberAccessValue reads the materialized fact (#1524, Rule 2).
    private readonly ConcurrentDictionary<MemberAccessPattern, (TypeSymbol TypeSymbol, int TypeIndex)> _patternMemberAccessResolutions =
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

    private readonly ConcurrentDictionary<Pattern, bool> _patternTotality =
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

    // Map parameter nodes to the variable symbol the checker binds for them. The function scope is
    // exited after type checking, and `Parameter` is a standalone record rather than a Node — so
    // FindNodeAtPosition can never return one and a declaration cursor lands on the enclosing
    // FunctionDef instead. A parameter nothing references is in no reference collection either,
    // which leaves this as the only route from the declaration back to the symbol (#1359). Mirrors
    // _withItemSymbols/_exceptHandlerSymbols, which have the same shape and the same problem.
    private readonly ConcurrentDictionary<Parameter, VariableSymbol> _parameterSymbols =
        new(ReferenceEqualityComparer.Instance);

    // Map a walrus expression / an inline `out name: T` argument to the variable symbol it binds.
    // Both bind a name from an EXPRESSION position, so no Identifier node carries the symbol; the
    // emitter reads the symbol's CodeGenInfo for the spelling and the node's TargetBinding for
    // declaration-vs-assignment (#1560 R2/R3). Mirrors _parameterSymbols in shape and merge.
    private readonly ConcurrentDictionary<WalrusExpression, VariableSymbol> _walrusSymbols =
        new(ReferenceEqualityComparer.Instance);
    private readonly ConcurrentDictionary<ModifiedArgument, VariableSymbol> _inlineOutSymbols =
        new(ReferenceEqualityComparer.Instance);

    // Map each rebinding to the binding it REPLACED in the same scope. A plain `x = ...` defines a
    // FRESH VariableSymbol (TypeChecker.CheckAssignment's simple-identifier branch) which
    // Scope.Define swaps in for the previous one, so each binding's reference collection holds only
    // the occurrences between it and the next rebinding — and a rename driven from any one of them
    // covers only that fragment (#1359).
    //
    // Only PLAIN REASSIGNMENT belongs here, because only plain reassignment is the same variable:
    // the emitter assigns to the same C# local and versions (x, x_1) solely on REDECLARATION
    // (`x: T = ...` twice), which is a genuinely different variable that must NOT rename along.
    // Loop targets never chain — both binding sites reuse an existing same-scope symbol instead of
    // defining a fresh one.
    private readonly ConcurrentDictionary<VariableSymbol, VariableSymbol> _rebindingPredecessors =
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

    // Map expressions in truthiness contexts (if, while, and/or/not, assert, ternary,
    // comprehension filter, match guard) to the lowering shape codegen must apply (#1558).
    // Keyed on the condition/operand expression; absent means no truthiness wrapping needed
    // (backward compat for positions not yet wired).
    private readonly ConcurrentDictionary<Expression, TruthinessLowering> _truthinessLowerings =
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

    private readonly ConcurrentDictionary<Expression, MultiAxisAccessLowering> _multiAxisAccessLowerings =
        new(ReferenceEqualityComparer.Instance);

    private readonly ConcurrentDictionary<Node, OperatorLowering> _operatorLowerings =
        new(ReferenceEqualityComparer.Instance);

    private readonly ConcurrentDictionary<Expression, IterationLowering> _iterationLowerings =
        new(ReferenceEqualityComparer.Instance);

    // #1642: one record per ComparisonChain node carrying the lowering of EVERY link — the same
    // classification the binary form of `Operands[i] <op> Operands[i+1]` would record (ordering
    // kind + equality strategy). Keyed on the chain, not its operands: an operand can be a BinaryOp
    // with its own OperatorLowering tag.
    private readonly ConcurrentDictionary<Expression, ComparisonChainLowering> _comparisonChainLowerings =
        new(ReferenceEqualityComparer.Instance);

    // #1572: Map a member-access expression to an interface cast the emitter must wrap the receiver
    // in before accessing the member. Only present when the member is reachable exclusively through
    // an explicitly-implemented interface (e.g. IList.IsFixedSize on List<T>). The TypeChecker
    // discovers this via CLR reflection; the emitter reads the fact and emits
    // ((InterfaceType)receiver).MemberName (Critical Rule 2 pattern (b)).
    private readonly ConcurrentDictionary<Expression, InterfaceCastLowering> _interfaceCastLowerings =
        new(ReferenceEqualityComparer.Instance);

    private readonly ConcurrentDictionary<Node, TargetBinding> _targetBindings =
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

    // #1638: Map a builtin or overloaded function name used as a value to the eta-expanded lambda
    // shape codegen must emit. The TypeChecker records the selected overload's parameter and return
    // types so the emitter generates `(T1 _p0, T2 _p1) => Sharpy.Builtins.X(_p0, _p1)` instead of
    // the bare method group `Sharpy.Builtins.X`, which breaks on struct boxing, generic inference,
    // CS0121 ambiguity, and optional/params elision. Keyed by node identity.
    private readonly ConcurrentDictionary<Expression, CallableReferenceLowering> _callableReferenceLowerings =
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

    private readonly ConcurrentDictionary<FunctionCall, string> _calleeAliasTargetNames =
        new(ReferenceEqualityComparer.Instance);

    // Map declarations to their source generator bindings (bracket attributes that resolve to SourceGenerator subclasses)
    private readonly ConcurrentDictionary<Statement, List<GeneratorBinding>> _generatorBindings =
        new(ReferenceEqualityComparer.Instance);

    // Track statements that were produced by a source generator.
    // Value is the generator name (e.g., "GenerateEquals"). Used by LSP to display
    // "Generated by @[X]" on hover (Phase 7).
    private readonly ConcurrentDictionary<Statement, string> _generatedStatements =
        new(ReferenceEqualityComparer.Instance);

    // Map a bracket attribute (@[...]) decorator to the imported .NET namespace (C# spelling) that
    // brings its attribute type into scope, recorded by DecoratorValidator when the name resolves
    // ONLY through an imported namespace (not the bare name or an always-in-scope namespace).
    // UnusedImportValidator reads it so an import used solely by a bracket attribute counts as used
    // (#1429, Critical Rule 2 pattern (b)).
    private readonly ConcurrentDictionary<Decorator, string> _bracketAttributeResolvedNamespaces =
        new(ReferenceEqualityComparer.Instance);

    // Map valued return statements in void-returning functions to the lowering codegen must apply.
    // Recorded by the TypeChecker when the return operand types as VoidType inside a VoidType function
    // (return None → elide; return void_call() → evaluate-then-return). Absent ⇒ normal return path.
    // Keyed by node identity (#1514, Critical Rule 2 pattern (b)).
    private readonly ConcurrentDictionary<ReturnStatement, ReturnLowering> _returnLowerings =
        new(ReferenceEqualityComparer.Instance);

    // Map lambda body expressions to an elision decision when the body is a NoneLiteral against
    // a void delegate target (Action). Absent ⇒ normal body emission. Keyed by node identity
    // (#1514, Critical Rule 2 pattern (b)).
    private readonly ConcurrentDictionary<Expression, LambdaBodyLowering> _lambdaBodyLowerings =
        new(ReferenceEqualityComparer.Instance);

    private readonly ConcurrentDictionary<ExpressionStatement, StatementLowering> _statementLowerings =
        new(ReferenceEqualityComparer.Instance);

    private readonly ConcurrentDictionary<Expression, SliceLowering> _sliceLowerings =
        new(ReferenceEqualityComparer.Instance);

    // Map match scrutinee expressions to a typed-null cast when the scrutinee is a bare NoneLiteral.
    // Absent ⇒ normal scrutinee emission. Void-call scrutinees are REFUSED (SPY0275), not lowered.
    // Keyed by node identity (#1526, Critical Rule 2 pattern (b)).
    private readonly ConcurrentDictionary<Expression, MatchScrutineeLowering> _matchScrutineeLowerings =
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

    public void RecordDefinitelyAssignedBareLocal(VariableDeclaration decl)
    {
        _definitelyAssignedBareLocals[decl] = 0;
    }

    public bool IsDefinitelyAssignedBareLocal(VariableDeclaration decl)
    {
        return _definitelyAssignedBareLocals.ContainsKey(decl);
    }

    // #1438: TypeChecker call-node resolution routes must record targets through
    // TypeChecker.RecordResolvedCallTarget (which also runs the deprecation check), not by
    // calling this directly — otherwise a new route silently skips @deprecated warnings.
    public void SetCallTarget(FunctionCall call, FunctionSymbol target)
    {
        _callTargets[call] = target;
    }

    public FunctionSymbol? GetCallTarget(FunctionCall call)
    {
        return _callTargets.TryGetValue(call, out var target) ? target : null;
    }

    public void SetDefaultInterfaceDispatch(FunctionCall call, string interfaceName)
    {
        _defaultInterfaceDispatches[call] = interfaceName;
    }

    public string? GetDefaultInterfaceDispatch(FunctionCall call)
    {
        return _defaultInterfaceDispatches.TryGetValue(call, out var name) ? name : null;
    }

    public void SetClrPropertyCallLowering(FunctionCall call)
    {
        _clrPropertyCallLowerings[call] = true;
    }

    public bool IsClrPropertyCall(FunctionCall call)
    {
        return _clrPropertyCallLowerings.TryGetValue(call, out _);
    }

    public void SetCallableObjectDispatch(FunctionCall call, CallableObjectDispatch dispatch)
    {
        _callableObjectDispatches[call] = dispatch;
    }

    public CallableObjectDispatch? GetCallableObjectDispatch(FunctionCall call)
    {
        return _callableObjectDispatches.TryGetValue(call, out var dispatch) ? dispatch : null;
    }

    public void SetFunctoolsPartialSpec(FunctionCall call, FunctoolsPartialSpec spec)
    {
        _functoolsPartialSpecs[call] = spec;
    }

    public FunctoolsPartialSpec? GetFunctoolsPartialSpec(FunctionCall call)
    {
        return _functoolsPartialSpecs.TryGetValue(call, out var spec) ? spec : null;
    }

    public void SetCalleeRouting(FunctionCall call, CalleeRouting routing)
    {
        _calleeRoutings[call] = routing;
    }

    public CalleeRouting? GetCalleeRouting(FunctionCall call)
    {
        return _calleeRoutings.TryGetValue(call, out var routing) ? routing : null;
    }

    public void SetCalleeAliasTargetName(FunctionCall call, string targetName)
    {
        _calleeAliasTargetNames[call] = targetName;
    }

    public string? GetCalleeAliasTargetName(FunctionCall call)
    {
        return _calleeAliasTargetNames.TryGetValue(call, out var name) ? name : null;
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
    /// Records that <paramref name="expr"/> produces a value whose emitted C# type is char-based, and
    /// how it must be converted to the Sharpy <c>str</c> its semantic type already claims (#1291).
    /// Set by the TypeChecker at the seam that reads the reflected signature, so the conversion lands
    /// on the expression that produces the char rather than on each of the many positions that consume
    /// one.
    /// </summary>
    public void SetCharMaterialization(Expression expr, CharMaterializationKind kind)
    {
        _charMaterializations[expr] = kind;
    }

    /// <summary>
    /// The char-to-str conversion an expression must be wrapped in, or <c>null</c> when its emitted
    /// type already matches its semantic type — which is every expression that did not come from a
    /// CLR char.
    /// </summary>
    public CharMaterializationKind? GetCharMaterialization(Expression expr)
    {
        return _charMaterializations.TryGetValue(expr, out var kind) ? kind : null;
    }

    /// <summary>
    /// Records that a bare <c>None</c> lands in an <see cref="OptionalType"/> destination and must
    /// therefore emit <c>Optional&lt;T&gt;.None</c> rather than C# <c>null</c> (#1478). Set by the
    /// TypeChecker at the seam that knows the destination, so the emitter re-derives nothing.
    /// </summary>
    public void SetOptionalNoneMaterialization(Expression expr, OptionalType target)
    {
        _optionalNoneMaterializations[expr] = target;
    }

    /// <summary>
    /// The optional type a bare <c>None</c> must materialize as, or <c>null</c> when it emits plain
    /// C# <c>null</c> — which is every <c>None</c> whose destination is nullable rather than
    /// optional.
    /// </summary>
    public OptionalType? GetOptionalNoneMaterialization(Expression expr)
    {
        return _optionalNoneMaterializations.TryGetValue(expr, out var target) ? target : null;
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
    /// Records that this augmented assignment should lower to a mutation call instead of the default
    /// rebind (#1428). Set by the TypeChecker when <c>inplace_augassign</c> is enabled and the
    /// assignment matches <see cref="AugmentedCollectionAssignment.Classify"/>.
    /// </summary>
    public void SetAugmentedAssignMutation(Assignment node, string clrMethodName)
    {
        _augmentedAssignMutations[node] = clrMethodName;
    }

    /// <summary>
    /// Gets the mutation method name for an augmented assignment, or <c>null</c> when the assignment
    /// keeps the default rebind semantics (flag off, or not a classified shape).
    /// </summary>
    public string? GetAugmentedAssignMutation(Assignment node)
    {
        return _augmentedAssignMutations.TryGetValue(node, out var method) ? method : null;
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
    /// Records the eta-expanded lambda shape for a builtin or overloaded function name used as a
    /// value (#1638). The emitter reads this to generate a typed lambda instead of a bare method
    /// group (Critical Rule 2 pattern (b)).
    /// </summary>
    public void SetCallableReferenceLowering(Expression reference, CallableReferenceLowering lowering)
    {
        _callableReferenceLowerings[reference] = lowering;
    }

    /// <summary>
    /// Gets the callable-reference lowering for an expression, or <c>null</c> when the node is not
    /// a builtin/overloaded function name used as a value.
    /// </summary>
    public CallableReferenceLowering? GetCallableReferenceLowering(Expression reference)
    {
        return _callableReferenceLowerings.TryGetValue(reference, out var lowering) ? lowering : null;
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

    public void SetPatternMemberAccessResolution(MemberAccessPattern pattern, TypeSymbol typeSymbol, int typeIndex)
    {
        _patternMemberAccessResolutions[pattern] = (typeSymbol, typeIndex);
    }

    public (TypeSymbol TypeSymbol, int TypeIndex)? GetPatternMemberAccessResolution(MemberAccessPattern pattern)
    {
        return _patternMemberAccessResolutions.TryGetValue(pattern, out var resolution) ? resolution : null;
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

    public void SetPatternTotality(Pattern pattern, bool isTotal)
    {
        _patternTotality[pattern] = isTotal;
    }

    public bool? GetPatternTotality(Pattern pattern)
    {
        return _patternTotality.TryGetValue(pattern, out var isTotal) ? isTotal : null;
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

    /// <summary>
    /// Records that an f-string interpolation operand must be wrapped in the given runtime
    /// conversion before it is interpolated (#1480).
    /// </summary>
    public void SetInterpolationStrWrapping(Expression expr, InterpolationStrWrapping wrapping) =>
        _interpolationStrWrappings[expr] = wrapping;

    /// <summary>
    /// The conversion an f-string interpolation operand must be wrapped in, or null when the
    /// default <c>$"{x}"</c> rendering is already correct (the overwhelmingly common case).
    /// </summary>
    public InterpolationStrWrapping? GetInterpolationStrWrapping(Expression expr) =>
        _interpolationStrWrappings.TryGetValue(expr, out var wrapping) ? wrapping : null;

    public void SetReturnLowering(ReturnStatement ret, ReturnLowering lowering) =>
        _returnLowerings[ret] = lowering;

    public ReturnLowering? GetReturnLowering(ReturnStatement ret) =>
        _returnLowerings.TryGetValue(ret, out var lowering) ? lowering : null;

    public void SetLambdaBodyLowering(Expression body, LambdaBodyLowering lowering) =>
        _lambdaBodyLowerings[body] = lowering;

    public void SetStatementLowering(ExpressionStatement stmt, StatementLowering lowering) =>
        _statementLowerings[stmt] = lowering;

    public StatementLowering? GetStatementLowering(ExpressionStatement stmt) =>
        _statementLowerings.TryGetValue(stmt, out var lowering) ? lowering : null;

    public void SetSliceLowering(Expression expr, SliceLowering lowering) =>
        _sliceLowerings[expr] = lowering;

    public SliceLowering? GetSliceLowering(Expression expr) =>
        _sliceLowerings.TryGetValue(expr, out var lowering) ? lowering : null;

    public LambdaBodyLowering? GetLambdaBodyLowering(Expression body) =>
        _lambdaBodyLowerings.TryGetValue(body, out var lowering) ? lowering : null;

    public void SetMatchScrutineeLowering(Expression scrutinee, MatchScrutineeLowering lowering) =>
        _matchScrutineeLowerings[scrutinee] = lowering;

    public MatchScrutineeLowering? GetMatchScrutineeLowering(Expression scrutinee) =>
        _matchScrutineeLowerings.TryGetValue(scrutinee, out var lowering) ? lowering : null;

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
    /// Records that a bracket-attribute decorator's type resolves through the imported .NET
    /// namespace <paramref name="clrNamespace"/> (its C# spelling), so the import that brings that
    /// namespace into scope counts as used (#1429).
    /// </summary>
    public void SetBracketAttributeResolvedNamespace(Decorator decorator, string clrNamespace)
    {
        _bracketAttributeResolvedNamespaces[decorator] = clrNamespace;
    }

    /// <summary>
    /// The imported .NET namespace (C# spelling) a bracket-attribute decorator resolves through, or
    /// null when it resolves without an import (bare name / always-in-scope) or was not recorded.
    /// </summary>
    public string? GetBracketAttributeResolvedNamespace(Decorator decorator)
    {
        return _bracketAttributeResolvedNamespaces.TryGetValue(decorator, out var ns) ? ns : null;
    }

    /// <summary>
    /// Every imported .NET namespace a bracket attribute in this file resolves through. Read by
    /// UnusedImportValidator to count imports used solely by bracket attributes (#1429).
    /// </summary>
    public IEnumerable<string> GetAllBracketAttributeResolvedNamespaces()
        => _bracketAttributeResolvedNamespaces.Values;

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
    /// Records the variable symbol a parameter binds. Called where the checker defines it, so the
    /// binding is reachable from the declaration node whether or not the body reads the parameter.
    /// </summary>
    public void SetParameterSymbol(Parameter parameter, VariableSymbol symbol)
    {
        _parameterSymbols[parameter] = symbol;
    }

    /// <inheritdoc/>
    public VariableSymbol? GetParameterSymbol(Parameter parameter)
    {
        return _parameterSymbols.TryGetValue(parameter, out var symbol) ? symbol : null;
    }

    /// <summary>Records the variable symbol a walrus expression binds (#1560 R2).</summary>
    public void SetWalrusSymbol(WalrusExpression walrus, VariableSymbol symbol)
        => _walrusSymbols[walrus] = symbol;

    /// <summary>The variable symbol a walrus expression binds, or null when unchecked.</summary>
    public VariableSymbol? GetWalrusSymbol(WalrusExpression walrus)
        => _walrusSymbols.GetValueOrDefault(walrus);

    /// <summary>Records the variable symbol an inline <c>out name: T</c> argument binds (#1560 R3).</summary>
    public void SetInlineOutSymbol(ModifiedArgument argument, VariableSymbol symbol)
        => _inlineOutSymbols[argument] = symbol;

    /// <summary>The variable symbol an inline <c>out</c> argument binds, or null when unchecked.</summary>
    public VariableSymbol? GetInlineOutSymbol(ModifiedArgument argument)
        => _inlineOutSymbols.GetValueOrDefault(argument);

    /// <summary>
    /// Records that <paramref name="rebinding"/> replaced <paramref name="predecessor"/> — the same
    /// variable, rebound. Called from the checker where both are in hand and the scope is alive.
    /// </summary>
    public void SetRebindingPredecessor(VariableSymbol rebinding, VariableSymbol predecessor)
    {
        _rebindingPredecessors[rebinding] = predecessor;
    }

    /// <summary>
    /// The binding <paramref name="variable"/> was DECLARED as — the root of its rebinding chain, or
    /// itself when it was never rebound. Predecessor lookups only, so a store check costs one walk
    /// up the chain rather than <see cref="GetBindingChain"/>'s forward scan (#1706).
    /// </summary>
    public VariableSymbol GetRootBinding(VariableSymbol variable)
    {
        var root = variable;
        var guard = _rebindingPredecessors.Count + 1;
        while (_rebindingPredecessors.TryGetValue(root, out var previous) && guard-- > 0)
            root = previous;
        return root;
    }

    /// <inheritdoc/>
    public IReadOnlyList<VariableSymbol> GetBindingChain(Symbol symbol)
    {
        if (symbol is not VariableSymbol variable)
            return System.Array.Empty<VariableSymbol>();

        if (_rebindingPredecessors.IsEmpty)
            return new[] { variable };

        // Back to the root binding. The guard is against a cycle the checker should never record;
        // a malformed chain must not hang the language server.
        var root = variable;
        var guard = _rebindingPredecessors.Count + 1;
        while (_rebindingPredecessors.TryGetValue(root, out var previous) && guard-- > 0)
            root = previous;

        // Forward from the root. Successors are found by scanning, which is cheap: the map holds
        // one entry per rebinding in the compilation and chains are a handful of links.
        var chain = new List<VariableSymbol> { root };
        var current = root;
        while (guard-- > 0)
        {
            var next = _rebindingPredecessors
                .FirstOrDefault(kvp => ReferenceEquals(kvp.Value, current)).Key;

            if (next == null)
                break;

            chain.Add(next);
            current = next;
        }

        return chain;
    }

    public void SetTargetBinding(Node node, TargetBinding binding)
        => _targetBindings[node] = binding;

    public TargetBinding? GetTargetBinding(Node node)
        => _targetBindings.GetValueOrDefault(node);

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
    /// Records how an expression in a truthiness context should be lowered by codegen (#1558).
    /// Called by the TypeChecker at every truth position (if, while, and/or/not, assert, ternary,
    /// comprehension filter, match guard) so the emitter reads the tag and never re-derives it.
    /// </summary>
    public void SetTruthinessLowering(Expression expr, TruthinessLowering lowering)
    {
        _truthinessLowerings[expr] = lowering;
    }

    /// <summary>
    /// Gets the truthiness lowering for an expression, or <c>null</c> if none was recorded.
    /// </summary>
    public TruthinessLowering? GetTruthinessLowering(Expression expr)
    {
        return _truthinessLowerings.TryGetValue(expr, out var lowering) ? lowering : null;
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

    public void SetMultiAxisAccessLowering(Expression multiAxis, MultiAxisAccessLowering lowering)
    {
        _multiAxisAccessLowerings[multiAxis] = lowering;
    }

    public MultiAxisAccessLowering? GetMultiAxisAccessLowering(Expression multiAxis)
    {
        return _multiAxisAccessLowerings.TryGetValue(multiAxis, out var lowering) ? lowering : null;
    }

    public void SetOperatorLowering(Node node, OperatorLowering lowering)
    {
        _operatorLowerings[node] = lowering;
    }

    public OperatorLowering? GetOperatorLowering(Node node)
    {
        return _operatorLowerings.TryGetValue(node, out var lowering) ? lowering : null;
    }

    public void SetIterationLowering(Expression iterator, IterationLowering lowering)
    {
        _iterationLowerings[iterator] = lowering;
    }

    public IterationLowering? GetIterationLowering(Expression iterator)
    {
        return _iterationLowerings.TryGetValue(iterator, out var lowering) ? lowering : null;
    }

    public void SetComparisonChainLowering(Expression chain, ComparisonChainLowering lowering)
    {
        _comparisonChainLowerings[chain] = lowering;
    }

    public ComparisonChainLowering? GetComparisonChainLowering(Expression chain)
    {
        return _comparisonChainLowerings.TryGetValue(chain, out var lowering) ? lowering : null;
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
    /// Records that the member access requires an interface cast on the receiver.
    /// Only set when the member is exclusively available through an explicitly-implemented interface.
    /// </summary>
    public void SetInterfaceCastLowering(Expression memberAccess, InterfaceCastLowering lowering)
    {
        _interfaceCastLowerings[memberAccess] = lowering;
    }

    /// <summary>
    /// Gets the interface cast lowering for a member access, or <c>null</c> when no cast is needed.
    /// </summary>
    public InterfaceCastLowering? GetInterfaceCastLowering(Expression memberAccess)
    {
        return _interfaceCastLowerings.TryGetValue(memberAccess, out var lowering) ? lowering : null;
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

        foreach (var kvp in other._definitelyAssignedBareLocals)
            _definitelyAssignedBareLocals.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._callTargets)
            _callTargets.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._augmentedAssignMutations)
            _augmentedAssignMutations.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._defaultInterfaceDispatches)
            _defaultInterfaceDispatches.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._clrPropertyCallLowerings)
            _clrPropertyCallLowerings.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._callableObjectDispatches)
            _callableObjectDispatches.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._functoolsPartialSpecs)
            _functoolsPartialSpecs.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._typeAnnotations)
            _typeAnnotations.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._narrowedExpressionTypes)
            _narrowedExpressionTypes.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._narrowedReadLowerings)
            _narrowedReadLowerings.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._sequenceMaterializations)
            _sequenceMaterializations.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._charMaterializations)
            _charMaterializations.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._optionalNoneMaterializations)
            _optionalNoneMaterializations.TryAdd(kvp.Key, kvp.Value);

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

        foreach (var kvp in other._interpolationStrWrappings)
            _interpolationStrWrappings.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._patternUnionCases)
            _patternUnionCases.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._patternMemberAccessResolutions)
            _patternMemberAccessResolutions.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._patternConstants)
            _patternConstants.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._patternTypes)
            _patternTypes.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._patternTotality)
            _patternTotality.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._errorRecoveryNodes)
            _errorRecoveryNodes.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._contextManagerKinds)
            _contextManagerKinds.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._withItemSymbols)
            _withItemSymbols.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._exceptHandlerSymbols)
            _exceptHandlerSymbols.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._parameterSymbols)
            _parameterSymbols.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._walrusSymbols)
            _walrusSymbols.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._inlineOutSymbols)
            _inlineOutSymbols.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._rebindingPredecessors)
            _rebindingPredecessors.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._functionDeclarationSymbols)
            _functionDeclarationSymbols.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._binaryOpLowerings)
            _binaryOpLowerings.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._truthinessLowerings)
            _truthinessLowerings.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._indexAccessLowerings)
            _indexAccessLowerings.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._multiAxisAccessLowerings)
            _multiAxisAccessLowerings.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._operatorLowerings)
            _operatorLowerings.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._iterationLowerings)
            _iterationLowerings.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._comparisonChainLowerings)
            _comparisonChainLowerings.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._genericReferences)
            _genericReferences.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._constructorReferenceLowerings)
            _constructorReferenceLowerings.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._callableReferenceLowerings)
            _callableReferenceLowerings.TryAdd(kvp.Key, kvp.Value);

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

        foreach (var kvp in other._bracketAttributeResolvedNamespaces)
            _bracketAttributeResolvedNamespaces.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._calleeRoutings)
            _calleeRoutings.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._calleeAliasTargetNames)
            _calleeAliasTargetNames.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._returnLowerings)
            _returnLowerings.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._lambdaBodyLowerings)
            _lambdaBodyLowerings.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._statementLowerings)
            _statementLowerings.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._sliceLowerings)
            _sliceLowerings.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._matchScrutineeLowerings)
            _matchScrutineeLowerings.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._interfaceCastLowerings)
            _interfaceCastLowerings.TryAdd(kvp.Key, kvp.Value);

        foreach (var kvp in other._targetBindings)
            _targetBindings.TryAdd(kvp.Key, kvp.Value);

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
/// How codegen converts a value whose emitted C# type is char-based into the Sharpy <c>str</c> its
/// semantic type claims (#1291). Sharpy has no char type — a CLR <c>char</c> is a one-character
/// <c>str</c> at the surface, which is also what Python means by <c>s[0]</c>.
/// </summary>
/// <remarks>
/// The conversion is recorded on the expression that PRODUCES the char, not on the positions that
/// consume it. That is what makes the fix complete rather than per-position: the reported repro alone
/// reached a <c>str</c> annotation, a <c>list()</c> conversion, a slice, a parameter and a
/// <c>return</c>, and each is ordinary str handling once the producer hands back a string.
/// </remarks>
public enum CharMaterializationKind
{
    /// <summary>A scalar <c>char</c> — emit <c>.ToString()</c>.</summary>
    Scalar,

    /// <summary>
    /// A <c>char[]</c> — emit an <c>Array.ConvertAll</c> to <c>string[]</c> of one-character strings.
    /// The copy is deliberate and unavoidable: a <c>string[]</c> view of a <c>char[]</c> does not
    /// exist, so unlike the CLR-sequence materialization next door there is no aliasing alternative
    /// to weigh.
    /// </summary>
    Array,

    /// <summary>
    /// An <c>IEnumerable&lt;char&gt;</c> — emit an <c>Enumerable.Select</c> to
    /// <c>IEnumerable&lt;string&gt;</c> of one-character strings (#1401).
    /// <para>
    /// This is the element half of the CLR-sequence materialization next door, not a replacement for
    /// it: the projection re-represents the elements and #1251's rule still wraps the result in the
    /// Sharpy collection, in that order — which is exactly the order
    /// <c>RoslynEmitter.GenerateExpression</c> applies the two facts in. Lazy by construction, so a
    /// value that never lands in a collection slot pays nothing but the <c>Select</c>.
    /// </para>
    /// </summary>
    Sequence,

    /// <summary>
    /// The REVERSE direction (#1402): a one-character <c>str</c> literal bound to a CLR <c>char</c>
    /// parameter — emit the C# character literal (<c>"a"</c> → <c>'a'</c>).
    /// <para>
    /// Recorded on the ARGUMENT rather than on a producer, because a <c>str</c> going IN has no
    /// char-producing expression to key on: the fact lives on the parameter and is decided at the
    /// call seam. Only a single-character LITERAL carries it — a computed <c>str</c> and a
    /// multi-character literal are refused, because taking the first character would be Sharpy
    /// inventing a truncation .NET never asked for.
    /// </para>
    /// </summary>
    Literal
}

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
    /// The isinstance tuple form now denotes <c>tuple[A, B]</c> (#1532) — one closed type.
    /// An <c>except</c> binding has a principled type to bind at — the same common base
    /// <c>try[A | B]</c> already uses for its Result error type — so the multi-catch form is
    /// supported here and structurally different from the isinstance case.
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
/// The emission shape codegen applies for a cast (<c>value to T?</c> / <c>value as? T</c> /
/// <c>value as! T</c>) whose source and stripped target are both plain numeric primitives. The
/// TypeChecker classifies the pair during type checking so the emitter switches on the tag alone and
/// never inspects operand types (#1110, #1306, Critical Rule 2 pattern (b)).
/// </summary>
public enum TypeCoercionLoweringKind
{
    /// <summary>
    /// Widening or identity (int→long/float32/double, long→float32/double, float32→double,
    /// double→float32, and same-type) in the failable form. Emit <c>Optional&lt;T&gt;.Some((T)value)</c>
    /// unconditionally — the value always fits (double→float32 maps overflow to ±∞ and preserves NaN,
    /// both representable). Never routed through the type pattern, which would raise a CS8794-class
    /// "expression is always true/false" warning on an identity source (spy-test C# compiles under
    /// TreatWarningsAsErrors).
    /// </summary>
    NumericAlwaysFits,

    /// <summary>
    /// Narrowing in the failable form (<c>as?</c>). Emit
    /// <c>global::Sharpy.NumericSafeCast.{HelperMethod}(({SourceHubType})value)</c>, which range-checks
    /// and returns <c>None</c> for out-of-range, NaN, or ±∞.
    /// </summary>
    NumericRangeChecked,

    /// <summary>
    /// Narrowing in the throwing form (<c>as!</c> / <c>to</c>). Emit
    /// <c>global::Sharpy.NumericCheckedCast.{HelperMethod}(({SourceHubType})value)</c>, which throws
    /// <c>Sharpy.OverflowError</c> out of range and <c>Sharpy.ValueError</c> for NaN. A bare C# cast
    /// here is <c>unchecked</c>, so it wrapped silently — <c>big as! int</c> printed <c>0</c> (#1306).
    /// Recorded ONLY for pairs that can fail: a widening throw-mode coercion records nothing and keeps
    /// the bare cast, so its generated C# is unchanged.
    /// </summary>
    NumericChecked
}

/// <summary>
/// A materialized numeric cast lowering decision, keyed per coercion node (Critical Rule 2 pattern
/// (b), #1110, #1306). Absent from <see cref="SemanticInfo"/> ⇒ the emitter uses its default lowering
/// for the mode: the type pattern for <c>as?</c> (correct for object/reference/optional/non-numeric
/// sources) and a bare C# cast for <c>as!</c>.
/// </summary>
/// <param name="Kind">Which emission shape to apply.</param>
/// <param name="HelperMethod">
/// For the two range-checked kinds, the helper method to invoke — <c>ToIntOrNone</c> on
/// <c>Sharpy.NumericSafeCast</c>, <c>ToInt</c> on <c>Sharpy.NumericCheckedCast</c>; <c>null</c> for
/// <see cref="TypeCoercionLoweringKind.NumericAlwaysFits"/>.
/// </param>
/// <param name="SourceHubType">
/// The C# keyword the operand is cast to before the call (<c>long</c>, <c>ulong</c>, or <c>double</c>),
/// or <c>null</c> when the operand's own type is already the hub and no cast is emitted. The helpers
/// take only those three parameter shapes; without pinning the hub a <c>uint</c> operand is
/// CS0121-ambiguous between the <c>long</c> and <c>ulong</c> overloads.
/// </param>
public sealed record TypeCoercionLowering(
    TypeCoercionLoweringKind Kind,
    string? HelperMethod = null,
    string? SourceHubType = null);

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
    NoneCheck,

    /// <summary>
    /// Lower to <c>EqualityComparer&lt;T&gt;.Default.Equals(left, right)</c>. Used for type-parameter
    /// operands where C# does not allow native <c>==</c> on unconstrained generic types.
    /// </summary>
    EqualityComparerDefault
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
    /// <summary>
    /// The call targets the BUILTIN of that name. Recorded for the <c>builtins.</c>-qualified
    /// spelling, whose C# form is the bare spelling's — the qualified syntax has none of its own
    /// (<c>Sharpy.Builtins.Dict()</c> names no method), and whether the receiver is the builtins
    /// module is a semantic fact the emitter cannot re-derive from a collapsed scope (#1322).
    /// </summary>
    Builtin,

    /// <summary>
    /// The call targets a USER symbol that shadows a builtin name (#1326).
    /// </summary>
    UserSymbol
}

/// <summary>
/// The runtime conversion an f-string interpolation operand must be wrapped in before it is
/// interpolated, when the default <c>$"{x}"</c> rendering (<c>x.ToString()</c>) is not what Python
/// prints for that operand's type. Recorded by the TypeChecker, applied verbatim by the emitter
/// (Critical Rule 2 pattern (b), #1480).
/// </summary>
public enum InterpolationStrWrapping
{
    /// <summary>
    /// Route the operand through <c>Sharpy.Builtins.Str</c> — the same function <c>str(x)</c> and
    /// the explicit <c>{x!s}</c> conversion already use, so the three spellings agree by
    /// construction rather than by three parallel implementations.
    /// </summary>
    Str
}

public enum ReturnLoweringKind
{
    /// <summary>The return operand is a NoneLiteral — elide it, emitting a bare <c>return;</c>.</summary>
    ElideNoneOperand,
    /// <summary>The return operand is a void call — evaluate it, then return: <c>{ f(); return; }</c>.</summary>
    EvaluateOperandThenReturn
}

public sealed record ReturnLowering(ReturnLoweringKind Kind);

public enum LambdaBodyLoweringKind
{
    /// <summary>The lambda body is a NoneLiteral against a void delegate — emit an empty block body.</summary>
    ElideNoneBody
}

public sealed record LambdaBodyLowering(LambdaBodyLoweringKind Kind);

public enum StatementLoweringKind
{
    PlainStatement,
    Discard,
    ElideNoneLiteral,
    ElideMethodGroupStatement
}

public sealed record StatementLowering(StatementLoweringKind Kind);

public enum MultiAxisDimensionKind { Index, Slice }
public enum MultiAxisAccessKind { IndexSpread, SliceCall }
public sealed record MultiAxisAccessLowering(
    MultiAxisAccessKind Kind,
    System.Collections.Immutable.ImmutableArray<MultiAxisDimensionKind> Dimensions);

public enum OperatorLoweringKind
{
    Native,
    TrueDivisionCastLeft,
    ShiftCountCastToInt,
    OptionalNoneTest,
    OptionalCoalesceBothOptional,
    OptionalUnwrapOr,
    StringRepeatStrLeft,
    StringRepeatStrRight,
    StringOrdinalCompare,
    TypeParameterCompareTo,
    DecimalPow,
    FloatPow,
    IntegerPowInt,
    IntegerPowLong,
    NegateLiteralInt,
    NegateLiteralLong,
    /// <summary><c>//</c> with a <c>decimal</c> operand: <c>Builtins.DecimalFloorDiv</c> (native truncating quotient) (#1658).</summary>
    DecimalFloorDivide,
    /// <summary><c>//</c> with a float32/float64 operand (no decimal): the float <c>Builtins.FloorDiv</c> overload (#1658).</summary>
    FloatFloorDivide,
    /// <summary><c>//</c> over integer operands only: the int/long <c>Builtins.FloorDiv</c> overload (#1658).</summary>
    IntegerFloorDivide,
    /// <summary><c>%</c> with a <c>decimal</c> operand: <c>Builtins.DecimalMod</c> (native truncating remainder) (#1658).</summary>
    DecimalModulo,
    /// <summary><c>%</c> over int/long/float32/float64 operands: <c>Builtins.FloorMod</c> (sign of the divisor) (#1658). User <c>__mod__</c> / CLR <c>op_Modulus</c> operands record nothing — native <c>%</c>.</summary>
    FlooredModulo,
}

public sealed record OperatorLowering(OperatorLoweringKind Kind, SemanticType? NarrowTo = null);

public enum IterationLoweringKind { EnumValues, StringEnumValues, StringChars }
public sealed record IterationLowering(IterationLoweringKind Kind);

/// <summary>
/// The lowering of one comparison — a binary <c>==</c>/<c>!=</c>/<c>&lt;</c>/… or one link of a
/// <see cref="ComparisonChain"/>. <see cref="Kind"/> is the ordering lowering
/// (<see cref="OperatorLoweringKind.StringOrdinalCompare"/>, <see cref="OperatorLoweringKind.TypeParameterCompareTo"/>
/// or <see cref="OperatorLoweringKind.Native"/>); <see cref="Equality"/> is the <c>==</c>/<c>!=</c> strategy
/// (<c>null</c> for ordering operators). Both positions are classified by the same TypeChecker helper (#1642).
/// </summary>
public sealed record ComparisonLinkLowering(OperatorLoweringKind Kind, BinaryOpLowering? Equality);

/// <summary>Per-link lowering of a <see cref="ComparisonChain"/>; <c>Links[i]</c> is operator <c>i</c> (#1642).</summary>
public sealed record ComparisonChainLowering(
    System.Collections.Immutable.ImmutableArray<ComparisonLinkLowering> Links);

public enum SliceLoweringKind { List, Array, Str, Bytes, NdArray, UserProtocol, Tuple }

public sealed record SliceLowering(
    SliceLoweringKind Kind,
    SemanticType? ResultType = null,
    int[]? TupleElementIndices = null);

public enum MatchScrutineeLoweringKind
{
    /// <summary>The scrutinee is a bare NoneLiteral — cast to <c>(object?)null</c> for a valid switch.</summary>
    CastToNullableObject
}

public sealed record MatchScrutineeLowering(MatchScrutineeLoweringKind Kind);

// FunctoolsPartialSpec (#1520) lives in FunctoolsPartialSpec.cs, sibling to
// SelfInterfaceBridgeSpec — the same fully-resolved-spec pattern, node-keyed.

/// <summary>
/// How codegen should lower an expression used in a truthiness context (<c>if</c>, <c>while</c>,
/// <c>and</c>/<c>or</c>/<c>not</c>, <c>assert</c>, ternary, comprehension filter, match guard).
/// The TypeChecker records this during checking so the emitter switches on the tag alone and
/// never re-inspects the operand type to pick a conversion (Critical Rule 2 pattern (b), #1558).
/// </summary>
public enum TruthinessLowering
{
    /// <summary>Expression is already <c>bool</c> — no conversion needed.</summary>
    NativeBool,

    /// <summary><c>int</c>: emit <c>x != 0</c>.</summary>
    IntNotZero,

    /// <summary><c>float</c> (double): emit <c>x != 0.0d</c>.</summary>
    FloatNotZero,

    /// <summary><c>long</c>: emit <c>x != 0L</c>.</summary>
    LongNotZero,

    /// <summary><c>str</c>: emit <c>x.Length &gt; 0</c>.</summary>
    StringNotEmpty,

    /// <summary><c>bytes</c>: emit <c>x.Count &gt; 0</c>.</summary>
    BytesNotEmpty,

    /// <summary>Collection implementing <c>ISized</c>: emit <c>x.Count &gt; 0</c>.</summary>
    CollectionNotEmpty,

    /// <summary><c>Optional&lt;T&gt;</c>: emit <c>x.IsSome</c>.</summary>
    OptionalIsSome,

    /// <summary>Nullable <c>T?</c>: emit <c>x != null</c>.</summary>
    NullableNotNull,

    /// <summary>UDT implementing <c>IBoolConvertible</c> (has <c>__bool__</c>): emit <c>x.IsTrue</c>.</summary>
    BoolConvertible,

    /// <summary>UDT implementing <c>ISized</c> (has <c>__len__</c> but not <c>__bool__</c>): emit <c>x.Count &gt; 0</c>.</summary>
    SizedNotEmpty,

    /// <summary><c>NoneType</c>: always false (emit <c>false</c>).</summary>
    AlwaysFalse
}

/// <summary>
/// Records that a member access requires casting the receiver to a CLR interface
/// before accessing the member (explicitly-implemented interface members, #1572).
/// </summary>
/// <param name="InterfaceTypeName">
/// The arity-stripped CLR full name of the interface (e.g. <c>System.Collections.IList</c> or
/// <c>System.Collections.Generic.ICollection</c>). Never carries a <c>`N</c> suffix.
/// </param>
/// <param name="TypeArguments">
/// The closed type arguments of a generic interface, already mapped to semantic types so the
/// emitter can render them without reflection; empty for a non-generic interface.
/// </param>
public sealed record InterfaceCastLowering(string InterfaceTypeName, IReadOnlyList<SemanticType> TypeArguments);

public enum TargetBindingKind { Declares, Rebinds }

public sealed record TargetBinding(TargetBindingKind Kind);

/// <summary>
/// Recorded on a <see cref="FunctionCall"/> whose callee resolves through <c>__call__</c>.
/// The emitter reads this to emit <c>obj.Invoke(args)</c> instead of <c>obj(args)</c>.
/// </summary>
/// <param name="InvokeMethodName">The CLR method name to emit (always "Invoke").</param>
/// <param name="ReturnType">The return type of the <c>__call__</c> method.</param>
public sealed record CallableObjectDispatch(string InvokeMethodName, SemanticType ReturnType);
