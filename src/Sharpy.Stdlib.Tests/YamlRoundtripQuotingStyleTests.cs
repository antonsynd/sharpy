using Xunit;

namespace Sharpy.Stdlib.Tests;

/// <summary>
/// The two dump surfaces agree on HOW to quote, not merely on WHICH strings need quoting (#1472).
///
/// <para>
/// History: the surfaces once shared only the resolver — the since-deleted
/// <c>YamlRoundtrip.NeedsQuoting</c> and the converter now named <c>YamlStringStyleConverter</c>
/// both asked <c>YamlScalarResolver</c> — and diverged in rendering, because the roundtrip
/// renderer hardcoded double quotes where <c>safe_dump</c> and both Python oracles emit single
/// quotes. A shared predicate with unshared rendering still produces two different documents for
/// one value, which is the defect class the one-authority design exists to prevent. Since #1542
/// both surfaces take the whole decision from <see cref="YamlScalarStyleAuthority"/>.
/// </para>
///
/// <para>
/// Boundary measured against PyYAML 6.0.3 / python 3.12.13 on 2026-08-15. PyYAML single-quotes
/// everything it quotes — including newlines, leading and trailing spaces, and indicator
/// characters — and reaches for double quotes in exactly one case: the string contains a
/// character single quoting cannot carry, meaning a C0 control character other than newline, or
/// DEL.
/// </para>
/// <code>
/// 'true'         -> 'true'\n            'tab\tchar'    -> "tab\tchar"\n
/// 'a: b'         -> 'a: b'\n            '\x01ctl'      -> "\x01ctl"\n
/// '#hash'        -> '#hash'\n           'a\x7fdel'     -> "a\x7Fdel"\n
/// '  lead'       -> '  lead'\n          'x: \ty'       -> "x: \ty"\n
/// 'line1\nline2' -> 'line1\n\n  line2'\n
/// </code>
/// </summary>
public class YamlRoundtripQuotingStyleTests
{
    /// <summary>
    /// The corpus the cross-surface spy test was NARROWED away from while this issue was open —
    /// <c>"true"</c> and <c>"a: b"</c> were the two cells that failed. Widened here as well as in
    /// <c>yaml_module_tests.spy</c>, since the C# surface can assert the exact bytes.
    /// </summary>
    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("null")]
    [InlineData("yes")]
    [InlineData("no")]
    [InlineData("on")]
    [InlineData("off")]
    [InlineData("0x1A")]
    [InlineData("010")]
    [InlineData("1_000")]
    [InlineData("1.5")]
    [InlineData(".inf")]
    [InlineData(".nan")]
    [InlineData("~")]
    [InlineData("a: b")]
    [InlineData("#hash")]
    [InlineData("- dash")]
    [InlineData("  lead")]
    [InlineData("trail  ")]
    [InlineData("")]
    [InlineData("hello")]
    [InlineData("abc")]
    [InlineData("2024-01-01")]
    public void RoundtripDump_AgreesWithSafeDump_OverTheAmbiguousCorpus(string value)
    {
        Assert.Equal(Yaml.SafeDump(value), Yaml.RoundtripDump(value));
    }

    /// <summary>
    /// The single-quote cells stated as bytes rather than only as cross-surface equality, because
    /// equality alone would be satisfied by both surfaces being wrong together — the same
    /// self-consistency trap #1467 sat in.
    /// </summary>
    [Fact]
    public void BothSurfaces_SingleQuoteWhatPyYamlSingleQuotes()
    {
        Assert.Equal("'true'\n", Yaml.SafeDump("true"));
        Assert.Equal("'true'\n", Yaml.RoundtripDump("true"));

        Assert.Equal("'a: b'\n", Yaml.SafeDump("a: b"));
        Assert.Equal("'a: b'\n", Yaml.RoundtripDump("a: b"));

        Assert.Equal("'0x1A'\n", Yaml.RoundtripDump("0x1A"));
        Assert.Equal("'yes'\n", Yaml.RoundtripDump("yes"));
    }

    /// <summary>
    /// The other side of the boundary: a string carrying a control character cannot be
    /// single-quoted, so <c>roundtrip_dump</c> must reach for double quotes. Without this the fix
    /// would read as "always single-quote", which produces unparseable YAML for a tab.
    ///
    /// <para>
    /// Asserted on <c>roundtrip_dump</c> so the boundary is pinned against the oracle directly;
    /// since #1542 <c>safe_dump</c> reaches the same answer through the shared authority, and the
    /// cross-surface equality is asserted in
    /// <see cref="FormerResidue_NowAgreement_BothSurfacesConsultOneAuthority"/>.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("tab\tchar")]
    [InlineData("\u0001ctl")]
    [InlineData("a\u007Fdel")]
    [InlineData("x: \ty")]
    public void RoundtripDump_DoubleQuotesWhatSingleQuotingCannotCarry(string value)
    {
        Assert.StartsWith("\"", Yaml.RoundtripDump(value));
    }

    /// <summary>
    /// A newline is NOT an escape-requiring character — single quoting carries it — so it must
    /// stay on the single-quoted side of the boundary. This is the cell that stops the rule from
    /// being written as "anything non-printable", which would have moved multi-line strings too,
    /// and it matches PyYAML's <c>'line1\n\n  line2'</c> byte for byte.
    /// </summary>
    [Fact]
    public void ANewlineStaysOnTheSingleQuotedSide()
    {
        string dumped = Yaml.RoundtripDump("line1\nline2");

        Assert.DoesNotContain("\"", dumped);
        Assert.Equal("'line1\n\n  line2'\n", dumped);
    }

    /// <summary>
    /// The former cross-surface residue (#1542) is now AGREEMENT — both surfaces consult
    /// <see cref="YamlScalarStyleAuthority"/> and produce the same output. Converted from
    /// divergence pins to agreement assertions.
    /// </summary>
    [Fact]
    public void FormerResidue_NowAgreement_BothSurfacesConsultOneAuthority()
    {
        // 12:30 is plain on BOTH surfaces — Sharpy's resolver declines sexagesimal (#1465),
        // so the authority says plain. A deviation from PyYAML (which quotes it), not a bug.
        Assert.Equal(Yaml.SafeDump("12:30"), Yaml.RoundtripDump("12:30"));

        // a:b and a#b are both plain — no space before colon/hash.
        Assert.Equal(Yaml.SafeDump("a:b"), Yaml.RoundtripDump("a:b"));
        Assert.Equal(Yaml.SafeDump("a#b"), Yaml.RoundtripDump("a#b"));

        // tab is double-quoted on BOTH surfaces (matching PyYAML).
        Assert.Equal(Yaml.SafeDump("tab\tchar"), Yaml.RoundtripDump("tab\tchar"));
        Assert.StartsWith("\"", Yaml.SafeDump("tab\tchar"));

        // multi-line is single-quoted on BOTH surfaces (matching PyYAML).
        Assert.Equal(Yaml.SafeDump("line1\nline2"), Yaml.RoundtripDump("line1\nline2"));
    }

    /// <summary>
    /// Flow-mode dumps use the authority's FLOW rules: a comma is data in block context but an
    /// indicator inside a flow collection, so <c>safe_dump(..., default_flow_style=True)</c> must
    /// quote it — the styling decision belongs to <see cref="YamlScalarStyleAuthority"/>, not to
    /// YamlDotNet's internal emitter analysis.
    ///
    /// <para>
    /// Oracle: PyYAML 6.0.3, python3 3.12.13, measured 2026-08-20:
    /// <c>yaml.safe_dump({'k': 'a,b'}, default_flow_style=True)</c> → <c>"{k: 'a,b'}\n"</c>;
    /// block mode → <c>"k: a,b\n"</c> (plain).
    /// </para>
    /// </summary>
    [Fact]
    public void FlowModeDump_QuotesFlowIndicators_BlockModeDoesNot()
    {
        var data = new Dict<string, object?> { ["k"] = "a,b" };

        Assert.Equal("{k: 'a,b'}\n", Yaml.SafeDump(data, defaultFlowStyle: true));
        Assert.Equal("k: a,b\n", Yaml.SafeDump(data));
    }

    /// <summary>
    /// KEY-position styling through <c>safe_dump</c>: the converter styles every string,
    /// including mapping keys, so a forced style must survive YamlDotNet's key emission.
    ///
    /// <para>
    /// Oracle: PyYAML 6.0.3, python3 3.12.13, measured 2026-08-20 with
    /// <c>yaml.safe_dump({key: 1})</c>: tab key → <c>"tab\tkey": 1</c> (double-quoted);
    /// colon-space key → <c>'a: b': 1</c> (single-quoted); multi-line key →
    /// explicit-key form <c>? 'l1\n\n  l2'\n: 1</c>. The STYLE matches PyYAML at every
    /// cell; the one deviation is the escape SPELLING inside the double-quoted scalar —
    /// YamlDotNet emits the raw tab byte where PyYAML spells <c>\t</c> (#1598), key and
    /// value position alike, so the tab assertions below carry a literal tab.
    /// </para>
    /// </summary>
    [Fact]
    public void KeyPositionStyling_MatchesPyYaml()
    {
        Assert.Equal("\"tab\tkey\": 1\n", Yaml.SafeDump(new Dict<string, object?> { ["tab\tkey"] = 1 }));
        Assert.Equal("'a: b': 1\n", Yaml.SafeDump(new Dict<string, object?> { ["a: b"] = 1 }));
        Assert.Equal("? 'l1\n\n  l2'\n: 1\n", Yaml.SafeDump(new Dict<string, object?> { ["l1\nl2"] = 1 }));

        // Value position, exact bytes: same double-quoted style, same raw-tab spelling (#1598).
        Assert.Equal("\"tab\tchar\"\n", Yaml.SafeDump("tab\tchar"));
    }

    /// <summary>
    /// Whatever the quoting, the value survives the trip — the property that makes a rendering
    /// change safe rather than merely oracle-shaped.
    /// </summary>
    [Theory]
    [InlineData("true")]
    [InlineData("a: b")]
    [InlineData("yes")]
    [InlineData("tab\tchar")]
    [InlineData("line1\nline2")]
    [InlineData("")]
    public void QuotedScalars_RoundTripThroughSafeLoad(string value)
    {
        Assert.Equal(value, Yaml.SafeLoad(Yaml.RoundtripDump(value)));
        Assert.Equal(value, Yaml.SafeLoad(Yaml.SafeDump(value)));
    }
}
