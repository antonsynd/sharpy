using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class Str_Tests
{
    [Fact]
    public void Add_ConcatenatesTwoStrings()
    {
        var s1 = new Str("Hello");
        var s2 = new Str(" World");
        var result = s1 + s2;
        result.Should().Be(new Str("Hello World"));
    }

    [Fact]
    public void OperatorPlus_DelegatesToAdd()
    {
        var s1 = new Str("Hello");
        var s2 = new Str(" World");
        var result = s1 + s2;
        result.Should().Be(new Str("Hello World"));
    }

    [Fact]
    public void Mul_ReplicatesString()
    {
        var s = new Str("Ha");
        var result = s * 3;
        result.Should().Be(new Str("HaHaHa"));
    }

    [Fact]
    public void Mul_WithZero_ReturnsEmptyString()
    {
        var s = new Str("Hello");
        var result = s * 0;
        result.Should().Be(new Str(""));
    }

    [Fact]
    public void Mul_WithNegative_ReturnsEmptyString()
    {
        var s = new Str("Hello");
        var result = s * (-5);
        result.Should().Be(new Str(""));
    }

    [Fact]
    public void OperatorMultiply_DelegatesToMul()
    {
        var s = new Str("Ha");
        var result = s * 3;
        result.Should().Be(new Str("HaHaHa"));
    }

    [Fact]
    public void OperatorMultiply_Reversed_DelegatesToRMul()
    {
        var s = new Str("Ha");
        var result = 3 * s;
        result.Should().Be(new Str("HaHaHa"));
    }

    [Fact]
    public void Lt_ComparesLexicographically()
    {
        var s1 = new Str("apple");
        var s2 = new Str("banana");
        (s1 < s2).Should().BeTrue();
        (s2 < s1).Should().BeFalse();
    }

    [Fact]
    public void Le_ComparesLexicographically()
    {
        var s1 = new Str("apple");
        var s2 = new Str("apple");
        var s3 = new Str("banana");
        (s1 <= s2).Should().BeTrue();
        (s1 <= s3).Should().BeTrue();
        (s3 <= s1).Should().BeFalse();
    }

    [Fact]
    public void Gt_ComparesLexicographically()
    {
        var s1 = new Str("banana");
        var s2 = new Str("apple");
        (s1 > s2).Should().BeTrue();
        (s2 > s1).Should().BeFalse();
    }

    [Fact]
    public void Ge_ComparesLexicographically()
    {
        var s1 = new Str("banana");
        var s2 = new Str("banana");
        var s3 = new Str("apple");
        (s1 >= s2).Should().BeTrue();
        (s1 >= s3).Should().BeTrue();
        (s3 >= s1).Should().BeFalse();
    }

    [Fact]
    public void OperatorLessThan_Works()
    {
        var s1 = new Str("apple");
        var s2 = new Str("banana");
        (s1 < s2).Should().BeTrue();
        (s2 < s1).Should().BeFalse();
    }

    [Fact]
    public void OperatorGreaterThan_Works()
    {
        var s1 = new Str("banana");
        var s2 = new Str("apple");
        (s1 > s2).Should().BeTrue();
        (s2 > s1).Should().BeFalse();
    }

    [Fact]
    public void Contains_FindsSubstring()
    {
        var s = new Str("Hello World");
        s.Contains(new Str("World")).Should().BeTrue();
        s.Contains(new Str("xyz")).Should().BeFalse();
    }

    [Fact]
    public void Hash_ReturnsConsistentValue()
    {
        var s1 = new Str("Hello");
        var s2 = new Str("Hello");
        s1.GetHashCode().Should().Be(s2.GetHashCode());
    }

    [Fact]
    public void Split_WithoutSeparator_SplitsOnWhitespace()
    {
        var s = new Str("Hello World Test");
        var result = s.Split();
        Len(result).Should().Be(3);
        result[0].Should().Be(new Str("Hello"));
        result[1].Should().Be(new Str("World"));
        result[2].Should().Be(new Str("Test"));
    }

    [Fact]
    public void Split_WithSeparator_SplitsCorrectly()
    {
        var s = new Str("a,b,c,d");
        var result = s.Split(new Str(","));
        Len(result).Should().Be(4);
        result[0].Should().Be(new Str("a"));
        result[1].Should().Be(new Str("b"));
        result[2].Should().Be(new Str("c"));
        result[3].Should().Be(new Str("d"));
    }

    [Fact]
    public void Split_WithMaxSplit_LimitsSplits()
    {
        var s = new Str("a,b,c,d");
        var result = s.Split(new Str(","), maxsplit: 2);
        Len(result).Should().Be(3);
        result[0].Should().Be(new Str("a"));
        result[1].Should().Be(new Str("b"));
        result[2].Should().Be(new Str("c,d"));
    }

    [Fact]
    public void Join_ConcatenatesStrings()
    {
        var s = new Str(", ");
        var items = new List<Str> { new Str("a"), new Str("b"), new Str("c") };
        var result = s.Join(items);
        result.Should().Be(new Str("a, b, c"));
    }

    [Fact]
    public void Strip_RemovesWhitespace()
    {
        var s = new Str("  hello  ");
        var result = s.Strip();
        result.Should().Be(new Str("hello"));
    }

    [Fact]
    public void Strip_WithChars_RemovesSpecifiedChars()
    {
        var s = new Str("xxxhelloxxx");
        var result = s.Strip(new Str("x"));
        result.Should().Be(new Str("hello"));
    }

    [Fact]
    public void LStrip_RemovesLeadingWhitespace()
    {
        var s = new Str("  hello  ");
        var result = s.LStrip();
        result.Should().Be(new Str("hello  "));
    }

    [Fact]
    public void RStrip_RemovesTrailingWhitespace()
    {
        var s = new Str("  hello  ");
        var result = s.RStrip();
        result.Should().Be(new Str("  hello"));
    }

    [Fact]
    public void Replace_ReplacesAllOccurrences()
    {
        var s = new Str("hello hello hello");
        var result = s.Replace(new Str("hello"), new Str("hi"));
        result.Should().Be(new Str("hi hi hi"));
    }

    [Fact]
    public void Replace_WithCount_ReplacesLimitedOccurrences()
    {
        var s = new Str("hello hello hello");
        var result = s.Replace(new Str("hello"), new Str("hi"), count: 2);
        result.Should().Be(new Str("hi hi hello"));
    }

    [Fact]
    public void IsAlpha_ReturnsTrueForAlphabetic()
    {
        new Str("abc").IsAlpha().Should().BeTrue();
        new Str("abc123").IsAlpha().Should().BeFalse();
        new Str("").IsAlpha().Should().BeFalse();
    }

    [Fact]
    public void IsDigit_ReturnsTrueForDigits()
    {
        new Str("123").IsDigit().Should().BeTrue();
        new Str("abc123").IsDigit().Should().BeFalse();
        new Str("").IsDigit().Should().BeFalse();
    }

    [Fact]
    public void IsAlnum_ReturnsTrueForAlphanumeric()
    {
        new Str("abc123").IsAlnum().Should().BeTrue();
        new Str("abc 123").IsAlnum().Should().BeFalse();
        new Str("").IsAlnum().Should().BeFalse();
    }

    [Fact]
    public void IsSpace_ReturnsTrueForWhitespace()
    {
        new Str("   ").IsSpace().Should().BeTrue();
        new Str(" a ").IsSpace().Should().BeFalse();
        new Str("").IsSpace().Should().BeFalse();
    }

    [Fact]
    public void Lower_ConvertsToLowercase()
    {
        var s = new Str("HELLO");
        var result = s.Lower();
        result.Should().Be(new Str("hello"));
    }

    [Fact]
    public void Upper_ConvertsToUppercase()
    {
        var s = new Str("hello");
        var result = s.Upper();
        result.Should().Be(new Str("HELLO"));
    }

    [Fact]
    public void Capitalize_CapitalizesFirstCharacter()
    {
        var s = new Str("hello world");
        var result = s.Capitalize();
        result.Should().Be(new Str("Hello world"));
    }

    [Fact]
    public void Iter_IteratesOverCharacters()
    {
        var s = new Str("abc");
        var iterator = s.__Iter__();

        iterator.__Next__().Should().Be(new Str("a"));
        iterator.__Next__().Should().Be(new Str("b"));
        iterator.__Next__().Should().Be(new Str("c"));

        Assert.Throws<StopIteration>(() => iterator.__Next__());
    }

    [Fact]
    public void Find_FindsSubstringAtStart()
    {
        var s = new Str("hello world");
        var result = s.Find(new Str("hello"));
        result.Should().Be(0);
    }

    [Fact]
    public void Find_FindsSubstringInMiddle()
    {
        var s = new Str("hello world");
        var result = s.Find(new Str("world"));
        result.Should().Be(6);
    }

    [Fact]
    public void Find_ReturnsMinusOneWhenNotFound()
    {
        var s = new Str("hello world");
        var result = s.Find(new Str("xyz"));
        result.Should().Be(-1);
    }

    [Fact]
    public void Find_WithStartParameter()
    {
        var s = new Str("hello hello");
        var result = s.Find(new Str("hello"), start: 1);
        result.Should().Be(6);
    }

    [Fact]
    public void Find_WithEndParameter()
    {
        var s = new Str("hello world");
        var result = s.Find(new Str("world"), start: 0, end: 5);
        result.Should().Be(-1);
    }

    [Fact]
    public void Index_FindsSubstring()
    {
        var s = new Str("hello world");
        var result = s.Index(new Str("world"));
        result.Should().Be(6);
    }

    [Fact]
    public void Index_ThrowsWhenNotFound()
    {
        var s = new Str("hello world");
        Assert.Throws<ValueError>(() => s.Index(new Str("xyz")));
    }

    [Fact]
    public void StartsWith_ReturnsTrueForPrefix()
    {
        var s = new Str("hello world");
        s.StartsWith(new Str("hello")).Should().BeTrue();
    }

    [Fact]
    public void StartsWith_ReturnsFalseForNonPrefix()
    {
        var s = new Str("hello world");
        s.StartsWith(new Str("world")).Should().BeFalse();
    }

    [Fact]
    public void StartsWith_WithStartParameter()
    {
        var s = new Str("hello world");
        s.StartsWith(new Str("world"), start: 6).Should().BeTrue();
    }

    [Fact]
    public void StartsWith_WithEndParameter()
    {
        var s = new Str("hello world");
        s.StartsWith(new Str("hello"), start: 0, end: 5).Should().BeTrue();
    }

    [Fact]
    public void EndsWith_ReturnsTrueForSuffix()
    {
        var s = new Str("hello world");
        s.EndsWith(new Str("world")).Should().BeTrue();
    }

    [Fact]
    public void EndsWith_ReturnsFalseForNonSuffix()
    {
        var s = new Str("hello world");
        s.EndsWith(new Str("hello")).Should().BeFalse();
    }

    [Fact]
    public void EndsWith_WithStartParameter()
    {
        var s = new Str("hello world");
        s.EndsWith(new Str("hello"), start: 0, end: 5).Should().BeTrue();
    }

    [Fact]
    public void EndsWith_WithEndParameter()
    {
        var s = new Str("hello world");
        s.EndsWith(new Str("world"), start: 6).Should().BeTrue();
    }

    [Fact]
    public void Title_ConvertsToTitleCase()
    {
        var s = new Str("hello world");
        var result = s.Title();
        result.Should().Be(new Str("Hello World"));
    }

    [Fact]
    public void Title_HandlesMultipleWords()
    {
        var s = new Str("the quick brown fox");
        var result = s.Title();
        result.Should().Be(new Str("The Quick Brown Fox"));
    }

    [Fact]
    public void Title_HandlesMixedCase()
    {
        var s = new Str("hELLo WoRLd");
        var result = s.Title();
        result.Should().Be(new Str("Hello World"));
    }

    [Fact]
    public void Title_HandlesEmptyString()
    {
        var s = new Str("");
        var result = s.Title();
        result.Should().Be(new Str(""));
    }

    [Fact]
    public void Count_CountsOccurrences()
    {
        var s = new Str("hello hello hello");
        var count = s.Count(new Str("hello"));
        count.Should().Be(3);
    }

    [Fact]
    public void Count_NonOverlapping()
    {
        var s = new Str("aaaa");
        var count = s.Count(new Str("aa"));
        count.Should().Be(2); // Non-overlapping
    }

    [Fact]
    public void Count_WithStartAndEnd()
    {
        var s = new Str("hello hello hello");
        var count = s.Count(new Str("hello"), start: 6, end: 17);
        count.Should().Be(2);
    }

    [Fact]
    public void Count_NotFound()
    {
        var s = new Str("hello world");
        var count = s.Count(new Str("xyz"));
        count.Should().Be(0);
    }

    [Fact]
    public void Center_PadsString()
    {
        var s = new Str("hello");
        var result = s.Center(11);
        result.Should().Be(new Str("   hello   "));
    }

    [Fact]
    public void Center_WithCustomFillChar()
    {
        var s = new Str("hello");
        var result = s.Center(11, new Str("*"));
        result.Should().Be(new Str("***hello***"));
    }

    [Fact]
    public void Center_NoNeedToPad()
    {
        var s = new Str("hello");
        var result = s.Center(3);
        result.Should().Be(new Str("hello"));
    }

    [Fact]
    public void CaseFold_LowersCaseInsensitive()
    {
        var s = new Str("HELLO World");
        var result = s.CaseFold();
        result.Should().Be(new Str("hello world"));
    }

    [Fact]
    public void IsLower_ReturnsTrueForLowercase()
    {
        new Str("hello").IsLower().Should().BeTrue();
        new Str("Hello").IsLower().Should().BeFalse();
        new Str("HELLO").IsLower().Should().BeFalse();
        new Str("123").IsLower().Should().BeFalse();
        new Str("").IsLower().Should().BeFalse();
    }

    [Fact]
    public void IsUpper_ReturnsTrueForUppercase()
    {
        new Str("HELLO").IsUpper().Should().BeTrue();
        new Str("Hello").IsUpper().Should().BeFalse();
        new Str("hello").IsUpper().Should().BeFalse();
        new Str("123").IsUpper().Should().BeFalse();
        new Str("").IsUpper().Should().BeFalse();
    }

    [Fact]
    public void IsTitle_ReturnsTrueForTitleCase()
    {
        new Str("Hello World").IsTitle().Should().BeTrue();
        new Str("Hello world").IsTitle().Should().BeFalse();
        new Str("HELLO WORLD").IsTitle().Should().BeFalse();
        new Str("hello world").IsTitle().Should().BeFalse();
        new Str("").IsTitle().Should().BeFalse();
    }
}
