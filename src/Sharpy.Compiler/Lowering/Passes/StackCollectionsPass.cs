using System.Collections.Immutable;
using System.Linq;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Lowering.Passes;

/// <summary>
/// Stack-collections IR pass (E3, #1057) behind <c>opt_stack_collections</c>. A list literal whose
/// only use is as the direct iterator of a <c>for</c> statement provably never escapes, so it is
/// lowered to a raw array (<c>new T[] { ... }</c>) rather than the three-object <c>Sharpy.List</c>
/// construction (the wrapper, its backing <c>List&lt;T&gt;</c>, and that list's array). Iteration order
/// and per-element evaluation are identical, so the substitution is observably transparent — the pass
/// only replaces the container, never the elements.
/// </summary>
/// <remarks>
/// <para>
/// The v1 escape rule is <b>syntactic and conservative</b>: eligibility is decided purely by the
/// literal's position in the AST (it is exactly a <see cref="ForStatement.Iterator"/>), never by data
/// flow. Anything else — a literal that is assigned, passed to a call, returned, indexed, or reached by
/// <c>len()</c> — is left as a <c>Sharpy.List</c>. A spread literal (<c>[*xs]</c>) is excluded because
/// it has no raw-array initializer form. When in doubt, the pass does not lower.
/// </para>
/// <para>
/// Two further escape-free positions the plan named for v1 are <b>deliberately deferred</b> to
/// <see href="https://github.com/antonsynd/sharpy/issues/1103">#1103</see>: a <c>len([..])</c> receiver
/// (folding to the element count needs a materialized "this call is the builtin <c>len</c>" fact so the
/// pass can identify it purely — the builtin/shadowing resolution lives in the emitter's
/// <c>CodeGenContext</c>, and re-deriving it in a pass would break emitter purity — plus a
/// side-effect-free-elements analysis), and a constant-index receiver (<c>[..][k]</c>, whose raw-array
/// element access diverges from <c>Sharpy.List</c> indexing on negative and out-of-bounds indices, and
/// whose result type must be preserved on the hot, snapshot-tested index path). Both exceed a pure
/// syntactic escape rule, so v1 ships only the for-iterator case.
/// </para>
/// </remarks>
internal sealed class StackCollectionsPass : IrTreeRewriter, IIrPass
{
    public string FlagName => "opt_stack_collections";

    /// <summary>
    /// Converts a <c>for</c>-statement's direct list-literal iterator to a stack array — the one child
    /// that provably never escapes. The remaining children (target, body, else body) and every other
    /// node fall through to the shared base walk, so a nested <c>for</c> still gets its own chance.
    /// </summary>
    protected override IrNode RewriteNode(IrNode node, IrRewriteContext context)
    {
        switch (node)
        {
            case IrOpaqueStatement { Ast: ForStatement forStatement } forStmt:
                {
                    var children = RewriteForChildren(forStmt.Children, forStatement, context, out var childrenChanged);
                    return childrenChanged
                        ? new IrOpaqueStatement(forStmt.Ast, forStmt.Span, children)
                        : forStmt;
                }

            default:
                return base.RewriteNode(node, context);
        }
    }

    private ImmutableArray<IrNode> RewriteForChildren(ImmutableArray<IrNode> children, ForStatement forStatement, IrRewriteContext context, out bool changed)
    {
        ImmutableArray<IrNode>.Builder? builder = null;
        for (int i = 0; i < children.Length; i++)
        {
            IrNode rewritten;
            if (children[i] is IrOpaqueExpression { Ast: ListLiteral literal } iterator
                && ReferenceEquals(literal, forStatement.Iterator)
                && IsStackEligibleIterator(literal))
            {
                rewritten = new IrStackArray(literal, iterator.Type, iterator.Span, iterator.Children);
            }
            else
            {
                rewritten = RewriteNode(children[i], context);
            }

            if (!ReferenceEquals(rewritten, children[i]))
                (builder ??= children.ToBuilder())[i] = rewritten;
        }

        changed = builder is not null;
        return builder is null ? children : builder.ToImmutable();
    }

    /// <summary>
    /// A list literal is stack-eligible as a for-iterator when it has no spread element: a spread
    /// (<c>[*xs]</c>) has no raw-array initializer form, and its wrapper spread-builder path must stay.
    /// Element evaluation and iteration order are identical between the array and the wrapper, so no
    /// side-effect-free condition is needed for iteration (unlike the deferred len/index elisions).
    /// </summary>
    private static bool IsStackEligibleIterator(ListLiteral literal) =>
        !literal.Elements.Any(element => element is SpreadElement);
}
