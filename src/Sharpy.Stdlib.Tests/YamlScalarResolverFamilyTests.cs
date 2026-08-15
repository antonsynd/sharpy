using Xunit;

namespace Sharpy.Stdlib.Tests;

/// <summary>
/// The measured PyYAML cell table for the YAML 1.1 scalar families <see cref="Yaml.SafeLoad"/>
/// does not yet type (#1465), and the negative controls that bound each family.
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
    /// <c>08</c> belongs to this list by PyYAML's rule and is NOT in it, because Sharpy reads it as
    /// the integer 8 today — see
    /// <see cref="LeadingZeroOctal_IsReadAsDecimalToday_Pending1465"/>.
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
    /// PENDING #1465 — the half of the octal family that is a VALUE change rather than an addition,
    /// and the reason this family cannot be implemented as "add an arm and leave the rest alone".
    ///
    /// <para>
    /// The existing decimal arm is <c>long.TryParse(value, NumberStyles.AllowLeadingSign)</c>,
    /// which is happy to read a leading zero and happy to read <c>8</c> and <c>9</c> under one.
    /// So Sharpy today answers <c>010</c> with <b>10</b> where PyYAML answers <b>8</b>, and
    /// <c>08</c> with <b>8</b> where PyYAML answers the <b>string</b>. Both are silent
    /// wrong-value divergences rather than missing-feature ones — worse than the gap #1465
    /// describes, and invisible to any test that only asks "did it come back a number".
    /// </para>
    ///
    /// <para>
    /// PyYAML's decimal arm is <c>[-+]?(?:0|[1-9][0-9_]*)</c>: a leading zero puts the scalar in
    /// the OCTAL arm or nowhere. When the family lands, these three cells become 8, -8 and the
    /// string <c>"08"</c>.
    /// </para>
    /// </summary>
    [Fact]
    public void LeadingZeroOctal_IsReadAsDecimalToday_Pending1465()
    {
        Assert.Equal(10, Yaml.SafeLoad("010"));
        Assert.Equal(-10, Yaml.SafeLoad("-010"));
        Assert.Equal(8, Yaml.SafeLoad("08"));
    }

    /// <summary>
    /// PENDING #1465 — pins the CURRENT divergence for the bool family. PyYAML reads every cell
    /// here as a bool; Sharpy reads a string. These assertions FLIP to <c>true</c>/<c>false</c>
    /// when the family lands — that flip is the deliverable, not a regression.
    /// </summary>
    [Fact]
    public void BoolFamily_YesNoOnOff_StillResolveAsStrings_Pending1465()
    {
        foreach (string cell in new[] { "yes", "Yes", "YES", "on", "On", "ON" })
        {
            Assert.Equal(cell, Yaml.SafeLoad(cell));
        }
        foreach (string cell in new[] { "no", "No", "NO", "off", "Off", "OFF" })
        {
            Assert.Equal(cell, Yaml.SafeLoad(cell));
        }
    }

    /// <summary>
    /// PENDING #1465 — pins the CURRENT divergence for the int families (hex, binary, underscore
    /// separators, and the underscored octal). PyYAML reads every cell here as an int; Sharpy's
    /// <c>long.TryParse</c> arm rejects each one, so each comes back the original string. These
    /// flip to the measured integers when the families land. The leading-zero octal cells that
    /// today resolve as DECIMAL are pinned separately — see
    /// <see cref="LeadingZeroOctal_IsReadAsDecimalToday_Pending1465"/>.
    /// </summary>
    [Fact]
    public void IntFamilies_HexOctalBinaryUnderscore_StillResolveAsStrings_Pending1465()
    {
        foreach (string cell in new[]
                 {
                     "0x1A", "-0x1a", "+0x1A", "0x_1A", "0x1_A",
                     "0_10",
                     "0b101", "-0b101", "0b1_01",
                     "1_000", "1_000_000", "+1_000", "-1_0",
                 })
        {
            Assert.Equal(cell, Yaml.SafeLoad(cell));
        }
    }

    /// <summary>
    /// PENDING #1465 — the underscore separator reaches the FLOAT mantissa too, which the issue
    /// text does not mention and the probe found: PyYAML reads <c>1_0.5</c> as 10.5. Sharpy's
    /// float regex documents the omission as deferred to this issue
    /// (<c>YamlScalarResolver.Yaml11Float</c>), so it flips with the int family rather than being
    /// a separate decision.
    /// </summary>
    [Fact]
    public void FloatMantissa_UnderscoreSeparators_StillResolveAsStrings_Pending1465()
    {
        Assert.Equal("1_0.5", Yaml.SafeLoad("1_0.5"));
        Assert.Equal("1_0.5_5", Yaml.SafeLoad("1_0.5_5"));
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
