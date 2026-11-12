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
/// <param name="ExitShape">Whether the __exit__ can suppress exceptions.</param>
/// <param name="ExitMethod">The resolved __exit__/__aexit__ method, or null for IDisposable.</param>
/// <param name="Span">The originating source span.</param>
internal sealed record IrWithItem(
    IrExpression ContextExpr,
    ContextManagerKind Kind,
    ContextManagerExitShape ExitShape,
    FunctionSymbol? ExitMethod,
    VariableSymbol? AsVar,
    TextSpan Span) : IrNode(null, Span)
{
    /// <inheritdoc/>
    public override ImmutableArray<IrNode> Children => ImmutableArray.Create<IrNode>(ContextExpr);
}

/// <summary>
/// A lowered augmented assignment on a mutable collection (<c>xs += ys</c>, <c>s |= t</c>,
/// <c>d |= e</c>, …) that mutates in place rather than rebinding (#1428, #1614; P82 #2443 migrates
/// <c>SemanticInfo._augmentedAssignMutations</c>). The pass builds it exactly when
/// <c>AugmentedCollectionAssignment.Classify</c> names a mutator for the assignment and the target's
/// recorded type; any other assignment stays an <see cref="IrOpaqueStatement"/>. The emitter reads
/// <see cref="ClrMethodName"/> and emits <c>receiver.ClrMethodName(value)</c>.
/// </summary>
/// <param name="ClrMethodName">The CLR method name on the Sharpy.Core collection (e.g. <c>Extend</c>).
/// A <c>string</c>, not the classifier's record, because code generation may not see Semantic types
/// (<c>EmitterCarrierOnlyConformanceTests</c>).</param>
/// <param name="Span">The originating source span.</param>
/// <param name="Children">The lowered target and value, in <c>GetChildNodes</c> order (totality).</param>
internal sealed record IrCollectionMutation(
    string ClrMethodName,
    TextSpan Span,
    ImmutableArray<IrNode> Children) : IrStatement(Span)
{
    /// <inheritdoc/>
    public override ImmutableArray<IrNode> Children { get; } = Children;
}

/// <summary>
/// A lowered store of a payload value into a narrowed <c>T?</c> (Optional) slot that re-wraps the value
/// as <c>Optional&lt;T&gt;.Some(value)</c> (R-T payload rule, #1755 B; P82 #2443 migrates
/// <c>SemanticInfo._optionalStoreWraps</c>). The pass builds it for an <see cref="Assignment"/> to an
/// identifier whose declared slot is an <see cref="OptionalType"/> when the store is a plain <c>=</c>
/// recorded at the payload type, an augmented operator (the result re-wraps), or a <c>??=</c> whose
/// value is not itself Optional; any other assignment stays an <see cref="IrOpaqueStatement"/> (an
/// in-place collection mutation, <see cref="IrCollectionMutation"/>, is decided first).
/// </summary>
/// <param name="WrapAs">The slot's Optional type: the emitter prints <c>Optional&lt;T&gt;.Some(value)</c>
/// for it.</param>
/// <param name="Operator">The operator lowering the checker recorded on the assignment (an augmented
/// store into a narrowed Optional slot, <c>x //= 2</c>: P82 tranche 3, #2443, plan-92249e D1);
/// <see cref="OperatorLoweringKind.Native"/> when nothing was recorded (a plain <c>=</c>).</param>
/// <param name="NarrowTo">The type the augmented result narrows to, or <c>null</c>.</param>
/// <param name="Span">The originating source span.</param>
/// <param name="Children">The lowered target and value, in <c>GetChildNodes</c> order (totality).</param>
internal sealed record IrOptionalStoreWrap(
    OptionalType WrapAs,
    OperatorLoweringKind Operator,
    SemanticType? NarrowTo,
    TextSpan Span,
    ImmutableArray<IrNode> Children) : IrStatement(Span), IIrOperatorSite
{
    /// <inheritdoc/>
    public override ImmutableArray<IrNode> Children { get; } = Children;
}

/// <summary>
/// An <see cref="Assignment"/> whose key carries a recorded operator lowering
/// (<c>SemanticInfo._operatorLowerings</c>; P82 tranche 3, #2443, plan-92249e D1) and that is neither
/// an in-place collection mutation (<see cref="IrCollectionMutation"/>) nor a store into a narrowed
/// Optional slot (<see cref="IrOptionalStoreWrap"/>, which carries its own operator): an augmented
/// operator (<c>n //= 2</c>, <c>x **= 2</c>) or a <c>??=</c> on an Optional target. Precedence on an
/// assignment is mutation, wrap, operator, opaque; an assignment with no entry stays an
/// <see cref="IrOpaqueStatement"/>.
/// </summary>
/// <param name="Operator">How code generation must emit the operator.</param>
/// <param name="NarrowTo">The type the augmented result narrows to, or <c>null</c>.</param>
/// <param name="Span">The originating source span.</param>
/// <param name="Children">The lowered target and value, in <c>GetChildNodes</c> order (totality).</param>
internal sealed record IrOperatorAssignment(
    OperatorLoweringKind Operator,
    SemanticType? NarrowTo,
    TextSpan Span,
    ImmutableArray<IrNode> Children) : IrStatement(Span), IIrOperatorSite
{
    /// <inheritdoc/>
    public override ImmutableArray<IrNode> Children { get; } = Children;
}

/// <summary>
/// A lowered <c>try</c> some of whose <c>except</c> handlers, or some of whose tuple-handler types, an
/// earlier handler already catches (#2363). Python tries handlers in order and never runs the covered
/// part; C# refuses a catch for a type after a catch for its base (CS0160). The pass builds this node
/// from <c>ExceptHandlerReachability</c> (the classifier the SPY0488 warning reads) only when something
/// is covered; any other <c>try</c> stays an <see cref="IrOpaqueStatement"/>. The emitter reads
/// <see cref="Handlers"/> and leaves the covered catches out.
/// </summary>
/// <param name="Handlers">One plan per handler, aligned with <c>TryStatement.Handlers</c>.</param>
/// <param name="Span">The originating source span.</param>
/// <param name="Children">The lowered children, in <c>GetChildNodes</c> order (totality).</param>
internal sealed record IrPrunedTry(
    ImmutableArray<IrHandlerPlan> Handlers,
    TextSpan Span,
    ImmutableArray<IrNode> Children) : IrStatement(Span)
{
    /// <inheritdoc/>
    public override ImmutableArray<IrNode> Children { get; } = Children;
}

/// <summary>
/// What the emitter emits for one <c>except</c> handler of an <see cref="IrPrunedTry"/>.
/// </summary>
/// <param name="Emitted">False when an earlier handler covers every type the handler names.</param>
/// <param name="EmittedTypes">One flag per exception type the handler names, in source order (the
/// elements of a tuple spelling, or its one type); empty for a bare <c>except:</c>.</param>
internal sealed record IrHandlerPlan(bool Emitted, ImmutableArray<bool> EmittedTypes);
