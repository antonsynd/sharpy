using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Classifies an augmented assignment whose target is a mutable collection that a second binding
/// can observe (#1394).
/// </summary>
/// <remarks>
/// <para>
/// CPython's `s |= {3}` calls `__ior__` and mutates in place, so every name bound to that set sees
/// the change. Sharpy compiles augmented assignment on a collection to a REBINDING of the target
/// (spec: <c>assignment_operators.md</c>), so a second binding keeps the old value. The divergence
/// is only observable when a second binding exists, which is why this is a query rather than a
/// blanket rule — "a hint that fires constantly trains users to ignore the band".
/// </para>
/// <para>
/// Shared rather than validator-private on purpose: a #1428 lowering of augmented assignment to C#
/// 14's user-defined compound assignment would key on this same classification, and one door means
/// the hint and the lowering cannot drift apart about what "aliased" means. If that lowering ever
/// materializes the answer node-keyed into <see cref="SemanticInfo"/>, the new dictionary must join
/// <c>SemanticInfo.MergeFrom</c> or its entries vanish in the per-file→project merge.
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
    /// Whether <paramref name="node"/> is an augmented assignment on a mutable collection with a
    /// second binding to the target visible in <paramref name="enclosingBody"/>.
    /// </summary>
    /// <param name="node">The candidate assignment.</param>
    /// <param name="targetType">The target's semantic type, as the caller resolved it.</param>
    /// <param name="enclosingBody">The enclosing function body, or null at module level.</param>
    public static bool IsAliasObservable(
        Assignment node,
        SemanticType? targetType,
        IReadOnlyList<Statement>? enclosingBody)
    {
        if (node.Operator == AssignmentOperator.Assign)
            return false;

        // The operators that have a mutating CPython counterpart on these types. `frozenset` is
        // excluded by MutatesInPlaceInPython: verified with python3, `f |= {3}` rebinds there too,
        // so Sharpy already agrees and a hint would be a false positive.
        if (node.Operator is not (AssignmentOperator.PlusAssign or AssignmentOperator.OrAssign
            or AssignmentOperator.AndAssign or AssignmentOperator.MinusAssign
            or AssignmentOperator.XorAssign))
        {
            return false;
        }

        if (node.Target is not Identifier target || !MutatesInPlaceInPython(targetType))
            return false;

        return enclosingBody != null && HasSecondBinding(enclosingBody, target.Name);
    }

    /// <summary>
    /// The collection types whose augmented-assignment operators mutate in place in CPython.
    /// Verified with python3: `set |=`, `list +=` and `dict |=` are all visible through an alias;
    /// `frozenset |=` is not, because it rebinds there too.
    /// </summary>
    private static bool MutatesInPlaceInPython(SemanticType? type)
        => type is GenericType { Name: "list" or "set" or "dict" };

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
