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

        // Type subsumption: an earlier UNGUARDED arm that matches every value of its recorded type
        // makes any later arm whose recorded type is contained in it unreachable (#1651 sibling,
        // #1672). Fully irrefutable patterns are reported by the loop above; this loop catches the
        // arm that is total only WITHIN its own type — `case int():` over an `object` scrutinee
        // subsumes a later `case 99:`, which reached the C# compiler as CS8120 behind SPY0908.
        //
        // Totality against the SCRUTINEE is deliberately not the gate: `case int():` over `object`
        // is not total, which is exactly why these arms went unreported. The requirement is that the
        // earlier pattern refutes on its TYPE ALONE (CoversItsRecordedType) — a literal or a
        // refutable sub-pattern also refutes on a value, and then the later arm stays reachable.
        for (int i = 0; i < arms.Count - 1; i++)
        {
            var (pattern, guard) = arms[i];
            if (guard != null) continue;
            if (ExhaustivenessHelper.IsIrrefutable(pattern, context.SemanticInfo)) continue;
            if (!CoversItsRecordedType(pattern, context.SemanticInfo)) continue;

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
                        $"This arm is unreachable: an earlier arm matches every "
                        + $"'{earlierType.GetDisplayName()}', which covers this "
                        + $"'{laterType!.GetDisplayName()}' pattern. Move this arm before it, or "
                        + "guard the earlier arm",
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

    /// <summary>
    /// Whether the pattern matches EVERY value of the type recorded for it — i.e. its only ground
    /// for refusing a value is the type test itself.
    /// <para>
    /// This is the subsumption rule's left-hand side. <c>case int():</c> and <c>case int(n):</c>
    /// qualify (the sub-pattern binds, it does not filter), <c>case Box(a, b):</c> qualifies, and
    /// <c>case 99:</c>, <c>case int(99):</c> and <c>case Box(1, b):</c> do NOT — each refutes on a
    /// value as well as on a type, so a later arm of the same type is still reachable.
    /// </para>
    /// </summary>
    private static bool CoversItsRecordedType(Pattern pattern, SemanticInfo? info)
    {
        return pattern switch
        {
            TypePattern => true,
            AsPattern asp => CoversItsRecordedType(asp.Inner, info),
            PositionalPattern pp =>
                pp.Elements.All(e => ExhaustivenessHelper.IsIrrefutable(e, info)),
            PropertyPattern prop => prop.Fields.Length == 0,
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

    /// <summary>
    /// Whether every value the later arm can match is already matched by the earlier arm.
    /// <para>
    /// A pattern is a RUNTIME type test, so containment here is exact for a builtin: <c>case
    /// float():</c> does not match a boxed <c>int</c>, even though <c>int</c> is implicitly
    /// convertible to <c>float</c> and therefore assignable to it. Using assignability for that
    /// pair would refuse <c>case float(): … case 1:</c>, which runs and prints the literal's arm
    /// (verified against python3 3.12, which prints the same). Reference types keep assignability,
    /// which is where inheritance lives.
    /// </para>
    /// </summary>
    private static bool TypeSubsumes(SemanticType? earlierType, SemanticType? laterType)
    {
        if (earlierType == null || laterType == null)
            return false;
        if (earlierType is UnknownType || laterType is UnknownType)
            return false;
        if (earlierType is BuiltinType || laterType is BuiltinType)
            return earlierType.Equals(laterType);
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
