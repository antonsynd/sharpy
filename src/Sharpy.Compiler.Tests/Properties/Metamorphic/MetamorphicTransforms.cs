using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Tests.Properties.Metamorphic.Transforms;

namespace Sharpy.Compiler.Tests.Properties.Metamorphic;

/// <summary>
/// The registry of semantics-preserving source transforms, plus the <b>allowed-delta table</b> that
/// says which diagnostic codes each transform may legitimately introduce.
///
/// <para>"No diagnostic regression" is not naive equality: a transform that appends unreachable code
/// is <em>supposed</em> to raise the unreachable-code warning. The table makes that judgement
/// explicit and reviewable (the same discipline as the front-end parity harness's normalization
/// rules) — a code outside a transform's entry is a violation, no exceptions and no per-fixture
/// fudging.</para>
/// </summary>
internal static class MetamorphicTransforms
{
    public static readonly IReadOnlyList<IAstTransform> All = new IAstTransform[]
    {
        new CommentInsertionTransform(),
        new PassInsertionTransform(),
        new ParensWrapTransform(),
        new ParensWrapCalleeTransform(),
        new DeadCodeAfterReturnTransform(),
        new IfTrueWrapTransform(),
        new SwapCommutativeTransform(),
        new AlphaRenameTransform(),
        new AddRedundantTypeAnnotationTransform(),
    };

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedNewCodes =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            // Appending a declaration after `return` is unreachable BY CONSTRUCTION, and the
            // declaration it appends is never read. Both warnings are the transform working as
            // designed; anything else it provokes is a real regression.
            [nameof(DeadCodeAfterReturnTransform)] = new HashSet<string>(StringComparer.Ordinal)
            {
                DiagnosticCodes.Validation.UnreachableCodeWarning,
                DiagnosticCodes.Validation.UnusedVariable,
            },

            // Every other transform is diagnostic-neutral: inserting a comment, a `pass`, redundant
            // parentheses, an `if True:` wrapper, swapping integer-literal operands, renaming a local
            // or spelling out an inferred type must not make the compiler say anything new.
            [nameof(CommentInsertionTransform)] = Empty,
            [nameof(PassInsertionTransform)] = Empty,
            [nameof(ParensWrapTransform)] = Empty,
            [nameof(ParensWrapCalleeTransform)] = Empty,
            [nameof(IfTrueWrapTransform)] = Empty,
            [nameof(SwapCommutativeTransform)] = Empty,
            [nameof(AlphaRenameTransform)] = Empty,
            [nameof(AddRedundantTypeAnnotationTransform)] = Empty,
        };

    private static IReadOnlySet<string> Empty => new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Diagnostic codes <paramref name="transform"/> is permitted to introduce. Throws for an
    /// unregistered transform: a new transform must state its delta contract, never inherit a
    /// permissive default.
    /// </summary>
    public static IReadOnlySet<string> AllowedNewDiagnostics(IAstTransform transform)
    {
        var key = transform.GetType().Name;
        return AllowedNewCodes.TryGetValue(key, out var codes)
            ? codes
            : throw new InvalidOperationException(
                $"Transform '{key}' has no allowed-delta entry in {nameof(MetamorphicTransforms)}. " +
                "Add one (usually empty) so its diagnostic contract is explicit.");
    }
}
