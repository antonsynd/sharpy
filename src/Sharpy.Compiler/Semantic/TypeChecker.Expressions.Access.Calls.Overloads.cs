using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// TypeChecker partial class: Overload resolution core algorithm and specificity comparison
/// </summary>
internal partial class TypeChecker
{
    /// <summary>
    /// Captures all inputs for overload resolution, reducing parameter count on <see cref="ResolveOverloadCore"/>.
    /// </summary>
    /// <param name="Candidates">The list of overload candidates.</param>
    /// <param name="TotalArgCount">Total argument count at the call site.</param>
    /// <param name="ArgTypes">Resolved argument types.</param>
    /// <param name="SkipSelfParam">If true, computes per-overload self offset (for instance methods).</param>
    /// <param name="TypeSubstitution">Optional function to substitute type parameters before comparison.</param>
    /// <param name="SkipUnknownTypes">If true, skip type comparison when either side is UnknownType.</param>
    /// <param name="KeywordArgNames">Names of keyword arguments at the call site, used to filter out
    /// overloads that lack matching parameter names (e.g., a params overload with no 'reverse' param).</param>
    internal record OverloadResolutionContext(
        List<FunctionSymbol> Candidates,
        int TotalArgCount,
        List<SemanticType> ArgTypes,
        bool SkipSelfParam = false,
        Func<SemanticType, SemanticType>? TypeSubstitution = null,
        bool SkipUnknownTypes = false,
        IReadOnlyCollection<string>? KeywordArgNames = null,
        FunctionCall? Call = null);

    /// <summary>
    /// Resolves a binary operator-dunder / <c>__getitem__</c> overload (self + a single argument)
    /// through the shared <see cref="ResolveOverloadCore"/>, so Engine B (operator dunders,
    /// <c>__getitem__</c>) uses the same order-independent, specificity-based betterness as call
    /// resolution (#975). Injected into
    /// <see cref="TypeInferenceService.DeterministicBinaryOverloadResolver"/> by the constructor.
    /// The candidates carry <c>self</c> as their first parameter, so
    /// <see cref="OverloadResolutionContext.SkipSelfParam"/> is set and the argument is matched
    /// against the parameter after <c>self</c>. Returns <c>null</c> when nothing matches or the best
    /// is ambiguous (the caller then falls back or reports "unsupported operator").
    /// </summary>
    private FunctionSymbol? ResolveDunderOverload(IReadOnlyList<FunctionSymbol> candidates, SemanticType argType)
    {
        var (match, _, _) = ResolveOverloadCore(new OverloadResolutionContext(
            Candidates: candidates.ToList(),
            TotalArgCount: 1,
            ArgTypes: new List<SemanticType> { argType },
            SkipSelfParam: true));
        return match;
    }

    /// <summary>
    /// Core overload resolution algorithm shared by all overload resolution methods.
    /// Performs two-pass matching: first filters by argument count, then checks type compatibility.
    /// </summary>
    /// <returns>A tuple of (matched overload, arity-filtered candidates, whether resolution was ambiguous).</returns>
    private (FunctionSymbol? Match, List<FunctionSymbol> ArityCandidates, bool IsAmbiguous) ResolveOverloadCore(
        OverloadResolutionContext context)
    {
        int GetSelfOffset(FunctionSymbol o) =>
            context.SkipSelfParam && o.Parameters.Count > 0 && o.Parameters[0].Name == PythonNames.Self ? 1 : 0;

        // First pass: filter by argument count
        var arityCandidates = context.Candidates.Where(o =>
        {
            var selfOffset = GetSelfOffset(o);
            var requiredParams = o.Parameters.Skip(selfOffset).Count(p => !p.HasDefault && !p.IsVariadic);
            var hasVariadic = o.Parameters.Skip(selfOffset).Any(p => p.IsVariadic);
            var totalParams = o.Parameters.Count - selfOffset;
            if (hasVariadic)
                return context.TotalArgCount >= requiredParams;
            return context.TotalArgCount >= requiredParams && context.TotalArgCount <= totalParams;
        }).ToList();

        // Filter by keyword argument names: exclude overloads where
        // (a) any keyword arg name has no matching parameter, or
        // (b) the positional arg count doesn't cover the remaining required params
        //     after removing keyword-satisfied ones.
        // This disambiguates calls like merge(a, b, reverse=True) between a params
        // overload and one with a named 'reverse' parameter.
        if (context.KeywordArgNames is { Count: > 0 })
        {
            var positionalArgCount = context.TotalArgCount - context.KeywordArgNames.Count;
            var kwFiltered = arityCandidates.Where(o =>
            {
                var selfOffset = GetSelfOffset(o);
                var paramsAfterSelf = o.Parameters.Skip(selfOffset).ToList();
                var paramNames = paramsAfterSelf.Select(p => p.Name).ToHashSet();

                // Every keyword arg must have a matching parameter name
                if (!context.KeywordArgNames.All(kw => paramNames.Contains(kw)))
                    return false;

                // For non-variadic overloads, verify that positional args cover
                // exactly the required parameters NOT supplied by keyword args.
                if (!paramsAfterSelf.Any(p => p.IsVariadic))
                {
                    var kwSet = context.KeywordArgNames.ToHashSet();
                    var nonKwRequired = paramsAfterSelf
                        .Where(p => !p.HasDefault && !kwSet.Contains(p.Name))
                        .Count();
                    var nonKwTotal = paramsAfterSelf
                        .Where(p => !kwSet.Contains(p.Name))
                        .Count();
                    if (positionalArgCount < nonKwRequired || positionalArgCount > nonKwTotal)
                        return false;
                }

                return true;
            }).ToList();

            // Only apply the filter if it leaves at least one candidate;
            // otherwise fall through to normal resolution so existing error
            // reporting (unknown keyword argument) kicks in.
            if (kwFiltered.Count > 0)
                arityCandidates = kwFiltered;
        }

        // Second pass: check type compatibility
        var matchingOverloads = new List<FunctionSymbol>();
        foreach (var overload in arityCandidates)
        {
            var selfOffset = GetSelfOffset(overload);
            bool typesMatch = true;
            var variadicParam = overload.Parameters.Skip(selfOffset).FirstOrDefault(p => p.IsVariadic);

            for (int i = 0; i < context.ArgTypes.Count; i++)
            {
                SemanticType expectedType;
                var paramIdx = i + selfOffset;
                if (paramIdx < overload.Parameters.Count && !overload.Parameters[paramIdx].IsVariadic)
                {
                    expectedType = overload.Parameters[paramIdx].Type;
                }
                else if (variadicParam != null)
                {
                    expectedType = variadicParam.Type;
                }
                else
                {
                    typesMatch = false;
                    break;
                }

                if (context.TypeSubstitution != null)
                    expectedType = context.TypeSubstitution(expectedType);

                if (context.SkipUnknownTypes && (expectedType is UnknownType || context.ArgTypes[i] is UnknownType))
                    continue;

                // Type parameters act as wildcards during overload resolution —
                // generic type inference happens later in C# compilation.
                if (expectedType is TypeParameterType)
                    continue;

                if (ContainsTypeParameter(expectedType))
                {
                    // For parameterized generics (e.g., list[T], list[list[T]]), the
                    // argument must structurally match the expected shape (same outer
                    // name/arity, recursively), with bare type parameters acting as
                    // wildcards only at their own position. Without the recursion a flat
                    // list[int] would wildcard-match a nested list[list[T]] (the inner
                    // int absorbed into T), tying two generic overloads (#957); the outer
                    // name check also keeps list[int] from matching array[T] (#954).
                    if (!ArgMatchesGenericShape(context.ArgTypes[i], expectedType))
                    {
                        typesMatch = false;
                        break;
                    }
                    continue;
                }

                if (!IsArgumentAssignable(context.ArgTypes[i], expectedType))
                {
                    if (IsSystemTypeParameter(expectedType)
                        && context.Call != null
                        && i < context.Call.Arguments.Length
                        && _semanticInfo.IsTypeReference(context.Call.Arguments[i]))
                    {
                        continue;
                    }
                    typesMatch = false;
                    break;
                }
            }
            if (typesMatch)
            {
                matchingOverloads.Add(overload);
            }
        }

        // Disambiguate: prefer exact arity match
        if (matchingOverloads.Count > 1)
        {
            var exactArityMatches = matchingOverloads.Where(o =>
                o.Parameters.Count - GetSelfOffset(o) == context.TotalArgCount
            ).ToList();

            if (exactArityMatches.Count == 1)
                return (exactArityMatches[0], arityCandidates, false);

            // When multiple exact-arity overloads remain, prefer the one with fewer
            // type parameters. This breaks ties between e.g. Merge<T>(a, b, reverse)
            // and Merge<T, TKey>(iterables[], key, reverse) by choosing the simpler generic.
            var candidates = exactArityMatches.Count > 1 ? exactArityMatches : matchingOverloads;
            var minTypeParams = candidates.Min(o => o.TypeParameters.Count);
            var fewerTypeParamMatches = candidates.Where(o => o.TypeParameters.Count == minTypeParams).ToList();
            if (fewerTypeParamMatches.Count == 1)
                return (fewerTypeParamMatches[0], arityCandidates, false);

            // Specificity tiebreaker: prefer the overload whose parameter types are
            // strictly more specific (e.g., list[int] beats IEnumerable<int>).
            // Follows C#'s "better function member" rule (§12.6.4.3).
            var specificityWinner = FindMostSpecificOverload(fewerTypeParamMatches, context);
            if (specificityWinner != null)
                return (specificityWinner, arityCandidates, false);

            return (null, arityCandidates, true);
        }

        return (matchingOverloads.Count == 1 ? matchingOverloads[0] : null, arityCandidates, false);
    }

    /// <summary>
    /// Determines whether overload <paramref name="a"/> has strictly more specific parameter
    /// types than overload <paramref name="b"/> for the given call arguments.  A parameter is
    /// "more specific" when its type is assignable to the other's but not vice-versa
    /// (e.g., <c>list[int]</c> is more specific than <c>IEnumerable&lt;int&gt;</c>).
    /// Mirrors C#'s "better function member" rule (§12.6.4.3).
    /// </summary>
    private bool IsMoreSpecificOverload(FunctionSymbol a, FunctionSymbol b, OverloadResolutionContext context)
    {
        int SelfOffset(FunctionSymbol o) =>
            context.SkipSelfParam && o.Parameters.Count > 0 && o.Parameters[0].Name == PythonNames.Self ? 1 : 0;

        var selfOffsetA = SelfOffset(a);
        var selfOffsetB = SelfOffset(b);
        var variadicA = a.Parameters.Skip(selfOffsetA).FirstOrDefault(p => p.IsVariadic);
        var variadicB = b.Parameters.Skip(selfOffsetB).FirstOrDefault(p => p.IsVariadic);

        bool hasStrictlyBetter = false;

        for (int i = 0; i < context.ArgTypes.Count; i++)
        {
            SemanticType GetParamType(FunctionSymbol o, int selfOff, ParameterSymbol? variadic)
            {
                var paramIdx = i + selfOff;
                if (paramIdx < o.Parameters.Count && !o.Parameters[paramIdx].IsVariadic)
                    return o.Parameters[paramIdx].Type;
                if (variadic != null)
                    return variadic.Type;
                return SemanticType.Unknown;
            }

            var paramTypeA = GetParamType(a, selfOffsetA, variadicA);
            var paramTypeB = GetParamType(b, selfOffsetB, variadicB);

            if (context.TypeSubstitution != null)
            {
                paramTypeA = context.TypeSubstitution(paramTypeA);
                paramTypeB = context.TypeSubstitution(paramTypeB);
            }

            // Equal types contribute nothing to the comparison — unless the
            // underlying CLR types differ (e.g., ClrTypeBridge maps IEnumerable<T>
            // and Sharpy.List<T> both to list[T]).
            if (paramTypeA.Equals(paramTypeB))
            {
                var clrTypeA = ResolveClrParameterType(a, i + selfOffsetA, paramTypeA);
                var clrTypeB = ResolveClrParameterType(b, i + selfOffsetB, paramTypeB);
                if (clrTypeA != null && clrTypeB != null && clrTypeA != clrTypeB)
                {
                    if (clrTypeB.IsAssignableFrom(clrTypeA) && !clrTypeA.IsAssignableFrom(clrTypeB))
                        hasStrictlyBetter = true;
                    else if (clrTypeA.IsAssignableFrom(clrTypeB) && !clrTypeB.IsAssignableFrom(clrTypeA))
                        return false;
                }
                continue;
            }

            var aToB = IsAssignable(paramTypeA, paramTypeB);
            var bToA = IsAssignable(paramTypeB, paramTypeA);

            if (aToB && !bToA)
            {
                // A's parameter is strictly more specific at this position.
                hasStrictlyBetter = true;
            }
            else if (bToA && !aToB)
            {
                // A's parameter is strictly less specific at this position — A cannot win.
                return false;
            }
            else if (IsMoreSpecificType(paramTypeA, paramTypeB))
            {
                // Assignability is neutral (e.g. list[T] vs list[list[T]] under open type
                // parameters), but A is structurally more specific (C# §12.6.4.4: a type
                // parameter is less specific than a structured type). This lets
                // Array(list[list[T]]) win over Array(list[T]) for a nested literal (#957).
                hasStrictlyBetter = true;
            }
            else if (IsMoreSpecificType(paramTypeB, paramTypeA))
            {
                return false;
            }
            // Both assignable or neither, and structurally equal: no preference here.
        }

        return hasStrictlyBetter;
    }

    /// <summary>
    /// The overload set a callable REFERENCE denotes, or null when the expression is not a reference
    /// to a declared callable (a local of function type, a lambda, a delegate field). Covers the same
    /// callee kinds the call path resolves — bare functions and builtins, instance and builtin-type
    /// methods, and module-qualified functions — so <see cref="CheckReferencedCallableOverloads"/>
    /// sees the real candidate set no matter how the callable was named (#1170).
    /// </summary>
    /// <returns>The overload set, plus the receiver's type-argument substitution — a method reached
    /// through <c>list[int]</c> denotes signatures with <c>T</c> already replaced by <c>int</c>, which
    /// is what a target type is compared against.</returns>
    private (List<FunctionSymbol> Overloads, Func<SemanticType, SemanticType> Substitute)?
        ResolveReferencedCallableOverloads(Expression reference)
    {
        static SemanticType Identity(SemanticType t) => t;

        switch (reference)
        {
            case Identifier id:
            {
                // A local/parameter binding shadows the declaration: after `g = xs.pop`, `g` is a
                // variable of function type, not a reference to an overload set.
                if (_symbolTable.Lookup(id.Name) is VariableSymbol)
                    return null;
                var functionOverloads = _symbolTable.LookupFunctionOverloads(id.Name)
                    ?? _symbolTable.BuiltinRegistry.GetFunctionOverloads(id.Name);
                return functionOverloads == null ? null : (functionOverloads, Identity);
            }

            case MemberAccess memberAccess:
            {
                // The receiver's type was recorded when the member access was checked.
                var receiverType = _semanticInfo.GetExpressionType(memberAccess.Object);
                if (receiverType == null)
                    return null;

                if (receiverType is ModuleType)
                {
                    var moduleOverloads = LookupModuleFunctionOverloads(receiverType, memberAccess.Member);
                    return moduleOverloads == null ? null : (moduleOverloads, Identity);
                }

                var methodOverloads = LookupInstanceMethodOverloads(receiverType, memberAccess.Member);
                if (methodOverloads == null)
                    return null;

                var (ownerSymbol, typeArgs) = ResolveBuiltinTypeInfo(receiverType);
                ownerSymbol ??= receiverType switch
                {
                    UserDefinedType { Symbol: { } udtSymbol } => udtSymbol,
                    GenericType { GenericDefinition: { } genericDefinition } => genericDefinition,
                    _ => null
                };
                typeArgs ??= (receiverType as GenericType)?.TypeArguments;

                if (ownerSymbol == null || typeArgs == null
                    || ownerSymbol.TypeParameters.Count != typeArgs.Count)
                {
                    return (methodOverloads, Identity);
                }

                var typeParameters = ownerSymbol.TypeParameters;
                return (methodOverloads, t => SubstituteTypeParameters(t, typeParameters, typeArgs));
            }

            default:
                return null;
        }
    }

    /// <summary>
    /// Applies the value-position rule for a reference to an OVERLOADED callable (#1170).
    ///
    /// <para>The member/function lookups return whichever overload they find first, and that became
    /// the binding's type. When the overloads accept DIFFERENT NUMBERS OF ARGUMENTS the pick decides
    /// how many arguments the binding will accept, so an arbitrary one is silently wrong:
    /// <c>g = xs.pop</c> bound the zero-argument <c>pop()</c>, and <c>g(0)</c> then failed an arity
    /// check against an overload nobody chose. Such a reference is resolved from the target type when
    /// there is one, and rejected (SPY0336) when there is not.</para>
    ///
    /// <para>An arity-uniform overload set — every candidate taking the same number of arguments — is
    /// left alone. The conversion families (<c>int</c>, <c>str</c>, <c>float</c>) and single-argument
    /// builtins like <c>len</c> are all of this shape, and passing them as a key/map function
    /// (<c>map(int, xs)</c>, <c>min(xs, key=len)</c>) is established, working behavior: whichever
    /// candidate binds, calls through the binding pass the same arity check. Only their parameter
    /// types differ, and that is what ordinary assignability at the call site already reports.</para>
    ///
    /// <para>Only reached for references in VALUE position; the immediate callee of a call keeps its
    /// placeholder type because the call path resolves the real overload against the arguments.</para>
    /// </summary>
    /// <returns>The type to bind: the selected overload's signature, or Unknown after reporting
    /// SPY0336. Returns <paramref name="referencedType"/> unchanged when nothing is ambiguous.</returns>
    private SemanticType CheckReferencedCallableOverloads(Expression reference, FunctionType referencedType)
    {
        if (ResolveReferencedCallableOverloads(reference) is not var (overloads, substitute)
            || overloads.Count <= 1)
        {
            return referencedType;
        }

        // Distinct signatures only: an overload set can carry duplicate entries for the same
        // signature (a discovered method registered under several spellings), and those are not an
        // ambiguity the user can resolve.
        var candidates = new List<(FunctionSymbol Symbol, FunctionType Signature)>();
        foreach (var overload in overloads)
        {
            var signature = ReferenceSignatureOf(overload, substitute);
            if (!candidates.Any(c => c.Signature.Equals(signature)))
                candidates.Add((overload, signature));
        }

        if (candidates.Count <= 1 || !CandidateAritiesDiverge(candidates))
            return referencedType;

        // Target-typed selection: an annotated target, a parameter the reference is passed to, or a
        // declared return type supplies the signature the user meant. `_expectedType` already carries
        // it at every one of those positions.
        if (_expectedType is FunctionType target)
        {
            var matching = candidates.Where(c => SignatureSatisfiesTarget(c.Signature, target)).ToList();
            if (matching.Count == 1)
                return matching[0].Signature;
        }

        var signatures = string.Join("\n  ", candidates.Select(c => $"{c.Symbol.Name}{c.Signature.GetDisplayName()}"));
        AddError(
            $"'{DescribeReference(reference)}' has {candidates.Count} overloads taking different numbers of "
            + "arguments, so it cannot be used as a value without a target type to select one. Candidates:\n  "
            + signatures,
            reference.LineStart, reference.ColumnStart,
            code: DiagnosticCodes.Semantic.AmbiguousCallableReference,
            span: reference.Span);
        return SemanticType.Unknown;
    }

    /// <summary>
    /// Whether the candidates accept different numbers of arguments — the case where the choice of
    /// overload changes what calls through the binding are legal. Each candidate's range is
    /// [required, total] once optional parameters are accounted for.
    /// </summary>
    private static bool CandidateAritiesDiverge(
        List<(FunctionSymbol Symbol, FunctionType Signature)> candidates)
    {
        static (int Required, int Total) ArityOf(FunctionType signature) =>
            (signature.ParameterTypes.Count - signature.OptionalParameterCount,
             signature.ParameterTypes.Count);

        var first = ArityOf(candidates[0].Signature);
        return candidates.Any(c => ArityOf(c.Signature) != first);
    }

    /// <summary>
    /// The signature a reference to <paramref name="overload"/> denotes: its parameters and return
    /// type, with a leading <c>self</c> dropped because the receiver is already bound at the
    /// reference site (<c>xs.pop</c> denotes <c>(int) -&gt; int</c>, not <c>(list[int], int) -&gt; int</c>).
    /// </summary>
    private static FunctionType ReferenceSignatureOf(
        FunctionSymbol overload, Func<SemanticType, SemanticType> substitute)
    {
        var selfOffset = overload.Parameters.Count > 0
            && overload.Parameters[0].Name == PythonNames.Self
            ? 1 : 0;
        var parameters = overload.Parameters.Select(p => p with { Type = substitute(p.Type) }).ToList();
        return FunctionType.FromParameters(
            parameters, substitute(overload.ReturnType), skipLeading: selfOffset);
    }

    /// <summary>
    /// Whether a candidate signature can be bound to a target function type: same arity (counting the
    /// target's parameters against the candidate's required ones), every target parameter assignable
    /// to the candidate's corresponding parameter (contravariant), and the candidate's return type
    /// assignable to the target's (covariant).
    /// </summary>
    private static bool SignatureSatisfiesTarget(FunctionType candidate, FunctionType target)
    {
        var required = candidate.ParameterTypes.Count - candidate.OptionalParameterCount;
        if (target.ParameterTypes.Count < required || target.ParameterTypes.Count > candidate.ParameterTypes.Count)
            return false;

        for (var i = 0; i < target.ParameterTypes.Count; i++)
        {
            if (!target.ParameterTypes[i].IsAssignableTo(candidate.ParameterTypes[i]))
                return false;
        }

        return target.ReturnType is UnknownType || candidate.ReturnType.IsAssignableTo(target.ReturnType);
    }

    /// <summary>How to name a callable reference in a diagnostic.</summary>
    private static string DescribeReference(Expression reference) => reference switch
    {
        Identifier id => id.Name,
        MemberAccess memberAccess => memberAccess.Member,
        _ => "reference"
    };
}
