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

    /// <summary>
    /// The narrowed operand expression at the point the fact was recognised (e.g. the <c>x</c> in
    /// <c>x is not None</c> / <c>isinstance(x, T)</c>). Retained so the condition interpreter can
    /// resolve the operand's type when producing the emitter decision. Not used by the dataflow
    /// engine (which resolves against the read-site type) and not part of equality.
    /// </summary>
    public Expression? SourceExpression { get; }

    private NarrowingFact(string key, NarrowingActionKind kind, string? typeKey, Expression? typeExpression, Expression? sourceExpression)
    {
        Key = key;
        Kind = kind;
        TypeKey = typeKey;
        TypeExpression = typeExpression;
        SourceExpression = sourceExpression;
    }

    /// <summary>Creates a <c>RemoveNone</c> fact for the given key and narrowed operand.</summary>
    public static NarrowingFact RemoveNone(string key, Expression? sourceExpression = null) =>
        new(key, NarrowingActionKind.RemoveNone, typeKey: null, typeExpression: null, sourceExpression);

    /// <summary>Creates an <c>IsType</c> fact for the given key, target type expression and narrowed operand.</summary>
    public static NarrowingFact IsType(string key, string typeKey, Expression typeExpression, Expression? sourceExpression = null) =>
        new(key, NarrowingActionKind.IsType, typeKey, typeExpression, sourceExpression);

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
    /// <param name="denotesBuiltinsModule">Answers whether a member-access receiver denotes the
    /// <c>builtins</c> module, so <c>builtins.isinstance(x, T)</c> narrows exactly as bare
    /// <c>isinstance</c> does (#1381, the #1322 agreement contract). REQUIRED, deliberately: an
    /// optional parameter would mean narrowing recognition knows about qualified spellings down
    /// some call paths and not others — one rule with two behaviours depending on how you arrived —
    /// and a missed call site would change narrowing silently instead of failing to build.</param>
    public static IEnumerable<NarrowingFact> Recognize(
        Expression condition, bool onTrueBranch, Func<Expression, bool> denotesBuiltinsModule)
    {
        switch (condition)
        {
            case Parenthesized paren:
                return Recognize(paren.Expression, onTrueBranch, denotesBuiltinsModule);

            // `not E` flips the branch polarity.
            case UnaryOp { Operator: UnaryOperator.Not } notOp:
                return Recognize(notOp.Operand, !onTrueBranch, denotesBuiltinsModule);

            // then-branch of `A and B`: both hold.
            case BinaryOp { Operator: BinaryOperator.And } andOp when onTrueBranch:
                return Recognize(andOp.Left, true, denotesBuiltinsModule)
                    .Concat(Recognize(andOp.Right, true, denotesBuiltinsModule));

            // else-branch of `A or B` is the then-branch of `not A and not B` (De Morgan): both hold.
            case BinaryOp { Operator: BinaryOperator.Or } orOp when !onTrueBranch:
                return Recognize(orOp.Left, false, denotesBuiltinsModule)
                    .Concat(Recognize(orOp.Right, false, denotesBuiltinsModule));

            default:
                return RecognizeLeaf(condition, onTrueBranch, denotesBuiltinsModule);
        }
    }

    /// <summary>
    /// Recognises the narrowing facts from a single <em>leaf</em> condition (no boolean/parenthesis
    /// composition): <c>x is not None</c> / <c>x is None</c> / <c>isinstance(x, T)</c>. Shared with the
    /// TypeChecker's condition interpreter (which handles the composition and resolves the recognised
    /// facts to concrete types + the emitter decision) so both agree on the leaf grammar (#1042).
    /// </summary>
    public static IEnumerable<NarrowingFact> RecognizeLeaf(
        Expression condition, bool onTrueBranch, Func<Expression, bool> denotesBuiltinsModule)
    {
        switch (condition)
        {
            // then-branch of `x is not None` / `x != None` removes None from x.
            case BinaryOp { Operator: BinaryOperator.IsNot, Right: NoneLiteral } isNot when onTrueBranch:
                return RemoveNoneFact(isNot.Left);
            // Unreachable from Sharpy source today: `== None`/`!= None` is rejected by the type checker
            // (only `is`/`is not None` are supported). These cases keep the recognizer complete and are
            // exercised by engine unit tests; they become live if the operator is ever supported — see #1079.
            case BinaryOp { Operator: BinaryOperator.NotEqual, Right: NoneLiteral } notEq when onTrueBranch:
                return RemoveNoneFact(notEq.Left);

            // else-branch of `x is None` / `x == None` removes None from x.
            case BinaryOp { Operator: BinaryOperator.Is, Right: NoneLiteral } isOp when !onTrueBranch:
                return RemoveNoneFact(isOp.Left);
            // Unreachable from Sharpy source today — see #1079 (as above).
            case BinaryOp { Operator: BinaryOperator.Equal, Right: NoneLiteral } eq when !onTrueBranch:
                return RemoveNoneFact(eq.Left);

            // then-branch of `isinstance(x, T)` narrows x to T. Matched against the canonical
            // (paren-stripped) callee so `(isinstance)(x, T)` narrows identically (#1170, #1168) —
            // the callee shape is what selects the special form, and parentheses do not change it.
            case FunctionCall call
                when onTrueBranch && call.Arguments.Length >= 2
                    && AstHelper.UnwrapParenthesized(call.Function) is Identifier { Name: IsInstance }:
                return IsInstanceFact(call.Arguments[0], call.Arguments[1]);

            // The QUALIFIED spelling narrows identically (#1381). `builtins.isinstance` denotes what
            // bare `isinstance` denotes (#1322), so refusing to narrow it made the qualifier change
            // meaning rather than scope. Recognition stays syntactic — the spelling `mod.isinstance`
            // for an arbitrary `mod` is not enough, which is why the module identity arrives as a
            // threaded predicate rather than being matched here. It cannot be a pre-marked node:
            // ComputeNarrowingFlow runs before the body walk that would set the mark.
            case FunctionCall qualifiedCall
                when onTrueBranch && qualifiedCall.Arguments.Length >= 2
                    && AstHelper.UnwrapParenthesized(qualifiedCall.Function)
                        is MemberAccess { IsMemberBacktickEscaped: false, Member: IsInstance } qualified
                    && denotesBuiltinsModule(qualified.Object):
                return IsInstanceFact(qualifiedCall.Arguments[0], qualifiedCall.Arguments[1]);

            default:
                return Enumerable.Empty<NarrowingFact>();
        }
    }

    private static IEnumerable<NarrowingFact> RemoveNoneFact(Expression narrowed)
    {
        var key = AstHelper.ExtractNarrowingKey(narrowed);
        return key == null
            ? Enumerable.Empty<NarrowingFact>()
            : new[] { NarrowingFact.RemoveNone(key, narrowed) };
    }

    private static IEnumerable<NarrowingFact> IsInstanceFact(Expression narrowed, Expression typeArgument)
    {
        var key = AstHelper.ExtractNarrowingKey(narrowed);
        var typeKey = DescribeTypeExpression(typeArgument);
        return key == null || typeKey == null
            ? Enumerable.Empty<NarrowingFact>()
            : new[] { NarrowingFact.IsType(key, typeKey, typeArgument, narrowed) };
    }

    /// <summary>
    /// Produces a stable textual descriptor for an <c>isinstance</c> type argument, used as the
    /// fact's equality key. Handles bare type names, module-qualified names
    /// (<c>isinstance(x, mod.Type)</c>) and the closed generic spelling
    /// (<c>isinstance(x, Box[int])</c>, <c>isinstance(x, dict[str, int])</c>); returns null for
    /// shapes we cannot key.
    /// <para>
    /// This is a KEYING function, not a type resolution: it stays purely textual so that two checks
    /// against the same written type at different source locations produce equal facts and survive
    /// the intersection join at a CFG merge point. Resolution happens later, in the TypeChecker,
    /// against the type the type-operand classifier recorded.
    /// </para>
    /// <para>Deliberately partial dispatch (documented-by-design in DispatchSiteInventoryTests):
    /// the default returns null for un-keyable forms and no semantic decision keys on this
    /// switch — an unkeyed type test simply produces no narrowing fact.</para>
    /// </summary>
    private static string? DescribeTypeExpression(Expression typeExpression) => typeExpression switch
    {
        // (A, B) denotes tuple[A, B] in a type position (#1532); (T) denotes tuple[T].
        Parenthesized paren => DescribeTupleTypeExpression(paren.Expression),
        TupleLiteral tuple => DescribeTupleElements(tuple),
        Identifier id => id.Name,
        MemberAccess ma when DescribeTypeExpression(ma.Object) is { } objKey => $"{objKey}.{ma.Member}",
        IndexAccess { Object: { } baseName } index
            when DescribeTypeExpression(baseName) is { } baseKey
                && DescribeTypeArgumentList(index.Index) is { } argsKey => $"{baseKey}[{argsKey}]",
        _ => null
    };

    /// <summary>
    /// Narrowing key builder for a parenthesized type expression — delegates to
    /// <see cref="DescribeTypeExpression"/> or <see cref="DescribeTupleElements"/>.
    /// Returns null for un-keyable forms; no semantic decision keys on this switch.
    /// </summary>
    private static string? DescribeTupleTypeExpression(Expression inner) => inner switch
    {
        TupleLiteral tuple => DescribeTupleElements(tuple),
        _ when DescribeTypeExpression(inner) is { } key => $"({key})",
        _ => null
    };

    private static string? DescribeTupleElements(TupleLiteral tuple)
    {
        var keys = new List<string>();
        foreach (var element in tuple.Elements)
        {
            var key = DescribeTypeExpression(element);
            if (key == null)
                return null;
            keys.Add(key);
        }
        return $"({string.Join(", ", keys)})";
    }

    /// <summary>
    /// Describes a generic argument list — a single type expression, or the <c>TupleLiteral</c> a
    /// multi-argument index parses to. Returns null when any element is unkeyable, so a partially
    /// describable list never produces a key two different types could share.
    /// </summary>
    private static string? DescribeTypeArgumentList(Expression index)
    {
        if (index is not TupleLiteral elements)
            return DescribeTypeExpression(index);

        var keys = new List<string>(elements.Elements.Length);
        foreach (var element in elements.Elements)
        {
            if (DescribeTypeExpression(element) is not { } key)
                return null;
            keys.Add(key);
        }
        return string.Join(", ", keys);
    }
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
    private readonly IReadOnlyDictionary<Expression, IReadOnlyCollection<NarrowingFact>> _branchConditionFacts;

    internal NarrowingFlowResult(
        IReadOnlyDictionary<BasicBlock, IReadOnlyCollection<NarrowingFact>> blockEntryFacts,
        IReadOnlyDictionary<Statement, IReadOnlyCollection<NarrowingFact>> statementFacts,
        IReadOnlyDictionary<Expression, IReadOnlyCollection<NarrowingFact>> branchConditionFacts)
    {
        _blockEntryFacts = blockEntryFacts;
        _statementFacts = statementFacts;
        _branchConditionFacts = branchConditionFacts;
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

    /// <summary>
    /// True if <paramref name="statement"/> is a node the analysis tracked (i.e. it appears directly
    /// in a basic block, so its fact set is meaningful). Compound statements whose bodies are
    /// decomposed into other blocks are not tracked; callers inherit the enclosing facts for those.
    /// </summary>
    public bool IsTracked(Statement statement) => _statementFacts.ContainsKey(statement);

    /// <summary>
    /// The narrowing facts that hold when a conditional branch's <paramref name="condition"/> is
    /// evaluated (after the statements preceding the branch in its block, before the branch's own
    /// facts apply). Keyed by the condition expression of a <see cref="ConditionalBranchTerminator"/>
    /// (e.g. an <c>if</c>/<c>while</c> test), so a compound statement's condition can see the
    /// narrowings established by the enclosing flow. Empty for expressions that are not branch
    /// conditions.
    /// </summary>
    public IReadOnlyCollection<NarrowingFact> FactsBeforeBranch(Expression condition) =>
        _branchConditionFacts.TryGetValue(condition, out var facts) ? facts : Empty;
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
    public static NarrowingFlowResult Analyze(ControlFlowGraph cfg, Func<Expression, bool> denotesBuiltinsModule)
    {
        // Universe of all facts that could ever be generated anywhere in the graph. Needed for the
        // optimistic (full-set) initialisation of the intersection fixed point.
        var universe = CollectUniverse(cfg, denotesBuiltinsModule);

        if (universe.Count == 0)
        {
            // No condition or assert narrows anything: every point holds the empty set.
            var emptyBlocks = new Dictionary<BasicBlock, IReadOnlyCollection<NarrowingFact>>(ReferenceEqualityComparer.Instance);
            var emptyStatements = new Dictionary<Statement, IReadOnlyCollection<NarrowingFact>>(ReferenceEqualityComparer.Instance);
            var emptyConditions = new Dictionary<Expression, IReadOnlyCollection<NarrowingFact>>(ReferenceEqualityComparer.Instance);
            return new NarrowingFlowResult(emptyBlocks, emptyStatements, emptyConditions);
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

                if (!TryComputeInSet(block, outSets, denotesBuiltinsModule, out var inSet))
                    continue; // unreachable block — leave its optimistic out-set untouched

                var newOut = Transfer(block, inSet, recordStatementFacts: null, denotesBuiltinsModule);

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
        var branchConditionFacts = new Dictionary<Expression, IReadOnlyCollection<NarrowingFact>>(ReferenceEqualityComparer.Instance);

        foreach (var block in cfg.Blocks)
        {
            if (block == cfg.Entry)
                continue;

            if (!TryComputeInSet(block, outSets, denotesBuiltinsModule, out var inSet))
                continue;

            blockEntryFacts[block] = Freeze(inSet);
            var outSet = Transfer(block, inSet, recordStatementFacts: statementFacts, denotesBuiltinsModule);

            // A conditional branch's condition is evaluated after the block's statements, before the
            // branch facts apply — that is exactly the block out-set.
            if (block.Terminator is ConditionalBranchTerminator branch)
                branchConditionFacts[branch.Condition] = Freeze(outSet);

            // A match subject sits in the same position as an `if` condition: evaluated after the
            // block's statements, before any arm is entered (#1299). It cannot ride a
            // ConditionalBranchTerminator because a match has N targets, so the builder records it
            // on the block instead.
            if (block.MatchSubject is { } subject)
                branchConditionFacts[subject] = Freeze(outSet);

            // Match EXPRESSION subjects evaluated within the block get the same treatment, so
            // CheckMatchExpression can read the narrowing at the subject's evaluation point (#1502).
            foreach (var exprSubject in block.MatchExpressionSubjects)
                branchConditionFacts[exprSubject] = Freeze(outSet);
        }

        return new NarrowingFlowResult(blockEntryFacts, statementFacts, branchConditionFacts);
    }

    /// <summary>
    /// Computes in[block] as the intersection of the edge fact sets of all normal predecessors, or
    /// the empty set if any exception predecessor exists. Returns false when the block has no
    /// predecessors at all (unreachable).
    /// </summary>
    private static bool TryComputeInSet(
        BasicBlock block,
        IReadOnlyDictionary<BasicBlock, HashSet<NarrowingFact>> outSets,
        Func<Expression, bool> denotesBuiltinsModule,
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
            var edgeFacts = EdgeFacts(pred, block, outSets[pred], denotesBuiltinsModule);
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
    private static HashSet<NarrowingFact> EdgeFacts(BasicBlock pred, BasicBlock successor, HashSet<NarrowingFact> predOut, Func<Expression, bool> denotesBuiltinsModule)
    {
        var facts = new HashSet<NarrowingFact>(predOut);

        if (pred.Terminator is ConditionalBranchTerminator branch)
        {
            bool toTrue = ReferenceEquals(successor, branch.TrueTarget);
            bool toFalse = ReferenceEquals(successor, branch.FalseTarget);

            if (toTrue && toFalse)
            {
                // Both polarities reach the same block: only facts common to both edges are known.
                var trueFacts = new HashSet<NarrowingFact>(NarrowingConditionInterpreter.Recognize(branch.Condition, true, denotesBuiltinsModule));
                trueFacts.IntersectWith(NarrowingConditionInterpreter.Recognize(branch.Condition, false, denotesBuiltinsModule));
                facts.UnionWith(trueFacts);
            }
            else if (toTrue)
            {
                facts.UnionWith(NarrowingConditionInterpreter.Recognize(branch.Condition, true, denotesBuiltinsModule));
            }
            else if (toFalse)
            {
                facts.UnionWith(NarrowingConditionInterpreter.Recognize(branch.Condition, false, denotesBuiltinsModule));
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
        Dictionary<Statement, IReadOnlyCollection<NarrowingFact>>? recordStatementFacts,
        Func<Expression, bool> denotesBuiltinsModule)
    {
        var facts = new HashSet<NarrowingFact>(inSet);

        // Variables rebound at block entry (for-loop targets, with-as bindings) lose their facts
        // before the body runs — the name now holds a fresh value (#1042).
        foreach (var rebindKey in block.EntryRebinds)
            facts.RemoveWhere(fact => KeyIsInvalidatedBy(fact.Key, rebindKey));

        foreach (var statement in block.Statements)
        {
            if (recordStatementFacts != null)
                recordStatementFacts[statement] = Freeze(facts);

            Kill(facts, statement);
            Gen(facts, statement, denotesBuiltinsModule);
        }

        return facts;
    }

    /// <summary>
    /// Removes facts invalidated by everything the statement WRITES: its assignment target(s), and
    /// every walrus target reachable from it.
    ///
    /// <para>The walrus half is not a refinement — it is the difference between a narrowing that
    /// describes the variable and one that describes a value the variable no longer holds (#1757).
    /// A walrus writes its target from inside an EXPRESSION, so it can appear in a declaration's
    /// initializer, a call argument or a condition, and none of those is an <see cref="Assignment"/>
    /// statement. Keying only on Assignment left the fact live: inside
    /// <c>if isinstance(x, int):</c>, the statement <c>y: object = (x := "s")</c> was followed by a
    /// <c>print(x)</c> that still read <c>x</c> as <c>int</c> and emitted <c>((int)x!)</c> —
    /// InvalidCastException at runtime, where the statement twin <c>x = "s"</c> prints <c>s</c>.</para>
    ///
    /// <para>Deliberately statement-granular, exactly as the assignment half is: the snapshot in
    /// <see cref="Transfer"/> is taken BEFORE this runs, so a read textually before the walrus in
    /// the SAME statement still sees the fact. That is the same resolution the assignment half has
    /// (an assignment's own RHS reads the pre-store facts), and the same one
    /// <see cref="DefiniteAssignmentAnalysis"/> documents for its walrus collection.</para>
    ///
    /// <para><b>RemoveNone facts survive a store whose value is definitely not None</b> (R-T; the
    /// augmented form was the first case, #1755 B). The soundness argument is syntactic because
    /// this analysis runs before type checking and cannot see the checker's verdict: a
    /// <c>RemoveNone</c> fact says "the slot holds a non-None value"; after <c>x = v</c> it still
    /// does exactly when <c>v</c> is not None. <see cref="ValueIsDefinitelyNotNone"/> admits only
    /// shapes whose value cannot be None whatever the checker decides — literals, collection and
    /// comprehension literals, lambdas, arithmetic/comparison/bitwise operators (an Optional or
    /// <c>T | None</c> operand is refused SPY0222/SPY0223, so an accepted operation is
    /// payload-typed), the boolean operators and conditionals when every arm qualifies, and a read
    /// whose own narrowing key carries a RemoveNone fact. Everything else kills — a call
    /// (including <c>Some(…)</c> and <c>None()</c>, which the checker admits as declared-slot
    /// stores: the value is a wrapper, and <c>g() -> int?</c> may be None), an un-narrowed read,
    /// bare <c>None</c>. Without the survival, the back edge of a <c>for</c>/<c>while</c> body
    /// killed the fact at the loop head, so <c>d = 5</c> inside a loop under
    /// <c>if d is not None:</c> was SPY0604 while the same store in a <c>try</c> body ran; with a
    /// value-blind survival, <c>d = g()</c> would keep a fact the value no longer satisfies and the
    /// next read would <c>.Unwrap()</c> a None at runtime. <c>IsType</c> facts keep today's kill:
    /// the stored value's type may differ from the tested one.</para>
    /// </summary>
    private static void Kill(HashSet<NarrowingFact> facts, Statement statement)
    {
        if (statement is Assignment assignment)
        {
            var valueKeepsNonNone = assignment.Operator != AssignmentOperator.Assign
                || ValueIsDefinitelyNotNone(assignment.Value, facts);

            foreach (var assignedKey in CollectAssignedKeys(assignment.Target))
                KillKey(facts, assignedKey, keepRemoveNone: valueKeepsNonNone);
        }

        foreach (var walrus in CollectWalrusExpressions(statement))
        {
            KillKey(facts, walrus.Target, keepRemoveNone: ValueIsDefinitelyNotNone(walrus.Value, facts));
        }
    }

    /// <summary>
    /// Kills every fact <paramref name="assignedKey"/> invalidates. When the stored value is
    /// definitely not None (<paramref name="keepRemoveNone"/>), the <c>RemoveNone</c> fact on
    /// EXACTLY that key survives — the slot still holds a non-None value. Facts on nested keys
    /// (<c>x.y</c>, <c>x[0]</c>) always die when <c>x</c> is reassigned: the new object's members
    /// are unknown whatever the object itself is, so a surviving <c>x.y</c> fact would let the
    /// next read <c>.Value</c> a None.
    /// </summary>
    private static void KillKey(HashSet<NarrowingFact> facts, string assignedKey, bool keepRemoveNone)
    {
        facts.RemoveWhere(fact => KeyIsInvalidatedBy(fact.Key, assignedKey)
            && !(keepRemoveNone && fact.Kind == NarrowingActionKind.RemoveNone && fact.Key == assignedKey));
    }

    /// <summary>
    /// Whether a stored value cannot be None, decided from its syntactic shape and the facts in
    /// effect before the store (see <see cref="Kill"/> for the soundness argument). Deliberately
    /// partial: the default arm is the conservative answer — a shape this method does not recognise
    /// is treated as possibly None, so an unlisted node kind can only ever kill a fact, never keep
    /// one it should not (documented-by-design; the block-kind and kill-control cells of
    /// StoreTargetMatrixTests and NarrowingFlowAnalysisTests exercise both directions).
    /// </summary>
    private static bool ValueIsDefinitelyNotNone(Expression value, HashSet<NarrowingFact> facts)
    {
        switch (value)
        {
            case Parenthesized paren:
                return ValueIsDefinitelyNotNone(paren.Expression, facts);

            case IntegerLiteral or FloatLiteral or StringLiteral or BytesLiteralExpression
                or FStringLiteral or TStringLiteral or BooleanLiteral
                or ListLiteral or DictLiteral or SetLiteral or TupleLiteral
                or ListComprehension or SetComprehension or DictComprehension or DictSpreadComprehension
                or LambdaExpression:
                return true;

            case NoneLiteral:
                return false;

            case Identifier or MemberAccess or IndexAccess:
                {
                    var key = AstHelper.ExtractNarrowingKey(value);
                    if (key == null)
                        return false;
                    foreach (var fact in facts)
                    {
                        if (fact.Key == key && fact.Kind == NarrowingActionKind.RemoveNone)
                            return true;
                    }
                    return false;
                }

            case BinaryOp binary:
                return binary.Operator switch
                {
                    // `a and b` / `a or b` evaluate to one of their operands.
                    BinaryOperator.And or BinaryOperator.Or
                        => ValueIsDefinitelyNotNone(binary.Left, facts) && ValueIsDefinitelyNotNone(binary.Right, facts),
                    // `a ?? b` evaluates to b when a is None.
                    BinaryOperator.NullCoalesce => ValueIsDefinitelyNotNone(binary.Right, facts),
                    // Arithmetic, comparison, membership, identity and bitwise operators produce a
                    // value; a wrapper operand is refused by the checker (SPY0222).
                    _ => true,
                };

            case ComparisonChain:
                return true;

            case UnaryOp unary:
                return unary.Operator == UnaryOperator.Not || ValueIsDefinitelyNotNone(unary.Operand, facts);

            case ConditionalExpression conditional:
                return ValueIsDefinitelyNotNone(conditional.ThenValue, facts)
                    && ValueIsDefinitelyNotNone(conditional.ElseValue, facts);

            case WalrusExpression walrus:
                return ValueIsDefinitelyNotNone(walrus.Value, facts);

            default:
                // Calls (Some(…), None(), g()), await, casts, slices, maybe/try — possibly None.
                return false;
        }
    }

    /// <summary>
    /// Every walrus in <paramref name="node"/>. Does not descend into a lambda body — a walrus
    /// there binds when the lambda RUNS, not where it is written, which is the same boundary
    /// <see cref="DefiniteAssignmentAnalysis"/> draws.
    /// </summary>
    private static IEnumerable<WalrusExpression> CollectWalrusExpressions(Node node)
    {
        if (node is LambdaExpression)
            yield break;

        if (node is WalrusExpression walrus)
            yield return walrus;

        foreach (var child in node.GetChildNodes())
        {
            foreach (var nested in CollectWalrusExpressions(child))
                yield return nested;
        }
    }

    /// <summary>Adds facts implied by an <c>assert</c> statement (its condition must hold afterward).</summary>
    private static void Gen(HashSet<NarrowingFact> facts, Statement statement, Func<Expression, bool> denotesBuiltinsModule)
    {
        if (statement is AssertStatement assert)
        {
            facts.UnionWith(NarrowingConditionInterpreter.Recognize(assert.Test, true, denotesBuiltinsModule));
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
    private static HashSet<NarrowingFact> CollectUniverse(ControlFlowGraph cfg, Func<Expression, bool> denotesBuiltinsModule)
    {
        var universe = new HashSet<NarrowingFact>();

        foreach (var block in cfg.Blocks)
        {
            if (block.Terminator is ConditionalBranchTerminator branch)
            {
                universe.UnionWith(NarrowingConditionInterpreter.Recognize(branch.Condition, true, denotesBuiltinsModule));
                universe.UnionWith(NarrowingConditionInterpreter.Recognize(branch.Condition, false, denotesBuiltinsModule));
            }

            foreach (var statement in block.Statements)
            {
                if (statement is AssertStatement assert)
                    universe.UnionWith(NarrowingConditionInterpreter.Recognize(assert.Test, true, denotesBuiltinsModule));
            }
        }

        return universe;
    }

    private static IReadOnlyCollection<NarrowingFact> Freeze(HashSet<NarrowingFact> facts) =>
        facts.Count == 0 ? Array.Empty<NarrowingFact>() : new List<NarrowingFact>(facts);
}
