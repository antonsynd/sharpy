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

            case PositionalPattern positionalPattern:
                var posUnionCase = semanticInfo.GetPatternUnionCase(positionalPattern);
                if (posUnionCase != null)
                {
                    covered.Add(posUnionCase.Name);
                }
                break;

            case TypePattern typePattern:
                var typeUnionCase = semanticInfo.GetPatternUnionCase(typePattern);
                if (typeUnionCase != null)
                {
                    covered.Add(typeUnionCase.Name);
                }
                else
                {
                    covered.Add(typePattern.Type.Name);
                }
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
