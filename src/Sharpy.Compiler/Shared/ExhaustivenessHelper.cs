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
                => info?.GetPatternTotality(head.Lodge) == true,
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
            _ when PatternHead.TryGet(pattern, out var head) && info?.GetPatternTotality(head.Lodge) == true
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

        var coveredCases = new HashSet<string>();
        foreach (var (pattern, guard) in arms)
        {
            // Guarded arms don't guarantee coverage
            if (guard != null)
                continue;

            CollectCoveredCases(pattern, semanticInfo, coveredCases);
        }

        return allCases.All(coveredCases.Contains);
    }
}
