using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic.Registry;
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
                var argNode = ArgumentNodeAt(context.Call, i);
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
                    // A projected argument (one in an iterable position, #1159, #1198) is
                    // shape-matched on the type codegen will pass as well as on its own.
                    if (!ArgMatchesGenericShape(context.ArgTypes[i], expectedType)
                        && !(ProjectedArgumentType(argNode) is { } projectedArg
                             && ArgMatchesGenericShape(projectedArg, expectedType)))
                    {
                        typesMatch = false;
                        break;
                    }
                    continue;
                }

                if (!IsArgumentAssignable(context.ArgTypes[i], expectedType, argNode))
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
    /// The AST node bound to positional argument <paramref name="index"/>, or null when the caller
    /// supplied no call node (the dunder-overload path) or the index falls past the positional
    /// arguments (a keyword-satisfied or variadic slot). Argument-binding assignability consults the
    /// node for the projection an argument carries (<see cref="ProjectedArgumentType"/>, #1159).
    /// </summary>
    private static Expression? ArgumentNodeAt(FunctionCall? call, int index) =>
        call != null && index < call.Arguments.Length ? call.Arguments[index] : null;

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
    /// <summary>
    /// The value-position rules for a callable reference (#1168, #1170), applied at one choke point in
    /// <see cref="CheckExpression"/> for every reference that is not a call's own callee.
    ///
    /// <para>Three rules, in order: a form that exists only as call syntax is rejected outright
    /// (SPY0337); a builtin type constructor reference is pinned to a signature, bound as a
    /// call-only alias, or rejected (SPY0342, #1182); and an overload set whose candidates take
    /// different numbers of arguments is resolved from the target type or rejected (SPY0336).
    /// Everything else keeps the type it already had.</para>
    /// </summary>
    private SemanticType CheckValuePositionReference(Expression reference, SemanticType type)
    {
        if (CallSyntaxOnlyFormOf(reference, type) is { } callSyntaxOnlyForm)
        {
            AddError(
                $"{callSyntaxOnlyForm.Description} must be called as a function; it cannot be used as a value. "
                + $"Wrap it in a lambda to pass it around: {callSyntaxOnlyForm.LambdaEscape}",
                reference.LineStart, reference.ColumnStart,
                code: DiagnosticCodes.Semantic.CallSyntaxOnlyReference,
                span: reference.Span);
            return SemanticType.Unknown;
        }

        if (CheckConstructorReference(reference, type) is { } constructorReferenceType)
            return constructorReferenceType;

        return type is FunctionType referencedFunctionType
            ? CheckReferencedCallableOverloads(reference, referencedFunctionType)
            : type;
    }

    /// <summary>
    /// The value-position rule for a bare builtin type-constructor reference (<c>f = int</c>,
    /// <c>f = dict</c>) — Sharpy's method group (#1182). Three tiers, in order:
    ///
    /// <list type="number">
    /// <item><description>An expected function type supplies a signature: the reference binds that
    /// signature and records a <see cref="ConstructorReferenceLowering"/> for codegen.</description></item>
    /// <item><description>A binding with no signature anywhere becomes a call-only ALIAS carrying
    /// <see cref="ConstructorReferenceType"/>; each call through it resolves like a call of the
    /// builtin itself.</description></item>
    /// <item><description>Anything else has no signature and no way to acquire one: SPY0342.</description></item>
    /// </list>
    ///
    /// <para>A builtin type NAME written in one of the established non-value positions is left with
    /// the type it has today (see <see cref="IsConstructorReferenceValueUse"/>) — those positions
    /// already work, and rejecting or re-typing on the reference alone is what broke them last time
    /// (#1170). A read whose type is already the carrier is always a value use, because nothing but
    /// this rule produces one.</para>
    /// </summary>
    /// <returns>The type to bind, or <c>null</c> when the reference is not a builtin constructor
    /// reference in a position this rule governs, so the caller's remaining rules apply.</returns>
    private SemanticType? CheckConstructorReference(Expression reference, SemanticType type)
    {
        if (ConstructorReferenceOf(reference, type) is not { } constructorReference)
            return null;

        // A read already typed as the carrier is always a value use; nothing but this rule makes one.
        var isValueUse = type is ConstructorReferenceType || IsConstructorReferenceValueUse(reference);

        // Tier 1. A target type with unresolved type parameters — a generic parameter such as map's
        // `(T) -> R` — supplies no signature; it is not a failure to match one.
        //
        // Pinning runs even in the non-value positions, where it can only widen what binds: a
        // reference that pins works where the legacy synthesized signature was rejected
        // (`apply(int, "5")` for `apply(fn: (str) -> int, …)`), and one that does not pin falls back
        // to the typing that position has today rather than acquiring a diagnostic.
        if (_expectedType is FunctionType target && !ContainsTypeParameter(target))
        {
            if (TryPinConstructorReference(reference, constructorReference, target))
                return target;

            if (!isValueUse)
                return null;

            AddError(
                $"{ConstructorReferenceSubject(constructorReference)} has no constructor signature matching "
                + $"'{target.GetDisplayName()}'. {ConstructorReferenceShapes(constructorReference)}",
                reference.LineStart, reference.ColumnStart,
                code: DiagnosticCodes.Semantic.UnpinnedConstructorReference,
                span: reference.Span);
            return SemanticType.Unknown;
        }

        if (!isValueUse)
            return null;

        // Tier 2. `f = int` / `f = dict`: nothing says which signature was meant, so the name aliases
        // the builtin and every call through it is resolved at its own call site. A binding whose
        // target already holds an alias is a RE-alias (`f = int; …; f = str`) — its expected type is
        // the previous carrier, which supplies no signature either.
        if (_expectedType is null or ConstructorReferenceType
            && _currentBindingValue != null
            && ReferenceEquals(UnwrapParenthesized(_currentBindingValue), UnwrapParenthesized(reference)))
        {
            return constructorReference;
        }

        // An alias read in a call argument whose parameter type is generic (`map(f, xs)`) binds the
        // same synthesized signature the builtin NAME binds there, so an alias behaves in argument
        // positions exactly like the name it aliases. The read still needs a lowering: the alias
        // BINDING emits nothing, so the read must emit the builtin's method group, not the name.
        if (type is ConstructorReferenceType && IsDirectCallArgument(reference)
            && constructorReference.Family == ConstructorReferenceFamily.Conversion
            && SynthesizePrimitiveFunctionType(constructorReference.Symbol) is FunctionType synthesized)
        {
            _semanticInfo.SetConstructorReferenceLowering(reference,
                new ConstructorReferenceLowering(
                    ConstructorReferenceFamily.Conversion, constructorReference.Name,
                    synthesized.ReturnType, synthesized.ParameterTypes.Count));
            return synthesized;
        }

        // Tier 3.
        AddError(
            $"cannot infer a single callable signature for {ConstructorReferenceSubject(constructorReference)}: "
            + $"{ConstructorReferenceAmbiguityReason(constructorReference)}, and this position supplies "
            + "no signature to select one. Annotate the target with a function type "
            + $"({ConstructorReferenceAnnotationExample(constructorReference)}), bind it to a name you "
            + "only ever call, or wrap the construction in a lambda.",
            reference.LineStart, reference.ColumnStart,
            code: DiagnosticCodes.Semantic.UnpinnedConstructorReference,
            span: reference.Span);
        return SemanticType.Unknown;
    }

    /// <summary>
    /// The constructor reference an expression denotes, or null when it is not one. A read already
    /// typed as the carrier denotes itself; a bare identifier denotes one when it resolves to a type
    /// symbol with an emittable construction shape — a builtin of a recognized family, or a user
    /// class/struct (#1211).
    ///
    /// <para>Shadowing is preserved by construction: <c>_symbolTable.Lookup</c> returns the user
    /// declaration's own symbol, and the registry-identity test is what decides WHICH family rule
    /// applies. A user class named <c>int</c> is therefore its own UserType reference pinning
    /// against its own constructors — never the builtin's conversion overload set.</para>
    /// </summary>
    private ConstructorReferenceType? ConstructorReferenceOf(Expression reference, SemanticType type)
    {
        if (type is ConstructorReferenceType carrier)
            return carrier;

        if (reference is not Identifier id || _symbolTable.Lookup(id.Name) is not TypeSymbol typeSymbol)
            return null;

        var family = ReferenceEquals(typeSymbol, _symbolTable.BuiltinRegistry.GetType(id.Name))
            ? ConstructorReferenceFamilyOf(typeSymbol)
            : UserConstructorReferenceFamilyOf(typeSymbol);

        return family is not { } resolved
            ? null
            : new ConstructorReferenceType { Name = id.Name, Symbol = typeSymbol, Family = resolved };
    }

    /// <summary>
    /// Whether a user-declared type is constructible as a reference, and so which family it takes
    /// (#1211). Only <c>Class</c> and <c>Struct</c> qualify, and only when not abstract.
    ///
    /// <para>Deliberately NOT gated on having a declared constructor: <c>TypeSymbol.Constructors</c>
    /// is populated only from a declared <c>__init__</c> or dataclass synthesis, so a plain
    /// <c>class Point: x: int = 0</c> has none — and that is #1211's own repro. A class with no
    /// declared constructor offers exactly the zero-argument shape.</para>
    ///
    /// <para>Interfaces, enums, unions, delegates and abstract classes return null and keep the
    /// typing they have today rather than acquiring a new diagnostic — the same posture
    /// <see cref="ConstructorReferenceFamilyOf"/> takes for <c>object</c>/<c>bytes</c>/the view
    /// types.</para>
    /// </summary>
    private static ConstructorReferenceFamily? UserConstructorReferenceFamilyOf(TypeSymbol typeSymbol)
        => typeSymbol.TypeKind is TypeKind.Class or TypeKind.Struct && !typeSymbol.IsAbstract
            ? ConstructorReferenceFamily.UserType
            : null;

    /// <summary>
    /// Which construction shape a builtin type emits as, or null for a builtin type that is not a
    /// constructor reference at all (<c>object</c>, <c>bytes</c>, the view and iterator types): those
    /// keep the behavior they have today rather than acquiring a new diagnostic.
    /// </summary>
    private ConstructorReferenceFamily? ConstructorReferenceFamilyOf(TypeSymbol typeSymbol)
    {
        // Exactly the set whose reference synthesizes a signature today (SynthesizePrimitiveFunctionType):
        // a primitive backed by a Sharpy.Builtins overload set.
        if (PrimitiveCatalog.IsPrimitive(typeSymbol.Name)
            && _symbolTable.BuiltinRegistry.GetFunctionOverloads(typeSymbol.Name) is { Count: > 0 })
        {
            return ConstructorReferenceFamily.Conversion;
        }

        return typeSymbol.Name switch
        {
            BuiltinNames.List or BuiltinNames.Dict or BuiltinNames.Set or BuiltinNames.Tuple =>
                ConstructorReferenceFamily.Collection,
            _ => null
        };
    }

    /// <summary>
    /// Whether a builtin type NAME at this reference is being used as a value. False for the
    /// positions where a type name legitimately appears and already works — the receiver of a static
    /// member (<c>int.parse(s)</c>, <c>dict.fromkeys(ks)</c>), a type test's type argument including
    /// the tuple spelling, a node recorded elsewhere as naming a type, and a direct call argument
    /// (<c>map(int, xs)</c>, <c>sorted(xs, key=int)</c>, <c>defaultdict(list)</c>), where a C# target
    /// type exists and the established typing must not change (#1170).
    /// </summary>
    private bool IsConstructorReferenceValueUse(Expression reference)
        => !IsCurrentMemberAccessQualifier(reference)
            && !IsTypeTestTypeArgument(reference)
            && !IsCurrentIndexArgument(reference)
            && !_semanticInfo.IsTypeReference(reference)
            && !IsDirectCallArgument(reference);

    /// <summary>
    /// Whether the reference is a type argument of the index currently being checked
    /// (<c>Outer.Inner[int]</c>). See <c>_currentIndexArguments</c>.
    /// </summary>
    private bool IsCurrentIndexArgument(Expression reference)
        => _currentIndexArguments?.Contains(UnwrapParenthesized(reference)) == true;

    /// <summary>
    /// The index expression plus, for a multi-argument index, each of its elements, for
    /// <c>_currentIndexArguments</c>.
    /// </summary>
    private static HashSet<Expression> IndexArgumentSetOf(Expression index)
    {
        var arguments = new HashSet<Expression>(ReferenceEqualityComparer.Instance);
        var unwrapped = UnwrapParenthesized(index);
        arguments.Add(unwrapped);
        if (unwrapped is TupleLiteral indexTuple)
        {
            foreach (var element in indexTuple.Elements)
                arguments.Add(UnwrapParenthesized(element));
        }

        return arguments;
    }

    /// <summary>
    /// Whether the reference names a type being tested for: the type argument of the enclosing type
    /// test, or one element of the tuple spelling <c>isinstance(x, (int, str))</c>.
    /// </summary>
    private bool IsTypeTestTypeArgument(Expression reference)
    {
        if (_typeTestTypeArgument == null)
            return false;

        var unwrapped = UnwrapParenthesized(reference);
        if (ReferenceEquals(_typeTestTypeArgument, unwrapped))
            return true;

        return _typeTestTypeArgument is TupleLiteral typeTuple
            && typeTuple.Elements.Any(element => ReferenceEquals(UnwrapParenthesized(element), unwrapped));
    }

    /// <summary>Whether the reference is a direct argument of the call currently being checked.</summary>
    private bool IsDirectCallArgument(Expression reference)
        => _currentCallArguments?.Contains(UnwrapParenthesized(reference)) == true;

    /// <summary>
    /// Pins a constructor reference to <paramref name="target"/> when the builtin can construct that
    /// signature, recording the lowering codegen applies. The pinned Sharpy type is the target itself:
    /// the declared C# delegate type is what the user wrote, and the emitted method group or
    /// constructor lambda is converted to it.
    /// </summary>
    private bool TryPinConstructorReference(
        Expression reference, ConstructorReferenceType constructorReference, FunctionType target)
    {
        var pinnable = constructorReference.Family switch
        {
            ConstructorReferenceFamily.Conversion => ConversionSignatureSatisfies(constructorReference, target),
            ConstructorReferenceFamily.UserType => UserTypeSignatureSatisfies(constructorReference, target),
            _ => CollectionSignatureSatisfies(constructorReference, target),
        };

        if (!pinnable)
            return false;

        _semanticInfo.SetConstructorReferenceLowering(reference,
            new ConstructorReferenceLowering(
                constructorReference.Family, constructorReference.Name,
                target.ReturnType, target.ParameterTypes.Count));
        return true;
    }

    /// <summary>
    /// Whether one of the builtin's conversion overloads can be bound to <paramref name="target"/>.
    /// The emitted C# is the <c>Sharpy.Builtins.X</c> method group, so this asks the same question
    /// C#'s method-group conversion will ask of it.
    /// </summary>
    private bool ConversionSignatureSatisfies(ConstructorReferenceType constructorReference, FunctionType target)
    {
        var overloads = _symbolTable.BuiltinRegistry.GetFunctionOverloads(constructorReference.Name);
        return overloads != null
            && overloads.Any(overload =>
                SignatureSatisfiesTarget(ReferenceSignatureOf(overload, t => t), target));
    }

    /// <summary>
    /// Whether one of the user class's DECLARED constructor overloads can be bound to
    /// <paramref name="target"/> (#1211). The rule is the class's own constructors, which is why
    /// this is a third family rather than a generalized collection: the conversion families match a
    /// <c>Sharpy.Builtins</c> overload set and the collection families a known generic shape.
    ///
    /// <para>Candidates come from <see cref="ResolveInitializerConstructorCandidates"/>, the same
    /// walk ordinary construction uses, so a constructor inherited from a base class is found the
    /// same way. An empty candidate list is not "no shapes" — a class with no declared
    /// <c>__init__</c> offers exactly the zero-argument one.</para>
    ///
    /// <para>The target's return type must be this very class. A signature returning something else
    /// is not a construction of it, however well the parameters line up.</para>
    /// </summary>
    private bool UserTypeSignatureSatisfies(ConstructorReferenceType constructorReference, FunctionType target)
    {
        var typeSymbol = constructorReference.Symbol;
        if (target.ReturnType is not UserDefinedType returned
            || !ReferenceEquals(returned.Symbol, typeSymbol))
        {
            return false;
        }

        var candidates = ResolveInitializerConstructorCandidates(typeSymbol);
        if (candidates.Count == 0)
            return target.ParameterTypes.Count == 0;

        // __init__ returns None; the SHAPE a reference pins to returns the constructed class, so the
        // candidate's return type is replaced before the comparison.
        return candidates.Any(constructor =>
            SignatureSatisfiesTarget(
                ReferenceSignatureOf(constructor, t => t) with { ReturnType = target.ReturnType },
                target));
    }

    /// <summary>
    /// Whether the collection builtin can construct <paramref name="target"/>. Two shapes are
    /// emittable: the empty constructor (<c>() -&gt; list[int]</c>) and the copy constructor over the
    /// same collection type (<c>(list[int]) -&gt; list[int]</c>). Anything else — a conversion from a
    /// different iterable, or <c>tuple</c>, whose arity is part of its type (#1159) — has no single
    /// constructor to emit and falls through to the alias or the diagnostic.
    /// </summary>
    private static bool CollectionSignatureSatisfies(ConstructorReferenceType constructorReference, FunctionType target)
    {
        if (target.ReturnType is not GenericType constructed
            || !string.Equals(constructed.Name, constructorReference.Name, StringComparison.Ordinal)
            || ContainsTypeParameter(constructed))
        {
            return false;
        }

        return target.ParameterTypes.Count switch
        {
            0 => true,
            1 => target.ParameterTypes[0].Equals(constructed),
            _ => false
        };
    }

    /// <summary>
    /// How a SPY0342 message names the thing being referenced. The builtin families keep the
    /// wording they have always had — three <c>.error</c> fixtures pin it verbatim
    /// (<c>constructor_reference_wrong_shape</c>, <c>_unpinned_stored</c>, <c>_unpinned_conditional</c>)
    /// — and only the user families gain new wording (#1211).
    /// </summary>
    private static string ConstructorReferenceSubject(ConstructorReferenceType constructorReference)
        => constructorReference.Family == ConstructorReferenceFamily.UserType
            ? $"{(constructorReference.Symbol.TypeKind == TypeKind.Struct ? "struct" : "class")} "
                + $"'{constructorReference.Name}'"
            : $"builtin type '{constructorReference.Name}'";

    /// <summary>Why a constructor reference has no single signature, for the SPY0342 message.</summary>
    private string ConstructorReferenceAmbiguityReason(ConstructorReferenceType constructorReference)
        => constructorReference.Family switch
        {
            ConstructorReferenceFamily.Conversion => $"'{constructorReference.Name}' names an overload set",
            ConstructorReferenceFamily.UserType =>
                ResolveInitializerConstructorCandidates(constructorReference.Symbol) is { Count: > 1 } overloads
                    ? $"'{constructorReference.Name}' declares {overloads.Count} constructor overloads"
                    : "a constructor reference has no runtime value of its own",
            _ => $"'{constructorReference.Name}' is generic, so its type arguments are unknown here",
        };

    /// <summary>An annotation that would pin this reference, for the SPY0342 message.</summary>
    private string ConstructorReferenceAnnotationExample(ConstructorReferenceType constructorReference)
        => constructorReference.Family switch
        {
            ConstructorReferenceFamily.Conversion =>
                $"f: (str) -> {constructorReference.Name} = {constructorReference.Name}",
            ConstructorReferenceFamily.UserType =>
                $"f: {UserTypeShapeOf(constructorReference).GetDisplayName()} = {constructorReference.Name}",
            _ => $"f: () -> {constructorReference.Name}[...] = {constructorReference.Name}",
        };

    /// <summary>The construction shapes a type offers, for the no-matching-signature message.</summary>
    private string ConstructorReferenceShapes(ConstructorReferenceType constructorReference)
    {
        if (constructorReference.Family == ConstructorReferenceFamily.Collection)
        {
            return $"'{constructorReference.Name}' can be pinned to its empty constructor "
                + $"(() -> {constructorReference.Name}[...]) or its copy constructor "
                + $"({constructorReference.Name}[...] -> {constructorReference.Name}[...]).";
        }

        if (constructorReference.Family == ConstructorReferenceFamily.UserType)
        {
            var shapes = UserTypeConstructorShapes(constructorReference)
                .Select(shape => shape.GetDisplayName())
                .Distinct()
                .ToList();
            return "Candidates:\n  " + string.Join("\n  ", shapes);
        }

        var overloads = _symbolTable.BuiltinRegistry.GetFunctionOverloads(constructorReference.Name);
        if (overloads == null || overloads.Count == 0)
            return string.Empty;

        var signatures = overloads
            .Select(overload => ReferenceSignatureOf(overload, t => t).GetDisplayName())
            .Distinct()
            .ToList();
        return "Candidates:\n  " + string.Join("\n  ", signatures);
    }

    /// <summary>
    /// The signatures a user class or struct can be pinned to (#1211): one per declared constructor,
    /// each returning the class itself rather than <c>__init__</c>'s None. A class with no declared
    /// constructor offers exactly the zero-argument shape — the same rule
    /// <see cref="UserTypeSignatureSatisfies"/> applies, so the message can never advertise a shape
    /// that would not actually pin.
    /// </summary>
    private IEnumerable<FunctionType> UserTypeConstructorShapes(ConstructorReferenceType constructorReference)
    {
        var constructed = new UserDefinedType
        {
            Name = constructorReference.Name,
            Symbol = constructorReference.Symbol,
        };

        var candidates = ResolveInitializerConstructorCandidates(constructorReference.Symbol);
        return candidates.Count == 0
            ? new[] { FunctionType.FromParameters(new List<ParameterSymbol>(), constructed) }
            : candidates.Select(constructor =>
                ReferenceSignatureOf(constructor, t => t) with { ReturnType = constructed });
    }

    /// <summary>The first shape a user type offers, for the SPY0342 annotation suggestion.</summary>
    private FunctionType UserTypeShapeOf(ConstructorReferenceType constructorReference)
        => UserTypeConstructorShapes(constructorReference).First();

    /// <summary>
    /// Recognises a reference to a form that Sharpy supports only as call syntax, describing it for
    /// the diagnostic; null for anything usable as a value.
    ///
    /// <para>Two forms, each verified to have no working value-position behavior today:
    /// <c>isinstance</c>, which is a compile-time narrowing construct rather than a function and binds
    /// a signature no call through it can satisfy (#1168); and a union variant constructor
    /// (<c>Shape.Circle</c>), whose reference binds the case type and fails at the use site with a bare
    /// "not callable". Following the <c>Ok</c>/<c>Some</c> precedent (SPY0230) and #1138's SPY0335, the
    /// rejection is deliberate and non-breaking to lift later if these gain first-class values.</para>
    ///
    /// <para>A bare builtin type constructor reference (<c>f = dict</c>) is NOT one of these forms and
    /// is not rejected here: it is a value with no natural type, governed by
    /// <see cref="CheckConstructorReference"/> (#1182). That rule cannot key off the reference alone
    /// either, because a builtin type NAME appears in several legitimate non-value positions this
    /// choke point cannot tell apart from a value: the receiver of a static member
    /// (<c>int.parse(s)</c>, <c>dict.fromkeys(ks)</c>), a type argument (<c>isinstance(x, int)</c>)
    /// including inside a tuple of them, and a keyword argument whose delegate target is not carried
    /// in <c>_expectedType</c> (<c>sorted(xs, key=int)</c>). Rejecting on the reference alone broke
    /// all three (#1170), so the position is what decides.</para>
    /// </summary>
    private (string Description, string LambdaEscape)? CallSyntaxOnlyFormOf(
        Expression reference, SemanticType type)
    {
        // A type test's type argument names a type; it is not a value use of that type.
        if (ReferenceEquals(reference, _typeTestTypeArgument))
            return null;

        // A union variant constructor: Shape.Circle. Same shape the union-case construction arm in
        // CheckFunctionCall matches, so the call form stays fully functional.
        if (reference is MemberAccess variantAccess
            && type is UserDefinedType { Symbol.BaseType: { TypeKind: TypeKind.Union } })
        {
            return ($"union variant constructor '{variantAccess.Member}'",
                $"lambda ...: {DescribeMemberPath(variantAccess)}(...)");
        }

        if (reference is not Identifier id)
            return null;

        // A user declaration shadowing the builtin name is an ordinary callable.
        var symbol = _symbolTable.Lookup(id.Name);

        if (id.Name == BuiltinNames.Isinstance && symbol is not FunctionSymbol { CodeGenInfo: not null })
            return ("'isinstance'", $"lambda v: {BuiltinNames.Isinstance}(v, SomeType)");

        return null;
    }

    /// <summary>Renders a member access back to source form for a diagnostic suggestion.</summary>
    private static string DescribeMemberPath(MemberAccess memberAccess) => memberAccess.Object switch
    {
        Identifier objectId => $"{objectId.Name}.{memberAccess.Member}",
        MemberAccess nested => $"{DescribeMemberPath(nested)}.{memberAccess.Member}",
        _ => memberAccess.Member
    };

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
