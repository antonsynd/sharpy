using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.CodeGen;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Instrument verification for <see cref="EmittedTreePrecedence"/> (plan-683c8b Decision 2, contract §5):
/// the hand-written precedence table is trusted only because Roslyn's own parser agrees with it.
/// For an expression tree <c>e</c>, <c>Violations(e)</c> is empty <b>iff</b> reparsing
/// <c>e.NormalizeWhitespace().ToFullString()</c> yields the same kind-shape (node kinds, recursively,
/// over expression children). A disagreement in either direction is a table bug — or, when
/// <c>Violations</c> is empty and the shape still differs by a non-precedence mechanism (token
/// adjacency such as <c>- -x</c> printing as <c>--x</c>, the generic-call ambiguity
/// <c>a &lt; b &gt; (c)</c>), a sibling class of the same contract to file, not to absorb into the table.
/// </summary>
internal static class PrecedenceCrossCheck
{
    /// <summary>
    /// Structural kind-shape equality with the one benign normalization the reparse guard already
    /// documents: the emitter builds qualified callees as <c>QualifiedName</c>, the parser as
    /// <c>SimpleMemberAccessExpression</c>; they bind identically.
    /// </summary>
    public static bool KindShapeMatches(SyntaxNode original, SyntaxNode reparsed)
    {
        // The emitter spells a negative constant as ONE numeric-literal token (`-1`); the parser
        // always reads it as unary minus over a literal. Same program, different node shape.
        if (IsNegativeLiteralToken(original) && IsUnaryMinusOverLiteral(reparsed))
            return true;
        if (IsNegativeLiteralToken(reparsed) && IsUnaryMinusOverLiteral(original))
            return true;

        if (NormalizeKind(original.Kind()) != NormalizeKind(reparsed.Kind()))
            return false;

        var origChildren = original.ChildNodes().OfType<ExpressionSyntax>().ToList();
        var reparsedChildren = reparsed.ChildNodes().OfType<ExpressionSyntax>().ToList();
        if (origChildren.Count != reparsedChildren.Count)
            return false;

        for (int i = 0; i < origChildren.Count; i++)
        {
            if (!KindShapeMatches(origChildren[i], reparsedChildren[i]))
                return false;
        }

        return true;
    }

    private static SyntaxKind NormalizeKind(SyntaxKind kind) => kind switch
    {
        // Emitter-built qualified callees vs parser-built member access: bind identically.
        SyntaxKind.QualifiedName => SyntaxKind.SimpleMemberAccessExpression,
        // `new T() { }` — the emitter tags an empty initializer as a collection initializer, the
        // parser as an object initializer; both are the same empty brace pair.
        SyntaxKind.CollectionInitializerExpression or SyntaxKind.ArrayInitializerExpression
            or SyntaxKind.ComplexElementInitializerExpression => SyntaxKind.ObjectInitializerExpression,
        _ => kind,
    };

    private static bool IsNegativeLiteralToken(SyntaxNode node) =>
        node is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NumericLiteralExpression } lit
        && lit.Token.Text.StartsWith('-');

    private static bool IsUnaryMinusOverLiteral(SyntaxNode node) =>
        node is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.UnaryMinusExpression } prefix
        && prefix.Operand is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NumericLiteralExpression };

    /// <summary>
    /// Every full-expression slot in <paramref name="root"/> (the value of an expression statement,
    /// initializer, return, condition, argument, arrow body, interpolation hole, parenthesis, switch
    /// arm, throw, yield or iteration source) on which the table and Roslyn's parser disagree. Each
    /// line names the direction: <c>table-lax</c> (no violation reported, yet the reparse changed the
    /// shape — precedence rule missing, or a token-adjacency sibling) or <c>table-strict</c>
    /// (a violation reported, yet the reparse kept the shape — the rule is stricter than the grammar).
    /// </summary>
    public static IReadOnlyList<string> Disagreements(SyntaxNode root)
    {
        var lines = new List<string>();
        foreach (var expr in root.DescendantNodes().OfType<ExpressionSyntax>())
        {
            if (!IsFullExpressionSlot(expr))
                continue;

            // Leading trivia carries the statement's #line directive; it is not part of the expression.
            var text = expr.WithoutTrivia().NormalizeWhitespace().ToFullString();
            var reparsed = SyntaxFactory.ParseExpression(text);
            if (reparsed.ContainsDiagnostics)
                continue; // not an expression Roslyn can stand alone (e.g. a lambda body fragment) — the binding diff covers it

            bool hasViolations = EmittedTreePrecedence.Violations(expr).Count > 0;
            bool sameShape = KindShapeMatches(expr, reparsed);
            if (hasViolations == sameShape)
            {
                int line = expr.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                var flat = text.Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
                if (flat.Length > 140)
                    flat = flat.Substring(0, 140) + "...";
                lines.Add($"{(hasViolations ? "table-strict" : "table-lax")} at line {line}: {expr.Kind()} `{flat}`");
            }
        }
        return lines;
    }

    private static bool IsFullExpressionSlot(ExpressionSyntax expr) => expr.Parent switch
    {
        ExpressionStatementSyntax => true,
        EqualsValueClauseSyntax => true,
        ReturnStatementSyntax => true,
        ThrowStatementSyntax => true,
        YieldStatementSyntax => true,
        IfStatementSyntax ifs => ifs.Condition == expr,
        WhileStatementSyntax ws => ws.Condition == expr,
        DoStatementSyntax ds => ds.Condition == expr,
        ForEachStatementSyntax fe => fe.Expression == expr,
        ArgumentSyntax => true,
        ArrowExpressionClauseSyntax => true,
        InterpolationSyntax i => i.Expression == expr,
        ParenthesizedExpressionSyntax => true,
        SwitchExpressionArmSyntax arm => arm.Expression == expr,
        InitializerExpressionSyntax => true,
        _ => false,
    };
}
