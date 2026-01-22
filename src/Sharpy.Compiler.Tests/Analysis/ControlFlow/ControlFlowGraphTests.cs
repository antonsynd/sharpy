using Xunit;
using Sharpy.Compiler.Analysis.ControlFlow;
using static Sharpy.Compiler.Tests.Analysis.ControlFlow.ControlFlowTestHelpers;

namespace Sharpy.Compiler.Tests.Analysis.ControlFlow;

public class ControlFlowGraphTests
{
    [Fact]
    public void CreateLinearCfg_HasEntryAndExit()
    {
        var cfg = CreateLinearCfg(Pass());

        Assert.NotNull(cfg.Entry);
        Assert.NotNull(cfg.Exit);
        Assert.Equal("entry", cfg.Entry.Label);
        Assert.Equal("exit", cfg.Exit.Label);
    }

    [Fact]
    public void CreateLinearCfg_EntryConnectsToBody()
    {
        var cfg = CreateLinearCfg(Pass());

        Assert.Single(cfg.Entry.Successors);
    }

    [Fact]
    public void CreateLinearCfg_AssignsBlockIds()
    {
        var cfg = CreateLinearCfg(Pass());

        // All blocks should have unique IDs assigned
        var ids = cfg.Blocks.Select(b => b.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void CreateDiamondCfg_HasCorrectStructure()
    {
        var cfg = CreateDiamondCfg(
            Bool(true),
            new[] { Pass() },
            new[] { Pass() }
        );

        Assert.Equal(6, cfg.Blocks.Count); // entry, cond, then, else, merge, exit
    }

    [Fact]
    public void CreateLoopCfg_HasBackEdge()
    {
        var cfg = CreateLoopCfg(Bool(true), new[] { Pass() });

        var body = cfg.Blocks.First(b => b.Label == "loop_body");
        var header = cfg.Blocks.First(b => b.Label == "loop_header");

        Assert.Contains(header, body.Successors); // back edge
    }

    [Fact]
    public void FindUnreachableBlocks_EmptyForConnectedGraph()
    {
        var cfg = CreateLinearCfg(Pass());

        var unreachable = cfg.FindUnreachableBlocks();

        Assert.Empty(unreachable);
    }

    [Fact]
    public void FindUnreachableBlocks_FindsDisconnectedBlocks()
    {
        var cfg = CreateLinearCfg(Pass());
        var orphan = new BasicBlock("orphan");

        // Add orphan to blocks list without connecting it
        var blocks = cfg.Blocks.ToList();
        blocks.Add(orphan);
        var newCfg = new ControlFlowGraph(cfg.Entry, cfg.Exit, blocks);

        var unreachable = newCfg.FindUnreachableBlocks();

        Assert.Contains(orphan, unreachable);
    }

    [Fact]
    public void GetReversePostOrder_EntryComesFirst()
    {
        var cfg = CreateLinearCfg(Pass());

        var rpo = cfg.GetReversePostOrder();

        Assert.Equal(cfg.Entry, rpo.First());
    }

    [Fact]
    public void GetReversePostOrder_ExitComesLast()
    {
        var cfg = CreateLinearCfg(Pass());

        var rpo = cfg.GetReversePostOrder();

        Assert.Equal(cfg.Exit, rpo.Last());
    }

    [Fact]
    public void FindBlocksNotReachingExit_AllReachInLinearCfg()
    {
        var cfg = CreateLinearCfg(Pass());

        var notReaching = cfg.FindBlocksNotReachingExit();

        Assert.Empty(notReaching);
    }

    [Fact]
    public void FindThrowingBlocks_FindsThrowTerminators()
    {
        var entry = new BasicBlock("entry");
        var throwing = new BasicBlock("throwing");
        var exit = new BasicBlock("exit");

        ConnectBlocks(entry, throwing);
        entry.Terminator = new BranchTerminator(throwing);
        throwing.Terminator = new ThrowTerminator(Id("error"));

        var cfg = new ControlFlowGraph(entry, exit, new List<BasicBlock> { entry, throwing, exit });

        var throwingBlocks = cfg.FindThrowingBlocks();

        Assert.Single(throwingBlocks);
        Assert.Contains(throwing, throwingBlocks);
    }
}
