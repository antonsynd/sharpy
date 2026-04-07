using Xunit;
using FluentAssertions;
using System.Linq;

namespace Sharpy.Core.Tests;

/// <summary>
/// Tests for the <see cref="Sharpy.Str"/> readonly struct.
/// </summary>
public class StrStructTests
{
    #region Construction and Conversion

    [Fact]
    public void Constructor_NullString_CoalescesToEmpty()
    {
        var s = new Str((string)null!);
        ((string)s).Should().Be("");
    }

    [Fact]
    public void Constructor_EmptyString_IsEmpty()
    {
        var s = new Str("");
        ((string)s).Should().Be("");
    }

    [Fact]
    public void Constructor_NonEmpty_PreservesValue()
    {
        var s = new Str("hello");
        ((string)s).Should().Be("hello");
    }

    [Fact]
    public void ImplicitConversion_StringToStr()
    {
        Str s = "hello";
        ((string)s).Should().Be("hello");
    }

    [Fact]
    public void ImplicitConversion_StrToString()
    {
        Str s = new Str("hello");
        string result = s;
        result.Should().Be("hello");
    }

    [Fact]
    public void ToString_ReturnsUnderlyingValue()
    {
        var s = new Str("hello");
        s.ToString().Should().Be("hello");
    }

    #endregion

    #region Equality

    [Fact]
    public void Equals_SameContent_ReturnsTrue()
    {
        Str a = "hello";
        Str b = "hello";
        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentContent_ReturnsFalse()
    {
        Str a = "hello";
        Str b = "world";
        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void Equals_Object_String_ReturnsTrue()
    {
        Str s = "hello";
        s.Equals((object)"hello").Should().BeTrue();
    }

    [Fact]
    public void Equals_Object_Str_ReturnsTrue()
    {
        Str a = "hello";
        Str b = "hello";
        a.Equals((object)b).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_SameContent_SameHash()
    {
        Str a = "hello";
        Str b = "hello";
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void OperatorEquals_SameContent()
    {
        Str a = "hello";
        Str b = "hello";
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void OperatorNotEquals_DifferentContent()
    {
        Str a = "hello";
        Str b = "world";
        (a != b).Should().BeTrue();
    }

    #endregion

    #region Comparison

    [Fact]
    public void CompareTo_LessThan()
    {
        Str a = "abc";
        Str b = "abd";
        a.CompareTo(b).Should().BeNegative();
    }

    [Fact]
    public void OperatorLessThan()
    {
        Str a = "abc";
        Str b = "abd";
        (a < b).Should().BeTrue();
    }

    [Fact]
    public void OperatorGreaterThan()
    {
        Str a = "abd";
        Str b = "abc";
        (a > b).Should().BeTrue();
    }

    [Fact]
    public void OperatorLessOrEqual_Equal()
    {
        Str a = "abc";
        Str b = "abc";
        (a <= b).Should().BeTrue();
    }

    [Fact]
    public void OperatorGreaterOrEqual_Equal()
    {
        Str a = "abc";
        Str b = "abc";
        (a >= b).Should().BeTrue();
    }

    #endregion

    #region ISized

    [Fact]
    public void ISized_Count_ReturnsLength()
    {
        Str s = "hello";
        ((ISized)s).Count.Should().Be(5);
    }

    [Fact]
    public void ISized_EmptyString_ReturnsZero()
    {
        Str s = "";
        ((ISized)s).Count.Should().Be(0);
    }

    #endregion

    #region IBoolConvertible

    [Fact]
    public void IBoolConvertible_NonEmpty_IsTrue()
    {
        Str s = "hello";
        ((IBoolConvertible)s).IsTrue.Should().BeTrue();
    }

    [Fact]
    public void IBoolConvertible_Empty_IsFalse()
    {
        Str s = "";
        ((IBoolConvertible)s).IsTrue.Should().BeFalse();
    }

    #endregion

    #region Truthiness Operators

    [Fact]
    public void TruthOperator_NonEmpty_IsTrue()
    {
        Str s = "hello";
        if (s)
        {
            // Expected
        }
        else
        {
            Assert.Fail("Expected truthy");
        }
    }

    [Fact]
    public void TruthOperator_Empty_IsFalse()
    {
        Str s = "";
        if (s)
        {
            Assert.Fail("Expected falsy");
        }
    }

    #endregion

    #region Concatenation

    [Fact]
    public void OperatorPlus_Concatenates()
    {
        Str a = "hello";
        Str b = " world";
        Str result = a + b;
        ((string)result).Should().Be("hello world");
    }

    #endregion

    #region Repetition

    [Fact]
    public void OperatorMultiply_RepeatsString()
    {
        Str s = "ab";
        Str result = s * 3;
        ((string)result).Should().Be("ababab");
    }

    [Fact]
    public void OperatorMultiply_IntOnLeft()
    {
        Str s = "ab";
        Str result = 3 * s;
        ((string)result).Should().Be("ababab");
    }

    [Fact]
    public void OperatorMultiply_ZeroCount_ReturnsEmpty()
    {
        Str s = "ab";
        Str result = s * 0;
        ((string)result).Should().Be("");
    }

    [Fact]
    public void OperatorMultiply_NegativeCount_ReturnsEmpty()
    {
        Str s = "ab";
        Str result = s * -1;
        ((string)result).Should().Be("");
    }

    [Fact]
    public void OperatorMultiply_One_ReturnsSame()
    {
        Str s = "ab";
        Str result = s * 1;
        ((string)result).Should().Be("ab");
    }

    #endregion

    #region Indexing

    [Fact]
    public void Indexer_PositiveIndex()
    {
        Str s = "hello";
        ((string)s[0]).Should().Be("h");
        ((string)s[4]).Should().Be("o");
    }

    [Fact]
    public void Indexer_NegativeIndex()
    {
        Str s = "hello";
        ((string)s[-1]).Should().Be("o");
        ((string)s[-5]).Should().Be("h");
    }

    [Fact]
    public void Indexer_OutOfRange_ThrowsIndexError()
    {
        Str s = "hello";
        FluentActions.Invoking(() => { var _ = s[5]; }).Should().Throw<IndexError>();
        FluentActions.Invoking(() => { var _ = s[-6]; }).Should().Throw<IndexError>();
    }

    #endregion

    #region Slicing

    [Fact]
    public void Slice_BasicSlice()
    {
        Str s = "hello";
        ((string)s.Slice(1, 3, null)).Should().Be("el");
    }

    [Fact]
    public void Slice_NegativeIndices()
    {
        Str s = "hello";
        ((string)s.Slice(-3, null, null)).Should().Be("llo");
    }

    [Fact]
    public void Slice_Step()
    {
        Str s = "hello";
        ((string)s.Slice(null, null, 2)).Should().Be("hlo");
    }

    [Fact]
    public void Slice_ReverseStep()
    {
        Str s = "hello";
        ((string)s.Slice(null, null, -1)).Should().Be("olleh");
    }

    #endregion

    #region Iteration

    [Fact]
    public void Iteration_YieldsStrNotChar()
    {
        Str s = "abc";
        var items = s.ToList();
        items.Should().HaveCount(3);
        items[0].Should().BeOfType<Str>();
        ((string)items[0]).Should().Be("a");
        ((string)items[1]).Should().Be("b");
        ((string)items[2]).Should().Be("c");
    }

    [Fact]
    public void Iteration_EmptyString_YieldsNothing()
    {
        Str s = "";
        s.ToList().Should().BeEmpty();
    }

    #endregion

    #region Contains

    [Fact]
    public void Contains_SubstringPresent_ReturnsTrue()
    {
        Str s = "hello world";
        s.Contains((Str)"world").Should().BeTrue();
    }

    [Fact]
    public void Contains_SubstringAbsent_ReturnsFalse()
    {
        Str s = "hello world";
        s.Contains((Str)"xyz").Should().BeFalse();
    }

    [Fact]
    public void Contains_EmptySubstring_ReturnsTrue()
    {
        Str s = "hello";
        s.Contains((Str)"").Should().BeTrue();
    }

    #endregion

    #region Reverse Iteration

    [Fact]
    public void ReverseEnumerator_YieldsReversed()
    {
        Str s = "abc";
        var reversed = new System.Collections.Generic.List<Str>();
        var enumerator = ((IReverseEnumerable<Str>)s).GetReverseEnumerator();
        while (enumerator.MoveNext())
        {
            reversed.Add(enumerator.Current);
        }
        reversed.Should().HaveCount(3);
        ((string)reversed[0]).Should().Be("c");
        ((string)reversed[1]).Should().Be("b");
        ((string)reversed[2]).Should().Be("a");
    }

    #endregion

    #region Case Methods

    [Fact]
    public void Upper_ReturnsUppercase()
    {
        Str s = "hello";
        ((string)s.Upper()).Should().Be("HELLO");
    }

    [Fact]
    public void Lower_ReturnsLowercase()
    {
        Str s = "HELLO";
        ((string)s.Lower()).Should().Be("hello");
    }

    [Fact]
    public void Capitalize_FirstCharUpper()
    {
        Str s = "hello world";
        ((string)s.Capitalize()).Should().Be("Hello world");
    }

    [Fact]
    public void Title_TitlecasesWords()
    {
        Str s = "hello world";
        ((string)s.Title()).Should().Be("Hello World");
    }

    [Fact]
    public void Swapcase_SwapsCase()
    {
        Str s = "Hello World";
        ((string)s.Swapcase()).Should().Be("hELLO wORLD");
    }

    [Fact]
    public void Casefold_FoldsCase()
    {
        Str s = "Straße";
        ((string)s.Casefold()).Should().Be("strasse");
    }

    #endregion

    #region Strip Methods

    [Fact]
    public void Strip_RemovesWhitespace()
    {
        Str s = "  hello  ";
        ((string)s.Strip()).Should().Be("hello");
    }

    [Fact]
    public void Strip_WithChars()
    {
        Str s = "xxhelloxx";
        ((string)s.Strip((Str)"x")).Should().Be("hello");
    }

    [Fact]
    public void Lstrip_RemovesLeadingWhitespace()
    {
        Str s = "  hello";
        ((string)s.Lstrip()).Should().Be("hello");
    }

    [Fact]
    public void Rstrip_RemovesTrailingWhitespace()
    {
        Str s = "hello  ";
        ((string)s.Rstrip()).Should().Be("hello");
    }

    #endregion

    #region Justify Methods

    [Fact]
    public void Center_CentersString()
    {
        Str s = "hi";
        ((string)s.Center(10)).Should().Be("    hi    ");
    }

    [Fact]
    public void Center_WithFillchar()
    {
        Str s = "hi";
        ((string)s.Center(10, '-')).Should().Be("----hi----");
    }

    [Fact]
    public void Ljust_LeftJustifies()
    {
        Str s = "hi";
        ((string)s.Ljust(5)).Should().Be("hi   ");
    }

    [Fact]
    public void Rjust_RightJustifies()
    {
        Str s = "hi";
        ((string)s.Rjust(5)).Should().Be("   hi");
    }

    [Fact]
    public void Zfill_PadsWithZeros()
    {
        Str s = "42";
        ((string)s.Zfill(5)).Should().Be("00042");
    }

    [Fact]
    public void Zfill_PreservesSign()
    {
        Str s = "-42";
        ((string)s.Zfill(5)).Should().Be("-0042");
    }

    #endregion

    #region Prefix/Suffix Methods

    [Fact]
    public void Removeprefix_RemovesPrefix()
    {
        Str s = "HelloWorld";
        ((string)s.Removeprefix((Str)"Hello")).Should().Be("World");
    }

    [Fact]
    public void Removeprefix_NoMatch_ReturnsOriginal()
    {
        Str s = "HelloWorld";
        ((string)s.Removeprefix((Str)"Bye")).Should().Be("HelloWorld");
    }

    [Fact]
    public void Removesuffix_RemovesSuffix()
    {
        Str s = "HelloWorld";
        ((string)s.Removesuffix((Str)"World")).Should().Be("Hello");
    }

    #endregion

    #region Replace

    [Fact]
    public void Replace_AllOccurrences()
    {
        Str s = "hello world";
        ((string)s.Replace((Str)"world", (Str)"there")).Should().Be("hello there");
    }

    [Fact]
    public void Replace_WithCount()
    {
        Str s = "aaa";
        ((string)s.Replace((Str)"a", (Str)"b", 2)).Should().Be("bba");
    }

    [Fact]
    public void Replace_EmptyOld_InsertsBetweenChars()
    {
        Str s = "ab";
        ((string)s.Replace((Str)"", (Str)"-")).Should().Be("-a-b-");
    }

    #endregion

    #region Find/Rfind

    [Fact]
    public void Find_SubstringPresent_ReturnsIndex()
    {
        Str s = "hello";
        s.Find((Str)"lo").Should().Be(3);
    }

    [Fact]
    public void Find_SubstringAbsent_ReturnsMinusOne()
    {
        Str s = "hello";
        s.Find((Str)"xyz").Should().Be(-1);
    }

    [Fact]
    public void Rfind_FindsLastOccurrence()
    {
        Str s = "hello hello";
        s.Rfind((Str)"hello").Should().Be(6);
    }

    #endregion

    #region Index/Rindex

    [Fact]
    public void Index_SubstringPresent_ReturnsIndex()
    {
        Str s = "hello";
        s.Index((Str)"lo").Should().Be(3);
    }

    [Fact]
    public void Index_SubstringAbsent_ThrowsValueError()
    {
        Str s = "hello";
        FluentActions.Invoking(() => s.Index((Str)"xyz")).Should().Throw<ValueError>();
    }

    #endregion

    #region Count

    [Fact]
    public void Count_NonOverlapping()
    {
        Str s = "banana";
        s.Count((Str)"an").Should().Be(2);
    }

    [Fact]
    public void Count_EmptySubstring()
    {
        Str s = "hello";
        s.Count((Str)"").Should().Be(6); // len + 1
    }

    #endregion

    #region Startswith/Endswith

    [Fact]
    public void Startswith_True()
    {
        Str s = "hello";
        s.Startswith((Str)"he").Should().BeTrue();
    }

    [Fact]
    public void Endswith_True()
    {
        Str s = "hello";
        s.Endswith((Str)"lo").Should().BeTrue();
    }

    #endregion

    #region Predicates

    [Fact]
    public void Isdigit_AllDigits_True()
    {
        ((Str)"123").Isdigit().Should().BeTrue();
    }

    [Fact]
    public void Isdigit_WithDot_False()
    {
        ((Str)"12.3").Isdigit().Should().BeFalse();
    }

    [Fact]
    public void Isalpha_AllLetters_True()
    {
        ((Str)"hello").Isalpha().Should().BeTrue();
    }

    [Fact]
    public void Isalnum_MixedAlphaNum_True()
    {
        ((Str)"abc123").Isalnum().Should().BeTrue();
    }

    [Fact]
    public void Isspace_AllWhitespace_True()
    {
        ((Str)"  \t\n").Isspace().Should().BeTrue();
    }

    [Fact]
    public void Isupper_AllUpper_True()
    {
        ((Str)"HELLO").Isupper().Should().BeTrue();
    }

    [Fact]
    public void Islower_AllLower_True()
    {
        ((Str)"hello").Islower().Should().BeTrue();
    }

    [Fact]
    public void Istitle_TitleCase_True()
    {
        ((Str)"Hello World").Istitle().Should().BeTrue();
    }

    [Fact]
    public void Isnumeric_Digits_True()
    {
        ((Str)"123").Isnumeric().Should().BeTrue();
    }

    [Fact]
    public void Isdecimal_Digits_True()
    {
        ((Str)"123").Isdecimal().Should().BeTrue();
    }

    [Fact]
    public void Isidentifier_ValidId_True()
    {
        ((Str)"my_var").Isidentifier().Should().BeTrue();
    }

    [Fact]
    public void Isidentifier_StartsWithDigit_False()
    {
        ((Str)"1abc").Isidentifier().Should().BeFalse();
    }

    [Fact]
    public void Isprintable_Printable_True()
    {
        ((Str)"hello").Isprintable().Should().BeTrue();
    }

    [Fact]
    public void Isprintable_ControlChar_False()
    {
        ((Str)"\x00").Isprintable().Should().BeFalse();
    }

    [Fact]
    public void Isprintable_Empty_True()
    {
        ((Str)"").Isprintable().Should().BeTrue();
    }

    [Fact]
    public void Isascii_AllAscii_True()
    {
        ((Str)"hello").Isascii().Should().BeTrue();
    }

    [Fact]
    public void Isascii_NonAscii_False()
    {
        ((Str)"héllo").Isascii().Should().BeFalse();
    }

    [Fact]
    public void Isascii_Empty_True()
    {
        ((Str)"").Isascii().Should().BeTrue();
    }

    #endregion

    #region Split

    [Fact]
    public void Split_OnWhitespace()
    {
        Str s = "a b  c";
        var result = s.Split();
        result.Should().HaveCount(3);
        ((string)result[0]).Should().Be("a");
        ((string)result[1]).Should().Be("b");
        ((string)result[2]).Should().Be("c");
    }

    [Fact]
    public void Split_OnSeparator()
    {
        Str s = "a,b,c";
        var result = s.Split((Str)",");
        result.Should().HaveCount(3);
        ((string)result[0]).Should().Be("a");
    }

    [Fact]
    public void Split_WithMaxsplit()
    {
        Str s = "a,b,c,d";
        var result = s.Split((Str)",", 2);
        result.Should().HaveCount(3);
        ((string)result[2]).Should().Be("c,d");
    }

    [Fact]
    public void Split_EmptySep_ThrowsValueError()
    {
        Str s = "abc";
        FluentActions.Invoking(() => s.Split((Str)"")).Should().Throw<ValueError>();
    }

    #endregion

    #region Rsplit

    [Fact]
    public void Rsplit_WithMaxsplit()
    {
        Str s = "a,b,c,d";
        var result = s.Rsplit((Str)",", 2);
        result.Should().HaveCount(3);
        ((string)result[0]).Should().Be("a,b");
    }

    #endregion

    #region Splitlines

    [Fact]
    public void Splitlines_BasicNewlines()
    {
        Str s = "a\nb\nc";
        var result = s.Splitlines();
        result.Should().HaveCount(3);
        ((string)result[0]).Should().Be("a");
    }

    [Fact]
    public void Splitlines_KeepEnds()
    {
        Str s = "a\nb\n";
        var result = s.Splitlines(true);
        result.Should().HaveCount(2);
        ((string)result[0]).Should().Be("a\n");
    }

    #endregion

    #region Partition

    [Fact]
    public void Partition_Found()
    {
        Str s = "a.b.c";
        var (before, sep, after) = s.Partition((Str)".");
        ((string)before).Should().Be("a");
        ((string)sep).Should().Be(".");
        ((string)after).Should().Be("b.c");
    }

    [Fact]
    public void Partition_NotFound()
    {
        Str s = "abc";
        var (before, sep, after) = s.Partition((Str)".");
        ((string)before).Should().Be("abc");
        ((string)sep).Should().Be("");
        ((string)after).Should().Be("");
    }

    [Fact]
    public void Rpartition_Found()
    {
        Str s = "a.b.c";
        var (before, sep, after) = s.Rpartition((Str)".");
        ((string)before).Should().Be("a.b");
        ((string)sep).Should().Be(".");
        ((string)after).Should().Be("c");
    }

    #endregion

    #region Join

    [Fact]
    public void Join_StrIterable()
    {
        Str sep = ", ";
        var items = new List<Str> { "a", "b", "c" };
        ((string)sep.Join(items)).Should().Be("a, b, c");
    }

    [Fact]
    public void Join_StringIterable()
    {
        Str sep = ", ";
        var items = new string[] { "a", "b", "c" };
        ((string)sep.Join(items)).Should().Be("a, b, c");
    }

    #endregion

    #region Expandtabs

    [Fact]
    public void Expandtabs_Default()
    {
        Str s = "a\tb";
        string result = s.Expandtabs();
        result.Should().Contain("a");
        result.Should().Contain("b");
        result.Should().NotContain("\t");
    }

    [Fact]
    public void Expandtabs_Custom()
    {
        Str s = "a\tb";
        ((string)s.Expandtabs(4)).Should().Be("a   b");
    }

    #endregion

    #region Encode

    [Fact]
    public void Encode_Utf8_ReturnsBytes()
    {
        Str s = "hello";
        var bytes = s.Encode();
        bytes.Length.Should().Be(5);
    }

    [Fact]
    public void Encode_Ascii()
    {
        Str s = "hello";
        var bytes = s.Encode("ascii");
        bytes.Length.Should().Be(5);
    }

    #endregion

    #region Maketrans / Translate

    [Fact]
    public void Maketrans_And_Translate()
    {
        var table = Str.Maketrans("aeiou", "12345");
        Str s = "apple";
        ((string)s.Translate(table)).Should().Be("1ppl2");
    }

    #endregion
}
