using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
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
internal sealed class LoweringPass
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

        return new IrCompilation(irModules.ToImmutable(), new IrIndex(state.Index), state.WithItems);
    }

    private static IrStatement LowerStatement(Statement statement, SemanticInfo semanticInfo, LoweringState state)
    {
        if (statement is WithStatement withStatement)
            return LowerWithStatement(withStatement, semanticInfo, state);

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
    /// lowering fact — a static-extension dispatch (E2 #1056, migrates _staticExtensionDispatches).
    /// Returns <c>null</c> (falls back to an opaque wrapper) for ordinary member accesses.
    /// </summary>
    private static IrMemberAccess? TryLowerMemberAccess(MemberAccess memberAccess, SemanticInfo semanticInfo, LoweringState state)
    {
        var extensionDispatch = semanticInfo.GetStaticExtensionDispatchForIr(memberAccess);
        if (extensionDispatch is null)
            return null;

        var receiver = LowerExpression(memberAccess.Object, semanticInfo, state);
        return new IrMemberAccess(
            receiver,
            extensionDispatch,
            ResolvedClrMemberName: null,
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
internal sealed record IrCompilation(
    ImmutableArray<IrModule> Modules,
    IrIndex Index,
    IReadOnlyDictionary<WithItem, IrWithItem> WithItems);

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
