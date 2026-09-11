using System.Reflection;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Utilities;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// TypeChecker partial class: THE CLR call-route seam.
///
/// <para>Every route that binds a Sharpy call to a reflected .NET member — instance method, static
/// method, constructor, indexer, extension method — asks its applicability and its betterness
/// question HERE, of one formula that agrees with C# §12.6.4 (#1753, #1798). Before this seam there
/// were four: the instance route decided applicability with no betterness at all (so every overload
/// set with an <c>object</c> sibling became SPY0601 — <c>StringWriter.write("x")</c>,
/// <c>sb.insert(0, "y")</c>, <c>TextWriter.write_line(str)</c> in the stdlib's own sources), the
/// static route carried three ad-hoc tie-breaks the instance route never saw, the constructor route
/// ran two deciders in sequence, and the indexer route asked nothing.</para>
///
/// <para><b>The formula, in order.</b> Bind each written argument to a parameter — positionally, or
/// BY NAME for a keyword argument, on every route (a CLR route used to bail on any keyword, so
/// <c>Vector2(x="a", y="b")</c> reached Roslyn as CS1503). Then applicability: a formal accepts an
/// argument, or it does not, by ONE answer (<see cref="ClrFormalVerdict"/>) that the post-selection
/// argument check gives too — the two disagreeing is what made <c>Socket.receive_from(buf, ref ep)</c>
/// a false SPY0354. Then betterness (<see cref="IsBetterClrBinding"/>): C#'s better-conversion rule
/// over the formals' CLR types, with C#'s own tie-breakers (normal form beats expanded, fewer
/// skipped optionals) folded in so that one predicate decides the whole order. SPY0601 only when no
/// candidate is better than every other.</para>
///
/// <para><b>Applicability is C#'s question; nullability is Sharpy's.</b> A bare <c>None</c> is
/// applicable to every reference-type and <c>Nullable&lt;T&gt;</c> formal exactly as C#'s null
/// literal is, so the candidate set is the set Roslyn would find. Sharpy's stricter rule — a formal
/// .NET declares non-nullable does not take <c>None</c> (SPY0229) — is applied to the SELECTED
/// candidate. Deciding it during applicability instead made the refusal disappear: it knocked
/// <c>Combine(string, string)</c> out of the set, the <c>params string[]</c> sibling won by default,
/// and the spec's own <c>Path.combine(None, "b")</c> example compiled and crashed at runtime.</para>
/// </summary>
internal partial class TypeChecker
{
    // ---------------------------------------------------------------------------------------
    // Formals
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// One CLR formal as this seam sees it: the reflected <see cref="ParameterInfo"/> (for its name,
    /// its declared nullability and its <c>params</c> mark) paired with the CLOSED type an argument
    /// actually faces. The two differ on the extension route, whose formals are closed by the
    /// receiver while the reflected parameters are still open, and for a <c>params</c> tail bound in
    /// expanded form, where each written argument faces the ELEMENT type.
    /// </summary>
    private readonly record struct ClrFormal(
        ParameterInfo? Parameter,
        Type ClrType,
        bool IsParamsElement)
    {
        internal static ClrFormal Of(ParameterInfo parameter)
            => new(parameter, parameter.ParameterType, IsParamsElement: false);

        internal static ClrFormal Of(ParameterInfo parameter, Type closedType)
            => new(parameter, closedType, IsParamsElement: false);

        internal static ClrFormal Element(ParameterInfo parameter, Type closedArrayType)
            => new(parameter, closedArrayType.GetElementType() ?? closedArrayType, IsParamsElement: true);
    }

    /// <summary>
    /// The formal in Sharpy vocabulary, or <c>null</c> when the bridge has no word for it —
    /// <c>object</c>, an interface it cannot spell, a delegate no assignability rule matches. A
    /// caller that gets <c>null</c> asks .NET about <see cref="ClrFormal.ClrType"/> directly rather
    /// than checking against <c>object</c>, which accepts everything.
    /// </summary>
    private SemanticType? MapClrFormal(ClrFormal formal)
    {
        SemanticType mapped;
        if (formal is { Parameter: { } parameter, IsParamsElement: false }
            && parameter.ParameterType == formal.ClrType)
        {
            mapped = _bclGenericMethodBridge.MapParameterType(parameter);
        }
        else
        {
            mapped = _bclGenericMethodBridge.MapClrParameterTypeToSemanticType(formal.ClrType);
            if (formal is { Parameter: { } declaring, IsParamsElement: false }
                && Discovery.ClrDeclaredNullability.DeclaresNullableArgument(declaring))
            {
                mapped = Discovery.ClrDeclaredNullability.Apply(mapped, declaredNullable: true);
            }
        }

        // The declared-nullability wrapper is asked PAST, not through: `IFormatProvider?` maps to
        // `NullableType{UnmappedClrType}`, and the object-likeness that makes a formal unspellable
        // lives on the payload. Reading only the outer type is what let a `str` satisfy
        // `sb.append_line("ok", 42)`'s IFormatProvider formal once #1705 started wrapping it.
        var core = mapped switch
        {
            NullableType nullable => nullable.UnderlyingType,
            OptionalType optional => optional.UnderlyingType,
            var other => other
        };

        if (core is UnknownType || IsObjectType(core))
            return null;

        // A delegate formal maps to a GenericType spelling (Func, Action, Predicate) the bridge
        // cannot match a Sharpy FunctionType against, so it stays unspellable (#1640).
        if (core is GenericType && typeof(Delegate).IsAssignableFrom(formal.ClrType))
            return null;

        return mapped;
    }

    /// <summary>The display this seam names a formal by: its Sharpy spelling when the mapping is
    /// faithful, its CLR name otherwise (an <c>IFormatProvider</c> is named, not called
    /// <c>object</c>).</summary>
    private string ClrFormalDisplay(ClrFormal formal)
        => MapClrFormal(formal) is { } mapped && !IsLossyClrMapping(formal.ClrType, mapped)
            ? mapped.GetDisplayName()
            : Shared.ClrNameHelper.StripArity(formal.ClrType.Name);

    /// <summary>
    /// Whether nothing this seam knows can adjudicate an argument against <paramref name="formal"/>,
    /// whichever description of the formal is used. A <c>ref</c>/<c>out</c> or pointer formal is
    /// unwritable from Sharpy and a different diagnosis; one still naming a type parameter has no
    /// concrete formal at all; <see cref="System.Type"/> is satisfied by a type reference;
    /// <c>object</c> accepts everything; a ref-struct (<c>Span</c>, <c>ReadOnlySpan</c>) is reached by
    /// conversions reflection cannot enumerate.
    ///
    /// <para><b>The op_Implicit arm counts INBOUND conversions only.</b> It used to ask whether the
    /// formal's type declares ANY <c>op_Implicit</c>, and <see cref="System.String"/> declares one —
    /// the OUTBOUND <c>implicit operator ReadOnlySpan&lt;char&gt;(string)</c>. So every <c>string</c>
    /// formal in .NET was "undecidable": <c>Path.combine(None, "b")</c>, <c>Uri(None)</c>, an
    /// <c>int</c> where a <c>string</c> was declared — all compiled, and the ones with a <c>None</c>
    /// crashed at runtime. A conversion is inbound when it PRODUCES the formal's type, and when the
    /// argument's own CLR type is known the question is narrower still: does an inbound conversion
    /// exist FROM THAT type. <c>decimal</c> stays undecidable for an <c>int</c> argument (C# converts
    /// it implicitly) and becomes decidable for a <c>double</c> one (C# does not) — which is what
    /// lets <c>Math.max(1, 2.5)</c> pick <c>Max(double, double)</c> instead of reporting an
    /// ambiguity.</para>
    ///
    /// <para>A delegate formal is undecidable only against an argument that could BE a delegate — a
    /// lambda or a method group, whose conversion is C#'s to decide. Against a <c>str</c> it is
    /// decidable and refuses, which is what makes <c>xs.first_or_default("a")</c> a diagnostic.</para>
    ///
    /// <para>An enum is NOT undecidable: it reaches the call as the bridge's <c>int</c>, which is a
    /// lossy spelling, and .NET decides it through the argument's own CLR type (#1573).</para>
    /// </summary>
    private static bool ClrFormalIsUndecidable(ClrFormal formal, SemanticType argType, Type? argClrType)
    {
        var formalClrType = formal.ClrType;

        if (formalClrType.IsByRef || formalClrType.IsPointer
            || formalClrType.ContainsGenericParameters
            || formalClrType.IsByRefLike
            || formalClrType == typeof(Type) || formalClrType == typeof(object))
        {
            return true;
        }

        if (typeof(Delegate).IsAssignableFrom(formalClrType))
            return argType is FunctionType or GenericFunctionType || argClrType == null;

        return HasInboundImplicitConversion(formalClrType, argClrType);
    }

    /// <summary>
    /// Whether a user-defined implicit conversion can PRODUCE <paramref name="target"/> — from
    /// <paramref name="sourceClrType"/> specifically when the argument's type is known, from anything
    /// when it is not. Both operands are asked, because C# looks for the operator on the source type
    /// and on the target type alike.
    /// </summary>
    private static bool HasInboundImplicitConversion(Type target, Type? sourceClrType)
        => DeclaresInboundImplicitConversion(target, target, sourceClrType)
           || (sourceClrType != null
               && DeclaresInboundImplicitConversion(sourceClrType, target, sourceClrType));

    private static bool DeclaresInboundImplicitConversion(Type declaringType, Type target, Type? sourceClrType)
        => declaringType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Any(m => m.Name == "op_Implicit"
                      && m.ReturnType == target
                      && (sourceClrType == null
                          || (m.GetParameters() is { Length: 1 } ps
                              && ps[0].ParameterType.IsAssignableFrom(sourceClrType))));

    /// <summary>How a formal answered one argument.</summary>
    private enum ClrAcceptance
    {
        /// <summary>The formal takes the argument, or nothing here can say it does not.</summary>
        Accepted,
        /// <summary>Refused, and the formal's Sharpy spelling states the refusal — a diagnostic may
        /// name it.</summary>
        RefusedByVocabulary,
        /// <summary>Refused by .NET on the argument's own CLR type while the Sharpy spelling
        /// accepted (the lossy-mapping arm, #1573). Naming the spelling would read "cannot pass
        /// 'int32' to a parameter of type 'int32'", so the refusal stays unexplained.</summary>
        RefusedByClr
    }

    private readonly record struct ClrArgumentVerdict(ClrAcceptance Kind, string ExpectedDisplay)
    {
        internal bool Accepted => Kind == ClrAcceptance.Accepted;
    }

    private static readonly ClrArgumentVerdict ClrAccepted = new(ClrAcceptance.Accepted, string.Empty);

    /// <summary>
    /// THE acceptance answer. Applicability filtering and the post-selection argument check both read
    /// this and nothing else, so a formal cannot be applicable and then rejected, or rejected and
    /// then quietly bound.
    /// </summary>
    /// <param name="literalAdaptation">
    /// Whether a value SHAPE may rescue a type that does not convert — an unsuffixed float literal
    /// into a <c>float32</c> formal, an in-range integer constant into a narrow one. Sharpy's store
    /// seam allows both; C# allows only the second. Applicability is therefore asked TWICE (see
    /// <see cref="DecideClrCall"/>): once with the shapes off, so that the candidate set is the one
    /// Roslyn would find, and only if that set is empty with them on, so that
    /// <c>Vector2(1.0, 2.0)</c> — where the adaptation is the only reading — still binds.
    /// </param>
    private ClrArgumentVerdict ClrFormalVerdict(
        ClrFormal formal, SemanticType argType, Expression? node, bool literalAdaptation = true)
    {
        // A bare `None` is C#'s null literal: applicable to every reference-type and Nullable<T>
        // formal and to nothing else. Asked FIRST, because a `None` carries no semantic type of its
        // own and would otherwise fall into the unsettled-type arm below and be accepted by every
        // formal, value types included. Sharpy's stricter DECLARED-nullability rule is a
        // post-selection check (ClrFormalRefusesNone) so that a refusal cannot be hidden by a
        // sibling overload winning the set.
        if (node != null && UnwrapParenthesized(node) is NoneLiteral)
        {
            if (!formal.ClrType.IsValueType)
                return ClrAccepted;

            return Nullable.GetUnderlyingType(formal.ClrType) != null
                ? ClrAccepted
                : new ClrArgumentVerdict(ClrAcceptance.RefusedByVocabulary, ClrFormalDisplay(formal));
        }

        // An argument whose own type is not settled carries no fact to adjudicate on: an error
        // recovery, a still-open type parameter, a lambda still being inferred.
        if (argType is UnknownType or TypeParameterType
            || (argType is FunctionType argFn && argFn.HasUnresolvedTypes()))
        {
            return ClrAccepted;
        }

        var argClrType = TryGetClrType(argType);

        if (ClrFormalIsUndecidable(formal, argType, argClrType))
            return ClrAccepted;

        // An argument the bridge collapsed to `object` is ITS OWN degradation, not a fact about the
        // value — the same reason an `object` receiver is exempt from the extension proof (#1206 D2).
        // Refusing on it is how a permissive channel becomes a false error: the stdlib's
        // `String.trim_end(Path.directory_separator_char, ...)` passes char statics that the member
        // resolver declines to type.
        if (argType is UnmappedClrType)
            return ClrAccepted;

        // The char row (#1402) OWNS a CLR `char` formal: the one conversion Sharpy performs into it is
        // a single-character str literal, and RecordClrCharArguments states both the acceptance and
        // the refusal in the vocabulary of that rule. The bridge's mapping is lossy here (char maps to
        // str), so leaving the formal to the general check would refuse every str — including the
        // one-character literal that DOES convert — and the candidate would never be selected at all.
        if (formal.ClrType == typeof(char) && argType.Equals(SemanticType.Str))
            return ClrAccepted;

        var mapped = MapClrFormal(formal);
        if (mapped == null)
        {
            // The bridge collapsed the formal to `object`, which is a degradation and not a fact —
            // checking against it accepts everything, which is how `sb.append_line("ok", 42)` stayed
            // silent (its arity-2 overload's first parameter is IFormatProvider) and why
            // `Byte.to_string(str)` looked ambiguous. .NET is authoritative on the raw type whenever
            // the argument has a CLR type of its own.
            if (argClrType == null || formal.ClrType.IsAssignableFrom(argClrType))
                return ClrAccepted;

            return new ClrArgumentVerdict(
                ClrAcceptance.RefusedByVocabulary, Shared.ClrNameHelper.StripArity(formal.ClrType.Name));
        }

        var clrAccepts = argClrType != null && formal.ClrType.IsAssignableFrom(argClrType);

        // The float32/decimal literal narrowings are value SHAPES the store seam admits at every
        // position (#1688 Decision 6) and that `allowConstantConversion` does not gate. C# has no
        // counterpart for either, so the strict phase refuses them by name.
        if (!literalAdaptation && node != null
            && (ImplicitConversions.IsFloat32LiteralNarrowing(mapped, argType, UnwrapParenthesized(node))
                || ImplicitConversions.IsDecimalLiteralNarrowing(mapped, argType, UnwrapParenthesized(node))))
        {
            return new ClrArgumentVerdict(ClrAcceptance.RefusedByVocabulary, ClrFormalDisplay(formal));
        }

        if (IsArgumentAssignable(argType, mapped, node, literalAdaptation))
        {
            // The Sharpy spelling accepted. It stands unless the mapping is lossy and .NET rejects
            // the argument's own CLR type, in which case the acceptance proved nothing (#1573).
            if (clrAccepts || argClrType == null || !IsLossyClrMapping(formal.ClrType, mapped))
                return ClrAccepted;

            return new ClrArgumentVerdict(ClrAcceptance.RefusedByClr, ClrFormalDisplay(formal));
        }

        if (clrAccepts)
            return ClrAccepted;

        return new ClrArgumentVerdict(ClrAcceptance.RefusedByVocabulary, ClrFormalDisplay(formal));
    }

    /// <summary>
    /// Sharpy's declared-nullability rule, asked of the SELECTED candidate: a formal .NET declares
    /// non-nullable does not take a bare <c>None</c> (SPY0229). Never asked of a <c>params</c>
    /// element, whose element nullability reflection does not carry.
    /// </summary>
    private bool ClrFormalRefusesNone(ClrFormal formal, Expression? node)
        => node != null
           && UnwrapParenthesized(node) is NoneLiteral
           && !formal.ClrType.IsValueType
           && formal is { Parameter: { } parameter, IsParamsElement: false }
           && Discovery.ClrDeclaredNullability.DeclaresNonNullableArgument(parameter);

    // ---------------------------------------------------------------------------------------
    // Candidates, arguments, bindings
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// One callable this seam can bind a call to. <paramref name="Parameters"/> are the reflected
    /// parameters (names, nullability, <c>params</c> mark); <paramref name="ClrTypes"/> are the CLOSED
    /// types an argument faces, which differ on the extension route.
    /// </summary>
    private sealed record ClrCallCandidate(
        string Name,
        IReadOnlyList<ParameterInfo> Parameters,
        IReadOnlyList<Type> ClrTypes,
        object? Origin)
    {
        internal static ClrCallCandidate Of(MethodBase method)
        {
            var parameters = method.GetParameters();
            return new ClrCallCandidate(
                method is ConstructorInfo
                    ? Shared.ClrNameHelper.StripArity(method.DeclaringType?.Name ?? method.Name)
                    : method.Name,
                parameters,
                parameters.Select(p => p.ParameterType).ToList(),
                method);
        }

        internal MethodBase? Method => Origin as MethodBase;

        internal int ParamsIndex
            => Parameters.Count > 0 && IsClrParamsArray(Parameters[^1]) ? Parameters.Count - 1 : -1;

        internal string Describe()
            => $"{Name}({string.Join(", ", ClrTypes.Select(t => Shared.ClrNameHelper.StripArity(t.Name)))})";
    }

    /// <summary>The written arguments of one call, in the shape this seam binds them from.</summary>
    private sealed record ClrCallArguments(
        FunctionCall? Call,
        IReadOnlyList<SemanticType> Positional,
        IReadOnlyList<(string Name, SemanticType Type, Expression Node)> Keywords)
    {
        internal int Count => Positional.Count + Keywords.Count;

        internal Expression? NodeAt(int ordinal)
            => Call != null && ordinal < Call.Arguments.Length ? Call.Arguments[ordinal] : null;
    }

    /// <summary>One written argument bound to one formal of one candidate.</summary>
    private readonly record struct ClrBoundArgument(
        int ParameterIndex,
        int? Ordinal,
        string? Keyword,
        SemanticType Type,
        Expression? Node,
        ClrFormal Formal);

    private enum ClrBindFailureKind { None, UnknownKeyword, DuplicateArgument, MissingRequired, Arity }

    private sealed record ClrCallBinding(
        ClrCallCandidate Candidate,
        IReadOnlyList<ClrBoundArgument> Arguments,
        bool Expanded,
        ClrBindFailureKind Failure,
        string? FailureName)
    {
        internal bool Bound => Failure == ClrBindFailureKind.None;
    }

    /// <summary>
    /// Binds the written arguments to <paramref name="candidate"/>'s parameters — positionally, then
    /// BY NAME for each keyword argument, the way C# does and the way every Sharpy route does. A
    /// keyword name matches its CLR parameter verbatim or through the Pythonic snake_case spelling,
    /// the one spelling rule the member-name matcher already applies (#1591).
    /// </summary>
    private ClrCallBinding BindClrCall(ClrCallCandidate candidate, ClrCallArguments args)
    {
        var parameters = candidate.Parameters;
        var paramsIndex = candidate.ParamsIndex;
        var filled = new bool[parameters.Count];
        var bound = new List<ClrBoundArgument>();
        var expanded = false;

        // A `params` tail is in NORMAL form when exactly one argument reaches it and that argument is
        // the array itself; otherwise it is expanded and each argument faces the element type.
        var normalFormParams = paramsIndex >= 0
            && args.Positional.Count == paramsIndex + 1
            && ClrFormalVerdict(
                    ClrFormal.Of(parameters[paramsIndex], candidate.ClrTypes[paramsIndex]),
                    args.Positional[paramsIndex],
                    args.NodeAt(paramsIndex))
                .Accepted;

        for (int ordinal = 0; ordinal < args.Positional.Count; ordinal++)
        {
            if (paramsIndex >= 0 && ordinal >= paramsIndex && !normalFormParams)
            {
                expanded = true;
                filled[paramsIndex] = true;
                bound.Add(new ClrBoundArgument(
                    paramsIndex, ordinal, null, args.Positional[ordinal], args.NodeAt(ordinal),
                    ClrFormal.Element(parameters[paramsIndex], candidate.ClrTypes[paramsIndex])));
                continue;
            }

            if (ordinal >= parameters.Count)
                return new ClrCallBinding(candidate, bound, expanded, ClrBindFailureKind.Arity, null);

            filled[ordinal] = true;
            bound.Add(new ClrBoundArgument(
                ordinal, ordinal, null, args.Positional[ordinal], args.NodeAt(ordinal),
                ClrFormal.Of(parameters[ordinal], candidate.ClrTypes[ordinal])));
        }

        foreach (var (name, type, node) in args.Keywords)
        {
            var index = FindClrParameterIndex(parameters, name);
            if (index < 0)
                return new ClrCallBinding(candidate, bound, expanded, ClrBindFailureKind.UnknownKeyword, name);
            if (filled[index])
                return new ClrCallBinding(candidate, bound, expanded, ClrBindFailureKind.DuplicateArgument, name);

            filled[index] = true;
            bound.Add(new ClrBoundArgument(
                index, null, name, type, node,
                ClrFormal.Of(parameters[index], candidate.ClrTypes[index])));
        }

        for (int i = 0; i < parameters.Count; i++)
        {
            if (filled[i] || i == paramsIndex || parameters[i].IsOptional)
                continue;
            return new ClrCallBinding(
                candidate, bound, expanded, ClrBindFailureKind.MissingRequired, parameters[i].Name);
        }

        return new ClrCallBinding(candidate, bound, expanded, ClrBindFailureKind.None, null);
    }

    /// <summary>
    /// The parameter a keyword argument names: the verbatim CLR spelling, or the camelCase form of
    /// the written snake_case name — the one spelling rule <c>ValidateClrKeywordArgumentNames</c>
    /// steers callers to.
    /// </summary>
    private static int FindClrParameterIndex(IReadOnlyList<ParameterInfo> parameters, string keyword)
    {
        for (int i = 0; i < parameters.Count; i++)
        {
            if (parameters[i].Name == keyword)
                return i;
        }

        var camel = Shared.NameMangler.ToCamelCase(keyword);
        for (int i = 0; i < parameters.Count; i++)
        {
            if (parameters[i].Name == camel)
                return i;
        }

        return -1;
    }

    /// <summary>Whether every bound argument of <paramref name="binding"/> is accepted by its
    /// formal, recording the first refusal for the same-argument rule and how many arguments were
    /// accepted before it — the measure of how far a failing candidate got, which is what decides
    /// which one a no-match reports against.</summary>
    private bool ClrBindingIsApplicable(
        ClrCallBinding binding, bool literalAdaptation,
        out List<ClrArgumentRefusal> refusals, out int acceptedPrefix)
    {
        refusals = new List<ClrArgumentRefusal>();
        acceptedPrefix = 0;

        foreach (var argument in binding.Arguments)
        {
            var verdict = ClrFormalVerdict(
                argument.Formal, argument.Type, argument.Node, literalAdaptation);
            if (verdict.Accepted)
            {
                acceptedPrefix++;
                continue;
            }

            if (verdict.Kind == ClrAcceptance.RefusedByVocabulary)
            {
                refusals.Add(new ClrArgumentRefusal(
                    argument.Ordinal, argument.Keyword, argument.Type, verdict.ExpectedDisplay,
                    MapClrFormal(argument.Formal)));
            }
            return false;
        }

        return true;
    }

    /// <summary>One candidate's FIRST refused argument, in the vocabulary a diagnostic can name it
    /// with (#1775, extended to the CLR routes: an unspellable CLR formal still has a name —
    /// <c>IFormatProvider</c>, <c>Func</c> — and naming it is the whole point).</summary>
    private readonly record struct ClrArgumentRefusal(
        int? Ordinal, string? Keyword, SemanticType ArgumentType, string ExpectedDisplay,
        SemanticType? MappedExpected);

    // ---------------------------------------------------------------------------------------
    // Betterness (C# §12.6.4.3)
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Which of two formals converts <paramref name="argType"/> better: negative when
    /// <paramref name="a"/> does, positive when <paramref name="b"/> does, zero when neither. The
    /// order is C#'s better-conversion-from-expression rule, read over CLR types:
    /// <list type="number">
    /// <item>identity — the argument's own CLR type IS the formal — beats every conversion;</item>
    /// <item>the formal whose Sharpy spelling IS the argument's type beats one reached by a
    /// conversion (so a <c>str</c> picks <c>String</c> over <c>Object</c>);</item>
    /// <item>a formal this seam can spell beats one it cannot — <c>object</c>, an unspellable
    /// interface, a ref-struct — which is the arm that stops obliviousness from manufacturing an
    /// ambiguity;</item>
    /// <item>the more specific of two reference types wins (<c>String</c> over <c>Object</c>,
    /// <c>List&lt;T&gt;</c> over <c>IEnumerable&lt;T&gt;</c>);</item>
    /// <item>for the rest, C#'s better-conversion-target rule: <c>T1</c> beats <c>T2</c> when
    /// <c>T1</c> converts implicitly to <c>T2</c> and not the reverse (<c>float32</c> beats
    /// <c>float64</c>, <c>int32</c> beats <c>int64</c>).</item>
    /// </list>
    /// </summary>
    private int CompareClrFormals(ClrFormal a, ClrFormal b, SemanticType argType, Expression? node)
    {
        if (a.ClrType == b.ClrType)
            return 0;

        var argClrType = TryGetClrType(argType);

        var identityA = argClrType != null && a.ClrType == argClrType;
        var identityB = argClrType != null && b.ClrType == argClrType;
        if (identityA != identityB)
            return identityA ? -1 : 1;

        var mappedA = MapClrFormal(a);
        var mappedB = MapClrFormal(b);

        var exactA = mappedA != null && mappedA.Equals(argType);
        var exactB = mappedB != null && mappedB.Equals(argType);
        if (exactA != exactB)
            return exactA ? -1 : 1;

        // A conversion the argument's TYPE licenses beats one only its literal SHAPE does — C#
        // §12.6.4.4 reads the expression's type first, and Sharpy's float-literal adaptation has no
        // C# counterpart at all (`float f = 2.5;` is CS0664).
        var typeAcceptsA = mappedA != null
            && IsArgumentAssignable(argType, mappedA, node, allowConstantConversion: false);
        var typeAcceptsB = mappedB != null
            && IsArgumentAssignable(argType, mappedB, node, allowConstantConversion: false);
        if (typeAcceptsA != typeAcceptsB)
            return typeAcceptsA ? -1 : 1;

        var spellableA = IsSpellableClrFormal(a, mappedA);
        var spellableB = IsSpellableClrFormal(b, mappedB);
        if (spellableA != spellableB)
            return spellableA ? -1 : 1;

        var aFromB = a.ClrType.IsAssignableFrom(b.ClrType);
        var bFromA = b.ClrType.IsAssignableFrom(a.ClrType);
        if (aFromB && !bFromA)
            return 1;
        if (bFromA && !aFromB)
            return -1;

        if (mappedA != null && mappedB != null && !mappedA.Equals(mappedB))
        {
            var aToB = IsArgumentAssignable(mappedA, mappedB, argument: null, allowConstantConversion: false);
            var bToA = IsArgumentAssignable(mappedB, mappedA, argument: null, allowConstantConversion: false);
            if (aToB && !bToA)
                return -1;
            if (bToA && !aToB)
                return 1;
        }

        return 0;
    }

    /// <summary>Whether this seam has an honest word for the formal. <c>object</c> and a ref-struct
    /// never beat a formal that can be named.</summary>
    private static bool IsSpellableClrFormal(ClrFormal formal, SemanticType? mapped)
        => mapped != null && formal.ClrType != typeof(object) && !formal.ClrType.IsByRefLike;

    /// <summary>
    /// C#'s better function member (§12.6.4.3): better on some argument and no worse on any, with
    /// C#'s own tie-breakers folded in so ONE predicate decides the whole order — normal form beats
    /// expanded <c>params</c> form, and fewer skipped optional parameters beats more.
    /// </summary>
    private bool IsBetterClrBinding(ClrCallBinding a, ClrCallBinding b, ClrCallArguments args)
    {
        var anyBetter = false;

        foreach (var argument in a.Arguments)
        {
            var counterpart = argument.Keyword != null
                ? b.Arguments.FirstOrDefault(x => x.Keyword == argument.Keyword)
                : b.Arguments.FirstOrDefault(x => x.Ordinal == argument.Ordinal);
            if (counterpart.Formal.ClrType == null)
                return false;

            var comparison = CompareClrFormals(
                argument.Formal, counterpart.Formal, argument.Type, argument.Node);
            if (comparison > 0)
                return false;
            if (comparison < 0)
                anyBetter = true;
        }

        if (anyBetter)
            return true;

        // Every argument converts equally well. C#'s tie-breakers decide, in its order.
        if (a.Expanded != b.Expanded)
            return !a.Expanded;

        return a.Candidate.Parameters.Count < b.Candidate.Parameters.Count;
    }

    // ---------------------------------------------------------------------------------------
    // The decision
    // ---------------------------------------------------------------------------------------

    private enum ClrCallOutcome
    {
        /// <summary>One candidate won; its arguments are checked against it.</summary>
        Selected,
        /// <summary>No candidate binds this many arguments.</summary>
        Arity,
        /// <summary>A keyword argument names no parameter of any candidate.</summary>
        UnknownKeyword,
        /// <summary>Candidates bound, none is applicable.</summary>
        NoMatch,
        /// <summary>Several applicable candidates and none better than the rest.</summary>
        Ambiguous
    }

    private sealed record ClrCallDecision(
        ClrCallOutcome Outcome,
        ClrCallBinding? Selected,
        IReadOnlyList<ClrCallBinding> Pool,
        IReadOnlyList<ClrArgumentRefusal> Refusals,
        int BoundCount,
        string? FailureName,
        ClrCallBinding? BestFailing = null);

    /// <summary>The bound candidates every argument is applicable to, with each failing candidate's
    /// reach — how many arguments it accepted before refusing one.</summary>
    private List<ClrCallBinding> ApplicableClrBindings(
        IReadOnlyList<ClrCallBinding> bound, bool literalAdaptation,
        out List<ClrArgumentRefusal> refusals, out Dictionary<ClrCallBinding, int> reach)
    {
        refusals = new List<ClrArgumentRefusal>();
        reach = new Dictionary<ClrCallBinding, int>();
        var applicable = new List<ClrCallBinding>();

        foreach (var binding in bound)
        {
            if (ClrBindingIsApplicable(binding, literalAdaptation, out var candidateRefusals, out var accepted))
            {
                applicable.Add(binding);
                continue;
            }

            refusals.AddRange(candidateRefusals);
            reach[binding] = accepted;
        }

        return applicable;
    }

    /// <summary>
    /// THE decision every CLR route makes: bind, filter by applicability, then pick the better
    /// candidate. Reports nothing — the routes' wordings differ and each owns its own — so this stays
    /// the one place the ANSWER is computed.
    /// </summary>
    private ClrCallDecision DecideClrCall(
        IReadOnlyList<ClrCallCandidate> candidates, ClrCallArguments args)
    {
        var bindings = candidates.Select(c => BindClrCall(c, args)).ToList();
        var bound = bindings.Where(b => b.Bound).ToList();

        if (bound.Count == 0)
        {
            var unknownKeyword = bindings.FirstOrDefault(
                b => b.Failure == ClrBindFailureKind.UnknownKeyword);
            if (unknownKeyword != null
                && bindings.All(b => b.Failure == ClrBindFailureKind.UnknownKeyword))
            {
                return new ClrCallDecision(
                    ClrCallOutcome.UnknownKeyword, null, Array.Empty<ClrCallBinding>(),
                    Array.Empty<ClrArgumentRefusal>(), 0, unknownKeyword.FailureName);
            }

            return new ClrCallDecision(
                ClrCallOutcome.Arity, null, Array.Empty<ClrCallBinding>(),
                Array.Empty<ClrArgumentRefusal>(), 0, null);
        }

        // Applicability, C#'s set first. Sharpy's literal adaptations (a float literal into a
        // float32 formal) have no C# counterpart, so admitting them into the candidate set makes
        // overload sets ambiguous that C# resolves — `Math.max(1, 2.5)` gained Max(Single, Single)
        // and Max(Decimal, Decimal) that way, and betterness cannot undo it, because C#'s own
        // better-conversion-target rule genuinely prefers `float` for the `1`. They are therefore a
        // FALLBACK: consulted only when the C#-faithful set is empty, which is where
        // `Vector2(1.0, 2.0)` lives.
        var applicable = ApplicableClrBindings(bound, literalAdaptation: false, out var refusals, out var reach);
        if (applicable.Count == 0)
            applicable = ApplicableClrBindings(bound, literalAdaptation: true, out refusals, out reach);

        if (applicable.Count == 0)
        {
            // The candidates that got FURTHEST are the ones a no-match reports against, the way C#
            // reports CS1503 against the overload its own resolution preferred: `DateTime(2020, 1,
            // "x")` is argument 3's type error, not "no overload takes these three". A candidate that
            // fell over at argument 1 has nothing to say about argument 3, so its refusal is not
            // allowed to veto the agreement among those that reached it.
            ClrCallBinding? bestFailing = null;
            var maximalRefusals = refusals;
            var maximalCount = bound.Count;
            if (reach.Count > 0)
            {
                var furthest = reach.Max(r => r.Value);
                var tied = reach.Where(r => r.Value == furthest).Select(r => r.Key).ToList();
                if (tied.Count == 1)
                    bestFailing = tied[0];
                maximalCount = tied.Count;
                maximalRefusals = new List<ClrArgumentRefusal>();
                foreach (var binding in tied)
                {
                    if (!ClrBindingIsApplicable(binding, literalAdaptation: true, out var r, out _))
                        maximalRefusals.AddRange(r);
                }
            }
            return new ClrCallDecision(
                ClrCallOutcome.NoMatch, null, bound, maximalRefusals, maximalCount, null, bestFailing);
        }

        if (applicable.Count == 1)
        {
            return new ClrCallDecision(
                ClrCallOutcome.Selected, applicable[0], applicable, refusals, bound.Count, null);
        }

        // The MAXIMAL candidates: those no other candidate is better than. One of them is the call's
        // binding; several is C#'s ambiguity, and naming only those is what keeps the SPY0601 candidate
        // list to the overloads a reader has to choose between — `Console.write_line(None)` is a choice
        // between WriteLine(Char[]) and WriteLine(String), not between those and the object overload
        // both of them beat.
        var maximal = applicable
            .Where(candidate => !applicable.Any(
                other => !ReferenceEquals(other, candidate) && IsBetterClrBinding(other, candidate, args)))
            .ToList();

        if (maximal.Count == 1)
        {
            return new ClrCallDecision(
                ClrCallOutcome.Selected, maximal[0], applicable, refusals, bound.Count, null);
        }

        return new ClrCallDecision(
            ClrCallOutcome.Ambiguous, null, maximal.Count > 0 ? maximal : applicable, refusals, bound.Count, null);
    }

    /// <summary>
    /// Whether an argument of this call carries no fact to adjudicate on, so a refusal would be a
    /// guess: an error recovery, or a lambda whose types are still being inferred and which Roslyn
    /// has delegate information for that this seam lacks (#1569).
    /// </summary>
    /// <remarks>
    /// An argument the bridge collapsed to <c>object</c> (<see cref="UnmappedClrType"/>) counts as
    /// unadjudicable for the SET, not just for its own formal. It accepts every formal, so it cannot
    /// discriminate between them — and a set nothing can discriminate is not an ambiguity, it is a
    /// question this seam cannot answer: `Convert.to_int32(finfo.Attributes)`, whose enum-typed
    /// argument the member resolver declines to type, would otherwise be SPY0601 between six
    /// overloads C# picks one of.
    /// </remarks>
    private bool ClrCallCannotAdjudicate(ClrCallArguments args)
    {
        for (int ordinal = 0; ordinal < args.Positional.Count; ordinal++)
        {
            // A `None` has no semantic type of its own, so it reads as Unknown — but it IS a fact,
            // and the one the nullability and ambiguity arms are about. `sb.append(None)` used to
            // fall out here and reach Roslyn as CS0121.
            if (args.NodeAt(ordinal) is { } node && UnwrapParenthesized(node) is NoneLiteral)
                continue;
            if (ClrArgumentIsUnadjudicable(args.Positional[ordinal]))
                return true;
        }

        foreach (var (_, type, node) in args.Keywords)
        {
            if (UnwrapParenthesized(node) is NoneLiteral)
                continue;
            if (ClrArgumentIsUnadjudicable(type))
                return true;
        }

        return false;
    }

    private static bool ClrArgumentIsUnadjudicable(SemanticType type)
        => type is UnknownType or UnmappedClrType
           || (type is FunctionType fn && fn.HasUnresolvedTypes());

    // ---------------------------------------------------------------------------------------
    // The post-selection argument check
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Checks each argument of the SELECTED candidate and records the conversions the emitter reads.
    /// Reports the same refusals this seam's applicability answer gives, in the vocabulary that names
    /// the formal — plus Sharpy's own declared-nullability rule (SPY0229), which applicability
    /// deliberately does not decide.
    /// </summary>
    /// <param name="skipOrdinals">Argument positions the caller has already decided on its own terms
    /// — the CLR-<c>char</c> parameters (#1402), whose <c>str</c> argument is governed by the
    /// single-character-literal rule and would otherwise be reported here a second time.</param>
    private void CheckClrBindingArguments(
        ClrCallBinding binding, ClrCallArguments args, string memberDisplay,
        HashSet<int>? skipOrdinals = null)
    {
        foreach (var argument in binding.Arguments)
        {
            if (argument.Ordinal is { } ordinal && skipOrdinals?.Contains(ordinal) == true)
                continue;

            var formal = argument.Formal;

            // Sharpy's declared-nullability rule, decided on the winner (#1705). `Path.combine(None,
            // "b")` reaches here having SELECTED Combine(string, string) — the refusal is not hidden
            // by the params sibling that used to win the set by default.
            if (ClrFormalRefusesNone(formal, argument.Node))
            {
                var nullableDisplay = MapClrFormal(formal) is { } mappedFormal
                    ? mappedFormal.GetDisplayName()
                    : Shared.ClrNameHelper.StripArity(formal.ClrType.Name);
                var noneNode = argument.Node!;
                AddError(
                    $"Cannot pass 'None' to parameter '{formal.Parameter!.Name}' of '{memberDisplay}' — "
                    + $"it is declared non-nullable ('{nullableDisplay}')",
                    noneNode.LineStart, noneNode.ColumnStart,
                    code: DiagnosticCodes.Semantic.NullabilityViolation,
                    span: noneNode.Span);
                continue;
            }

            var verdict = ClrFormalVerdict(formal, argument.Type, argument.Node);

            if (MapClrFormal(formal) is { } expected)
            {
                // Materialization is recorded before the acceptance question, in the same order the
                // argument-binding seam uses (ValidateCallArguments), so the checker and the emitter
                // agree about copies (#1251, #1260).
                RecordSequenceMaterialization(argument.Node, argument.Type, expected);

                if (verdict.Accepted)
                {
                    ApplyArgumentConversion(
                        argument.Keyword != null
                            ? StorePosition.ArgumentKeyword
                            : StorePosition.ArgumentPositional,
                        argument.Node, argument.Type, expected);
                    continue;
                }
            }
            else if (verdict.Accepted)
            {
                continue;
            }

            var slot = argument.Keyword != null
                ? $"Argument '{argument.Keyword}'"
                : $"Argument {(argument.Ordinal ?? 0) + 1}";
            var node = argument.Node;
            AddError(
                $"{slot} of '{memberDisplay}' expects '{verdict.ExpectedDisplay}' "
                + $"but got '{argument.Type.GetDisplayName()}'",
                node?.LineStart ?? args.Call?.LineStart ?? 0,
                node?.ColumnStart ?? args.Call?.ColumnStart ?? 0,
                code: DiagnosticCodes.Semantic.TypeMismatch,
                span: node?.Span ?? args.Call?.Span);
        }
    }

    /// <summary>
    /// The CLR seam's same-argument rule: when every bound candidate refused the SAME argument, that
    /// argument's own type mismatch is the diagnostic, not "no matching overload" (#1775). Reaches
    /// further than the Sharpy-route helper it mirrors, because a CLR formal the bridge cannot spell
    /// still has a name — <c>IFormatProvider</c>, <c>Func</c> — and naming it is what turns
    /// <c>sb.append_line("ok", 42)</c> and <c>xs.first_or_default("a")</c> into diagnostics.
    /// </summary>
    private bool TryReportClrSameArgumentRefusal(
        ClrCallArguments args, ClrCallDecision decision, string memberDisplay)
    {
        if (decision.BoundCount == 0 || decision.Refusals.Count != decision.BoundCount)
            return false;

        var first = decision.Refusals[0];
        if (decision.Refusals.Any(r => r.Ordinal != first.Ordinal || r.Keyword != first.Keyword))
            return false;
        if (first.ArgumentType is UnknownType)
            return false;

        // Every formal spellable and all agreeing: the store seam's own refusal — code, message and
        // steer — through the helper every other route reports through, so a bare None into `int` is
        // SPY0229 and a bare value into `int?` is SPY0604 here as everywhere else.
        var expectedTypes = new List<SemanticType>();
        foreach (var refusal in decision.Refusals)
        {
            if (refusal.MappedExpected is not { } mapped || mapped is UnknownType)
            {
                expectedTypes.Clear();
                break;
            }
            if (!expectedTypes.Any(t => t.Equals(mapped)))
                expectedTypes.Add(mapped);
        }

        var node = first.Keyword != null
            ? args.Keywords.FirstOrDefault(k => k.Name == first.Keyword).Node
            : args.NodeAt(first.Ordinal ?? 0);

        if (expectedTypes.Count == 1)
        {
            if (first.Keyword != null)
            {
                CheckStoreAt(StorePosition.ArgumentKeyword, node, first.ArgumentType, expectedTypes[0],
                    node?.LineStart ?? args.Call?.LineStart ?? 0,
                    node?.ColumnStart ?? args.Call?.ColumnStart ?? 0,
                    node?.Span ?? args.Call?.Span,
                    slotName: first.Keyword);
            }
            else
            {
                CheckStore(StorePosition.ArgumentPositional, node, first.ArgumentType, expectedTypes[0],
                    (Node?)node ?? args.Call!, node?.Span ?? args.Call?.Span);
            }
            return true;
        }

        var displays = decision.Refusals
            .Select(r => r.ExpectedDisplay)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var slotName = first.Keyword != null
            ? $"Argument '{first.Keyword}'"
            : $"Argument {(first.Ordinal ?? 0) + 1}";
        var expectedList = displays.Count == 1
            ? $"'{displays[0]}'"
            : string.Join(" or ", displays.Select(d => $"'{d}'"));

        AddError(
            $"{slotName} of '{memberDisplay}' expects {expectedList} but got "
            + $"'{first.ArgumentType.GetDisplayName()}'",
            node?.LineStart ?? args.Call?.LineStart ?? 0,
            node?.ColumnStart ?? args.Call?.ColumnStart ?? 0,
            code: DiagnosticCodes.Semantic.TypeMismatch,
            span: node?.Span ?? args.Call?.Span);
        return true;
    }

    /// <summary>
    /// The SPY0601 steer: the first argument position at which the surviving candidates disagree, and
    /// the spellings of each candidate's formal there — the types the user can cast the argument to.
    /// </summary>
    private string DescribeClrDisambiguatingCast(IReadOnlyList<ClrCallBinding> pool, ClrCallArguments args)
    {
        foreach (var probe in pool[0].Arguments)
        {
            var spellings = new List<string>();
            foreach (var binding in pool)
            {
                var counterpart = probe.Keyword != null
                    ? binding.Arguments.FirstOrDefault(x => x.Keyword == probe.Keyword)
                    : binding.Arguments.FirstOrDefault(x => x.Ordinal == probe.Ordinal);
                if (counterpart.Formal.ClrType == null)
                    continue;

                // The cast TARGET is the formal's underlying type: a `string?` parameter is
                // disambiguated by casting to `str` (`str?` would read as Optional) (#1705).
                var spelling = MapClrFormal(counterpart.Formal) is { } formal
                    ? (formal is NullableType nullable ? nullable.UnderlyingType : formal).GetDisplayName()
                    : Shared.ClrNameHelper.StripArity(counterpart.Formal.ClrType.Name);
                if (!spellings.Contains(spelling, StringComparer.Ordinal))
                    spellings.Add(spelling);
            }

            if (spellings.Count > 1)
            {
                var position = probe.Keyword != null ? $"'{probe.Keyword}'" : $"{(probe.Ordinal ?? 0) + 1}";
                return $"Disambiguate by casting argument {position} to one of: {string.Join(", ", spellings)}";
            }
        }

        return "Disambiguate by casting the argument to the intended type";
    }

    /// <summary>The candidate list an ambiguity or a no-match names.</summary>
    private static string DescribeClrCandidates(IReadOnlyList<ClrCallBinding> pool)
        => string.Join(", ", pool.Select(b => b.Candidate.Describe()));

    // ---------------------------------------------------------------------------------------
    // The routes' shared entry point
    // ---------------------------------------------------------------------------------------

    /// <summary>The written arguments of <paramref name="call"/>, positional and keyword alike.</summary>
    private ClrCallArguments ClrCallArgumentsOf(
        FunctionCall call, List<SemanticType> argTypes, Dictionary<string, SemanticType> kwargTypes)
    {
        var keywords = new List<(string, SemanticType, Expression)>();
        foreach (var kwarg in call.KeywordArguments)
        {
            var type = kwargTypes.TryGetValue(kwarg.Name, out var known)
                ? known
                : _semanticInfo.GetExpressionType(kwarg.Value) ?? SemanticType.Unknown;
            keywords.Add((kwarg.Name, type, kwarg.Value));
        }

        return new ClrCallArguments(call, argTypes, keywords);
    }

    /// <summary>
    /// Reports <paramref name="decision"/> and, when a candidate won, checks its arguments — the ONE
    /// reporting path every CLR route shares, so an arity, a no-match, an ambiguity and an argument
    /// mismatch read the same however the call was spelled. Returns the selected binding, or null.
    /// </summary>
    /// <param name="arityMessage">The route's own arity wording ("'X' expects 1 to 2 arguments but got
    /// 3"), which differs for a constructor.</param>
    /// <param name="suppressRefusals">The instance route's extension fallback: a name an extension
    /// method could also answer is not this seam's to refuse, so a refusal is dropped while a
    /// selection still binds (#1798).</param>
    /// <param name="reportAmbiguity">False for the constructor route on an open generic type, whose
    /// reflected parameters still name type parameters, so "several survive" is not a fact.</param>
    private ClrCallBinding? ReportClrCallDecision(
        ClrCallDecision decision, ClrCallArguments args, string memberDisplay,
        string arityMessage, bool suppressRefusals = false, bool reportAmbiguity = true,
        HashSet<int>? skipOrdinals = null)
    {
        var call = args.Call!;

        switch (decision.Outcome)
        {
            case ClrCallOutcome.Selected:
                // The char row first: it owns its formal's acceptance AND its refusal wording, and the
                // positions it answered are excluded from the general check below, which would
                // otherwise report the same str/char mismatch a second time in its own words. Every
                // route reaches it here — it used to be a static-route special case (#1402).
                var charOrdinals = RecordClrCharArguments(decision.Selected!, memberDisplay);
                if (skipOrdinals != null)
                    charOrdinals.UnionWith(skipOrdinals);
                CheckClrBindingArguments(decision.Selected!, args, memberDisplay, charOrdinals);
                return decision.Selected;

            case ClrCallOutcome.Arity:
                if (suppressRefusals)
                    return null;
                AddError(arityMessage, call.LineStart, call.ColumnStart,
                    code: DiagnosticCodes.Semantic.WrongArgumentCount, span: call.Span);
                return null;

            case ClrCallOutcome.UnknownKeyword:
                if (suppressRefusals)
                    return null;
                ReportUnknownClrKeyword(call, decision.FailureName!, decision.Pool);
                return null;

            case ClrCallOutcome.NoMatch:
                // An argument still being inferred carries no fact to adjudicate on, and Roslyn has
                // delegate information this seam lacks (#1569).
                if (suppressRefusals || ClrCallCannotAdjudicate(args))
                    return null;

                // A SINGLE bound candidate is the call the user wrote, so every one of its arguments is
                // as diagnosable as a selected candidate's — `Vector2(x="a", y="b")` is two mismatches,
                // not one. The same-argument rule below is for a SET that agrees.
                if (decision.BoundCount == 1 && decision.BestFailing is { } soleFailing)
                {
                    CheckClrBindingArguments(soleFailing, args, memberDisplay, skipOrdinals);
                    return null;
                }
                if (TryReportSameArgumentRefusal(
                        call, args.Positional, decision.BoundCount,
                        decision.Refusals.Select(ToOverloadCandidateFailure).ToList()))
                {
                    return null;
                }
                if (TryReportClrSameArgumentRefusal(args, decision, memberDisplay))
                    return null;
                if (decision.BestFailing is { } bestFailing)
                {
                    CheckClrBindingArguments(bestFailing, args, memberDisplay, skipOrdinals);
                    return null;
                }
                AddError(
                    $"No matching overload for '{memberDisplay}' with the given argument types. "
                    + $"Candidates: {DescribeClrCandidates(decision.Pool)}",
                    call.LineStart, call.ColumnStart,
                    code: DiagnosticCodes.Semantic.NoMatchingOverload, span: call.Span);
                return null;

            default:
                if (suppressRefusals || !reportAmbiguity || ClrCallCannotAdjudicate(args))
                    return null;
                AddError(
                    $"Call to '{memberDisplay}' is ambiguous between {decision.Pool.Count} overloads: "
                    + $"{DescribeClrCandidates(decision.Pool)}. "
                    + DescribeClrDisambiguatingCast(decision.Pool, args),
                    call.LineStart, call.ColumnStart,
                    code: DiagnosticCodes.SemanticOverflow.AmbiguousClrOverload, span: call.Span);
                return null;
        }
    }

    /// <summary>
    /// The Sharpy-route same-argument record built from a CLR refusal, so the ONE helper every route
    /// reports through (<see cref="TryReportSameArgumentRefusal"/>) answers the CLR routes too.
    /// </summary>
    private static OverloadCandidateFailure ToOverloadCandidateFailure(ClrArgumentRefusal refusal)
        => new(new ArgumentRef(refusal.Ordinal, refusal.Keyword),
            refusal.MappedExpected ?? SemanticType.Unknown,
            OverloadFailureKind.Type);

    /// <summary>
    /// SPY0234 for a keyword no candidate binds, with the did-you-mean over the Python spellings of
    /// the candidates' parameter names.
    /// </summary>
    private void ReportUnknownClrKeyword(
        FunctionCall call, string keyword, IReadOnlyList<ClrCallBinding> pool)
    {
        var kwarg = call.KeywordArguments.FirstOrDefault(k => k.Name == keyword);
        if (kwarg == null)
            return;

        var names = pool
            .SelectMany(b => b.Candidate.Parameters)
            .Select(p => p.Name)
            .OfType<string>()
            .Select(CanonicalClrParameterSpelling)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var suggestion = EditDistance.FindClosestMatch(keyword, names);
        var message = $"Unknown keyword argument '{keyword}'";
        if (suggestion != null)
            message += $". Did you mean '{suggestion}'?";

        AddError(message, kwarg.LineStart, kwarg.ColumnStart,
            code: DiagnosticCodes.Semantic.UnknownKeywordArgument,
            span: kwarg.Span ?? kwarg.Value.Span,
            data: SuggestionData(suggestion));
    }

    // ---------------------------------------------------------------------------------------
    // The extension route
    // ---------------------------------------------------------------------------------------

    /// <summary>What asking the extension surface about a call established.</summary>
    private enum ClrExtensionProbe
    {
        /// <summary>Nothing by that name binds this receiver with these argument shapes, so the
        /// extension surface does not answer the call and the instance decision stands.</summary>
        NoCandidates,
        /// <summary>An extension method takes the call. Nothing to report.</summary>
        Bound,
        /// <summary>Every extension candidate refused, and the refusal was reported.</summary>
        Refused,
        /// <summary>The shapes this seam cannot decide — keyword arguments, a spread, a lambda still
        /// being inferred — so the call stays as permissive as it was.</summary>
        Undecidable
    }

    /// <summary>
    /// The extension-method route, asked through THE seam. C#'s precedence governs: extensions are
    /// consulted only when no instance member is applicable, which is exactly where the instance route
    /// calls this. Before it existed the route decided BY NAME — the mere reachability of an extension
    /// spelling suppressed every instance refusal, and an extension's own argument mistype
    /// (<c>xs.first_or_default("a")</c>) reached Roslyn as CS1503 behind SPY0908.
    /// </summary>
    private ClrExtensionProbe ProbeClrExtensionCall(
        FunctionCall call, string memberName, SemanticType receiverType, Type receiverClrType,
        ClrCallArguments args, string memberDisplay)
    {
        // A keyword argument has no positional correspondence to the shape list the resolver pairs
        // formals by, and a spread stands for however many arguments the sequence holds (#1591).
        if (args.Keywords.Count > 0 || call.Arguments.Any(a => a is SpreadElement))
            return ClrExtensionProbe.Undecidable;

        // `object` is where the bridge GAVE UP, so the emitted expression may well be a sequence the
        // checker cannot see, and refusing on a non-fact is how a permissive channel becomes a false
        // error (#1206 D2).
        if (IsObjectType(receiverType))
            return ClrExtensionProbe.Undecidable;

        var shapes = new Discovery.ClrExtensionMethodResolver.ExtensionArgumentShape[call.Arguments.Length];
        for (int i = 0; i < call.Arguments.Length; i++)
        {
            shapes[i] = UnwrapParenthesized(call.Arguments[i]) is LambdaExpression lambda
                ? Discovery.ClrExtensionMethodResolver.ExtensionArgumentShape.Lambda(lambda.Parameters.Length)
                : Discovery.ClrExtensionMethodResolver.ExtensionArgumentShape.Value;
        }

        var resolved = Discovery.ClrExtensionMethodResolver.TryResolveAllFromReceiver(
            receiverClrType, Shared.NameMangler.ToPascalCase(memberName), shapes);
        if (resolved.Count == 0)
        {
            resolved = Discovery.ClrExtensionMethodResolver.TryResolveAllFromReceiver(
                receiverClrType, memberName, shapes);
        }
        if (resolved.Count == 0)
        {
            // Nothing of that name bound these shapes. When the name DOES have an overload this
            // receiver satisfies, the reason is the COUNT — the resolver pairs formals with shapes by
            // position and requires them to match exactly — so the arity is refused by name rather
            // than left to CS1501 behind SPY0908. A lambda argument is excluded: its own arity is one
            // of the things the resolver matched on, and a mismatch there is not the call's count.
            var arities = Discovery.ClrExtensionMethodResolver.CandidateArities(
                Shared.NameMangler.ToPascalCase(memberName));
            if (arities.Count > 0
                && !arities.Contains(call.Arguments.Length)
                && !call.Arguments.Any(a => UnwrapParenthesized(a) is LambdaExpression)
                && Discovery.ClrExtensionMethodResolver.AnyOverloadAcceptsReceiver(
                    receiverClrType, Shared.NameMangler.ToPascalCase(memberName)))
            {
                var expected = arities.Count == 1
                    ? $"{arities[0]} argument{(arities[0] == 1 ? "" : "s")}"
                    : $"{arities.Min()} to {arities.Max()} arguments";
                AddError(
                    $"'{memberDisplay}' expects {expected} but got {call.Arguments.Length}",
                    call.LineStart, call.ColumnStart,
                    code: DiagnosticCodes.Semantic.WrongArgumentCount, span: call.Span);
                return ClrExtensionProbe.Refused;
            }

            return ClrExtensionProbe.NoCandidates;
        }

        var candidates = resolved
            .Select(partial => new ClrCallCandidate(
                partial.ClrMethodName,
                partial.OpenMethod.GetParameters().Skip(1).ToList(),
                partial.ParameterTypes,
                partial))
            .Where(candidate => candidate.Parameters.Count == candidate.ClrTypes.Count)
            .ToList();
        if (candidates.Count == 0)
            return ClrExtensionProbe.NoCandidates;

        var decision = DecideClrCall(candidates, args);

        if (decision.Outcome == ClrCallOutcome.Selected)
            return ClrExtensionProbe.Bound;

        // The resolver closes type parameters from the RECEIVER only, so an argument-determined one is
        // still open here and an "ambiguity" between two such candidates is not a fact about the call —
        // inference has yet to run. When every surviving candidate is CLOSED, the ambiguity IS the
        // fact, and Roslyn will reach the same one: `xs.first_or_default(None)` is CS0121 between
        // FirstOrDefault(source, TSource) and FirstOrDefault(source, Func<TSource, bool>).
        if (decision.Outcome == ClrCallOutcome.Ambiguous)
        {
            var allClosed = decision.Pool.All(binding =>
                binding.Candidate.Origin is Discovery.ClrExtensionMethodResolver.PartialResolution
                {
                    OpenTypeParameterNames.Count: 0
                });
            if (!allClosed || ClrCallCannotAdjudicate(args))
                return ClrExtensionProbe.Bound;

            AddError(
                $"Call to '{memberDisplay}' is ambiguous between {decision.Pool.Count} overloads: "
                + $"{DescribeClrCandidates(decision.Pool)}. "
                + DescribeClrDisambiguatingCast(decision.Pool, args),
                call.LineStart, call.ColumnStart,
                code: DiagnosticCodes.SemanticOverflow.AmbiguousClrOverload, span: call.Span);
            return ClrExtensionProbe.Refused;
        }
        if (decision.Outcome == ClrCallOutcome.Arity || ClrCallCannotAdjudicate(args))
            return ClrExtensionProbe.Undecidable;

        return ReportClrCallDecision(
                decision, args, memberDisplay,
                arityMessage: $"'{memberDisplay}' expects {candidates[0].Parameters.Count} arguments "
                    + $"but got {args.Count}")
            is null
            ? ClrExtensionProbe.Refused
            : ClrExtensionProbe.Bound;
    }

    // ---------------------------------------------------------------------------------------
    // The indexer route
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Whether the CLR indexers of <paramref name="closedClrType"/> accept this key, reporting the
    /// mismatch by name when none does — the fifth CLR call route, and the one that used to ask
    /// nothing at all: <c>d["wrong"]</c> on a <c>Dictionary[int, str]</c> reached Roslyn as CS1503
    /// behind SPY0908, and the element type it inferred came from an indexer the key cannot reach.
    /// The acceptance question is the seam's (<see cref="ClrFormalVerdict"/>), so a key decides here
    /// exactly as an argument decides at every other route.
    /// </summary>
    /// <param name="forStore">A store validates against the SETTER's index parameter, a read against
    /// the getter's — the same positional rule the user-protocol arm applies (#1620).</param>
    private bool ClrIndexerAcceptsKey(
        IndexAccess indexAccess, Type closedClrType, SemanticType indexType, bool forStore)
    {
        // A key with no settled type carries no fact to adjudicate on.
        if (indexType is UnknownType or TypeParameterType)
            return true;

        var indexers = closedClrType.GetProperties()
            .Where(p => p.GetIndexParameters().Length == 1
                        && (forStore ? p.GetSetMethod() != null : p.GetGetMethod() != null))
            .ToList();

        // No single-key indexer to decide against: a multi-key indexer, or a type whose subscript
        // only codegen resolves. Not this seam's question.
        if (indexers.Count == 0)
            return true;

        var refused = new List<string>();
        var noneRefusals = new List<(string Parameter, string Display)>();
        foreach (var indexer in indexers)
        {
            var formal = ClrFormal.Of(indexer.GetIndexParameters()[0]);

            // Sharpy's declared-nullability rule reaches the subscript too: `d[None]` on a
            // Dictionary[str, int] is SPY0229, not a null key at runtime.
            if (ClrFormalRefusesNone(formal, indexAccess.Index))
            {
                noneRefusals.Add((formal.Parameter!.Name ?? "key", ClrFormalDisplay(formal)));
                continue;
            }

            var verdict = ClrFormalVerdict(formal, indexType, indexAccess.Index);
            if (verdict.Accepted)
                return true;
            if (verdict.Kind == ClrAcceptance.RefusedByVocabulary
                && !refused.Contains(verdict.ExpectedDisplay, StringComparer.Ordinal))
            {
                refused.Add(verdict.ExpectedDisplay);
            }
        }

        if (noneRefusals.Count > 0 && refused.Count == 0)
        {
            AddError(
                $"Cannot pass 'None' to parameter '{noneRefusals[0].Parameter}' of "
                + $"'{Shared.ClrNameHelper.StripArity(closedClrType.Name)}' indexer — it is declared "
                + $"non-nullable ('{noneRefusals[0].Display}')",
                indexAccess.Index.LineStart, indexAccess.Index.ColumnStart,
                code: DiagnosticCodes.Semantic.NullabilityViolation,
                span: indexAccess.Index.Span);
            return false;
        }

        // Every indexer refused for a reason the formal's own spelling does not state (the lossy arm):
        // naming it would be a tautology, so the call stays as permissive as it was.
        if (refused.Count == 0)
            return true;

        var expected = refused.Count == 1
            ? $"'{refused[0]}'"
            : string.Join(" or ", refused.Select(r => $"'{r}'"));
        AddError(
            $"Cannot index '{Shared.ClrNameHelper.StripArity(closedClrType.Name)}' with a key of type "
            + $"'{indexType.GetDisplayName()}' — its indexer takes {expected}",
            indexAccess.Index.LineStart, indexAccess.Index.ColumnStart,
            code: DiagnosticCodes.Semantic.TypeMismatch,
            span: indexAccess.Index.Span);
        return false;
    }
}
