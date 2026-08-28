using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Analysis.ControlFlow;

/// <summary>
/// Forward dataflow analysis over a CFG to detect use-before-assign on bare-declared
/// local variables (<c>x: int</c> with no initializer). A variable is definitely assigned
/// at a program point only if it is assigned on ALL paths from the entry block to that point.
/// </summary>
internal static class DefiniteAssignmentAnalysis
{
    /// <summary>
    /// A use-before-assign violation: a bare-declared variable read before being definitely assigned.
    /// </summary>
    internal readonly record struct Violation(
        VariableDeclaration Declaration,
        Identifier ReadSite);

    /// <summary>
    /// Finds all use-before-assign violations for bare-declared local variables in the given CFG.
    /// </summary>
    public static IReadOnlyList<Violation> FindViolations(ControlFlowGraph cfg)
    {
        var bareDecls = new Dictionary<string, VariableDeclaration>();
        var assignedInBlock = new Dictionary<BasicBlock, HashSet<string>>();
        var readsInBlock = new Dictionary<BasicBlock, List<(string Name, Identifier Node, int StatementIndex)>>();

        foreach (var block in cfg.Blocks)
        {
            var blockAssigned = new HashSet<string>();
            var blockReads = new List<(string, Identifier, int)>();

            for (int i = 0; i < block.Statements.Count; i++)
            {
                var stmt = block.Statements[i];

                if (stmt is VariableDeclaration vd && vd.InitialValue == null && vd.Type != null)
                {
                    bareDecls.TryAdd(vd.Name, vd);
                }

                if (stmt is Assignment { Operator: AssignmentOperator.Assign } assignment)
                {
                    CollectAssignedNames(assignment.Target, blockAssigned);
                }

                CollectReads(stmt, blockReads, i);
            }

            foreach (var expr in block.Expressions)
            {
                CollectReadsFromExpr(expr, blockReads, block.Statements.Count);
            }

            if (block.Terminator is ConditionalBranchTerminator cbt)
            {
                CollectReadsFromExpr(cbt.Condition, blockReads, block.Statements.Count);
            }

            assignedInBlock[block] = blockAssigned;
            readsInBlock[block] = blockReads;
        }

        if (bareDecls.Count == 0)
            return Array.Empty<Violation>();

        var bareNames = new HashSet<string>(bareDecls.Keys);

        var outSets = new Dictionary<BasicBlock, HashSet<string>>();
        foreach (var block in cfg.Blocks)
        {
            outSets[block] = block == cfg.Entry
                ? new HashSet<string>()
                : new HashSet<string>(bareNames);
        }

        var rpo = cfg.GetReversePostOrder();
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var block in rpo)
            {
                if (block == cfg.Entry)
                    continue;

                var inSet = ComputeInSet(block, bareNames, outSets);
                if (inSet == null)
                    continue;

                var newOut = new HashSet<string>(inSet);
                newOut.UnionWith(assignedInBlock[block]);

                if (!newOut.SetEquals(outSets[block]))
                {
                    outSets[block] = newOut;
                    changed = true;
                }
            }
        }

        var violations = new List<Violation>();
        foreach (var block in cfg.Blocks)
        {
            if (block == cfg.Entry)
                continue;

            var definitelyAssigned = ComputeInSet(block, bareNames, outSets)
                ?? new HashSet<string>();

            var localAssigned = new HashSet<string>(definitelyAssigned);

            for (int i = 0; i < block.Statements.Count; i++)
            {
                var stmt = block.Statements[i];

                foreach (var (name, node, stmtIdx) in readsInBlock[block])
                {
                    if (stmtIdx == i && bareDecls.ContainsKey(name) && !localAssigned.Contains(name))
                    {
                        violations.Add(new Violation(bareDecls[name], node));
                    }
                }

                if (stmt is Assignment { Operator: AssignmentOperator.Assign } assignment)
                {
                    CollectAssignedNames(assignment.Target, localAssigned);
                }
            }

            foreach (var (name, node, stmtIdx) in readsInBlock[block])
            {
                if (stmtIdx == block.Statements.Count && bareDecls.ContainsKey(name) && !localAssigned.Contains(name))
                {
                    violations.Add(new Violation(bareDecls[name], node));
                }
            }
        }

        return violations;
    }

    private static HashSet<string>? ComputeInSet(BasicBlock block, HashSet<string> bareNames,
        Dictionary<BasicBlock, HashSet<string>> outSets)
    {
        if (block.ExceptionPredecessors.Count > 0)
            return new HashSet<string>();
        if (block.Predecessors.Count > 0)
        {
            var inSet = new HashSet<string>(bareNames);
            foreach (var pred in block.Predecessors)
                inSet.IntersectWith(outSets[pred]);
            return inSet;
        }
        return null;
    }

    private static void CollectAssignedNames(Expression target, HashSet<string> assigned)
    {
        switch (target)
        {
            case Identifier id:
                assigned.Add(id.Name);
                break;
            case TupleLiteral tuple:
                foreach (var element in tuple.Elements)
                    CollectAssignedNames(element, assigned);
                break;
            case StarExpression star:
                CollectAssignedNames(star.Operand, assigned);
                break;
            case Parenthesized paren:
                CollectAssignedNames(paren.Expression, assigned);
                break;
            case IndexAccess:
            case MemberAccess:
                break;
        }
    }

    private static void CollectReads(Statement stmt, List<(string, Identifier, int)> reads, int stmtIdx)
    {
        if (stmt is Assignment assign)
        {
            CollectReadsFromExpr(assign.Value, reads, stmtIdx);
            CollectTargetReads(assign.Target, reads, stmtIdx);
            return;
        }

        foreach (var child in stmt.GetChildNodes())
        {
            if (child is Expression expr)
                CollectReadsFromExpr(expr, reads, stmtIdx);
        }
    }

    private static void CollectTargetReads(Expression target, List<(string, Identifier, int)> reads, int stmtIdx)
    {
        switch (target)
        {
            case Identifier:
                break;
            case TupleLiteral tuple:
                foreach (var element in tuple.Elements)
                    CollectTargetReads(element, reads, stmtIdx);
                break;
            case StarExpression star:
                CollectTargetReads(star.Operand, reads, stmtIdx);
                break;
            case Parenthesized paren:
                CollectTargetReads(paren.Expression, reads, stmtIdx);
                break;
            default:
                CollectReadsFromExpr(target, reads, stmtIdx);
                break;
        }
    }

    private static void CollectReadsFromExpr(Expression expr, List<(string, Identifier, int)> reads, int stmtIdx)
    {
        if (expr is Identifier id)
        {
            reads.Add((id.Name, id, stmtIdx));
            return;
        }

        if (expr is LambdaExpression)
            return;

        foreach (var child in expr.GetChildNodes())
        {
            if (child is Expression childExpr)
                CollectReadsFromExpr(childExpr, reads, stmtIdx);
        }
    }
}
