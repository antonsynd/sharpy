using Xunit;
using static Sharpy.YamlScalarStyleAuthority;

namespace Sharpy.Stdlib.Tests;

/// <summary>
/// Cell-by-cell coverage of <see cref="YamlScalarStyleAuthority"/> — the one
/// authority both dump surfaces consult (#1542).
///
/// Oracle: PyYAML 6.0.3, python3 3.12.13, block context (default_flow_style=False).
/// <code>
/// python3 -c "import yaml; print(repr(yaml.dump(value, default_flow_style=False)))"
/// </code>
/// </summary>
public class YamlScalarStyleAuthorityTests
{
    [Fact]
    public void EmptyString_SingleQuoted()
        => Assert.Equal(Style.SingleQuoted, Choose("", wouldResolve: false));

    [Fact]
    public void PlainString_Plain()
        => Assert.Equal(Style.Plain, Choose("hello", wouldResolve: false));

    [Fact]
    public void ResolverClaimed_SingleQuoted()
        => Assert.Equal(Style.SingleQuoted, Choose("true", wouldResolve: true));

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("null")]
    [InlineData("yes")]
    [InlineData("no")]
    [InlineData(".inf")]
    [InlineData(".nan")]
    [InlineData("~")]
    [InlineData("0x1A")]
    public void AllResolverClaimedValues_SingleQuoted(string value)
        => Assert.Equal(Style.SingleQuoted, Choose(value, wouldResolve: true));

    [Fact]
    public void ColonNotFollowedBySpace_Plain()
        => Assert.Equal(Style.Plain, Choose("a:b", wouldResolve: false));

    [Fact]
    public void ColonSpace_SingleQuoted()
        => Assert.Equal(Style.SingleQuoted, Choose("a: b", wouldResolve: false));

    [Fact]
    public void HashNotPrecededBySpace_Plain()
        => Assert.Equal(Style.Plain, Choose("a#b", wouldResolve: false));

    [Fact]
    public void SpaceHash_SingleQuoted()
        => Assert.Equal(Style.SingleQuoted, Choose("a #b", wouldResolve: false));

    [Fact]
    public void Tab_DoubleQuoted()
        => Assert.Equal(Style.DoubleQuoted, Choose("tab\tchar", wouldResolve: false));

    [Fact]
    public void Multiline_SingleQuoted()
        => Assert.Equal(Style.SingleQuoted, Choose("line1\nline2", wouldResolve: false));

    [Theory]
    [InlineData("- dash")]
    [InlineData("? question")]
    public void LeadingIndicatorWithSpace_SingleQuoted(string value)
        => Assert.Equal(Style.SingleQuoted, Choose(value, wouldResolve: false));

    [Theory]
    [InlineData("#hash")]
    [InlineData("&anchor")]
    [InlineData("*ref")]
    [InlineData("!tag")]
    [InlineData("|block")]
    [InlineData(">fold")]
    [InlineData("%directive")]
    [InlineData("@at")]
    [InlineData("`backtick")]
    public void LeadingSpecialChar_SingleQuoted(string value)
        => Assert.Equal(Style.SingleQuoted, Choose(value, wouldResolve: false));

    [Fact]
    public void LeadingSpace_SingleQuoted()
        => Assert.Equal(Style.SingleQuoted, Choose(" lead", wouldResolve: false));

    [Fact]
    public void TrailingSpace_SingleQuoted()
        => Assert.Equal(Style.SingleQuoted, Choose("trail ", wouldResolve: false));

    [Fact]
    public void ColonAtEnd_SingleQuoted()
        => Assert.Equal(Style.SingleQuoted, Choose("end:", wouldResolve: false));

    [Fact]
    public void C0Control_DoubleQuoted()
        => Assert.Equal(Style.DoubleQuoted, Choose("\x01ctl", wouldResolve: false));

    [Fact]
    public void Del_DoubleQuoted()
        => Assert.Equal(Style.DoubleQuoted, Choose("a\u007Fdel", wouldResolve: false));

    [Fact]
    public void ColonAtStartNoSpace_Plain()
        => Assert.Equal(Style.Plain, Choose(":start", wouldResolve: false));

    [Fact]
    public void ColonAtStartWithSpace_SingleQuoted()
        => Assert.Equal(Style.SingleQuoted, Choose(": start", wouldResolve: false));

    [Fact]
    public void Comma_Plain()
        => Assert.Equal(Style.Plain, Choose("a,b", wouldResolve: false));

    [Fact]
    public void FlowSequence_SingleQuoted()
        => Assert.Equal(Style.SingleQuoted, Choose("[]", wouldResolve: false));

    [Fact]
    public void FlowMapping_SingleQuoted()
        => Assert.Equal(Style.SingleQuoted, Choose("{}", wouldResolve: false));

    [Fact]
    public void DocumentIndicator_SingleQuoted()
        => Assert.Equal(Style.SingleQuoted, Choose("---", wouldResolve: false));

    [Fact]
    public void DocumentEndIndicator_SingleQuoted()
        => Assert.Equal(Style.SingleQuoted, Choose("...", wouldResolve: false));

    [Fact]
    public void SexagesimalDeclined_Plain()
    {
        // 12:30 is sexagesimal in PyYAML but declined by Sharpy (#1465).
        // The resolver passes wouldResolve=false, so the authority says plain.
        Assert.Equal(Style.Plain, Choose("12:30", wouldResolve: false));
    }

    // ------------------------------------------------------------------
    // Flow-vs-block split: characters that are data in block context but
    // indicators inside a flow collection.
    //
    // Oracle: PyYAML 6.0.3, python3 3.12.13, measured 2026-08-20:
    //   python3 -c "import yaml; print(repr(yaml.safe_dump({'k': v}, default_flow_style=True)))"
    //   'a,b' / 'a[b' / 'a]b' / 'a{b' / 'x? y' -> single-quoted in flow, plain in block;
    //   'plain' -> plain in both.
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("a,b")]
    [InlineData("a[b")]
    [InlineData("a]b")]
    [InlineData("a{b")]
    [InlineData("x? y")]
    public void FlowIndicatorChars_SingleQuotedInFlow_PlainInBlock(string value)
    {
        Assert.Equal(Style.SingleQuoted, Choose(value, wouldResolve: false, flowContext: true));
        Assert.Equal(Style.Plain, Choose(value, wouldResolve: false, flowContext: false));
    }

    [Fact]
    public void PlainString_PlainInFlowToo()
        => Assert.Equal(Style.Plain, Choose("plain", wouldResolve: false, flowContext: true));
}
