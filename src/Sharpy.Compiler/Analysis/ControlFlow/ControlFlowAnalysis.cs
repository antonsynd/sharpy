using System.Collections.Immutable;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Analysis.ControlFlow;

/// <summary>
/// Provides control flow analysis utilities using the CFG.
/// </summary>
internal static class ControlFlowAnalysis
{
    /// <summary>
    /// Check if all paths through the function return a value.
    /// Returns blocks that don't reach the exit via a return.
    /// </summary>
    public static ImmutableArray<BasicBlock> FindMissingReturnPaths(ControlFlowGraph cfg)
    {
        // Find all blocks that:
        // 1. Branch directly to exit without returning
        // 2. Are not the entry or exit block
        // 3. Are reachable from entry

        var missingReturn = new List<BasicBlock>();

        // First, find all reachable blocks
        var reachable = new HashSet<BasicBlock>();
        var worklist = new Queue<BasicBlock>();
        worklist.Enqueue(cfg.Entry);
        while (worklist.Count > 0)
        {
            var block = worklist.Dequeue();
            if (!reachable.Add(block))
                continue;
            foreach (var succ in block.Successors)
                worklist.Enqueue(succ);
        }

        foreach (var block in cfg.Blocks)
        {
            if (block == cfg.Entry || block == cfg.Exit)
                continue;

            // Only consider reachable blocks
            if (!reachable.Contains(block))
                continue;

            // If this block reaches exit but doesn't return
            if (block.Terminator is BranchTerminator branch &&
                branch.Target == cfg.Exit &&
                !block.Statements.Any(s => s is ReturnStatement))
            {
                missingReturn.Add(block);
            }
        }

        return missingReturn.ToImmutableArray();
    }

    /// <summary>
    /// Find blocks containing unreachable code (after return/raise/break/continue).
    /// </summary>
    public static ImmutableArray<UnreachableCodeInfo> FindUnreachableCode(ControlFlowGraph cfg)
    {
        var result = new List<UnreachableCodeInfo>();
        var unreachableBlocks = cfg.FindUnreachableBlocks();

        foreach (var block in unreachableBlocks)
        {
            if (block.Statements.Count > 0)
            {
                result.Add(new UnreachableCodeInfo(
                    block,
                    block.Statements[0]
                ));
            }
        }

        return result.ToImmutableArray();
    }

    /// <summary>
    /// Check if break/continue are only used inside loops.
    /// </summary>
    public static ImmutableArray<ControlFlowError> ValidateLoopControlFlow(ControlFlowGraph cfg)
    {
        var errors = new List<ControlFlowError>();

        foreach (var block in cfg.Blocks)
        {
            if (block.Terminator is BreakTerminator breakTerm)
            {
                // If target is null or not a loop exit, it's an error
                // (The builder sets target to null if not in a loop)
                if (breakTerm.SourceStatement != null &&
                    !IsLoopExitBlock(breakTerm.Target, cfg))
                {
                    errors.Add(new ControlFlowError(
                        "'break' statement outside loop",
                        breakTerm.SourceStatement
                    ));
                }
            }

            if (block.Terminator is ContinueTerminator contTerm)
            {
                if (contTerm.SourceStatement != null &&
                    !IsLoopHeaderBlock(contTerm.Target, cfg))
                {
                    errors.Add(new ControlFlowError(
                        "'continue' statement outside loop",
                        contTerm.SourceStatement
                    ));
                }
            }
        }

        return errors.ToImmutableArray();
    }

    /// <summary>
    /// For async/await: identify regions between await points.
    /// Each region becomes a state in the state machine.
    /// </summary>
    public static ImmutableArray<AsyncStateRegion> IdentifyAsyncRegions(ControlFlowGraph cfg)
    {
        var rpo = cfg.GetReversePostOrder();

        // Check if any block contains an await
        bool hasAwaits = rpo.Any(b => b.ContainsAwait);

        if (!hasAwaits)
        {
            // No awaits - single region containing all blocks
            return ImmutableArray.Create(new AsyncStateRegion(
                0,
                cfg.Blocks.ToImmutableArray(),
                null
            ));
        }

        // Split into regions: each await-containing block ends a region and
        // starts a new one. Consecutive non-await blocks are grouped together.
        var regions = new List<AsyncStateRegion>();
        int stateId = 0;
        var currentRegionBlocks = new List<BasicBlock>();

        foreach (var block in rpo)
        {
            if (block.ContainsAwait)
            {
                // Include this block in the current region (pre-await code)
                currentRegionBlocks.Add(block);

                // Extract the actual AwaitExpression from this block's statements
                var awaitExpr = ExtractAwaitExpression(block);

                // End the current region
                regions.Add(new AsyncStateRegion(
                    stateId++,
                    currentRegionBlocks.ToImmutableArray(),
                    awaitExpr
                ));
                currentRegionBlocks = new List<BasicBlock>();
            }
            else
            {
                currentRegionBlocks.Add(block);
            }
        }

        // Final region: blocks after the last await (continuation to exit)
        if (currentRegionBlocks.Count > 0)
        {
            regions.Add(new AsyncStateRegion(
                stateId,
                currentRegionBlocks.ToImmutableArray(),
                null // No await in the final continuation region
            ));
        }

        return regions.ToImmutableArray();
    }

    /// <summary>
    /// Extracts the first AwaitExpression from a block's statements.
    /// </summary>
    private static Expression? ExtractAwaitExpression(BasicBlock block)
    {
        foreach (var stmt in block.Statements)
        {
            var awaitExpr = FindFirstAwait(stmt);
            if (awaitExpr != null)
                return awaitExpr;
        }
        return null;
    }

    /// <summary>
    /// Recursively walks an AST node to find the first AwaitExpression.
    /// </summary>
    private static AwaitExpression? FindFirstAwait(Node node)
    {
        if (node is AwaitExpression awaitExpr)
            return awaitExpr;

        foreach (var child in node.GetChildNodes())
        {
            var found = FindFirstAwait(child);
            if (found != null)
                return found;
        }

        return null;
    }

    private static bool IsLoopExitBlock(BasicBlock? block, ControlFlowGraph cfg)
    {
        // Check if the block is a loop exit block by looking for specific labels.
        // Loop exit blocks are labeled "while_exit" or "for_exit".
        // We use Contains("_exit") to also match numbered exit blocks like "while_exit_0".
        return block != null &&
               (block.Label.Contains("while_exit") ||
                block.Label.Contains("for_exit"));
    }

    private static bool IsLoopHeaderBlock(BasicBlock? block, ControlFlowGraph cfg)
    {
        // Check if the block is a loop header block by looking for specific labels.
        // Loop header blocks are labeled "while_header" or "for_header".
        return block != null &&
               (block.Label.Contains("while_header") ||
                block.Label.Contains("for_header"));
    }
}

/// <summary>
/// Information about unreachable code.
/// </summary>
internal record UnreachableCodeInfo(
    BasicBlock Block,
    Statement FirstUnreachableStatement
);

/// <summary>
/// A control flow validation error.
/// </summary>
internal record ControlFlowError(
    string Message,
    Statement Statement
);

/// <summary>
/// A region of code between await points for async state machine generation.
/// </summary>
internal record AsyncStateRegion(
    int StateId,
    ImmutableArray<BasicBlock> Blocks,
    Expression? AwaitExpression
);
