using Xunit;

namespace Sharpy.Stdlib.Tests;

/// <summary>
/// <c>safe_dump_all</c>'s multi-document stream shape, matched to PyYAML (#1471).
///
/// <para>
/// FLIPPED. These assertions previously pinned the shape #1348 deliberately excluded from the
/// per-document end-marker rule — <c>1.0\n---\nhello\n</c>, a free-standing separator and no
/// marker at all. They now carry PyYAML's answers. The exclusion was right about the danger it
/// named (per-document markers would have produced <c>1.0\n...\n---\nhello\n...\n</c>, markers
/// BETWEEN documents, which is nobody's shape) and wrong about the remedy.
/// </para>
///
/// <para>
/// The finding that resolved it, and the reason this did not need a new rule: <b>PyYAML's "one
/// marker at stream end" IS #1348's per-document rule applied to the last document</b>. So the
/// marker question is still asked in exactly one place — <c>YamlDocumentEnd</c> — and
/// <c>SafeDumpAll</c> only decides which document gets asked, plus how the separator folds.
/// </para>
///
/// <para>
/// All shapes below measured against PyYAML 6.0.3 / python 3.12.13 on 2026-08-15; the full
/// 19-shape table is recorded on #1471.
/// </para>
/// </summary>
public class YamlDumpAllStreamShapeTests
{
    /// <summary>
    /// The headline cell: an inline separator and exactly one marker, at stream end.
    /// </summary>
    [Fact]
    public void SafeDumpAll_ScalarDocuments_FoldTheSeparatorInlineAndEndTheStreamOnce()
    {
        var documents = new List<object?> { 1.0, "hello" };

        Assert.Equal("1.0\n--- hello\n...\n", Yaml.SafeDumpAll(documents));
    }

    /// <summary>
    /// Three of them, so the "exactly one marker" claim is falsifiable by a middle document
    /// acquiring one — the failure mode #1348 named and refused to ship.
    /// </summary>
    [Fact]
    public void SafeDumpAll_ThreeScalars_CarryOneMarkerNotThree()
    {
        var documents = new List<object?> { 1, 2, 3 };

        Assert.Equal("1\n--- 2\n--- 3\n...\n", Yaml.SafeDumpAll(documents));
    }

    /// <summary>
    /// The control that gives the assertions above their meaning: each of those documents dumped
    /// ON ITS OWN carries the marker. The stream path no longer suppresses the rule — it applies
    /// it to the last document only.
    /// </summary>
    [Fact]
    public void SafeDump_OfEachDocumentAlone_CarriesTheMarker()
    {
        Assert.Equal("1.0\n...\n", Yaml.SafeDump(1.0));
        Assert.Equal("hello\n...\n", Yaml.SafeDump("hello"));
    }

    /// <summary>
    /// Where "one document" and "one stream" coincide, and the case an implementer of #1471 has
    /// to decide explicitly: PyYAML agrees with its own <c>safe_dump</c> here, and so does Sharpy.
    /// </summary>
    [Fact]
    public void SafeDumpAll_SingleScalarDocument_AgreesWithSafeDump()
    {
        Assert.Equal("1.0\n...\n", Yaml.SafeDumpAll(new List<object?> { 1.0 }));
        Assert.Equal(Yaml.SafeDump(1.0), Yaml.SafeDumpAll(new List<object?> { 1.0 }));
        Assert.Equal(Yaml.SafeDump("hello"), Yaml.SafeDumpAll(new List<object?> { "hello" }));
    }

    /// <summary>
    /// BLOCK collections put their content on the line AFTER the separator, and carry no marker.
    /// This is the cell that disproves the tempting rule "fold inline when the document is one
    /// line": <c>b: 2</c> is one line and still gets its own.
    /// </summary>
    [Fact]
    public void SafeDumpAll_BlockCollections_PutTheSeparatorOnItsOwnLine()
    {
        var first = new Dict<string, object?>();
        first["a"] = 1;
        var second = new Dict<string, object?>();
        second["b"] = 2;

        Assert.Equal("a: 1\n---\nb: 2\n", Yaml.SafeDumpAll(new List<object?> { first, second }));

        var seqFirst = new List<object?> { 1, 2 };
        var seqSecond = new List<object?> { 3 };
        Assert.Equal("- 1\n- 2\n---\n- 3\n", Yaml.SafeDumpAll(new List<object?> { seqFirst, seqSecond }));
    }

    /// <summary>
    /// FLOW collections fold inline, which is what makes the rule block-vs-flow rather than
    /// scalar-vs-collection. Empty collections are always emitted flow, so they fold inline even
    /// with <c>defaultFlowStyle</c> off.
    /// </summary>
    [Fact]
    public void SafeDumpAll_FlowCollections_FoldTheSeparatorInline()
    {
        Assert.Equal("x\n--- []\n", Yaml.SafeDumpAll(new List<object?> { "x", new List<object?>() }));
        Assert.Equal("x\n--- {}\n", Yaml.SafeDumpAll(new List<object?> { "x", new Dict<string, object?>() }));

        var first = new Dict<string, object?>();
        first["a"] = 1;
        var second = new Dict<string, object?>();
        second["b"] = 2;
        Assert.Equal(
            "{a: 1}\n--- {b: 2}\n",
            Yaml.SafeDumpAll(new List<object?> { first, second }, defaultFlowStyle: true));
    }

    /// <summary>
    /// The stream-end marker follows the LAST document's own kind, which is the whole content of
    /// the rule. A quoted scalar last means NO marker even though the stream ends in a scalar —
    /// the same style-not-value distinction <c>YamlDocumentEnd</c> already makes.
    /// </summary>
    [Fact]
    public void SafeDumpAll_TheEndMarkerFollowsTheLastDocumentsKind()
    {
        // Plain scalar last -> marker.
        Assert.Equal("x\n--- null\n...\n", Yaml.SafeDumpAll(new List<object?> { "x", null }));

        // Quoted scalar last -> no marker. `yes` and `` are quoted because the resolver claims
        // them (#1417/#1465), which is what makes these cells interesting rather than arbitrary.
        Assert.Equal("1\n--- 'yes'\n", Yaml.SafeDumpAll(new List<object?> { 1, "yes" }));
        Assert.Equal("x\n--- ''\n", Yaml.SafeDumpAll(new List<object?> { "x", "" }));

        // Block collection last -> no marker.
        var mapping = new Dict<string, object?>();
        mapping["a"] = 1;
        Assert.Equal("x\n---\na: 1\n", Yaml.SafeDumpAll(new List<object?> { "x", mapping }));
    }

    /// <summary>
    /// Mixed streams and a nested block document, so the separator decision is exercised per
    /// document rather than once for the stream.
    /// </summary>
    [Fact]
    public void SafeDumpAll_MixedStreams_DecideThePrefixPerDocument()
    {
        var mapping = new Dict<string, object?>();
        mapping["a"] = 1;
        Assert.Equal("a: 1\n--- x\n...\n", Yaml.SafeDumpAll(new List<object?> { mapping, "x" }));

        var inner = new Dict<string, object?>();
        inner["b"] = 1;
        var outer = new Dict<string, object?>();
        outer["a"] = inner;
        Assert.Equal("a:\n  b: 1\n--- z\n...\n", Yaml.SafeDumpAll(new List<object?> { outer, "z" }));

        Assert.Equal("1\n--- null\n--- 2\n...\n", Yaml.SafeDumpAll(new List<object?> { 1, null, 2 }));
    }

    /// <summary>
    /// The empty stream, which PyYAML renders as the empty string.
    /// </summary>
    [Fact]
    public void SafeDumpAll_NoDocuments_IsTheEmptyString()
    {
        Assert.Equal("", Yaml.SafeDumpAll(new List<object?>()));
    }

    /// <summary>
    /// The new shape reads back as the documents it came from — the property that makes the flip
    /// safe to ship rather than merely oracle-shaped.
    /// </summary>
    [Fact]
    public void TheNewStreamShape_RoundTrips()
    {
        var documents = new List<object?> { 1.0, "hello" };
        List<object?> reloaded = Yaml.SafeLoadAll(Yaml.SafeDumpAll(documents));

        Assert.Equal(2, reloaded.Length);
        Assert.Equal(1.0, reloaded[0]);
        Assert.Equal("hello", reloaded[1]);
    }

    /// <summary>
    /// BACKWARD COMPATIBILITY. Streams written by pre-#1471 Sharpy exist, and changing what the
    /// dumper WRITES must not change what the loader ACCEPTS. The retired shape — free-standing
    /// separator, no stream-end marker — must still load as the same two documents.
    /// </summary>
    [Fact]
    public void TheRetiredStreamShape_StillLoadsCorrectly()
    {
        List<object?> reloaded = Yaml.SafeLoadAll("1.0\n---\nhello\n");

        Assert.Equal(2, reloaded.Length);
        Assert.Equal(1.0, reloaded[0]);
        Assert.Equal("hello", reloaded[1]);

        List<object?> collections = Yaml.SafeLoadAll("a: 1\n---\n- 2\n");
        Assert.Equal(2, collections.Length);
    }
}
