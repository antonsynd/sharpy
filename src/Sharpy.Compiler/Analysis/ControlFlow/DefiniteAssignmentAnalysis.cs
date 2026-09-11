using System.Collections.Immutable;
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

            // Entry expressions (a match case's guard) run before statement 0, so their walruses
            // are assigned for the whole block and their reads are judged at index -1.
            foreach (var entryExpr in block.EntryExpressions)
            {
                CollectWalrusBareDecls(entryExpr, bareDecls, declaredNames);
                CollectWalrusTargets(entryExpr, blockAssigned);
                CollectReadsFromExpr(entryExpr, blockReads, -1, lambdaReads);
            }

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

            // An entry expression's reads are judged before it assigns anything (Python evaluates
            // the guard left to right), then its walruses are credited for the statements below.
            foreach (var (name, node, stmtIdx) in readsInBlock[block])
            {
                if (stmtIdx == -1 && bareDecls.ContainsKey(name) && !localAssigned.Contains(name))
                {
                    violations.Add(new Violation(bareDecls[name], node));
                }
            }

            foreach (var entryExpr in block.EntryExpressions)
                CollectWalrusTargets(entryExpr, localAssigned);

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
    /// Adds the walrus targets that are <b>definitely assigned</b> once <paramref name="node"/> has
    /// been evaluated, not descending into lambda bodies (a lambda's walrus binds the lambda's own
    /// scope). A walrus in a conditionally-evaluated position — a short-circuited operand, an
    /// untaken ternary arm, a comparison-chain operand past the first link, a comprehension element
    /// over a possibly-empty iterable — may never run, so it is not credited here; the caller's
    /// when-true/when-false edge sets carry it on the path where it does run
    /// (<see cref="ComputeWalrusWhenTrueFalse"/>).
    /// </summary>
    private static void CollectWalrusTargets(Node node, HashSet<string> assigned)
    {
        if (node is LambdaExpression)
            return;
        if (node is Expression expr)
        {
            assigned.UnionWith(ComputeDefinitelyAssignedWalruses(expr));
            return;
        }
        foreach (var child in node.GetChildNodes())
            CollectWalrusTargets(child, assigned);
    }

    /// <summary>
    /// "Definitely assigned after <paramref name="expr"/>" is <c>T(expr) ∩ F(expr)</c> (C#
    /// §12.6.4.2): the names assigned on both outcomes are the ones assigned regardless of outcome.
    /// </summary>
    private static HashSet<string> ComputeDefinitelyAssignedWalruses(Expression expr)
    {
        var (whenTrue, whenFalse) = ComputeWalrusWhenTrueFalse(expr);
        whenTrue.IntersectWith(whenFalse);
        return whenTrue;
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
    /// Computes the when-true and when-false walrus assignment sets for an expression, following
    /// C# §12.6.4.2 (the definite-assignment rules for boolean expressions). A walrus <c>n := v</c>
    /// adds <c>n</c> to both sets — it always assigns when evaluated. Every construct that
    /// conditionally evaluates a subexpression has an arm here, so that a walrus in a branch that
    /// may not run is credited only on the path where it does:
    /// <list type="bullet">
    /// <item><description><c>and</c> / <c>or</c> — short-circuited operand.</description></item>
    /// <item><description><c>not</c> — swaps the sets.</description></item>
    /// <item><description>Ternary <c>x if c else y</c> — Design Decision 7:
    /// <c>A = (T(c) ∪ A(x)) ∩ (F(c) ∪ A(y))</c>, per-outcome below.</description></item>
    /// <item><description>Comparison chain <c>a &lt; b &lt; c</c> — operands past index 1 run only
    /// when every earlier link held, so they are credited when-true only.</description></item>
    /// <item><description>Comprehension / generator expression — the element and the <c>if</c>
    /// clauses run once per item, zero times over an empty iterable, so only the first
    /// <c>for</c> clause's iterator is credited. python3 agrees:
    /// <c>[(w := v) for v in []]</c> then reading <c>w</c> is an
    /// <c>UnboundLocalError</c>.</description></item>
    /// <item><description><c>?</c> early-return — everything evaluated after a <c>?</c> in the same
    /// expression runs only when the <c>?</c> did not return.</description></item>
    /// </list>
    /// Every other expression evaluates all of its children, so the default arm unions them. The
    /// partiality is deliberate and its direction is the safe one: an unlisted kind falls to that
    /// default and its walrus is CREDITED, which is what this analysis did for every kind before the
    /// conditional arms existed — so a kind nobody has classified yet cannot refuse a legal program,
    /// only fail to refuse an illegal one. That is the contract; adding a kind that conditionally
    /// evaluates a sub-expression means adding an arm here.
    /// </summary>
    private static (HashSet<string> WhenTrue, HashSet<string> WhenFalse) ComputeWalrusWhenTrueFalse(
        Expression expr)
    {
        if (expr is LambdaExpression)
        {
            // A lambda's body runs when the lambda is CALLED, not where it is written, and its
            // walruses bind the lambda's own scope.
            return (new HashSet<string>(), new HashSet<string>());
        }

        if (expr is WalrusExpression walrus)
        {
            // The value is evaluated first, then the target is bound.
            var assigned = ComputeDefinitelyAssignedWalruses(walrus.Value);
            assigned.Add(walrus.Target);
            return (assigned, new HashSet<string>(assigned));
        }

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

        if (expr is BinaryOp { Operator: BinaryOperator.NullCoalesce } coalesce)
        {
            // `a ?? b` evaluates b only when a is absent, so b's walruses are conditional. The
            // result's truth value carries no narrowing, so both sides get the same set.
            var unconditional = ComputeDefinitelyAssignedWalruses(coalesce.Left);
            return (unconditional, new HashSet<string>(unconditional));
        }

        if (expr is ConditionalExpression ternary)
        {
            // Design Decision 7 / C# §12.6.4.28 (the ?: rule). The condition always runs; exactly
            // one arm runs, so a name counts only if the branch that skips it assigns it anyway:
            //   T(c ? x : y) = (T(c) ∪ T(x)) ∩ (F(c) ∪ T(y))
            //   F(c ? x : y) = (T(c) ∪ F(x)) ∩ (F(c) ∪ F(y))
            // (T(c) and F(c) each already contain everything the condition assigns, so the
            // condition's own walruses survive both intersections.)
            var (tc, fc) = ComputeWalrusWhenTrueFalse(ternary.Test);
            var (tx, fx) = ComputeWalrusWhenTrueFalse(ternary.ThenValue);
            var (ty, fy) = ComputeWalrusWhenTrueFalse(ternary.ElseValue);

            static HashSet<string> Combine(
                HashSet<string> condSide, HashSet<string> armSide,
                HashSet<string> otherCondSide, HashSet<string> otherArmSide)
            {
                var left = new HashSet<string>(condSide);
                left.UnionWith(armSide);
                var right = new HashSet<string>(otherCondSide);
                right.UnionWith(otherArmSide);
                left.IntersectWith(right);
                return left;
            }

            return (Combine(tc, tx, fc, ty), Combine(tc, fx, fc, fy));
        }

        if (expr is ComparisonChain chain)
        {
            // `a < b < c` short-circuits like `&&`: operands 0 and 1 always run, operand k > 1 runs
            // only when every earlier link held. A chain's own value carries no narrowing, so each
            // operand contributes its unconditional set.
            var unconditional = new HashSet<string>();
            if (chain.Operands.Length > 0)
                unconditional.UnionWith(ComputeDefinitelyAssignedWalruses(chain.Operands[0]));
            if (chain.Operands.Length > 1)
                unconditional.UnionWith(ComputeDefinitelyAssignedWalruses(chain.Operands[1]));

            var whenTrue = new HashSet<string>(unconditional);
            for (int i = 2; i < chain.Operands.Length; i++)
                whenTrue.UnionWith(ComputeDefinitelyAssignedWalruses(chain.Operands[i]));

            return (whenTrue, unconditional);
        }

        if (expr is ListComprehension or SetComprehension or DictComprehension or GeneratorExpression)
        {
            // The element, the value, and every `if` clause run once per item — zero times over an
            // empty iterable — and a nested `for` clause's iterator runs per outer item. Only the
            // FIRST for-clause's iterator is evaluated exactly once.
            var clauses = expr switch
            {
                ListComprehension lc => lc.Clauses,
                SetComprehension sc => sc.Clauses,
                DictComprehension dc => dc.Clauses,
                GeneratorExpression ge => ge.Clauses,
                _ => ImmutableArray<ComprehensionClause>.Empty
            };

            var outerIterator = new HashSet<string>();
            foreach (var clause in clauses)
            {
                if (clause is ForClause forClause)
                {
                    outerIterator.UnionWith(ComputeDefinitelyAssignedWalruses(forClause.Iterator));
                    break;
                }
            }

            return (outerIterator, new HashSet<string>(outerIterator));
        }

        // Default: every child is evaluated, left to right. A `?` early-returns from the enclosing
        // function, so nothing after one is definitely evaluated — stop crediting at that point.
        var all = new HashSet<string>();
        foreach (var child in expr.GetChildNodes())
        {
            if (child is not Expression childExpr)
            {
                // A non-expression child (a comprehension clause, a keyword-argument wrapper …)
                // still contains expressions that run unconditionally here.
                CollectWalrusTargets(child, all);
                continue;
            }

            all.UnionWith(ComputeDefinitelyAssignedWalruses(childExpr));
            if (ContainsQuestionMark(childExpr))
                break;
        }

        return (all, new HashSet<string>(all));
    }

    /// <summary>
    /// True when <paramref name="expr"/> evaluates a <c>?</c> operator, which returns early from the
    /// enclosing function. Does not descend into a lambda body (its <c>?</c>, if any, runs when the
    /// lambda is called) — and the checker refuses <c>?</c> there outright (SPY0462).
    /// </summary>
    private static bool ContainsQuestionMark(Node node)
    {
        if (node is LambdaExpression)
            return false;
        if (node is QuestionMarkExpression)
            return true;
        foreach (var child in node.GetChildNodes())
        {
            if (ContainsQuestionMark(child))
                return true;
        }
        return false;
    }
}
