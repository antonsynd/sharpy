using System.Linq;
using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

/// <summary>
/// Additional tests for the Bytes type not covered by BytesTests.cs.
/// Focuses on: comparison operators (&lt;/&lt;=/&gt;/&gt;=), operator true/false,
/// Fromhex edge cases, and empty-bytes edge cases for various methods.
/// </summary>
public class BytesAdditionalTests
{
    #region Comparison operators

    [Fact]
    public void LessThan_ShorterPrefix_ReturnsTrue()
    {
        var a = new Bytes(new byte[] { 1, 2, 3 });
        var b = new Bytes(new byte[] { 1, 2, 4 });
        (a < b).Should().BeTrue();
    }

    [Fact]
    public void LessThan_GreaterBytes_ReturnsFalse()
    {
        var a = new Bytes(new byte[] { 1, 2, 4 });
        var b = new Bytes(new byte[] { 1, 2, 3 });
        (a < b).Should().BeFalse();
    }

    [Fact]
    public void LessThanOrEqual_EqualBytes_ReturnsTrue()
    {
        var a = new Bytes(new byte[] { 1, 2, 3 });
        var b = new Bytes(new byte[] { 1, 2, 3 });
        (a <= b).Should().BeTrue();
    }

    [Fact]
    public void LessThanOrEqual_GreaterBytes_ReturnsFalse()
    {
        var a = new Bytes(new byte[] { 1, 2, 4 });
        var b = new Bytes(new byte[] { 1, 2, 3 });
        (a <= b).Should().BeFalse();
    }

    [Fact]
    public void GreaterThan_GreaterByte_ReturnsTrue()
    {
        var a = new Bytes(new byte[] { 1, 2, 4 });
        var b = new Bytes(new byte[] { 1, 2, 3 });
        (a > b).Should().BeTrue();
    }

    [Fact]
    public void GreaterThan_LesserBytes_ReturnsFalse()
    {
        var a = new Bytes(new byte[] { 1, 2, 3 });
        var b = new Bytes(new byte[] { 1, 2, 4 });
        (a > b).Should().BeFalse();
    }

    [Fact]
    public void GreaterThanOrEqual_EqualBytes_ReturnsTrue()
    {
        var a = new Bytes(new byte[] { 1, 2, 3 });
        var b = new Bytes(new byte[] { 1, 2, 3 });
        (a >= b).Should().BeTrue();
    }

    [Fact]
    public void GreaterThanOrEqual_LesserBytes_ReturnsFalse()
    {
        var a = new Bytes(new byte[] { 1, 2, 3 });
        var b = new Bytes(new byte[] { 1, 2, 4 });
        (a >= b).Should().BeFalse();
    }

    [Fact]
    public void CompareTo_ShorterIsPrefixOfLonger_ShorterIsLess()
    {
        // Python: bytes([1, 2]) < bytes([1, 2, 3]) == True
        var a = new Bytes(new byte[] { 1, 2 });
        var b = new Bytes(new byte[] { 1, 2, 3 });
        (a < b).Should().BeTrue();
        (b > a).Should().BeTrue();
    }

    [Fact]
    public void CompareTo_EmptyBytesLessThanNonEmpty()
    {
        // Python: bytes() < bytes([1]) == True
        var empty = new Bytes(new byte[0]);
        var nonEmpty = new Bytes(new byte[] { 1 });
        (empty < nonEmpty).Should().BeTrue();
    }

    #endregion

    #region Operator true / operator false (truthiness via if-branching)

    [Fact]
    public void OperatorTrue_NonEmptyBytes_EnteredIfBranch()
    {
        var b = new Bytes(new byte[] { 1 });
        bool enteredIf = false;
        if (b)
        {
            enteredIf = true;
        }
        enteredIf.Should().BeTrue();
    }

    [Fact]
    public void OperatorFalse_EmptyBytes_EnteredElseBranch()
    {
        var b = new Bytes(new byte[0]);
        bool enteredElse = false;
        if (b)
        {
            // should not enter
        }
        else
        {
            enteredElse = true;
        }
        enteredElse.Should().BeTrue();
    }

    #endregion

    #region Fromhex edge cases

    [Fact]
    public void Fromhex_EmptyString_ReturnsEmptyBytes()
    {
        var b = Bytes.Fromhex("");
        b.Length.Should().Be(0);
    }

    [Fact]
    public void Fromhex_OddLength_ThrowsValueError()
    {
        // Python: bytes.fromhex("abc") raises ValueError (odd length)
        var act = () => Bytes.Fromhex("abc");
        act.Should().Throw<ValueError>();
    }

    #endregion

    #region Find edge cases

    [Fact]
    public void Find_InEmptyBytes_ReturnsMinusOne()
    {
        var b = new Bytes(new byte[0]);
        var sub = new Bytes(new byte[] { 1 });
        b.Find(sub).Should().Be(-1);
    }

    [Fact]
    public void Rfind_InEmptyBytes_ReturnsMinusOne()
    {
        var b = new Bytes(new byte[0]);
        var sub = new Bytes(new byte[] { 1 });
        b.Rfind(sub).Should().Be(-1);
    }

    [Fact]
    public void Count_InEmptyBytes_ReturnsZero()
    {
        var b = new Bytes(new byte[0]);
        var sub = new Bytes(new byte[] { 1 });
        b.Count(sub).Should().Be(0);
    }

    #endregion

    #region Startswith / Endswith — empty prefix/suffix

    [Fact]
    public void Startswith_EmptyPrefix_ReturnsTrue()
    {
        // Python: bytes([1,2,3]).startswith(b"") == True
        var b = new Bytes(new byte[] { 1, 2, 3 });
        b.Startswith(new Bytes(new byte[0])).Should().BeTrue();
    }

    [Fact]
    public void Endswith_EmptySuffix_ReturnsTrue()
    {
        // Python: bytes([1,2,3]).endswith(b"") == True
        var b = new Bytes(new byte[] { 1, 2, 3 });
        b.Endswith(new Bytes(new byte[0])).Should().BeTrue();
    }

    [Fact]
    public void Startswith_PrefixLongerThanBytes_ReturnsFalse()
    {
        var b = new Bytes(new byte[] { 1 });
        b.Startswith(new Bytes(new byte[] { 1, 2 })).Should().BeFalse();
    }

    [Fact]
    public void Endswith_SuffixLongerThanBytes_ReturnsFalse()
    {
        var b = new Bytes(new byte[] { 1 });
        b.Endswith(new Bytes(new byte[] { 0, 1 })).Should().BeFalse();
    }

    #endregion

    #region Strip / Lstrip / Rstrip with chars parameter (if available) — empty bytes

    [Fact]
    public void Strip_EmptyBytes_ReturnsEmpty()
    {
        var b = new Bytes(new byte[0]);
        b.Strip().Length.Should().Be(0);
    }

    [Fact]
    public void Lstrip_EmptyBytes_ReturnsEmpty()
    {
        var b = new Bytes(new byte[0]);
        b.Lstrip().Length.Should().Be(0);
    }

    [Fact]
    public void Rstrip_EmptyBytes_ReturnsEmpty()
    {
        var b = new Bytes(new byte[0]);
        b.Rstrip().Length.Should().Be(0);
    }

    #endregion

    #region Upper / Lower — edge cases

    [Fact]
    public void Upper_EmptyBytes_ReturnsEmpty()
    {
        var b = new Bytes(new byte[0]);
        b.Upper().Length.Should().Be(0);
    }

    [Fact]
    public void Lower_EmptyBytes_ReturnsEmpty()
    {
        var b = new Bytes(new byte[0]);
        b.Lower().Length.Should().Be(0);
    }

    [Fact]
    public void Upper_NonAsciiBytes_LeftUnchanged()
    {
        // Bytes above 127 are not ASCII letters — Upper() should not change them
        var b = new Bytes(new byte[] { 200, 201 });
        var result = b.Upper();
        result[0].Should().Be(200);
        result[1].Should().Be(201);
    }

    [Fact]
    public void Lower_NonAsciiBytes_LeftUnchanged()
    {
        var b = new Bytes(new byte[] { 200, 201 });
        var result = b.Lower();
        result[0].Should().Be(200);
        result[1].Should().Be(201);
    }

    #endregion

    #region Replace — empty old pattern

    [Fact]
    public void Replace_EmptyOld_InsertsNewBetweenEachByte()
    {
        // Python: b"ab".replace(b"", b"-") == b"-a-b-"
        var b = new Bytes(new byte[] { 97, 98 }); // "ab"
        var result = b.Replace(
            new Bytes(new byte[0]),
            new Bytes(new byte[] { 45 })); // "-"
        result.Decode().Should().Be("-a-b-");
    }

    #endregion

    #region GetHashCode — equal bytes have same hash

    [Fact]
    public void GetHashCode_EqualBytes_ReturnSameHash()
    {
        var a = new Bytes(new byte[] { 1, 2, 3 });
        var b = new Bytes(new byte[] { 1, 2, 3 });
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    #endregion

    #region ToArray — returns copy

    [Fact]
    public void ToArray_ReturnsCopyNotReference()
    {
        var b = new Bytes(new byte[] { 1, 2, 3 });
        var arr = b.ToArray();
        arr[0] = 99;
        b[0].Should().Be(1); // original unchanged
    }

    #endregion
}
