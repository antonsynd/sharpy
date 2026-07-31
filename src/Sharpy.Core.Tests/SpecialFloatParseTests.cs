using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

/// <summary>
/// Acceptance table for CPython's special float spellings (#1203).
///
/// <para>Two of the three groups below are <b>regression pins, not proof of a fix</b>:
/// <c>double.TryParse</c> against <c>InvariantCulture</c> already matched that culture's
/// <c>Infinity</c>/<c>NaN</c> symbols case-insensitively (and honoured a leading sign), so every
/// full <c>infinity</c> spelling and every <c>nan</c> spelling parsed before
/// <c>TryParseSpecialFloat</c> existed. The genuinely new capability is exactly three forms:
/// <c>inf</c>, <c>-inf</c>, <c>+inf</c> (case-insensitively). The pins exist so that routing both
/// seams through the shared predicate cannot silently drop what the BCL used to accept.</para>
///
/// <para>Every expected value here was verified against <c>python3</c> on 2026-07-31.</para>
/// </summary>
public class SpecialFloatParseTests
{
    // --- Genuinely new: the abbreviated forms the BCL rejects -------------------------------

    [Theory]
    [InlineData("inf")]
    [InlineData("INF")]
    [InlineData("Inf")]
    [InlineData("iNf")]
    [InlineData("+inf")]
    [InlineData("+INF")]
    public void TryParseSpecialFloat_AbbreviatedPositiveInfinity_IsNewlyAccepted(string s)
    {
        DoubleParse.TryParseSpecialFloat(s, out var value).Should().BeTrue();
        value.Should().Be(double.PositiveInfinity);
    }

    [Theory]
    [InlineData("-inf")]
    [InlineData("-INF")]
    [InlineData("-Inf")]
    public void TryParseSpecialFloat_AbbreviatedNegativeInfinity_IsNewlyAccepted(string s)
    {
        DoubleParse.TryParseSpecialFloat(s, out var value).Should().BeTrue();
        value.Should().Be(double.NegativeInfinity);
    }

    [Fact]
    public void AbbreviatedInf_WasRejectedByTheBcl_WhichIsWhyThePredicateExists()
    {
        // The premise of #1203's fix: this is the only group double.TryParse does not cover.
        double.TryParse("inf", System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out _).Should().BeFalse();
    }

    // --- Regression pins: already accepted by double.TryParse before the predicate ----------

    [Theory]
    [InlineData("infinity")]
    [InlineData("Infinity")]
    [InlineData("INFINITY")]
    [InlineData("+Infinity")]
    [InlineData("+infinity")]
    public void TryParseSpecialFloat_FullPositiveInfinity_RegressionPin(string s)
    {
        DoubleParse.TryParseSpecialFloat(s, out var value).Should().BeTrue();
        value.Should().Be(double.PositiveInfinity);
    }

    [Theory]
    [InlineData("-infinity")]
    [InlineData("-Infinity")]
    [InlineData("-INFINITY")]
    public void TryParseSpecialFloat_FullNegativeInfinity_RegressionPin(string s)
    {
        DoubleParse.TryParseSpecialFloat(s, out var value).Should().BeTrue();
        value.Should().Be(double.NegativeInfinity);
    }

    [Theory]
    [InlineData("nan")]
    [InlineData("NAN")]
    [InlineData("NaN")]
    [InlineData("+nan")]
    [InlineData("-nan")]
    public void TryParseSpecialFloat_Nan_RegressionPin_SignIsDiscarded(string s)
    {
        // CPython: float("-nan") is nan, not -nan. The sign is not applied.
        DoubleParse.TryParseSpecialFloat(s, out var value).Should().BeTrue();
        double.IsNaN(value).Should().BeTrue();
    }

    [Theory]
    [InlineData("infinity")]
    [InlineData("Infinity")]
    [InlineData("nan")]
    [InlineData("-nan")]
    public void FullSpellings_WereAlreadyAcceptedByTheBcl_SoThePinsAreNotNewCapability(string s)
    {
        double.TryParse(s, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out _).Should().BeTrue();
    }

    // --- Must stay rejected -----------------------------------------------------------------

    [Theory]
    [InlineData("in f")]
    [InlineData("infinit")]
    [InlineData("infinityy")]
    [InlineData("i")]
    [InlineData("")]
    [InlineData("+")]
    [InlineData("-")]
    [InlineData("++inf")]
    [InlineData("inf.0")]
    [InlineData("3.14")]
    [InlineData("nano")]
    public void TryParseSpecialFloat_NonSpecialOrMalformed_IsRejected(string s)
    {
        DoubleParse.TryParseSpecialFloat(s, out var value).Should().BeFalse();
        value.Should().Be(0.0);
    }
}
