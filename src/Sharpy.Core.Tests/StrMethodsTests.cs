using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class StrMethods_Tests
{
    #region LJust Tests

    [Fact]
    public void LJust_WithWidthLargerThanString_PadsWithSpaces()
    {
        // Arrange
        var str = new Str("hello");

        // Act
        var result = str.LJust(10);

        // Assert
        ((string)result).Should().Be("hello     ");
        result.__Len__().Should().Be(10);
    }

    [Fact]
    public void LJust_WithWidthSmallerThanString_ReturnsOriginal()
    {
        // Arrange
        var str = new Str("hello");

        // Act
        var result = str.LJust(3);

        // Assert
        ((string)result).Should().Be("hello");
    }

    [Fact]
    public void LJust_WithCustomFillChar_PadsCorrectly()
    {
        // Arrange
        var str = new Str("hello");

        // Act
        var result = str.LJust(10, "*");

        // Assert
        ((string)result).Should().Be("hello*****");
    }

    [Fact]
    public void LJust_WithMultiCharFillChar_ThrowsTypeError()
    {
        // Arrange
        var str = new Str("hello");

        // Act & Assert
        str.Invoking(s => s.LJust(10, "ab"))
            .Should().Throw<TypeError>()
            .WithMessage("*one character*");
    }

    #endregion

    #region RJust Tests

    [Fact]
    public void RJust_WithWidthLargerThanString_PadsWithSpaces()
    {
        // Arrange
        var str = new Str("hello");

        // Act
        var result = str.RJust(10);

        // Assert
        ((string)result).Should().Be("     hello");
        result.__Len__().Should().Be(10);
    }

    [Fact]
    public void RJust_WithCustomFillChar_PadsCorrectly()
    {
        // Arrange
        var str = new Str("hello");

        // Act
        var result = str.RJust(10, "0");

        // Assert
        ((string)result).Should().Be("00000hello");
    }

    #endregion

    #region ZFill Tests

    [Fact]
    public void ZFill_WithPositiveNumber_PadsWithZeros()
    {
        // Arrange
        var str = new Str("42");

        // Act
        var result = str.ZFill(5);

        // Assert
        ((string)result).Should().Be("00042");
    }

    [Fact]
    public void ZFill_WithNegativeNumber_PadsAfterSign()
    {
        // Arrange
        var str = new Str("-42");

        // Act
        var result = str.ZFill(5);

        // Assert
        ((string)result).Should().Be("-0042");
    }

    [Fact]
    public void ZFill_WithPlusSign_PadsAfterSign()
    {
        // Arrange
        var str = new Str("+42");

        // Act
        var result = str.ZFill(5);

        // Assert
        ((string)result).Should().Be("+0042");
    }

    [Fact]
    public void ZFill_WithWidthSmallerThanString_ReturnsOriginal()
    {
        // Arrange
        var str = new Str("12345");

        // Act
        var result = str.ZFill(3);

        // Assert
        ((string)result).Should().Be("12345");
    }

    #endregion

    #region SwapCase Tests

    [Fact]
    public void SwapCase_WithMixedCase_SwapsCorrectly()
    {
        // Arrange
        var str = new Str("Hello World");

        // Act
        var result = str.SwapCase();

        // Assert
        ((string)result).Should().Be("hELLO wORLD");
    }

    [Fact]
    public void SwapCase_WithAllUpperCase_ReturnsLowerCase()
    {
        // Arrange
        var str = new Str("HELLO");

        // Act
        var result = str.SwapCase();

        // Assert
        ((string)result).Should().Be("hello");
    }

    [Fact]
    public void SwapCase_WithNumbers_RemainsUnchanged()
    {
        // Arrange
        var str = new Str("123ABC");

        // Act
        var result = str.SwapCase();

        // Assert
        ((string)result).Should().Be("123abc");
    }

    [Fact]
    public void SwapCase_WithEmptyString_ReturnsEmpty()
    {
        // Arrange
        var str = new Str("");

        // Act
        var result = str.SwapCase();

        // Assert
        ((string)result).Should().Be("");
    }

    #endregion

    #region RFind Tests

    [Fact]
    public void RFind_FindsSubstringFromRight()
    {
        // Arrange
        var str = new Str("hello hello");

        // Act
        var result = str.RFind("hello");

        // Assert
        result.Should().Be(6);
    }

    [Fact]
    public void RFind_WithNotFoundSubstring_ReturnsMinusOne()
    {
        // Arrange
        var str = new Str("hello");

        // Act
        var result = str.RFind("world");

        // Assert
        result.Should().Be(-1);
    }

    [Fact]
    public void RFind_WithStartEnd_SearchesInRange()
    {
        // Arrange
        var str = new Str("hello hello");

        // Act
        var result = str.RFind("hello", 0, 5);

        // Assert
        result.Should().Be(0); // Only finds first occurrence
    }

    #endregion

    #region RIndex Tests

    [Fact]
    public void RIndex_FindsSubstringFromRight()
    {
        // Arrange
        var str = new Str("hello hello");

        // Act
        var result = str.RIndex("hello");

        // Assert
        result.Should().Be(6);
    }

    [Fact]
    public void RIndex_WithNotFoundSubstring_ThrowsValueError()
    {
        // Arrange
        var str = new Str("hello");

        // Act & Assert
        str.Invoking(s => s.RIndex("world"))
            .Should().Throw<ValueError>()
            .WithMessage("*not found*");
    }

    #endregion

    #region RSplit Tests

    [Fact]
    public void RSplit_WithNoMaxSplit_SplitsAll()
    {
        // Arrange
        var str = new Str("a b c d");

        // Act
        var result = str.RSplit();

        // Assert
        result.Should().HaveCount(4);
        ((string)result[0]).Should().Be("a");
        ((string)result[3]).Should().Be("d");
    }

    [Fact]
    public void RSplit_WithMaxSplit_SplitsFromRight()
    {
        // Arrange
        var str = new Str("a b c d");

        // Act
        var result = str.RSplit(null, 2);

        // Assert
        result.Should().HaveCount(3);
        ((string)result[0]).Should().Be("a b"); // First two kept together
        ((string)result[1]).Should().Be("c");
        ((string)result[2]).Should().Be("d");
    }

    [Fact]
    public void RSplit_WithSeparator_SplitsCorrectly()
    {
        // Arrange
        var str = new Str("a,b,c,d");

        // Act
        var result = str.RSplit(",", 2);

        // Assert
        result.Should().HaveCount(3);
        ((string)result[0]).Should().Be("a,b");
        ((string)result[1]).Should().Be("c");
        ((string)result[2]).Should().Be("d");
    }

    #endregion

    #region Partition Tests

    [Fact]
    public void Partition_WithFoundSeparator_ReturnsTuple()
    {
        // Arrange
        var str = new Str("hello world");

        // Act
        var (before, sep, after) = str.Partition(" ");

        // Assert
        ((string)before).Should().Be("hello");
        ((string)sep).Should().Be(" ");
        ((string)after).Should().Be("world");
    }

    [Fact]
    public void Partition_WithNotFoundSeparator_ReturnsOriginalAndEmpty()
    {
        // Arrange
        var str = new Str("hello");

        // Act
        var (before, sep, after) = str.Partition(" ");

        // Assert
        ((string)before).Should().Be("hello");
        ((string)sep).Should().Be("");
        ((string)after).Should().Be("");
    }

    [Fact]
    public void Partition_WithEmptySeparator_ThrowsValueError()
    {
        // Arrange
        var str = new Str("hello");

        // Act & Assert
        str.Invoking(s => s.Partition(""))
            .Should().Throw<ValueError>()
            .WithMessage("*empty separator*");
    }

    #endregion

    #region RPartition Tests

    [Fact]
    public void RPartition_WithFoundSeparator_ReturnsTuple()
    {
        // Arrange
        var str = new Str("hello world hello");

        // Act
        var (before, sep, after) = str.RPartition(" ");

        // Assert
        ((string)before).Should().Be("hello world");
        ((string)sep).Should().Be(" ");
        ((string)after).Should().Be("hello");
    }

    [Fact]
    public void RPartition_WithNotFoundSeparator_ReturnsEmptyAndOriginal()
    {
        // Arrange
        var str = new Str("hello");

        // Act
        var (before, sep, after) = str.RPartition(" ");

        // Assert
        ((string)before).Should().Be("");
        ((string)sep).Should().Be("");
        ((string)after).Should().Be("hello");
    }

    #endregion

    #region IsAscii Tests

    [Fact]
    public void IsAscii_WithAsciiString_ReturnsTrue()
    {
        // Arrange
        var str = new Str("hello123");

        // Act
        var result = str.IsAscii();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsAscii_WithUnicodeString_ReturnsFalse()
    {
        // Arrange
        var str = new Str("hello☺");

        // Act
        var result = str.IsAscii();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsAscii_WithEmptyString_ReturnsTrue()
    {
        // Arrange
        var str = new Str("");

        // Act
        var result = str.IsAscii();

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region IsDecimal Tests

    [Fact]
    public void IsDecimal_WithDecimalDigits_ReturnsTrue()
    {
        // Arrange
        var str = new Str("12345");

        // Act
        var result = str.IsDecimal();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsDecimal_WithLetters_ReturnsFalse()
    {
        // Arrange
        var str = new Str("123abc");

        // Act
        var result = str.IsDecimal();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsDecimal_WithEmptyString_ReturnsFalse()
    {
        // Arrange
        var str = new Str("");

        // Act
        var result = str.IsDecimal();

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region IsIdentifier Tests

    [Fact]
    public void IsIdentifier_WithValidIdentifier_ReturnsTrue()
    {
        // Arrange
        var str = new Str("my_variable_123");

        // Act
        var result = str.IsIdentifier();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsIdentifier_StartingWithNumber_ReturnsFalse()
    {
        // Arrange
        var str = new Str("123variable");

        // Act
        var result = str.IsIdentifier();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsIdentifier_WithSpaces_ReturnsFalse()
    {
        // Arrange
        var str = new Str("my variable");

        // Act
        var result = str.IsIdentifier();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsIdentifier_WithUnderscore_ReturnsTrue()
    {
        // Arrange
        var str = new Str("_private");

        // Act
        var result = str.IsIdentifier();

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region IsNumeric Tests

    [Fact]
    public void IsNumeric_WithNumbers_ReturnsTrue()
    {
        // Arrange
        var str = new Str("12345");

        // Act
        var result = str.IsNumeric();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsNumeric_WithLetters_ReturnsFalse()
    {
        // Arrange
        var str = new Str("abc");

        // Act
        var result = str.IsNumeric();

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region IsPrintable Tests

    [Fact]
    public void IsPrintable_WithPrintableChars_ReturnsTrue()
    {
        // Arrange
        var str = new Str("hello world 123");

        // Act
        var result = str.IsPrintable();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsPrintable_WithControlChars_ReturnsFalse()
    {
        // Arrange
        var str = new Str("hello\nworld");

        // Act
        var result = str.IsPrintable();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsPrintable_WithEmptyString_ReturnsTrue()
    {
        // Arrange
        var str = new Str("");

        // Act
        var result = str.IsPrintable();

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region SplitLines Tests

    [Fact]
    public void SplitLines_WithNewlines_SplitsCorrectly()
    {
        // Arrange
        var str = new Str("hello\nworld\ntest");

        // Act
        var result = str.SplitLines();

        // Assert
        result.Should().HaveCount(3);
        ((string)result[0]).Should().Be("hello");
        ((string)result[1]).Should().Be("world");
        ((string)result[2]).Should().Be("test");
    }

    [Fact]
    public void SplitLines_WithKeepEnds_KeepsNewlines()
    {
        // Arrange
        var str = new Str("hello\nworld\n");

        // Act
        var result = str.SplitLines(true);

        // Assert
        result.Should().HaveCount(2);
        ((string)result[0]).Should().Be("hello\n");
        ((string)result[1]).Should().Be("world\n");
    }

    [Fact]
    public void SplitLines_WithCRLF_HandlesCorrectly()
    {
        // Arrange
        var str = new Str("hello\r\nworld");

        // Act
        var result = str.SplitLines();

        // Assert
        result.Should().HaveCount(2);
        ((string)result[0]).Should().Be("hello");
        ((string)result[1]).Should().Be("world");
    }

    [Fact]
    public void SplitLines_WithCRLFKeepEnds_KeepsBoth()
    {
        // Arrange
        var str = new Str("hello\r\nworld");

        // Act
        var result = str.SplitLines(true);

        // Assert
        result.Should().HaveCount(2);
        ((string)result[0]).Should().Be("hello\r\n");
        ((string)result[1]).Should().Be("world");
    }

    [Fact]
    public void SplitLines_WithEmptyString_ReturnsEmptyList()
    {
        // Arrange
        var str = new Str("");

        // Act
        var result = str.SplitLines();

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region RemovePrefix Tests

    [Fact]
    public void RemovePrefix_WithMatchingPrefix_RemovesIt()
    {
        // Arrange
        var str = new Str("TestString");

        // Act
        var result = str.RemovePrefix("Test");

        // Assert
        ((string)result).Should().Be("String");
    }

    [Fact]
    public void RemovePrefix_WithoutMatchingPrefix_ReturnsOriginal()
    {
        // Arrange
        var str = new Str("TestString");

        // Act
        var result = str.RemovePrefix("Hello");

        // Assert
        ((string)result).Should().Be("TestString");
    }

    [Fact]
    public void RemovePrefix_WithEmptyPrefix_ReturnsOriginal()
    {
        // Arrange
        var str = new Str("TestString");

        // Act
        var result = str.RemovePrefix("");

        // Assert
        ((string)result).Should().Be("TestString");
    }

    #endregion

    #region RemoveSuffix Tests

    [Fact]
    public void RemoveSuffix_WithMatchingSuffix_RemovesIt()
    {
        // Arrange
        var str = new Str("TestString");

        // Act
        var result = str.RemoveSuffix("String");

        // Assert
        ((string)result).Should().Be("Test");
    }

    [Fact]
    public void RemoveSuffix_WithoutMatchingSuffix_ReturnsOriginal()
    {
        // Arrange
        var str = new Str("TestString");

        // Act
        var result = str.RemoveSuffix("World");

        // Assert
        ((string)result).Should().Be("TestString");
    }

    #endregion

    #region MakeTrans Tests

    [Fact]
    public void MakeTrans_WithTwoStrings_CreatesTable()
    {
        // Arrange & Act
        var table = Str.MakeTrans("abc", "123");

        // Assert
        table.__Len__().Should().Be(3);
        ((string)table[(uint)'a']).Should().Be("1");
        ((string)table[(uint)'b']).Should().Be("2");
        ((string)table[(uint)'c']).Should().Be("3");
    }

    [Fact]
    public void MakeTrans_WithUnequalLengths_ThrowsValueError()
    {
        // Act & Assert
        var act = () => Str.MakeTrans("abc", "12");

        act.Should().Throw<ValueError>()
            .WithMessage("*equal length*");
    }

    [Fact]
    public void MakeTrans_WithThreeStrings_CreatesTableWithDeletions()
    {
        // Arrange & Act
        var table = Str.MakeTrans("abc", "123", "d");

        // Assert
        table.__Len__().Should().Be(4);
        table[(uint)'d'].Should().BeNull();
    }

    #endregion

    #region Translate Tests

    [Fact]
    public void Translate_WithSimpleTable_TranslatesCorrectly()
    {
        // Arrange
        var str = new Str("abc");
        var table = Str.MakeTrans("abc", "123");

        // Act
        var result = str.Translate(table);

        // Assert
        ((string)result).Should().Be("123");
    }

    [Fact]
    public void Translate_WithDeletions_RemovesChars()
    {
        // Arrange
        var str = new Str("abcd");
        var table = Str.MakeTrans("ab", "12", "d");

        // Act
        var result = str.Translate(table);

        // Assert
        ((string)result).Should().Be("12c"); // 'd' removed
    }

    [Fact]
    public void Translate_WithUnmappedChars_KeepsThem()
    {
        // Arrange
        var str = new Str("abcxyz");
        var table = Str.MakeTrans("abc", "123");

        // Act
        var result = str.Translate(table);

        // Assert
        ((string)result).Should().Be("123xyz");
    }

    #endregion

    #region ExpandTabs Tests

    [Fact]
    public void ExpandTabs_WithDefaultTabSize_ExpandsCorrectly()
    {
        // Arrange
        var str = new Str("hello\tworld");

        // Act
        var result = str.ExpandTabs();

        // Assert
        ((string)result).Should().Be("hello   world"); // 8 - 5 = 3 spaces
    }

    [Fact]
    public void ExpandTabs_WithCustomTabSize_ExpandsCorrectly()
    {
        // Arrange
        var str = new Str("hello\tworld");

        // Act
        var result = str.ExpandTabs(4);

        // Assert
        ((string)result).Should().NotContain("\t");
    }

    [Fact]
    public void ExpandTabs_WithNoTabs_ReturnsOriginal()
    {
        // Arrange
        var str = new Str("hello world");

        // Act
        var result = str.ExpandTabs();

        // Assert
        ((string)result).Should().Be("hello world");
    }

    [Fact]
    public void ExpandTabs_WithNewlines_ResetsColumn()
    {
        // Arrange
        var str = new Str("hello\n\tworld");

        // Act
        var result = str.ExpandTabs(8);

        // Assert
        ((string)result).Should().Be("hello\n        world");
    }

    [Fact]
    public void ExpandTabs_WithNegativeTabSize_ThrowsValueError()
    {
        // Arrange
        var str = new Str("hello\tworld");

        // Act & Assert
        str.Invoking(s => s.ExpandTabs(-1))
            .Should().Throw<ValueError>()
            .WithMessage("*non-negative*");
    }

    #endregion

    #region Format Tests

    [Fact]
    public void Format_ThrowsNotImplementedException()
    {
        // Arrange
        var str = new Str("Hello {0}");

        // Act & Assert
        str.Invoking(s => s.Format("world"))
            .Should().Throw<NotImplementedException>()
            .WithMessage("*v0.6*");
    }

    [Fact]
    public void FormatMap_ThrowsNotImplementedException()
    {
        // Arrange
        var str = new Str("Hello {name}");
        var mapping = new Dict<Str, Str>();
        mapping["name"] = "world";

        // Act & Assert
        str.Invoking(s => s.FormatMap(mapping))
            .Should().Throw<NotImplementedException>()
            .WithMessage("*v0.6*");
    }

    #endregion

    #region Negative Tests

    [Fact]
    public void RJust_WithNullFillChar_ThrowsArgumentNullException()
    {
        // Arrange
        var str = new Str("test");

        // Act & Assert
        str.Invoking(s => s.RJust(10, null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ZFill_WithZeroWidth_ReturnsOriginalString()
    {
        // Arrange
        var str = new Str("42");

        // Act
        var result = str.ZFill(0u);

        // Assert
        ((string)result).Should().Be("42");
    }

    [Fact]
    public void RFind_WithNullSubstring_ThrowsArgumentNullException()
    {
        // Arrange
        var str = new Str("hello world");

        // Act & Assert
        str.Invoking(s => s.RFind(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RIndex_WithNonexistentSubstring_ThrowsValueError()
    {
        // Arrange
        var str = new Str("hello");

        // Act & Assert
        str.Invoking(s => s.RIndex("xyz"))
            .Should().Throw<ValueError>()
            .WithMessage("*not found*");
    }

    [Fact]
    public void RSplit_WithNullSeparator_SplitsOnWhitespace()
    {
        // Arrange
        var str = new Str("hello  world  test");

        // Act
        var result = str.RSplit(null, 1);

        // Assert
        result.__Len__().Should().Be(2);
        ((string)result[0]).Should().Be("hello  world");
        ((string)result[1]).Should().Be("test");
    }

    [Fact]
    public void RSplit_WithEmptyString_ThrowsValueError()
    {
        // Arrange
        var str = new Str("hello");

        // Act & Assert
        str.Invoking(s => s.RSplit("", 1))
            .Should().Throw<ValueError>()
            .WithMessage("*empty separator*");
    }

    [Fact]
    public void Partition_WithNullSeparator_ThrowsArgumentNullException()
    {
        // Arrange
        var str = new Str("hello world");

        // Act & Assert
        str.Invoking(s => s.Partition(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RPartition_WithNullSeparator_ThrowsArgumentNullException()
    {
        // Arrange
        var str = new Str("hello world hello");

        // Act & Assert
        str.Invoking(s => s.RPartition(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RPartition_WithEmptySeparator_ThrowsValueError()
    {
        // Arrange
        var str = new Str("hello");

        // Act & Assert
        str.Invoking(s => s.RPartition(""))
            .Should().Throw<ValueError>()
            .WithMessage("*empty separator*");
    }

    [Fact]
    public void IsIdentifier_WithEmptyString_ReturnsFalse()
    {
        // Arrange
        var str = new Str("");

        // Act
        var result = str.IsIdentifier();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsNumeric_WithEmptyString_ReturnsFalse()
    {
        // Arrange
        var str = new Str("");

        // Act
        var result = str.IsNumeric();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void RemovePrefix_WithNullPrefix_ThrowsArgumentNullException()
    {
        // Arrange
        var str = new Str("hello");

        // Act & Assert
        str.Invoking(s => s.RemovePrefix(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RemoveSuffix_WithNullSuffix_ThrowsArgumentNullException()
    {
        // Arrange
        var str = new Str("hello");

        // Act & Assert
        str.Invoking(s => s.RemoveSuffix(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MakeTrans_WithMismatchedLengths_ThrowsValueError()
    {
        // Arrange & Act & Assert
        var invoking = () => Str.MakeTrans("abc", "xy");

        invoking.Should().Throw<ValueError>()
            .WithMessage("*equal length*");
    }

    [Fact]
    public void MakeTrans_WithNullX_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        var invoking = () => Str.MakeTrans(null!, "abc");

        invoking.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MakeTrans_WithNullY_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        var invoking = () => Str.MakeTrans("abc", null!);

        invoking.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MakeTrans_WithNullMapping_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        var invoking = () => Str.MakeTrans((IReadOnlyDictionary<Str, Str?>)null!);

        invoking.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Translate_WithNullTable_ThrowsArgumentNullException()
    {
        // Arrange
        var str = new Str("hello");

        // Act & Assert
        str.Invoking(s => s.Translate(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ExpandTabs_WithZeroTabSize_UsesEmptyString()
    {
        // Arrange
        var str = new Str("hello\tworld");

        // Act
        var result = str.ExpandTabs(0);

        // Assert
        ((string)result).Should().Be("helloworld");
    }

    [Fact]
    public void SwapCase_WithEmptyString_ReturnsEmptyString()
    {
        // Arrange
        var str = new Str("");

        // Act
        var result = str.SwapCase();

        // Assert
        ((string)result).Should().Be("");
    }

    [Fact]
    public void LJust_WithZeroWidth_ReturnsOriginalString()
    {
        // Arrange
        var str = new Str("hello");

        // Act
        var result = str.LJust(0);

        // Assert
        ((string)result).Should().Be("hello");
    }

    [Fact]
    public void RJust_WithEmptyString_PadsCorrectly()
    {
        // Arrange
        var str = new Str("");

        // Act
        var result = str.RJust(5, "*");

        // Assert
        ((string)result).Should().Be("*****");
    }

    [Fact]
    public void ZFill_WithEmptyString_PadsCorrectly()
    {
        // Arrange
        var str = new Str("");

        // Act
        var result = str.ZFill(5);

        // Assert
        ((string)result).Should().Be("00000");
    }

    [Fact]
    public void RFind_WithEmptySubstring_ThrowsValueError()
    {
        // Arrange
        var str = new Str("hello");

        // Act & Assert
        str.Invoking(s => s.RFind(""))
            .Should().Throw<ValueError>()
            .WithMessage("*empty*");
    }

    [Fact]
    public void RIndex_WithEmptySubstring_ThrowsValueError()
    {
        // Arrange
        var str = new Str("hello");

        // Act & Assert
        str.Invoking(s => s.RIndex(""))
            .Should().Throw<ValueError>()
            .WithMessage("*empty*");
    }

    [Fact]
    public void IsIdentifier_WithNumberStart_ReturnsFalse()
    {
        // Arrange
        var str = new Str("123abc");

        // Act
        var result = str.IsIdentifier();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsIdentifier_WithSpecialChars_ReturnsFalse()
    {
        // Arrange
        var str = new Str("hello-world");

        // Act
        var result = str.IsIdentifier();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void SplitLines_WithOnlyLineBreaks_ReturnsEmptyStrings()
    {
        // Arrange
        var str = new Str("\n\n\n");

        // Act
        var result = str.SplitLines();

        // Assert
        result.__Len__().Should().Be(3);
        foreach (var line in result)
        {
            ((string)line).Should().Be("");
        }
    }

    [Fact]
    public void Partition_WithNonexistentSeparator_ReturnsStringAndEmptyParts()
    {
        // Arrange
        var str = new Str("hello world");

        // Act
        var result = str.Partition("xyz");

        // Assert
        ((string)result.Item1).Should().Be("hello world");
        ((string)result.Item2).Should().Be("");
        ((string)result.Item3).Should().Be("");
    }

    [Fact]
    public void RPartition_WithNonexistentSeparator_ReturnsEmptyPartsAndString()
    {
        // Arrange
        var str = new Str("hello world");

        // Act
        var result = str.RPartition("xyz");

        // Assert
        ((string)result.Item1).Should().Be("");
        ((string)result.Item2).Should().Be("");
        ((string)result.Item3).Should().Be("hello world");
    }

    #endregion
}
