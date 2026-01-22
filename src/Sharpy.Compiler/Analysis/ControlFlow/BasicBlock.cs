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
public sealed class BasicBlock
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
    /// The terminator instruction that ends this block.
    /// Null only for the exit block.
    /// </summary>
    public BlockTerminator? Terminator { get; internal set; }

    /// <summary>
    /// For async analysis: true if any statement in this block contains an await expression.
    /// Set during CFG construction by scanning for AwaitExpression nodes.
    /// </summary>
    public bool ContainsAwait { get; internal set; }

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

    public override string ToString() =>
        string.IsNullOrEmpty(Label) ? $"BB{Id}" : $"BB{Id}:{Label}";
}
