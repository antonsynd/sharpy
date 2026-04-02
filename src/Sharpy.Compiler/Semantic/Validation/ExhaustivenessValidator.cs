using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Validates that match statements and expressions are exhaustive for enum, bool, and tagged union scrutinee types.
/// Match statements emit a warning (SPY0463) for non-exhaustive matches;
/// match expressions emit an error (SPY0416) since they must produce a value.
/// </summary>
internal class ExhaustivenessValidator : SemanticValidatorBase
{
    public override string Name => "ExhaustivenessValidator";
    public override int Order => 405; // After ControlFlowValidator (400), before PropertyValidator (410)

    private SemanticContext _context = null!;
    private ICompilerLogger _logger = NullLogger.Instance;

    public override void Validate(Module module, SemanticContext context)
    {
        _context = context;
        _logger = context.Logger;
        _logger.LogDebug("Starting exhaustiveness validation");

        var visitor = new MatchCollector();
        visitor.Visit(module);

        foreach (var matchStmt in visitor.MatchStatements)
        {
            CheckMatchStatementExhaustiveness(matchStmt);
        }

        foreach (var matchExpr in visitor.MatchExpressions)
        {
            CheckMatchExpressionExhaustiveness(matchExpr);
        }
    }

    private void CheckMatchStatementExhaustiveness(MatchStatement matchStmt)
    {
        var scrutineeType = _context.SemanticInfo.GetExpressionType(matchStmt.Scrutinee);
        if (scrutineeType == null || scrutineeType is UnknownType)
            return;

        var casePairs = matchStmt.Cases.Select(c => (c.Pattern, c.Guard)).ToList();
        var missingCases = GetMissingCases(scrutineeType, casePairs);
        if (missingCases == null)
        {
            // Non-finite type: warn if no arm is unconditionally exhaustive
            if (!HasUnconditionallyExhaustiveArm(casePairs))
            {
                AddWarning(_context,
                    $"Match statement on non-finite type '{scrutineeType.GetDisplayName()}' has no wildcard or binding pattern to ensure exhaustiveness",
                    matchStmt.LineStart, matchStmt.ColumnStart,
                    code: DiagnosticCodes.Validation.NonExhaustiveMatch,
                    span: matchStmt.Span);
            }
            return;
        }

        if (missingCases.Count > 0)
        {
            var caseList = string.Join(", ", missingCases);
            AddWarning(_context,
                $"Match statement is not exhaustive. Missing cases: {caseList}",
                matchStmt.LineStart, matchStmt.ColumnStart,
                code: DiagnosticCodes.Validation.NonExhaustiveMatch,
                span: matchStmt.Span);
        }
    }

    private void CheckMatchExpressionExhaustiveness(MatchExpression matchExpr)
    {
        var scrutineeType = _context.SemanticInfo.GetExpressionType(matchExpr.Scrutinee);
        if (scrutineeType == null || scrutineeType is UnknownType)
            return;

        var armPairs = matchExpr.Arms.Select(a => (a.Pattern, a.Guard)).ToList();
        var missingCases = GetMissingCases(scrutineeType, armPairs);
        if (missingCases == null)
        {
            // Non-finite type: error if no arm is unconditionally exhaustive
            if (!HasUnconditionallyExhaustiveArm(armPairs))
            {
                AddError(_context,
                    $"Match expression on non-finite type '{scrutineeType.GetDisplayName()}' must have a wildcard or binding pattern to ensure exhaustiveness",
                    matchExpr.LineStart, matchExpr.ColumnStart,
                    code: DiagnosticCodes.Validation.NonExhaustiveMatchExpression,
                    span: matchExpr.Span);
            }
            return;
        }

        if (missingCases.Count > 0)
        {
            var caseList = string.Join(", ", missingCases);
            AddError(_context,
                $"Match expression is not exhaustive. Missing cases: {caseList}",
                matchExpr.LineStart, matchExpr.ColumnStart,
                code: DiagnosticCodes.Validation.NonExhaustiveMatchExpression,
                span: matchExpr.Span);
        }
    }

    /// <summary>
    /// Determines which cases are missing from a match over a finite type.
    /// Returns null if the scrutinee is not a finite type (enum, bool, union).
    /// Returns an empty list if the match is exhaustive.
    /// </summary>
    private List<string>? GetMissingCases(
        SemanticType scrutineeType,
        IEnumerable<(Pattern Pattern, Expression? Guard)> cases)
    {
        // Get the set of all possible case names for this type
        var allCases = ExhaustivenessHelper.GetFiniteTypeCases(scrutineeType);
        if (allCases == null)
            return null; // Not a finite type

        var coveredCases = new HashSet<string>();
        bool hasWildcard = false;

        foreach (var (pattern, guard) in cases)
        {
            // Guarded patterns are not considered exhaustive
            if (guard != null)
                continue;

            if (PatternCoversAll(pattern))
            {
                hasWildcard = true;
                break;
            }

            ExhaustivenessHelper.CollectCoveredCases(pattern, _context.SemanticInfo, coveredCases);
        }

        if (hasWildcard)
            return new List<string>(); // Wildcard covers everything

        var missing = allCases.Where(c => !coveredCases.Contains(c)).ToList();
        return missing;
    }

    /// <summary>
    /// Returns true if a pattern unconditionally covers all values (wildcard or binding).
    /// </summary>
    private bool PatternCoversAll(Pattern pattern)
    {
        return pattern switch
        {
            WildcardPattern => true,
            BindingPattern => true,
            OrPattern or => or.Alternatives.Any(PatternCoversAll),
            _ => false
        };
    }

    /// <summary>
    /// Returns true if at least one arm has an unconditionally exhaustive pattern
    /// (wildcard or binding) without a guard condition.
    /// </summary>
    private bool HasUnconditionallyExhaustiveArm(
        IEnumerable<(Pattern Pattern, Expression? Guard)> arms)
    {
        return arms.Any(a => a.Guard == null && PatternCoversAll(a.Pattern));
    }

    /// <summary>
    /// AstVisitor that collects all MatchStatement and MatchExpression nodes.
    /// </summary>
    private class MatchCollector : AstVisitor
    {
        public List<MatchStatement> MatchStatements { get; } = new();
        public List<MatchExpression> MatchExpressions { get; } = new();

        public override void VisitMatchStatement(MatchStatement node)
        {
            MatchStatements.Add(node);
            DefaultVisit(node); // Continue traversal to find nested matches
        }

        public override void VisitMatchExpression(MatchExpression node)
        {
            MatchExpressions.Add(node);
            DefaultVisit(node); // Continue traversal to find nested matches
        }
    }
}
