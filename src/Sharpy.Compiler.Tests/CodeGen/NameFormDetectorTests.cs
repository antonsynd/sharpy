using FluentAssertions;
using Sharpy.Compiler.Shared;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

[Collection("Sequential")]
public class NameFormDetectorTests
{
    #region Detect Tests

    [Fact]
    public void Detect_SnakeCase_ReturnsSnakeCase()
    {
        NameFormDetector.Detect("get_user_name").Should().Be(NameForm.SnakeCase);
        NameFormDetector.Detect("a_b_c").Should().Be(NameForm.SnakeCase);
        NameFormDetector.Detect("item1_count").Should().Be(NameForm.SnakeCase);
    }

    [Fact]
    public void Detect_PascalCase_ReturnsPascalCase()
    {
        NameFormDetector.Detect("HttpClient").Should().Be(NameForm.PascalCase);
        NameFormDetector.Detect("XMLParser").Should().Be(NameForm.PascalCase);
    }

    [Fact]
    public void Detect_CamelCase_ReturnsCamelCase()
    {
        NameFormDetector.Detect("httpClient").Should().Be(NameForm.CamelCase);
        NameFormDetector.Detect("iPhone").Should().Be(NameForm.CamelCase);
    }

    [Fact]
    public void Detect_ScreamingSnakeCase_ReturnsScreamingSnakeCase()
    {
        NameFormDetector.Detect("MAX_SIZE").Should().Be(NameForm.ScreamingSnakeCase);
        NameFormDetector.Detect("HTTP_STATUS_2XX").Should().Be(NameForm.ScreamingSnakeCase);
    }

    [Fact]
    public void Detect_SingleWordLower_ReturnsSingleWordLower()
    {
        NameFormDetector.Detect("hello").Should().Be(NameForm.SingleWordLower);
    }

    [Fact]
    public void Detect_SingleWordUpper_ReturnsSingleWordUpper()
    {
        NameFormDetector.Detect("HTTP").Should().Be(NameForm.SingleWordUpper);
    }

    [Fact]
    public void Detect_Dunder_ReturnsDunder()
    {
        NameFormDetector.Detect("__init__").Should().Be(NameForm.Dunder);
    }

    [Fact]
    public void Detect_Literal_ReturnsLiteral()
    {
        NameFormDetector.Detect("`some_name`").Should().Be(NameForm.Literal);
    }

    [Fact]
    public void Detect_ConsecutiveUnderscores_ReturnsUnrecognized()
    {
        NameFormDetector.Detect("foo__bar").Should().Be(NameForm.Unrecognized);
    }

    [Fact]
    public void Detect_MixedCaseWithUnderscores_ReturnsUnrecognized()
    {
        NameFormDetector.Detect("Foo_bar").Should().Be(NameForm.Unrecognized);
    }

    [Fact]
    public void Detect_EmptyString_ReturnsUnrecognized()
    {
        NameFormDetector.Detect("").Should().Be(NameForm.Unrecognized);
    }

    [Fact]
    public void Detect_NullString_ReturnsUnrecognized()
    {
        NameFormDetector.Detect(null!).Should().Be(NameForm.Unrecognized);
    }

    [Fact]
    public void Detect_SingleCharacter_ClassifiesCorrectly()
    {
        NameFormDetector.Detect("x").Should().Be(NameForm.SingleWordLower);
        NameFormDetector.Detect("X").Should().Be(NameForm.SingleWordUpper);
    }

    #endregion

    #region HasConsecutiveUnderscores Tests

    [Theory]
    [InlineData("foo__bar", true)]
    [InlineData("foo_bar", false)]
    [InlineData("___", true)]
    public void HasConsecutiveUnderscores_DetectsCorrectly(string input, bool expected)
    {
        NameFormDetector.HasConsecutiveUnderscores(input).Should().Be(expected);
    }

    #endregion

    #region IsConstantCaseName Tests

    [Theory]
    [InlineData("MAX_SIZE", true)]
    [InlineData("HTTP", true)]
    [InlineData("MAX_2", true)]
    [InlineData("hello", false)]
    [InlineData("HttpClient", false)]
    [InlineData("_", false)]
    [InlineData("", false)]
    public void IsConstantCaseName_ClassifiesCorrectly(string input, bool expected)
    {
        NameFormDetector.IsConstantCaseName(input).Should().Be(expected);
    }

    [Fact]
    public void IsConstantCaseName_NullString_ReturnsFalse()
    {
        NameFormDetector.IsConstantCaseName(null!).Should().BeFalse();
    }

    #endregion
}
