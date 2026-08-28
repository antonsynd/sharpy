using System.Collections.Immutable;
using Sharpy.Compiler.Analysis.ControlFlow;
using Sharpy.Compiler.Parser.Ast;
using Xunit;
using Xunit.Abstractions;
using static Sharpy.Compiler.Tests.Analysis.ControlFlow.ControlFlowTestHelpers;

namespace Sharpy.Compiler.Tests.Analysis.ControlFlow;

/// <summary>
/// Guards that with-context expressions, match scrutinees, and match guards are recorded
/// in <see cref="BasicBlock.Expressions"/> after building a CFG (#1635).
/// </summary>
public class BlockExpressionTotalityTests
{
    private readonly ITestOutputHelper _output;
    private readonly ControlFlowGraphBuilder _builder = new();

    public BlockExpressionTotalityTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void WithStatement_ContextExpression_AppearsInBlockExpressions()
    {
        var ctxExpr = Id("ctx");
        var func = CreateFunction("with_test", ImmutableArray.Create<Statement>(
            new WithStatement
            {
                Items = ImmutableArray.Create(new WithItem
                {
                    ContextExpression = ctxExpr,
                    Name = "f"
                }),
                Body = ImmutableArray.Create<Statement>(Pass())
            }
        ));

        var cfg = _builder.Build(func);

        var blocksWithExpressions = cfg.Blocks
            .Where(b => b.Expressions.Count > 0)
            .ToList();

        _output.WriteLine($"Blocks with Expressions: {blocksWithExpressions.Count}");
        foreach (var block in blocksWithExpressions)
        {
            _output.WriteLine($"  {block.Label}: {block.Expressions.Count} expression(s)");
            foreach (var expr in block.Expressions)
                _output.WriteLine($"    {expr.GetType().Name}");
        }

        Assert.NotEmpty(blocksWithExpressions);
        var allExpressions = blocksWithExpressions.SelectMany(b => b.Expressions).ToList();
        Assert.Contains(ctxExpr, allExpressions);
    }

    [Fact]
    public void WithStatement_MultipleItems_AllContextExpressionsRecorded()
    {
        var ctx1 = Id("ctx1");
        var ctx2 = Id("ctx2");
        var func = CreateFunction("with_multi", ImmutableArray.Create<Statement>(
            new WithStatement
            {
                Items = ImmutableArray.Create(
                    new WithItem { ContextExpression = ctx1, Name = "f1" },
                    new WithItem { ContextExpression = ctx2, Name = "f2" }
                ),
                Body = ImmutableArray.Create<Statement>(Pass())
            }
        ));

        var cfg = _builder.Build(func);

        var allExpressions = cfg.Blocks.SelectMany(b => b.Expressions).ToList();
        Assert.Contains(ctx1, allExpressions);
        Assert.Contains(ctx2, allExpressions);
    }

    [Fact]
    public void MatchStatement_Scrutinee_AppearsInBlockExpressions()
    {
        var scrutinee = Id("value");
        var func = CreateFunction("match_test", ImmutableArray.Create<Statement>(
            new MatchStatement
            {
                Scrutinee = scrutinee,
                Cases = ImmutableArray.Create(new MatchCase
                {
                    Pattern = new WildcardPattern(),
                    Body = ImmutableArray.Create<Statement>(Pass())
                })
            }
        ));

        var cfg = _builder.Build(func);

        var blocksWithExpressions = cfg.Blocks
            .Where(b => b.Expressions.Count > 0)
            .ToList();

        _output.WriteLine($"Blocks with Expressions: {blocksWithExpressions.Count}");
        foreach (var block in blocksWithExpressions)
        {
            _output.WriteLine($"  {block.Label}: {block.Expressions.Count} expression(s)");
            foreach (var expr in block.Expressions)
                _output.WriteLine($"    {expr.GetType().Name}");
        }

        Assert.NotEmpty(blocksWithExpressions);
        var allExpressions = blocksWithExpressions.SelectMany(b => b.Expressions).ToList();
        Assert.Contains(scrutinee, allExpressions);
    }

    [Fact]
    public void MatchStatement_Guard_AppearsInBlockExpressions()
    {
        var scrutinee = Id("value");
        var guard = Bool(true);
        var func = CreateFunction("match_guard", ImmutableArray.Create<Statement>(
            new MatchStatement
            {
                Scrutinee = scrutinee,
                Cases = ImmutableArray.Create(new MatchCase
                {
                    Pattern = new WildcardPattern(),
                    Guard = guard,
                    Body = ImmutableArray.Create<Statement>(Pass())
                })
            }
        ));

        var cfg = _builder.Build(func);

        var allExpressions = cfg.Blocks.SelectMany(b => b.Expressions).ToList();
        Assert.Contains(scrutinee, allExpressions);
        Assert.Contains(guard, allExpressions);
    }

    private static FunctionDef CreateFunction(string name, ImmutableArray<Statement> body)
    {
        return new FunctionDef
        {
            Name = name,
            Parameters = ImmutableArray<Parameter>.Empty,
            Body = body
        };
    }
}
