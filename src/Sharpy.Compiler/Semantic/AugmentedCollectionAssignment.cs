using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Classifies an augmented assignment whose target is a mutable collection (#1394, #1428, #1614).
/// </summary>
/// <remarks>
/// <para>
/// CPython's `s |= {3}` calls `__ior__` and mutates in place, so every name bound to that set sees
/// the change. Since <c>inplace_augassign</c> graduated (#1614) Sharpy does the same: an augmented
/// assignment on a list/set/dict lowers to the mutating method this table names
/// (<c>extend</c>, <c>update</c>, <c>difference_update</c>, …; spec:
/// <c>assignment_operators.md</c> "Augmented assignment on collections"). The SPY0478 transition
/// hint that used to warn about the rebind-vs-mutation divergence is retired.
/// </para>
/// <para>
/// This is the ONE-DOOR classifier: the lowering keys on the classification alone — mutation
/// semantics never depend on whether an alias happens to exist — and the mutator name comes from
/// the same table, so <c>s -= t</c> lowers to <c>difference_update</c>, never <c>update</c>.
/// </para>
/// </remarks>
internal static class AugmentedCollectionAssignment
{
    /// <summary>
    /// The mutation method a gated augmented assignment lowers to.
    /// </summary>
    /// <param name="PythonName">The Python spelling (e.g. <c>extend</c>) — used in the SPY0478 hint message.</param>
    /// <param name="ClrName">The CLR method name on the Sharpy.Core collection (e.g. <c>Extend</c>) — used by the emitter.</param>
    internal record AugmentedMutation(string PythonName, string ClrName);

    /// <summary>
    /// Classifies an augmented assignment as a mutation-in-place candidate. Returns the mutation
    /// method names if the operator×type×target combination matches the CPython __iadd__-family
    /// matrix, or <c>null</c> if this is not a mutating shape (plain assign, wrong operator,
    /// non-collection type, or non-Identifier target).
    /// </summary>
    /// <remarks>
    /// Identifier, attribute (<c>self.xs</c>) and index (<c>d[k]</c>) targets are accepted;
    /// the seven operators with a mutating CPython counterpart on list/set/dict.
    /// <c>frozenset</c> is excluded: verified with python3, <c>f |= {3}</c> rebinds there too,
    /// so Sharpy already agrees.
    /// </remarks>
    public static AugmentedMutation? Classify(Assignment node, SemanticType? targetType)
    {
        if (node.Operator == AssignmentOperator.Assign)
            return null;

        if (node.Target is not (Identifier or MemberAccess or IndexAccess))
            return null;

        return (node.Operator, targetType) switch
        {
            (AssignmentOperator.PlusAssign, GenericType { Name: "list" })
                => new AugmentedMutation("extend", "Extend"),
            (AssignmentOperator.StarAssign, GenericType { Name: "list" })
                => new AugmentedMutation("in_place_repeat", "InPlaceRepeat"),
            (AssignmentOperator.OrAssign, GenericType { Name: "set" })
                => new AugmentedMutation("update", "Update"),
            (AssignmentOperator.AndAssign, GenericType { Name: "set" })
                => new AugmentedMutation("intersection_update", "IntersectionUpdate"),
            (AssignmentOperator.MinusAssign, GenericType { Name: "set" })
                => new AugmentedMutation("difference_update", "DifferenceUpdate"),
            (AssignmentOperator.XorAssign, GenericType { Name: "set" })
                => new AugmentedMutation("symmetric_difference_update", "SymmetricDifferenceUpdate"),
            (AssignmentOperator.OrAssign, GenericType { Name: "dict" })
                => new AugmentedMutation("update", "Update"),
            _ => null,
        };
    }

}
