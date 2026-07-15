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
