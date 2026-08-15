using Xunit;

namespace Sharpy.Stdlib.Tests;

/// <summary>
/// The YAML 1.1 float boundary table for <c>YamlScalarResolver</c> (#1423).
///
/// <para>
/// Every cell was measured against PyYAML 6.0.3 on CPython 3.12 rather than derived from the
/// spec text, because the three boundaries below are exactly the ones a paraphrase of the
/// production loses — and the plan this implements paraphrased it and lost all three.
/// </para>
/// </summary>
public class YamlScalarResolverFloatRuleTests
{
    /// <summary>
    /// Spellings YAML 1.1 calls floats. The dot is mandatory; a leading dot is allowed with no
    /// sign; an exponent is allowed only with an explicit sign.
    /// </summary>
    [Theory]
    [InlineData("0.5", 0.5)]
    [InlineData("-0.5", -0.5)]
    [InlineData("+0.5", 0.5)]
    [InlineData(".5", 0.5)]
    [InlineData("5.", 5.0)]
    [InlineData("1.", 1.0)]
    [InlineData("0.0", 0.0)]
    [InlineData("1.0e-7", 1e-7)]
    [InlineData("1.0e+7", 10000000.0)]
    [InlineData("1.0E+7", 10000000.0)]
    public void PlainScalar_SpelledAsAYaml11Float_ResolvesToDouble(string text, double expected)
    {
        Assert.Equal(expected, YamlScalarResolver.Resolve(text));
    }

    /// <summary>
    /// Spellings that are NOT YAML 1.1 floats and must stay strings. <c>double.TryParse</c> accepts
    /// every one of them, which is why the resolver cannot delegate the question to it — that
    /// delegation is #1423 itself.
    /// </summary>
    [Theory]
    // The dot is mandatory in the mantissa — #1423's own repro.
    [InlineData("1e-7")]
    [InlineData("1e7")]
    [InlineData("1E7")]
    [InlineData("-1e-7")]
    [InlineData("+1e7")]
    // The exponent's sign is mandatory: `1.0e7` is a string, `1.0e+7` is a float.
    [InlineData("1.0e7")]
    [InlineData("1.5e3")]
    [InlineData("1.5E3")]
    [InlineData("0.5e10")]
    // The leading-dot arm admits no sign: `.5` is a float, `+.5` and `-.5` are not.
    [InlineData("+.5")]
    [InlineData("-.5")]
    // Shapes no arm matches at all.
    [InlineData("1.2.3")]
    [InlineData("e7")]
    [InlineData("1e")]
    [InlineData(".")]
    [InlineData("-.")]
    public void PlainScalar_NotSpelledAsAYaml11Float_StaysAString(string text)
    {
        Assert.Equal(text, YamlScalarResolver.Resolve(text));
    }

    /// <summary>
    /// The underscore digit separator, FLIPPED: pinned as strings while #1465 was open, now
    /// carrying PyYAML's values. The pin did its job — it is what made the float half of #1465
    /// impossible to land silently while only the int half was being thought about.
    /// </summary>
    [Theory]
    [InlineData("1_0.5", 10.5)]
    [InlineData("1.0_5", 1.05)]
    [InlineData(".5_5", 0.55)]
    [InlineData("1_.5", 1.5)]
    public void PlainScalar_UnderscoreSeparatedFloat_ResolvesToTheMeasuredValue(string text, double expected)
    {
        Assert.Equal(expected, YamlScalarResolver.Resolve(text));
    }

    /// <summary>
    /// The separator does not loosen the arms it runs through: the leading-dot arm still needs a
    /// DIGIT first, and a separator cannot stand in for the mandatory dot or the mandatory
    /// exponent sign.
    /// </summary>
    [Theory]
    [InlineData("._5")]
    [InlineData("1_e-7")]
    [InlineData("1.0e_7")]
    public void PlainScalar_UnderscoreDoesNotWidenTheFloatArms(string text)
    {
        Assert.Equal(text, YamlScalarResolver.Resolve(text));
    }

    /// <summary>
    /// The special float spellings keep their own arms, ahead of the shape rule.
    /// </summary>
    [Theory]
    [InlineData(".inf", double.PositiveInfinity)]
    [InlineData("-.inf", double.NegativeInfinity)]
    [InlineData("+.inf", double.PositiveInfinity)]
    public void PlainScalar_SpecialFloatSpellings_KeepTheirArms(string text, double expected)
    {
        Assert.Equal(expected, YamlScalarResolver.Resolve(text));
    }

    [Fact]
    public void PlainScalar_NotANumber_ResolvesToNaN()
    {
        Assert.Equal(double.NaN, YamlScalarResolver.Resolve(".nan"));
    }

    /// <summary>
    /// Integers resolve ahead of the float rule and are untouched by it — a bare integer has no
    /// dot, so a change to the float shape must not make `42` a string.
    /// </summary>
    [Theory]
    [InlineData("42", 42)]
    [InlineData("-0", 0)]
    [InlineData("+7", 7)]
    public void PlainScalar_Integer_IsUnaffectedByTheFloatRule(string text, int expected)
    {
        Assert.Equal(expected, YamlScalarResolver.Resolve(text));
    }
}
