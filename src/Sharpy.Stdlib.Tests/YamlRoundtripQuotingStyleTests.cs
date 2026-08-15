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
    /// The cross-surface disagreement that REMAINS after #1472, pinned so it is a known residue
    /// with an issue behind it rather than a gap — #1542.
    ///
    /// <para>
    /// #1472 was the RENDERING split, for strings both surfaces already agreed needed quoting.
    /// What is left is the PREDICATE split: <c>roundtrip_dump</c> asks <c>NeedsQuoting</c>'s
    /// character loop, which quotes on any <c>:</c> or <c>#</c>, while <c>safe_dump</c> delegates
    /// to YamlDotNet's scalar analysis. YAML's real rule is narrower (<c>": "</c> and
    /// <c>" #"</c>), so <c>roundtrip_dump</c> over-quotes; and in the other direction
    /// <c>safe_dump</c> emits tabs plain and multi-line strings as folded blocks where PyYAML
    /// quotes. Both are wider than this issue — closing them changes the shape of every
    /// multi-line string in every dumped document.
    /// </para>
    ///
    /// <para>
    /// Pinned as assertions rather than left as prose so #1542 flips loudly instead of becoming
    /// folklore — the discipline that made #1467 and #1471 visible in the first place.
    /// </para>
    /// </summary>
    [Fact]
    public void TheCrossSurfaceResidueOutside1472sCorpus_IsPinnedTo1542()
    {
        // roundtrip_dump over-quotes a bare colon; safe_dump and PyYAML leave it plain. (PyYAML
        // quotes `12:30` only because it resolves sexagesimal, which Sharpy declines — #1465 —
        // so plain is Sharpy's correct answer and roundtrip_dump is the odd one out.)
        Assert.Equal("12:30\n...\n", Yaml.SafeDump("12:30"));
        Assert.Equal("'12:30'\n", Yaml.RoundtripDump("12:30"));

        // safe_dump emits a raw tab plain; PyYAML and roundtrip_dump double-quote it.
        Assert.Equal("tab\tchar\n...\n", Yaml.SafeDump("tab\tchar"));

        // safe_dump folds a multi-line string into a block scalar; PyYAML single-quotes it.
        Assert.Equal(">-\n  line1\n\n  line2\n", Yaml.SafeDump("line1\nline2"));
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
