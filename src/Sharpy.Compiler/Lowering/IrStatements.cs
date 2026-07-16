using System.Collections.Immutable;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Text;

namespace Sharpy.Compiler.Lowering;

/// <summary>
/// Base type for IR nodes that denote an action rather than a value. Statements carry no
/// <see cref="IrNode.Type"/> (it is always <c>null</c>).
/// </summary>
internal abstract record IrStatement(TextSpan Span) : IrNode(null, Span);

/// <summary>
/// A not-yet-migrated statement: a totality wrapper (Design Decision 1b) that carries the original
/// <see cref="Ast"/> node plus its lowered <see cref="IrNode.Children"/>, so the IR tree is
/// structurally complete even before a construct has its own typed node. Migration replaces these
/// with typed nodes; counting the remaining opaque constructors measures "what's left".
/// </summary>
/// <param name="Ast">The original AST statement this wraps.</param>
/// <param name="Span">The originating source span.</param>
/// <param name="Children">The lowered child IR nodes.</param>
internal sealed record IrOpaqueStatement(
    Statement Ast,
    TextSpan Span,
    ImmutableArray<IrNode> Children) : IrStatement(Span)
{
    /// <inheritdoc/>
    public override ImmutableArray<IrNode> Children { get; } = Children;
}

/// <summary>
/// A lowered <c>defer</c>: the scope-exit guard whose <see cref="DeferredBody"/> runs on every exit
/// path of the enclosing suite (E2 #1056, migrates the <c>defer</c> transform out of
/// <c>RoslynEmitter.Statements.cs</c>). The emitter turns a guard into a
/// <c>try { remainder } finally { DeferredBody }</c> envelope, and the recursive split of the
/// enclosing suite (later defers nesting inside earlier ones, so their finally blocks run in reverse
/// declaration order — LIFO) is what threads the guards together.
/// <para>
/// In E2 the <em>guarded</em> remainder is not folded onto this node — it is the following siblings
/// of the <c>defer</c> in the enclosing suite, which the emitter has in hand and generates statefully
/// (hoisting, line directives). Folding the guarded body onto the node requires suite-level IR
/// lowering, a deliberate post-E2 step. This node makes the scope-exit lowering explicit in the IR
/// (the <c>DeferStatement</c> no longer lowers to an opaque wrapper) and carries the deferred body so
/// the tree stays total.
/// </para>
/// </summary>
/// <param name="DeferredBody">The lowered statements that run on scope exit (the <c>finally</c> body).</param>
/// <param name="Span">The originating source span.</param>
internal sealed record IrScopeGuard(
    ImmutableArray<IrStatement> DeferredBody,
    TextSpan Span) : IrStatement(Span)
{
    /// <inheritdoc/>
    public override ImmutableArray<IrNode> Children { get; } = DeferredBody.CastArray<IrNode>();
}

/// <summary>
/// A lowered <c>with</c>-item. Carries the context-manager <see cref="Kind"/> — the fact that today
/// lives node-keyed in <c>SemanticInfo._contextManagerKinds</c> — and the resolved
/// <see cref="AsVar"/> (<c>_withItemSymbols</c>), which is otherwise unavailable to a backend
/// because the <c>with</c>-scope is exited after type checking. It is neither an expression nor a
/// statement; it is a structural part of a <c>with</c> statement.
/// </summary>
/// <param name="ContextExpr">The lowered context expression (the resource being managed).</param>
/// <param name="Kind">Which context-manager protocol the resource implements, deciding between a
/// C# <c>using</c> statement and explicit enter/exit calls.</param>
/// <param name="AsVar">The variable symbol bound by the <c>as</c> clause, or <c>null</c> when there
/// is no <c>as</c> clause.</param>
/// <param name="Span">The originating source span.</param>
internal sealed record IrWithItem(
    IrExpression ContextExpr,
    ContextManagerKind Kind,
    VariableSymbol? AsVar,
    TextSpan Span) : IrNode(null, Span)
{
    /// <inheritdoc/>
    public override ImmutableArray<IrNode> Children => ImmutableArray.Create<IrNode>(ContextExpr);
}
