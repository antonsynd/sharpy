using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;

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

        var missingCases = GetMissingCases(scrutineeType, matchStmt.Cases.Select(c => (c.Pattern, c.Guard)));
        if (missingCases == null)
            return; // Not a finite type or already exhaustive

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

        var missingCases = GetMissingCases(scrutineeType, matchExpr.Arms.Select(a => (a.Pattern, a.Guard)));
        if (missingCases == null)
            return; // Not a finite type or already exhaustive

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
        var allCases = GetFiniteTypeCases(scrutineeType);
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

            CollectCoveredCases(pattern, scrutineeType, coveredCases);
        }

        if (hasWildcard)
            return new List<string>(); // Wildcard covers everything

        var missing = allCases.Where(c => !coveredCases.Contains(c)).ToList();
        return missing;
    }

    /// <summary>
    /// Gets all possible case names for a finite type, or null if the type is not finite.
    /// </summary>
    private List<string>? GetFiniteTypeCases(SemanticType scrutineeType)
    {
        // Bool type: True and False
        if (scrutineeType is BuiltinType bt && bt == BuiltinType.Bool)
        {
            return new List<string> { "True", "False" };
        }

        // Enum type: all enum member names
        if (scrutineeType is UserDefinedType udt && udt.Symbol?.TypeKind == TypeKind.Enum)
        {
            return udt.Symbol.Fields.Select(f => f.Name).ToList();
        }

        // Tagged union type (non-generic): all union case names
        if (scrutineeType is UserDefinedType unionUdt && unionUdt.Symbol?.TypeKind == TypeKind.Union)
        {
            return unionUdt.Symbol.UnionCases.Select(c => c.Name).ToList();
        }

        // Tagged union type (generic): all union case names
        if (scrutineeType is GenericType gt && gt.GenericDefinition?.TypeKind == TypeKind.Union)
        {
            return gt.GenericDefinition.UnionCases.Select(c => c.Name).ToList();
        }

        return null; // Not a finite type
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
    /// Collects the case names covered by a pattern into the provided set.
    /// </summary>
    private void CollectCoveredCases(Pattern pattern, SemanticType scrutineeType, HashSet<string> covered)
    {
        switch (pattern)
        {
            case LiteralPattern literal:
                // For bools: True/False literals
                if (literal.Literal is BooleanLiteral boolLit)
                {
                    covered.Add(boolLit.Value ? "True" : "False");
                }
                break;

            case MemberAccessPattern memberAccess:
                // For enums: Color.RED -> "RED"
                // For union unit cases: Option.None -> "None"
                if (memberAccess.Parts.Length >= 2)
                {
                    // The resolved union case from SemanticInfo takes priority
                    var unionCase = _context.SemanticInfo.GetPatternUnionCase(memberAccess);
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
                // For union cases: Ok(v) -> "Ok"
                var posUnionCase = _context.SemanticInfo.GetPatternUnionCase(positionalPattern);
                if (posUnionCase != null)
                {
                    covered.Add(posUnionCase.Name);
                }
                else if (positionalPattern.Type != null)
                {
                    // Try to resolve as union case from type name
                    var typeName = positionalPattern.Type.Name;
                    if (typeName.Contains('.'))
                    {
                        var parts = typeName.Split('.');
                        covered.Add(parts[^1]);
                    }
                    else
                    {
                        covered.Add(typeName);
                    }
                }
                break;

            case TypePattern typePattern:
                // Type patterns can match union cases
                covered.Add(typePattern.Type.Name);
                break;

            case OrPattern orPattern:
                // Or pattern covers the union of its alternatives
                foreach (var alt in orPattern.Alternatives)
                {
                    CollectCoveredCases(alt, scrutineeType, covered);
                }
                break;
        }
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
