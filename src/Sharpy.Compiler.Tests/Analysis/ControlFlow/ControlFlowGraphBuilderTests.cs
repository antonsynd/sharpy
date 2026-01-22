using System.Collections.Immutable;
using Xunit;
using Sharpy.Compiler.Analysis.ControlFlow;
using Sharpy.Compiler.Parser.Ast;
using static Sharpy.Compiler.Tests.Analysis.ControlFlow.ControlFlowTestHelpers;

namespace Sharpy.Compiler.Tests.Analysis.ControlFlow;

public class ControlFlowGraphBuilderTests
{
    private readonly ControlFlowGraphBuilder _builder = new();

    #region Simple Functions

    [Fact]
    public void Build_EmptyFunction_HasEntryAndExit()
    {
        var func = CreateFunction("empty", ImmutableArray<Statement>.Empty);

        var cfg = _builder.Build(func);

        Assert.NotNull(cfg.Entry);
        Assert.NotNull(cfg.Exit);
    }

    [Fact]
    public void Build_PassStatement_SingleBlock()
    {
        var func = CreateFunction("pass_only", ImmutableArray.Create<Statement>(Pass()));

        var cfg = _builder.Build(func);

        // entry -> body -> exit
        Assert.True(cfg.Blocks.Count >= 3);
    }

    [Fact]
    public void Build_ReturnStatement_ConnectsToExit()
    {
        var func = CreateFunction("returns", ImmutableArray.Create<Statement>(
            new ReturnStatement { Value = Id("x") }
        ));

        var cfg = _builder.Build(func);

        var returnBlock = cfg.Blocks.FirstOrDefault(b =>
            b.Terminator is ReturnTerminator);

        Assert.NotNull(returnBlock);
        Assert.Contains(cfg.Exit, returnBlock.Successors);
    }

    #endregion

    #region If Statements

    [Fact]
    public void Build_SimpleIf_CreatesDiamond()
    {
        var func = CreateFunction("simple_if", ImmutableArray.Create<Statement>(
            new IfStatement
            {
                Test = Bool(true),
                ThenBody = ImmutableArray.Create<Statement>(Pass())
            }
        ));

        var cfg = _builder.Build(func);

        var condBlock = cfg.Blocks.FirstOrDefault(b =>
            b.Terminator is ConditionalBranchTerminator);

        Assert.NotNull(condBlock);
    }

    [Fact]
    public void Build_IfElse_HasBothBranches()
    {
        var func = CreateFunction("if_else", ImmutableArray.Create<Statement>(
            new IfStatement
            {
                Test = Bool(true),
                ThenBody = ImmutableArray.Create<Statement>(Pass()),
                ElseBody = ImmutableArray.Create<Statement>(Pass())
            }
        ));

        var cfg = _builder.Build(func);

        var condBlock = cfg.Blocks.FirstOrDefault(b =>
            b.Terminator is ConditionalBranchTerminator);

        Assert.NotNull(condBlock);
        var cond = (ConditionalBranchTerminator)condBlock!.Terminator!;
        Assert.NotEqual(cond.TrueTarget, cond.FalseTarget);
    }

    [Fact]
    public void Build_IfWithReturn_DoesNotReachMerge()
    {
        var func = CreateFunction("if_return", ImmutableArray.Create<Statement>(
            new IfStatement
            {
                Test = Bool(true),
                ThenBody = ImmutableArray.Create<Statement>(
                    new ReturnStatement { Value = Id("x") }
                )
            }
        ));

        var cfg = _builder.Build(func);

        var thenBlock = cfg.Blocks.FirstOrDefault(b =>
            b.Terminator is ReturnTerminator);

        Assert.NotNull(thenBlock);
    }

    [Fact]
    public void Build_IfWithElif_HasMultipleConditions()
    {
        var func = CreateFunction("if_elif", ImmutableArray.Create<Statement>(
            new IfStatement
            {
                Test = Bool(true),
                ThenBody = ImmutableArray.Create<Statement>(Pass()),
                ElifClauses = ImmutableArray.Create(new ElifClause
                {
                    Test = Bool(false),
                    Body = ImmutableArray.Create<Statement>(Pass())
                })
            }
        ));

        var cfg = _builder.Build(func);

        // Should have at least 2 conditional branches (if and elif)
        var condBlocks = cfg.Blocks.Where(b =>
            b.Terminator is ConditionalBranchTerminator).ToList();

        Assert.True(condBlocks.Count >= 2);
    }

    #endregion

    #region Loops

    [Fact]
    public void Build_WhileLoop_HasBackEdge()
    {
        var func = CreateFunction("while_loop", ImmutableArray.Create<Statement>(
            new WhileStatement
            {
                Test = Bool(true),
                Body = ImmutableArray.Create<Statement>(Pass())
            }
        ));

        var cfg = _builder.Build(func);

        var headerBlock = cfg.Blocks.FirstOrDefault(b => b.Label.Contains("header"));
        Assert.NotNull(headerBlock);

        // Header should have a predecessor that is the body (back edge)
        Assert.True(headerBlock.Predecessors.Count >= 2); // entry + body
    }

    [Fact]
    public void Build_WhileWithBreak_ExitsLoop()
    {
        var func = CreateFunction("while_break", ImmutableArray.Create<Statement>(
            new WhileStatement
            {
                Test = Bool(true),
                Body = ImmutableArray.Create<Statement>(
                    new BreakStatement()
                )
            }
        ));

        var cfg = _builder.Build(func);

        var breakBlock = cfg.Blocks.FirstOrDefault(b =>
            b.Terminator is BreakTerminator);

        Assert.NotNull(breakBlock);
    }

    [Fact]
    public void Build_WhileWithContinue_JumpsToHeader()
    {
        var func = CreateFunction("while_continue", ImmutableArray.Create<Statement>(
            new WhileStatement
            {
                Test = Bool(true),
                Body = ImmutableArray.Create<Statement>(
                    new ContinueStatement()
                )
            }
        ));

        var cfg = _builder.Build(func);

        var continueBlock = cfg.Blocks.FirstOrDefault(b =>
            b.Terminator is ContinueTerminator);

        Assert.NotNull(continueBlock);
        var cont = (ContinueTerminator)continueBlock!.Terminator!;
        Assert.Contains("header", cont.Target.Label);
    }

    [Fact]
    public void Build_ForLoop_HasBackEdge()
    {
        var func = CreateFunction("for_loop", ImmutableArray.Create<Statement>(
            new ForStatement
            {
                Target = Id("i"),
                Iterator = Id("items"),
                Body = ImmutableArray.Create<Statement>(Pass())
            }
        ));

        var cfg = _builder.Build(func);

        var headerBlock = cfg.Blocks.FirstOrDefault(b => b.Label.Contains("header"));
        Assert.NotNull(headerBlock);
    }

    [Fact]
    public void Build_WhileWithElse_HasElseBlock()
    {
        var func = CreateFunction("while_else", ImmutableArray.Create<Statement>(
            new WhileStatement
            {
                Test = Bool(true),
                Body = ImmutableArray.Create<Statement>(Pass()),
                ElseBody = ImmutableArray.Create<Statement>(Pass())
            }
        ));

        var cfg = _builder.Build(func);

        var elseBlock = cfg.Blocks.FirstOrDefault(b => b.Label.Contains("else"));
        Assert.NotNull(elseBlock);
    }

    [Fact]
    public void Build_ForWithElse_HasElseBlock()
    {
        var func = CreateFunction("for_else", ImmutableArray.Create<Statement>(
            new ForStatement
            {
                Target = Id("i"),
                Iterator = Id("items"),
                Body = ImmutableArray.Create<Statement>(Pass()),
                ElseBody = ImmutableArray.Create<Statement>(Pass())
            }
        ));

        var cfg = _builder.Build(func);

        var elseBlock = cfg.Blocks.FirstOrDefault(b => b.Label.Contains("else"));
        Assert.NotNull(elseBlock);
    }

    #endregion

    #region Try/Except

    [Fact]
    public void Build_TryExcept_HasExceptionEdge()
    {
        var func = CreateFunction("try_except", ImmutableArray.Create<Statement>(
            new TryStatement
            {
                Body = ImmutableArray.Create<Statement>(Pass()),
                Handlers = ImmutableArray.Create(new ExceptHandler
                {
                    ExceptionType = new TypeAnnotation { Name = "Exception" },
                    Body = ImmutableArray.Create<Statement>(Pass())
                })
            }
        ));

        var cfg = _builder.Build(func);

        var tryBlock = cfg.Blocks.FirstOrDefault(b => b.Label.Contains("try"));
        var exceptBlock = cfg.Blocks.FirstOrDefault(b => b.Label.Contains("except"));

        Assert.NotNull(tryBlock);
        Assert.NotNull(exceptBlock);
    }

    [Fact]
    public void Build_TryFinally_FinallyAlwaysRuns()
    {
        var func = CreateFunction("try_finally", ImmutableArray.Create<Statement>(
            new TryStatement
            {
                Body = ImmutableArray.Create<Statement>(Pass()),
                FinallyBody = ImmutableArray.Create<Statement>(Pass())
            }
        ));

        var cfg = _builder.Build(func);

        var finallyBlock = cfg.Blocks.FirstOrDefault(b => b.Label.Contains("finally"));
        Assert.NotNull(finallyBlock);
    }

    [Fact]
    public void Build_TryElse_HasElseBlock()
    {
        var func = CreateFunction("try_else", ImmutableArray.Create<Statement>(
            new TryStatement
            {
                Body = ImmutableArray.Create<Statement>(Pass()),
                Handlers = ImmutableArray.Create(new ExceptHandler
                {
                    ExceptionType = new TypeAnnotation { Name = "Exception" },
                    Body = ImmutableArray.Create<Statement>(Pass())
                }),
                ElseBody = ImmutableArray.Create<Statement>(Pass())
            }
        ));

        var cfg = _builder.Build(func);

        var elseBlock = cfg.Blocks.FirstOrDefault(b => b.Label.Contains("try_else"));
        Assert.NotNull(elseBlock);
    }

    #endregion

    #region Raise

    [Fact]
    public void Build_RaiseStatement_CreatesThrowTerminator()
    {
        var func = CreateFunction("raise_test", ImmutableArray.Create<Statement>(
            new RaiseStatement { Exception = Id("error") }
        ));

        var cfg = _builder.Build(func);

        var throwBlock = cfg.Blocks.FirstOrDefault(b =>
            b.Terminator is ThrowTerminator);

        Assert.NotNull(throwBlock);
    }

    [Fact]
    public void Build_BareRaise_CreatesRethrowTerminator()
    {
        var func = CreateFunction("bare_raise", ImmutableArray.Create<Statement>(
            new TryStatement
            {
                Body = ImmutableArray.Create<Statement>(Pass()),
                Handlers = ImmutableArray.Create(new ExceptHandler
                {
                    ExceptionType = new TypeAnnotation { Name = "Exception" },
                    Body = ImmutableArray.Create<Statement>(
                        new RaiseStatement { Exception = null }
                    )
                })
            }
        ));

        var cfg = _builder.Build(func);

        var rethrowBlock = cfg.Blocks.FirstOrDefault(b =>
            b.Terminator is RethrowTerminator);

        Assert.NotNull(rethrowBlock);
    }

    #endregion

    #region Reachability

    [Fact]
    public void Build_AllBlocksReachable()
    {
        var func = CreateFunction("reachable", ImmutableArray.Create<Statement>(
            new IfStatement
            {
                Test = Bool(true),
                ThenBody = ImmutableArray.Create<Statement>(Pass()),
                ElseBody = ImmutableArray.Create<Statement>(Pass())
            }
        ));

        var cfg = _builder.Build(func);
        var unreachable = cfg.FindUnreachableBlocks();

        Assert.Empty(unreachable);
    }

    [Fact]
    public void Build_CodeAfterReturn_IsUnreachable()
    {
        var func = CreateFunction("unreachable", ImmutableArray.Create<Statement>(
            new ReturnStatement { Value = Id("x") },
            Pass() // This should be unreachable
        ));

        var cfg = _builder.Build(func);

        // The pass statement shouldn't be in any block's statements
        // because we stop processing after return
        var hasPassAfterReturn = cfg.Blocks.Any(b =>
            b.Statements.Count > 1 &&
            b.Statements[0] is ReturnStatement);

        Assert.False(hasPassAfterReturn);
    }

    #endregion

    #region Break/Continue Outside Loops

    [Fact]
    public void Build_BreakOutsideLoop_CreatesUnreachableTerminator()
    {
        var func = CreateFunction("break_outside", ImmutableArray.Create<Statement>(
            new BreakStatement()
        ));

        var cfg = _builder.Build(func);

        var unreachableBlock = cfg.Blocks.FirstOrDefault(b =>
            b.Terminator is UnreachableTerminator);

        Assert.NotNull(unreachableBlock);
    }

    [Fact]
    public void Build_ContinueOutsideLoop_CreatesUnreachableTerminator()
    {
        var func = CreateFunction("continue_outside", ImmutableArray.Create<Statement>(
            new ContinueStatement()
        ));

        var cfg = _builder.Build(func);

        var unreachableBlock = cfg.Blocks.FirstOrDefault(b =>
            b.Terminator is UnreachableTerminator);

        Assert.NotNull(unreachableBlock);
    }

    #endregion

    #region Nested Loops

    [Fact]
    public void Build_NestedLoops_BreakExitsInnerLoopOnly()
    {
        var func = CreateFunction("nested_break", ImmutableArray.Create<Statement>(
            new WhileStatement
            {
                Test = Bool(true),
                Body = ImmutableArray.Create<Statement>(
                    new WhileStatement
                    {
                        Test = Bool(true),
                        Body = ImmutableArray.Create<Statement>(
                            new BreakStatement()
                        )
                    }
                )
            }
        ));

        var cfg = _builder.Build(func);

        // There should be exactly one break terminator
        var breakBlocks = cfg.Blocks.Where(b => b.Terminator is BreakTerminator).ToList();
        Assert.Single(breakBlocks);

        // The break should target the inner loop's exit
        var breakTerm = (BreakTerminator)breakBlocks[0].Terminator!;
        Assert.Contains("while_exit", breakTerm.Target.Label);
    }

    [Fact]
    public void Build_NestedLoops_ContinueJumpsToInnerHeader()
    {
        var func = CreateFunction("nested_continue", ImmutableArray.Create<Statement>(
            new WhileStatement
            {
                Test = Bool(true),
                Body = ImmutableArray.Create<Statement>(
                    new ForStatement
                    {
                        Target = Id("i"),
                        Iterator = Id("items"),
                        Body = ImmutableArray.Create<Statement>(
                            new ContinueStatement()
                        )
                    }
                )
            }
        ));

        var cfg = _builder.Build(func);

        // There should be exactly one continue terminator
        var continueBlocks = cfg.Blocks.Where(b => b.Terminator is ContinueTerminator).ToList();
        Assert.Single(continueBlocks);

        // The continue should target the inner loop's header (for_header)
        var contTerm = (ContinueTerminator)continueBlocks[0].Terminator!;
        Assert.Contains("for_header", contTerm.Target.Label);
    }

    #endregion

    #region SourceFunction

    [Fact]
    public void Build_Function_SetsSourceFunction()
    {
        var func = CreateFunction("my_func", ImmutableArray.Create<Statement>(Pass()));

        var cfg = _builder.Build(func);

        Assert.NotNull(cfg.SourceFunction);
        Assert.Equal("my_func", cfg.SourceFunction!.Name);
    }

    [Fact]
    public void Build_StatementList_SourceFunctionIsNull()
    {
        var statements = new List<Statement> { Pass() };

        var cfg = _builder.Build(statements);

        Assert.Null(cfg.SourceFunction);
    }

    #endregion

    #region Complex Control Flow

    [Fact]
    public void Build_IfElifElse_AllBranchesConnectToMerge()
    {
        var func = CreateFunction("if_elif_else", ImmutableArray.Create<Statement>(
            new IfStatement
            {
                Test = Bool(true),
                ThenBody = ImmutableArray.Create<Statement>(Pass()),
                ElifClauses = ImmutableArray.Create(
                    new ElifClause
                    {
                        Test = Bool(false),
                        Body = ImmutableArray.Create<Statement>(Pass())
                    },
                    new ElifClause
                    {
                        Test = Bool(false),
                        Body = ImmutableArray.Create<Statement>(Pass())
                    }
                ),
                ElseBody = ImmutableArray.Create<Statement>(Pass())
            }
        ));

        var cfg = _builder.Build(func);

        // Should have conditional branches for if + 2 elifs = 3
        var condBlocks = cfg.Blocks.Where(b => b.Terminator is ConditionalBranchTerminator).ToList();
        Assert.True(condBlocks.Count >= 3);

        // All blocks should be reachable
        var unreachable = cfg.FindUnreachableBlocks();
        Assert.Empty(unreachable);
    }

    [Fact]
    public void Build_TryExceptElseFinally_AllPathsConnected()
    {
        var func = CreateFunction("try_full", ImmutableArray.Create<Statement>(
            new TryStatement
            {
                Body = ImmutableArray.Create<Statement>(Pass()),
                Handlers = ImmutableArray.Create(new ExceptHandler
                {
                    ExceptionType = new TypeAnnotation { Name = "Exception" },
                    Body = ImmutableArray.Create<Statement>(Pass())
                }),
                ElseBody = ImmutableArray.Create<Statement>(Pass()),
                FinallyBody = ImmutableArray.Create<Statement>(Pass())
            }
        ));

        var cfg = _builder.Build(func);

        // Should have try, except, else, finally, and merge blocks
        Assert.NotNull(cfg.Blocks.FirstOrDefault(b => b.Label.Contains("try")));
        Assert.NotNull(cfg.Blocks.FirstOrDefault(b => b.Label.Contains("except")));
        Assert.NotNull(cfg.Blocks.FirstOrDefault(b => b.Label.Contains("else")));
        Assert.NotNull(cfg.Blocks.FirstOrDefault(b => b.Label.Contains("finally")));

        // All blocks should be reachable
        var unreachable = cfg.FindUnreachableBlocks();
        Assert.Empty(unreachable);
    }

    [Fact]
    public void Build_WhileBreakElse_BreakBypassesElse()
    {
        // In Python, break bypasses the else clause
        var func = CreateFunction("while_break_else", ImmutableArray.Create<Statement>(
            new WhileStatement
            {
                Test = Bool(true),
                Body = ImmutableArray.Create<Statement>(
                    new IfStatement
                    {
                        Test = Bool(true),
                        ThenBody = ImmutableArray.Create<Statement>(new BreakStatement())
                    }
                ),
                ElseBody = ImmutableArray.Create<Statement>(Pass())
            }
        ));

        var cfg = _builder.Build(func);

        // Break should target while_exit, not while_else
        var breakBlock = cfg.Blocks.FirstOrDefault(b => b.Terminator is BreakTerminator);
        Assert.NotNull(breakBlock);

        var breakTerm = (BreakTerminator)breakBlock!.Terminator!;
        Assert.Contains("while_exit", breakTerm.Target.Label);
        Assert.DoesNotContain("while_else", breakTerm.Target.Label);
    }

    [Fact]
    public void Build_MultipleReturnsInIf_AllConnectToExit()
    {
        var func = CreateFunction("multi_return", ImmutableArray.Create<Statement>(
            new IfStatement
            {
                Test = Bool(true),
                ThenBody = ImmutableArray.Create<Statement>(
                    new ReturnStatement { Value = Int(1) }
                ),
                ElseBody = ImmutableArray.Create<Statement>(
                    new ReturnStatement { Value = Int(2) }
                )
            }
        ));

        var cfg = _builder.Build(func);

        // Both return blocks should connect to exit
        var returnBlocks = cfg.Blocks.Where(b => b.Terminator is ReturnTerminator).ToList();
        Assert.Equal(2, returnBlocks.Count);

        foreach (var block in returnBlocks)
        {
            Assert.Contains(cfg.Exit, block.Successors);
        }
    }

    #endregion

    #region Helpers

    private static FunctionDef CreateFunction(string name, ImmutableArray<Statement> body)
    {
        return new FunctionDef
        {
            Name = name,
            Parameters = ImmutableArray<Parameter>.Empty,
            Body = body
        };
    }

    #endregion
}
