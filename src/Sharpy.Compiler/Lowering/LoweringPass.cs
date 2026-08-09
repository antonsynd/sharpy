using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Text;

namespace Sharpy.Compiler.Lowering;

/// <summary>
/// Builds the lowering IR from the type-checked AST. A single deterministic top-down walk visits
/// each AST node exactly once and produces exactly one IR node per lowered construct, keyed back to
/// the originating AST node in the <see cref="IrCompilation.Index"/> (Design Decision 1a).
/// </summary>
/// <remarks>
/// <para>
/// The pass runs <b>once per project, after <c>SemanticInfo.MergeFrom</c></b> (Design Decision 2),
/// so it observes the merged, project-level semantic facts rather than a per-file slice. It reads
/// each materialized fact — never re-deriving one — so no new inference enters code generation
/// (emitter purity, B2, is preserved).
/// </para>
/// <para>
/// In E2 Phase 1 the pass emits only totality wrappers
/// (<see cref="IrOpaqueExpression"/>/<see cref="IrOpaqueStatement"/>); the typed nodes exist in the
/// vocabulary and later migration phases switch individual constructs onto them. The output is not
/// yet read by any backend.
/// </para>
/// </remarks>
internal sealed partial class LoweringPass
{
    /// <summary>
    /// Lowers every module to its IR form and returns the whole-project
    /// <see cref="IrCompilation"/> together with the AST-to-IR index.
    /// </summary>
    /// <param name="modules">The modules to lower, each paired with its source file path. The
    /// caller supplies these in a deterministic (source-path-ordered) sequence so the resulting
    /// module order does not depend on enumeration order.</param>
    /// <param name="semanticInfo">The merged, project-level semantic facts.</param>
    /// <param name="symbolTable">The global symbol table. Consumed by later migration phases; the
    /// v1 opaque lowering does not need it.</param>
    public IrCompilation Lower(
        IReadOnlyList<(string FilePath, Module Ast)> modules,
        SemanticInfo semanticInfo,
        SymbolTable symbolTable)
    {
        var state = new LoweringState();

        var irModules = ImmutableArray.CreateBuilder<IrModule>(modules.Count);
        foreach (var (filePath, ast) in modules)
        {
            var body = ImmutableArray.CreateBuilder<IrStatement>(ast.Body.Length);
            foreach (var statement in ast.Body)
                body.Add(LowerStatement(statement, semanticInfo, state));
            irModules.Add(new IrModule(filePath, body.ToImmutable()));
        }

        return new IrCompilation(irModules.ToImmutable(), new IrIndex(state.Index), state.WithItems, state.GeneratorFunctions);
    }

    private static IrStatement LowerStatement(Statement statement, SemanticInfo semanticInfo, LoweringState state)
    {
        // Record the generator-routing fact on the IR (E2 #1056, migrates _generatorFunctions's
        // codegen consumers). The walk visits every FunctionDef — module, method, and nested — so
        // this captures them all. yield emission stays in the emitter (Roslyn builds the machine).
        if (statement is FunctionDef functionDef && semanticInfo.IsGenerator(functionDef))
            state.GeneratorFunctions.Add(functionDef);

        if (statement is WithStatement withStatement)
            return LowerWithStatement(withStatement, semanticInfo, state);

        // A defer lowers to an explicit scope-exit guard rather than an opaque wrapper (E2 #1056,
        // migrates the defer transform). The suite-split/LIFO nesting stays emitter-side.
        if (statement is DeferStatement deferStatement)
        {
            var guard = LowerDefer(deferStatement, semanticInfo, state);
            state.Index[deferStatement] = guard;
            return guard;
        }

        var children = LowerChildren(statement, semanticInfo, state);
        var ir = new IrOpaqueStatement(statement, SpanOf(statement), children);
        state.Index[statement] = ir;
        return ir;
    }

    /// <summary>
    /// Lowers a <c>with</c> statement, producing an <see cref="IrWithItem"/> per item that carries
    /// the context-manager kind (E2 #1056, migrates _contextManagerKinds). The statement itself
    /// stays an opaque wrapper — its structural <c>using</c>/try lowering is Phase 3 — and each
    /// context expression is lowered exactly once, referenced by both the opaque children and its
    /// <see cref="IrWithItem"/>, preserving totality. A <c>WithItem</c> is not an AST
    /// <see cref="Node"/>, so the IrWithItems live in a dedicated reference-keyed lookup rather than
    /// the node-keyed index.
    /// </summary>
    private static IrStatement LowerWithStatement(WithStatement withStatement, SemanticInfo semanticInfo, LoweringState state)
    {
        var children = ImmutableArray.CreateBuilder<IrNode>();

        // WithStatement.GetChildNodes yields all context expressions, then all body statements —
        // mirror that order so the opaque children match a plain LowerChildren walk (totality).
        foreach (var item in withStatement.Items)
        {
            var contextExpr = LowerExpression(item.ContextExpression, semanticInfo, state);
            children.Add(contextExpr);

            var kind = semanticInfo.GetContextManagerKindForIr(item.ContextExpression);
            if (kind is { } contextManagerKind)
            {
                // _withItemSymbols is an ISemanticQuery/LSP fact, not a codegen consumer, so the
                // as-variable (AsVar) is not folded here — deferred (see report).
                state.WithItems[item] = new IrWithItem(contextExpr, contextManagerKind, null, item.Span ?? TextSpan.Empty);
            }
        }

        foreach (var bodyStatement in withStatement.Body)
            children.Add(LowerStatement(bodyStatement, semanticInfo, state));

        var ir = new IrOpaqueStatement(withStatement, SpanOf(withStatement), children.ToImmutable());
        state.Index[withStatement] = ir;
        return ir;
    }

    private static IrExpression LowerExpression(Expression expression, SemanticInfo semanticInfo, LoweringState state)
    {
        IrExpression ir = expression switch
        {
            // Constant-folded integer exponentiation lowers to a leaf constant: the operands are
            // folded away, so they are not lowered or indexed (E2 #1056, migrates _foldedIntegerConstants).
            //
            // WHY ONLY `**` FOLDS UNCONDITIONALLY (#1335). This is not the first step of a general
            // constant-folding policy — it exists because `**` has no C# operator, so an unfolded
            // constant power would have to emit a `CheckedIntPow` call for a value already known at
            // compile time. `+ - *` deliberately ride Roslyn's own constant folding instead: SPY0348
            // guarantees no overflowing constant reaches the emitter, so `print(2 + 3)` emits `2 + 3`
            // and the C# compiler folds it. Sharpy-side folding of those operators happens only under
            // the opt-in `opt_const_fold` pass (ConstFoldPass). Emitting fewer decisions here keeps the
            // generated C# readable and keeps one folding authority per operator family.
            BinaryOp binOp when TryLowerFoldedPower(binOp, semanticInfo) is { } folded => folded,
            // Equality (==/!=) carries its lowering strategy on the IR node (E2 #1056, migrates
            // _binaryOpLowerings).
            BinaryOp { Operator: BinaryOperator.Equal or BinaryOperator.NotEqual } eqOp
                => LowerEqualityComparison(eqOp, semanticInfo, state),
            // Index access (obj[i]) carries its lowering strategy on the IR node (E2 #1056, migrates
            // _indexAccessLowerings).
            IndexAccess indexAccess => LowerIndexAccess(indexAccess, semanticInfo, state),
            // Member access (obj.member) carries the static-extension-dispatch and resolved-CLR-name
            // facts on the IR node (E2 #1056, migrates _staticExtensionDispatches / _resolvedClrMemberNames).
            MemberAccess memberAccess when TryLowerMemberAccess(memberAccess, semanticInfo, state) is { } irMember
                => irMember,
            // List/set/dict comprehensions carry their imperative-loop decisions on the IR node
            // (E2 #1056, migrates the comprehension transform). Dict-spread comprehensions are a
            // separate transform and stay opaque for now.
            ListComprehension listComp
                => LowerComprehension(listComp, BuiltinNames.List, listComp.Clauses,
                    listComp.Element is SpreadElement, semanticInfo, state),
            SetComprehension setComp
                => LowerComprehension(setComp, BuiltinNames.Set, setComp.Clauses,
                    setComp.Element is SpreadElement, semanticInfo, state),
            DictComprehension dictComp
                => LowerComprehension(dictComp, BuiltinNames.Dict, dictComp.Clauses,
                    elementIsSpread: false, semanticInfo, state),
            _ => new IrOpaqueExpression(expression, semanticInfo.GetExpressionType(expression), SpanOf(expression),
                LowerChildren(expression, semanticInfo, state)),
        };
        state.Index[expression] = ir;
        return ir;
    }

    private static IrEqualityComparison LowerEqualityComparison(BinaryOp binOp, SemanticInfo semanticInfo, LoweringState state)
    {
        var left = LowerExpression(binOp.Left, semanticInfo, state);
        var right = LowerExpression(binOp.Right, semanticInfo, state);
        return new IrEqualityComparison(
            left,
            right,
            semanticInfo.GetBinaryOpLoweringForIr(binOp),
            semanticInfo.GetExpressionType(binOp),
            SpanOf(binOp));
    }

    /// <summary>
    /// Lowers a member access to an <see cref="IrMemberAccess"/> when it carries a member-keyed
    /// lowering fact — a static-extension dispatch (E2 #1056, migrates _staticExtensionDispatches) or
    /// a resolved CLR member name (migrates _resolvedClrMemberNames). Returns <c>null</c> (falls back
    /// to an opaque wrapper) for ordinary member accesses that carry neither.
    /// </summary>
    private static IrMemberAccess? TryLowerMemberAccess(MemberAccess memberAccess, SemanticInfo semanticInfo, LoweringState state)
    {
        var extensionDispatch = semanticInfo.GetStaticExtensionDispatchForIr(memberAccess);
        var resolvedClrMemberName = semanticInfo.GetResolvedClrMemberNameForIr(memberAccess);
        if (extensionDispatch is null && resolvedClrMemberName is null)
            return null;

        var receiver = LowerExpression(memberAccess.Object, semanticInfo, state);
        return new IrMemberAccess(
            receiver,
            extensionDispatch,
            resolvedClrMemberName,
            semanticInfo.GetExpressionType(memberAccess),
            SpanOf(memberAccess));
    }

    private static IrIndexAccess LowerIndexAccess(IndexAccess indexAccess, SemanticInfo semanticInfo, LoweringState state)
    {
        var receiver = LowerExpression(indexAccess.Object, semanticInfo, state);
        var argument = LowerExpression(indexAccess.Index, semanticInfo, state);
        return new IrIndexAccess(
            receiver,
            ImmutableArray.Create(argument),
            semanticInfo.GetIndexAccessLoweringForIr(indexAccess),
            semanticInfo.GetExpressionType(indexAccess),
            SpanOf(indexAccess));
    }

    /// <summary>
    /// Re-derives a constant integer power (<c>base ** exponent</c>) into an <see cref="IrConstant"/>,
    /// mirroring the type checker's <c>TryFoldIntegerPower</c> fold conditions exactly (same pure
    /// <see cref="IntegerConstantEvaluator"/>). Returns <c>null</c> when the expression is not a folded
    /// constant power. The result-type (<c>int</c>/<c>long</c>) is read from the already-inferred
    /// expression type; overflow beyond <c>long</c> errored earlier (SPY0328) and never reaches here,
    /// but is guarded defensively.
    /// </summary>
    private static IrConstant? TryLowerFoldedPower(BinaryOp binOp, SemanticInfo semanticInfo)
    {
        if (binOp.Operator != BinaryOperator.Power)
            return null;

        if (!IntegerConstantEvaluator.TryGetConstantInteger(binOp.Left, out var baseValue)
            || !IntegerConstantEvaluator.TryGetConstantInteger(binOp.Right, out var exponent))
            return null;

        // Negative exponents keep the runtime (double) path; a constant exponent larger than int32
        // can never produce a fixed-width result. Both mirror TryFoldIntegerPower.
        if (exponent.Sign < 0 || exponent > int.MaxValue)
            return null;

        var result = System.Numerics.BigInteger.Pow(baseValue, (int)exponent);
        if (result < long.MinValue || result > long.MaxValue)
            return null;

        return new IrConstant((long)result, semanticInfo.GetExpressionType(binOp), SpanOf(binOp));
    }

    private static ImmutableArray<IrNode> LowerChildren(Node node, SemanticInfo semanticInfo, LoweringState state)
    {
        ImmutableArray<IrNode>.Builder? builder = null;
        foreach (var child in node.GetChildNodes())
        {
            builder ??= ImmutableArray.CreateBuilder<IrNode>();
            switch (child)
            {
                case Statement statement:
                    builder.Add(LowerStatement(statement, semanticInfo, state));
                    break;
                case Expression expression:
                    builder.Add(LowerExpression(expression, semanticInfo, state));
                    break;
                default:
                    // Structural helper nodes (comprehension clauses, subscript dimensions, ...)
                    // are neither Expression nor Statement and get no IR node of their own in v1;
                    // flatten through them so their Expression/Statement descendants still lower to
                    // exactly one IR node each.
                    builder.AddRange(LowerChildren(child, semanticInfo, state));
                    break;
            }
        }

        return builder?.ToImmutable() ?? ImmutableArray<IrNode>.Empty;
    }

    private static TextSpan SpanOf(Node node) => node.Span ?? TextSpan.Empty;

    /// <summary>
    /// Mutable accumulation state for a single lowering run: the reference-keyed AST→IR index and the
    /// with-item lookup. A <c>WithItem</c> is not an AST <see cref="Node"/>, so it cannot live in the
    /// node-keyed <see cref="Index"/>; both use reference equality because AST records have value
    /// equality but must be distinguished by identity (same discipline as SemanticInfo's side-tables).
    /// </summary>
    private sealed class LoweringState
    {
        public Dictionary<Node, IrNode> Index { get; } = new(ReferenceEqualityComparer.Instance);
        public Dictionary<WithItem, IrWithItem> WithItems { get; } = new(ReferenceEqualityComparer.Instance);
        public HashSet<FunctionDef> GeneratorFunctions { get; } = new(ReferenceEqualityComparer.Instance);
    }
}

/// <summary>
/// The lowering IR for an entire project: one <see cref="IrModule"/> per source file, the
/// <see cref="IrIndex"/> mapping each originating AST node back to its IR node, and the
/// <see cref="WithItems"/> lookup for <c>with</c>-item facts (with-items are not AST nodes).
/// </summary>
/// <param name="Modules">The per-file IR modules, in source-path order.</param>
/// <param name="Index">The AST-to-IR index (transitional scaffolding, Design Decision 1a).</param>
/// <param name="WithItems">Reference-keyed lookup from a <c>with</c>-item to its lowered
/// <see cref="IrWithItem"/> (E2 #1056, migrates _contextManagerKinds).</param>
/// <param name="GeneratorFunctions">Reference-keyed set of the <c>FunctionDef</c>s that are
/// generators (E2 #1056, migrates _generatorFunctions's codegen consumers). Both this and
/// <see cref="WithItems"/> are IR side-lookups pending typed function/with nodes.</param>
internal sealed record IrCompilation(
    ImmutableArray<IrModule> Modules,
    IrIndex Index,
    IReadOnlyDictionary<WithItem, IrWithItem> WithItems,
    IReadOnlySet<FunctionDef> GeneratorFunctions)
{
    private static readonly IReadOnlyDictionary<Expression, IrConstant> EmptyFolds =
        new Dictionary<Expression, IrConstant>(ReferenceEqualityComparer.Instance);

    private static readonly IReadOnlyDictionary<Expression, IrLoweredLoop> EmptyLoops =
        new Dictionary<Expression, IrLoweredLoop>(ReferenceEqualityComparer.Instance);

    private static readonly IReadOnlySet<ListLiteral> EmptyStackLiterals =
        new HashSet<ListLiteral>(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// AST expressions the E3 const-folding pass (<c>opt_const_fold</c>, #640) reduced to a compile-time
    /// <see cref="IrConstant"/>, keyed by the expression node the AST-driven emitter will generate. Empty
    /// unless the pass ran for a module, so the default (flag-off) emitter path is byte-identical. The
    /// pass manager fills it from the rewritten modules' <see cref="IrConstant.SourceAst"/> entries.
    /// </summary>
    public IReadOnlyDictionary<Expression, IrConstant> FoldedConstants { get; init; } = EmptyFolds;

    /// <summary>
    /// Comprehensions the E3 comprehension pass (<c>opt_comprehension_fusion</c>, #1057) optimized,
    /// keyed by the comprehension AST node the emitter generates. Empty unless the pass ran, so the
    /// default emitter path is byte-identical; the emitter reads the optimized <see cref="IrLoweredLoop"/>
    /// from here in preference to the initial-lowering copy in <see cref="Index"/>.
    /// </summary>
    public IReadOnlyDictionary<Expression, IrLoweredLoop> OptimizedComprehensions { get; init; } = EmptyLoops;

    /// <summary>
    /// List literals the E3 stack-collections pass (<c>opt_stack_collections</c>, #1057) proved
    /// non-escaping (their only use is as a <c>for</c>-statement iterator), keyed by the literal AST
    /// node the emitter generates. The emitter emits <c>new T[] { ... }</c> for exactly these literals
    /// instead of the <c>Sharpy.List</c> wrapper. Empty unless the pass ran for a module, so the default
    /// (flag-off) emitter path is byte-identical; the pass manager fills it from the rewritten modules'
    /// <see cref="IrStackArray"/> nodes.
    /// </summary>
    public IReadOnlySet<ListLiteral> StackAllocatedLiterals { get; init; } = EmptyStackLiterals;

    /// <summary>
    /// Whether <paramref name="functionDef"/> is a generator (its body contains <c>yield</c>), read
    /// by code generation to route iterator/method emission. Replaces the emitter's former
    /// <c>SemanticInfo.IsGenerator</c> read (E2 #1056).
    /// </summary>
    public bool IsGenerator(FunctionDef functionDef) => GeneratorFunctions.Contains(functionDef);
}

/// <summary>
/// The lowering IR for a single source file: the lowered top-level statements.
/// </summary>
/// <param name="FilePath">The source file this module was lowered from.</param>
/// <param name="Body">The lowered top-level statements, in source order.</param>
internal sealed record IrModule(string FilePath, ImmutableArray<IrStatement> Body);

/// <summary>
/// A reference-keyed map from AST node to the IR node it lowered to. This is transitional
/// scaffolding: while the emitter still dispatches over the AST, a migrated fact is read by looking
/// the AST node up here and reading the IR node's field (Design Decision 1a). It is owned by the
/// <c>Lowering</c> component, built once post-merge, and never itself merged.
/// </summary>
internal sealed class IrIndex : IReadOnlyDictionary<Node, IrNode>
{
    private readonly IReadOnlyDictionary<Node, IrNode> _map;

    public IrIndex(IReadOnlyDictionary<Node, IrNode> map) => _map = map;

    /// <inheritdoc/>
    public IrNode this[Node key] => _map[key];

    /// <inheritdoc/>
    public IEnumerable<Node> Keys => _map.Keys;

    /// <inheritdoc/>
    public IEnumerable<IrNode> Values => _map.Values;

    /// <inheritdoc/>
    public int Count => _map.Count;

    /// <inheritdoc/>
    public bool ContainsKey(Node key) => _map.ContainsKey(key);

    /// <inheritdoc/>
    public bool TryGetValue(Node key, [MaybeNullWhen(false)] out IrNode value) => _map.TryGetValue(key, out value);

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<Node, IrNode>> GetEnumerator() => _map.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _map.GetEnumerator();
}
