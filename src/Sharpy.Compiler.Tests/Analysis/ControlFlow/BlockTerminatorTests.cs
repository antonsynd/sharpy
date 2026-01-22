using Xunit;
using Sharpy.Compiler.Analysis.ControlFlow;
using Sharpy.Compiler.Parser.Ast;
using static Sharpy.Compiler.Tests.Analysis.ControlFlow.ControlFlowTestHelpers;

namespace Sharpy.Compiler.Tests.Analysis.ControlFlow;

public class BlockTerminatorTests
{
    [Fact]
    public void BranchTerminator_HasSingleTarget()
    {
        var target = new BasicBlock("target");
        var terminator = new BranchTerminator(target);

        Assert.Equal(target, terminator.Target);
    }

    [Fact]
    public void ConditionalBranchTerminator_HasBothTargets()
    {
        var trueTarget = new BasicBlock("true");
        var falseTarget = new BasicBlock("false");
        var terminator = new ConditionalBranchTerminator(
            Bool(true), trueTarget, falseTarget);

        Assert.Equal(trueTarget, terminator.TrueTarget);
        Assert.Equal(falseTarget, terminator.FalseTarget);
    }

    [Fact]
    public void ReturnTerminator_CanHaveValue()
    {
        var terminator = new ReturnTerminator(Id("x"));

        Assert.NotNull(terminator.Value);
    }

    [Fact]
    public void ReturnTerminator_CanBeVoid()
    {
        var terminator = new ReturnTerminator(null);

        Assert.Null(terminator.Value);
    }

    [Fact]
    public void ThrowTerminator_HasException()
    {
        var terminator = new ThrowTerminator(Id("error"));

        Assert.NotNull(terminator.Exception);
    }

    [Fact]
    public void BreakTerminator_HasTarget()
    {
        var target = new BasicBlock("loop_exit");
        var terminator = new BreakTerminator(target);

        Assert.Equal(target, terminator.Target);
    }

    [Fact]
    public void ContinueTerminator_HasTarget()
    {
        var target = new BasicBlock("loop_header");
        var terminator = new ContinueTerminator(target);

        Assert.Equal(target, terminator.Target);
    }

    [Fact]
    public void RethrowTerminator_HasNoException()
    {
        var terminator = new RethrowTerminator();

        // RethrowTerminator represents bare 'raise' with no exception expression
        Assert.NotNull(terminator);
    }

    [Fact]
    public void UnreachableTerminator_Exists()
    {
        var terminator = new UnreachableTerminator();

        Assert.NotNull(terminator);
    }

    [Fact]
    public void BlockTerminator_CanHaveSourceStatement()
    {
        var stmt = new ReturnStatement { Value = Id("x") };
        var terminator = new ReturnTerminator(Id("x")) { SourceStatement = stmt };

        Assert.Equal(stmt, terminator.SourceStatement);
    }
}
