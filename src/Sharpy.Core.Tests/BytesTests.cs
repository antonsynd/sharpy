using Xunit;
using FluentAssertions;
using System.Linq;

namespace Sharpy.Core.Tests;

public class BytesTests
{
    #region Constructor and Basic Properties

    [Fact]
    public void Constructor_WithByteArray_CopiesData()
    {
        var data = new byte[] { 1, 2, 3 };
        var b = new Bytes(data);
        data[0] = 99;
        b[0].Should().Be(1);
    }

    [Fact]
    public void Constructor_WithNull_CreatesEmpty()
    {
        var b = new Bytes(null);
        b.Length.Should().Be(0);
    }

    [Fact]
    public void Length_ReturnsCorrectValue()
    {
        var b = new Bytes(new byte[] { 104, 101, 108, 108, 111 });
        b.Length.Should().Be(5);
    }

    #endregion

    #region Indexing

    [Fact]
    public void Indexer_ReturnsIntValue()
    {
        var b = new Bytes(new byte[] { 104, 101, 108, 108, 111 });
        b[0].Should().Be(104);
        b[4].Should().Be(111);
    }

    [Fact]
    public void Indexer_NegativeIndex_CountsFromEnd()
    {
        var b = new Bytes(new byte[] { 104, 101, 108, 108, 111 });
        b[-1].Should().Be(111);
        b[-2].Should().Be(108);
        b[-5].Should().Be(104);
    }

    [Fact]
    public void Indexer_OutOfRange_ThrowsIndexError()
    {
        var b = new Bytes(new byte[] { 1, 2, 3 });
        var act = () => { var _ = b[5]; };
        act.Should().Throw<IndexError>();
    }

    [Fact]
    public void Indexer_NegativeOutOfRange_ThrowsIndexError()
    {
        var b = new Bytes(new byte[] { 1, 2, 3 });
        var act = () => { var _ = b[-4]; };
        act.Should().Throw<IndexError>();
    }

    #endregion

    #region Hex / Fromhex / Decode

    [Fact]
    public void Hex_NoSeparator_ReturnsHexString()
    {
        var b = new Bytes(new byte[] { 104, 101, 108, 108, 111 });
        b.Hex().Should().Be("68656c6c6f");
    }

    [Fact]
    public void Hex_WithSeparator_InsertsSep()
    {
        var b = new Bytes(new byte[] { 104, 101, 108, 108, 111 });
        b.Hex(":").Should().Be("68:65:6c:6c:6f");
    }

    [Fact]
    public void Hex_WithSeparatorAndBytesPerSep_GroupsCorrectly()
    {
        var b = new Bytes(new byte[] { 104, 101, 108, 108, 111 });
        b.Hex(":", 2).Should().Be("68:656c:6c6f");
    }

    [Fact]
    public void Hex_Empty_ReturnsEmptyString()
    {
        var b = new Bytes(new byte[0]);
        b.Hex().Should().Be("");
    }

    [Fact]
    public void Fromhex_ValidHex_ReturnsBytes()
    {
        var b = Bytes.Fromhex("68656c6c6f");
        b.Length.Should().Be(5);
        b[0].Should().Be(104);
        b[4].Should().Be(111);
    }

    [Fact]
    public void Fromhex_WithSpaces_IgnoresSpaces()
    {
        var b = Bytes.Fromhex("68 65 6C 6C 6F");
        b.Length.Should().Be(5);
        b.Hex().Should().Be("68656c6c6f");
    }

    [Fact]
    public void Fromhex_InvalidHex_ThrowsValueError()
    {
        var act = () => Bytes.Fromhex("xyz");
        act.Should().Throw<ValueError>();
    }

    [Fact]
    public void Decode_Utf8_ReturnsString()
    {
        var b = new Bytes(new byte[] { 104, 101, 108, 108, 111 });
        b.Decode().Should().Be("hello");
        b.Decode("utf-8").Should().Be("hello");
    }

    [Fact]
    public void Decode_Ascii_ReturnsString()
    {
        var b = new Bytes(new byte[] { 72, 73 });
        b.Decode("ascii").Should().Be("HI");
    }

    [Fact]
    public void Decode_UnknownEncoding_ThrowsValueError()
    {
        var b = new Bytes(new byte[] { 1 });
        var act = () => b.Decode("unknown-encoding");
        act.Should().Throw<ValueError>();
    }

    #endregion

    #region Operators

    [Fact]
    public void Concatenation_ReturnsCombinedBytes()
    {
        var a = new Bytes(new byte[] { 1, 2 });
        var b = new Bytes(new byte[] { 3, 4 });
        var c = a + b;
        c.Length.Should().Be(4);
        c[0].Should().Be(1);
        c[3].Should().Be(4);
    }

    [Fact]
    public void Repetition_RepeatsBytes()
    {
        var b = new Bytes(new byte[] { 1, 2 });
        var r = b * 3;
        r.Length.Should().Be(6);
        r[0].Should().Be(1);
        r[1].Should().Be(2);
        r[4].Should().Be(1);
        r[5].Should().Be(2);
    }

    [Fact]
    public void Repetition_ZeroOrNegative_ReturnsEmpty()
    {
        var b = new Bytes(new byte[] { 1, 2 });
        (b * 0).Length.Should().Be(0);
        (b * -1).Length.Should().Be(0);
    }

    [Fact]
    public void Repetition_IntTimesBytes()
    {
        var b = new Bytes(new byte[] { 1 });
        var r = 3 * b;
        r.Length.Should().Be(3);
    }

    [Fact]
    public void Equality_EqualBytes_ReturnsTrue()
    {
        var a = new Bytes(new byte[] { 1, 2, 3 });
        var b = new Bytes(new byte[] { 1, 2, 3 });
        (a == b).Should().BeTrue();
        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void Equality_DifferentBytes_ReturnsFalse()
    {
        var a = new Bytes(new byte[] { 1, 2, 3 });
        var b = new Bytes(new byte[] { 1, 2, 4 });
        (a == b).Should().BeFalse();
        (a \!= b).Should().BeTrue();
    }

    [Fact]
    public void Contains_ByteValue_ReturnsCorrectly()
    {
        var b = new Bytes(new byte[] { 104, 101, 108, 108, 111 });
        b.Contains(111).Should().BeTrue();
        b.Contains(120).Should().BeFalse();
    }

    [Fact]
    public void Contains_SubBytes_ReturnsCorrectly()
    {
        var b = new Bytes(new byte[] { 104, 101, 108, 108, 111 });
        b.Contains(new Bytes(new byte[] { 108, 108 })).Should().BeTrue();
        b.Contains(new Bytes(new byte[] { 120, 121 })).Should().BeFalse();
    }

    [Fact]
    public void Contains_OutOfRangeValue_ThrowsValueError()
    {
        var b = new Bytes(new byte[] { 1 });
        var act = () => b.Contains(256);
        act.Should().Throw<ValueError>();
    }

    #endregion

    #region ISized and IBoolConvertible

    [Fact]
    public void ISized_Count_ReturnsLength()
    {
        var b = new Bytes(new byte[] { 1, 2, 3 });
        ((ISized)b).Count.Should().Be(3);
    }

    [Fact]
    public void IBoolConvertible_NonEmpty_IsTrue()
    {
        var b = new Bytes(new byte[] { 1 });
        ((IBoolConvertible)b).IsTrue.Should().BeTrue();
    }

    [Fact]
    public void IBoolConvertible_Empty_IsFalse()
    {
        var b = new Bytes(new byte[0]);
        ((IBoolConvertible)b).IsTrue.Should().BeFalse();
    }

    #endregion

    #region IEnumerable<int>

    [Fact]
    public void Enumerable_IteratesByteValuesAsInts()
    {
        var b = new Bytes(new byte[] { 104, 101, 108, 108, 111 });
        var values = b.ToList();
        values.Should().Equal(104, 101, 108, 108, 111);
    }

    #endregion

    #region Slicing

    [Fact]
    public void Slice_BasicSlice_ReturnsSubsequence()
    {
        var b = new Bytes(new byte[] { 104, 101, 108, 108, 111 });
        var s = b.Slice(1, 3, null);
        s.Length.Should().Be(2);
        s[0].Should().Be(101);
        s[1].Should().Be(108);
    }

    [Fact]
    public void Slice_WithStep_SkipsElements()
    {
        var b = new Bytes(new byte[] { 0, 1, 2, 3, 4, 5 });
        var s = b.Slice(0, 6, 2);
        s.Length.Should().Be(3);
        s[0].Should().Be(0);
        s[1].Should().Be(2);
        s[2].Should().Be(4);
    }

    [Fact]
    public void Slice_NegativeStep_Reverses()
    {
        var b = new Bytes(new byte[] { 0, 1, 2, 3, 4 });
        var s = b.Slice(null, null, -1);
        s.Length.Should().Be(5);
        s[0].Should().Be(4);
        s[4].Should().Be(0);
    }

    [Fact]
    public void Slice_ZeroStep_ThrowsValueError()
    {
        var b = new Bytes(new byte[] { 1, 2, 3 });
        var act = () => b.Slice(0, 3, 0);
        act.Should().Throw<ValueError>();
    }

    #endregion

    #region Search Methods

    [Fact]
    public void Find_ExistingSubsequence_ReturnsIndex()
    {
        var b = new Bytes(new byte[] { 104, 101, 108, 108, 111 });
        var sub = new Bytes(new byte[] { 108, 111 });
        b.Find(sub).Should().Be(3);
    }

    [Fact]
    public void Find_NotFound_ReturnsNegativeOne()
    {
        var b = new Bytes(new byte[] { 104, 101, 108, 108, 111 });
        var sub = new Bytes(new byte[] { 120, 121, 122 });
        b.Find(sub).Should().Be(-1);
    }

    [Fact]
    public void Rfind_FindsLastOccurrence()
    {
        var b = new Bytes(new byte[] { 104, 101, 108, 108, 111 });
        var sub = new Bytes(new byte[] { 108 });
        b.Rfind(sub).Should().Be(3);
    }

    [Fact]
    public void Count_CountsOccurrences()
    {
        var b = new Bytes(new byte[] { 104, 101, 108, 108, 111 });
        var sub = new Bytes(new byte[] { 108 });
        b.Count(sub).Should().Be(2);
    }

    #endregion

    #region Replace

    [Fact]
    public void Replace_AllOccurrences()
    {
        var b = new Bytes(new byte[] { 104, 101, 108, 108, 111 });
        var result = b.Replace(
            new Bytes(new byte[] { 108 }),
            new Bytes(new byte[] { 76 }));
        result[2].Should().Be(76);
        result[3].Should().Be(76);
        result.Length.Should().Be(5);
    }

    [Fact]
    public void Replace_WithCount_LimitsReplacements()
    {
        var b = new Bytes(new byte[] { 97, 97, 98, 97, 98, 97, 98 });
        var result = b.Replace(
            new Bytes(new byte[] { 97 }),
            new Bytes(new byte[] { 88 }),
            2);
        result[0].Should().Be(88);
        result[1].Should().Be(88);
        result[2].Should().Be(98);
        result[3].Should().Be(97);
    }

    #endregion

    #region Startswith / Endswith

    [Fact]
    public void Startswith_MatchingPrefix_ReturnsTrue()
    {
        var b = new Bytes(new byte[] { 104, 101, 108, 108, 111 });
        b.Startswith(new Bytes(new byte[] { 104, 101 })).Should().BeTrue();
    }

    [Fact]
    public void Startswith_NonMatchingPrefix_ReturnsFalse()
    {
        var b = new Bytes(new byte[] { 104, 101, 108, 108, 111 });
        b.Startswith(new Bytes(new byte[] { 120 })).Should().BeFalse();
    }

    [Fact]
    public void Endswith_MatchingSuffix_ReturnsTrue()
    {
        var b = new Bytes(new byte[] { 104, 101, 108, 108, 111 });
        b.Endswith(new Bytes(new byte[] { 108, 111 })).Should().BeTrue();
    }

    [Fact]
    public void Endswith_NonMatchingSuffix_ReturnsFalse()
    {
        var b = new Bytes(new byte[] { 104, 101, 108, 108, 111 });
        b.Endswith(new Bytes(new byte[] { 120 })).Should().BeFalse();
    }

    #endregion

    #region Split / Join

    [Fact]
    public void Split_OnWhitespace_SplitsAndTrims()
    {
        var b = new Bytes(new byte[] { 104, 101, 108, 108, 111, 32, 119, 111, 114, 108, 100 });
        var parts = b.Split();
        parts.Should().HaveCount(2);
        parts[0].Decode().Should().Be("hello");
        parts[1].Decode().Should().Be("world");
    }

    [Fact]
    public void Split_OnSeparator_SplitsCorrectly()
    {
        var b = new Bytes(new byte[] { 104, 101, 108, 108, 111, 32, 119, 111, 114, 108, 100 });
        var parts = b.Split(new Bytes(new byte[] { 108 }));
        parts.Should().HaveCount(4);
        parts[0].Decode().Should().Be("he");
        parts[1].Decode().Should().Be("");
        parts[2].Decode().Should().Be("o wor");
        parts[3].Decode().Should().Be("d");
    }

    [Fact]
    public void Join_CombinesWithSeparator()
    {
        var sep = new Bytes(new byte[] { 32 });
        var items = new Sharpy.List<Bytes>(new[]
        {
            new Bytes(new byte[] { 104, 101, 108, 108, 111 }),
            new Bytes(new byte[] { 119, 111, 114, 108, 100 })
        });
        var result = sep.Join(items);
        result.Decode().Should().Be("hello world");
    }

    #endregion

    #region Strip / Lstrip / Rstrip

    [Fact]
    public void Strip_RemovesLeadingAndTrailingWhitespace()
    {
        var b = new Bytes(new byte[] { 32, 32, 104, 101, 108, 108, 111, 32, 32 });
        var result = b.Strip();
        result.Decode().Should().Be("hello");
    }

    [Fact]
    public void Lstrip_RemovesLeadingWhitespace()
    {
        var b = new Bytes(new byte[] { 32, 32, 104, 101, 108, 108, 111, 32, 32 });
        var result = b.Lstrip();
        result.Decode().Should().Be("hello  ");
    }

    [Fact]
    public void Rstrip_RemovesTrailingWhitespace()
    {
        var b = new Bytes(new byte[] { 32, 32, 104, 101, 108, 108, 111, 32, 32 });
        var result = b.Rstrip();
        result.Decode().Should().Be("  hello");
    }

    #endregion

    #region Upper / Lower

    [Fact]
    public void Upper_ConvertsAsciiLowercaseToUppercase()
    {
        var b = new Bytes(new byte[] { 104, 101, 108, 108, 111 });
        var result = b.Upper();
        result.Decode().Should().Be("HELLO");
    }

    [Fact]
    public void Lower_ConvertsAsciiUppercaseToLowercase()
    {
        var b = new Bytes(new byte[] { 72, 69, 76, 76, 79 });
        var result = b.Lower();
        result.Decode().Should().Be("hello");
    }

    #endregion

    #region ToString

    [Fact]
    public void ToString_PrintableAscii_ShowsCharacters()
    {
        var b = new Bytes(new byte[] { 104, 101, 108, 108, 111 });
        b.ToString().Should().Be("b'hello'");
    }

    [Fact]
    public void ToString_NonPrintable_ShowsHexEscapes()
    {
        var b = new Bytes(new byte[] { 0, 1, 255 });
        b.ToString().Should().Be(@"b'\x00\x01\xff'");
    }

    #endregion

    #region Encode/Decode Roundtrip

    [Fact]
    public void EncodeDecode_Roundtrip()
    {
        var original = "hello world";
        var encoded = new Bytes(System.Text.Encoding.UTF8.GetBytes(original));
        var decoded = encoded.Decode("utf-8");
        decoded.Should().Be(original);
    }

    #endregion
}
