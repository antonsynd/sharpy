using System.Collections.Immutable;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Text;
using Xunit;

namespace Sharpy.Compiler.Tests.Shared;

public class AstHelperTests
{
    #region TryGetConstantIntIndex

    [Theory]
    [InlineData("0", 0)]
    [InlineData("1", 1)]
    [InlineData("42", 42)]
    [InlineData("100", 100)]
    public void TryGetConstantIntIndex_ReturnsTrue_ForIntegerLiterals(string value, int expected)
    {
        var expr = new IntegerLiteral { Value = value };
        Assert.True(AstHelper.TryGetConstantIntIndex(expr, out var result));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("1", -1)]
    [InlineData("5", -5)]
    [InlineData("42", -42)]
    public void TryGetConstantIntIndex_ReturnsTrue_ForNegativeLiterals(string innerValue, int expected)
    {
        var expr = new UnaryOp
        {
            Operator = UnaryOperator.Minus,
            Operand = new IntegerLiteral { Value = innerValue }
        };
        Assert.True(AstHelper.TryGetConstantIntIndex(expr, out var result));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TryGetConstantIntIndex_ReturnsFalse_ForStringLiteral()
    {
        var expr = new StringLiteral { Value = "hello" };
        Assert.False(AstHelper.TryGetConstantIntIndex(expr, out _));
    }

    [Fact]
    public void TryGetConstantIntIndex_ReturnsFalse_ForIdentifier()
    {
        var expr = new Identifier { Name = "x" };
        Assert.False(AstHelper.TryGetConstantIntIndex(expr, out _));
    }

    [Fact]
    public void TryGetConstantIntIndex_ReturnsFalse_ForUnaryPlus()
    {
        var expr = new UnaryOp
        {
            Operator = UnaryOperator.Plus,
            Operand = new IntegerLiteral { Value = "1" }
        };
        Assert.False(AstHelper.TryGetConstantIntIndex(expr, out _));
    }

    [Fact]
    public void TryGetConstantIntIndex_ReturnsFalse_ForFloatLiteral()
    {
        var expr = new FloatLiteral { Value = "3.14" };
        Assert.False(AstHelper.TryGetConstantIntIndex(expr, out _));
    }

    #endregion

    #region ExtractNarrowingKey

    [Theory]
    [InlineData("x", "x")]
    [InlineData("my_var", "my_var")]
    public void ExtractNarrowingKey_ReturnsName_ForIdentifiers(string name, string expected)
    {
        var expr = new Identifier { Name = name };
        Assert.Equal(expected, AstHelper.ExtractNarrowingKey(expr));
    }

    [Fact]
    public void ExtractNarrowingKey_ReturnsDottedPath_ForMemberAccess()
    {
        var expr = new MemberAccess
        {
            Object = new Identifier { Name = "self" },
            Member = "value"
        };
        Assert.Equal("self.value", AstHelper.ExtractNarrowingKey(expr));
    }

    [Fact]
    public void ExtractNarrowingKey_ReturnsChainedPath_ForNestedMemberAccess()
    {
        var expr = new MemberAccess
        {
            Object = new MemberAccess
            {
                Object = new Identifier { Name = "self" },
                Member = "inner"
            },
            Member = "value"
        };
        Assert.Equal("self.inner.value", AstHelper.ExtractNarrowingKey(expr));
    }

    [Fact]
    public void ExtractNarrowingKey_ReturnsBracketPath_ForIndexAccess()
    {
        var expr = new IndexAccess
        {
            Object = new Identifier { Name = "arr" },
            Index = new Identifier { Name = "i" }
        };
        Assert.Equal("arr[i]", AstHelper.ExtractNarrowingKey(expr));
    }

    [Fact]
    public void ExtractNarrowingKey_ReturnsNull_ForUnsupportedExpressions()
    {
        var expr = new IntegerLiteral { Value = "42" };
        Assert.Null(AstHelper.ExtractNarrowingKey(expr));
    }

    [Fact]
    public void ExtractNarrowingKey_ReturnsNull_ForBinaryOp()
    {
        var expr = new BinaryOp
        {
            Left = new Identifier { Name = "a" },
            Operator = BinaryOperator.Add,
            Right = new Identifier { Name = "b" }
        };
        Assert.Null(AstHelper.ExtractNarrowingKey(expr));
    }

    #endregion

    #region ContainsWalrusExpression

    [Fact]
    public void ContainsWalrusExpression_ReturnsTrue_ForWalrus()
    {
        var expr = new WalrusExpression
        {
            Target = "x",
            Value = new IntegerLiteral { Value = "1" }
        };
        Assert.True(AstHelper.ContainsWalrusExpression(expr));
    }

    [Fact]
    public void ContainsWalrusExpression_ReturnsFalse_ForSimpleExpression()
    {
        var expr = new Identifier { Name = "x" };
        Assert.False(AstHelper.ContainsWalrusExpression(expr));
    }

    [Fact]
    public void ContainsWalrusExpression_ReturnsTrue_ForNestedInBinaryOp()
    {
        var expr = new BinaryOp
        {
            Left = new WalrusExpression
            {
                Target = "x",
                Value = new IntegerLiteral { Value = "1" }
            },
            Operator = BinaryOperator.Add,
            Right = new IntegerLiteral { Value = "2" }
        };
        Assert.True(AstHelper.ContainsWalrusExpression(expr));
    }

    #endregion

    #region Ellipsis stub detection (#1214)

    private static ExpressionStatement Stmt(Expression expr) => new() { Expression = expr };

    private static Expression Paren(Expression inner) => new Parenthesized { Expression = inner };

    private static ImmutableArray<Statement> Body(params Statement[] statements) =>
        ImmutableArray.Create(statements);

    [Fact]
    public void TryGetEllipsisStub_ReturnsNode_ForBareEllipsis()
    {
        var literal = new EllipsisLiteral();
        Assert.True(AstHelper.TryGetEllipsisStub(Stmt(literal), out var found));
        Assert.Same(literal, found);
    }

    [Fact]
    public void TryGetEllipsisStub_ReturnsInnerNode_ForParenthesizedEllipsis()
    {
        var literal = new EllipsisLiteral();
        Assert.True(AstHelper.TryGetEllipsisStub(Stmt(Paren(literal)), out var found));
        Assert.Same(literal, found);
    }

    [Fact]
    public void TryGetEllipsisStub_ReturnsInnerNode_ForDoubleParenthesizedEllipsis()
    {
        var literal = new EllipsisLiteral();
        Assert.True(AstHelper.TryGetEllipsisStub(Stmt(Paren(Paren(literal))), out var found));
        Assert.Same(literal, found);
    }

    [Fact]
    public void TryGetEllipsisStub_PreservesSpan_ThroughParentheses()
    {
        // The Span distinction is what BodylessSyntaxValidator keys on: a parser-synthesized
        // body-less ellipsis has a null Span, a user-written one does not. Unwrapping must not
        // erase it (#1214, D1).
        var spanned = new EllipsisLiteral { Span = new TextSpan(7, 3) };
        Assert.True(AstHelper.TryGetEllipsisStub(Stmt(Paren(spanned)), out var found));
        Assert.NotNull(found.Span);
        Assert.Equal(new TextSpan(7, 3), found.Span!.Value);

        var synthesized = new EllipsisLiteral();
        Assert.True(AstHelper.TryGetEllipsisStub(Stmt(synthesized), out var synthesizedFound));
        Assert.Null(synthesizedFound.Span);
    }

    [Fact]
    public void TryGetEllipsisStub_ReturnsFalse_ForPassStatement()
    {
        Assert.False(AstHelper.TryGetEllipsisStub(new PassStatement(), out var found));
        Assert.Null(found);
    }

    [Fact]
    public void TryGetEllipsisStub_ReturnsFalse_ForNonEllipsisExpression()
    {
        Assert.False(AstHelper.TryGetEllipsisStub(Stmt(new IntegerLiteral { Value = "1" }), out _));
        Assert.False(AstHelper.TryGetEllipsisStub(Stmt(Paren(new Identifier { Name = "x" })), out _));
    }

    [Fact]
    public void TryGetEllipsisStub_ReturnsFalse_ForNonExpressionStatement()
    {
        Assert.False(AstHelper.TryGetEllipsisStub(new ReturnStatement(), out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IsEllipsisStubBody_ReturnsTrue_ForSingleEllipsis(bool parenthesized)
    {
        Expression expr = new EllipsisLiteral();
        if (parenthesized)
            expr = Paren(expr);

        Assert.True(AstHelper.IsEllipsisStubBody(Body(Stmt(expr))));
    }

    [Fact]
    public void IsEllipsisStubBody_ReturnsFalse_ForPassBody()
    {
        Assert.False(AstHelper.IsEllipsisStubBody(Body(new PassStatement())));
    }

    [Fact]
    public void IsEllipsisStubBody_ReturnsFalse_ForEmptyBody()
    {
        Assert.False(AstHelper.IsEllipsisStubBody(ImmutableArray<Statement>.Empty));
    }

    [Fact]
    public void IsEllipsisStubBody_ReturnsFalse_ForMultiStatementBody()
    {
        Assert.False(AstHelper.IsEllipsisStubBody(
            Body(Stmt(new EllipsisLiteral()), Stmt(new EllipsisLiteral()))));
    }

    [Fact]
    public void IsEllipsisStubBody_ReturnsFalse_ForRealBody()
    {
        Assert.False(AstHelper.IsEllipsisStubBody(Body(new ReturnStatement())));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IsAbstractStubBody_ReturnsTrue_ForSingleEllipsis(bool parenthesized)
    {
        Expression expr = new EllipsisLiteral();
        if (parenthesized)
            expr = Paren(expr);

        Assert.True(AstHelper.IsAbstractStubBody(Body(Stmt(expr))));
    }

    [Fact]
    public void IsAbstractStubBody_ReturnsTrue_ForDoubleParenthesizedEllipsis()
    {
        Assert.True(AstHelper.IsAbstractStubBody(Body(Stmt(Paren(Paren(new EllipsisLiteral()))))));
    }

    [Fact]
    public void IsAbstractStubBody_ReturnsTrue_ForPassBody()
    {
        // `pass` is a co-equal stub body at the abstract-member seams; the ellipsis-only wrapper
        // must not be used there or `pass` stubs silently stop being recognized (#1214).
        Assert.True(AstHelper.IsAbstractStubBody(Body(new PassStatement())));
    }

    [Fact]
    public void IsAbstractStubBody_ReturnsFalse_ForEmptyBody()
    {
        // The one site that also accepts an empty body keeps that disjunct at its own call site.
        Assert.False(AstHelper.IsAbstractStubBody(ImmutableArray<Statement>.Empty));
    }

    [Fact]
    public void IsAbstractStubBody_ReturnsFalse_ForMultiStatementBody()
    {
        Assert.False(AstHelper.IsAbstractStubBody(Body(new PassStatement(), new PassStatement())));
    }

    [Fact]
    public void IsAbstractStubBody_ReturnsFalse_ForRealBody()
    {
        Assert.False(AstHelper.IsAbstractStubBody(Body(new ReturnStatement())));
    }

    #endregion
}
