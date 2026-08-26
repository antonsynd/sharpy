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

            var description = ExhaustivenessHelper.DescribeIrrefutable(pattern, context.SemanticInfo)
                ?? "pattern";
            AddError(
                context,
                $"{description} makes remaining patterns unreachable",
                pattern.LineStart, pattern.ColumnStart,
                code: DiagnosticCodes.ValidationOverflow.IrrefutablePatternNotLast,
                span: pattern.Span);
        }
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
