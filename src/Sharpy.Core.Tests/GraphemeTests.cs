using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class Grapheme_Tests
{
    // ----- Length -----

    [Fact]
    public void Length_Ascii_ReturnsCharacterCount()
    {
        Sharpy.Grapheme.Length("hello").Should().Be(5);
    }

    [Fact]
    public void Length_EmptyString_ReturnsZero()
    {
        Sharpy.Grapheme.Length("").Should().Be(0);
    }

    [Fact]
    public void Length_CombiningAccent_TreatedAsSingleGrapheme()
    {
        // "é" composed as e + U+0301 (combining acute accent)
        Sharpy.Grapheme.Length("é").Should().Be(1);
    }

    [Fact]
    public void Length_PrecomposedAccent_IsSingleGrapheme()
    {
        // "é" as a single precomposed code point
        Sharpy.Grapheme.Length("é").Should().Be(1);
    }

    [Fact]
    public void Length_SurrogatePairEmoji_IsSingleGrapheme()
    {
        // U+1F600 GRINNING FACE — surrogate pair in UTF-16
        Sharpy.Grapheme.Length("\U0001F600").Should().Be(1);
    }

    [Fact]
    public void Length_ZwjFamilySequence_IsSingleGrapheme()
    {
        // Family ZWJ sequence: 👨‍👩‍👧‍👦
        var family = "\U0001F468‍\U0001F469‍\U0001F467‍\U0001F466";
        Sharpy.Grapheme.Length(family).Should().Be(1);
    }

    [Fact]
    public void Length_MixedAsciiAndEmoji_CountsCorrectly()
    {
        Sharpy.Grapheme.Length("hi\U0001F600!").Should().Be(4);
    }

    // ----- Graphemes -----

    [Fact]
    public void Graphemes_Ascii_SplitsByCharacter()
    {
        Sharpy.Grapheme.Graphemes("abc").Should().Equal("a", "b", "c");
    }

    [Fact]
    public void Graphemes_EmptyString_ReturnsEmpty()
    {
        Sharpy.Grapheme.Graphemes("").Should().BeEmpty();
    }

    [Fact]
    public void Graphemes_CombiningAccent_KeepsTogether()
    {
        Sharpy.Grapheme.Graphemes("éa").Should().Equal("é", "a");
    }

    [Fact]
    public void Graphemes_Emoji_KeepsSurrogatePairTogether()
    {
        Sharpy.Grapheme.Graphemes("a\U0001F600b").Should().Equal("a", "\U0001F600", "b");
    }

    // ----- At -----

    [Fact]
    public void At_PositiveIndex_ReturnsGrapheme()
    {
        Sharpy.Grapheme.At("abc", 0).Should().Be("a");
        Sharpy.Grapheme.At("abc", 1).Should().Be("b");
        Sharpy.Grapheme.At("abc", 2).Should().Be("c");
    }

    [Fact]
    public void At_NegativeIndex_CountsFromEnd()
    {
        Sharpy.Grapheme.At("abc", -1).Should().Be("c");
        Sharpy.Grapheme.At("abc", -2).Should().Be("b");
        Sharpy.Grapheme.At("abc", -3).Should().Be("a");
    }

    [Fact]
    public void At_Emoji_ReturnsWholeGrapheme()
    {
        Sharpy.Grapheme.At("a\U0001F600b", 1).Should().Be("\U0001F600");
    }

    [Fact]
    public void At_IndexEqualsLength_ThrowsIndexError()
    {
        var act = () => Sharpy.Grapheme.At("abc", 3);
        act.Should().Throw<IndexError>();
    }

    [Fact]
    public void At_NegativeBeyondStart_ThrowsIndexError()
    {
        var act = () => Sharpy.Grapheme.At("abc", -4);
        act.Should().Throw<IndexError>();
    }

    [Fact]
    public void At_EmptyString_ThrowsIndexError()
    {
        var act = () => Sharpy.Grapheme.At("", 0);
        act.Should().Throw<IndexError>();
    }

    // ----- Slice -----

    [Fact]
    public void Slice_FullRange_ReturnsAll()
    {
        Sharpy.Grapheme.Slice("hello", 0, 5).Should().Be("hello");
    }

    [Fact]
    public void Slice_MidRange_ReturnsSubstring()
    {
        Sharpy.Grapheme.Slice("hello", 1, 4).Should().Be("ell");
    }

    [Fact]
    public void Slice_OpenEnd_ReturnsToEnd()
    {
        Sharpy.Grapheme.Slice("hello", 2).Should().Be("llo");
    }

    [Fact]
    public void Slice_EmptyString_ReturnsEmpty()
    {
        Sharpy.Grapheme.Slice("", 0, 5).Should().Be("");
    }

    [Fact]
    public void Slice_StartAtEnd_ReturnsEmpty()
    {
        Sharpy.Grapheme.Slice("hello", 5, 5).Should().Be("");
    }

    [Fact]
    public void Slice_NegativeStart_CountsFromEnd()
    {
        Sharpy.Grapheme.Slice("hello", -3).Should().Be("llo");
    }

    [Fact]
    public void Slice_NegativeEnd_CountsFromEnd()
    {
        Sharpy.Grapheme.Slice("hello", 0, -2).Should().Be("hel");
    }

    [Fact]
    public void Slice_StartGreaterThanEnd_ReturnsEmpty()
    {
        Sharpy.Grapheme.Slice("hello", 3, 1).Should().Be("");
    }

    [Fact]
    public void Slice_OutOfRangeClamped()
    {
        Sharpy.Grapheme.Slice("hi", 0, 100).Should().Be("hi");
    }

    [Fact]
    public void Slice_AcrossEmoji_KeepsClustersWhole()
    {
        var s = "a\U0001F600b\U0001F600c";
        Sharpy.Grapheme.Slice(s, 1, 4).Should().Be("\U0001F600b\U0001F600");
    }
}
