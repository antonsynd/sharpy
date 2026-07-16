using System.Collections.Immutable;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Lowering.Passes;

/// <summary>
/// Comprehension optimization pass (E3, #1057) behind <c>opt_comprehension_fusion</c>. v1 generalizes
/// the D4 single-source capacity preallocation to <b>multi-<c>for</c></b> comprehensions: when every
/// source is a sized collection AND every source is provably effect-free (a variable or attribute) AND
/// there is <b>no</b> <c>if</c> clause, the result collection is presized to the product of the
/// sources' counts. It sets <see cref="IrLoweredLoop.ProductPreallocation"/>; the emitter reads that
/// flag and builds the product capacity by re-evaluating each (effect-free) source's <c>Count</c>.
/// </summary>
/// <remarks>
/// <para>
/// The two safety conditions come from the plan and the reviewer's riders: <b>no filters</b> because a
/// filtered cross-product would grossly over-reserve capacity (a `10^4 × 10^4` cross-product with a
/// filter yielding 10 elements would reserve ~10^8); <b>effect-free sources only</b> because the
/// capacity re-evaluates each source, so a source with side effects (any call) must not be re-run.
/// A single-<c>for</c> sized comprehension is already presized by the initial lowering, so this pass
/// only marks multi-<c>for</c> ones.
/// </para>
/// <para>
/// v1 does <b>not</b> implement the plan's comprehension/aggregation <em>fusion</em> (folding a
/// comprehension whose sole consumer is another loop or an aggregation like <c>sum([..])</c> into one,
/// eliminating the intermediate collection) — that requires a fundamentally different emitter shape
/// and is tracked as a scoped follow-up in
/// <see href="https://github.com/antonsynd/sharpy/issues/1102">#1102</see>.
/// </para>
/// </remarks>
internal sealed class ComprehensionFusionPass : IIrPass
{
    public string FlagName => "opt_comprehension_fusion";

    public IrModule Rewrite(IrModule module, IrRewriteContext context)
    {
        var body = ImmutableArray.CreateBuilder<IrStatement>(module.Body.Length);
        var changed = false;
        foreach (var statement in module.Body)
        {
            var optimized = (IrStatement)Optimize(statement, context);
            body.Add(optimized);
            changed |= !ReferenceEquals(optimized, statement);
        }

        return changed ? module with { Body = body.ToImmutable() } : module;
    }

    private IrNode Optimize(IrNode node, IrRewriteContext context)
    {
        switch (node)
        {
            case IrLoweredLoop loop:
                {
                    var children = OptimizeChildren(loop.Children, context, out var childrenChanged);
                    var rebuilt = childrenChanged
                        ? new IrLoweredLoop(loop.CollectionKind, loop.Clauses, loop.SoleForClause, loop.Capacity,
                            loop.ElementIsSpread, loop.Comprehension, loop.Type, loop.Span, children)
                        : loop;
                    return QualifiesForProductPreallocation(loop, context)
                        ? rebuilt with { ProductPreallocation = true }
                        : rebuilt;
                }

            case IrOpaqueExpression opaque:
                {
                    var children = OptimizeChildren(opaque.Children, context, out var childrenChanged);
                    return childrenChanged
                        ? new IrOpaqueExpression(opaque.Ast, opaque.Type, opaque.Span, children)
                        : opaque;
                }

            case IrOpaqueStatement opaqueStatement:
                {
                    var children = OptimizeChildren(opaqueStatement.Children, context, out var childrenChanged);
                    return childrenChanged
                        ? new IrOpaqueStatement(opaqueStatement.Ast, opaqueStatement.Span, children)
                        : opaqueStatement;
                }

            default:
                // Typed nodes (equality, index, member, scope guard, with-item): v1 does not descend
                // into them looking for nested comprehensions — conservative and rare.
                return node;
        }
    }

    private ImmutableArray<IrNode> OptimizeChildren(ImmutableArray<IrNode> children, IrRewriteContext context, out bool changed)
    {
        ImmutableArray<IrNode>.Builder? builder = null;
        for (int i = 0; i < children.Length; i++)
        {
            var optimized = Optimize(children[i], context);
            if (!ReferenceEquals(optimized, children[i]))
                (builder ??= children.ToBuilder())[i] = optimized;
        }

        changed = builder is not null;
        return builder is null ? children : builder.ToImmutable();
    }

    /// <summary>
    /// True when <paramref name="loop"/> is a multi-<c>for</c> comprehension with no <c>if</c> clause
    /// whose every <c>for</c>-source is a sized collection referenced through an effect-free expression
    /// (a plain variable or attribute). Single-<c>for</c> comprehensions are already presized by the
    /// initial lowering, so they never qualify here.
    /// </summary>
    private static bool QualifiesForProductPreallocation(IrLoweredLoop loop, IrRewriteContext context)
    {
        var forClauses = 0;
        foreach (var clause in loop.Clauses)
        {
            switch (clause)
            {
                case IfClause:
                    return false; // filters would make the product a gross over-reservation.
                case ForClause forClause:
                    forClauses++;
                    if (!IsEffectFreeSource(forClause.Iterator))
                        return false; // the capacity re-evaluates the source, so it must be side-effect-free.
                    if (!LoweringPass.IsSizedComprehensionSource(context.SemanticInfo.GetExpressionType(forClause.Iterator)))
                        return false;
                    break;
            }
        }

        return forClauses >= 2;
    }

    /// <summary>
    /// A source safe to re-evaluate for its <c>Count</c>: a plain variable or attribute access. Calls
    /// (including <c>range(...)</c>) are excluded in v1 — conservative, per the "literal/variable/
    /// attribute only" scope.
    /// </summary>
    private static bool IsEffectFreeSource(Expression source) => source is Identifier or MemberAccess;
}
