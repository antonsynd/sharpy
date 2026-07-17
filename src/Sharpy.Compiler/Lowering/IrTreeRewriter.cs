using System.Collections.Immutable;

namespace Sharpy.Compiler.Lowering;

/// <summary>
/// Shared immutable tree-walk for the E3 lowering passes (#1104): the module-body entry walk, the
/// lazily-allocating children walk, and the opaque-node rebuild plus typed-node fallthrough that every
/// pass duplicated. Concrete passes derive from this and override <see cref="RewriteNode"/> to inject
/// their node-level logic only, calling <c>base.RewriteNode</c>/<see cref="RewriteChildren"/> for the
/// shared cases. Change is tracked by <see cref="object.ReferenceEquals(object?, object?)"/>: a subtree
/// is rebuilt only when a descendant actually changed, and an unchanged tree flows back by reference so
/// the manager detects a no-op cheaply.
/// </summary>
/// <remarks>
/// This is not itself an <see cref="IIrPass"/> — the passes keep declaring <see cref="IIrPass"/> and
/// their <see cref="IIrPass.FlagName"/>; the inherited public <see cref="Rewrite"/> satisfies the
/// interface method. The <see cref="IrRewriteContext"/> is threaded as a parameter through the walk
/// rather than stashed on the instance, so the pass carries <b>no</b> mutable state and the singleton
/// instances held by <see cref="IrPassManager"/> stay safe to reuse across concurrent compilations.
/// </remarks>
internal abstract class IrTreeRewriter
{
    /// <summary>
    /// Rewrites <paramref name="module"/> by walking each top-level statement through
    /// <see cref="RewriteNode"/>, returning the same instance when nothing changed.
    /// </summary>
    public IrModule Rewrite(IrModule module, IrRewriteContext context)
    {
        var body = ImmutableArray.CreateBuilder<IrStatement>(module.Body.Length);
        var changed = false;
        foreach (var statement in module.Body)
        {
            var rewritten = (IrStatement)RewriteNode(statement, context);
            body.Add(rewritten);
            changed |= !ReferenceEquals(rewritten, statement);
        }

        return changed ? module with { Body = body.ToImmutable() } : module;
    }

    /// <summary>
    /// Rewrites a single node. The base handles the shared cases: an <see cref="IrOpaqueExpression"/> or
    /// <see cref="IrOpaqueStatement"/> recurses into its children (rebuilding only on change), and every
    /// typed node is returned as-is (a pass that needs to descend into one overrides this method). The
    /// <paramref name="context"/> is threaded through so an override can consult project-level semantic
    /// facts without any instance state.
    /// </summary>
    protected virtual IrNode RewriteNode(IrNode node, IrRewriteContext context)
    {
        switch (node)
        {
            case IrOpaqueExpression opaque:
                {
                    var children = RewriteChildren(opaque.Children, context, out var childrenChanged);
                    return childrenChanged
                        ? new IrOpaqueExpression(opaque.Ast, opaque.Type, opaque.Span, children)
                        : opaque;
                }

            case IrOpaqueStatement opaqueStatement:
                {
                    var children = RewriteChildren(opaqueStatement.Children, context, out var childrenChanged);
                    return childrenChanged
                        ? new IrOpaqueStatement(opaqueStatement.Ast, opaqueStatement.Span, children)
                        : opaqueStatement;
                }

            default:
                // Typed nodes (equality, index, member, lowered loop, scope guard, with-item, constant,
                // stack array): the shared walk does not descend into them — conservative and rare, and a
                // pass opts in per node type by overriding this method.
                return node;
        }
    }

    /// <summary>
    /// Rewrites <paramref name="children"/> through <see cref="RewriteNode"/>, allocating a builder only
    /// when a child actually changes (so an unchanged array flows back by reference).
    /// </summary>
    protected ImmutableArray<IrNode> RewriteChildren(ImmutableArray<IrNode> children, IrRewriteContext context, out bool changed)
    {
        ImmutableArray<IrNode>.Builder? builder = null;
        for (int i = 0; i < children.Length; i++)
        {
            var rewritten = RewriteNode(children[i], context);
            if (!ReferenceEquals(rewritten, children[i]))
                (builder ??= children.ToBuilder())[i] = rewritten;
        }

        changed = builder is not null;
        return builder is null ? children : builder.ToImmutable();
    }
}
