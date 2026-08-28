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
    /// <param name="ReceiverIsLeadingParameter">If true, a candidate whose leading parameter is not
    /// named <c>self</c> may still carry the receiver there, and it is identified by SHAPE — the
    /// arguments match the TRAILING parameters. Only the operator-dunder path sets this; see
    /// <see cref="ResolveDunderOverload"/> for why the method-call paths must not.</param>
    internal record OverloadResolutionContext(
        List<FunctionSymbol> Candidates,
        int TotalArgCount,
        List<SemanticType> ArgTypes,
        bool SkipSelfParam = false,
        Func<SemanticType, SemanticType>? TypeSubstitution = null,
        bool SkipUnknownTypes = false,
        IReadOnlyCollection<string>? KeywordArgNames = null,
        FunctionCall? Call = null,
        bool ReceiverIsLeadingParameter = false);

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
    /// <remarks>
    /// <c>internal</c> rather than <c>private</c> for a test seam, warranted here in a way it usually
    /// is not: which path answers an operator dunder — this resolver or the CLR fallback beneath it —
    /// is invisible in emitted C# by construction, so no <c>.spy</c> fixture can observe it and this
    /// return value is the only place the fact exists (#1395).
    /// </remarks>
    internal FunctionSymbol? ResolveDunderOverload(
        IReadOnlyList<FunctionSymbol> candidates, SemanticType argType, SemanticType? receiver = null)
    {
        var substitution = BuildReceiverSubstitution(receiver);

        // A CLR type reflects BOTH directions of a repetition operator — `List<T> * int` AND
        // `int * List<T>` — and the resolver checks only the OPERAND (the trailing parameter), so
        // without this it matched `list * list` against the reversed overload's `List<T>` operand and
        // typed it list[int], where the fallback (which never applies the wrong-direction operator)
        // refuses it. Keep only candidates whose leading (receiver) parameter admits the actual
        // receiver, so the resolver agrees with the fallback (#1395).
        var applicable = receiver is null
            ? candidates.ToList()
            : candidates.Where(c => ReceiverAdmitsCandidate(c, receiver, substitution)).ToList();

        var (match, _, _) = ResolveOverloadCore(new OverloadResolutionContext(
            Candidates: applicable,
            TotalArgCount: 1,
            ArgTypes: new List<SemanticType> { argType },
            SkipSelfParam: true,
            // A CLR-discovered operator reflects its receiver as `left`/`a`, never `self`, so the
            // skip is by SHAPE here — see ReceiverOffsetOf. Without this the arity filter asked
            // `1 >= 2` and dropped every CLR operator candidate before betterness (#1395).
            ReceiverIsLeadingParameter: true,
            // Close the candidates' open parameter types at the receiver's type arguments so a
            // generic CLR operator (`Dict<K,V> operator|`, `NdArray<T> operator+`) is resolved at the
            // receiver's instantiation, not at open `T`. The single-candidate path already does this
            // via CloseOverReceiver; threading it here is what makes the two paths select the SAME
            // overload (#1395, agreement test). (The numpy ICE itself is prevented by the
            // constraint-aware ResolveClrParameterType, not by this.)
            TypeSubstitution: substitution));
        return match;
    }

    /// <summary>
    /// Whether an operator candidate's LEADING (receiver) parameter admits <paramref name="receiver"/>
    /// — the actual left operand's type. A CLR type surfaces both directions of a repetition operator,
    /// so the wrong-direction overload (leading parameter <c>int</c> for a <c>list</c> receiver) must
    /// be dropped rather than matched on its operand (#1395). A Sharpy-shaped operator's receiver is
    /// its bound <c>self</c>, and a single-operand dunder (an indexer's <c>this[int]</c>) has no
    /// receiver parameter — both admit any receiver here.
    /// </summary>
    private bool ReceiverAdmitsCandidate(
        FunctionSymbol candidate, SemanticType receiver, Func<SemanticType, SemanticType>? substitution)
    {
        if (candidate.Parameters.Count < 2 || candidate.Parameters[0].Name == PythonNames.Self)
            return true;

        var leading = candidate.Parameters[0].Type;
        if (substitution != null)
            leading = substitution(leading);

        // An open leading parameter stands for whatever the receiver would bind it to (a wildcard);
        // only a resolved, mismatched receiver position rules the candidate out.
        if (leading is TypeParameterType or UnknownType or SelfType)
            return true;

        return IsAssignable(receiver, leading);
    }

    /// <summary>
    /// A substitution closing a candidate operator's open type parameters at
    /// <paramref name="receiver"/>'s type arguments (<c>owner.TypeParameters → receiver.TypeArguments</c>),
    /// or null when the receiver is not a closed generic instantiation. Mirrors
    /// <c>TypeInferenceService.CloseOverReceiver</c>, the single-candidate path's substitution, so the
    /// two paths agree (#1395).
    /// </summary>
    private static Func<SemanticType, SemanticType>? BuildReceiverSubstitution(SemanticType? receiver)
    {
        if (receiver is not GenericType { GenericDefinition: { } definition } instantiation)
            return null;

        var typeParameters = definition.TypeParameters;
        if (typeParameters.Count == 0 || typeParameters.Count != instantiation.TypeArguments.Count)
            return null;

        var substitutions = new Dictionary<string, SemanticType>(typeParameters.Count, StringComparer.Ordinal);
        for (int i = 0; i < typeParameters.Count; i++)
            substitutions[typeParameters[i].Name] = instantiation.TypeArguments[i];

        return t => TypeSubstitution.Apply(t, substitutions);
    }

    /// <summary>
    /// How many leading parameters of <paramref name="o"/> the receiver occupies, for a call described
    /// by <paramref name="context"/>. The single source of this rule: it was two divergent copies
    /// (arity filtering and betterness comparison), and fixing one alone would leave the tiebreak
    /// mis-offset (#1395).
    /// </summary>
    /// <remarks>
    /// Name first, then shape. A leading parameter literally named <c>self</c> is the receiver. Failing
    /// that, ONLY on the operator-dunder path (<see cref="OverloadResolutionContext.ReceiverIsLeadingParameter"/>),
    /// a candidate carrying MORE parameters than the call's arguments has its receiver in the leading
    /// position(s) — the arguments match the TRAILING parameters, which is exactly what the
    /// single-candidate path's <c>AcceptsArgument</c> assumes (the operand is <c>Parameters[^1]</c>,
    /// "by SHAPE, never by name"). The two method-call sites that also set <c>SkipSelfParam</c> must NOT
    /// set the flag: a CLR instance method reflects WITHOUT a receiver parameter (<c>xs.append(x)</c> is
    /// <c>(item)</c>), so a leading parameter not named <c>self</c> IS the first argument there.
    /// </remarks>
    private static int ReceiverOffsetOf(FunctionSymbol o, OverloadResolutionContext context)
    {
        if (!context.SkipSelfParam || o.Parameters.Count == 0)
            return 0;
        if (o.Parameters[0].Name == PythonNames.Self)
            return 1;
        if (context.ReceiverIsLeadingParameter && o.Parameters.Count > context.TotalArgCount)
            return o.Parameters.Count - context.TotalArgCount;
        return 0;
    }

    /// <summary>
    /// Core overload resolution algorithm shared by all overload resolution methods.
    /// Performs two-pass matching: first filters by argument count, then checks type compatibility.
    /// </summary>
    /// <returns>A tuple of (matched overload, arity-filtered candidates, whether resolution was ambiguous).</returns>
    private (FunctionSymbol? Match, List<FunctionSymbol> ArityCandidates, bool IsAmbiguous) ResolveOverloadCore(
        OverloadResolutionContext context)
    {
        int GetSelfOffset(FunctionSymbol o) => ReceiverOffsetOf(o, context);

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
        // Name matching uses the same two arms as FindKeywordParameter — verbatim, then the
        // camel-cased spelling of a snake-written kwarg — so a Python-spelled kwarg selects
        // among CLR overloads (verbatim camelCase names) the same way validation later reads
        // the selected one (#1591).
        if (context.KeywordArgNames is { Count: > 0 })
        {
            var positionalArgCount = context.TotalArgCount - context.KeywordArgNames.Count;
            var kwFiltered = arityCandidates.Where(o =>
            {
                var selfOffset = GetSelfOffset(o);
                var paramsAfterSelf = o.Parameters.Skip(selfOffset).ToList();
                var paramNames = paramsAfterSelf.Select(p => p.Name).ToHashSet();
                bool HasParameterFor(string kw) =>
                    paramNames.Contains(kw) || paramNames.Contains(NameMangler.ToCamelCase(kw));

                // Every keyword arg must have a matching parameter name
                if (!context.KeywordArgNames.All(HasParameterFor))
                    return false;

                // For non-variadic overloads, verify that positional args cover
                // exactly the required parameters NOT supplied by keyword args.
                if (!paramsAfterSelf.Any(p => p.IsVariadic))
                {
                    var kwSet = context.KeywordArgNames
                        .SelectMany(kw => new[] { kw, NameMangler.ToCamelCase(kw) })
                        .ToHashSet();
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

                // §10.2.11 constant conversions participate in applicability; the resulting ties
                // are broken by identity-match → better-conversion-target → signed-beats-unsigned
                // in IsMoreSpecificOverload (#1464).
                if (!IsArgumentAssignable(context.ArgTypes[i], expectedType, argNode, allowConstantConversion: true))
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
        int SelfOffset(FunctionSymbol o) => ReceiverOffsetOf(o, context);

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

            // §12.6.4.6: if the argument's natural type exactly matches one parameter
            // type but not the other, the matching type is strictly better. This is
            // what makes f(byte)/f(int) called with an int literal pick f(int) — the
            // argument type IS int, so identity wins over the constant conversion to
            // byte (#1464, measured C# verdict Case 4).
            var argType = context.ArgTypes[i];
            bool aMatchesExact = paramTypeA.Equals(argType);
            bool bMatchesExact = paramTypeB.Equals(argType);
            if (aMatchesExact && !bMatchesExact)
            { hasStrictlyBetter = true; continue; }
            if (bMatchesExact && !aMatchesExact)
            { return false; }

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
            // §12.6.4.7: when the lattice is neutral, signed beats unsigned (#1464,
            // measured C# verdict Case 2).
            else if (IsSignedBeatsUnsigned(paramTypeA, paramTypeB))
            {
                hasStrictlyBetter = true;
            }
            else if (IsSignedBeatsUnsigned(paramTypeB, paramTypeA))
            {
                return false;
            }
            // Both assignable or neither, and structurally equal: no preference here.
        }

        return hasStrictlyBetter;
    }

    /// <summary>
    /// C# §12.6.4.7: a signed integer type is a better conversion target than an unsigned
    /// integer type of the same or LARGER width. The pairs are: int8 beats uint8/uint16/uint32/uint64;
    /// int16 beats uint16/uint32/uint64; int32 beats uint32/uint64; int64 beats uint64.
    /// Only applies when the two types are otherwise lattice-incomparable (#1464) — which is
    /// also why no width check is needed here: an unsigned type NARROWER than the signed one
    /// converts implicitly into it, so the lattice comparison above decides those pairs before
    /// this rule is consulted, leaving exactly the enumerated table reachable. Measured against
    /// csc (2026-08-21): f(sbyte)/f(ulong) with f(100) picks sbyte, f(short)/f(ulong) picks
    /// short — pinned by overload_constant_conv_sbyte_uint64.
    /// </summary>
    private static bool IsSignedBeatsUnsigned(SemanticType signed, SemanticType unsigned)
    {
        var signedInfo = Registry.PrimitiveCatalog.GetPrimitiveInfo(signed);
        var unsignedInfo = Registry.PrimitiveCatalog.GetPrimitiveInfo(unsigned);
        if (signedInfo == null || unsignedInfo == null)
            return false;
        return signedInfo.Kind == Registry.PrimitiveCatalog.NumericKind.SignedInteger
            && unsignedInfo.Kind == Registry.PrimitiveCatalog.NumericKind.UnsignedInteger;
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
    /// (SPY0337); a type constructor reference is pinned to a signature or refused (SPY0342, or
    /// SPY0346 for a type with no construction — #1182, #1248, #1250); and an overload set whose
    /// candidates take different numbers of arguments is resolved from the target type or rejected
    /// (SPY0336). Everything else keeps the type it already had.</para>
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

        var resultType = type is FunctionType referencedFunctionType
            ? CheckReferencedCallableOverloads(reference, referencedFunctionType)
            : type;

        // #1638: record an eta-expanded lambda lowering for builtin function names used as values.
        // The emitter needs this to generate a typed lambda instead of a bare method group, which
        // breaks on struct boxing, generic inference, CS0121 ambiguity, and params/optional elision.
        if (resultType is FunctionType resolvedFt)
            TryRecordCallableReferenceLowering(reference, resolvedFt);

        return resultType;
    }

    /// <summary>
    /// Records a <see cref="CallableReferenceLowering"/> when the reference names a builtin function
    /// that should emit as an eta-expanded lambda instead of a bare method group (#1638). Only fires
    /// for identifiers that resolve to a BuiltinRegistry function or type-builtin function — user
    /// functions emit through CodeGenInfo/symbol resolution and need no lowering record.
    /// </summary>
    private void TryRecordCallableReferenceLowering(Expression reference, FunctionType ft)
    {
        if (reference is not Identifier id)
            return;

        // bytes is excluded: it emits as the struct type, not via Builtins.Bytes (#1347).
        if (id.Name == Shared.BuiltinNames.Bytes)
            return;

        var resolvedSymbol = _semanticInfo.GetIdentifierSymbol(id);

        // A VariableSymbol shadows the builtin — skip.
        if (resolvedSymbol is VariableSymbol)
            return;

        // A user-declared FunctionSymbol shadows the builtin — skip. Only the builtin registry's
        // own FunctionSymbol (identity check) gets the lowering. User functions emit through their
        // own CodeGenInfo path, and at type-checking time CodeGenInfo is null for ALL symbols.
        if (resolvedSymbol is FunctionSymbol fs)
        {
            var builtinFs = _symbolTable.BuiltinRegistry.GetFunction(id.Name);
            if (builtinFs == null || !ReferenceEquals(fs, builtinFs))
                return;
        }
        else if (resolvedSymbol is TypeSymbol)
        {
            if (_symbolTable.BuiltinRegistry.GetFunction(id.Name) == null)
                return;
        }
        else
        {
            return;
        }

        var qualifiedName = "Sharpy.Builtins." +
            Shared.NameCasing.ResolveMethod(id.Name, id.IsNameBacktickEscaped);

        _semanticInfo.SetCallableReferenceLowering(reference, new CallableReferenceLowering(
            qualifiedName, ft.ParameterTypes, ft.ReturnType));
    }

    /// <summary>
    /// The value-position rule for a bare builtin type-constructor reference (<c>f = int</c>,
    /// <c>f = dict</c>) — Sharpy's method group (#1182). Two tiers, in order:
    ///
    /// <list type="number">
    /// <item><description>An expected function type supplies a signature: the reference binds that
    /// signature and records a <see cref="ConstructorReferenceLowering"/> for codegen.</description></item>
    /// <item><description>Anything else has no signature and no way to acquire one: SPY0342.</description></item>
    /// </list>
    ///
    /// <para>There used to be a middle tier: a binding with no signature anywhere became a call-only
    /// ALIAS carrying <see cref="ConstructorReferenceType"/>, resolved at each call site. It is
    /// retired (#1248). It was untyped by design — it had no runtime representation, emitted no C#,
    /// and was resolved where it was READ rather than where it was written, which is a compile-time
    /// macro impersonating a value; with overloaded constructors, which signature was meant was
    /// unknowable until each call. C# has no constructor values at all, and Java's <c>Point::new</c>
    /// is legal only in target-typed positions — which is tier 1. NO BINDING CARRIES THE CARRIER
    /// ANY MORE; a callee typed as one is a checker bug.</para>
    ///
    /// <para>Ahead of both, a name that resolves to a type with no construction at all — a
    /// user-declared interface, enum, union, delegate or abstract class — is refused outright
    /// (SPY0346, #1250). That is not tier 2 with a different message: tier 2 means this position
    /// supplies no signature to select among the ones the type offers, which presumes it offers
    /// some.</para>
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
        // A parameter typed as CLR System.Type asks for a TYPE TOKEN, not a callable:
        // `assert_raises(SomeError)` passes the type itself and constructs nothing. So this position
        // does not fail to supply a signature — it supplies one explicitly, and it is not a
        // constructor signature. Every non-value exclusion this rule already carries is a position
        // where a type NAME legitimately means the type (a type test's argument, a static-member
        // receiver, #1170); this is the same kind, reached through a parameter type rather than
        // through syntax.
        //
        // Without it, a bare exception name in an assert_raises drew SPY0342 — but only for types
        // with SEVERAL constructors, because a single candidate pinned by accident and went silent.
        // That made every multi-constructor exception type unusable with assert_raises while the
        // single-constructor ones passed, which is why it survived #1248's own fixture sweep (#1282).
        if (_expectedType is { } expectedParameterType && IsSystemTypeParameter(expectedParameterType))
            return null;

        var classification = ClassifyConstructorReference(reference, type);

        if (classification.Reference is not { } constructorReference)
        {
            if (classification.NonConstructible is { } nonConstructible)
                return RefuseNonConstructibleReference(reference, nonConstructible);

            return classification.UnfamiliedBuiltin is { } unfamiliedBuiltin
                ? RefuseUnfamiliedBuiltinReference(reference, unfamiliedBuiltin)
                : null;
        }

        var isValueUse = IsConstructorReferenceValueUse(reference);

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
                return RefuseUnpinnedCallArgument(reference, constructorReference, target);

            AddError(
                $"{ConstructorReferenceSubject(constructorReference)} has no constructor signature matching "
                + $"'{target.GetDisplayName()}'. {ConstructorReferenceShapes(constructorReference, target.ReturnType)}",
                reference.LineStart, reference.ColumnStart,
                code: DiagnosticCodes.Semantic.UnpinnedConstructorReference,
                span: reference.Span);
            return SemanticType.Unknown;
        }

        // A user class in a direct call argument binds its single constructor shape, so
        // `map(Point, xs)` and `sorted(xs, key=Point)` work exactly as `map(int, xs)` does (#1211).
        // Runs BEFORE the value-use guard because it must cover the bare NAME too, which that guard
        // sends back to the position's existing typing — and for a user class that typing is a bare
        // type name in an argument slot, which reached codegen as `Point` where a delegate was
        // expected (CS0119 behind SPY0908). The builtin families have a legacy synthesized signature
        // for this position; a user class had nothing, so this is the arm that gives it one.
        //
        // Only an UNAMBIGUOUS shape binds. Several constructor overloads supply no more of an answer
        // here than an overload set does, so they fall through to the deliberate refusal below rather
        // than silently picking the first. The other non-value positions are excluded exactly as
        // IsConstructorReferenceValueUse excludes them — a type test's type argument
        // (`isinstance(x, Point)`) is a direct call argument too, and re-typing it is the #1170
        // over-fire this whole rule is built to avoid.
        if (constructorReference.Family == ConstructorReferenceFamily.UserType
            && IsConstructorReferenceCallArgument(reference)
            && SingleUserTypeShapeOf(constructorReference) is { } argumentShape)
        {
            _semanticInfo.SetConstructorReferenceLowering(reference,
                new ConstructorReferenceLowering(
                    ConstructorReferenceFamily.UserType, constructorReference.Name,
                    argumentShape.ReturnType, argumentShape.ParameterTypes.Count));
            return argumentShape;
        }

        // A type alias in call-argument position (map(bint, xs)) classifies through its target
        // but the codegen fallback for bare TypeSymbol names does not resolve TypeAliasSymbol,
        // so the lowering must be materialized here (#1588).
        if (!isValueUse && constructorReference.Family != ConstructorReferenceFamily.UserType
            && reference is Identifier aliasRef
            && LookupBySpelling(aliasRef).Symbol is TypeAliasSymbol)
        {
            _semanticInfo.SetConstructorReferenceLowering(reference,
                new ConstructorReferenceLowering(
                    constructorReference.Family, constructorReference.Name,
                    SemanticType.Unknown, 0));
        }

        if (RefuseConstructorReferenceInNonCallableArgument(reference, constructorReference) is { } notCallable)
            return notCallable;

        if (!isValueUse)
            return RefuseUnpinnedCallArgument(reference, constructorReference, target: null);

        // Tier 2. Nothing here says which signature was meant, and unlike before there is no third
        // option: the call-only alias is retired (#1248).
        AddError(
            $"cannot infer a single callable signature for {ConstructorReferenceSubject(constructorReference)}: "
            + $"{ConstructorReferenceAmbiguityReason(constructorReference)}, and this position supplies "
            + "no signature to select one. Annotate the target with a function type "
            + $"({ConstructorReferenceAnnotationExample(constructorReference)}), call the type directly, "
            + "or wrap the construction in a lambda.",
            reference.LineStart, reference.ColumnStart,
            code: DiagnosticCodes.Semantic.UnpinnedConstructorReference,
            span: reference.Span);
        return SemanticType.Unknown;
    }

    /// <summary>
    /// Refuses a constructor reference in a direct call argument whose parameter type cannot hold a
    /// callable AT ALL (#1490), or returns null to leave the position the typing it has today.
    ///
    /// <para>The sibling of <see cref="RefuseUnfamiliedBuiltinReference"/>'s widened gate, for the
    /// family that HAS a signature. <c>takes(int)</c> for <c>def takes(t: object)</c> bound the
    /// synthesized conversion overload set, passed the argument check (everything converts to
    /// <c>object</c>) and reached codegen as a METHOD GROUP — CS1503 behind SPY0908. <c>list</c> in
    /// the same slot reached codegen as an open generic type name — CS0305. Neither is refusable by
    /// the unfamilied gate, because both families are exactly the ones whose fallback signature is
    /// what makes <c>map(int, xs)</c> work.</para>
    ///
    /// <para><b>The rule is the PARAMETER, not the reference.</b> A constructor reference is a
    /// callable value, so the only positions that can consume one are the positions that can hold a
    /// callable: a function type (including a CLR <c>Func</c>/<c>Action</c>, which
    /// <c>ClrTypeBridge</c> maps to <see cref="FunctionType"/>), a declared delegate, or a
    /// <c>System.Type</c> parameter, which asks for the type token instead and is a special form's
    /// marking (see <see cref="CheckConstructorReference"/>). Everything else is refused.</para>
    ///
    /// <para>Silence where the parameter is UNKNOWN is deliberate and is what keeps the #1170
    /// positions working: a parameter this checker cannot see must keep the typing it has today
    /// rather than acquire a refusal. A type parameter is the same case — <c>map(int, xs)</c>'s
    /// parameter is <c>(T) -&gt; R</c>, a function type, and passes on its own merits.</para>
    ///
    /// <para><b><c>_expectedType</c> alone is not the parameter type</b>, and reading it as one is
    /// how this rule first went wrong. The argument loop overwrites it only when a parameter type is
    /// in hand, so in <c>d: defaultdict[str, list[int]] = defaultdict(list)</c> and
    /// <c>zs: list[str] = sorted(xs, key=str)</c> it still holds the ASSIGNMENT's declared type
    /// while the argument is checked — and both shipped fixtures went red, refused against a
    /// parameter they were never passed to. <c>_parameterTypedArgument</c> is what makes the read
    /// sound: it names the one argument the current <c>_expectedType</c> belongs to.</para>
    /// </summary>
    private SemanticType? RefuseConstructorReferenceInNonCallableArgument(
        Expression reference, ConstructorReferenceType constructorReference)
    {
        if (!IsConstructorReferenceCallArgument(reference)
            || !ReferenceEquals(_parameterTypedArgument, UnwrapParenthesized(reference))
            || _expectedType is not { } parameterType
            || CanHoldACallable(parameterType))
        {
            return null;
        }

        AddError(
            $"{ConstructorReferenceSubject(constructorReference)} names a type, and this argument's "
            + $"parameter type '{parameterType.GetDisplayName()}' is not a function type, so it "
            + "cannot hold a constructor reference. Construct the value "
            + $"('{constructorReference.Name}(...)'), or wrap the construction in a lambda "
            + $"('lambda: {constructorReference.Name}()').",
            reference.LineStart, reference.ColumnStart,
            code: DiagnosticCodes.Semantic.UnpinnedConstructorReference,
            span: reference.Span);
        return SemanticType.Unknown;
    }

    /// <summary>
    /// Whether a parameter type can hold a callable value — the predicate
    /// <see cref="RefuseConstructorReferenceInNonCallableArgument"/> rests on. Conservative by
    /// construction: anything this checker cannot positively classify answers TRUE, so an
    /// unrecognised shape keeps the typing it has today instead of acquiring a false refusal at an
    /// interop seam.
    /// </summary>
    private static bool CanHoldACallable(SemanticType parameterType) => parameterType switch
    {
        FunctionType or GenericFunctionType or TypeParameterType or UnknownType => true,
        NullableType nullable => CanHoldACallable(nullable.UnderlyingType),
        OptionalType optional => CanHoldACallable(optional.UnderlyingType),
        UserDefinedType { Symbol.TypeKind: TypeKind.Delegate } => true,
        _ => IsSystemTypeParameter(parameterType)
    };

    /// <summary>
    /// Refuses a constructor reference in a direct call argument that could not be pinned (#1249),
    /// or returns null to leave the position the typing it has today.
    ///
    /// <para>Only the <c>UserType</c> family refuses. The <c>Conversion</c> and <c>Collection</c>
    /// families keep the legacy fallback deliberately: their fallback is a synthesized signature
    /// that WORKS, and it is what makes <c>map(int, xs)</c> and <c>sorted(xs, key=str)</c> compile.
    /// A user class had no such fallback — the bare name reached codegen in an argument slot and
    /// produced CS0119 behind SPY0908 — so there is nothing to preserve and everything to refuse.
    /// </para>
    ///
    /// <para>Gated on <see cref="IsConstructorReferenceCallArgument"/>, which repeats every
    /// non-value exclusion verbatim, so a type test's type argument and a static-member receiver are
    /// still skipped (#1170).</para>
    ///
    /// <para>Three reasons, and which one applies is decided by what was missing, not by how many
    /// constructors the class declares. Several overloads are NOT by themselves a failure: with a
    /// concrete target, tier 1 selects among them and <c>build(xs, make=Many)</c> pins. They only
    /// fail where no target exists at all, which is this position.</para>
    /// </summary>
    private SemanticType? RefuseUnpinnedCallArgument(
        Expression reference, ConstructorReferenceType constructorReference, FunctionType? target)
    {
        if (constructorReference.Family != ConstructorReferenceFamily.UserType
            || !IsConstructorReferenceCallArgument(reference))
        {
            return null;
        }

        var subject = ConstructorReferenceSubject(constructorReference);
        string reason;
        if (target is { } concreteTarget)
        {
            reason = $"has no constructor signature matching '{concreteTarget.GetDisplayName()}'. "
                + ConstructorReferenceShapes(constructorReference, concreteTarget.ReturnType);
        }
        else if (constructorReference.Symbol.TypeParameters.Count > 0)
        {
            reason = "is generic, and its type arguments cannot be determined from this position: "
                + "a constructor reference takes them from the target, and this argument's parameter "
                + "type supplies none. Annotate the parameter with a concrete function type "
                + $"({ConstructorReferenceAnnotationExample(constructorReference)}), or wrap the "
                + "construction in a lambda.";
        }
        else
        {
            reason = "declares several constructors, and this position supplies no signature to "
                + $"select one. {ConstructorReferenceShapes(constructorReference)}\n"
                + "Annotate the parameter with a concrete function type, or wrap the construction "
                + "in a lambda.";
        }

        AddError($"{subject} {reason}",
            reference.LineStart, reference.ColumnStart,
            code: DiagnosticCodes.Semantic.UnpinnedConstructorReference,
            span: reference.Span);
        return SemanticType.Unknown;
    }

    /// <summary>
    /// What a name denotes for the constructor-reference rule. At most one of the three is set: a
    /// <see cref="Reference"/> the tier rules apply to, a <see cref="NonConstructible"/> type that
    /// has no construction for a reference to denote (#1250), or an
    /// <see cref="UnfamiliedBuiltin"/> — a builtin that constructs but has no family, so no
    /// signature can be pinned (#1272). All null means the name is not this rule's business, and
    /// the caller's remaining rules apply.
    /// </summary>
    private readonly record struct ConstructorReferenceClassification(
        ConstructorReferenceType? Reference,
        NonConstructibleTypeName? NonConstructible,
        string? UnfamiliedBuiltin);

    /// <summary>
    /// A type name with no construction, described for the diagnostics that refuse it.
    /// </summary>
    /// <param name="Name">The type's name, as written.</param>
    /// <param name="Noun">The kind as a bare noun phrase, for "Cannot instantiate {Noun} '{Name}'"
    /// (SPY0280 at a call). "abstract class" rather than "class" keeps that message byte-identical
    /// to the one it has shipped with.</param>
    /// <param name="Description">The kind as a predicate phrase, for "'{Name}' is {Description}"
    /// (SPY0346 at a reference).</param>
    /// <param name="Suggestion">What to write instead. Shared by both diagnostics.</param>
    private readonly record struct NonConstructibleTypeName(
        string Name, string Noun, string Description, string Suggestion);

    /// <summary>
    /// Classifies the name an expression denotes. A read already typed as the carrier denotes
    /// itself; a bare identifier denotes a constructor reference when it resolves to a type symbol
    /// with an emittable construction shape — a builtin of a recognized family, or a user
    /// class/struct (#1211) — and a NON-CONSTRUCTIBLE type when it resolves to a user-declared
    /// interface, enum, union, delegate or abstract class (#1250).
    ///
    /// <para>Shadowing is preserved by construction: <c>_symbolTable.Lookup</c> returns the user
    /// declaration's own symbol, and the registry-identity test is what decides WHICH family rule
    /// applies. A user class named <c>int</c> is therefore its own UserType reference pinning
    /// against its own constructors — never the builtin's conversion overload set.</para>
    ///
    /// <para>A BUILTIN with no family is usually NOT called non-constructible, because usually it
    /// is not: <c>object()</c>, <c>bytes()</c>, <c>decimal()</c> and <c>frozenset()</c> construct
    /// and run. Saying otherwise would send a reader to fix the wrong thing. The iterator and view
    /// types are the exception and DO draw SPY0346 — <c>Iterator&lt;T&gt;</c> is abstract and the
    /// views' only constructor is <c>internal</c> (#1346); that was invisible while they were
    /// registered against <c>typeof(object)</c> placeholders, which construct fine. It is
    /// reported instead as an unfamilied builtin, which SPY0342 refuses for the accurate reason —
    /// no signature is available to pin — rather than letting the bare name reach codegen (#1272).
    /// The family grants that were once "the other half of #1272" have since LANDED for four of the
    /// seven: <c>mk: () -&gt; frozenset[int] = frozenset</c> pins the way <c>list</c> does, and so do
    /// <c>frozendict</c>, <c>object</c> and <c>decimal</c> (504a8eb25; fixture
    /// <c>builtins/constructor_reference_families_1272</c>). <c>bytes</c> has since joined them
    /// with the Conversion family (#1347 — its statics moved to <c>BytesFromhex</c>, so the
    /// <c>Builtins.Bytes</c> overload set no longer shadows the TYPE in a static-member
    /// qualifier). What is still unfamilied is <c>Iterator</c>/the view types, which are
    /// non-constructible and now draw SPY0346 instead (#1346).</para>
    /// </summary>
    private ConstructorReferenceClassification ClassifyConstructorReference(
        Expression reference, SemanticType type)
    {
        if (type is ConstructorReferenceType carrier)
            return new ConstructorReferenceClassification(carrier, null, null);

        // By SPELLING, not by name: an escaped binding that spells a builtin holds the name in the
        // symbol table, and a plain lookup handed it back for the BARE reference — so this returned
        // "not a type reference" and `h: (str) -> int = int` lost its overload pinning in any scope
        // containing a `` `int` `` binding (#1326).
        // A builtins-QUALIFIED spelling denotes exactly what the bare spelling denotes (#1322), so it
        // takes exactly the same tiers — pinned by an expected function type, or refused by SPY0342
        // naming the spelling actually written (#1382). Resolved here rather than in CheckMemberAccess
        // so both spellings share one rule instead of one growing a parallel copy.
        if (reference is MemberAccess qualified
            && BuiltinsQualifiedTypeOf(qualified) is { } qualifiedSymbol)
        {
            return ClassifyResolvedConstructorReference(
                qualifiedSymbol, qualified.Member, DescribeMemberPath(qualified), isBuiltin: true);
        }

        // A user-module-qualified type reference (`lib.Circle`) resolves through the same
        // tiers as bare `Circle` (#1586). The module-export switch has already marked it as a
        // type reference; the resolved UserDefinedType arrives as the `type` parameter (not yet
        // cached in SemanticInfo at this point in the CheckExpression flow).
        if (reference is MemberAccess userQualified
            && _semanticInfo.IsTypeReference(reference)
            && type is UserDefinedType { Symbol: TypeSymbol userTypeSym })
        {
            return ClassifyResolvedConstructorReference(
                userTypeSym, userTypeSym.Name, DescribeMemberPath(userQualified), isBuiltin: false);
        }

        // A user-module-qualified ALIAS of a primitive (`lib.Handle` where lib declares
        // `type Handle = int`) denotes exactly what the bare `Handle` denotes (#1636, R3(ii)):
        // the alias-expansion route below for bare identifiers is taken here for the qualified
        // spelling, so both are pinned by the expected function type. Before this arm the
        // qualified spelling fell through to the synthesized `(__synth_T0) -> int` and was
        // refused with SPY0220 while the bare spelling ran.
        if (reference is MemberAccess aliasQualified
            && TryResolveTypeSymbolFromMemberAccess(aliasQualified, out var qualifiedAlias) is { } aliasTarget
            && qualifiedAlias != null)
        {
            var isAliasBuiltin = _symbolTable.BuiltinRegistry.IsBuiltinSymbol(aliasTarget);
            return ClassifyResolvedConstructorReference(
                aliasTarget, aliasTarget.Name, DescribeMemberPath(aliasQualified), isAliasBuiltin);
        }

        if (reference is not Identifier id)
            return default;

        var lookedUp = LookupBySpelling(id).Symbol;
        TypeSymbol? typeSymbol = lookedUp as TypeSymbol;

        if (typeSymbol == null && lookedUp is TypeAliasSymbol aliasSymbol
            && aliasSymbol.TypeAnnotation != null)
        {
            var expanded = _typeResolver.ResolveTypeAnnotation(aliasSymbol.TypeAnnotation);
            typeSymbol = expanded switch
            {
                UserDefinedType { Symbol: TypeSymbol ts } => ts,
                BuiltinType => _symbolTable.BuiltinRegistry.GetType(aliasSymbol.TypeAnnotation.Name),
                _ => null
            };
        }

        if (typeSymbol == null)
            return default;

        var isBuiltin = ReferenceEquals(typeSymbol, _symbolTable.BuiltinRegistry.GetType(typeSymbol.Name));
        return ClassifyResolvedConstructorReference(typeSymbol, typeSymbol.Name, writtenName: null, isBuiltin);
    }

    /// <summary>
    /// The registry type a builtins-qualified member access denotes, or null when it is not one.
    /// Reads the receiver's already-computed <see cref="ModuleType"/> and defers to the same
    /// <c>TryResolveBuiltinsQualifiedType</c> the callee position uses, so the two positions cannot
    /// disagree about what <c>builtins.X</c> names.
    /// </summary>
    private TypeSymbol? BuiltinsQualifiedTypeOf(MemberAccess memberAccess)
    {
        if (_semanticInfo.GetExpressionType(memberAccess.Object) is not ModuleType moduleType)
            return null;
        // builtins.None is refused by SPY0214 before this point; do not re-resolve it
        // for the constructor-reference tiers or it draws a cascading SPY0342.
        if (memberAccess.Member == BuiltinNames.None)
            return null;
        return TryResolveBuiltinsQualifiedType(
            moduleType.Symbol, memberAccess.Member, memberAccess.IsMemberBacktickEscaped,
            isCalleePosition: false);
    }

    /// <summary>
    /// Classifies a reference whose type symbol is already resolved, shared by the bare and the
    /// builtins-qualified spellings.
    /// </summary>
    /// <param name="name">The name the registry and codegen key on — always the BARE name.</param>
    /// <param name="writtenName">The spelling to name in a diagnostic, or null when it is
    /// <paramref name="name"/>.</param>
    private ConstructorReferenceClassification ClassifyResolvedConstructorReference(
        TypeSymbol typeSymbol, string name, string? writtenName, bool isBuiltin)
    {
        var family = isBuiltin
            ? ConstructorReferenceFamilyOf(typeSymbol)
            : UserConstructorReferenceFamilyOf(typeSymbol);

        if (family is { } resolved)
        {
            return new ConstructorReferenceClassification(
                new ConstructorReferenceType
                {
                    Name = name,
                    WrittenNameOverride = writtenName,
                    Symbol = typeSymbol,
                    Family = resolved
                },
                null, null);
        }

        // Non-constructibility outranks "unfamilied" for a BUILTIN too (#1346). The two refusals
        // answer different questions — SPY0346 says no construction exists to refer to, SPY0342 says
        // one exists but no signature is available to pin it — and until now every builtin got the
        // second regardless. That was accurate while the registry named typeof(object) placeholders,
        // which are constructible; it stopped being accurate the moment Iterator named its real
        // abstract type and the dict views named types whose only constructor is internal.
        var nonConstructible = NonConstructibleTypeNameOf(typeSymbol);
        if (nonConstructible is not null)
            return new ConstructorReferenceClassification(null, nonConstructible, null);

        return isBuiltin
            ? new ConstructorReferenceClassification(null, null, writtenName ?? name)
            : new ConstructorReferenceClassification(null, null, null);
    }

    /// <summary>
    /// Why a user-declared type has no construction for a reference to denote, or null when it has
    /// one (#1250).
    ///
    /// <para>Exactly the kinds <see cref="UserConstructorReferenceFamilyOf"/> declines: an
    /// interface, an enum, a union type name, an abstract class, and a DELEGATE. Sharpy delegates
    /// are named function TYPES that a compatible callable is assigned to
    /// (<c>cb: Cb = log</c>) — <c>delegates.md</c> documents no construction spelling, and C#'s
    /// <c>new Cb(log)</c> has no Sharpy equivalent, so a delegate name is no more a value than an
    /// interface name is.</para>
    ///
    /// <para>A non-abstract class or struct returns null here and never reaches this method in
    /// practice, since it takes the UserType family instead.</para>
    /// <para>Exhaustiveness note: a future <see cref="TypeKind"/> falls to the null arm and is
    /// treated as constructible — extend the switch when a kind is added, or the new kind's
    /// value uses leak SPY0908 the way #1250's did.</para>
    /// </summary>
    /// <summary>
    /// Whether every constructor of the backing CLR type is inaccessible to generated code.
    /// Reflection rather than <see cref="TypeSymbol.Constructors"/> — see the arm that calls this
    /// for why that list cannot answer the question (#1346).
    /// </summary>
    private static bool HasNoAccessibleConstructor(TypeSymbol typeSymbol)
        => typeSymbol.ClrType is { } clrType
            && !clrType.IsAbstract
            // A VALUE TYPE always has an implicit public parameterless constructor, and
            // GetConstructors does not enumerate it — so an empty result means "no DECLARED
            // constructor", not "not constructible". Without this, `tuple` (registered as
            // System.ValueTuple) was refused: measured, it broke three fixtures, and via SPY0280 at
            // CALL sites rather than the SPY0346 this arm is written for, because
            // NonConstructibleTypeNameOf is shared by both.
            && !clrType.IsValueType
            && clrType.GetConstructors(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).Length == 0;

    private static NonConstructibleTypeName? NonConstructibleTypeNameOf(TypeSymbol typeSymbol)
    {
        var name = typeSymbol.Name;
        return typeSymbol.TypeKind switch
        {
            TypeKind.Interface => new NonConstructibleTypeName(name, "interface", "an interface and has no constructor",
                "Name a concrete implementing type instead, or wrap the construction in a lambda."),
            TypeKind.Enum => new NonConstructibleTypeName(name, "enum", "an enum and has no constructor",
                $"A member is the value you want: '{name}.SOME_MEMBER'."),
            TypeKind.Union => new NonConstructibleTypeName(name, "union", "a union and has no constructor",
                $"Construct one of its variants instead: '{name}.SomeCase(...)'."),
            TypeKind.Delegate => new NonConstructibleTypeName(name, "delegate type", "a delegate type and has no constructor",
                $"Assign a compatible function or lambda to it instead: 'cb: {name} = handler'."),
            TypeKind.Class or TypeKind.Struct when typeSymbol.IsAbstract =>
                new NonConstructibleTypeName(name, "abstract class", "abstract and has no constructor",
                    "Name a concrete subclass instead, or wrap the construction in a lambda."),
            // A CLR-backed type whose constructors are all inaccessible to generated code. The dict
            // views are the case: `internal DictKeyView(Dictionary<K,V>.KeyCollection)` is the only
            // one, and InternalsVisibleTo covers the test assemblies, never user code — so the NAME
            // denotes no construction and `d.keys()` is how you obtain one (#1346).
            //
            // Asks the CLR TYPE, not TypeSymbol.Constructors, and that distinction is the whole fix.
            // `Constructors` means two different things depending on which path built the symbol:
            // RegisterType fills it from ClrConstructorSurface (public instance only), while
            // LoadBuiltinTypes inserts discovery-built symbols straight into the table and fills it
            // NOWHERE. So an empty list means "no public constructor" for a registered type and
            // "never populated" for a discovered one. Keying on it refused `ValueError` — which
            // declares two public constructors and is reached by the discovery path — measured, and
            // the reason this arm reads reflection instead.
            // ASYMMETRY, deliberate: the PREDICATE is general (any CLR-backed type with no accessible
            // constructor) but the WORDING names the dict views, because they are the only types
            // that reach it today. The predicate is the half that must not over-fire; the phrase
            // only has to be true. If the predicate ever admits something else, this wording needs
            // revisiting — it is specific by choice, not by accident, and leaving that unsaid would
            // make it one more field whose meaning depends on how you got here (#1473).
            TypeKind.Class or TypeKind.Struct when HasNoAccessibleConstructor(typeSymbol) =>
                new NonConstructibleTypeName(name, "type", "obtained from the API that returns it",
                    $"Call the API that produces one — for a dict view, 'd.keys()', 'd.values()' or "
                    + $"'d.items()' — rather than naming '{name}'."),
            _ => null,
        };
    }

    /// <summary>
    /// The message refusing a direct construction of a type that has no construction (SPY0280), or
    /// null when the type can be constructed (#1271).
    ///
    /// <para>Reads the same <see cref="NonConstructibleTypeNameOf"/> authority the value-position
    /// refusal does, so a kind cannot be refused at a reference and silently accepted at a call.
    /// Before this, the check tested <c>IsAbstract</c> directly, which only abstract classes and
    /// unions carry — an interface, an enum and a delegate reached codegen and produced CS1955
    /// behind SPY0908.</para>
    ///
    /// <para>A union is <c>IsAbstract</c> too and was therefore reported as an abstract class. It
    /// is now named as a union; the abstract-class wording itself is unchanged.</para>
    /// </summary>
    private static string? CannotInstantiateMessageOf(TypeSymbol typeSymbol)
        => NonConstructibleTypeNameOf(typeSymbol) is { } nonConstructible
            ? $"Cannot instantiate {nonConstructible.Noun} '{nonConstructible.Name}'. "
                + nonConstructible.Suggestion
            : null;

    /// <summary>
    /// Refuses a non-constructible type name used as a value (#1250), or returns null to leave the
    /// position the typing it has today.
    ///
    /// <para>Gated on the positions where the name is being used AS a value, plus the direct
    /// call-argument position — the same pair #1249's residue uses. Both leak identically today
    /// (<c>f = IShape</c> and <c>map(IShape, xs)</c> each reach codegen as a bare type name), and
    /// <see cref="IsConstructorReferenceCallArgument"/> repeats every other exclusion verbatim, so
    /// a type test's type argument, a static-member receiver, an index argument and a recorded type
    /// reference are all still skipped. Those are the #1170 over-fire positions and they keep the
    /// typing they have today.</para>
    ///
    /// <para>Minus one position neither predicate knows about: an ENUM name is a legitimate iterable
    /// (<c>for c in Color</c>), and that reads as a value use to both. See
    /// <c>_currentIterationSource</c>.</para>
    /// </summary>
    private SemanticType? RefuseNonConstructibleReference(
        Expression reference, NonConstructibleTypeName nonConstructible)
    {
        if (IsCurrentIterationSource(reference))
            return null;

        if (!IsConstructorReferenceValueUse(reference) && !IsConstructorReferenceCallArgument(reference))
            return null;

        AddError(
            $"'{nonConstructible.Name}' is {nonConstructible.Description}, "
            + $"so its name is not a value. {nonConstructible.Suggestion}",
            reference.LineStart, reference.ColumnStart,
            code: DiagnosticCodes.Semantic.NonConstructibleTypeReference,
            span: reference.Span);
        return SemanticType.Unknown;
    }

    /// <summary>
    /// Refuses a reference to a builtin type that constructs but has no constructor-reference
    /// family — <c>object</c>, <c>decimal</c>, <c>frozenset</c>, <c>frozendict</c>,
    /// <c>Iterator</c>, the view types (#1272; <c>bytes</c> left this list when it gained the
    /// Conversion family, #1347) — or returns null to leave the position the typing it
    /// has today.
    ///
    /// <para>SPY0342 rather than SPY0346, because the reason is that no signature is available to
    /// pin, not that the type cannot be constructed: every one of these constructs. Without this the
    /// name falls through every rule and reaches codegen bare, so the rule's headline invariant —
    /// every unpinned reference draws SPY0342 — would be true of user classes and builtin families
    /// and quietly false of seven builtin types.</para>
    ///
    /// <para><b>The gate rule, for both refusals: refuse where the position leaks, preserve where it
    /// works.</b> This gate is now the SAME as <see cref="RefuseNonConstructibleReference"/>'s —
    /// value uses plus the direct call-argument position — because the call-argument position was
    /// measured to leak for this family too: <c>takes(ValueError)</c> for
    /// <c>def takes(t: object)</c> produced CS0119 behind SPY0908, and so did <c>map(bytes, xs)</c>
    /// (#1490, #1347). #1272 is CLOSED — do not read it here as an open work item.</para>
    ///
    /// <para><b>Why widening is safe now, and what makes it stay safe.</b> The position that WORKS
    /// is <c>with assert_raises(ValueError):</c>, a special form whose argument the emitter lowers
    /// to a TYPE ARGUMENT of the framework-neutral catch rather than to a value. Widening this gate
    /// broke ten shipped unittest tests once (the #1170 over-fire) — but that was BEFORE #1282 gave
    /// <see cref="CheckConstructorReference"/> its <see cref="IsSystemTypeParameter"/> early return.
    /// A special form declares its type-argument position in its own SIGNATURE
    /// (<c>Unittest.AssertRaises(System.Type exceptionType, …)</c>), the checker marks that position
    /// before either refusal runs, and the marking is what this gate rests on — not a name list. So
    /// the rule that distinguishes an ordinary call argument from a special form's type argument is
    /// "the parameter asks for a <c>System.Type</c>", and every future special form gets the same
    /// protection for free by declaring the same parameter type. Mutation-tested: removing that
    /// early return turns <c>unittest/assert_raises_*</c> red (see
    /// <c>builtins/unfamilied_builtin_call_argument_1490</c>).</para>
    ///
    /// <para>The untouched positions were measured working: <c>x: object = 5</c>,
    /// <c>isinstance(x, object)</c>, <c>list[object]</c>, <c>dict[str, object]</c>, and annotated
    /// construction of every one of these types.</para>
    /// </summary>
    private SemanticType? RefuseUnfamiliedBuiltinReference(Expression reference, string name)
    {
        if (IsCurrentIterationSource(reference))
            return null;

        if (!IsConstructorReferenceValueUse(reference) && !IsConstructorReferenceCallArgument(reference))
            return null;

        AddError(
            $"cannot infer a callable signature for builtin type '{name}': it has no constructor "
            + "reference form, so no position can supply one. Call it directly "
            + $"('{name}(...)'), or wrap the construction in a lambda "
            + $"('lambda: {name}()').",
            reference.LineStart, reference.ColumnStart,
            code: DiagnosticCodes.Semantic.UnpinnedConstructorReference,
            span: reference.Span);
        return SemanticType.Unknown;
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
    /// <para>Interfaces, enums, unions, delegates and abstract classes return null, and none of
    /// them has a construction for a reference to denote: <see cref="NonConstructibleTypeNameOf"/>
    /// refuses all five in a value position (SPY0346, #1250). That is NOT the same posture
    /// <see cref="ConstructorReferenceFamilyOf"/> takes for <c>object</c> — it
    /// constructs, so it falls through unrefused (#1272). The VIEW types no longer belong in that
    /// list: their only constructor is <c>internal</c>, so they are refused here too (#1346).</para>
    /// </summary>
    private static ConstructorReferenceFamily? UserConstructorReferenceFamilyOf(TypeSymbol typeSymbol)
        => typeSymbol.TypeKind is TypeKind.Class or TypeKind.Struct && !typeSymbol.IsAbstract
            ? ConstructorReferenceFamily.UserType
            : null;

    /// <summary>
    /// Which construction shape a builtin type emits as, or null for a builtin type this rule does
    /// not govern (<c>object</c>, <c>decimal</c>, <c>frozenset</c>, the view and
    /// iterator types): those keep the behavior they have today. <c>bytes</c> IS governed —
    /// the explicit Conversion arm below (#1347).
    ///
    /// <para>Null here means "no family", NOT necessarily "not constructible": <c>object()</c> and
    /// <c>frozenset()</c> (with a target type) construct, and a reference to one falls through to
    /// the position's typing and reaches codegen as a bare type name (#1272). Constructibility is a
    /// SEPARATE question answered by <see cref="NonConstructibleTypeNameOf"/>, which now outranks
    /// this for a builtin — the iterator and view types return null here AND are non-constructible
    /// (#1346).</para>
    /// </summary>
    private ConstructorReferenceFamily? ConstructorReferenceFamilyOf(TypeSymbol typeSymbol)
    {
        // Exactly the set whose reference synthesizes a signature today (SynthesizePrimitiveFunctionType):
        // a primitive backed by a Sharpy.Builtins overload set. `object` and `decimal` join it here
        // because Core now carries their overloads (#1272).
        if (PrimitiveCatalog.IsPrimitive(typeSymbol.Name)
            && _symbolTable.BuiltinRegistry.GetFunctionOverloads(typeSymbol.Name) is { Count: > 0 })
        {
            return ConstructorReferenceFamily.Conversion;
        }

        // bytes gets the Conversion family via an explicit arm, not PrimitiveCatalog membership —
        // PrimitiveCatalog's CSharpName invariant is "a C# keyword" and `bytes` has none (#1347).
        if (typeSymbol.Name == BuiltinNames.Bytes
            && _symbolTable.BuiltinRegistry.GetFunctionOverloads(typeSymbol.Name) is { Count: > 0 })
        {
            return ConstructorReferenceFamily.Conversion;
        }

        return typeSymbol.Name switch
        {
            // frozenset/frozendict construct exactly like set/dict — real zero-arg and copy
            // constructors returning a GenericType — so they take the same family (#1272).
            BuiltinNames.List or BuiltinNames.Dict or BuiltinNames.Set or BuiltinNames.Tuple
                or BuiltinNames.FrozenSet or BuiltinNames.FrozenDict =>
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
    /// Whether the reference is the iterator of the for statement or comprehension for-clause
    /// currently being checked (<c>for c in Color</c>). See <c>_currentIterationSource</c>.
    /// </summary>
    private bool IsCurrentIterationSource(Expression reference)
        => _currentIterationSource != null
            && ReferenceEquals(UnwrapParenthesized(_currentIterationSource), UnwrapParenthesized(reference));

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
    /// test (including tuple elements, since <c>(A, B)</c> denotes <c>tuple[A, B]</c>).
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
                ConstructedTypeFor(constructorReference, target), target.ParameterTypes.Count));
        return true;
    }

    /// <summary>
    /// What a pinned constructor reference's lambda constructs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It stopped being the target's return type once pinning became return-covariant:
    /// <c>f: () -&gt; Animal = Cat</c> emitting <c>() =&gt; new Animal()</c> is a silently wrong VALUE,
    /// not a type error (#1270). When the target names a construction of the referenced class itself,
    /// its return type IS what the lambda constructs, type arguments and all.
    /// </para>
    /// <para>
    /// Across a GENERIC base the referenced class must further be constructed at the arguments the
    /// base clause pinned: <c>f: () -&gt; Base[str] = Pair</c> over <c>class Pair[T](Base[T])</c>
    /// emits <c>() =&gt; new Pair&lt;string&gt;()</c>, from the very substitution the acceptance test
    /// solved for (#1345). Emitting the bare <c>Pair</c> there would not compile, and emitting
    /// <c>Base[str]</c> would be #1270's bug again.
    /// </para>
    /// </remarks>
    private SemanticType ConstructedTypeFor(
        ConstructorReferenceType constructorReference, FunctionType target)
    {
        if (constructorReference.Family != ConstructorReferenceFamily.UserType)
            return target.ReturnType;

        var typeSymbol = constructorReference.Symbol;
        var namesThisClass = target.ReturnType switch
        {
            UserDefinedType returned => ReferenceEquals(returned.Symbol, typeSymbol),
            GenericType generic => generic.GenericDefinition is null
                ? string.Equals(generic.Name, typeSymbol.Name, StringComparison.Ordinal)
                : ReferenceEquals(generic.GenericDefinition, typeSymbol),
            _ => false
        };

        if (namesThisClass)
            return target.ReturnType;

        var substitute = UserTypeConstructionSubstitutionOf(constructorReference, target.ReturnType);
        return substitute is null
            ? new UserDefinedType { Name = constructorReference.Name, Symbol = typeSymbol }
            : substitute(SelfConstructionOf(typeSymbol));
    }

    /// <summary>
    /// Whether one of the builtin's conversion overloads can be bound to <paramref name="target"/>.
    /// The emitted C# is the <c>Sharpy.Builtins.X</c> method group, so this asks the same question
    /// C#'s method-group conversion will ask of it.
    /// </summary>
    private bool ConversionSignatureSatisfies(ConstructorReferenceType constructorReference, FunctionType target)
    {
        var overloads = _symbolTable.BuiltinRegistry.GetFunctionOverloads(constructorReference.Name);
        if (overloads == null)
            return false;

        // The target's return type names the builtin (e.g., `bytes`). The overload's return
        // type may name the CLR struct (e.g., `Bytes`). Normalize the comparison: if the
        // target return type matches the constructor-reference's OWN type, accept any overload
        // whose arity and parameter types fit — the return type is the type being constructed.
        var registryType = _symbolTable.BuiltinRegistry.GetType(constructorReference.Name);
        var normalizedTarget = target;
        if (registryType != null
            && target.ReturnType is UserDefinedType { Symbol: not null } targetUdt
            && ReferenceEquals(targetUdt.Symbol, registryType))
        {
            normalizedTarget = target with { ReturnType = SemanticType.Unknown };
        }

        return overloads.Any(overload =>
            SignatureSatisfiesTarget(ReferenceSignatureOf(overload, t => t), normalizedTarget));
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
        if (UserTypeConstructionSubstitutionOf(constructorReference, target.ReturnType) is not { } substitute)
            return false;

        var candidates = ResolveInitializerConstructorCandidates(constructorReference.Symbol);
        if (candidates.Count == 0)
            return target.ParameterTypes.Count == 0;

        // __init__ returns None; the SHAPE a reference pins to returns the constructed class, so the
        // candidate's return type is replaced before the comparison.
        return candidates.Any(constructor =>
            SignatureSatisfiesTarget(
                ReferenceSignatureOf(constructor, substitute) with { ReturnType = target.ReturnType },
                target));
    }

    /// <summary>
    /// The type-parameter substitution under which <paramref name="returnType"/> is a construction
    /// of this class, or null when it is not one at all (#1211).
    ///
    /// <para>A non-generic class needs no substitution: the return type is the class itself. A
    /// GENERIC class takes its type arguments from the target, exactly as the collection families
    /// do — <c>mk: () -&gt; list[int] = list</c> has always pinned the bare <c>list</c> from the
    /// target's arguments, and <c>mb: (int) -&gt; Box[int] = Box</c> is the same shape one axiom
    /// down. Without this, a generic class's constructors are compared with <c>T</c> unsubstituted
    /// and never match.</para>
    ///
    /// <para>Identity is by symbol. Only when the target carries no generic definition — an
    /// annotation-produced type need not — does the name decide, which is as precise as that type
    /// allows.</para>
    ///
    /// <para>When the target names neither, it may still name a construction of a BASE class, whose
    /// substitution is solved from the base clause rather than read off the target
    /// (<see cref="BaseClauseConstructionSubstitutionOf"/>, #1345).</para>
    /// </summary>
    private Func<SemanticType, SemanticType>? UserTypeConstructionSubstitutionOf(
        ConstructorReferenceType constructorReference, SemanticType returnType)
    {
        var typeSymbol = constructorReference.Symbol;

        if (returnType is UserDefinedType returned
            && (ReferenceEquals(returned.Symbol, typeSymbol)
                // Return covariance (#1270): a reference to Cat satisfies a `() -> Animal` slot,
                // because constructing a Cat produces an Animal. This is the `f: () -> Animal = Cat;
                // if flag: f = Dog` idiom, and it is what `SignatureSatisfiesTarget` — already
                // return-covariant — never got to judge, since the exactness test here ran first.
                // Non-generic on both sides is answered here, by identity: no substitution is needed
                // and none is available. Interfaces count, via InheritsFrom's interface walk — the
                // base-clause arm below walks base CLASSES only.
                || (typeSymbol.TypeParameters.Count == 0
                    && returned.Symbol is { TypeParameters.Count: 0 }
                    && TypeHierarchyService.InheritsFrom(typeSymbol, returned.Symbol, SemanticBinding))))
        {
            return static t => t;
        }

        if (returnType is GenericType generic
            && typeSymbol.TypeParameters.Count == generic.TypeArguments.Count
            && (generic.GenericDefinition is null
                ? string.Equals(generic.Name, typeSymbol.Name, StringComparison.Ordinal)
                : ReferenceEquals(generic.GenericDefinition, typeSymbol)))
        {
            return t => SubstituteTypeParameters(t, typeSymbol.TypeParameters, generic.TypeArguments);
        }

        // The target names a construction of a generic BASE (#1345). Neither arm above can answer it:
        // the first is identity, which cannot carry `[int]` from a base clause, and the second matches
        // the target against the class ITSELF. The substitution has to come from the base clause —
        // `class Box(Base[int])` says `T = int`, `class Pair[T](Base[T])` says `T` is whatever the
        // target's `Base[…]` argument is — which means walking the chain and composing each level's
        // map. That is real inference, which is why #1270 refused rather than guessed; the authority
        // now exists and the restriction can lift.
        return BaseClauseConstructionSubstitutionOf(typeSymbol, returnType);
    }

    /// <summary>
    /// The substitution under which <paramref name="typeSymbol"/>, constructed, produces
    /// <paramref name="returnType"/> — a construction of one of its BASE classes (#1345). Null when
    /// no ancestor matches, or when the match leaves any of the class's own type parameters unsolved.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The class's own parameters enter the walk as themselves, so each ancestor comes back expressed
    /// in terms of them (<c>Base[T]</c> for <c>class Pair[T](Base[T])</c>); unifying that against the
    /// target's arguments (<c>Base[str]</c>) solves for them. Composition across levels comes free
    /// from <see cref="GenericInstantiationWalker.EnumerateBaseChain"/>, so a two-level chain resolves
    /// as readily as a direct base.
    /// </para>
    /// <para>
    /// An unsolved parameter is a REFUSAL, not a guess. #1345's own reasoning is the reason: a wrong
    /// substitution silently constructs the wrong shape, and #1270's emission bug shows how that
    /// presents — a wrong value, not a type error. SPY0342 stays for those.
    /// </para>
    /// <para>
    /// This deliberately does NOT reuse <see cref="GenericTypeInferenceService.UnifyTypes"/>. That
    /// service is call-site inference: it truncates arity mismatches (<c>Math.Min</c>) and solves
    /// nested arguments through supertype walks, which is right where a compatibility check
    /// re-judges the result afterwards. Here the solution IS the result — it constructs the pinned
    /// type — and generic arguments are invariant, so a supertype-walk solution would pin a
    /// construction the target does not accept (measured 2026-08-13: <c>class Holder[T](Base[Sub[T]])</c>
    /// against <c>() -&gt; Base[Inner[int]]</c> where <c>Sub[T](Inner[T])</c> — SPY0342 today, and
    /// rightly, because <c>Base[Sub[int]]</c> is not a <c>Base[Inner[int]]</c>). The strict local
    /// unifier is the decision, not an oversight.
    /// </para>
    /// </remarks>
    private Func<SemanticType, SemanticType>? BaseClauseConstructionSubstitutionOf(
        TypeSymbol typeSymbol, SemanticType returnType)
    {
        TypeSymbol? targetDefinition;
        string targetName;
        IReadOnlyList<SemanticType> targetArguments;
        switch (returnType)
        {
            case GenericType generic:
                targetDefinition = generic.GenericDefinition;
                targetName = generic.Name;
                targetArguments = generic.TypeArguments;
                break;
            case UserDefinedType user:
                targetDefinition = user.Symbol;
                targetName = user.Name;
                targetArguments = Array.Empty<SemanticType>();
                break;
            default:
                return null;
        }

        var ownArguments = typeSymbol.TypeParameters
            .Select(tp => (SemanticType)new TypeParameterType { Name = tp.Name })
            .ToList();

        foreach (var ancestor in GenericInstantiationWalker.EnumerateBaseChain(
            typeSymbol, ownArguments, SemanticBinding, _typeResolver))
        {
            var matches = targetDefinition != null
                ? TypeHierarchyService.IsSameType(ancestor.Definition, targetDefinition)
                : string.Equals(ancestor.Definition.Name, targetName, StringComparison.Ordinal);
            if (!matches)
                continue;

            var solved = new Dictionary<string, SemanticType>(StringComparer.Ordinal);
            if (!UnifyAgainstTarget(ancestor.TypeArguments, targetArguments, solved))
                return null;

            if (typeSymbol.TypeParameters.Any(tp => !solved.ContainsKey(tp.Name)))
                return null;

            return t => TypeSubstitution.Apply(t, solved);
        }

        return null;
    }

    /// <summary>
    /// Solves <paramref name="pattern"/>'s type parameters against <paramref name="concrete"/>,
    /// accumulating into <paramref name="solved"/>. Structural and one-way: only the pattern side may
    /// contain type parameters, a parameter bound twice must agree, and everything else must match.
    /// </summary>
    private static bool UnifyAgainstTarget(
        IReadOnlyList<SemanticType> pattern,
        IReadOnlyList<SemanticType> concrete,
        Dictionary<string, SemanticType> solved)
    {
        if (pattern.Count != concrete.Count)
            return false;

        for (int i = 0; i < pattern.Count; i++)
        {
            if (!UnifyAgainstTarget(pattern[i], concrete[i], solved))
                return false;
        }

        return true;
    }

    private static bool UnifyAgainstTarget(
        SemanticType pattern, SemanticType concrete, Dictionary<string, SemanticType> solved)
    {
        if (pattern is TypeParameterType typeParameter)
        {
            return solved.TryGetValue(typeParameter.Name, out var alreadySolved)
                ? alreadySolved.Equals(concrete)
                : Bind(typeParameter.Name, concrete, solved);
        }

        if (pattern is GenericType patternGeneric && concrete is GenericType concreteGeneric)
        {
            return string.Equals(patternGeneric.Name, concreteGeneric.Name, StringComparison.Ordinal)
                && UnifyAgainstTarget(
                    patternGeneric.TypeArguments, concreteGeneric.TypeArguments, solved);
        }

        return pattern.Equals(concrete);

        static bool Bind(string name, SemanticType type, Dictionary<string, SemanticType> into)
        {
            into[name] = type;
            return true;
        }
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
                + $"'{constructorReference.WrittenName}'"
            : $"builtin type '{constructorReference.WrittenName}'";

    /// <summary>Why a constructor reference has no single signature, for the SPY0342 message.</summary>
    private string ConstructorReferenceAmbiguityReason(ConstructorReferenceType constructorReference)
        => constructorReference.Family switch
        {
            ConstructorReferenceFamily.Conversion => $"'{constructorReference.WrittenName}' names an overload set",
            ConstructorReferenceFamily.UserType =>
                ResolveInitializerConstructorCandidates(constructorReference.Symbol) is { Count: > 1 } overloads
                    ? $"'{constructorReference.WrittenName}' declares {overloads.Count} constructor overloads"
                    : "a constructor reference has no runtime value of its own",
            _ => $"'{constructorReference.WrittenName}' is generic, so its type arguments are unknown here",
        };

    /// <summary>
    /// An annotation that would pin this reference, for the SPY0342 message.
    ///
    /// <para>A GENERIC user class gets the placeholder spelling rather than its own shape: that
    /// shape carries the class's type PARAMETERS, and printing <c>f: (T) -> Box = Box</c> would
    /// suggest something that cannot compile — the parameters have to be substituted by whoever
    /// writes the annotation, which is the whole reason this position could not pin.</para>
    /// </summary>
    private string ConstructorReferenceAnnotationExample(ConstructorReferenceType constructorReference)
        => constructorReference.Family switch
        {
            // The ANNOTATION half of every hint uses the bare Name and the VALUE half uses the written
            // spelling: `builtins.dict` is a legal value but not a legal type annotation, so a hint
            // built entirely from the written spelling would tell the user to write code that does not
            // parse (#1382).
            ConstructorReferenceFamily.Conversion =>
                $"f: (str) -> {constructorReference.Name} = {constructorReference.WrittenName}",
            ConstructorReferenceFamily.UserType when constructorReference.Symbol.TypeParameters.Count == 0 =>
                $"f: {UserTypeShapeOf(constructorReference).GetDisplayName()} = {constructorReference.WrittenName}",
            _ => $"f: () -> {constructorReference.Name}[...] = {constructorReference.WrittenName}",
        };

    /// <summary>The construction shapes a type offers, for the no-matching-signature message.</summary>
    private string ConstructorReferenceShapes(
        ConstructorReferenceType constructorReference, SemanticType? targetReturnType = null)
    {
        if (constructorReference.Family == ConstructorReferenceFamily.Collection)
        {
            // Naming uses the written spelling; the SIGNATURE shapes are type syntax and stay bare.
            return $"'{constructorReference.WrittenName}' can be pinned to its empty constructor "
                + $"(() -> {constructorReference.Name}[...]) or its copy constructor "
                + $"({constructorReference.Name}[...] -> {constructorReference.Name}[...]).";
        }

        if (constructorReference.Family == ConstructorReferenceFamily.UserType)
        {
            var shapes = UserTypeConstructorShapes(constructorReference, targetReturnType)
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
    private IEnumerable<FunctionType> UserTypeConstructorShapes(
        ConstructorReferenceType constructorReference, SemanticType? targetReturnType = null)
    {
        var typeSymbol = constructorReference.Symbol;

        // Report the candidates under the SAME substitution the acceptance test used, so the shapes
        // printed are the ones actually compared against the target (#1270). Listing `(T) -> Box`
        // for a target of `(str) -> Box[int]` names neither what the class offers under that target
        // nor a spelling the reader can act on — the reader has to perform the substitution in their
        // head to see that the real mismatch is str against int.
        var substitute = targetReturnType is null
            ? null
            : UserTypeConstructionSubstitutionOf(constructorReference, targetReturnType);

        // With no substitution the class is shown constructing ITSELF, type parameters and all
        // (`(T) -> Box[T]`). Since #1345 a base clause's arguments ARE carried across the hierarchy,
        // so this is the answer only for targets that legitimately yield none: an unrelated return
        // type, a base whose written arguments cannot be read (#1287 leaves those UNKNOWN rather than
        // guessing), or a match leaving one of the class's own parameters unsolved. It remains
        // strictly better than the arity-less `Box` this printed before, which named no type at all.
        var map = substitute ?? (static t => t);
        var constructed = substitute != null && targetReturnType != null
            ? targetReturnType
            : SelfConstructionOf(typeSymbol);

        var candidates = ResolveInitializerConstructorCandidates(typeSymbol);
        return candidates.Count == 0
            ? new[] { FunctionType.FromParameters(new List<ParameterSymbol>(), constructed) }
            : candidates.Select(constructor =>
                ReferenceSignatureOf(constructor, map) with { ReturnType = constructed });
    }

    /// <summary>
    /// The type a class's constructor returns when nothing substitutes its type parameters: the
    /// class applied to its OWN parameters (<c>Box[T]</c>), or the plain type when it has none.
    /// </summary>
    private static SemanticType SelfConstructionOf(TypeSymbol typeSymbol)
    {
        if (typeSymbol.TypeParameters.Count == 0)
            return new UserDefinedType { Name = typeSymbol.Name, Symbol = typeSymbol };

        return new GenericType
        {
            Name = typeSymbol.Name,
            GenericDefinition = typeSymbol,
            TypeArguments = typeSymbol.TypeParameters
                .Select(tp => (SemanticType)new TypeParameterType { Name = tp.Name })
                .ToList(),
        };
    }

    /// <summary>The first shape a user type offers, for the SPY0342 annotation suggestion.</summary>
    private FunctionType UserTypeShapeOf(ConstructorReferenceType constructorReference)
        => UserTypeConstructorShapes(constructorReference).First();

    /// <summary>
    /// The one shape a user type offers, or null when it offers several or none it can commit to
    /// (#1211). Used only for the direct-call-argument position, which supplies no target type.
    ///
    /// <para>Two ways to have no single shape. Several declared constructors are no more of an
    /// answer here than an overload set is, so they do not bind. And a GENERIC class cannot bind
    /// either: its type arguments come from the target everywhere else, and this position has none,
    /// so the shape would carry <c>T</c> unsubstituted and reach codegen as a generic type name
    /// without arguments (CS0305). Both fall back to the position's existing typing — see #1249 for
    /// the residual SPY0908 that fallback still produces.</para>
    /// </summary>
    private FunctionType? SingleUserTypeShapeOf(ConstructorReferenceType constructorReference)
    {
        if (constructorReference.Symbol.TypeParameters.Count > 0)
            return null;

        var shapes = UserTypeConstructorShapes(constructorReference).Take(2).ToList();
        return shapes.Count == 1 ? shapes[0] : null;
    }

    /// <summary>
    /// Whether the reference is a direct call argument AND is in no OTHER non-value position — the
    /// one relaxation of <see cref="IsConstructorReferenceValueUse"/>. The remaining exclusions are
    /// repeated verbatim rather than approximated: a type test's type argument is a direct call
    /// argument too, and so is a type name recorded elsewhere, and re-typing either is the #1170
    /// over-fire.
    /// </summary>
    private bool IsConstructorReferenceCallArgument(Expression reference)
        => IsDirectCallArgument(reference)
            && !IsCurrentMemberAccessQualifier(reference)
            && !IsTypeTestTypeArgument(reference)
            && !IsCurrentIndexArgument(reference)
            && !_semanticInfo.IsTypeReference(reference);

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
        if (IsCurrentMemberAccessQualifier(reference))
            return referencedType;

        // The TYPE argument of a type test names a type, never a value, so the arity-divergence
        // rule does not apply — the same exemption the SPY0337 value-position rule takes. Without
        // it `isinstance(v, int8)` was refused with SPY0336 (14 conversion overloads of differing
        // arity) while `isinstance(v, int)` passed (#1637 sibling, 2026-08-28).
        if (IsTypeTestTypeArgument(reference))
            return referencedType;

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
            // When the expected type still contains unsolved type parameters (e.g. `(T) -> U` from
            // a generic function's parameter), the target cannot select among arity-divergent
            // overloads yet. Defer the selection: record the candidates and return the expected type
            // so generic inference can proceed with the other arguments. After inference substitutes
            // the type variables, ResolvePendingOverloadSelections re-enters this logic with the
            // now-concrete target (#1589). Only offered inside a call's argument scope — that is the
            // one place inference will come back with a concrete target; anywhere else the open
            // target is final and the reference is judged against it below.
            if (ContainsTypeParameter(target) && _pendingOverloadWatermark >= 0)
            {
                // Probe for a constructor-reference classification so the lowering can be recorded
                // at resolution time (the emitter needs it to emit a method group instead of a
                // bare type name).
                ConstructorReferenceFamily? ctorFamily = null;
                string? ctorName = null;
                if (reference is Identifier ctorId)
                {
                    var ctorTypeSymbol = _symbolTable.BuiltinRegistry.GetType(ctorId.Name);
                    if (ctorTypeSymbol != null)
                    {
                        ctorFamily = ConstructorReferenceFamilyOf(ctorTypeSymbol);
                        ctorName = ctorId.Name;
                    }
                }

                _pendingOverloadSelections.Add(new PendingOverloadSelection(
                    reference, candidates, target,
                    ConstructorFamily: ctorFamily, ConstructorName: ctorName));
                return target;
            }

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
    /// Whether a candidate signature satisfies a partially-concrete target — like
    /// <see cref="SignatureSatisfiesTarget"/> but treats <see cref="TypeParameterType"/> positions in
    /// the target as wildcards (#1589). Used by the deferred overload resolution where the target
    /// still has unsolved type parameters (e.g. <c>(int32) -&gt; TOut</c>): the return-type position
    /// carries <c>TOut</c>, and any concrete candidate return should match.
    /// </summary>
    private static bool SignatureSatisfiesPartialTarget(FunctionType candidate, FunctionType target)
    {
        var required = candidate.ParameterTypes.Count - candidate.OptionalParameterCount;
        if (target.ParameterTypes.Count < required || target.ParameterTypes.Count > candidate.ParameterTypes.Count)
            return false;

        for (var i = 0; i < target.ParameterTypes.Count; i++)
        {
            // Unsolved type parameters in the target are wildcards.
            if (target.ParameterTypes[i] is TypeParameterType)
                continue;
            if (!target.ParameterTypes[i].IsAssignableTo(candidate.ParameterTypes[i]))
                return false;
        }

        // Return type: a type parameter is a wildcard, anything else checks covariance.
        if (target.ReturnType is TypeParameterType or UnknownType)
            return true;
        if (candidate.ReturnType.IsAssignableTo(target.ReturnType))
            return true;
        if (candidate.ReturnType.ClrType != null && candidate.ReturnType.ClrType == target.ReturnType.ClrType)
            return true;
        return false;
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

        if (target.ReturnType is UnknownType)
            return true;
        if (candidate.ReturnType.IsAssignableTo(target.ReturnType))
            return true;
        // CLR type fallback: `bytes` (BuiltinType) and `Bytes` (UserDefinedType from a
        // Builtins overload) name the same CLR type. Without this, the constructor-reference
        // pin for bytes cannot match the overload's return type (#1582).
        if (candidate.ReturnType.ClrType != null && candidate.ReturnType.ClrType == target.ReturnType.ClrType)
            return true;
        return false;
    }

    /// <summary>How to name a callable reference in a diagnostic.</summary>
    private static string DescribeReference(Expression reference) => reference switch
    {
        Identifier id => id.Name,
        MemberAccess memberAccess => memberAccess.Member,
        _ => "reference"
    };

    /// <summary>
    /// Whether the call currently being checked recorded any deferred callable-reference selections
    /// (#1589): the entries at or past its watermark. Entries below it belong to enclosing calls and
    /// are invisible here.
    /// </summary>
    private bool HasPendingOverloadSelections
        => _pendingOverloadWatermark >= 0 && _pendingOverloadSelections.Count > _pendingOverloadWatermark;

    /// <summary>
    /// Infers type-parameter substitutions for the current call from its NON-deferred arguments
    /// (#1589): the formal/actual pairs go through structural unification, then every single-argument
    /// generic formal (<c>Iterable[T]</c>) is enriched with the actual's element type — the enrichment
    /// the deferred-lambda path applies too — because unification cannot see through differing outer
    /// names (<c>IEnumerable[T]</c> against <c>list[int]</c>). A deferred argument is recognisable by
    /// its type: the still-open expected <see cref="FunctionType"/> the deferral returned for it.
    /// </summary>
    /// <param name="formalAt">The callee's formal type at a positional argument index, or null past
    /// the last formal.</param>
    private Dictionary<string, SemanticType> InferSubstitutionsFromArguments(
        Func<int, SemanticType?> formalAt, List<SemanticType> argTypes)
    {
        var formals = new List<SemanticType>();
        var actuals = new List<SemanticType>();
        for (int argIdx = 0; argIdx < argTypes.Count; argIdx++)
        {
            if (formalAt(argIdx) is not { } formalType)
                break;
            if (argTypes[argIdx] is FunctionType argFt && ContainsTypeParameter(argFt))
                continue;
            formals.Add(formalType);
            actuals.Add(argTypes[argIdx]);
        }

        var substitutions = _genericInference.UnifyTypes(formals, actuals)
            ?? new Dictionary<string, SemanticType>(StringComparer.Ordinal);

        for (int i = 0; i < formals.Count; i++)
        {
            if (formals[i] is GenericType { TypeArguments.Count: 1 } iterableFormal
                && iterableFormal.TypeArguments[0] is TypeParameterType elementParam
                && _typeInference.InferIterableElementType(actuals[i]) is { } elementType
                && elementType is not UnknownType)
            {
                substitutions[elementParam.Name] = elementType;
            }
        }

        return substitutions;
    }

    /// <summary>
    /// Re-enters overload selection for the current call's deferred callable references (#1589), with
    /// <paramref name="substitutions"/> — the bindings the call's own inference produced — applied to
    /// each entry's expected type. Positions still open afterwards are wildcards
    /// (<see cref="SignatureSatisfiesPartialTarget"/>), so an unsolved return type does not block a
    /// selection the parameters decide. A unique match binds the reference to the selected signature,
    /// records the constructor-reference lowering the emitter needs, and — when
    /// <paramref name="argTypes"/> is given — replaces the open type at the argument's position so the
    /// call's return-type inference reads the concrete one. Anything else is refused SPY0336 at the
    /// reference. The current call's entries are removed either way: one bounded pass, no retry.
    /// </summary>
    /// <returns>The bindings the selected signatures reveal for type parameters that
    /// <paramref name="substitutions"/> left open (a selected <c>(str) -&gt; bytes</c> against the
    /// target <c>(str) -&gt; U</c> binds <c>U = bytes</c>), or null when there are none.</returns>
    private Dictionary<string, SemanticType>? ResolvePendingOverloadSelections(
        Dictionary<string, SemanticType> substitutions, List<SemanticType>? argTypes)
    {
        if (!HasPendingOverloadSelections)
            return null;

        Dictionary<string, SemanticType>? additionalBindings = null;
        for (int idx = _pendingOverloadWatermark; idx < _pendingOverloadSelections.Count; idx++)
        {
            var pending = _pendingOverloadSelections[idx];
            var concreteTarget = (FunctionType)TypeSubstitution.Apply(pending.ExpectedType, substitutions);
            var matching = pending.Candidates
                .Where(c => SignatureSatisfiesPartialTarget(c.Signature, concreteTarget))
                .ToList();

            if (matching.Count != 1)
            {
                var verdict = matching.Count == 0 ? "none of them accepts" : $"{matching.Count} of them accept";
                RefusePendingOverloadSelection(pending,
                    $"{verdict} the inferred target type '{concreteTarget.GetDisplayName()}', "
                    + "so no single overload can be selected");
                continue;
            }

            var selected = matching[0].Signature;
            _semanticInfo.SetExpressionType(pending.Reference, selected);

            // The emitter needs a method group (Builtins.Bytes), not a bare type name (Sharpy.Bytes) —
            // the lowering TryPinConstructorReference records on the non-deferred path.
            if (pending.ConstructorFamily is { } family && pending.ConstructorName is { } ctorName)
            {
                _semanticInfo.SetConstructorReferenceLowering(pending.Reference,
                    new ConstructorReferenceLowering(
                        family, ctorName, selected.ReturnType, selected.ParameterTypes.Count));
            }

            // By identity, not equality: two deferred arguments of one call can carry structurally
            // equal open targets, and each must replace its own position.
            if (argTypes != null)
            {
                for (int argIdx = 0; argIdx < argTypes.Count; argIdx++)
                {
                    if (ReferenceEquals(argTypes[argIdx], pending.ExpectedType))
                    {
                        argTypes[argIdx] = selected;
                        break;
                    }
                }
            }

            if (concreteTarget.ReturnType is TypeParameterType returnParam
                && selected.ReturnType is not TypeParameterType and not UnknownType)
            {
                (additionalBindings ??= new Dictionary<string, SemanticType>(StringComparer.Ordinal))
                    [returnParam.Name] = selected.ReturnType;
            }

            for (int p = 0; p < concreteTarget.ParameterTypes.Count && p < selected.ParameterTypes.Count; p++)
            {
                if (concreteTarget.ParameterTypes[p] is TypeParameterType paramTypeParam
                    && selected.ParameterTypes[p] is not TypeParameterType and not UnknownType)
                {
                    (additionalBindings ??= new Dictionary<string, SemanticType>(StringComparer.Ordinal))
                        [paramTypeParam.Name] = selected.ParameterTypes[p];
                }
            }
        }

        DiscardPendingOverloadSelections();
        return additionalBindings;
    }

    /// <summary>
    /// Refuses (SPY0336) every deferred selection the current call still holds and removes it. Reached
    /// when the call's check ends without inference having produced a target for them — the callee was
    /// not generic, or its dispatch never inferred — so the reference reads exactly as it did before
    /// deferral existed: an arity-divergent overload set with no target to select one.
    /// </summary>
    private void RefusePendingOverloadSelections()
    {
        if (!HasPendingOverloadSelections)
            return;

        for (int idx = _pendingOverloadWatermark; idx < _pendingOverloadSelections.Count; idx++)
        {
            RefusePendingOverloadSelection(_pendingOverloadSelections[idx],
                "so it cannot be used as a value without a target type to select one");
        }

        DiscardPendingOverloadSelections();
    }

    /// <summary>
    /// Drops the current call's deferred selections without diagnosing them: the call already failed
    /// (inference reported its own error), and a second error on its argument would be noise.
    /// </summary>
    /// <summary>
    /// Drops the deferred selection recorded for one reference node. Used for a TYPE position
    /// (the second argument of <c>isinstance</c>): the operand was checked as an expression first,
    /// and a primitive with arity-divergent conversion overloads (<c>int8</c>, 14 overloads via
    /// <c>IntParse</c>) deferred its selection, which nothing in a type test ever consumes — so it
    /// was refused as SPY0336 "cannot be used as a value" at scope close while <c>int</c>, whose
    /// overloads do not diverge, passed (#1637 sibling, found 2026-08-28).
    /// </summary>
    private void DiscardPendingOverloadSelectionFor(Expression reference)
    {
        if (!HasPendingOverloadSelections)
            return;
        for (int idx = _pendingOverloadSelections.Count - 1; idx >= _pendingOverloadWatermark; idx--)
        {
            if (ReferenceEquals(_pendingOverloadSelections[idx].Reference, reference))
                _pendingOverloadSelections.RemoveAt(idx);
        }
    }

    private void DiscardPendingOverloadSelections()
    {
        if (HasPendingOverloadSelections)
        {
            _pendingOverloadSelections.RemoveRange(
                _pendingOverloadWatermark, _pendingOverloadSelections.Count - _pendingOverloadWatermark);
        }
    }

    private void RefusePendingOverloadSelection(PendingOverloadSelection pending, string verdict)
    {
        var signatures = string.Join("\n  ",
            pending.Candidates.Select(c => $"{c.Symbol.Name}{c.Signature.GetDisplayName()}"));
        AddError(
            $"'{DescribeReference(pending.Reference)}' has {pending.Candidates.Count} overloads taking "
            + $"different numbers of arguments, {verdict}. Candidates:\n  " + signatures,
            pending.Reference.LineStart, pending.Reference.ColumnStart,
            code: DiagnosticCodes.Semantic.AmbiguousCallableReference,
            span: pending.Reference.Span);
        _semanticInfo.SetExpressionType(pending.Reference, SemanticType.Unknown);
    }
}
