using Xunit;
using Sharpy.Compiler.Analysis.ControlFlow;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Text;
using static Sharpy.Compiler.Tests.Analysis.ControlFlow.ControlFlowTestHelpers;

namespace Sharpy.Compiler.Tests.Analysis.ControlFlow;

public class BasicBlockTests
{
    [Fact]
    public void BasicBlock_DefaultsToEmptyStatements()
    {
        var block = new BasicBlock();

        Assert.Empty(block.Statements);
    }

    [Fact]
    public void BasicBlock_CanAddStatements()
    {
        var block = new BasicBlock();
        block.AddStatement(Pass());
        block.AddStatement(Pass());

        Assert.Equal(2, block.Statements.Count);
    }

    [Fact]
    public void BasicBlock_TracksSuccessors()
    {
        var block1 = new BasicBlock("block1");
        var block2 = new BasicBlock("block2");

        ConnectBlocks(block1, block2);

        Assert.Contains(block2, block1.Successors);
    }

    [Fact]
    public void BasicBlock_TracksPredecessors()
    {
        var block1 = new BasicBlock("block1");
        var block2 = new BasicBlock("block2");

        ConnectBlocks(block1, block2);

        Assert.Contains(block1, block2.Predecessors);
    }

    [Fact]
    public void BasicBlock_DoesNotDuplicateConnections()
    {
        var block1 = new BasicBlock();
        var block2 = new BasicBlock();

        ConnectBlocks(block1, block2);
        ConnectBlocks(block1, block2);

        Assert.Single(block1.Successors);
        Assert.Single(block2.Predecessors);
    }

    [Fact]
    public void BasicBlock_ToStringIncludesLabel()
    {
        var block = new BasicBlock("test_block");

        Assert.Contains("test_block", block.ToString());
    }

    [Fact]
    public void BasicBlock_SpanFromFirstStatement()
    {
        var block = new BasicBlock();
        var stmt = new PassStatement
        {
            Span = new TextSpan(10, 5)
        };
        block.AddStatement(stmt);

        Assert.NotNull(block.Span);
        Assert.Equal(10, block.Span!.Value.Start);
    }

    [Fact]
    public void BasicBlock_EmptyBlockHasNoSpan()
    {
        var block = new BasicBlock();

        Assert.Null(block.Span);
    }
}
