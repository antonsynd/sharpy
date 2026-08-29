using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Classifies an augmented assignment whose target is a mutable collection (#1394, #1428).
/// </summary>
/// <remarks>
/// <para>
/// CPython's `s |= {3}` calls `__ior__` and mutates in place, so every name bound to that set sees
/// the change. Sharpy compiles augmented assignment on a collection to a REBINDING of the target
/// (spec: <c>assignment_operators.md</c>), so a second binding keeps the old value.
/// </para>
/// <para>
/// This is the ONE-DOOR classifier shared by both the SPY0478 transition hint (#1394) and the
/// <c>inplace_augassign</c> experimental lowering (#1428). The hint asks "classification matches
/// AND a second binding exists"; the lowering keys on the classification alone — mutation-vs-rebind
/// semantics must not depend on whether an alias happens to exist. The hint's suggested mutator
/// comes from the same table, which fixes the prior bug where <c>s -= t</c> was steered to
/// <c>update</c> instead of <c>difference_update</c>.
/// </para>
/// <para>
/// <b>Precision is deliberately relaxed.</b> The precise question is whether a second binding is
/// LIVE at the assignment, which is a backward dataflow analysis; no liveness or def-use analysis
/// exists in this codebase (<c>Analysis/ControlFlow/</c> has only forward analyses). So this asks
/// the cheap structural question — does a second binding to this variable exist anywhere in the
/// enclosing function — which over-approximates: a hint can fire where the alias is already dead.
/// For a hint that is the right direction to be wrong in, and true liveness is the follow-up.
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

    /// <summary>
    /// Whether <paramref name="node"/> is an augmented assignment on a mutable collection with a
    /// second binding to the target visible in <paramref name="enclosingBody"/>.
    /// </summary>
    public static bool IsAliasObservable(
        Assignment node,
        SemanticType? targetType,
        IReadOnlyList<Statement>? enclosingBody)
    {
        if (Classify(node, targetType) is null)
            return false;

        return node.Target is Identifier target
            && enclosingBody != null
            && HasSecondBinding(enclosingBody, target.Name);
    }

    /// <summary>
    /// Whether some binding in the body puts this variable and a DIFFERENT name on one object —
    /// <c>t = s</c> or <c>s = t</c>, in either the annotated-declaration or the bare-assignment
    /// spelling. BOTH spellings matter: <c>t: set[int] = s</c> parses as a
    /// <see cref="VariableDeclaration"/>, not an <see cref="Assignment"/>, and it is the spelling
    /// the issue's own repro uses — searching only assignments made the query answer "no alias"
    /// for the exact program the hint exists for.
    /// </summary>
    private static bool HasSecondBinding(IReadOnlyList<Statement> body, string name)
    {
        foreach (var statement in Walk(body))
        {
            var (boundName, sourceExpr) = statement switch
            {
                Assignment { Operator: AssignmentOperator.Assign, Target: Identifier t } a
                    => (t.Name, a.Value),
                VariableDeclaration v => (v.Name, v.InitialValue),
                _ => (null, null),
            };

            if (boundName == null || sourceExpr is not Identifier source)
                continue;

            // `t = s` — another name takes this object; or `s = t` — this name takes another's.
            var bindsFromUs = string.Equals(source.Name, name, StringComparison.Ordinal)
                && !string.Equals(boundName, name, StringComparison.Ordinal);
            var bindsUsFrom = string.Equals(boundName, name, StringComparison.Ordinal)
                && !string.Equals(source.Name, name, StringComparison.Ordinal);

            if (bindsFromUs || bindsUsFrom)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Yields every statement in the body, descending into the block-bearing statements an
    /// aliasing assignment can hide in. Nested function bodies are excluded: they are their own
    /// scope, as they are for the emitter's slot table.
    /// </summary>
    private static IEnumerable<Statement> Walk(IReadOnlyList<Statement> body)
    {
        foreach (var statement in body)
        {
            yield return statement;

            foreach (var nested in statement switch
            {
                IfStatement s => s.ThenBody.Concat(s.ElseBody)
                    .Concat(s.ElifClauses.SelectMany(e => (IEnumerable<Statement>)e.Body)),
                WhileStatement s => s.Body.Concat(s.ElseBody),
                ForStatement s => s.Body.Concat(s.ElseBody),
                WithStatement s => (IEnumerable<Statement>)s.Body,
                TryStatement s => s.Body.Concat(s.Handlers.SelectMany(h => (IEnumerable<Statement>)h.Body))
                    .Concat(s.ElseBody).Concat(s.FinallyBody),
                MatchStatement s => s.Cases.SelectMany(c => (IEnumerable<Statement>)c.Body),
                _ => Enumerable.Empty<Statement>(),
            })
            {
                foreach (var inner in Walk(new[] { nested }))
                    yield return inner;
            }
        }
    }
}
