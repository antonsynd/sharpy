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

    [Fact]
    public void ValidateLoopControlFlow_BreakOutsideLoop_ReturnsError()
    {
        var func = new FunctionDef
        {
            Name = "break_outside",
            Body = ImmutableArray.Create<Statement>(new BreakStatement())
        };

        var cfg = _builder.Build(func);
        var errors = ControlFlowAnalysis.ValidateLoopControlFlow(cfg);

        // The builder creates BreakTerminator with null target for break outside loop,
        // which ValidateLoopControlFlow detects and reports as an error.
        Assert.Single(errors);
        Assert.Contains("'break' statement outside loop", errors[0].Message);
    }

    [Fact]
    public void ValidateLoopControlFlow_ContinueOutsideLoop_ReturnsError()
    {
        var func = new FunctionDef
        {
            Name = "continue_outside",
            Body = ImmutableArray.Create<Statement>(new ContinueStatement())
        };

        var cfg = _builder.Build(func);
        var errors = ControlFlowAnalysis.ValidateLoopControlFlow(cfg);

        // The builder creates ContinueTerminator with null target for continue outside loop,
        // which ValidateLoopControlFlow detects and reports as an error.
        Assert.Single(errors);
        Assert.Contains("'continue' statement outside loop", errors[0].Message);
    }

    [Fact]
    public void ValidateLoopControlFlow_NestedLoopBreak_NoErrors()
    {
        var func = new FunctionDef
        {
            Name = "nested_break",
            Body = ImmutableArray.Create<Statement>(
                new WhileStatement
                {
                    Test = Bool(true),
                    Body = ImmutableArray.Create<Statement>(
                        new ForStatement
                        {
                            Target = Id("i"),
                            Iterator = Id("items"),
                            Body = ImmutableArray.Create<Statement>(new BreakStatement())
                        }
                    )
                }
            )
        };

        var cfg = _builder.Build(func);
        var errors = ControlFlowAnalysis.ValidateLoopControlFlow(cfg);

        Assert.Empty(errors);
    }

    [Fact]
    public void IdentifyAsyncRegions_SingleAwait_TwoRegions()
    {
        var func = new FunctionDef
        {
            Name = "single_await",
            IsAsync = true,
            Body = ImmutableArray.Create<Statement>(
                Pass(),
                new ExpressionStatement
                {
                    Expression = new AwaitExpression { Operand = Id("task") }
                },
                Pass()
            )
        };

        var cfg = _builder.Build(func);
        var regions = ControlFlowAnalysis.IdentifyAsyncRegions(cfg);

        // Should have 2 regions: pre-await (including await block) and post-await
        Assert.Equal(2, regions.Length);
        Assert.NotNull(regions[0].AwaitExpression);
        Assert.Null(regions[1].AwaitExpression);
    }

    [Fact]
    public void IdentifyAsyncRegions_MultipleAwaits_MultipleRegions()
    {
        var func = new FunctionDef
        {
            Name = "multi_await",
            IsAsync = true,
            Body = ImmutableArray.Create<Statement>(
                new ExpressionStatement
                {
                    Expression = new AwaitExpression { Operand = Id("task1") }
                },
                new ExpressionStatement
                {
                    Expression = new AwaitExpression { Operand = Id("task2") }
                },
                Pass()
            )
        };

        var cfg = _builder.Build(func);
        var regions = ControlFlowAnalysis.IdentifyAsyncRegions(cfg);

        // The body block contains all 3 statements (no branching), and since
        // ContainsAwait is set once per block (not per statement), the block
        // forms one await region plus a continuation region (entry/exit blocks).
        Assert.True(regions.Length >= 2);
        Assert.True(regions.Count(r => r.AwaitExpression != null) >= 1);
    }

    [Fact]
    public void IdentifyAsyncRegions_AwaitInBranch_RegionsCreated()
    {
        var func = new FunctionDef
        {
            Name = "await_branch",
            IsAsync = true,
            Body = ImmutableArray.Create<Statement>(
                new IfStatement
                {
                    Test = Bool(true),
                    ThenBody = ImmutableArray.Create<Statement>(
                        new ExpressionStatement
                        {
                            Expression = new AwaitExpression { Operand = Id("task") }
                        }
                    ),
                    ElseBody = ImmutableArray.Create<Statement>(Pass())
                }
            )
        };

        var cfg = _builder.Build(func);
        var regions = ControlFlowAnalysis.IdentifyAsyncRegions(cfg);

        // Should have at least 2 regions: one containing the await block, one for continuation
        Assert.True(regions.Length >= 2);
        Assert.Contains(regions, r => r.AwaitExpression != null);
    }

    [Fact]
    public void IdentifyAsyncRegions_ExtractsAwaitExpression()
    {
        var awaitExpr = new AwaitExpression { Operand = Id("my_task") };
        var func = new FunctionDef
        {
            Name = "extract_await",
            IsAsync = true,
            Body = ImmutableArray.Create<Statement>(
                new ExpressionStatement { Expression = awaitExpr }
            )
        };

        var cfg = _builder.Build(func);
        var regions = ControlFlowAnalysis.IdentifyAsyncRegions(cfg);

        var awaitRegion = regions.FirstOrDefault(r => r.AwaitExpression != null);
        Assert.NotNull(awaitRegion);
        Assert.IsType<AwaitExpression>(awaitRegion.AwaitExpression);
    }

    [Fact]
    public void ValidateLoopControlFlow_NestedLoopContinue_NoErrors()
    {
        var func = new FunctionDef
        {
            Name = "nested_continue",
            Body = ImmutableArray.Create<Statement>(
                new ForStatement
                {
                    Target = Id("i"),
                    Iterator = Id("items"),
                    Body = ImmutableArray.Create<Statement>(
                        new WhileStatement
                        {
                            Test = Bool(true),
                            Body = ImmutableArray.Create<Statement>(new ContinueStatement())
                        }
                    )
                }
            )
        };

        var cfg = _builder.Build(func);
        var errors = ControlFlowAnalysis.ValidateLoopControlFlow(cfg);

        Assert.Empty(errors);
    }
}
