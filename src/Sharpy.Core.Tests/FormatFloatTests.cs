using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests;

/// <summary>
/// Layout rules for <see cref="Builtins.FormatFloat(double)"/> / <see cref="Builtins.FormatFloat(float)"/>.
/// Every double expectation below was produced by <c>python3 -c "print(repr(v))"</c> on 2026-07-31;
/// the float expectations are Sharpy's own decision (CPython has no float32) and are pinned as
/// byte-identical to what the pre-#1204 formatter produced.
/// </summary>
public class FormatFloatTests
{
    [Theory]
    // --- the #1204 divergence band: decpt == 17, i.e. [1e16, 1e17) ---
    [InlineData(9.99e15, "9990000000000000.0")]   // decpt == 16, positional under both rules
    [InlineData(1e16, "1e+16")]                   // decpt == 17, .NET was positional here
    [InlineData(1.5e16, "1.5e+16")]
    [InlineData(2e16 + 8, "2.000000000000001e+16")]
    [InlineData(1.0000000000000002e+16, "1.0000000000000002e+16")]
    [InlineData(1e17, "1e+17")]                   // decpt == 18, exponential under both rules
    // --- the lower boundary: decpt <= -4 ---
    [InlineData(0.0001, "0.0001")]                // decpt == -3
    [InlineData(1e-5, "1e-05")]                   // decpt == -4, two-digit exponent
    [InlineData(1e-7, "1e-07")]
    [InlineData(5e-324, "5e-324")]                // three-digit exponent
    // --- unchanged territory ---
    [InlineData(0.0, "0.0")]
    [InlineData(1e15, "1000000000000000.0")]
    [InlineData(1.23456789e15, "1234567890000000.0")]
    [InlineData(9999999999999998.0, "9999999999999998.0")]
    [InlineData(0.1 + 0.2, "0.30000000000000004")]
    [InlineData(123.456, "123.456")]
    [InlineData(-4.0, "-4.0")]
    [InlineData(0.5, "0.5")]
    [InlineData(1e300, "1e+300")]
    [InlineData(1e20, "1e+20")]
    [InlineData(1.7976931348623157e308, "1.7976931348623157e+308")]
    public void FormatFloat_Double_MatchesCPythonRepr(double value, string expected)
    {
        Builtins.FormatFloat(value).Should().Be(expected);
    }

    /// <summary>
    /// A zero significand carries no decpt, but it does carry a sign bit — Python prints
    /// -0.0, and the digit-decomposition rewrite must not drop that (#1186 produces -0.0).
    /// Kept out of the theory above because -0.0 and 0.0 render identically in test names.
    /// </summary>
    [Fact]
    public void FormatFloat_NegativeZero_KeepsTheSign()
    {
        Builtins.FormatFloat(-0.0).Should().Be("-0.0");
        Builtins.FormatFloat(-0.0f).Should().Be("-0.0");
        Builtins.FormatFloat(0.0).Should().Be("0.0");
        Builtins.FormatFloat(0.0f).Should().Be("0.0");
    }

    [Theory]
    [InlineData(double.NaN, "nan")]
    [InlineData(double.PositiveInfinity, "inf")]
    [InlineData(double.NegativeInfinity, "-inf")]
    public void FormatFloat_Double_SpecialValues_UsePythonSpellings(double value, string expected)
    {
        Builtins.FormatFloat(value).Should().Be(expected);
    }

    /// <summary>
    /// The float overload shares the renderer but not the threshold: it switches to
    /// exponential at decpt > 9, so float32 output is byte-identical to the pre-#1204
    /// formatter. Using the double's 16 here would move every one of the [1e9, 1e16)
    /// rows below into positional form.
    /// </summary>
    [Theory]
    [InlineData(3.14f, "3.14")]
    [InlineData(2.0f, "2.0")]
    [InlineData(0.5f, "0.5")]
    [InlineData(0.0f, "0.0")]
    [InlineData(1e8f, "100000000.0")]   // decpt == 9, the last positional decade
    [InlineData(1e9f, "1e+09")]         // decpt == 10, exponential
    [InlineData(9.99e14f, "9.99e+14")]
    [InlineData(1.5e15f, "1.5e+15")]
    [InlineData(1e-5f, "1e-05")]
    [InlineData(0.0001f, "0.0001")]
    public void FormatFloat_Single_KeepsItsOwnThreshold(float value, string expected)
    {
        Builtins.FormatFloat(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(float.NaN, "nan")]
    [InlineData(float.PositiveInfinity, "inf")]
    [InlineData(float.NegativeInfinity, "-inf")]
    public void FormatFloat_Single_SpecialValues_UsePythonSpellings(float value, string expected)
    {
        Builtins.FormatFloat(value).Should().Be(expected);
    }

    /// <summary>
    /// The single must not be widened to double before formatting: the two types have
    /// different shortest-round-trip digits for the same stored value.
    /// </summary>
    [Fact]
    public void FormatFloat_Single_DoesNotWidenToDouble()
    {
        const float value = 0.1f;

        Builtins.FormatFloat(value).Should().Be("0.1");
        Builtins.FormatFloat((double)value).Should().Be("0.10000000149011612");
    }

    /// <summary>
    /// Re-rendering from digits must round-trip: parsing the formatted string back yields
    /// the original value for every finite double, including the rewritten band.
    /// </summary>
    [Theory]
    [InlineData(1e16)]
    [InlineData(2e16 + 8)]
    [InlineData(1.0000000000000002e+16)]
    [InlineData(0.1 + 0.2)]
    [InlineData(5e-324)]
    [InlineData(1.7976931348623157e308)]
    [InlineData(-1.5e16)]
    public void FormatFloat_Double_RoundTrips(double value)
    {
        double.Parse(
            Builtins.FormatFloat(value),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture)
            .Should().Be(value);
    }
}
