using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Analysis.ControlFlow;

/// <summary>
/// The kind of symbolic narrowing an action performs.
/// </summary>
internal enum NarrowingActionKind
{
    /// <summary>Removes <c>None</c> from the keyed value's type (<c>x is not None</c> / <c>x is None</c> else-branch).</summary>
    RemoveNone,

    /// <summary>Narrows the keyed value to a target type (<c>isinstance(x, T)</c>).</summary>
    IsType
}

/// <summary>
/// A single type-narrowing fact: a symbolic action keyed by a narrowing-key path
/// (see <see cref="AstHelper.ExtractNarrowingKey"/>).
/// </summary>
/// <remarks>
/// <para>
/// Facts are deliberately <em>symbolic</em>: they name an action (<see cref="NarrowingActionKind.RemoveNone"/>,
/// <see cref="NarrowingActionKind.IsType"/>) and a textual key, but carry no resolved
/// <c>SemanticType</c>. Resolution against the value's live type happens at read time in the
/// TypeChecker (a later task), which keeps this dataflow engine free of TypeChecker state and
/// sidesteps the "locally-inferred types don't exist yet" ordering problem.
/// </para>
/// <para>
/// For <see cref="NarrowingActionKind.IsType"/> facts, <see cref="TypeExpression"/> holds the AST
/// type argument so the reader can resolve it later; equality/identity is based on the stable
/// <see cref="TypeKey"/> textual descriptor rather than the AST node, so two <c>isinstance(x, T)</c>
/// checks against the same <c>T</c> at different source locations produce equal facts (and thus
/// survive an intersection join).
/// </para>
/// </remarks>
internal sealed class NarrowingFact : IEquatable<NarrowingFact>
{
    /// <summary>The narrowing-key path (e.g. <c>x</c>, <c>self.value</c>, <c>t[0]</c>).</summary>
    public string Key { get; }

    /// <summary>The kind of narrowing this fact asserts.</summary>
    public NarrowingActionKind Kind { get; }

    /// <summary>
    /// For <see cref="NarrowingActionKind.IsType"/>: a stable textual descriptor of the target
    /// type expression, used for equality. Null for <see cref="NarrowingActionKind.RemoveNone"/>.
    /// </summary>
    public string? TypeKey { get; }

    /// <summary>
    /// For <see cref="NarrowingActionKind.IsType"/>: the AST type argument, retained so the reader
    /// can resolve it against live types at read time. Not part of equality.
    /// </summary>
    public Expression? TypeExpression { get; }

    private NarrowingFact(string key, NarrowingActionKind kind, string? typeKey, Expression? typeExpression)
    {
        Key = key;
        Kind = kind;
        TypeKey = typeKey;
        TypeExpression = typeExpression;
    }

    /// <summary>Creates a <c>RemoveNone</c> fact for the given key.</summary>
    public static NarrowingFact RemoveNone(string key) =>
        new(key, NarrowingActionKind.RemoveNone, typeKey: null, typeExpression: null);

    /// <summary>Creates an <c>IsType</c> fact for the given key and target type expression.</summary>
    public static NarrowingFact IsType(string key, string typeKey, Expression typeExpression) =>
        new(key, NarrowingActionKind.IsType, typeKey, typeExpression);

    public bool Equals(NarrowingFact? other) =>
        other is not null && Key == other.Key && Kind == other.Kind && TypeKey == other.TypeKey;

    public override bool Equals(object? obj) => Equals(obj as NarrowingFact);

    public override int GetHashCode() => HashCode.Combine(Key, Kind, TypeKey);

    public override string ToString() => Kind == NarrowingActionKind.RemoveNone
        ? $"{Key}: RemoveNone"
        : $"{Key}: IsType({TypeKey})";
}

/// <summary>
/// Pure structural recogniser that maps a branch condition to the set of narrowing facts that
/// hold on one of its branches. Shared by the dataflow engine here and (later) by the TypeChecker,
/// so both agree on which condition shapes narrow which keys.
/// </summary>
/// <remarks>
/// This mirrors the recursive structure of the TypeChecker's existing <c>ExtractNarrowedTypes</c>
/// (parentheses, <c>not</c>, <c>and</c>/<c>or</c> De Morgan, <c>is</c>/<c>is not None</c>,
/// <c>isinstance</c>) but yields <em>symbolic</em> facts instead of resolved types. It carries no
/// semantic state, so it can run before type checking.
/// </remarks>
internal static class NarrowingConditionInterpreter
{
    /// <summary>The builtin predicate whose truth narrows its first argument's type.</summary>
    private const string IsInstance = "isinstance";

    /// <summary>
    /// Yields the narrowing facts that hold when <paramref name="condition"/> evaluates to the
    /// branch selected by <paramref name="onTrueBranch"/> (<c>true</c> for the then-branch,
    /// <c>false</c> for the else-branch).
    /// </summary>
    public static IEnumerable<NarrowingFact> Recognize(Expression condition, bool onTrueBranch)
    {
        switch (condition)
        {
            case Parenthesized paren:
                return Recognize(paren.Expression, onTrueBranch);

            // `not E` flips the branch polarity.
            case UnaryOp { Operator: UnaryOperator.Not } notOp:
                return Recognize(notOp.Operand, !onTrueBranch);

            // then-branch of `A and B`: both hold.
            case BinaryOp { Operator: BinaryOperator.And } andOp when onTrueBranch:
                return Recognize(andOp.Left, true).Concat(Recognize(andOp.Right, true));

            // else-branch of `A or B` is the then-branch of `not A and not B` (De Morgan): both hold.
            case BinaryOp { Operator: BinaryOperator.Or } orOp when !onTrueBranch:
                return Recognize(orOp.Left, false).Concat(Recognize(orOp.Right, false));

            // then-branch of `x is not None` removes None from x.
            case BinaryOp { Operator: BinaryOperator.IsNot, Right: NoneLiteral } isNot when onTrueBranch:
                return RemoveNoneFact(isNot.Left);

            // else-branch of `x is None` removes None from x.
            case BinaryOp { Operator: BinaryOperator.Is, Right: NoneLiteral } isOp when !onTrueBranch:
                return RemoveNoneFact(isOp.Left);

            // then-branch of `isinstance(x, T)` narrows x to T.
            case FunctionCall { Function: Identifier { Name: IsInstance } } call
                when onTrueBranch && call.Arguments.Length >= 2:
                return IsInstanceFact(call.Arguments[0], call.Arguments[1]);

            default:
                return Enumerable.Empty<NarrowingFact>();
        }
    }

    private static IEnumerable<NarrowingFact> RemoveNoneFact(Expression narrowed)
    {
        var key = AstHelper.ExtractNarrowingKey(narrowed);
        return key == null
            ? Enumerable.Empty<NarrowingFact>()
            : new[] { NarrowingFact.RemoveNone(key) };
    }

    private static IEnumerable<NarrowingFact> IsInstanceFact(Expression narrowed, Expression typeArgument)
    {
        var key = AstHelper.ExtractNarrowingKey(narrowed);
        var typeKey = DescribeTypeExpression(typeArgument);
        return key == null || typeKey == null
            ? Enumerable.Empty<NarrowingFact>()
            : new[] { NarrowingFact.IsType(key, typeKey, typeArgument) };
    }

    /// <summary>
    /// Produces a stable textual descriptor for an <c>isinstance</c> type argument, used as the
    /// fact's equality key. Handles bare type names and module-qualified names
    /// (<c>isinstance(x, mod.Type)</c>); returns null for shapes we cannot key.
    /// </summary>
    private static string? DescribeTypeExpression(Expression typeExpression) => typeExpression switch
    {
        Identifier id => id.Name,
        MemberAccess ma when DescribeTypeExpression(ma.Object) is { } objKey => $"{objKey}.{ma.Member}",
        _ => null
    };
}

/// <summary>
/// The computed narrowing facts for one control flow graph: the fact set in effect at each block
/// entry and immediately before each statement executes.
/// </summary>
internal sealed class NarrowingFlowResult
{
    private static readonly IReadOnlyCollection<NarrowingFact> Empty = Array.Empty<NarrowingFact>();

    private readonly IReadOnlyDictionary<BasicBlock, IReadOnlyCollection<NarrowingFact>> _blockEntryFacts;
    private readonly IReadOnlyDictionary<Statement, IReadOnlyCollection<NarrowingFact>> _statementFacts;

    internal NarrowingFlowResult(
        IReadOnlyDictionary<BasicBlock, IReadOnlyCollection<NarrowingFact>> blockEntryFacts,
        IReadOnlyDictionary<Statement, IReadOnlyCollection<NarrowingFact>> statementFacts)
    {
        _blockEntryFacts = blockEntryFacts;
        _statementFacts = statementFacts;
    }

    /// <summary>
    /// The narrowing facts that hold on entry to <paramref name="block"/> (before its first
    /// statement executes and before its terminator's branch facts are applied).
    /// </summary>
    public IReadOnlyCollection<NarrowingFact> FactsAtEntry(BasicBlock block) =>
        _blockEntryFacts.TryGetValue(block, out var facts) ? facts : Empty;

    /// <summary>
    /// The narrowing facts that hold immediately before <paramref name="statement"/> executes.
    /// Returns an empty set for statements not in the analysed graph (e.g. bodies of nested
    /// function definitions, which form their own graphs).
    /// </summary>
    public IReadOnlyCollection<NarrowingFact> FactsBefore(Statement statement) =>
        _statementFacts.TryGetValue(statement, out var facts) ? facts : Empty;
}

/// <summary>
/// Forward dataflow analysis that computes type-narrowing facts over a <see cref="ControlFlowGraph"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Facts</b> are symbolic <see cref="NarrowingFact"/>s (see that type). <b>Gen</b> happens on the
/// branch edges of a <see cref="ConditionalBranchTerminator"/> (both polarities: <c>x is not None</c>
/// narrows the then-edge, <c>x is None</c> narrows the else-edge) and on <c>assert</c> statements.
/// <b>Kill</b> happens on assignment to a key or any prefix of it (assigning <c>x</c> kills facts about
/// <c>x</c>, <c>x.y</c>, and <c>x[0]</c>). The <b>join</b> at merge points is set intersection: a fact
/// holds after a merge only if it holds on every incoming edge. Exception predecessors contribute the
/// empty set (an exception can interrupt a block mid-way), so any block reachable by an exception edge
/// starts from no facts.
/// </para>
/// <para>
/// The fixed point is computed in reverse post-order with optimistic initialisation (every non-entry
/// block starts holding the full universe of possible facts, narrowed down to a stable set), following
/// the <see cref="DefiniteFieldAssignmentAnalysis"/> template. Because the CFG builder does not descend
/// into nested function definitions or lambdas, those bodies form their own graphs and never see the
/// enclosing scope's facts — narrowing is naturally per-function.
/// </para>
/// </remarks>
internal static class NarrowingFlowAnalysis
{
    /// <summary>
    /// Runs the analysis over <paramref name="cfg"/> and returns the narrowing facts in effect at each
    /// block entry and before each statement.
    /// </summary>
    public static NarrowingFlowResult Analyze(ControlFlowGraph cfg)
    {
        // Universe of all facts that could ever be generated anywhere in the graph. Needed for the
        // optimistic (full-set) initialisation of the intersection fixed point.
        var universe = CollectUniverse(cfg);

        if (universe.Count == 0)
        {
            // No condition or assert narrows anything: every point holds the empty set.
            var emptyBlocks = new Dictionary<BasicBlock, IReadOnlyCollection<NarrowingFact>>(ReferenceEqualityComparer.Instance);
            var emptyStatements = new Dictionary<Statement, IReadOnlyCollection<NarrowingFact>>(ReferenceEqualityComparer.Instance);
            return new NarrowingFlowResult(emptyBlocks, emptyStatements);
        }

        // out[entry] = empty; out[everything else] = universe (optimistic).
        var outSets = new Dictionary<BasicBlock, HashSet<NarrowingFact>>(ReferenceEqualityComparer.Instance);
        foreach (var block in cfg.Blocks)
        {
            outSets[block] = block == cfg.Entry
                ? new HashSet<NarrowingFact>()
                : new HashSet<NarrowingFact>(universe);
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

                if (!TryComputeInSet(block, outSets, out var inSet))
                    continue; // unreachable block — leave its optimistic out-set untouched

                var newOut = Transfer(block, inSet, recordStatementFacts: null);

                if (!newOut.SetEquals(outSets[block]))
                {
                    outSets[block] = newOut;
                    changed = true;
                }
            }
        }

        // Final pass: materialise the per-block-entry and per-statement fact sets from the converged
        // in-sets. The transfer function re-threads the block's statements to snapshot the facts in
        // effect just before each one.
        var blockEntryFacts = new Dictionary<BasicBlock, IReadOnlyCollection<NarrowingFact>>(ReferenceEqualityComparer.Instance);
        var statementFacts = new Dictionary<Statement, IReadOnlyCollection<NarrowingFact>>(ReferenceEqualityComparer.Instance);

        foreach (var block in cfg.Blocks)
        {
            if (block == cfg.Entry)
                continue;

            if (!TryComputeInSet(block, outSets, out var inSet))
                continue;

            blockEntryFacts[block] = Freeze(inSet);
            Transfer(block, inSet, recordStatementFacts: statementFacts);
        }

        return new NarrowingFlowResult(blockEntryFacts, statementFacts);
    }

    /// <summary>
    /// Computes in[block] as the intersection of the edge fact sets of all normal predecessors, or
    /// the empty set if any exception predecessor exists. Returns false when the block has no
    /// predecessors at all (unreachable).
    /// </summary>
    private static bool TryComputeInSet(
        BasicBlock block,
        IReadOnlyDictionary<BasicBlock, HashSet<NarrowingFact>> outSets,
        out HashSet<NarrowingFact> inSet)
    {
        if (block.ExceptionPredecessors.Count > 0)
        {
            // An exception can transfer control here mid-block, so no fact is guaranteed.
            inSet = new HashSet<NarrowingFact>();
            return true;
        }

        if (block.Predecessors.Count == 0)
        {
            inSet = new HashSet<NarrowingFact>();
            return false;
        }

        HashSet<NarrowingFact>? accumulated = null;
        foreach (var pred in block.Predecessors)
        {
            var edgeFacts = EdgeFacts(pred, block, outSets[pred]);
            if (accumulated == null)
            {
                accumulated = edgeFacts;
            }
            else
            {
                accumulated.IntersectWith(edgeFacts);
            }
        }

        inSet = accumulated ?? new HashSet<NarrowingFact>();
        return true;
    }

    /// <summary>
    /// The facts flowing along the edge <paramref name="pred"/> → <paramref name="successor"/>: the
    /// predecessor's out-set, plus the branch facts generated by a conditional terminator for the
    /// polarity that reaches this successor.
    /// </summary>
    private static HashSet<NarrowingFact> EdgeFacts(BasicBlock pred, BasicBlock successor, HashSet<NarrowingFact> predOut)
    {
        var facts = new HashSet<NarrowingFact>(predOut);

        if (pred.Terminator is ConditionalBranchTerminator branch)
        {
            bool toTrue = ReferenceEquals(successor, branch.TrueTarget);
            bool toFalse = ReferenceEquals(successor, branch.FalseTarget);

            if (toTrue && toFalse)
            {
                // Both polarities reach the same block: only facts common to both edges are known.
                var trueFacts = new HashSet<NarrowingFact>(NarrowingConditionInterpreter.Recognize(branch.Condition, true));
                trueFacts.IntersectWith(NarrowingConditionInterpreter.Recognize(branch.Condition, false));
                facts.UnionWith(trueFacts);
            }
            else if (toTrue)
            {
                facts.UnionWith(NarrowingConditionInterpreter.Recognize(branch.Condition, true));
            }
            else if (toFalse)
            {
                facts.UnionWith(NarrowingConditionInterpreter.Recognize(branch.Condition, false));
            }
        }

        return facts;
    }

    /// <summary>
    /// Applies a block's statements to <paramref name="inSet"/> in order — killing facts on
    /// assignment and generating facts on <c>assert</c> — and returns the resulting out-set. When
    /// <paramref name="recordStatementFacts"/> is non-null, snapshots the facts in effect just before
    /// each statement into it.
    /// </summary>
    private static HashSet<NarrowingFact> Transfer(
        BasicBlock block,
        HashSet<NarrowingFact> inSet,
        Dictionary<Statement, IReadOnlyCollection<NarrowingFact>>? recordStatementFacts)
    {
        var facts = new HashSet<NarrowingFact>(inSet);

        foreach (var statement in block.Statements)
        {
            if (recordStatementFacts != null)
                recordStatementFacts[statement] = Freeze(facts);

            Kill(facts, statement);
            Gen(facts, statement);
        }

        return facts;
    }

    /// <summary>Removes facts invalidated by an assignment to the statement's target(s).</summary>
    private static void Kill(HashSet<NarrowingFact> facts, Statement statement)
    {
        if (statement is not Assignment assignment)
            return;

        foreach (var assignedKey in CollectAssignedKeys(assignment.Target))
        {
            facts.RemoveWhere(fact => KeyIsInvalidatedBy(fact.Key, assignedKey));
        }
    }

    /// <summary>Adds facts implied by an <c>assert</c> statement (its condition must hold afterward).</summary>
    private static void Gen(HashSet<NarrowingFact> facts, Statement statement)
    {
        if (statement is AssertStatement assert)
        {
            facts.UnionWith(NarrowingConditionInterpreter.Recognize(assert.Test, true));
        }
    }

    /// <summary>
    /// Collects the narrowing keys written by an assignment target, recursing through tuple targets
    /// (unpacking) and unwrapping parentheses. Targets that cannot be keyed contribute nothing —
    /// consistent with the key system, which also cannot narrow such expressions.
    /// </summary>
    private static IEnumerable<string> CollectAssignedKeys(Expression target)
    {
        switch (target)
        {
            case Parenthesized paren:
                foreach (var key in CollectAssignedKeys(paren.Expression))
                    yield return key;
                break;

            case TupleLiteral tuple:
                foreach (var element in tuple.Elements)
                    foreach (var key in CollectAssignedKeys(element))
                        yield return key;
                break;

            default:
                var targetKey = AstHelper.ExtractNarrowingKey(target);
                if (targetKey != null)
                    yield return targetKey;
                break;
        }
    }

    /// <summary>
    /// True if a fact keyed <paramref name="factKey"/> is invalidated by an assignment to
    /// <paramref name="assignedKey"/>: an exact match, or the assigned key is a path prefix of the
    /// fact key (delimited by <c>.</c> or <c>[</c>, so <c>x</c> invalidates <c>x.y</c> and <c>x[0]</c>
    /// but not <c>xy</c>).
    /// </summary>
    private static bool KeyIsInvalidatedBy(string factKey, string assignedKey)
    {
        if (factKey == assignedKey)
            return true;

        if (factKey.Length > assignedKey.Length && factKey.StartsWith(assignedKey, StringComparison.Ordinal))
        {
            char boundary = factKey[assignedKey.Length];
            return boundary == '.' || boundary == '[';
        }

        return false;
    }

    /// <summary>
    /// Gathers every fact that any conditional branch edge or assert statement in the graph could
    /// generate. Used to seed the optimistic initialisation of the intersection fixed point.
    /// </summary>
    private static HashSet<NarrowingFact> CollectUniverse(ControlFlowGraph cfg)
    {
        var universe = new HashSet<NarrowingFact>();

        foreach (var block in cfg.Blocks)
        {
            if (block.Terminator is ConditionalBranchTerminator branch)
            {
                universe.UnionWith(NarrowingConditionInterpreter.Recognize(branch.Condition, true));
                universe.UnionWith(NarrowingConditionInterpreter.Recognize(branch.Condition, false));
            }

            foreach (var statement in block.Statements)
            {
                if (statement is AssertStatement assert)
                    universe.UnionWith(NarrowingConditionInterpreter.Recognize(assert.Test, true));
            }
        }

        return universe;
    }

    private static IReadOnlyCollection<NarrowingFact> Freeze(HashSet<NarrowingFact> facts) =>
        facts.Count == 0 ? Array.Empty<NarrowingFact>() : new List<NarrowingFact>(facts);
}
