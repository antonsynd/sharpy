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
    /// Returns the bare declarations (<c>x: int</c>, no initializer) found in the CFG,
    /// keyed by variable name.
    /// </summary>
    public static IReadOnlyDictionary<string, VariableDeclaration> FindBareDeclarations(ControlFlowGraph cfg)
    {
        var bareDecls = new Dictionary<string, VariableDeclaration>();
        foreach (var block in cfg.Blocks)
        {
            foreach (var stmt in block.Statements)
            {
                if (stmt is VariableDeclaration vd && vd.InitialValue == null && vd.Type != null)
                    bareDecls.TryAdd(vd.Name, vd);
            }
        }
        return bareDecls;
    }

    /// <summary>
    /// Finds all use-before-assign violations for bare-declared local variables in the given CFG.
    /// </summary>
    public static IReadOnlyList<Violation> FindViolations(ControlFlowGraph cfg)
    {
        var bareDecls = new Dictionary<string, VariableDeclaration>();
        var assignedInBlock = new Dictionary<BasicBlock, HashSet<string>>();
        var readsInBlock = new Dictionary<BasicBlock, List<(string Name, Identifier Node, int StatementIndex)>>();
        // Reads inside lambda bodies are not flow-positioned: the lambda may run after a later
        // assignment (`f = lambda: x; x = 7; f()` is legal Python). They are judged once, at the
        // end, against "is this local assigned ANYWHERE in the function" (#1635).
        var lambdaReads = new List<(string Name, Identifier Node)>();
        var edgeWalrus = new Dictionary<BasicBlock, (HashSet<string> WhenTrue, HashSet<string> WhenFalse)>();
        var declaredNames = new HashSet<string>();

        foreach (var block in cfg.Blocks)
        {
            var blockAssigned = new HashSet<string>();
            var blockReads = new List<(string, Identifier, int)>();

            // A block entered by rebinding its binder (for-target, with-as) assigns those names
            // before its first statement runs (#1635 write kinds).
            foreach (var key in block.EntryRebinds)
                blockAssigned.Add(key);

            for (int i = 0; i < block.Statements.Count; i++)
            {
                var stmt = block.Statements[i];

                if (stmt is VariableDeclaration vd)
                {
                    declaredNames.Add(vd.Name);
                    if (vd.InitialValue == null && vd.Type != null)
                        bareDecls.TryAdd(vd.Name, vd);
                }

                if (stmt is Assignment { Operator: AssignmentOperator.Assign } assignment)
                {
                    CollectAssignedNames(assignment.Target, blockAssigned);
                }

                CollectWalrusBareDecls(stmt, bareDecls, declaredNames);
                CollectWalrusTargets(stmt, blockAssigned);
                CollectReads(stmt, blockReads, i, lambdaReads);
            }

            foreach (var expr in block.Expressions)
            {
                CollectWalrusBareDecls(expr, bareDecls, declaredNames);
                CollectWalrusTargets(expr, blockAssigned);
                CollectReadsFromExpr(expr, blockReads, block.Statements.Count, lambdaReads);
            }

            if (block.Terminator is ConditionalBranchTerminator cbt)
            {
                CollectWalrusBareDecls(cbt.Condition, bareDecls, declaredNames);
                var (wTrue, wFalse) = ComputeWalrusWhenTrueFalse(cbt.Condition);
                var unconditional = new HashSet<string>(wTrue);
                unconditional.IntersectWith(wFalse);
                blockAssigned.UnionWith(unconditional);
                if (wTrue.Count > unconditional.Count || wFalse.Count > unconditional.Count)
                    edgeWalrus[block] = (wTrue, wFalse);

                CollectReadsFromExpr(cbt.Condition, blockReads, block.Statements.Count, lambdaReads);
            }

            assignedInBlock[block] = blockAssigned;
            readsInBlock[block] = blockReads;
        }

        if (bareDecls.Count == 0)
            return Array.Empty<Violation>();

        var bareNames = new HashSet<string>(bareDecls.Keys);

        var inSets = MustAssignDataflow.InitializeSets(cfg, bareNames);
        var outSets = MustAssignDataflow.InitializeSets(cfg, bareNames);

        var rpo = cfg.GetReversePostOrder();
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var block in rpo)
            {
                if (block == cfg.Entry)
                    continue;

                var inSet = MustAssignDataflow.ComputeInSet(block, bareNames, inSets, outSets,
                    edgeWalrus.Count > 0 ? edgeWalrus : null);
                if (inSet == null)
                    continue;

                // An exception successor reads THIS block's in-set, so a change here must
                // re-run the fixpoint even when the out-set is unchanged.
                if (!inSet.SetEquals(inSets[block]))
                {
                    inSets[block] = inSet;
                    changed = true;
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

            var definitelyAssigned = MustAssignDataflow.ComputeInSet(block, bareNames, inSets, outSets,
                edgeWalrus.Count > 0 ? edgeWalrus : null)
                ?? new HashSet<string>();

            var localAssigned = new HashSet<string>(definitelyAssigned);
            foreach (var key in block.EntryRebinds)
                localAssigned.Add(key);

            for (int i = 0; i < block.Statements.Count; i++)
            {
                var stmt = block.Statements[i];

                // A walrus binds before the reads that follow it; within one statement it is
                // credited at the statement's start (Python evaluates left to right, so a read
                // textually before the walrus in the same statement is not caught here).
                CollectWalrusTargets(stmt, localAssigned);

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

            foreach (var expr in block.Expressions)
                CollectWalrusTargets(expr, localAssigned);
            if (block.Terminator is ConditionalBranchTerminator terminatorCondition)
                CollectWalrusTargets(terminatorCondition.Condition, localAssigned);

            foreach (var (name, node, stmtIdx) in readsInBlock[block])
            {
                if (stmtIdx == block.Statements.Count && bareDecls.ContainsKey(name) && !localAssigned.Contains(name))
                {
                    violations.Add(new Violation(bareDecls[name], node));
                }
            }
        }

        // A bare local a lambda reads that is assigned NOWHERE in the function can never be bound
        // when the lambda runs (python3: NameError). Without this the definite initializer the
        // emitter adds for DA-proved locals turned the read into a silent `default` (#1635).
        if (lambdaReads.Count > 0)
        {
            var assignedAnywhere = new HashSet<string>();
            foreach (var assigned in assignedInBlock.Values)
                assignedAnywhere.UnionWith(assigned);
            foreach (var (name, node) in lambdaReads)
            {
                if (bareDecls.ContainsKey(name) && !assignedAnywhere.Contains(name))
                    violations.Add(new Violation(bareDecls[name], node));
            }
        }

        return violations;
    }

    /// <summary>
    /// Registers every walrus target as a bare declaration so the DA analysis can track
    /// reads of walrus-introduced variables that might not be assigned on all paths.
    /// Only registers names not already declared via <c>VariableDeclaration</c> (a walrus
    /// that rebinds an existing variable is not a new declaration).
    /// </summary>
    private static void CollectWalrusBareDecls(
        Node node, Dictionary<string, VariableDeclaration> bareDecls, HashSet<string> declaredNames)
    {
        if (node is LambdaExpression)
            return;
        if (node is WalrusExpression walrus)
        {
            if (!declaredNames.Contains(walrus.Target) && !bareDecls.ContainsKey(walrus.Target))
            {
                bareDecls[walrus.Target] = new VariableDeclaration
                {
                    Name = walrus.Target,
                    LineStart = walrus.LineStart,
                    ColumnStart = walrus.ColumnStart,
                };
            }
        }
        foreach (var child in node.GetChildNodes())
            CollectWalrusBareDecls(child, bareDecls, declaredNames);
    }

    /// <summary>
    /// Adds every walrus target (<c>name := value</c>) reachable from <paramref name="node"/>
    /// to <paramref name="assigned"/>, not descending into lambda bodies (a lambda's walrus binds
    /// the lambda's own scope).
    /// </summary>
    private static void CollectWalrusTargets(Node node, HashSet<string> assigned)
    {
        if (node is LambdaExpression)
            return;
        if (node is WalrusExpression walrus)
            assigned.Add(walrus.Target);
        foreach (var child in node.GetChildNodes())
            CollectWalrusTargets(child, assigned);
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
            case IndexAccess:
            case MemberAccess:
                break;
        }
    }

    private static void CollectReads(
        Statement stmt, List<(string, Identifier, int)> reads, int stmtIdx,
        List<(string, Identifier)> lambdaReads)
    {
        if (stmt is Assignment assign)
        {
            CollectReadsFromExpr(assign.Value, reads, stmtIdx, lambdaReads);
            CollectTargetReads(assign.Target, reads, stmtIdx, lambdaReads);
            return;
        }

        foreach (var child in stmt.GetChildNodes())
        {
            if (child is Expression expr)
                CollectReadsFromExpr(expr, reads, stmtIdx, lambdaReads);
        }
    }

    private static void CollectTargetReads(
        Expression target, List<(string, Identifier, int)> reads, int stmtIdx,
        List<(string, Identifier)> lambdaReads)
    {
        switch (target)
        {
            case Identifier:
                break;
            case TupleLiteral tuple:
                foreach (var element in tuple.Elements)
                    CollectTargetReads(element, reads, stmtIdx, lambdaReads);
                break;
            case StarExpression star:
                CollectTargetReads(star.Operand, reads, stmtIdx, lambdaReads);
                break;
            default:
                CollectReadsFromExpr(target, reads, stmtIdx, lambdaReads);
                break;
        }
    }

    private static void CollectReadsFromExpr(
        Expression expr, List<(string, Identifier, int)> reads, int stmtIdx,
        List<(string, Identifier)> lambdaReads)
    {
        if (expr is Identifier id)
        {
            reads.Add((id.Name, id, stmtIdx));
            return;
        }

        if (expr is LambdaExpression lambda)
        {
            CollectLambdaReads(lambda, lambdaReads);
            return;
        }

        foreach (var child in expr.GetChildNodes())
        {
            if (child is Expression childExpr)
                CollectReadsFromExpr(childExpr, reads, stmtIdx, lambdaReads);
        }
    }

    /// <summary>Collects every identifier read inside a lambda body (nested lambdas included).</summary>
    private static void CollectLambdaReads(Node node, List<(string, Identifier)> lambdaReads)
    {
        if (node is Identifier id)
        {
            lambdaReads.Add((id.Name, id));
            return;
        }
        foreach (var child in node.GetChildNodes())
            CollectLambdaReads(child, lambdaReads);
    }

    /// <summary>
    /// Computes the when-true and when-false walrus assignment sets for an expression,
    /// following C# §9.4.4.26-.28. A walrus <c>n := v</c> adds <c>n</c> to both sets
    /// (it always assigns when evaluated). Short-circuit <c>and</c>/<c>or</c> and <c>not</c>
    /// split or swap the sets so that a walrus in a short-circuited branch is credited
    /// only on the path where it actually evaluates.
    /// </summary>
    private static (HashSet<string> WhenTrue, HashSet<string> WhenFalse) ComputeWalrusWhenTrueFalse(
        Expression expr)
    {
        if (expr is BinaryOp { Operator: BinaryOperator.And } andExpr)
        {
            var (ta, fa) = ComputeWalrusWhenTrueFalse(andExpr.Left);
            var (tb, fb) = ComputeWalrusWhenTrueFalse(andExpr.Right);
            // T(a and b) = T(a) ∪ T(b)
            var t = new HashSet<string>(ta);
            t.UnionWith(tb);
            // F(a and b) = F(a) ∩ (T(a) ∪ F(b))
            var taUnionFb = new HashSet<string>(ta);
            taUnionFb.UnionWith(fb);
            var f = new HashSet<string>(fa);
            f.IntersectWith(taUnionFb);
            return (t, f);
        }

        if (expr is BinaryOp { Operator: BinaryOperator.Or } orExpr)
        {
            var (ta, fa) = ComputeWalrusWhenTrueFalse(orExpr.Left);
            var (tb, fb) = ComputeWalrusWhenTrueFalse(orExpr.Right);
            // T(a or b) = T(a) ∩ (F(a) ∪ T(b))
            var faUnionTb = new HashSet<string>(fa);
            faUnionTb.UnionWith(tb);
            var t = new HashSet<string>(ta);
            t.IntersectWith(faUnionTb);
            // F(a or b) = F(a) ∪ F(b)
            var f = new HashSet<string>(fa);
            f.UnionWith(fb);
            return (t, f);
        }

        if (expr is UnaryOp { Operator: UnaryOperator.Not } notExpr)
        {
            var (te, fe) = ComputeWalrusWhenTrueFalse(notExpr.Operand);
            return (fe, te);
        }

        var all = new HashSet<string>();
        CollectWalrusTargets(expr, all);
        return (all, new HashSet<string>(all));
    }
}
