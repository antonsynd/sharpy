using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Analysis.ControlFlow;

/// <summary>
/// A basic block is a sequence of statements with:
/// - Single entry point (only first statement can be branched to)
/// - Single exit point (only last statement can branch out)
/// - No internal control flow (no branches within the block)
/// </summary>
/// <remarks>
/// BasicBlock is a mutable class during CFG construction, then becomes
/// effectively immutable once the CFG is built. It is NOT a record because
/// we need reference identity (two blocks with same content are different blocks)
/// and mutable predecessor/successor lists during construction.
/// </remarks>
internal sealed class BasicBlock
{
    /// <summary>
    /// Unique identifier for this block within a CFG.
    /// Assigned by the ControlFlowGraph that owns this block.
    /// </summary>
    public int Id { get; internal set; }

    /// <summary>
    /// Human-readable label for debugging (e.g., "entry", "exit", "if_then", "loop_body").
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// The statements in this block, in execution order.
    /// Empty for synthetic blocks (entry/exit).
    /// </summary>
    public IReadOnlyList<Statement> Statements => _statements;
    private readonly List<Statement> _statements;

    /// <summary>
    /// Predecessor blocks - blocks that can transfer control TO this block.
    /// </summary>
    public IReadOnlyList<BasicBlock> Predecessors => _predecessors;
    private readonly List<BasicBlock> _predecessors = new();

    /// <summary>
    /// Successor blocks - blocks that control can transfer TO from this block.
    /// </summary>
    public IReadOnlyList<BasicBlock> Successors => _successors;
    private readonly List<BasicBlock> _successors = new();

    /// <summary>
    /// Exception predecessor blocks - blocks whose exceptions can transfer control TO this block.
    /// Distinguished from normal predecessors so dataflow analyses can use conservative
    /// assumptions (e.g., mustAssignIn rather than mustAssignOut) for exception edges.
    /// </summary>
    public IReadOnlyList<BasicBlock> ExceptionPredecessors => _exceptionPredecessors;
    private readonly List<BasicBlock> _exceptionPredecessors = new();

    /// <summary>
    /// The terminator instruction that ends this block.
    /// Null only for the exit block.
    /// </summary>
    public BlockTerminator? Terminator { get; internal set; }

    /// <summary>
    /// The subject expression of a <c>match</c> statement whose dispatch ends this block, or null
    /// (#1299).
    /// </summary>
    /// <remarks>
    /// A match dispatches to N case blocks, so it cannot wear a two-target
    /// <see cref="ConditionalBranchTerminator"/> without changing the graph's shape for every other
    /// analysis. It still needs what that terminator provides for narrowing: the expression whose
    /// facts are the block's out-set. Recording the subject here lets the flow analysis freeze the
    /// same fact set it freezes for an <c>if</c> condition, and leaves reachability and
    /// missing-return analysis looking at exactly the graph they looked at before.
    /// </remarks>
    public Parser.Ast.Expression? MatchSubject { get; internal set; }

    /// <summary>
    /// The subjects of match EXPRESSIONS evaluated within this block's statements (#1502). Unlike a
    /// match STATEMENT — which ends its own block and wears the single <see cref="MatchSubject"/> —
    /// a match expression sits inside a statement, and a block may hold several, so they are
    /// collected here. The flow analysis freezes the block's out-set for each, exactly as it does
    /// for <see cref="MatchSubject"/>, so <c>CheckMatchExpression</c> can read the narrowing at the
    /// subject's evaluation point instead of clearing the facts it inherits.
    /// </summary>
    public System.Collections.Generic.List<Parser.Ast.Expression> MatchExpressionSubjects { get; } = new();

    /// <summary>
    /// Expressions evaluated in this block that are not part of any statement — with-context
    /// expressions, match scrutinees, and match guards. Definite-assignment analysis checks
    /// these for reads after processing the block's statements.
    /// </summary>
    public List<Parser.Ast.Expression> Expressions { get; } = new();

    /// <summary>
    /// For async analysis: true if any statement in this block contains an await expression.
    /// Set during CFG construction by scanning for AwaitExpression nodes.
    /// </summary>
    public bool ContainsAwait { get; internal set; }

    /// <summary>
    /// Narrowing keys rebound at this block's entry, before its statements execute — the targets of a
    /// <c>for</c> loop (<c>for x in …</c>) or a <c>with … as x</c> binding. The narrowing dataflow
    /// analysis kills facts about these keys on entry, since the variable now holds a fresh value
    /// (#1042). Empty for ordinary blocks; consumed only by <c>NarrowingFlowAnalysis</c> (other CFG
    /// consumers ignore it).
    /// </summary>
    public IReadOnlyList<string> EntryRebinds { get; internal set; } = System.Array.Empty<string>();

    /// <summary>
    /// The source span of the first statement in this block (for diagnostics).
    /// </summary>
    public Text.TextSpan? Span => _statements.Count > 0 ? _statements[0].Span : null;

    public BasicBlock(string label = "")
    {
        Label = label;
        _statements = new List<Statement>();
    }

    /// <summary>
    /// Add a statement to this block. Only valid during CFG construction.
    /// </summary>
    internal void AddStatement(Statement stmt)
    {
        _statements.Add(stmt);
    }

    internal void AddPredecessor(BasicBlock block)
    {
        if (!_predecessors.Contains(block))
            _predecessors.Add(block);
    }

    internal void AddSuccessor(BasicBlock block)
    {
        if (!_successors.Contains(block))
            _successors.Add(block);
    }

    internal void AddExceptionPredecessor(BasicBlock block)
    {
        if (!_exceptionPredecessors.Contains(block))
            _exceptionPredecessors.Add(block);
    }

    public override string ToString() =>
        string.IsNullOrEmpty(Label) ? $"BB{Id}" : $"BB{Id}:{Label}";
}
