using FluentAssertions;
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
    [InlineData("__len__", "Length")]
    [InlineData("__contains__", "Contains")]
    [InlineData("__iter__", "GetEnumerator")]
    [InlineData("__bool__", "ToBoolean")]
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
}
