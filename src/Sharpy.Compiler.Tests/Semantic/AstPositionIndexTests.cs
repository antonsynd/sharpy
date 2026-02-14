using System.Collections.Immutable;
using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Text;

namespace Sharpy.Compiler.Tests.Semantic;

public class AstPositionIndexTests
{
    #region Empty Module

    [Fact]
    public void EmptyModule_FindNodeAtPosition_ReturnsNull()
    {
        // Module with no body and no span
        var module = new Module { Body = ImmutableArray<Statement>.Empty };
        var index = new AstPositionIndex(module);

        index.FindNodeAtPosition(0).Should().BeNull();
        index.FindNodeAtPosition(10).Should().BeNull();
    }

    [Fact]
    public void EmptyModule_FindNodesAtPosition_ReturnsEmpty()
    {
        var module = new Module { Body = ImmutableArray<Statement>.Empty };
        var index = new AstPositionIndex(module);

        index.FindNodesAtPosition(0).Should().BeEmpty();
    }

    [Fact]
    public void EmptyModule_Count_IsZero()
    {
        var module = new Module { Body = ImmutableArray<Statement>.Empty };
        var index = new AstPositionIndex(module);

        index.Count.Should().Be(0);
    }

    #endregion

    #region Simple Expression

    [Fact]
    public void SimpleExpression_FindNodeAtPosition_ReturnsCorrectNode()
    {
        // Simulate: x = 42
        //           ^---^  span [0, 6)
        //               ^^  IntegerLiteral span [4, 2) = "42"
        var intLiteral = new IntegerLiteral
        {
            Value = "42",
            Span = new TextSpan(4, 2)
        };
        var varDecl = new VariableDeclaration
        {
            Name = "x",
            InitialValue = intLiteral,
            Span = new TextSpan(0, 6)
        };
        var module = new Module
        {
            Body = ImmutableArray.Create<Statement>(varDecl),
            Span = new TextSpan(0, 6)
        };

        var index = new AstPositionIndex(module);

        // Position 0 is inside module and varDecl, deepest is varDecl
        index.FindNodeAtPosition(0).Should().Be(varDecl);

        // Position 4 is inside intLiteral (and module, varDecl), deepest is intLiteral
        index.FindNodeAtPosition(4).Should().Be(intLiteral);

        // Position 5 is inside intLiteral span [4,6)
        index.FindNodeAtPosition(5).Should().Be(intLiteral);
    }

    [Fact]
    public void SimpleExpression_PositionAtBoundary_ReturnsCorrectNode()
    {
        var intLiteral = new IntegerLiteral
        {
            Value = "42",
            Span = new TextSpan(4, 2) // [4, 6)
        };
        var varDecl = new VariableDeclaration
        {
            Name = "x",
            InitialValue = intLiteral,
            Span = new TextSpan(0, 6) // [0, 6)
        };
        var module = new Module
        {
            Body = ImmutableArray.Create<Statement>(varDecl),
            Span = new TextSpan(0, 6)
        };

        var index = new AstPositionIndex(module);

        // Position 6 is at End (exclusive), should not be contained
        index.FindNodeAtPosition(6).Should().BeNull();

        // Position 3 is inside module+varDecl but before intLiteral
        index.FindNodeAtPosition(3).Should().Be(varDecl);
    }

    #endregion

    #region Nested Nodes

    [Fact]
    public void NestedNodes_FindNodeAtPosition_ReturnsDeepestNode()
    {
        // Simulate: (1 + 2)
        // Module [0, 10), ExprStmt [0, 10), Paren [0, 7), BinaryOp [1, 5), IntLit "1" [1, 1), IntLit "2" [5, 1)
        var left = new IntegerLiteral { Value = "1", Span = new TextSpan(1, 1) };
        var right = new IntegerLiteral { Value = "2", Span = new TextSpan(5, 1) };
        var binOp = new BinaryOp
        {
            Left = left,
            Right = right,
            Operator = BinaryOperator.Add,
            Span = new TextSpan(1, 5) // [1, 6)
        };
        var paren = new Parenthesized
        {
            Expression = binOp,
            Span = new TextSpan(0, 7) // [0, 7)
        };
        var exprStmt = new ExpressionStatement
        {
            Expression = paren,
            Span = new TextSpan(0, 10) // [0, 10)
        };
        var module = new Module
        {
            Body = ImmutableArray.Create<Statement>(exprStmt),
            Span = new TextSpan(0, 10)
        };

        var index = new AstPositionIndex(module);

        // Position 1: inside left IntLit [1,2), binOp [1,6), paren [0,7), exprStmt [0,10), module [0,10)
        // Deepest = left (length 1)
        index.FindNodeAtPosition(1).Should().Be(left);

        // Position 5: inside right IntLit [5,6), binOp [1,6), paren [0,7), exprStmt, module
        index.FindNodeAtPosition(5).Should().Be(right);

        // Position 3: inside binOp [1,6), paren [0,7), exprStmt, module. No leaf literal.
        index.FindNodeAtPosition(3).Should().Be(binOp);
    }

    [Fact]
    public void NestedNodes_FindNodesAtPosition_ReturnsParentChain()
    {
        var left = new IntegerLiteral { Value = "1", Span = new TextSpan(1, 1) };
        var right = new IntegerLiteral { Value = "2", Span = new TextSpan(5, 1) };
        var binOp = new BinaryOp
        {
            Left = left,
            Right = right,
            Operator = BinaryOperator.Add,
            Span = new TextSpan(1, 5)
        };
        var paren = new Parenthesized
        {
            Expression = binOp,
            Span = new TextSpan(0, 7)
        };
        var exprStmt = new ExpressionStatement
        {
            Expression = paren,
            Span = new TextSpan(0, 10)
        };
        var module = new Module
        {
            Body = ImmutableArray.Create<Statement>(exprStmt),
            Span = new TextSpan(0, 10)
        };

        var index = new AstPositionIndex(module);

        var chain = index.FindNodesAtPosition(1);

        // Should be ordered outermost to innermost
        chain.Should().HaveCount(5);
        chain[0].Should().BeOneOf(module, exprStmt); // both have length 10
        chain[1].Should().BeOneOf(module, exprStmt);
        chain[2].Should().Be(paren);   // length 7
        chain[3].Should().Be(binOp);   // length 5
        chain[4].Should().Be(left);    // length 1
    }

    #endregion

    #region Between Nodes

    [Fact]
    public void PositionBetweenNodes_ReturnsNull()
    {
        // Two statements with a gap between them
        var stmt1 = new PassStatement { Span = new TextSpan(0, 4) };  // [0, 4)
        var stmt2 = new PassStatement { Span = new TextSpan(10, 4) }; // [10, 14)
        var module = new Module
        {
            Body = ImmutableArray.Create<Statement>(stmt1, stmt2)
            // No module-level span, so gap at positions 4-9 is empty
        };

        var index = new AstPositionIndex(module);

        // Position 5 is between the two statements
        index.FindNodeAtPosition(5).Should().BeNull();

        // Position 7 is also in the gap
        index.FindNodeAtPosition(7).Should().BeNull();
    }

    [Fact]
    public void PositionBeforeAllNodes_ReturnsNull()
    {
        var stmt = new PassStatement { Span = new TextSpan(10, 4) };
        var module = new Module
        {
            Body = ImmutableArray.Create<Statement>(stmt)
        };

        var index = new AstPositionIndex(module);

        index.FindNodeAtPosition(5).Should().BeNull();
    }

    [Fact]
    public void PositionAfterAllNodes_ReturnsNull()
    {
        var stmt = new PassStatement { Span = new TextSpan(0, 4) }; // [0, 4)
        var module = new Module
        {
            Body = ImmutableArray.Create<Statement>(stmt)
        };

        var index = new AstPositionIndex(module);

        index.FindNodeAtPosition(4).Should().BeNull();
        index.FindNodeAtPosition(100).Should().BeNull();
    }

    #endregion

    #region Nodes Without Span

    [Fact]
    public void NodesWithoutSpan_AreNotIndexed()
    {
        // A node without Span set (null) should be skipped
        var withSpan = new IntegerLiteral { Value = "1", Span = new TextSpan(0, 1) };
        var withoutSpan = new IntegerLiteral { Value = "2" }; // Span is null
        var listLit = new ListLiteral
        {
            Elements = ImmutableArray.Create<Expression>(withSpan, withoutSpan),
            Span = new TextSpan(0, 5)
        };
        var module = new Module
        {
            Body = ImmutableArray.Create<Statement>(
                new ExpressionStatement { Expression = listLit, Span = new TextSpan(0, 5) })
        };

        var index = new AstPositionIndex(module);

        // withoutSpan has no span, so it shouldn't be counted
        // Indexed: listLit, exprStmt, withSpan = 3
        index.Count.Should().Be(3);
    }

    [Fact]
    public void NodesWithEmptySpan_AreNotIndexed()
    {
        var emptySpan = new IntegerLiteral { Value = "1", Span = TextSpan.Empty }; // Length 0
        var module = new Module
        {
            Body = ImmutableArray.Create<Statement>(
                new ExpressionStatement { Expression = emptySpan, Span = new TextSpan(0, 5) })
        };

        var index = new AstPositionIndex(module);

        // Empty span node should be excluded, only exprStmt indexed
        index.Count.Should().Be(1);
    }

    #endregion

    #region Multiple Siblings

    [Fact]
    public void MultipleSiblings_FindCorrectNode()
    {
        // Three sequential statements
        var s1 = new PassStatement { Span = new TextSpan(0, 4) };
        var s2 = new PassStatement { Span = new TextSpan(5, 4) };
        var s3 = new PassStatement { Span = new TextSpan(10, 4) };
        var module = new Module
        {
            Body = ImmutableArray.Create<Statement>(s1, s2, s3),
            Span = new TextSpan(0, 14)
        };

        var index = new AstPositionIndex(module);

        index.FindNodeAtPosition(0).Should().Be(s1);
        index.FindNodeAtPosition(3).Should().Be(s1);
        index.FindNodeAtPosition(5).Should().Be(s2);
        index.FindNodeAtPosition(8).Should().Be(s2);
        index.FindNodeAtPosition(10).Should().Be(s3);
        index.FindNodeAtPosition(13).Should().Be(s3);
    }

    #endregion
}
