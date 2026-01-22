using System.Collections.Immutable;
using Xunit;
using Sharpy.Compiler.Analysis.ControlFlow;
using Sharpy.Compiler.Parser.Ast;
using static Sharpy.Compiler.Tests.Analysis.ControlFlow.ControlFlowTestHelpers;

namespace Sharpy.Compiler.Tests.Analysis.ControlFlow;

public class ControlFlowAnalysisTests
{
    private readonly ControlFlowGraphBuilder _builder = new();

    [Fact]
    public void FindMissingReturnPaths_AllPathsReturn_Empty()
    {
        var func = new FunctionDef
        {
            Name = "all_return",
            Body = ImmutableArray.Create<Statement>(
                new IfStatement
                {
                    Test = Bool(true),
                    ThenBody = ImmutableArray.Create<Statement>(
                        new ReturnStatement { Value = Id("x") }
                    ),
                    ElseBody = ImmutableArray.Create<Statement>(
                        new ReturnStatement { Value = Id("y") }
                    )
                }
            )
        };

        var cfg = _builder.Build(func);
        var missing = ControlFlowAnalysis.FindMissingReturnPaths(cfg);

        Assert.Empty(missing);
    }

    [Fact]
    public void FindMissingReturnPaths_MissingElseReturn_NotEmpty()
    {
        var func = new FunctionDef
        {
            Name = "missing_return",
            Body = ImmutableArray.Create<Statement>(
                new IfStatement
                {
                    Test = Bool(true),
                    ThenBody = ImmutableArray.Create<Statement>(
                        new ReturnStatement { Value = Id("x") }
                    )
                    // No else - missing return path
                }
            )
        };

        var cfg = _builder.Build(func);
        var missing = ControlFlowAnalysis.FindMissingReturnPaths(cfg);

        // There should be a path that doesn't return
        // (The merge block after the if)
        Assert.NotEmpty(missing);
    }

    [Fact]
    public void FindUnreachableCode_NoUnreachable_Empty()
    {
        var func = new FunctionDef
        {
            Name = "reachable",
            Body = ImmutableArray.Create<Statement>(
                Pass(),
                new ReturnStatement { Value = Id("x") }
            )
        };

        var cfg = _builder.Build(func);
        var unreachable = ControlFlowAnalysis.FindUnreachableCode(cfg);

        Assert.Empty(unreachable);
    }

    [Fact]
    public void ValidateLoopControlFlow_ValidBreak_NoErrors()
    {
        var func = new FunctionDef
        {
            Name = "valid_break",
            Body = ImmutableArray.Create<Statement>(
                new WhileStatement
                {
                    Test = Bool(true),
                    Body = ImmutableArray.Create<Statement>(new BreakStatement())
                }
            )
        };

        var cfg = _builder.Build(func);
        var errors = ControlFlowAnalysis.ValidateLoopControlFlow(cfg);

        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateLoopControlFlow_ValidContinue_NoErrors()
    {
        var func = new FunctionDef
        {
            Name = "valid_continue",
            Body = ImmutableArray.Create<Statement>(
                new WhileStatement
                {
                    Test = Bool(true),
                    Body = ImmutableArray.Create<Statement>(new ContinueStatement())
                }
            )
        };

        var cfg = _builder.Build(func);
        var errors = ControlFlowAnalysis.ValidateLoopControlFlow(cfg);

        Assert.Empty(errors);
    }

    [Fact]
    public void IdentifyAsyncRegions_NoAwait_SingleRegion()
    {
        var func = new FunctionDef
        {
            Name = "no_await",
            Body = ImmutableArray.Create<Statement>(Pass())
        };

        var cfg = _builder.Build(func);
        var regions = ControlFlowAnalysis.IdentifyAsyncRegions(cfg);

        Assert.Single(regions);
        Assert.Equal(0, regions[0].StateId);
    }

    [Fact]
    public void FindMissingReturnPaths_OnlyReturn_Empty()
    {
        var func = new FunctionDef
        {
            Name = "only_return",
            Body = ImmutableArray.Create<Statement>(
                new ReturnStatement { Value = Id("x") }
            )
        };

        var cfg = _builder.Build(func);
        var missing = ControlFlowAnalysis.FindMissingReturnPaths(cfg);

        Assert.Empty(missing);
    }

    [Fact]
    public void FindMissingReturnPaths_VoidFunction_NotEmpty()
    {
        var func = new FunctionDef
        {
            Name = "void_func",
            Body = ImmutableArray.Create<Statement>(
                Pass()
                // No return - this is OK for void functions but our analysis flags it
            )
        };

        var cfg = _builder.Build(func);
        var missing = ControlFlowAnalysis.FindMissingReturnPaths(cfg);

        // This returns blocks that fall through to exit without returning
        // Whether this is an error depends on the return type (handled by caller)
        Assert.NotEmpty(missing);
    }

    [Fact]
    public void FindMissingReturnPaths_NestedIf_AllPaths()
    {
        var func = new FunctionDef
        {
            Name = "nested_if",
            Body = ImmutableArray.Create<Statement>(
                new IfStatement
                {
                    Test = Bool(true),
                    ThenBody = ImmutableArray.Create<Statement>(
                        new IfStatement
                        {
                            Test = Bool(false),
                            ThenBody = ImmutableArray.Create<Statement>(
                                new ReturnStatement { Value = Id("a") }
                            ),
                            ElseBody = ImmutableArray.Create<Statement>(
                                new ReturnStatement { Value = Id("b") }
                            )
                        }
                    ),
                    ElseBody = ImmutableArray.Create<Statement>(
                        new ReturnStatement { Value = Id("c") }
                    )
                }
            )
        };

        var cfg = _builder.Build(func);
        var missing = ControlFlowAnalysis.FindMissingReturnPaths(cfg);

        Assert.Empty(missing);
    }

    [Fact]
    public void FindMissingReturnPaths_LoopWithReturn_NotEmpty()
    {
        // A return inside a loop doesn't guarantee all paths return
        var func = new FunctionDef
        {
            Name = "loop_return",
            Body = ImmutableArray.Create<Statement>(
                new WhileStatement
                {
                    Test = Bool(true),
                    Body = ImmutableArray.Create<Statement>(
                        new ReturnStatement { Value = Id("x") }
                    )
                }
                // If the loop never runs, this path doesn't return
            )
        };

        var cfg = _builder.Build(func);
        var missing = ControlFlowAnalysis.FindMissingReturnPaths(cfg);

        // The while exit block falls through to exit without returning
        Assert.NotEmpty(missing);
    }
}
