using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests;

/// <summary>
/// <see cref="PyFormatSpec.TryReadDecimal"/>, the ONE digit reader of the format mini-language
/// (#2017): width, precision, a <c>str.format</c> field index and an item key all read through it,
/// so it reads every Unicode decimal digit (category <c>Nd</c>, as <c>str.isdecimal</c>) — a non-BMP
/// digit is a surrogate pair and advances two code units. Values are python3 3.12's
/// (<c>format(65, '٣d')</c> is <c>' 65'</c>; <c>'²'</c> is not <c>Nd</c> and is refused).
/// </summary>
public class PyFormatSpecDecimalReaderTests
{
    [Theory]
    [InlineData("123d", 123, 3)]
    [InlineData("١٢٣d", 123, 3)]             // Arabic-Indic
    [InlineData("１２３d", 123, 3)]             // fullwidth
    [InlineData("\U0001D7CF\U0001D7D0\U0001D7D1d", 123, 6)] // mathematical bold: 3 surrogate pairs
    [InlineData("1٢\U0001D7D1", 123, 4)]               // mixed families
    [InlineData("d", 0, 0)]                                 // an empty run is 0 at the same position
    [InlineData("²d", 0, 0)]                           // superscript two is No, not Nd
    public void ReadsTheRunOfNdDigits(string text, int expected, int expectedEnd)
    {
        int pos = 0;
        PyFormatSpec.TryReadDecimal(text, ref pos, out int value).Should().BeTrue();
        value.Should().Be(expected);
        pos.Should().Be(expectedEnd);
    }

    [Theory]
    [InlineData("99999999999999999999")]
    [InlineData("٩٩٩٩٩٩٩٩٩٩٩")]
    public void RefusesAValueBeyondInt32(string text)
    {
        int pos = 0;
        PyFormatSpec.TryReadDecimal(text, ref pos, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(65, "٣d", " 65")]
    [InlineData(65, "５d", "   65")]
    [InlineData(65, "\U0001D7D1d", " 65")]
    [InlineData(1.23456, ".٢f", "1.23")]
    [InlineData(1.23456, "\U0001D7D4.\U0001D7D0f", "  1.23")]
    public void WidthAndPrecisionReadNdDigits(object value, string spec, string expected)
    {
        PyFormat.Apply(value, spec).Should().Be(expected);
    }

    [Fact]
    public void FieldIndexAndItemKeyReadNdDigits()
    {
        "{١}{٠}".Format("a", "b").Should().Be("ba");
        "{\U0001D7CF}".Format("a", "b").Should().Be("b");
        "{0[\U0001D7CF]}".Format(new List<string>(new[] { "x", "y" })).Should().Be("y");
        FluentActions.Invoking(() => "{99999999999999999999}".Format("a"))
            .Should().Throw<ValueError>().WithMessage("Too many decimal digits in format string");
    }
}
