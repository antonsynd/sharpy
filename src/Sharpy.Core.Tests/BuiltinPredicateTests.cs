using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

/// <summary>
/// Tests for builtin predicate and formatting functions not already covered in:
///   AllTests.cs, AnyTests.cs, FormatTests.cs.
/// </summary>
public class BuiltinPredicate_Tests
{
    // ── All — additional cases not in AllTests.cs ──

    [Fact]
    public void All_TruthyIntegers_ReturnsTrue()
    {
        // Python: all([1, 2, 3]) == True
        List<int> list = [1, 2, 3];
        All(list).Should().BeTrue();
    }

    [Fact]
    public void All_ZeroAmongIntegers_ReturnsFalse()
    {
        List<int> list = [1, 0, 3];
        All(list).Should().BeFalse();
    }

    [Fact]
    public void All_AllZeros_ReturnsFalse()
    {
        List<int> list = [0, 0, 0];
        All(list).Should().BeFalse();
    }

    // ── Any — additional cases not in AnyTests.cs ──

    [Fact]
    public void Any_AllZeroIntegers_ReturnsFalse()
    {
        List<int> list = [0, 0, 0];
        Any(list).Should().BeFalse();
    }

    [Fact]
    public void Any_SingleTruthyElement_ReturnsTrue()
    {
        List<int> list = [0, 5, 0];
        Any(list).Should().BeTrue();
    }

    [Fact]
    public void Any_SingleFalsyElement_ReturnsFalse()
    {
        List<int> list = [0];
        Any(list).Should().BeFalse();
    }

    // ── Format — additional cases not in FormatTests.cs ──

    [Fact]
    public void Format_DoubleWithLowerF2_ReturnsTwoDecimals()
    {
        // Python: format(3.14159, ".2f") == "3.14"
        // .NET equivalent: "F2" format specifier
        Format(3.14159, "F2").Should().Be("3.14");
    }

    [Fact]
    public void Format_IntegerWithX_ReturnsLowerHex()
    {
        // Python: format(255, "x") == "ff"
        // .NET: "x" gives lowercase hex
        Format(255, "x").Should().Be("ff");
    }

    [Fact]
    public void Format_IntegerWithUpperX_ReturnsUpperHex()
    {
        // Python: format(255, "X") == "FF"
        Format(255, "X").Should().Be("FF");
    }

    [Fact]
    public void Format_IntegerWithNoSpec_ReturnsDecimalString()
    {
        // Python: format(42) == "42"
        Format(42).Should().Be("42");
    }

    [Fact]
    public void Format_StringWithNoSpec_ReturnsString()
    {
        Format("hello").Should().Be("hello");
    }

    [Fact]
    public void Format_NullWithNoSpec_ReturnsNone()
    {
        Format((object?)null).Should().Be("None");
    }
}
