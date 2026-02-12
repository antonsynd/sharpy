using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.CodeGen;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

[Collection("Sequential")]
public class DunderMappingTests
{
    #region GetCSharpName Tests

    [Theory]
    [InlineData("__init__", "Constructor")]
    [InlineData("__str__", "ToString")]
    [InlineData("__eq__", "Equals")]
    [InlineData("__hash__", "GetHashCode")]
    [InlineData("__getitem__", "GetItem")]
    [InlineData("__setitem__", "SetItem")]
    [InlineData("__len__", "Count")]
    [InlineData("__contains__", "Contains")]
    [InlineData("__iter__", "GetEnumerator")]
    // __bool__ is no longer mapped — it uses special codegen (operator true/false)
    public void GetCSharpName_KnownDunder_ReturnsMapping(string dunderName, string expected)
    {
        DunderMapping.GetCSharpName(dunderName).Should().Be(expected);
    }

    [Fact]
    public void GetCSharpName_UnknownDunder_ReturnsNull()
    {
        DunderMapping.GetCSharpName("__unknown__").Should().BeNull();
    }

    #endregion

    #region TransformUnknownDunder Tests

    [Theory]
    [InlineData("__add__", "__Add__")]
    [InlineData("__sub__", "__Sub__")]
    [InlineData("__custom_method__", "__CustomMethod__")]
    public void TransformUnknownDunder_CapitalizesMiddle(string input, string expected)
    {
        DunderMapping.TransformUnknownDunder(input).Should().Be(expected);
    }

    #endregion

    #region IsDunderMethod Tests

    [Theory]
    [InlineData("__init__", true)]
    [InlineData("__custom_method__", true)]
    [InlineData("init", false)]
    [InlineData("_private", false)]
    [InlineData("__x__", false)]       // length 5, excluded (> 5 required)
    [InlineData("__too_short_", false)] // doesn't end with __
    public void IsDunderMethod_ClassifiesCorrectly(string input, bool expected)
    {
        DunderMapping.IsDunderMethod(input).Should().Be(expected);
    }

    #endregion

    #region ResolveCSharpName Tests

    [Theory]
    [InlineData("__init__", "Constructor")]
    [InlineData("__str__", "ToString")]
    [InlineData("__eq__", "Equals")]
    [InlineData("__add__", "__Add__")]       // Unknown dunder: transforms via TransformUnknownDunder
    [InlineData("__sub__", "__Sub__")]
    [InlineData("__custom_method__", "__CustomMethod__")]
    public void ResolveCSharpName_DunderMethod_ReturnsCorrectName(string name, string expected)
    {
        DunderMapping.ResolveCSharpName(name).Should().Be(expected);
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("get_value")]
    [InlineData("_private")]
    [InlineData("__x__")]   // Too short (length 5, needs > 5)
    public void ResolveCSharpName_NonDunder_ReturnsNull(string name)
    {
        DunderMapping.ResolveCSharpName(name).Should().BeNull();
    }

    #endregion

    #region HasMapping Tests

    [Fact]
    public void HasMapping_KnownDunder_ReturnsTrue()
    {
        DunderMapping.HasMapping("__init__").Should().BeTrue();
    }

    [Fact]
    public void HasMapping_UnknownDunder_ReturnsFalse()
    {
        DunderMapping.HasMapping("__add__").Should().BeFalse();
    }

    #endregion

    #region TryGetBinaryExpressionKind Tests

    [Theory]
    [InlineData("__add__", SyntaxKind.AddExpression)]
    [InlineData("__sub__", SyntaxKind.SubtractExpression)]
    [InlineData("__mul__", SyntaxKind.MultiplyExpression)]
    [InlineData("__div__", SyntaxKind.DivideExpression)]
    [InlineData("__mod__", SyntaxKind.ModuloExpression)]
    [InlineData("__and__", SyntaxKind.BitwiseAndExpression)]
    [InlineData("__or__", SyntaxKind.BitwiseOrExpression)]
    [InlineData("__xor__", SyntaxKind.ExclusiveOrExpression)]
    [InlineData("__lshift__", SyntaxKind.LeftShiftExpression)]
    [InlineData("__rshift__", SyntaxKind.RightShiftExpression)]
    [InlineData("__ne__", SyntaxKind.NotEqualsExpression)]
    [InlineData("__lt__", SyntaxKind.LessThanExpression)]
    [InlineData("__le__", SyntaxKind.LessThanOrEqualExpression)]
    [InlineData("__gt__", SyntaxKind.GreaterThanExpression)]
    [InlineData("__ge__", SyntaxKind.GreaterThanOrEqualExpression)]
    public void TryGetBinaryExpressionKind_OperatorDunder_ReturnsKind(string dunder, SyntaxKind expected)
    {
        DunderMapping.TryGetBinaryExpressionKind(dunder).Should().Be(expected);
    }

    [Theory]
    [InlineData("__eq__")]       // Handled by method map (→ Equals), not operator expression
    [InlineData("__init__")]
    [InlineData("__str__")]
    [InlineData("__neg__")]      // Unary, not binary
    [InlineData("__bool__")]
    [InlineData("not_a_dunder")]
    public void TryGetBinaryExpressionKind_NonBinaryOperator_ReturnsNull(string dunder)
    {
        DunderMapping.TryGetBinaryExpressionKind(dunder).Should().BeNull();
    }

    #endregion

    #region TryGetUnaryExpressionKind Tests

    [Theory]
    [InlineData("__neg__", SyntaxKind.UnaryMinusExpression)]
    [InlineData("__pos__", SyntaxKind.UnaryPlusExpression)]
    [InlineData("__invert__", SyntaxKind.BitwiseNotExpression)]
    public void TryGetUnaryExpressionKind_UnaryDunder_ReturnsKind(string dunder, SyntaxKind expected)
    {
        DunderMapping.TryGetUnaryExpressionKind(dunder).Should().Be(expected);
    }

    [Theory]
    [InlineData("__add__")]      // Binary, not unary
    [InlineData("__eq__")]
    [InlineData("__bool__")]
    [InlineData("not_a_dunder")]
    public void TryGetUnaryExpressionKind_NonUnaryOperator_ReturnsNull(string dunder)
    {
        DunderMapping.TryGetUnaryExpressionKind(dunder).Should().BeNull();
    }

    #endregion
}
