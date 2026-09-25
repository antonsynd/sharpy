namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Structural rewrite over a <see cref="SemanticType"/> tree: every node is offered to a rewrite
/// function in pre-order, and a node it declines is rebuilt from its rewritten children. Composite
/// nodes are rebuilt with <c>with</c>, so every member the walk does not visit (a generic's
/// definition and CLR origin, a union's symbol, a function type's optional/variadic shape) is
/// carried over unchanged, and a subtree with nothing to rewrite comes back as the SAME instance.
/// </summary>
/// <remarks>
/// <para>The composite arms are the complete set of <see cref="SemanticType"/> records that hold
/// other types. A new composite record needs an arm here, or every walk silently stops at it —
/// <c>UdtCodecIdentityTotalityTests</c> holds the arm set against the reflection census of
/// concrete subclasses (#2027).</para>
/// <para><see cref="TypeSubstitution.Apply"/> predates this walker and rebuilds with <c>new</c>
/// (dropping unvisited members); it is not rewritten onto the walker here.</para>
/// </remarks>
internal static class SemanticTypeWalker
{
    /// <summary>
    /// Rewrites <paramref name="type"/>. <paramref name="rewrite"/> returns the replacement for a
    /// node, or null to descend into it.
    /// </summary>
    internal static SemanticType Rewrite(SemanticType type, Func<SemanticType, SemanticType?> rewrite)
    {
        if (rewrite(type) is { } replaced)
            return replaced;

        switch (type)
        {
            case GenericType gt:
                return RewriteList(gt.TypeArguments, rewrite, out var gtArgs)
                    ? gt with { TypeArguments = gtArgs }
                    : gt;
            case GenericFunctionType gft:
                return RewriteList(gft.TypeArguments, rewrite, out var gftArgs)
                    ? gft with { TypeArguments = gftArgs }
                    : gft;
            case NullableType nt:
                {
                    var underlying = Rewrite(nt.UnderlyingType, rewrite);
                    return ReferenceEquals(underlying, nt.UnderlyingType) ? nt : nt with { UnderlyingType = underlying };
                }
            case OptionalType ot:
                {
                    var underlying = Rewrite(ot.UnderlyingType, rewrite);
                    return ReferenceEquals(underlying, ot.UnderlyingType) ? ot : ot with { UnderlyingType = underlying };
                }
            case ResultType rt:
                {
                    var ok = Rewrite(rt.OkType, rewrite);
                    var error = Rewrite(rt.ErrorType, rewrite);
                    return ReferenceEquals(ok, rt.OkType) && ReferenceEquals(error, rt.ErrorType)
                        ? rt
                        : rt with { OkType = ok, ErrorType = error };
                }
            case FunctionType ft:
                {
                    var paramsChanged = RewriteList(ft.ParameterTypes, rewrite, out var parameters);
                    var returnType = Rewrite(ft.ReturnType, rewrite);
                    return !paramsChanged && ReferenceEquals(returnType, ft.ReturnType)
                        ? ft
                        : ft with { ParameterTypes = parameters, ReturnType = returnType };
                }
            case TupleType tt:
                return RewriteList(tt.ElementTypes, rewrite, out var elements)
                    ? tt with { ElementTypes = elements }
                    : tt;
            case UnionType ut:
                return RewriteList(ut.CaseTypes, rewrite, out var cases)
                    ? ut with { CaseTypes = cases }
                    : ut;
            case TaskType { ResultType: { } result } task:
                {
                    var rewritten = Rewrite(result, rewrite);
                    return ReferenceEquals(rewritten, result) ? task : task with { ResultType = rewritten };
                }
            default:
                return type;
        }
    }

    /// <summary>
    /// Rewrites each element; answers whether any element changed. <paramref name="result"/> is the
    /// original list when nothing changed, else a fresh list.
    /// </summary>
    private static bool RewriteList(
        List<SemanticType> types, Func<SemanticType, SemanticType?> rewrite, out List<SemanticType> result)
    {
        List<SemanticType>? changed = null;
        for (var i = 0; i < types.Count; i++)
        {
            var rewritten = Rewrite(types[i], rewrite);
            if (changed == null && !ReferenceEquals(rewritten, types[i]))
                changed = types.Take(i).ToList();
            changed?.Add(rewritten);
        }

        result = changed ?? types;
        return changed != null;
    }
}
