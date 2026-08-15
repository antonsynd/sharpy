using Xunit;

namespace Sharpy.Stdlib.Tests;

/// <summary>
/// The measured PyYAML cell table for the YAML 1.1 scalar families <see cref="Yaml.SafeLoad"/>
/// types (#1465), and the negative controls that bound each family. Written while they were still
/// unimplemented, so the pending cells here were flipped to these answers as each family landed.
///
/// <para>
/// Every cell below was MEASURED, not recalled — <c>python3 -c "import yaml; ..."</c> against
/// <b>PyYAML 6.0.3 / python 3.12.13</b>, run 2026-08-15. That matters because the issue text's
/// family summaries are wrong in three places, and each error would have shipped as a bug had the
/// regexes been written from the prose: PyYAML's octal is <b>leading-zero</b> (<c>010</c> → 8) and
/// it rejects <c>0o10</c>; PyYAML <b>does</b> resolve <c>0b</c> binary, which the issue omits; and
/// single-letter <c>y</c>/<c>n</c> are <b>not</b> bools (that is YAML 1.1 the spec, not PyYAML the
/// implementation). The oracle wins over the issue; #1465 carries a correcting comment.
/// </para>
///
/// <para>PyYAML's own implicit-resolver regexes, read out of
/// <c>yaml.resolver.Resolver.yaml_implicit_resolvers</c> at the same version — these, not a
/// paraphrase of them, are what the resolver arms are derived from:</para>
/// <code>
/// bool:  ^(?:yes|Yes|YES|no|No|NO|true|True|TRUE|false|False|FALSE|on|On|ON|off|Off|OFF)$
/// int:   ^(?:[-+]?0b[0-1_]+
///          |[-+]?0[0-7_]+
///          |[-+]?(?:0|[1-9][0-9_]*)
///          |[-+]?0x[0-9a-fA-F_]+
///          |[-+]?[1-9][0-9_]*(?::[0-5]?[0-9])+)$
/// float: ^(?:[-+]?(?:[0-9][0-9_]*)\.[0-9_]*(?:[eE][-+][0-9]+)?
///          |\.[0-9][0-9_]*(?:[eE][-+][0-9]+)?
///          |[-+]?[0-9][0-9_]*(?::[0-5]?[0-9])+\.[0-9_]*
///          |[-+]?\.(?:inf|Inf|INF)|\.(?:nan|NaN|NAN))$
/// </code>
///
/// <para>Measured cells (<c>cells @ PyYAML 6.0.3 (measured)</c>):</para>
/// <code>
/// BOOL     yes Yes YES no No NO on On ON off Off OFF  -> bool
///          yEs y Y n N oN tRue                        -> str   (casing is all-or-nothing)
/// HEX      0x1A -&gt; 26   -0x1a -&gt; -26   +0x1A -&gt; 26   0x_1A -&gt; 26   0x1_A -&gt; 26
///          0X1a -&gt; str (capital X rejected)           0x -&gt; str
/// OCTAL    010 -&gt; 8     -010 -&gt; -8     0_10 -&gt; 8     00 -&gt; 0
///          08 -&gt; str (8 is not an octal digit)        0o10 -&gt; str (1.1 has no 0o)
/// BINARY   0b101 -&gt; 5   -0b101 -&gt; -5   0b1_01 -&gt; 5
///          0B101 -&gt; str (capital B rejected)          0b -&gt; str
/// UNDERSCR 1_000 -&gt; 1000  1_000_000 -&gt; 1000000  +1_000 -&gt; 1000  -1_0 -&gt; -10  1_ -&gt; 1
///          _1 -&gt; str (a leading underscore is not a digit)
///          1_0.5 -&gt; 10.5   1_0.5_5 -&gt; 10.55   (the float mantissa takes them too)
/// SEXAGES  12:30 -&gt; 750   -12:30 -&gt; -750   1:2:3 -&gt; 3723   (DECLINED for Sharpy, below)
/// DATE     2024-01-01 -&gt; datetime.date                      (DEFERRED, below)
/// </code>
/// </summary>
public class YamlScalarResolverFamilyTests
{
    /// <summary>
    /// The bound on the bool family: PyYAML accepts exactly three casings per word — all-lower,
    /// Title, all-UPPER — and nothing else. Both oracles agree here already, so these cells must
    /// NOT move when the family lands; they are what stops the fix from being written as a
    /// case-insensitive compare, which would wrongly claim <c>yEs</c>.
    /// </summary>
    [Fact]
    public void BoolFamily_MixedCasingAndSingleLetters_AreStringsInBothPyYamlAndSharpy()
    {
        foreach (string cell in new[] { "yEs", "y", "Y", "n", "N", "oN", "tRue", "oFF" })
        {
            Assert.Equal(cell, Yaml.SafeLoad(cell));
        }
    }

    /// <summary>
    /// The bound on the int families: spellings that LOOK like a member but are not one. Each is a
    /// string in PyYAML too, so like the casing controls these are agreement cells that must hold
    /// before and after #1465 — they are what stops the hex/octal/binary arms from being written
    /// too wide.
    ///
    /// <para>
    /// <c>08</c> belongs to this list by PyYAML's rule and is asserted alongside the octal family
    /// instead, because it used to come back as the integer 8 — see
    /// <see cref="LeadingZeroOctal_ReadsAsOctal"/>.
    /// </para>
    /// </summary>
    [Fact]
    public void IntFamilies_NearMissSpellings_AreStringsInBothPyYamlAndSharpy()
    {
        foreach (string cell in new[] { "0X1a", "0B101", "0o10", "0x", "0b", "_1" })
        {
            Assert.Equal(cell, Yaml.SafeLoad(cell));
        }
    }

    /// <summary>
    /// #1465 — the half of the octal family that was a VALUE change rather than an addition, and
    /// the reason this family could not be implemented as "add an arm and leave the rest alone".
    ///
    /// <para>
    /// The decimal arm used to be <c>long.TryParse(value, NumberStyles.AllowLeadingSign)</c>,
    /// which is happy to read a leading zero and happy to read <c>8</c> and <c>9</c> under one.
    /// So Sharpy answered <c>010</c> with <b>10</b> where PyYAML answers <b>8</b>, and <c>08</c>
    /// with <b>8</b> where PyYAML answers the <b>string</b>. Both were silent wrong-value
    /// divergences rather than missing-feature ones — worse than the gap #1465 describes, and
    /// invisible to any test that only asks "did it come back a number".
    /// </para>
    ///
    /// <para>
    /// PyYAML's decimal arm is <c>[-+]?(?:0|[1-9][0-9_]*)</c>: a leading zero puts the scalar in
    /// the OCTAL arm or nowhere. FLIPPED to that answer.
    /// </para>
    /// </summary>
    [Fact]
    public void LeadingZeroOctal_ReadsAsOctal()
    {
        Assert.Equal(8, Yaml.SafeLoad("010"));
        Assert.Equal(-8, Yaml.SafeLoad("-010"));
        Assert.Equal(10, Yaml.SafeLoad("0012"));

        // Not an octal at all, so not a number: `8` is not an octal digit.
        Assert.Equal("08", Yaml.SafeLoad("08"));
        Assert.Equal("0_8", Yaml.SafeLoad("0_8"));
    }

    /// <summary>
    /// #1465 — the two cases where a scalar the regex admits still cannot be a Sharpy integer, and
    /// so falls through to the string. Both are stated positions, not accidents.
    ///
    /// <para>
    /// <b>Overflow.</b> Python's ints are arbitrary precision; PyYAML answers
    /// <c>0xFFFFFFFFFFFFFFFF</c> with 18446744073709551615. Falling through to the string is the
    /// rule the decimal path already followed — a 20-digit decimal has always come back a string
    /// here — so the behaviour stays uniform across the families instead of gaining a per-radix
    /// exception. <c>long.MinValue</c> is the boundary that proves the accumulator is unsigned:
    /// its magnitude is one past <c>long.MaxValue</c>.
    /// </para>
    ///
    /// <para>
    /// <b>Nothing left after the separators.</b> <c>0x_</c> matches PyYAML's own regex and then
    /// crashes its constructor — measured, <c>ValueError: invalid literal for int() with base 16:
    /// ''</c>. Sharpy reads the string instead of propagating another library's internal
    /// inconsistency as an exception out of <c>safe_load</c>.
    /// </para>
    /// </summary>
    [Fact]
    public void IntFamilies_UnrepresentableScalars_FallThroughToTheString()
    {
        Assert.Equal("0xFFFFFFFFFFFFFFFF", Yaml.SafeLoad("0xFFFFFFFFFFFFFFFF"));
        Assert.Equal("99999999999999999999", Yaml.SafeLoad("99999999999999999999"));
        Assert.Equal("0x_", Yaml.SafeLoad("0x_"));
        Assert.Equal("0b_", Yaml.SafeLoad("0b_"));

        // The boundary the unsigned accumulator exists for, and the one either side of it.
        Assert.Equal(long.MinValue, Yaml.SafeLoad("-9223372036854775808"));
        Assert.Equal(long.MaxValue, Yaml.SafeLoad("9223372036854775807"));
        Assert.Equal("9223372036854775808", Yaml.SafeLoad("9223372036854775808"));
    }

    /// <summary>
    /// #1465 — the bool family, FLIPPED from the pinned divergence to PyYAML's answer. Every cell
    /// is one of the twelve spellings measured off PyYAML's own resolver regex.
    /// </summary>
    [Fact]
    public void BoolFamily_YesNoOnOff_ResolveAsBools()
    {
        foreach (string cell in new[] { "yes", "Yes", "YES", "on", "On", "ON" })
        {
            Assert.Equal(true, Yaml.SafeLoad(cell));
        }
        foreach (string cell in new[] { "no", "No", "NO", "off", "Off", "OFF" })
        {
            Assert.Equal(false, Yaml.SafeLoad(cell));
        }
    }

    /// <summary>
    /// The integration proof that the resolver is the SINGLE authority (#1339/#1417 design): the
    /// bool family was added to <c>YamlScalarResolver.Resolve</c> and NOWHERE else, yet dump-side
    /// quoting and the document-end marker both follow. <c>safe_dump("yes")</c> must now quote —
    /// otherwise it would emit a document that reads back as a bool — and a quoted scalar takes no
    /// <c>...</c> marker under #1348's rule, so the marker disappears in the same step.
    ///
    /// <para>
    /// Asserted here even though nothing in the dump path changed, because "it follows for free"
    /// is a claim about a design and claims about designs are what regress silently. Cells match
    /// PyYAML 6.0.3: <c>safe_dump('yes')</c> → <c>'yes'\n</c>.
    /// </para>
    /// </summary>
    [Fact]
    public void BoolFamily_DumpQuotesTheStringsAndDropsTheMarker()
    {
        Assert.Equal("'yes'\n", Yaml.SafeDump("yes"));
        Assert.Equal("'no'\n", Yaml.SafeDump("no"));
        Assert.Equal("'on'\n", Yaml.SafeDump("on"));
        Assert.Equal("'OFF'\n", Yaml.SafeDump("OFF"));

        // The control that gives those their meaning: a casing PyYAML does NOT resolve stays
        // plain and keeps its marker, so the quoting tracks the resolver rather than the word.
        Assert.Equal("yEs\n...\n", Yaml.SafeDump("yEs"));

        // And the round trip the quoting exists to protect.
        Assert.Equal("yes", Yaml.SafeLoad(Yaml.SafeDump("yes")));
    }

    /// <summary>
    /// #1465 — the int families FLIPPED to PyYAML's answers: hex, binary, the underscored octal,
    /// and the underscore separator on plain decimals. Values, not just types: a test that only
    /// asked "is it an int" would pass on <c>0x1A</c> → 1 as readily as on 26.
    /// </summary>
    [Fact]
    public void IntFamilies_HexBinaryUnderscore_ResolveToTheMeasuredIntegers()
    {
        Assert.Equal(26, Yaml.SafeLoad("0x1A"));
        Assert.Equal(-26, Yaml.SafeLoad("-0x1a"));
        Assert.Equal(26, Yaml.SafeLoad("+0x1A"));
        Assert.Equal(26, Yaml.SafeLoad("0x_1A"));
        Assert.Equal(26, Yaml.SafeLoad("0x1_A"));

        Assert.Equal(8, Yaml.SafeLoad("0_10"));

        Assert.Equal(5, Yaml.SafeLoad("0b101"));
        Assert.Equal(-5, Yaml.SafeLoad("-0b101"));
        Assert.Equal(5, Yaml.SafeLoad("0b1_01"));

        Assert.Equal(1000, Yaml.SafeLoad("1_000"));
        Assert.Equal(1000000, Yaml.SafeLoad("1_000_000"));
        Assert.Equal(1000, Yaml.SafeLoad("+1_000"));
        Assert.Equal(-10, Yaml.SafeLoad("-1_0"));

        // PyYAML strips separators wholesale rather than validating placement, so these are
        // permissive in exactly the same way it is (measured).
        Assert.Equal(1, Yaml.SafeLoad("1_"));
        Assert.Equal(10, Yaml.SafeLoad("1__0"));
        Assert.Equal(0, Yaml.SafeLoad("0_"));
    }

    /// <summary>
    /// #1465 — the dump side of the int families, which no line in the dump path was changed to
    /// produce. Same integration proof as the bool family: quoting keys on the resolver (#1417),
    /// so a string that would read back as a number is now quoted, and a quoted scalar drops the
    /// <c>...</c> marker under #1348. Cells match PyYAML 6.0.3.
    /// </summary>
    [Fact]
    public void IntFamilies_DumpQuotesTheStringsAndDropsTheMarker()
    {
        Assert.Equal("'0x1A'\n", Yaml.SafeDump("0x1A"));
        Assert.Equal("'1_000'\n", Yaml.SafeDump("1_000"));
        Assert.Equal("'010'\n", Yaml.SafeDump("010"));

        // Control: a near-miss the resolver does NOT claim stays plain and keeps its marker.
        Assert.Equal("0X1a\n...\n", Yaml.SafeDump("0X1a"));
        Assert.Equal("08\n...\n", Yaml.SafeDump("08"));

        Assert.Equal("0x1A", Yaml.SafeLoad(Yaml.SafeDump("0x1A")));
        Assert.Equal("010", Yaml.SafeLoad(Yaml.SafeDump("010")));
    }

    /// <summary>
    /// #1465 — the underscore separator reaches the FLOAT mantissa too, which the issue text does
    /// not mention and the probe found. <c>YamlScalarResolver.Yaml11Float</c> already recorded the
    /// omission as deferred to this issue, so it lands with the int families rather than as a
    /// separate decision. FLIPPED.
    /// </summary>
    [Fact]
    public void FloatMantissa_UnderscoreSeparators_ResolveToFloats()
    {
        Assert.Equal(10.5, Yaml.SafeLoad("1_0.5"));
        Assert.Equal(10.55, Yaml.SafeLoad("1_0.5_5"));
        Assert.Equal(0.55, Yaml.SafeLoad(".5_5"));
        Assert.Equal(1.5, Yaml.SafeLoad("1_.5"));
        Assert.Equal(1000.5, Yaml.SafeLoad("1_000.5"));
        Assert.Equal(0.01, Yaml.SafeLoad(".0_1"));

        // The leading-dot arm still requires a DIGIT first, separator or not.
        Assert.Equal("._5", Yaml.SafeLoad("._5"));
    }

    /// <summary>
    /// The families PyYAML resolves and Sharpy deliberately will NOT — see the decision record on
    /// <c>YamlScalarResolver.Resolve</c>. Pinned so the divergence is a stated position with a
    /// test behind it rather than a gap: sexagesimal is DECLINED (owner ruling 2026-08-13; YAML
    /// 1.2 dropped the type outright), timestamp is DEFERRED behind a target-type decision.
    ///
    /// <para>
    /// A unit cell rather than a differential-execution allowlist entry, deliberately: an
    /// allowlist entry says "this ought to agree and does not yet", and drains. This is a
    /// permanent stated position about load-side resolution, so it belongs in a test that asserts
    /// it, not in a ledger that expects it to go away.
    /// </para>
    /// </summary>
    [Fact]
    public void SexagesimalAndTimestamp_AreDeliberatelyNotResolved()
    {
        // PyYAML: 750, -750, 3723. Sharpy: strings, by decision.
        Assert.Equal("12:30", Yaml.SafeLoad("12:30"));
        Assert.Equal("-12:30", Yaml.SafeLoad("-12:30"));
        Assert.Equal("1:2:3", Yaml.SafeLoad("1:2:3"));

        // PyYAML: datetime.date(2024, 1, 1). Sharpy: a string, pending the target-type decision.
        Assert.Equal("2024-01-01", Yaml.SafeLoad("2024-01-01"));
    }

    /// <summary>
    /// The families Sharpy ALREADY agrees with PyYAML on, present so a widening of the new arms
    /// that swallowed one of them would fail here. <c>00</c> is the interesting cell: it is not a
    /// decimal (PyYAML's decimal arm is <c>0|[1-9][0-9_]*</c>) but IS an octal, so it resolves to
    /// 0 by a different arm than <c>0</c> does.
    /// </summary>
    [Fact]
    public void EstablishedFamilies_AgreeWithPyYamlToday()
    {
        Assert.Equal(0, Yaml.SafeLoad("0"));
        Assert.Equal(0, Yaml.SafeLoad("00"));
        Assert.Equal(12, Yaml.SafeLoad("+12"));
        Assert.Equal(-12, Yaml.SafeLoad("-12"));
        Assert.Equal(true, Yaml.SafeLoad("true"));
        Assert.Equal(false, Yaml.SafeLoad("False"));
        Assert.Null(Yaml.SafeLoad("null"));
        Assert.Null(Yaml.SafeLoad("~"));
        Assert.Equal(0.5, Yaml.SafeLoad(".5"));
        Assert.Equal("+.5", Yaml.SafeLoad("+.5"));
        Assert.Equal("1e-7", Yaml.SafeLoad("1e-7"));
        Assert.Equal(10000000.0, Yaml.SafeLoad("1.0e+7"));
    }
}
