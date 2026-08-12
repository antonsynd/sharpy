using System.Collections.Generic;
using CsCheck;
using Xunit;

namespace Sharpy.Stdlib.Tests;

/// <summary>
/// The standing guard that <c>safe_dump</c> and <c>safe_load</c> stay agreed: for every scalar in
/// the corpus, <c>safe_load(safe_dump(v))</c> recovers both the TYPE and the VALUE.
///
/// <para>
/// <b>The corpus is a requirement of this property, not an implementation detail.</b> A corpus of
/// ordinary strings passes today and passed before #1417 was fixed — measured: of `true`, `42`,
/// `1.5`, `null`, `hello`, only `hello` survived the round trip beforehand. So a corpus without
/// resolver-ambiguous forms is a test that cannot fail, and the ambiguous entries below are what
/// make this assert anything at all. Adding a scalar kind to <c>YamlScalarResolver</c> (see #1465)
/// means adding its forms here too.
/// </para>
///
/// <para>
/// <b>What this property cannot see, stated because its greenness will otherwise be
/// over-read.</b> It compares Sharpy against Sharpy. Two halves that are wrong in mirror-image
/// ways satisfy it — and that is not hypothetical: <c>safe_dump(None)</c> emits `--- ` with no
/// value token where PyYAML emits `null`, and <c>safe_load</c> reads that back as null, so this
/// property is GREEN on a real divergence (#1467). PyYAML parity is the differential oracle's job,
/// not this one's. What this owns is that the two directions cannot drift apart from each other.
/// </para>
/// </summary>
public class YamlScalarRoundTripPropertyTests
{
    /// <summary>
    /// Strings whose text another YAML type would claim. These are the load-bearing cells: without
    /// them the property is vacuous. Each was verified against PyYAML 6.0.3 as a round-trip
    /// identity there.
    /// </summary>
    public static readonly TheoryData<string> AmbiguousStrings = new()
    {
        "true", "True", "TRUE", "false", "False", "FALSE",
        "null", "Null", "NULL", "~",
        "42", "-7", "+0", "0",
        "1.5", "-0.5", ".5", "5.", "1.0e+7", "1.0e-7",
        ".inf", "-.inf", ".nan",
    };

    /// <summary>
    /// Strings the resolver types as strings already — the control half. These must round-trip too,
    /// and they must NOT acquire quotes they do not need.
    /// </summary>
    public static readonly TheoryData<string> PlainStrings = new()
    {
        "hello", "a b c", "Hello, World!", "1e-7", "1.0e7", "+.5", "0x1A", "yes", "12:30",
    };

    [Theory]
    [MemberData(nameof(AmbiguousStrings))]
    public void AmbiguousString_SurvivesTheRoundTripAsAString(string value)
    {
        object? back = Yaml.SafeLoad(Yaml.SafeDump(value));
        Assert.IsType<string>(back);
        Assert.Equal(value, back);
    }

    [Theory]
    [MemberData(nameof(PlainStrings))]
    public void PlainString_SurvivesTheRoundTripAsAString(string value)
    {
        object? back = Yaml.SafeLoad(Yaml.SafeDump(value));
        Assert.IsType<string>(back);
        Assert.Equal(value, back);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(42)]
    [InlineData(-7)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void Int_SurvivesTheRoundTrip(int value)
    {
        Assert.Equal(value, Yaml.SafeLoad(Yaml.SafeDump(value)));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.1)]
    [InlineData(-0.5)]
    [InlineData(3.141592653589793)]
    [InlineData(1e-7)]
    [InlineData(1e20)]
    public void Double_SurvivesTheRoundTrip(double value)
    {
        Assert.Equal(value, Yaml.SafeLoad(Yaml.SafeDump(value)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Bool_SurvivesTheRoundTrip(bool value)
    {
        Assert.Equal(value, Yaml.SafeLoad(Yaml.SafeDump(value)));
    }

    /// <summary>
    /// The generated half. The enumerated corpora above cover the forms we know are ambiguous; this
    /// covers the ones nobody thought of — a generated string that happens to land on a resolver
    /// form is exactly the case an enumeration misses. Strings are drawn from a small alphabet of
    /// the characters that appear in YAML's scalar productions, so hits are frequent rather than
    /// astronomically rare: a uniform-Unicode generator would almost never produce `.5` or `true`.
    ///
    /// <para>
    /// <b>The alphabet's narrowness and the short length bound are deliberate — do not "fix" the
    /// weighting.</b> Every ambiguous YAML scalar IS short, so the concentration is aimed exactly
    /// where the defects are.
    /// </para>
    ///
    /// <para>
    /// <b>To widen this, raise the LENGTH BOUND, not <c>iter</c>.</b> Measured: at
    /// <c>iter: 10_000</c> CsCheck reports ~7,900 of those SKIPPED as repeats, i.e. only ~2,100
    /// distinct values are exercised — a 31-character alphabet over lengths 0-6, weighted short,
    /// saturates quickly. Raising the iteration count against a saturated space buys almost
    /// nothing, and it is the intuitive move, which is why it is written down here.
    /// </para>
    ///
    /// <para>
    /// Shrinking was verified rather than assumed, using the mutation that reverts #1417: CsCheck
    /// shrank in 4 steps to <c>"0"</c> — the minimal ambiguous scalar, a string that dumps bare and
    /// reloads as an integer — and printed a replay seed. A failure here arrives as a repro.
    /// </para>
    /// </summary>
    [Fact]
    public void GeneratedString_SurvivesTheRoundTripAsAString()
    {
        Gen.String[Gen.Char["truefalsnyON0123456789.+-eE:_ x"], 0, 6]
            .Sample(s =>
            {
                object? back = Yaml.SafeLoad(Yaml.SafeDump(s));
                return back is string roundTripped && roundTripped == s;
            }, iter: 10_000);
    }
}
