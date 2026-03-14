using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;
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
}
