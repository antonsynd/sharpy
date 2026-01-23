using System.Collections.Immutable;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Analysis.ControlFlow;

/// <summary>
/// Represents the control flow graph for a function or method body.
/// </summary>
/// <remarks>
/// Once constructed, a ControlFlowGraph is effectively immutable.
/// The blocks list and block connections don't change after construction.
/// </remarks>
public sealed class ControlFlowGraph
{
    private int _nextBlockId;

    /// <summary>
    /// The synthetic entry block - has no statements, just connects to the first real block.
    /// </summary>
    public BasicBlock Entry { get; }

    /// <summary>
    /// The synthetic exit block - all return paths lead here.
    /// </summary>
    public BasicBlock Exit { get; }

    /// <summary>
    /// All blocks in the CFG, including entry and exit.
    /// Blocks are in no particular order.
    /// </summary>
    public IReadOnlyList<BasicBlock> Blocks => _blocks;
    private readonly List<BasicBlock> _blocks;

    /// <summary>
    /// The function or method this CFG was built from (for diagnostics).
    /// Null for module-level CFGs.
    /// </summary>
    public FunctionDef? SourceFunction { get; }

    /// <summary>
    /// The source file path (for diagnostics).
    /// </summary>
    public string? SourceFile { get; init; }

    internal ControlFlowGraph(BasicBlock entry, BasicBlock exit, List<BasicBlock> blocks, FunctionDef? sourceFunction = null)
    {
        Entry = entry;
        Exit = exit;
        _blocks = blocks;
        SourceFunction = sourceFunction;

        // Assign sequential IDs to all blocks
        foreach (var block in blocks)
        {
            block.Id = _nextBlockId++;
        }
    }

    /// <summary>
    /// Get all blocks in reverse post-order (useful for data flow analysis).
    /// Entry block comes first, exit block typically last.
    /// </summary>
    public IReadOnlyList<BasicBlock> GetReversePostOrder()
    {
        var visited = new HashSet<BasicBlock>();
        var postOrder = new List<BasicBlock>();

        void Visit(BasicBlock block)
        {
            if (!visited.Add(block))
                return;
            foreach (var succ in block.Successors)
                Visit(succ);
            postOrder.Add(block);
        }

        Visit(Entry);
        postOrder.Reverse();
        return postOrder;
    }

    /// <summary>
    /// Find all blocks that are unreachable from the entry block.
    /// </summary>
    public ImmutableHashSet<BasicBlock> FindUnreachableBlocks()
    {
        var reachable = new HashSet<BasicBlock>();
        var worklist = new Queue<BasicBlock>();
        worklist.Enqueue(Entry);

        while (worklist.Count > 0)
        {
            var block = worklist.Dequeue();
            if (!reachable.Add(block))
                continue;
            foreach (var succ in block.Successors)
                worklist.Enqueue(succ);
        }

        return _blocks.Where(b => !reachable.Contains(b)).ToImmutableHashSet();
    }

    /// <summary>
    /// Check if all paths through the CFG reach the exit block.
    /// Returns the blocks that don't reach exit (missing return).
    /// </summary>
    /// <remarks>
    /// Uses backward reachability from Exit. Blocks that can't reach Exit
    /// either throw exceptions, loop infinitely, or have missing returns.
    /// </remarks>
    public ImmutableHashSet<BasicBlock> FindBlocksNotReachingExit()
    {
        var reachesExit = new HashSet<BasicBlock> { Exit };
        var changed = true;

        while (changed)
        {
            changed = false;
            foreach (var block in _blocks)
            {
                if (reachesExit.Contains(block))
                    continue;
                if (block.Successors.Any(reachesExit.Contains))
                {
                    reachesExit.Add(block);
                    changed = true;
                }
            }
        }

        return _blocks.Where(b => !reachesExit.Contains(b)).ToImmutableHashSet();
    }

    /// <summary>
    /// Find all blocks that contain exception-throwing terminators.
    /// Useful for exception flow analysis.
    /// </summary>
    public ImmutableHashSet<BasicBlock> FindThrowingBlocks()
    {
        return _blocks
            .Where(b => b.Terminator is ThrowTerminator or RethrowTerminator)
            .ToImmutableHashSet();
    }
}
