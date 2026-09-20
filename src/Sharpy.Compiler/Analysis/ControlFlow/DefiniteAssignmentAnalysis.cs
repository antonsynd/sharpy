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
                CollectReadsFromExpr(entryExpr, blockReads, -1, deferredReads, NoShadow);
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
                CollectReads(stmt, blockReads, i, deferredReads, NoShadow);
            }

            foreach (var expr in block.Expressions)
            {
                CollectWalrusBareDecls(expr, bareDecls, declaredNames);
                CollectWalrusTargets(expr, blockAssigned);
                CollectReadsFromExpr(expr, blockReads, block.Statements.Count, deferredReads, NoShadow);
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

                CollectReadsFromExpr(cbt.Condition, blockReads, block.Statements.Count, deferredReads, NoShadow);
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

            // A nested def's WRITE-THROUGH assignment (`x = 1` with no local re-declaration of `x`
            // assigns the ENCLOSING local — C# closure semantics, Axiom 1; see
            // BlockScopeRedeclarationMatrixTests.OuterDeclaredReassignInside_WritesThrough) is
            // invisible to assignedInBlock above for the same reason its reads were invisible to
            // readsInBlock: a nested def never becomes a block statement of THIS function's CFG.
            // Collected symmetrically with the read side, so a SIBLING nested def's read of that
            // outer local is not wrongly judged "never assigned" (#1681 follow-up).
            if (cfg.SourceFunction != null)
            {
                foreach (var stmt in cfg.SourceFunction.Body)
                    CollectNestedDefWriteThroughs(stmt, assignedAnywhere);
            }

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
        List<(string, Identifier)> deferredReads, HashSet<string> shadowed)
    {
        if (stmt is Assignment assign)
        {
            CollectReadsFromExpr(assign.Value, reads, stmtIdx, deferredReads, shadowed);
            CollectTargetReads(assign.Target, reads, stmtIdx, deferredReads, shadowed);
            return;
        }

        foreach (var (child, childShadowed) in ScopedChildren(stmt, shadowed))
        {
            if (child is Expression expr)
                CollectReadsFromExpr(expr, reads, stmtIdx, deferredReads, childShadowed);
        }
    }

    private static void CollectTargetReads(
        Expression target, List<(string, Identifier, int)> reads, int stmtIdx,
        List<(string, Identifier)> deferredReads, HashSet<string> shadowed)
    {
        switch (target)
        {
            case Identifier:
                break;
            case TupleLiteral tuple:
                foreach (var element in tuple.Elements)
                    CollectTargetReads(element, reads, stmtIdx, deferredReads, shadowed);
                break;
            case StarExpression star:
                CollectTargetReads(star.Operand, reads, stmtIdx, deferredReads, shadowed);
                break;
            default:
                CollectReadsFromExpr(target, reads, stmtIdx, deferredReads, shadowed);
                break;
        }
    }

    /// <summary>
    /// Collects flow-positioned reads. <paramref name="shadowed"/> carries the names bound by the
    /// SCOPE-INTRODUCING expressions traversal has entered (a comprehension's or generator
    /// expression's clause targets, a lambda's parameters — see <see cref="ScopedChildren"/>): a
    /// read of such a name is that scope's OWN binding, not the enclosing function's same-named
    /// bare local, so it is not a use of the local and must not be judged against it (#1910).
    /// </summary>
    private static void CollectReadsFromExpr(
        Expression expr, List<(string, Identifier, int)> reads, int stmtIdx,
        List<(string, Identifier)> deferredReads, HashSet<string> shadowed)
    {
        if (expr is Identifier id)
        {
            if (!shadowed.Contains(id.Name))
                reads.Add((id.Name, id, stmtIdx));
            return;
        }

        if (expr is LambdaExpression lambda)
        {
            CollectDeferredReads(lambda, deferredReads, shadowed);
            return;
        }

        foreach (var (child, childShadowed) in ScopedChildren(expr, shadowed))
        {
            if (child is Expression childExpr)
                CollectReadsFromExpr(childExpr, reads, stmtIdx, deferredReads, childShadowed);
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
    /// Collects every identifier READ inside a deferred body — a lambda's or a nested <c>def</c>'s
    /// (nested lambdas/defs included) — treated alike because both run when the closure is CALLED,
    /// not where it is written (#1635, extended to nested defs by #1681). Mirrors
    /// <see cref="CollectReads"/>/<see cref="CollectTargetReads"/>'s split for an <c>Assignment</c>
    /// found inside: a lambda body can never contain one (it is a single expression), but a nested
    /// def's body can, and a bare-name assignment TARGET is a write, not a read — a
    /// same-named-outer-local's write-through assignment (`x = 1`, no local re-declaration; see
    /// <see cref="CollectNestedDefWriteThroughs"/>) must not also be miscounted as a read here.
    ///
    /// <para><paramref name="shadowed"/> carries every name the scopes traversal has entered BIND —
    /// a nested def's parameters and body-flat declarations, a lambda's parameters, a for target, a
    /// comprehension or generator clause target, a <c>with … as</c> / <c>except … as</c> name, a
    /// walrus target, a <c>case</c> pattern capture. Each binds a name scoped to that construct,
    /// shadowing a same-named local of the enclosing scope (C# scoping, Axiom 1), so a read of a
    /// shadowed name is that construct's OWN binding, not a deferred read of the enclosing scope's,
    /// and is excluded. <see cref="ScopedChildren"/> is the single roster of which construct binds
    /// what and over which sub-tree; layering composes at any depth. Judging by NAME alone — the
    /// pre-#1910 shape, which seeded only parameters and <c>VariableDeclaration</c>s — refused
    /// programs whose deferred body reads its own for/comprehension/match/lambda/with/except/walrus
    /// binding.</para>
    /// </summary>
    private static void CollectDeferredReads(
        Node node, List<(string, Identifier)> deferredReads, HashSet<string> shadowed)
    {
        if (node is Identifier id)
        {
            if (!shadowed.Contains(id.Name))
                deferredReads.Add((id.Name, id));
            return;
        }

        if (node is Assignment assign)
        {
            CollectDeferredReads(assign.Value, deferredReads, shadowed);
            CollectDeferredTargetReads(assign.Target, deferredReads, shadowed);
            return;
        }

        foreach (var (child, childShadowed) in ScopedChildren(node, shadowed))
            CollectDeferredReads(child, deferredReads, childShadowed);
    }

    /// <summary>Deferred-read counterpart of <see cref="CollectTargetReads"/>: a bare identifier
    /// assignment target is a write, not a read (a write-through one is collected separately by
    /// <see cref="CollectNestedDefWriteThroughs"/>), but a compound target's sub-expressions
    /// (<c>obj.attr = v</c>, <c>arr[i] = v</c>) still read <c>obj</c>/<c>arr</c>/<c>i</c>.</summary>
    private static void CollectDeferredTargetReads(
        Expression target, List<(string, Identifier)> deferredReads, HashSet<string> shadowed)
    {
        switch (target)
        {
            case Identifier:
                break;
            case TupleLiteral tuple:
                foreach (var element in tuple.Elements)
                    CollectDeferredTargetReads(element, deferredReads, shadowed);
                break;
            case StarExpression star:
                CollectDeferredTargetReads(star.Operand, deferredReads, shadowed);
                break;
            default:
                CollectDeferredReads(target, deferredReads, shadowed);
                break;
        }
    }

    /// <summary>
    /// The two BODY-FLAT binding forms: a <c>VariableDeclaration</c> and a walrus target bind for
    /// the whole enclosing function body, not just their own sub-tree, so they are collected up
    /// front when <see cref="ScopedChildren"/> enters a <c>def</c> rather than as traversal passes
    /// them (a sibling statement BELOW a walrus reads the bound name — <c>if (k := 5) &gt; 0:</c>
    /// then <c>return k</c>). Does not cross into a further nested <c>def</c> or lambda (each binds
    /// its own separate scope, collected at its own level when traversal reaches it) but DOES cross
    /// a comprehension: python3 binds a walrus inside one in the ENCLOSING function scope.
    /// Every other binding form is sub-tree scoped and lives in <see cref="ScopedChildren"/>.
    /// </summary>
    private static void CollectBodyFlatBindings(Node node, HashSet<string> names)
    {
        switch (node)
        {
            case FunctionDef:
            case LambdaExpression:
                return;
            case VariableDeclaration vd:
                names.Add(vd.Name);
                break;
            case WalrusExpression walrus:
                names.Add(walrus.Target);
                break;
        }

        foreach (var child in node.GetChildNodes())
            CollectBodyFlatBindings(child, names);
    }

    /// <summary>The shadow set at function-body level: nothing is shadowed there, because a binding
    /// form in the function's own body rebinds the function's own local rather than introducing a
    /// separate one. A fresh empty set per read (not a static field): every arm below copies
    /// before adding, and StaticStateConformanceTests refuses a static mutable collection.</summary>
    private static HashSet<string> NoShadow => new();

    /// <summary>
    /// The ONE roster of binding forms: enumerates <paramref name="node"/>'s children paired with
    /// the set of names shadowed for each — that is, the names bound by a scope-introducing
    /// construct, over exactly the sub-tree where that binding is in force.
    ///
    /// <para>This is the answer to "judge by SCOPE, not by name" (#1910 / the #1681 follow-up). Every
    /// reader of a deferred body — <see cref="CollectDeferredReads"/>, <see cref="CollectReadsFromExpr"/>
    /// and <see cref="CollectWriteThroughs"/> — descends through here, so the read side and the
    /// write-through side cannot drift apart. The arms, one per construct that binds:</para>
    /// <list type="bullet">
    /// <item><description><see cref="FunctionDef"/> — parameters plus the body-flat forms
    /// (<see cref="CollectBodyFlatBindings"/>), over the body. A parameter DEFAULT is evaluated in
    /// the ENCLOSING scope, so it keeps the outer set.</description></item>
    /// <item><description><see cref="LambdaExpression"/> — parameters, over the body.
    /// <see cref="LambdaExpression.GetChildNodes"/> excludes the parameters from traversal, which
    /// is why nothing bound them before: that exclusion stops them being read, not being
    /// BOUND.</description></item>
    /// <item><description><see cref="ForStatement"/> — the target, over target/body/else. The
    /// ITERATOR keeps the outer set (it is evaluated before the target binds), which is what leaves
    /// `for i in k` a genuine read of an outer `k`.</description></item>
    /// <item><description><see cref="ForClause"/> — a comprehension's or generator expression's own
    /// target, same split. Reached with the comprehension's full target set for every clause but the
    /// first, whose iterable python3 evaluates in the enclosing scope.</description></item>
    /// <item><description>The five comprehension/generator kinds — every clause target, over the
    /// element/key/value/spread and the <c>if</c> clauses.</description></item>
    /// <item><description><see cref="WithStatement"/> — each item's <c>as</c> target, over the
    /// following items and the body. Only a name-binding target binds
    /// (<see cref="CollectAssignedNames"/>); `with cm() as p.x` is a read of `p`.</description></item>
    /// <item><description><see cref="TryStatement"/> — a handler's <c>except … as</c> name, over
    /// that handler's filter and body only; the try/else/finally bodies keep the outer
    /// set.</description></item>
    /// <item><description><see cref="MatchStatement"/> / <see cref="MatchExpression"/> — each case's
    /// pattern captures at every sub-pattern depth (via the CFG builder's own roster,
    /// <c>ControlFlowGraphBuilder.CollectPatternBindingKeysInto</c>), over that case's
    /// pattern, guard and body. The scrutinee keeps the outer set.</description></item>
    /// </list>
    ///
    /// <para>Scoping each form to its own sub-tree rather than to the whole enclosing body is what
    /// keeps the write-through mirror sound: `x = 5` AFTER a `for x in …` inside a nested def is
    /// outside the loop's sub-tree, so it still writes through to the enclosing local (measured: it
    /// prints 5), while `x = 9` INSIDE the loop body assigns the loop variable and does not.</para>
    ///
    /// <para>The default arm — every construct that binds nothing — passes the set through
    /// unchanged. Adding an AST node that BINDS a name means adding an arm here; totality of the arm
    /// set is pinned by <c>ScopeBindingFormMatrixTests</c>.</para>
    /// </summary>
    private static IEnumerable<(Node Child, HashSet<string> Shadowed)> ScopedChildren(
        Node node, HashSet<string> shadowed)
    {
        switch (node)
        {
            case FunctionDef functionDef:
            {
                var inner = new HashSet<string>(shadowed);
                foreach (var param in functionDef.Parameters)
                    inner.Add(param.Name);
                foreach (var bodyStmt in functionDef.Body)
                    CollectBodyFlatBindings(bodyStmt, inner);

                foreach (var param in functionDef.Parameters)
                {
                    if (param.DefaultValue != null)
                        yield return (param.DefaultValue, shadowed);
                }
                foreach (var bodyStmt in functionDef.Body)
                    yield return (bodyStmt, inner);
                yield break;
            }

            case LambdaExpression lambda:
            {
                var inner = new HashSet<string>(shadowed);
                foreach (var param in lambda.Parameters)
                    inner.Add(param.Name);

                foreach (var param in lambda.Parameters)
                {
                    if (param.DefaultValue != null)
                        yield return (param.DefaultValue, shadowed);
                }
                yield return (lambda.Body, inner);
                yield break;
            }

            case ForStatement forStatement:
            {
                var inner = new HashSet<string>(shadowed);
                CollectAssignedNames(forStatement.Target, inner);

                yield return (forStatement.Iterator, shadowed);
                yield return (forStatement.Target, inner);
                foreach (var bodyStmt in forStatement.Body)
                    yield return (bodyStmt, inner);
                foreach (var elseStmt in forStatement.ElseBody)
                    yield return (elseStmt, inner);
                yield break;
            }

            case ForClause forClause:
            {
                var inner = new HashSet<string>(shadowed);
                CollectAssignedNames(forClause.Target, inner);

                yield return (forClause.Target, inner);
                yield return (forClause.Iterator, shadowed);
                yield break;
            }

            case ListComprehension:
            case SetComprehension:
            case DictComprehension:
            case DictSpreadComprehension:
            case GeneratorExpression:
            {
                var inner = new HashSet<string>(shadowed);
                ForClause? firstForClause = null;
                foreach (var clause in ComprehensionClausesOf(node))
                {
                    if (clause is not ForClause forClause)
                        continue;
                    firstForClause ??= forClause;
                    CollectAssignedNames(forClause.Target, inner);
                }

                foreach (var child in node.GetChildNodes())
                {
                    // The FIRST for-clause's iterable is evaluated in the ENCLOSING scope
                    // (python3: `[k for k in k]` reads the outer `k`), so it is handed the outer
                    // set and its own ForClause arm re-adds only its own target.
                    yield return (child, ReferenceEquals(child, firstForClause) ? shadowed : inner);
                }
                yield break;
            }

            case WithStatement withStatement:
            {
                var running = shadowed;
                foreach (var item in withStatement.Items)
                {
                    yield return (item.ContextExpression, running);
                    if (item.Target == null)
                        continue;
                    var next = new HashSet<string>(running);
                    CollectAssignedNames(item.Target, next);
                    running = next;
                    yield return (item.Target, running);
                }
                foreach (var bodyStmt in withStatement.Body)
                    yield return (bodyStmt, running);
                yield break;
            }

            case TryStatement tryStatement:
            {
                foreach (var bodyStmt in tryStatement.Body)
                    yield return (bodyStmt, shadowed);
                foreach (var handler in tryStatement.Handlers)
                {
                    var inner = shadowed;
                    if (!string.IsNullOrEmpty(handler.Name))
                        inner = new HashSet<string>(shadowed) { handler.Name! };
                    if (handler.Filter != null)
                        yield return (handler.Filter, inner);
                    foreach (var handlerStmt in handler.Body)
                        yield return (handlerStmt, inner);
                }
                foreach (var elseStmt in tryStatement.ElseBody)
                    yield return (elseStmt, shadowed);
                foreach (var finallyStmt in tryStatement.FinallyBody)
                    yield return (finallyStmt, shadowed);
                yield break;
            }

            case MatchStatement matchStatement:
            {
                yield return (matchStatement.Scrutinee, shadowed);
                foreach (var matchCase in matchStatement.Cases)
                {
                    var inner = WithPatternCaptures(shadowed, matchCase.Pattern);
                    yield return (matchCase.Pattern, inner);
                    if (matchCase.Guard != null)
                        yield return (matchCase.Guard, inner);
                    foreach (var bodyStmt in matchCase.Body)
                        yield return (bodyStmt, inner);
                }
                yield break;
            }

            case MatchExpression matchExpression:
            {
                yield return (matchExpression.Scrutinee, shadowed);
                foreach (var arm in matchExpression.Arms)
                {
                    var inner = WithPatternCaptures(shadowed, arm.Pattern);
                    yield return (arm.Pattern, inner);
                    if (arm.Guard != null)
                        yield return (arm.Guard, inner);
                    yield return (arm.Result, inner);
                }
                yield break;
            }

            default:
            {
                foreach (var child in node.GetChildNodes())
                    yield return (child, shadowed);
                yield break;
            }
        }
    }

    /// <summary>
    /// <paramref name="shadowed"/> plus every name <paramref name="pattern"/> captures, asking the
    /// CFG builder's rostered pattern-capture collector rather than re-deriving the sub-pattern
    /// roster here (one authority, guarded by <c>CfgPatternBindingTotalityTests</c>).
    /// </summary>
    private static HashSet<string> WithPatternCaptures(HashSet<string> shadowed, Pattern pattern)
    {
        var captures = new List<string>();
        ControlFlowGraphBuilder.CollectPatternBindingKeysInto(pattern, captures);
        if (captures.Count == 0)
            return shadowed;

        var inner = new HashSet<string>(shadowed);
        foreach (var capture in captures)
            inner.Add(capture);
        return inner;
    }

    /// <summary>The clause list of any comprehension or generator expression; empty for anything
    /// else. An <c>if</c>-chain rather than a switch: this is a property projection over the five
    /// kinds the caller has already matched, not a dispatch decision of its own.</summary>
    private static ImmutableArray<ComprehensionClause> ComprehensionClausesOf(Node node)
    {
        if (node is ListComprehension listComprehension)
            return listComprehension.Clauses;
        if (node is SetComprehension setComprehension)
            return setComprehension.Clauses;
        if (node is DictComprehension dictComprehension)
            return dictComprehension.Clauses;
        if (node is DictSpreadComprehension dictSpreadComprehension)
            return dictSpreadComprehension.Clauses;
        if (node is GeneratorExpression generatorExpression)
            return generatorExpression.Clauses;
        return ImmutableArray<ComprehensionClause>.Empty;
    }

    /// <summary>
    /// Finds every nested <c>def</c> reachable from <paramref name="node"/> and adds each one's
    /// WRITE-THROUGH assignment targets to <paramref name="assignedAnywhere"/> — the write-side
    /// counterpart of <see cref="CollectNestedDefDeferredReads"/> (#1681 follow-up). Mirrors that
    /// method's discovery shape: a nested def never becomes a block statement of the enclosing
    /// function's CFG, so this also has to walk the raw AST rather than <c>block.Statements</c>.
    /// Skips a <see cref="LambdaExpression"/> (its body is a single expression — it cannot contain
    /// an assignment STATEMENT at all, write-through or otherwise).
    /// </summary>
    private static void CollectNestedDefWriteThroughs(Node node, HashSet<string> assignedAnywhere)
    {
        if (node is LambdaExpression)
            return;

        if (node is FunctionDef nestedDef)
        {
            CollectWriteThroughs(nestedDef, assignedAnywhere, new HashSet<string>());
            return;
        }

        foreach (var child in node.GetChildNodes())
            CollectNestedDefWriteThroughs(child, assignedAnywhere);
    }

    /// <summary>
    /// Adds every <c>Assignment</c> target name reachable from <paramref name="node"/> to
    /// <paramref name="assignedAnywhere"/>, EXCEPT one in <paramref name="shadowed"/> — a name a
    /// construct traversal has entered BINDS, which is a SEPARATE name scoped to that construct
    /// rather than a write-through to the outer scope's same-named local (C# scoping, Axiom 1; the
    /// owner's write-through ruling only reaches a NON-shadowed name). The shadow sets come from
    /// <see cref="ScopedChildren"/> — the same roster <see cref="CollectDeferredReads"/> descends
    /// through, so the read side and the write side cannot disagree about what a name means, and
    /// shadowing composes at any depth.
    ///
    /// <para>Sub-tree scoping is load-bearing here, not just tidy: `x = 5` after a `for x in …`
    /// inside a nested def is outside the loop's sub-tree and still writes through to the enclosing
    /// local, while `x = 9` inside the loop body assigns the loop variable and must not be credited.
    /// Seeding the shadow set flat over the whole def body would refuse the first, which is the very
    /// class of false refusal this walk exists to avoid.</para>
    /// </summary>
    private static void CollectWriteThroughs(Node node, HashSet<string> assignedAnywhere, HashSet<string> shadowed)
    {
        // A lambda's body is a single expression: it cannot contain an assignment STATEMENT at all.
        if (node is LambdaExpression)
            return;

        if (node is Assignment { Operator: AssignmentOperator.Assign } assignment)
        {
            var targets = new HashSet<string>();
            CollectAssignedNames(assignment.Target, targets);
            foreach (var name in targets)
            {
                if (!shadowed.Contains(name))
                    assignedAnywhere.Add(name);
            }
        }

        foreach (var (child, childShadowed) in ScopedChildren(node, shadowed))
            CollectWriteThroughs(child, assignedAnywhere, childShadowed);
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
