using System.Collections.Immutable;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Validates PEP 572 restrictions on walrus expressions in comprehensions:
/// - Walrus in comprehension iterable position (SPY0704)
/// - Walrus rebinding the iteration variable (SPY0704)
/// </summary>
internal class WalrusPositionValidator : ValidatingAstWalker
{
    public override string Name => "WalrusPositionValidator";
    public override int Order => 408;

    private void CheckComprehension(ImmutableArray<ComprehensionClause> clauses, params Expression[] elements)
    {
        var allTargetNames = new HashSet<string>();

        foreach (var clause in clauses)
        {
            if (clause is ForClause forClause)
            {
                if (ContainsWalrus(forClause.Iterator))
                {
                    AddError(
                        "Assignment expression cannot be used in a comprehension iterable expression",
                        forClause.Iterator.LineStart, forClause.Iterator.ColumnStart,
                        DiagnosticCodes.ValidationOverflow.WalrusInProhibitedPosition);
                }

                CollectTargetNamesRecursive(forClause.Target, allTargetNames);
            }
        }

        if (allTargetNames.Count > 0)
        {
            foreach (var element in elements)
                CheckWalrusRebindingInNode(element, allTargetNames);

            foreach (var clause in clauses)
                CheckWalrusRebindingInNode(clause, allTargetNames);
        }
    }

    private static void CollectTargetNamesRecursive(Expression target, HashSet<string> names)
    {
        if (target is Identifier id)
            names.Add(id.Name);
        else if (target is TupleLiteral tuple)
        {
            foreach (var elem in tuple.Elements)
                CollectTargetNamesRecursive(elem, names);
        }
        else if (target is StarExpression star)
            CollectTargetNamesRecursive(star.Operand, names);
    }

    private void CheckWalrusRebindingInNode(Node node, HashSet<string> targetNames)
    {
        if (node is WalrusExpression walrus && targetNames.Contains(walrus.Target))
        {
            AddError(
                $"Assignment expression cannot rebind comprehension iteration variable '{walrus.Target}'",
                walrus.LineStart, walrus.ColumnStart,
                DiagnosticCodes.ValidationOverflow.WalrusInProhibitedPosition);
            return;
        }

        if (node is ForClause)
            return;

        foreach (var child in node.GetChildNodes())
            CheckWalrusRebindingInNode(child, targetNames);
    }

    private static bool ContainsWalrus(Expression expr)
    {
        if (expr is WalrusExpression)
            return true;

        foreach (var child in expr.GetChildNodes())
        {
            if (child is Expression childExpr && ContainsWalrus(childExpr))
                return true;
        }
        return false;
    }

    public override void VisitListComprehension(ListComprehension node)
    {
        CheckComprehension(node.Clauses, node.Element);
        base.VisitListComprehension(node);
    }

    public override void VisitSetComprehension(SetComprehension node)
    {
        CheckComprehension(node.Clauses, node.Element);
        base.VisitSetComprehension(node);
    }

    public override void VisitDictComprehension(DictComprehension node)
    {
        CheckComprehension(node.Clauses, node.Key, node.Value);
        base.VisitDictComprehension(node);
    }

    public override void VisitGeneratorExpression(GeneratorExpression node)
    {
        CheckComprehension(node.Clauses, node.Element);
        base.VisitGeneratorExpression(node);
    }
}
