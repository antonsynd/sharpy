using Xunit;

namespace Sharpy.Stdlib.Tests;

/// <summary>
/// The two dump surfaces agree on HOW to quote, not merely on WHICH strings need quoting (#1472).
///
/// <para>
/// They already shared the predicate — <c>YamlRoundtrip.NeedsQuoting</c> and
/// <c>YamlAmbiguousStringTypeConverter</c> both ask <c>YamlScalarResolver</c> — and diverged only
/// in rendering, because <c>QuotedScalar</c> hardcoded <c>DoubleQuoted</c> where <c>safe_dump</c>
/// and both Python oracles emit single quotes. A shared predicate with unshared rendering still
/// produces two different documents for one value, which is the defect class the one-authority
/// design exists to prevent.
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
    /// Asserted on <c>roundtrip_dump</c> alone rather than as cross-surface equality, because
    /// <c>safe_dump</c> emits a tab PLAIN and raw — it diverges from PyYAML here, and
    /// <c>roundtrip_dump</c> is now the surface that agrees with the oracle. Filed as #1542 and
    /// pinned in <see cref="TheCrossSurfaceResidueOutside1472sCorpus_IsPinnedTo1542"/>.
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
