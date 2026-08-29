using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic.Validation;

internal class MatchArmOrderValidator : SemanticValidatorBase
{
    public override string Name => "MatchArmOrderValidator";
    public override int Order => 406;

    public override void Validate(Module module, SemanticContext context)
    {
        var visitor = new MatchCollector();
        visitor.Visit(module);

        foreach (var matchStmt in visitor.MatchStatements)
        {
            CheckArms(
                context,
                matchStmt.Cases.Select(c => (c.Pattern, c.Guard)).ToList());
        }

        foreach (var matchExpr in visitor.MatchExpressions)
        {
            CheckArms(
                context,
                matchExpr.Arms.Select(a => (a.Pattern, a.Guard)).ToList());
        }
    }

    private void CheckArms(
        SemanticContext context,
        IReadOnlyList<(Pattern Pattern, Expression? Guard)> arms)
    {
        for (int i = 0; i < arms.Count - 1; i++)
        {
            var (pattern, guard) = arms[i];
            if (guard != null)
                continue;
            if (!ExhaustivenessHelper.IsIrrefutable(pattern, context.SemanticInfo))
                continue;

            bool isTotalTypePattern = IsTypeTotalPattern(pattern, context.SemanticInfo);
            if (isTotalTypePattern)
            {
                bool hasRefutableFollower = false;
                for (int j = i + 1; j < arms.Count; j++)
                {
                    if (arms[j].Guard != null
                        || !ExhaustivenessHelper.IsIrrefutable(arms[j].Pattern, context.SemanticInfo))
                    {
                        hasRefutableFollower = true;
                        break;
                    }
                }
                if (!hasRefutableFollower)
                    continue;
            }

            var description = ExhaustivenessHelper.DescribeIrrefutable(pattern, context.SemanticInfo)
                ?? "pattern";
            AddError(
                context,
                $"{description} makes remaining patterns unreachable",
                pattern.LineStart, pattern.ColumnStart,
                code: DiagnosticCodes.ValidationOverflow.IrrefutablePatternNotLast,
                span: pattern.Span);
        }

        // Type subsumption: an unguarded total type pattern whose recorded type contains
        // a later unguarded arm's recorded type (#1651 sibling). Patterns that are fully
        // irrefutable are handled above; this catches partial totality (e.g., `case int()`
        // over an `object` scrutinee subsumes `case 99:`).
        for (int i = 0; i < arms.Count - 1; i++)
        {
            var (pattern, guard) = arms[i];
            if (guard != null) continue;
            if (ExhaustivenessHelper.IsIrrefutable(pattern, context.SemanticInfo)) continue;
            if (!IsTypeTotalPattern(pattern, context.SemanticInfo)) continue;

            var earlierType = GetPatternRecordedType(pattern, context.SemanticInfo);
            if (earlierType == null) continue;

            for (int j = i + 1; j < arms.Count; j++)
            {
                if (arms[j].Guard != null) continue;
                var laterType = GetPatternRecordedType(arms[j].Pattern, context.SemanticInfo);
                if (TypeSubsumes(earlierType, laterType))
                {
                    AddError(
                        context,
                        $"Type '{earlierType.GetDisplayName()}' in earlier arm subsumes '{laterType!.GetDisplayName()}' pattern",
                        arms[j].Pattern.LineStart, arms[j].Pattern.ColumnStart,
                        code: DiagnosticCodes.ValidationOverflow.IrrefutablePatternNotLast,
                        span: arms[j].Pattern.Span);
                }
            }
        }
    }

    private static bool IsTypeTotalPattern(Pattern pattern, SemanticInfo? info)
    {
        return pattern switch
        {
            TypePattern tp => info?.GetPatternTotality(tp) == true,
            AsPattern { Inner: TypePattern tp } => info?.GetPatternTotality(tp) == true,
            PositionalPattern pp => info?.GetPatternTotality(pp) == true,
            AsPattern { Inner: PositionalPattern pp } => info?.GetPatternTotality(pp) == true,
            _ => false
        };
    }

    private static SemanticType? GetPatternRecordedType(Pattern pattern, SemanticInfo? info)
    {
        return pattern switch
        {
            TypePattern tp => info?.GetPatternType(tp),
            AsPattern asp => info?.GetPatternType(asp),
            PositionalPattern pp => info?.GetPatternType(pp),
            LiteralPattern lp => info?.GetPatternType(lp),
            _ => null
        };
    }

    private static bool TypeSubsumes(SemanticType? earlierType, SemanticType? laterType)
    {
        if (earlierType == null || laterType == null)
            return false;
        return laterType.IsAssignableTo(earlierType);
    }

    private class MatchCollector : AstVisitor
    {
        public List<MatchStatement> MatchStatements { get; } = new();
        public List<MatchExpression> MatchExpressions { get; } = new();

        public override void VisitMatchStatement(MatchStatement node)
        {
            MatchStatements.Add(node);
            DefaultVisit(node);
        }

        public override void VisitMatchExpression(MatchExpression node)
        {
            MatchExpressions.Add(node);
            DefaultVisit(node);
        }
    }
}
