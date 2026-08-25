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

                HashSet<string> inSet;
                if (block.ExceptionPredecessors.Count > 0)
                {
                    inSet = new HashSet<string>();
                }
                else if (block.Predecessors.Count > 0)
                {
                    inSet = new HashSet<string>(bareNames);
                    foreach (var pred in block.Predecessors)
                        inSet.IntersectWith(outSets[pred]);
                }
                else
                {
                    continue;
                }

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

            HashSet<string> definitelyAssigned;
            if (block.Predecessors.Count > 0)
            {
                definitelyAssigned = new HashSet<string>(bareNames);
                foreach (var pred in block.Predecessors)
                    definitelyAssigned.IntersectWith(outSets[pred]);
            }
            else
            {
                definitelyAssigned = new HashSet<string>();
            }

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
        }

        return violations;
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
        }
    }

    private static void CollectReads(Statement stmt, List<(string, Identifier, int)> reads, int stmtIdx)
    {
        switch (stmt)
        {
            case ExpressionStatement exprStmt:
                CollectReadsFromExpr(exprStmt.Expression, reads, stmtIdx);
                break;
            case ReturnStatement ret when ret.Value != null:
                CollectReadsFromExpr(ret.Value, reads, stmtIdx);
                break;
            case Assignment assign:
                CollectReadsFromExpr(assign.Value, reads, stmtIdx);
                break;
            case VariableDeclaration vd when vd.InitialValue != null:
                CollectReadsFromExpr(vd.InitialValue, reads, stmtIdx);
                break;
            case AssertStatement assert:
                CollectReadsFromExpr(assert.Test, reads, stmtIdx);
                if (assert.Message != null)
                    CollectReadsFromExpr(assert.Message, reads, stmtIdx);
                break;
        }
    }

    private static void CollectReadsFromExpr(Expression expr, List<(string, Identifier, int)> reads, int stmtIdx)
    {
        switch (expr)
        {
            case Identifier id:
                reads.Add((id.Name, id, stmtIdx));
                break;
            case FunctionCall call:
                CollectReadsFromExpr(call.Function, reads, stmtIdx);
                foreach (var arg in call.Arguments)
                    CollectReadsFromExpr(arg, reads, stmtIdx);
                foreach (var kwarg in call.KeywordArguments)
                    CollectReadsFromExpr(kwarg.Value, reads, stmtIdx);
                break;
            case BinaryOp bin:
                CollectReadsFromExpr(bin.Left, reads, stmtIdx);
                CollectReadsFromExpr(bin.Right, reads, stmtIdx);
                break;
            case UnaryOp un:
                CollectReadsFromExpr(un.Operand, reads, stmtIdx);
                break;
            case MemberAccess ma:
                CollectReadsFromExpr(ma.Object, reads, stmtIdx);
                break;
            case IndexAccess ia:
                CollectReadsFromExpr(ia.Object, reads, stmtIdx);
                CollectReadsFromExpr(ia.Index, reads, stmtIdx);
                break;
            case ConditionalExpression cond:
                CollectReadsFromExpr(cond.Test, reads, stmtIdx);
                CollectReadsFromExpr(cond.ThenValue, reads, stmtIdx);
                CollectReadsFromExpr(cond.ElseValue, reads, stmtIdx);
                break;
            case Parenthesized paren:
                CollectReadsFromExpr(paren.Expression, reads, stmtIdx);
                break;
        }
    }
}
