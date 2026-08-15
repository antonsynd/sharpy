using Xunit;

namespace Sharpy.Stdlib.Tests;

/// <summary>
/// <c>safe_dump</c>'s output for the two values whose emitted scalar is empty or absent — the
/// empty string and null — where Sharpy used to diverge from PyYAML (#1467).
///
/// <para>
/// FLIPPED. These assertions previously pinned the divergence (<c>--- ''\n</c> and <c>--- \n</c>);
/// they now carry PyYAML's answers (<c>''\n</c> and <c>null\n...\n</c>). The flip is the
/// deliverable. Both cells move together, deliberately: the issue's acceptance is two separate
/// bullets, and the null case is the worse half — a conforming parser reads <c>--- \n</c> as an
/// EMPTY DOCUMENT, so the value was not merely spelled oddly, it was gone.
/// </para>
///
/// <para>
/// The reason the round-trip property could never have caught this is worth keeping: Sharpy read
/// its own <c>--- \n</c> back as null perfectly well, so <c>safe_load(safe_dump(x)) == x</c> held
/// for both cells while neither agreed with PyYAML. Two halves agreeing with each other and
/// neither with the oracle is not parity evidence.
/// </para>
///
/// <para>
/// Cells measured against PyYAML 6.0.3 / python 3.12.13, 2026-08-15.
/// </para>
/// </summary>
public class YamlEmptyScalarDumpTests
{
    /// <summary>
    /// PyYAML emits <c>''</c>. Sharpy used to prefix an explicit document-start marker, because
    /// YamlDotNet's emitter forces one when the document's single scalar has an EMPTY VALUE
    /// (<c>Emitter.CheckEmptyDocument</c>) — a decision made from the event stream, so no choice
    /// of scalar style could avoid it. Distinct from #1348, which is the document-END marker.
    /// </summary>
    [Fact]
    public void SafeDump_EmptyString_EmitsAQuotedEmptyScalarWithNoDocumentStart()
    {
        Assert.Equal("''\n", Yaml.SafeDump(""));
    }

    /// <summary>
    /// The worse half of #1467: PyYAML emits <c>null\n...\n</c> and Sharpy emitted the marker and
    /// NO VALUE. Fixed by spelling null explicitly as a plain scalar; the <c>...</c> then arrives
    /// through #1348's existing rule rather than being appended by hand, which is why this cell
    /// also proves the two rules compose.
    /// </summary>
    [Fact]
    public void SafeDump_Null_EmitsThePlainScalarNullAndTheDocumentEndMarker()
    {
        Assert.Equal("null\n...\n", Yaml.SafeDump(null));
    }

    /// <summary>
    /// Controls: every other member of the family already matched PyYAML, which is what scoped
    /// #1467 to the empty/absent scalar rather than to document emission generally. These must
    /// NOT have moved.
    /// </summary>
    [Fact]
    public void SafeDump_NonEmptyScalarsAndEmptyCollections_AlreadyMatchPyYaml()
    {
        Assert.Equal("' '\n", Yaml.SafeDump(" "));
        Assert.Equal("[]\n", Yaml.SafeDump(new List<object?>()));
        Assert.Equal("{}\n", Yaml.SafeDump(new Dict<string, object?>()));
    }

    /// <summary>
    /// The same two values on the OTHER dump surface. <c>roundtrip_dump</c> drives YamlDotNet's
    /// <c>Emitter</c> directly rather than through a serializer, so it meets
    /// <c>CheckEmptyDocument</c> on its own account — the defect was never <c>safe_dump</c>'s
    /// alone, and fixing one surface would have left the two disagreeing. Same parallel-site rule
    /// that gave #1348 a single <c>YamlDocumentEnd</c> authority (#1145).
    /// </summary>
    /// <para>
    /// Asserted as "carries no document-start marker" rather than as byte equality with
    /// <c>safe_dump</c>, because the two surfaces still differ on the QUOTE CHARACTER here —
    /// <c>""</c> against <c>''</c> — which is #1472 and not this issue. Byte equality is #1472's
    /// own acceptance and is asserted there, in
    /// <c>YamlRoundtripQuotingStyleTests</c>. Splitting them keeps this cell falsifiable by the
    /// defect it names.
    /// </para>
    [Fact]
    public void RoundtripDump_EmptyAndNull_CarryNoDocumentStartMarker()
    {
        Assert.DoesNotContain("---", Yaml.RoundtripDump(""));
        Assert.DoesNotContain("---", Yaml.RoundtripDump(null));

        // Null is spelled, not omitted, on this surface too.
        Assert.Equal("null\n...\n", Yaml.RoundtripDump(null));
    }

    /// <summary>
    /// The new spellings load back to the values they came from — the property that makes the
    /// flip safe to ship rather than merely oracle-shaped.
    /// </summary>
    [Fact]
    public void TheNewSpellings_RoundTrip()
    {
        Assert.Equal("", Yaml.SafeLoad(Yaml.SafeDump("")));
        Assert.Null(Yaml.SafeLoad(Yaml.SafeDump(null)));
    }

    /// <summary>
    /// BACKWARD COMPATIBILITY. Documents written by older Sharpy exist on disk, and changing what
    /// the dumper WRITES must not change what the loader ACCEPTS. Both retired spellings must
    /// still read back as the values they encoded.
    /// </summary>
    [Fact]
    public void TheRetiredSpellings_StillLoadCorrectly()
    {
        Assert.Equal("", Yaml.SafeLoad("--- ''\n"));
        Assert.Null(Yaml.SafeLoad("--- \n"));

        // And the marker-free variants of each, which the pre-#1348 dumper produced.
        Assert.Equal("", Yaml.SafeLoad("''\n"));
        Assert.Null(Yaml.SafeLoad("null\n"));
        Assert.Null(Yaml.SafeLoad("null\n...\n"));
    }
}
