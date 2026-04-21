using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

/// <summary>
/// Additional tests for string format-related methods not covered by StrFormatTests.cs
/// or StringExtensionTests.cs. Focuses on: Maketrans 3-arg (deletion), Translate edge cases.
/// </summary>
public class StrFormatAdditionalTests
{
    #region Maketrans 3-arg (with deletion set)

    [Fact]
    public void Maketrans_ThreeArgs_DeletesCharsInZ()
    {
        // Python: str.maketrans("abc", "xyz", "lmn") maps a->x, b->y, c->z, deletes l,m,n
        var table = StringExtensions.Maketrans("abc", "xyz", "lmn");
        "abclmn".Translate(table).Should().Be("xyz");
    }

    [Fact]
    public void Maketrans_ThreeArgs_OverlappingDeleteAndMap_DeleteWins()
    {
        // Python: str.maketrans("a", "x", "a") — 'a' is mapped and deleted
        // The delete set overwrites the map: 'a' gets deleted (mapped to "")
        var table = StringExtensions.Maketrans("a", "x", "a");
        "abc".Translate(table).Should().Be("bc");
    }

    [Fact]
    public void Maketrans_ThreeArgs_EmptyDeleteSet_SameAsTwoArg()
    {
        var table = StringExtensions.Maketrans("aeiou", "12345", "");
        "apple".Translate(table).Should().Be("1ppl2");
    }

    [Fact]
    public void Maketrans_UnequalXY_ThrowsValueError()
    {
        var act = () => StringExtensions.Maketrans("abc", "xy");
        act.Should().Throw<ValueError>();
    }

    #endregion

    #region Translate edge cases

    [Fact]
    public void Translate_UnmappedCharsPassThrough()
    {
        var table = StringExtensions.Maketrans("a", "x");
        "abc".Translate(table).Should().Be("xbc");
    }

    [Fact]
    public void Translate_EmptyString_ReturnsEmpty()
    {
        var table = StringExtensions.Maketrans("aeiou", "12345");
        "".Translate(table).Should().Be("");
    }

    [Fact]
    public void Translate_AllCharsDeleted_ReturnsEmpty()
    {
        var table = StringExtensions.Maketrans("", "", "hello");
        "hello".Translate(table).Should().Be("");
    }

    [Fact]
    public void Translate_MappingToMultipleChars_ExpandsChars()
    {
        // Manually create a table with a multi-char mapping
        var table = new System.Collections.Generic.Dictionary<char, string>
        {
            ['a'] = "AA"
        };
        "abc".Translate(table).Should().Be("AAbc");
    }

    #endregion

    #region Format — additional edge cases not covered in StrFormatTests.cs

    [Fact]
    public void Format_NoArgs_LiteralOnly()
    {
        "hello world".Format().Should().Be("hello world");
    }

    [Fact]
    public void Format_SingleBraceOpen_ThrowsValueError()
    {
        var act = () => "hello { world".Format();
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void Format_SingleBraceClose_ThrowsValueError()
    {
        var act = () => "hello } world".Format();
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void Format_IntArg_FormatsAsString()
    {
        "{0}".Format(42).Should().Be("42");
    }

    [Fact]
    public void Format_FloatWithGeneralFormat_UsesDefaultRepresentation()
    {
        // No type specifier for float — uses default ToString
        "{0}".Format(3.14).Should().Be("3.14");
    }

    #endregion

    #region FormatMap — additional edge cases

    [Fact]
    public void FormatMap_EmptyTemplate_ReturnsEmpty()
    {
        var mapping = new Dict<string, object>();
        "".FormatMap(mapping).Should().Be("");
    }

    [Fact]
    public void FormatMap_LiteralBraces_PreservesEscaped()
    {
        var mapping = new Dict<string, object>();
        "{{}}".FormatMap(mapping).Should().Be("{}");
    }

    #endregion
}
