using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Shared;

/// <summary>
/// Centralized exhaustiveness logic for finite type matching.
/// Replaces duplicate implementations in ExhaustivenessValidator,
/// ControlFlowGraphBuilder, and RoslynEmitter.Patterns.
/// </summary>
internal static class ExhaustivenessHelper
{
    /// <summary>
    /// Returns the set of all case names for finite types (bool, enum, union, Optional, Result).
    /// Returns null for non-finite types.
    /// </summary>
    public static HashSet<string>? GetFiniteTypeCases(SemanticType scrutineeType)
    {
        // Bool type: True and False
        if (scrutineeType is BuiltinType bt && bt == BuiltinType.Bool)
        {
            return new HashSet<string> { WellKnownCaseNames.True, WellKnownCaseNames.False };
        }

        // Enum type: all enum member names
        if (scrutineeType is UserDefinedType udt && udt.Symbol?.TypeKind == TypeKind.Enum)
        {
            return new HashSet<string>(udt.Symbol.Fields.Select(f => f.Name));
        }

        // Tagged union type (non-generic): all union case names
        if (scrutineeType is UserDefinedType unionUdt && unionUdt.Symbol?.TypeKind == TypeKind.Union)
        {
            return new HashSet<string>(unionUdt.Symbol.UnionCases.Select(c => c.Name));
        }

        // Tagged union type (generic): all union case names
        if (scrutineeType is GenericType gt && gt.GenericDefinition?.TypeKind == TypeKind.Union)
        {
            return new HashSet<string>(gt.GenericDefinition.UnionCases.Select(c => c.Name));
        }

        // Optional type: Some and None
        if (scrutineeType is OptionalType)
        {
            return new HashSet<string> { WellKnownCaseNames.Some, WellKnownCaseNames.None };
        }

        // Result type: Ok and Err
        if (scrutineeType is ResultType)
        {
            return new HashSet<string> { WellKnownCaseNames.Ok, WellKnownCaseNames.Err };
        }

        // `T | None` is a finite family (P13 DD10): the payload case(s) ∪ None. A union/enum/bool
        // payload contributes its own finite cases; a concrete payload contributes the single
        // NullablePayload sentinel, covered by any PayloadTotal/Total head over the nullable.
        if (scrutineeType is NullableType nullable)
        {
            var payloadCases = GetFiniteTypeCases(nullable.UnderlyingType)
                ?? new HashSet<string> { WellKnownCaseNames.NullablePayload };
            payloadCases.Add(WellKnownCaseNames.None);
            return payloadCases;
        }

        return null;
    }

    /// <summary>
    /// Collects all case names covered by the given pattern into the provided set.
    /// </summary>
    public static void CollectCoveredCases(
        Pattern pattern,
        SemanticInfo semanticInfo,
        HashSet<string> covered)
    {
        // Every class-pattern head — `case C():`, `case C(v):` AND `case C(f=v):` — contributes the
        // case name it tests, through the ONE classifier. The property form went uncounted before
        // this routed through PatternHead, so `case Node(value=v):` drew a spurious SPY0463 (#1890).
        if (PatternHead.TryGet(pattern, out var head))
        {
            var headUnionCase = semanticInfo.GetPatternUnionCase(head.Lodge);
            if (headUnionCase != null)
            {
                covered.Add(headUnionCase.Name);
            }
            else if (semanticInfo.GetPatternCoverage(head.Lodge) == PatternCoverage.PayloadTotal)
            {
                // A payload head over `T | None` covers EVERY payload case of the family — recorded
                // as the ONE sentinel, which <see cref="MissingCases"/> reads as "every case but
                // None": the concrete-payload case GetFiniteTypeCases names with the same sentinel,
                // and every member of a finite payload ({A, B} of `E | None`, {True, False} of
                // `bool | None`). Mapping the head to the bare sentinel while the family expanded the
                // finite payload left `case E(): case None:` warning "Missing cases: A, B" and its
                // trailing `case _:` at CS8510 (P13 DD10; plan-6ca898 verify, D3 sibling).
                covered.Add(WellKnownCaseNames.NullablePayload);
            }
            else if (head.Type != null)
            {
                covered.Add(head.Type.Name);
            }
            return;
        }

        switch (pattern)
        {
            case LiteralPattern literal:
                if (literal.Literal is BooleanLiteral boolLit)
                {
                    covered.Add(boolLit.Value ? WellKnownCaseNames.True : WellKnownCaseNames.False);
                }
                // Check for union case recorded by type checker (e.g., None() for Optional)
                var litUnionCase = semanticInfo.GetPatternUnionCase(literal);
                if (litUnionCase != null)
                {
                    covered.Add(litUnionCase.Name);
                }
                // `case None:` over a `T | None` covers the None case of the finite family (P13 D3).
                if (semanticInfo.GetPatternCoverage(literal) == PatternCoverage.NoneArm)
                {
                    covered.Add(WellKnownCaseNames.None);
                }
                break;

            case BindingPattern binding:
                var bindingUnionCase = semanticInfo.GetPatternUnionCase(binding);
                if (bindingUnionCase != null)
                {
                    covered.Add(bindingUnionCase.Name);
                }
                break;

            case MemberAccessPattern memberAccess:
                if (memberAccess.Parts.Length >= 2)
                {
                    var unionCase = semanticInfo.GetPatternUnionCase(memberAccess);
                    if (unionCase != null)
                    {
                        covered.Add(unionCase.Name);
                    }
                    else
                    {
                        // For enums, the last part is the member name
                        covered.Add(memberAccess.Parts[^1]);
                    }
                }
                break;

            case AsPattern asPattern:
                // A head wrapped in `as` was already handled by PatternHead.TryGet above; this arm
                // recurses for an `as` over a non-head inner (a member-access or or-pattern).
                CollectCoveredCases(asPattern.Inner, semanticInfo, covered);
                break;

            case OrPattern orPattern:
                foreach (var alt in orPattern.Alternatives)
                {
                    CollectCoveredCases(alt, semanticInfo, covered);
                }
                break;
        }
    }

    /// <summary>
    /// Returns true if a pattern unconditionally covers all values of the scrutinee's static type
    /// (wildcard, unguarded binding with no constant/union-case, a class-pattern head whose coverage
    /// is <c>Total</c>, or an OrPattern containing any total alternative). Totality is a fact of the
    /// scrutinee's static type recorded by the checker, not of the pattern's spelling (DD9).
    /// </summary>
    public static bool IsTotal(Pattern pattern, SemanticInfo? info)
    {
        return pattern switch
        {
            WildcardPattern => true,
            BindingPattern bp =>
                info?.GetPatternConstantSymbol(bp) == null
                && info?.GetPatternUnionCase(bp) == null,
            AsPattern asp => IsTotal(asp.Inner, info),
            OrPattern or => or.Alternatives.Any(alt => IsTotal(alt, info)),
            GuardPattern => false,
            _ when PatternHead.TryGet(pattern, out var head)
                => info?.GetPatternCoverage(head.Lodge) == PatternCoverage.Total,
            _ => false
        };
    }

    /// <summary>
    /// Back-compat alias for <see cref="IsTotal"/> — a pattern that is total over the scrutinee's
    /// static type is irrefutable there (DD9). Retained for the CFG and reachability consumers.
    /// </summary>
    public static bool IsIrrefutable(Pattern pattern, SemanticInfo? info) => IsTotal(pattern, info);

    /// <summary>
    /// Returns a human-readable description of a total (irrefutable) pattern,
    /// or null if the pattern is not total.
    /// </summary>
    public static string? DescribeIrrefutable(Pattern pattern, SemanticInfo? info)
    {
        return pattern switch
        {
            WildcardPattern => "wildcard",
            BindingPattern bp when info?.GetPatternConstantSymbol(bp) == null
                && info?.GetPatternUnionCase(bp) == null => $"name capture '{bp.Name.Name}'",
            AsPattern asp => DescribeIrrefutable(asp.Inner, info) is { } innerDesc
                ? $"{innerDesc} as '{asp.Name.Name}'"
                : null,
            OrPattern or => or.Alternatives
                .Select(alt => DescribeIrrefutable(alt, info))
                .FirstOrDefault(d => d != null),
            _ when PatternHead.TryGet(pattern, out var head) && info?.GetPatternCoverage(head.Lodge) == PatternCoverage.Total
                => $"total class pattern '{head.Type?.Name}()'",
            _ => null
        };
    }

    /// <summary>
    /// Returns true if the match statement is semantically exhaustive
    /// (all cases of a finite type are covered by unguarded arms).
    /// </summary>
    public static bool IsExhaustiveMatch(
        SemanticType scrutineeType,
        IEnumerable<(Pattern Pattern, Expression? Guard)> arms,
        SemanticInfo semanticInfo)
    {
        var allCases = GetFiniteTypeCases(scrutineeType);
        if (allCases == null)
            return false;

        return MissingCases(allCases, CollectUnguardedCoveredCases(arms, semanticInfo)).Count == 0;
    }

    /// <summary>
    /// The case names of <paramref name="allCases"/> that <paramref name="covered"/> does not cover —
    /// the ONE reading of the covered set. A case is covered when it is named, or when it is a
    /// payload case (anything but <see cref="WellKnownCaseNames.None"/>) and the set holds the
    /// <see cref="WellKnownCaseNames.NullablePayload"/> sentinel a payload-total head over
    /// <c>T | None</c> records: that head covers every payload case, finite or not (P13 DD10).
    /// Returned in <paramref name="allCases"/>' order; render each name through
    /// <see cref="DescribeCase"/> before showing it.
    /// </summary>
    public static List<string> MissingCases(IEnumerable<string> allCases, HashSet<string> covered)
    {
        bool payloadCovered = covered.Contains(WellKnownCaseNames.NullablePayload);
        return allCases
            .Where(c => !covered.Contains(c) && !(payloadCovered && c != WellKnownCaseNames.None))
            .ToList();
    }

    /// <summary>
    /// The user-facing name of a case of <paramref name="scrutineeType"/>'s finite family: the
    /// concrete-payload sentinel of a <c>T | None</c> family is the payload TYPE
    /// (<c>list[int32]</c>), every other case is its own name. A diagnostic that printed the
    /// sentinel showed the literal placeholder <c>&lt;payload&gt;</c>.
    /// </summary>
    public static string DescribeCase(SemanticType scrutineeType, string caseName)
    {
        if (caseName == WellKnownCaseNames.NullablePayload && scrutineeType is NullableType nullable)
            return nullable.UnderlyingType.GetDisplayName();
        return caseName;
    }

    private static HashSet<string> CollectUnguardedCoveredCases(
        IEnumerable<(Pattern Pattern, Expression? Guard)> arms, SemanticInfo semanticInfo)
    {
        var coveredCases = new HashSet<string>();
        foreach (var (pattern, guard) in arms)
        {
            // Guarded arms don't guarantee coverage
            if (guard != null)
                continue;

            CollectCoveredCases(pattern, semanticInfo, coveredCases);
        }
        return coveredCases;
    }

    /// <summary>
    /// Returns true when the unguarded arms are exhaustive UNDER THE EMITTED C# LOWERING, so C#'s own
    /// switch-expression exhaustiveness proves a trailing catch-all unreachable (CS8510) — the ONE
    /// function that decides D5 (P13 DD11). Only the lowerings C# can prove qualify: <c>bool</c>
    /// (a closed type C# enumerates as <c>true</c>/<c>false</c>), synthetic
    /// <see cref="ResultType"/>/<see cref="OptionalType"/> deconstruct to a leading bool discriminant,
    /// and a <see cref="NullableType"/> to payload + null, where C# sees the payload covered by a
    /// type test (a payload-total head — the <see cref="WellKnownCaseNames.NullablePayload"/>
    /// sentinel) or by a payload family it proves on its own (<c>bool | None</c> as
    /// <c>true</c>/<c>false</c>/<c>null</c>). A user union lowers to closed case TYPES and an enum
    /// to a non-exhaustive integral — C# proves neither, bare or as the payload of <c>E | None</c>
    /// under member literals, so a trailing discard there is reachable by C#'s analysis and must be
    /// kept (the measured <c>Res</c> control).
    /// </summary>
    public static bool IsCSharpProvablyExhaustive(
        SemanticType scrutineeType,
        IEnumerable<(Pattern Pattern, Expression? Guard)> arms,
        SemanticInfo semanticInfo)
    {
        if (scrutineeType is NullableType nullable)
        {
            var covered = CollectUnguardedCoveredCases(arms, semanticInfo);
            if (!covered.Contains(WellKnownCaseNames.None))
                return false;
            return covered.Contains(WellKnownCaseNames.NullablePayload)
                || IsCSharpProvablyExhaustive(nullable.UnderlyingType, arms, semanticInfo);
        }

        bool isBool = scrutineeType is BuiltinType bt && bt == BuiltinType.Bool;
        if (!isBool && scrutineeType is not (ResultType or OptionalType))
            return false;

        return IsExhaustiveMatch(scrutineeType, arms, semanticInfo);
    }
}
