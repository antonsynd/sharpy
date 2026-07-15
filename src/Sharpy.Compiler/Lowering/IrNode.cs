using System.Collections.Immutable;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Text;

namespace Sharpy.Compiler.Lowering;

/// <summary>
/// Base type of the Sharpy lowering IR: a bound, typed, immutable, source-spanned tree that
/// sits between semantic analysis and code generation (design: <c>docs/design/lowering-ir.md</c>).
/// </summary>
/// <remarks>
/// <para>
/// The IR is modelled on Roslyn's <c>IOperation</c>. Every value node carries its resolved
/// <see cref="SemanticType"/> and originating <see cref="TextSpan"/>; every reference is to a
/// bound <see cref="Symbol"/>/<see cref="SemanticType"/> rather than a string to be re-resolved.
/// Nodes are immutable records built once by <c>LoweringPass</c> and never mutated in place — a
/// transform produces a new node — which keeps determinism (A3) a construction property and makes
/// E3's optimization passes tractable.
/// </para>
/// <para>
/// During E2 the IR is total from day one: constructs that have not yet been migrated onto a typed
/// node lower to <see cref="IrOpaqueExpression"/>/<see cref="IrOpaqueStatement"/> wrappers over the
/// original AST node (Design Decision 1b). Migration replaces opaque nodes with typed ones. This IR
/// is <b>codegen scaffolding</b>: it is owned by the <c>Lowering</c> component, built once per
/// project after <c>SemanticInfo.MergeFrom</c>, and is never itself merged (Design Decision 1a).
/// </para>
/// <para>
/// The IR carries no behaviour and holds no reference to <c>SyntaxFactory</c> or reflection: it is a
/// pure data vocabulary. The emitter (or a future backend) reads it and performs the final,
/// mechanical syntax mapping.
/// </para>
/// </remarks>
internal abstract record IrNode(SemanticType? Type, TextSpan Span)
{
    /// <summary>
    /// The child IR nodes of this node, in a fixed, source-deterministic order. This is a
    /// <b>structural</b> accessor used by the totality and determinism guards to walk the tree; it
    /// is not a semantic API, and it never varies with the order in which nodes were constructed.
    /// Leaf nodes return <see cref="ImmutableArray{T}.Empty"/>.
    /// </summary>
    public abstract ImmutableArray<IrNode> Children { get; }
}
