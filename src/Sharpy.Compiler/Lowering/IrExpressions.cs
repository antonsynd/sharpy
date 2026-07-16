using System.Collections.Immutable;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Text;

namespace Sharpy.Compiler.Lowering;

/// <summary>
/// Base type for IR nodes that denote a value. Every expression node carries the resolved
/// <see cref="IrNode.Type"/> the type checker inferred, so a backend reads types from the tree
/// rather than recomputing them.
/// </summary>
internal abstract record IrExpression(SemanticType? Type, TextSpan Span) : IrNode(Type, Span);

/// <summary>
/// A lowered equality comparison (<c>==</c>/<c>!=</c>). Carries the emission
/// <see cref="Strategy"/> as a field — the fact that today lives node-keyed in
/// <c>SemanticInfo._binaryOpLowerings</c> — so a backend switches on the tag alone and never
/// re-derives operator resolution.
/// </summary>
/// <param name="Left">The lowered left operand.</param>
/// <param name="Right">The lowered right operand.</param>
/// <param name="Strategy">How code generation must emit the comparison (native operator, instance
/// or static <c>Equals</c> call, or null pattern check).</param>
/// <param name="Type">The result type (<c>bool</c>).</param>
/// <param name="Span">The originating source span.</param>
internal sealed record IrEqualityComparison(
    IrExpression Left,
    IrExpression Right,
    BinaryOpLowering Strategy,
    SemanticType? Type,
    TextSpan Span) : IrExpression(Type, Span)
{
    /// <inheritdoc/>
    public override ImmutableArray<IrNode> Children => ImmutableArray.Create<IrNode>(Left, Right);
}

/// <summary>
/// A lowered index access (<c>obj[index]</c>). Carries the emission <see cref="Strategy"/> — the
/// fact that today lives node-keyed in <c>SemanticInfo._indexAccessLowerings</c> — so a backend
/// switches on the tag alone and never re-inspects operand types or reflects over CLR indexers.
/// </summary>
/// <param name="Receiver">The lowered object being indexed.</param>
/// <param name="Arguments">The lowered index argument(s); more than one for a multi-axis
/// (<c>params</c>) subscript.</param>
/// <param name="Strategy">How code generation must emit the access (native element access, string
/// or array helper, params spread, tuple item, or unchecked list access).</param>
/// <param name="Type">The element type produced by the access.</param>
/// <param name="Span">The originating source span.</param>
internal sealed record IrIndexAccess(
    IrExpression Receiver,
    ImmutableArray<IrExpression> Arguments,
    IndexAccessLowering Strategy,
    SemanticType? Type,
    TextSpan Span) : IrExpression(Type, Span)
{
    /// <inheritdoc/>
    public override ImmutableArray<IrNode> Children
    {
        get
        {
            var builder = ImmutableArray.CreateBuilder<IrNode>(Arguments.Length + 1);
            builder.Add(Receiver);
            foreach (var argument in Arguments)
                builder.Add(argument);
            return builder.ToImmutable();
        }
    }
}

/// <summary>
/// A lowered call. Carries the lowering facts that today live node-keyed in
/// <c>SemanticInfo</c>: the resolved <see cref="Target"/> (<c>_callTargets</c>), the
/// <see cref="ExtensionDispatch"/> decision (<c>_staticExtensionDispatches</c>) that routes a
/// shadowed <c>str</c> method to its extension class, and the <see cref="ResolvedClrMemberName"/>
/// (<c>_resolvedClrMemberNames</c>) that preserves CLR acronym casing without reflecting.
/// </summary>
/// <param name="Receiver">The lowered receiver for a method call, or <c>null</c> for a free call.</param>
/// <param name="Target">The resolved function symbol, when the call bound to one.</param>
/// <param name="Arguments">The lowered positional arguments.</param>
/// <param name="ExtensionDispatch">Set when the call must emit as a static extension-method
/// invocation (<c>global::Ext.Method(receiver, args...)</c>) rather than the instance form.</param>
/// <param name="ResolvedClrMemberName">The original CLR member name to emit, when acronym casing
/// must be preserved; otherwise <c>null</c> (ordinary name mangling applies).</param>
/// <param name="Type">The call's result type.</param>
/// <param name="Span">The originating source span.</param>
internal sealed record IrCall(
    IrExpression? Receiver,
    FunctionSymbol? Target,
    ImmutableArray<IrExpression> Arguments,
    StaticExtensionDispatch? ExtensionDispatch,
    string? ResolvedClrMemberName,
    SemanticType? Type,
    TextSpan Span) : IrExpression(Type, Span)
{
    /// <inheritdoc/>
    public override ImmutableArray<IrNode> Children
    {
        get
        {
            var builder = ImmutableArray.CreateBuilder<IrNode>(
                Arguments.Length + (Receiver is null ? 0 : 1));
            if (Receiver is not null)
                builder.Add(Receiver);
            foreach (var argument in Arguments)
                builder.Add(argument);
            return builder.ToImmutable();
        }
    }
}

/// <summary>
/// A lowered member access (<c>obj.member</c>) that carries the member-keyed lowering facts today
/// held in <c>SemanticInfo</c>: the <see cref="ExtensionDispatch"/> decision
/// (<c>_staticExtensionDispatches</c>) that routes a shadowed <c>str</c> method to its extension
/// class, and the <see cref="ResolvedClrMemberName"/> (<c>_resolvedClrMemberNames</c>) that preserves
/// CLR acronym casing without reflecting. Both are keyed by the member-access node, so they live here
/// rather than on <see cref="IrCall"/>.
/// </summary>
/// <param name="Receiver">The lowered receiver (the <c>obj</c> in <c>obj.member</c>).</param>
/// <param name="ExtensionDispatch">Set when a method call on this member access must emit as a static
/// extension-method invocation; otherwise <c>null</c>.</param>
/// <param name="ResolvedClrMemberName">The original CLR member name to emit, when acronym casing must
/// be preserved; otherwise <c>null</c> (ordinary name mangling applies).</param>
/// <param name="Type">The member access's resolved type.</param>
/// <param name="Span">The originating source span.</param>
internal sealed record IrMemberAccess(
    IrExpression Receiver,
    StaticExtensionDispatch? ExtensionDispatch,
    string? ResolvedClrMemberName,
    SemanticType? Type,
    TextSpan Span) : IrExpression(Type, Span)
{
    /// <inheritdoc/>
    public override ImmutableArray<IrNode> Children => ImmutableArray.Create<IrNode>(Receiver);
}

/// <summary>
/// A compile-time constant value: the fact that today lives node-keyed in
/// <c>SemanticInfo._foldedIntegerConstants</c>. A backend emits <see cref="Value"/> as a literal
/// directly instead of a runtime computation; the inferred width (<c>int</c>/<c>long</c>) is on
/// <see cref="IrNode.Type"/>.
/// </summary>
/// <param name="Value">The constant value (e.g. a boxed <c>long</c> for folded integer arithmetic).</param>
/// <param name="Type">The constant's type.</param>
/// <param name="Span">The originating source span.</param>
internal sealed record IrConstant(
    object Value,
    SemanticType? Type,
    TextSpan Span) : IrExpression(Type, Span)
{
    /// <inheritdoc/>
    public override ImmutableArray<IrNode> Children => ImmutableArray<IrNode>.Empty;
}

/// <summary>
/// A lowered list/set/dict comprehension: the imperative loop that builds a collection. Carries the
/// emission <b>decisions</b> the emitter used to compute inline (E2 #1056, migrates the
/// comprehension transform out of <c>RoslynEmitter.Expressions.Comprehensions.cs</c>):
/// <list type="bullet">
/// <item>the <see cref="CollectionKind"/> (<c>list</c>/<c>set</c>/<c>dict</c>),</item>
/// <item>the result collection type on <see cref="IrNode.Type"/> (its type arguments give the
/// element/key/value types),</item>
/// <item>the <see cref="ElementIsSpread"/> flag (spread <c>Extend</c>/<c>UnionWith</c> vs.
/// <c>Add</c>),</item>
/// <item>the <see cref="SoleForClause"/> (the single <c>for</c> clause, or <c>null</c>), and</item>
/// <item>the D4 capacity-preallocation decision as <see cref="Capacity"/>: non-<c>null</c> ⇒
/// presize the result from a sized single source, whose already-inferred element type is the
/// capacity expression's <see cref="IrNode.Type"/>.</item>
/// </list>
/// The <b>stateful</b> C# emission (temp naming, hoisting via <c>_hoistedStatements</c>,
/// loop-variable scope versioning, sub-expression <c>GenerateExpression</c>) stays in the emitter
/// during E2 — that is what keeps output byte-identical — so the AST <see cref="Clauses"/> ride along
/// for the emitter's stateful walk. The lowered clause sub-expressions live in
/// <see cref="IrNode.Children"/> for totality/determinism and for E3's comprehension-fusion pass.
/// </summary>
/// <param name="CollectionKind">The result collection kind: <c>BuiltinNames.List</c>/<c>Set</c>/<c>Dict</c>.</param>
/// <param name="Clauses">The AST comprehension clauses (ordered), consumed by the emitter's stateful
/// loop construction.</param>
/// <param name="SoleForClause">The single <c>for</c> clause when there is exactly one, else
/// <c>null</c> (capacity preallocation applies only to single-<c>for</c> comprehensions).</param>
/// <param name="Capacity">The lowered sized single source when D4 preallocation applies, else
/// <c>null</c>. Its <see cref="IrNode.Type"/> is the source's semantic type (used to choose the
/// hoisted-source declaration type). The emitter re-generates the source statefully; this field
/// records the decision and, for E3, the source to size from.</param>
/// <param name="ElementIsSpread">True when the element is a spread (<c>*it</c>), so the emitter uses
/// the collection's spread method instead of add.</param>
/// <param name="Type">The comprehension's own result type (<c>list[T]</c>/<c>set[T]</c>/<c>dict[K,V]</c>).</param>
/// <param name="Span">The originating source span.</param>
/// <param name="Children">The lowered child IR nodes (element/key/value and clause sub-expressions).</param>
internal sealed record IrLoweredLoop(
    string CollectionKind,
    ImmutableArray<ComprehensionClause> Clauses,
    ForClause? SoleForClause,
    IrExpression? Capacity,
    bool ElementIsSpread,
    SemanticType? Type,
    TextSpan Span,
    ImmutableArray<IrNode> Children) : IrExpression(Type, Span)
{
    /// <inheritdoc/>
    public override ImmutableArray<IrNode> Children { get; } = Children;
}

/// <summary>
/// A not-yet-migrated expression: a totality wrapper (Design Decision 1b) that carries the original
/// <see cref="Ast"/> node plus its lowered <see cref="IrNode.Children"/>, so the IR tree is
/// structurally complete even before a construct has its own typed node. Migration replaces these
/// with typed nodes; counting the remaining opaque constructors measures "what's left".
/// </summary>
/// <param name="Ast">The original AST expression this wraps.</param>
/// <param name="Type">The expression's resolved type, when the type checker recorded one.</param>
/// <param name="Span">The originating source span.</param>
/// <param name="Children">The lowered child IR nodes.</param>
internal sealed record IrOpaqueExpression(
    Expression Ast,
    SemanticType? Type,
    TextSpan Span,
    ImmutableArray<IrNode> Children) : IrExpression(Type, Span)
{
    /// <inheritdoc/>
    public override ImmutableArray<IrNode> Children { get; } = Children;
}
