using System.Collections.Immutable;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;
using Xunit;

namespace Sharpy.Compiler.Tests.Shared;

public class StatementWalkerTests
{
    [Fact]
    public void Any_ReturnsTrue_WhenPredicateMatches()
    {
        var stmts = ImmutableArray.Create<Statement>(
            new PassStatement(),
            new ReturnStatement { Value = new IntegerLiteral { Value = "1" } });

        Assert.True(StatementWalker.Any(stmts, s => s is ReturnStatement));
    }

    [Fact]
    public void Any_ReturnsFalse_WhenNoMatch()
    {
        var stmts = ImmutableArray.Create<Statement>(
            new PassStatement(),
            new PassStatement());

        Assert.False(StatementWalker.Any(stmts, s => s is ReturnStatement));
    }

    [Fact]
    public void Any_ReturnsFalse_ForEmptyBody()
    {
        Assert.False(StatementWalker.Any(
            ImmutableArray<Statement>.Empty,
            s => s is ReturnStatement));
    }

    [Fact]
    public void Any_FindsMatch_NestedInIfThenBody()
    {
        var stmts = ImmutableArray.Create<Statement>(
            new IfStatement
            {
                Test = new BooleanLiteral { Value = true },
                ThenBody = ImmutableArray.Create<Statement>(
                    new ReturnStatement { Value = new IntegerLiteral { Value = "1" } })
            });

        Assert.True(StatementWalker.Any(stmts, s => s is ReturnStatement));
    }

    [Fact]
    public void Any_FindsMatch_NestedInIfElseBody()
    {
        var stmts = ImmutableArray.Create<Statement>(
            new IfStatement
            {
                Test = new BooleanLiteral { Value = true },
                ThenBody = ImmutableArray.Create<Statement>(new PassStatement()),
                ElseBody = ImmutableArray.Create<Statement>(
                    new ReturnStatement { Value = new IntegerLiteral { Value = "1" } })
            });

        Assert.True(StatementWalker.Any(stmts, s => s is ReturnStatement));
    }

    [Fact]
    public void Any_FindsMatch_NestedInWhileBody()
    {
        var stmts = ImmutableArray.Create<Statement>(
            new WhileStatement
            {
                Test = new BooleanLiteral { Value = true },
                Body = ImmutableArray.Create<Statement>(
                    new ReturnStatement { Value = new IntegerLiteral { Value = "1" } })
            });

        Assert.True(StatementWalker.Any(stmts, s => s is ReturnStatement));
    }

    [Fact]
    public void Any_FindsMatch_NestedInForBody()
    {
        var stmts = ImmutableArray.Create<Statement>(
            new ForStatement
            {
                Target = new Identifier { Name = "i" },
                Iterator = new Identifier { Name = "items" },
                Body = ImmutableArray.Create<Statement>(
                    new ReturnStatement { Value = new IntegerLiteral { Value = "1" } })
            });

        Assert.True(StatementWalker.Any(stmts, s => s is ReturnStatement));
    }

    [Fact]
    public void Any_SkipsFunctionDef_NestedScope()
    {
        // StatementWalker should NOT recurse into FunctionDef
        var stmts = ImmutableArray.Create<Statement>(
            new FunctionDef
            {
                Name = "inner",
                Body = ImmutableArray.Create<Statement>(
                    new ReturnStatement { Value = new IntegerLiteral { Value = "1" } })
            });

        Assert.False(StatementWalker.Any(stmts, s => s is ReturnStatement));
    }

    [Fact]
    public void Any_SkipsClassDef_NestedScope()
    {
        var stmts = ImmutableArray.Create<Statement>(
            new ClassDef
            {
                Name = "Inner",
                Body = ImmutableArray.Create<Statement>(
                    new PassStatement())
            });

        // The PassStatement inside ClassDef should not be found
        Assert.False(StatementWalker.Any(stmts, s => s is PassStatement));
    }

    [Fact]
    public void FirstOrDefault_ReturnsFirstMatch()
    {
        var ret1 = new ReturnStatement { Value = new IntegerLiteral { Value = "1" } };
        var ret2 = new ReturnStatement { Value = new IntegerLiteral { Value = "2" } };
        var stmts = ImmutableArray.Create<Statement>(ret1, ret2);

        var found = StatementWalker.FirstOrDefault(stmts, s => s as ReturnStatement);
        Assert.Same(ret1, found);
    }

    [Fact]
    public void FirstOrDefault_ReturnsNull_WhenNoMatch()
    {
        var stmts = ImmutableArray.Create<Statement>(new PassStatement());

        var found = StatementWalker.FirstOrDefault(stmts, s => s as ReturnStatement);
        Assert.Null(found);
    }

    [Fact]
    public void FirstOrDefault_FindsMatch_DeeplyNested()
    {
        var innerReturn = new ReturnStatement { Value = new IntegerLiteral { Value = "42" } };
        var stmts = ImmutableArray.Create<Statement>(
            new IfStatement
            {
                Test = new BooleanLiteral { Value = true },
                ThenBody = ImmutableArray.Create<Statement>(
                    new WhileStatement
                    {
                        Test = new BooleanLiteral { Value = true },
                        Body = ImmutableArray.Create<Statement>(innerReturn)
                    })
            });

        var found = StatementWalker.FirstOrDefault(stmts, s => s as ReturnStatement);
        Assert.Same(innerReturn, found);
    }
}
