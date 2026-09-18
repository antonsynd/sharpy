using System.Collections.Immutable;
using System.Linq;
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
    /// The result of the two-pass definite-assignment analysis (#1839). A read that is unassigned
    /// even when suppression edges are IGNORED is a genuine <see cref="Violations"/> (refused,
    /// SPY0600); a read that is assigned when they are ignored but not when they are HONOURED is
    /// <see cref="RuntimeChecked"/> (an <c>UnboundLocalError</c> at runtime, not refused).
    /// <see cref="FlaggedNames"/> are the locals whose assignedness a suppression edge can change —
    /// the emitter gives each an assigned-flag.
    /// </summary>
    internal readonly record struct Result(
        IReadOnlyList<Violation> Violations,
        IReadOnlyList<Violation> RuntimeChecked,
        IReadOnlyCollection<string> FlaggedNames);

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
    /// Runs the dataflow twice — once ignoring suppression edges (strict), once honouring them
    /// (lenient) — and classifies each read: unassigned even strict → refused; assigned strict but
    /// not lenient → runtime-checked (#1839, R-AI).
    /// </summary>
    public static Result Analyze(ControlFlowGraph cfg)
    {
        var bareDecls = new Dictionary<string, VariableDeclaration>();
        var assignedInBlock = new Dictionary<BasicBlock, HashSet<string>>();
        var readsInBlock = new Dictionary<BasicBlock, List<(string Name, Identifier Node, int StatementIndex)>>();
        // Reads inside a DEFERRED body — a lambda's or a nested `def`'s — are not flow-positioned:
        // the closure may run after a later assignment (`f = lambda: x; x = 7; f()`, or the `def`
        // equivalent, is legal Python). They are judged once, at the end, against "is this local
        // assigned ANYWHERE in the function" (#1635, extended to nested defs by #1681 — lambda and
        // nested-def reads are judged by ONE rule).
        var deferredReads = new List<(string Name, Identifier Node)>();
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
                CollectReadsFromExpr(entryExpr, blockReads, -1, deferredReads);
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
                CollectReads(stmt, blockReads, i, deferredReads);
            }

            foreach (var expr in block.Expressions)
            {
                CollectWalrusBareDecls(expr, bareDecls, declaredNames);
                CollectWalrusTargets(expr, blockAssigned);
                CollectReadsFromExpr(expr, blockReads, block.Statements.Count, deferredReads);
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

                CollectReadsFromExpr(cbt.Condition, blockReads, block.Statements.Count, deferredReads);
            }

            assignedInBlock[block] = blockAssigned;
            readsInBlock[block] = blockReads;
        }

        // A nested `def` is skipped entirely by ControlFlowGraphBuilder (it forms its own graph and
        // never becomes a block statement of THIS function's CFG — see BuildStatement's `FunctionDef`
        // arm), so it is invisible to the per-block scan above. Its body is walked directly from the
        // raw AST here instead, and its reads get the exact same deferred treatment as a lambda's
        // (#1681 — lambda and nested-def reads are judged by ONE rule).
        if (cfg.SourceFunction != null)
        {
            foreach (var stmt in cfg.SourceFunction.Body)
                CollectNestedDefDeferredReads(stmt, deferredReads);
        }

        if (bareDecls.Count == 0)
            return new Result(Array.Empty<Violation>(), Array.Empty<Violation>(), Array.Empty<string>());

        var bareNames = new HashSet<string>(bareDecls.Keys);
        var edgeWalrusArg = edgeWalrus.Count > 0 ? edgeWalrus : null;

        // Two fixpoints: strict IGNORES suppression edges (the with completes normally), lenient
        // HONOURS them (a body exception can be swallowed before a body assignment ran). A read
        // unassigned even strict is a genuine violation; a read assigned strict but not lenient is
        // runtime-checked (#1839).
        var (inStrict, outStrict) = RunFixpoint(cfg, bareNames, assignedInBlock, edgeWalrusArg, honourSuppression: false);
        var (inLenient, outLenient) = RunFixpoint(cfg, bareNames, assignedInBlock, edgeWalrusArg, honourSuppression: true);

        var strictViolations = CollectFlowViolations(cfg, bareNames, bareDecls, readsInBlock, inStrict, outStrict, edgeWalrusArg, honourSuppression: false);
        var lenientViolations = CollectFlowViolations(cfg, bareNames, bareDecls, readsInBlock, inLenient, outLenient, edgeWalrusArg, honourSuppression: true);

        var genuine = new List<Violation>(strictViolations);
        var runtimeChecked = new List<Violation>();
        var strictSites = new HashSet<Identifier>(strictViolations.Select(v => v.ReadSite));
        foreach (var v in lenientViolations)
        {
            if (!strictSites.Contains(v.ReadSite))
                runtimeChecked.Add(v);
        }

        // A name is flagged when its assignedness differs between the passes at some point — the
        // suppression edge can make it unset there. Genuinely-violated names (unassigned in BOTH
        // passes) are excluded, so a conditionally-assigned local stays refused, not flagged.
        var flagged = new HashSet<string>();
        foreach (var block in cfg.Blocks)
        {
            if (block == cfg.Entry)
                continue;
            foreach (var name in inStrict[block])
            {
                if (!inLenient[block].Contains(name))
                    flagged.Add(name);
            }
        }

        // Lambda / nested-def reads run when the closure is CALLED, not where it is written. A local
        // assigned NOWHERE can never be bound then (python3: NameError → Sharpy UnboundLocalError):
        // a genuine violation. A local whose only assignment a suppression edge can skip is
        // runtime-checked — the same UnboundLocalError, but only when actually unset (#1839).
        if (deferredReads.Count > 0)
        {
            var assignedAnywhere = new HashSet<string>();
            foreach (var assigned in assignedInBlock.Values)
                assignedAnywhere.UnionWith(assigned);
            foreach (var (name, node) in deferredReads)
            {
                if (!bareDecls.ContainsKey(name))
                    continue;
                if (!assignedAnywhere.Contains(name))
                    genuine.Add(new Violation(bareDecls[name], node));
                else if (flagged.Contains(name))
                    runtimeChecked.Add(new Violation(bareDecls[name], node));
            }
        }

        return new Result(genuine, runtimeChecked, flagged);
    }

    /// <summary>Runs the must-assign fixpoint to convergence, honouring or ignoring suppression edges.</summary>
    private static (Dictionary<BasicBlock, HashSet<string>> InSets, Dictionary<BasicBlock, HashSet<string>> OutSets) RunFixpoint(
        ControlFlowGraph cfg,
        HashSet<string> bareNames,
        Dictionary<BasicBlock, HashSet<string>> assignedInBlock,
        IReadOnlyDictionary<BasicBlock, (HashSet<string> WhenTrue, HashSet<string> WhenFalse)>? edgeWalrus,
        bool honourSuppression)
    {
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
                    edgeWalrus, honourSuppression);
                if (inSet == null)
                    continue;

                // An exception/suppression successor reads THIS block's in-set, so a change here
                // must re-run the fixpoint even when the out-set is unchanged.
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

        return (inSets, outSets);
    }

    /// <summary>
    /// Walks each block in program order, computing the definitely-assigned set incrementally, and
    /// returns every bare-local read that is not definitely assigned when it runs — for the pass
    /// described by <paramref name="inSets"/>/<paramref name="outSets"/> and
    /// <paramref name="honourSuppression"/>.
    /// </summary>
    private static List<Violation> CollectFlowViolations(
        ControlFlowGraph cfg,
        HashSet<string> bareNames,
        Dictionary<string, VariableDeclaration> bareDecls,
        Dictionary<BasicBlock, List<(string Name, Identifier Node, int StatementIndex)>> readsInBlock,
        Dictionary<BasicBlock, HashSet<string>> inSets,
        Dictionary<BasicBlock, HashSet<string>> outSets,
        IReadOnlyDictionary<BasicBlock, (HashSet<string> WhenTrue, HashSet<string> WhenFalse)>? edgeWalrus,
        bool honourSuppression)
    {
        var violations = new List<Violation>();
        foreach (var block in cfg.Blocks)
        {
            if (block == cfg.Entry)
                continue;

            var definitelyAssigned = MustAssignDataflow.ComputeInSet(block, bareNames, inSets, outSets,
                edgeWalrus, honourSuppression);

            // A block with no predecessors of any honoured kind is unreachable in THIS pass — its
            // reads cannot execute, so they are not use-before-assign. Skipping it (rather than
            // treating it as the empty set) is what lets the strict pass leave a suppression-only
            // successor to the lenient pass: a `with Sup(): n = 5; raise` body reaches its exit only
            // by suppression, so the read after is runtime-checked, not refused (#1839).
            if (definitelyAssigned == null)
                continue;

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

    /// <summary>Set union, as a fresh set; the two inputs are left alone.</summary>
    private static HashSet<string> Union(HashSet<string> a, HashSet<string> b)
    {
        var result = new HashSet<string>(a);
        result.UnionWith(b);
        return result;
    }

    /// <summary>
    /// The compile-time truth value of <paramref name="expr"/>, or null when it does not have one.
    /// Recognizes exactly a boolean literal and <c>not</c> applied to one — C# §9.4.4's
    /// definite-assignment rules for <c>?:</c>, <c>&amp;&amp;</c> and <c>||</c> key on the operand
    /// being a <i>constant expression</i>, and those two spellings are what a user writes to mean
    /// "this branch always runs".
    ///
    /// <para>Deliberately not broader. This is a REACHABILITY judgement: widening it to fold
    /// arbitrary constant expressions would make the analysis credit walruses on the strength of a
    /// fold, and a fold that disagrees with the emitter's would refuse a program that runs. The
    /// conservative direction is to return null, which falls back to the general formula.</para>
    /// </summary>
    private static bool? TryGetConstantTruth(Expression expr)
    {
        return expr switch
        {
            BooleanLiteral literal => literal.Value,
            UnaryOp { Operator: UnaryOperator.Not } negation
                => TryGetConstantTruth(negation.Operand) is { } inner ? !inner : null,
            _ => null,
        };
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
        List<(string, Identifier)> deferredReads)
    {
        if (stmt is Assignment assign)
        {
            CollectReadsFromExpr(assign.Value, reads, stmtIdx, deferredReads);
            CollectTargetReads(assign.Target, reads, stmtIdx, deferredReads);
            return;
        }

        foreach (var child in stmt.GetChildNodes())
        {
            if (child is Expression expr)
                CollectReadsFromExpr(expr, reads, stmtIdx, deferredReads);
        }
    }

    private static void CollectTargetReads(
        Expression target, List<(string, Identifier, int)> reads, int stmtIdx,
        List<(string, Identifier)> deferredReads)
    {
        switch (target)
        {
            case Identifier:
                break;
            case TupleLiteral tuple:
                foreach (var element in tuple.Elements)
                    CollectTargetReads(element, reads, stmtIdx, deferredReads);
                break;
            case StarExpression star:
                CollectTargetReads(star.Operand, reads, stmtIdx, deferredReads);
                break;
            default:
                CollectReadsFromExpr(target, reads, stmtIdx, deferredReads);
                break;
        }
    }

    private static void CollectReadsFromExpr(
        Expression expr, List<(string, Identifier, int)> reads, int stmtIdx,
        List<(string, Identifier)> deferredReads)
    {
        if (expr is Identifier id)
        {
            reads.Add((id.Name, id, stmtIdx));
            return;
        }

        if (expr is LambdaExpression lambda)
        {
            CollectDeferredReads(lambda, deferredReads);
            return;
        }

        foreach (var child in expr.GetChildNodes())
        {
            if (child is Expression childExpr)
                CollectReadsFromExpr(childExpr, reads, stmtIdx, deferredReads);
        }
    }

    /// <summary>
    /// Finds every nested <c>def</c> reachable from <paramref name="node"/> and feeds each one's
    /// body through <see cref="CollectDeferredReads"/> — the same treatment a lambda gets (#1681,
    /// "lambda and nested-def reads are judged by ONE rule"). A nested <c>def</c> never becomes a
    /// block statement of the enclosing function's CFG (<c>ControlFlowGraphBuilder</c> treats it as
    /// control-flow-neutral: it forms its own graph, built separately when the walker visits it), so
    /// this walk operates on the raw AST rather than <c>block.Statements</c> — it is the discovery
    /// step the per-block scan in <see cref="Analyze"/> cannot perform. Does not descend into a
    /// <see cref="LambdaExpression"/>'s body (the per-block scan already collected its reads, and a
    /// lambda's body is a single expression that cannot itself contain a <c>def</c>), but a nested
    /// <c>def</c> found INSIDE another nested <c>def</c>'s body is still found, because
    /// <see cref="CollectDeferredReads"/> recurses through every child node without stopping at a
    /// further <c>FunctionDef</c> boundary once it is already inside a deferred body.
    /// </summary>
    private static void CollectNestedDefDeferredReads(Node node, List<(string, Identifier)> deferredReads)
    {
        if (node is LambdaExpression)
            return;

        if (node is FunctionDef nestedDef)
        {
            CollectDeferredReads(nestedDef, deferredReads, new HashSet<string>());
            return;
        }

        foreach (var child in node.GetChildNodes())
            CollectNestedDefDeferredReads(child, deferredReads);
    }

    /// <summary>
    /// Collects every identifier read inside a deferred body — a lambda's or a nested <c>def</c>'s
    /// (nested lambdas/defs included) — treated alike because both run when the closure is CALLED,
    /// not where it is written (#1635, extended to nested defs by #1681).
    ///
    /// <para><paramref name="shadowed"/> is null for a lambda body (its only own bindings are
    /// parameters, which <see cref="LambdaExpression.GetChildNodes"/> already excludes from
    /// traversal) and non-null once traversal has entered a nested <c>def</c>: that def's own
    /// parameters and declarations bind a name scoped to ITS body, shadowing a same-named local of
    /// the enclosing scope (C# scoping, Axiom 1) — a read of a shadowed name is the nested def's OWN
    /// local, not a deferred read of the enclosing scope's, so it is excluded. A further-nested
    /// <c>def</c> gets its own names layered on top, so shadowing composes correctly at any
    /// depth.</para>
    /// </summary>
    private static void CollectDeferredReads(
        Node node, List<(string, Identifier)> deferredReads, HashSet<string>? shadowed = null)
    {
        if (node is Identifier id)
        {
            if (shadowed == null || !shadowed.Contains(id.Name))
                deferredReads.Add((id.Name, id));
            return;
        }

        if (node is FunctionDef nestedDef)
        {
            var innerShadowed = shadowed == null ? new HashSet<string>() : new HashSet<string>(shadowed);
            foreach (var param in nestedDef.Parameters)
                innerShadowed.Add(param.Name);
            foreach (var bodyStmt in nestedDef.Body)
                CollectLocalDeclarationNames(bodyStmt, innerShadowed);

            foreach (var child in nestedDef.GetChildNodes())
                CollectDeferredReads(child, deferredReads, innerShadowed);
            return;
        }

        foreach (var child in node.GetChildNodes())
            CollectDeferredReads(child, deferredReads, shadowed);
    }

    /// <summary>
    /// Adds every name a <c>VariableDeclaration</c> introduces directly within <paramref name="node"/>
    /// to <paramref name="names"/>, without crossing into a further nested <c>def</c> or lambda (each
    /// binds its own separate scope, so its declarations don't shadow the CURRENT level — they are
    /// collected separately, at their own level, when <see cref="CollectDeferredReads"/> reaches
    /// them).
    /// </summary>
    private static void CollectLocalDeclarationNames(Node node, HashSet<string> names)
    {
        if (node is LambdaExpression or FunctionDef)
            return;
        if (node is VariableDeclaration vd)
            names.Add(vd.Name);
        foreach (var child in node.GetChildNodes())
            CollectLocalDeclarationNames(child, names);
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

            // C# §9.4.4's constant rules: when the first operand is a CONSTANT, one outcome is
            // unreachable and the general formula's intersection against it under-credits.
            if (TryGetConstantTruth(andExpr.Left) is { } andConst)
            {
                return andConst
                    // `True and b`: b always runs and decides the result.
                    ? (Union(ta, tb), Union(ta, fb))
                    // `False and b`: b never runs and the result is always false, so the true
                    // outcome is unreachable and carries the same state as the false one.
                    : (new HashSet<string>(fa), new HashSet<string>(fa));
            }

            // T(a and b) = T(a) ∪ T(b)
            var t = Union(ta, tb);
            // F(a and b) = F(a) ∩ (T(a) ∪ F(b))
            var f = new HashSet<string>(fa);
            f.IntersectWith(Union(ta, fb));
            return (t, f);
        }

        if (expr is BinaryOp { Operator: BinaryOperator.Or } orExpr)
        {
            var (ta, fa) = ComputeWalrusWhenTrueFalse(orExpr.Left);
            var (tb, fb) = ComputeWalrusWhenTrueFalse(orExpr.Right);

            if (TryGetConstantTruth(orExpr.Left) is { } orConst)
            {
                return orConst
                    // `True or b`: b never runs and the result is always true, so the false
                    // outcome is unreachable and carries the same state as the true one.
                    ? (new HashSet<string>(ta), new HashSet<string>(ta))
                    // `False or b`: b always runs and decides the result.
                    : (Union(fa, tb), Union(fa, fb));
            }

            // T(a or b) = T(a) ∩ (F(a) ∪ T(b))
            var t = new HashSet<string>(ta);
            t.IntersectWith(Union(fa, tb));
            // F(a or b) = F(a) ∪ F(b)
            var f = Union(fa, fb);
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

            // C# §9.4.4's constant rule: when the condition is a constant, only one arm is
            // reachable and the state after the `?:` is the state after THAT arm. Without this the
            // general formula still intersects against the unreachable arm, so
            // `(w := f()) if True else 0` — where python3 binds w unconditionally — was refused.
            if (TryGetConstantTruth(ternary.Test) is { } condConst)
            {
                return condConst
                    ? (Union(tc, tx), Union(tc, fx))
                    : (Union(fc, ty), Union(fc, fy));
            }

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
