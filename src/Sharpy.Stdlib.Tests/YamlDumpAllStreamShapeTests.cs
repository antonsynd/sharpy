using Xunit;

namespace Sharpy.Stdlib.Tests;

/// <summary>
/// Pins <c>safe_dump_all</c>'s multi-document output, which #1348 deliberately EXCLUDED from the
/// per-document end-marker rule. The stream-level shape is filed as #1471.
///
/// <para>
/// The exclusion is the conservative choice rather than the cheap one. PyYAML writes one marker at
/// stream end and folds the separator inline — <c>1.0\n--- hello\n...\n</c>. Letting each document
/// append its own marker would give <c>1.0\n...\n---\nhello\n...\n</c>: markers BETWEEN documents,
/// which is neither Sharpy's output nor PyYAML's, and which a conforming parser reads differently
/// again. That would be a divergence #1348 manufactured rather than closed, so the rule is gated
/// off here and the multi-document output is byte-identical to its pre-#1348 form.
/// </para>
///
/// <para>
/// Asserted rather than merely commented, for the reason <c>YamlEmptyScalarDumpTests</c> exists: a
/// silently-excluded behaviour becomes folklore, while a pinned one flips loudly when someone
/// implements #1471. These assertions are expected to CHANGE to PyYAML's shape at that point —
/// that flip is the deliverable, not a regression. A unit test rather than an integration fixture
/// because the bytes below are a live divergence from CPython, and fixtures are compared against
/// CPython by the differential-execution oracle.
/// </para>
/// </summary>
public class YamlDumpAllStreamShapeTests
{
    /// <summary>
    /// PyYAML 6.0.3 gives <c>1.0\n--- hello\n...\n</c> for the same input: an inline separator and
    /// one marker at stream end. Sharpy writes the separator on its own line and no marker at all —
    /// both halves of #1471, and both pre-existing.
    /// </summary>
    [Fact]
    public void SafeDumpAll_ScalarDocuments_HaveNoMarkersAndAFreeStandingSeparator_Pending1471()
    {
        var documents = new List<object?> { 1.0, "hello" };

        Assert.Equal("1.0\n---\nhello\n", Yaml.SafeDumpAll(documents));
    }

    /// <summary>
    /// The control that gives the assertion above its meaning: each of those documents dumped ON
    /// ITS OWN carries the marker. So the difference is the stream path suppressing the rule, not
    /// the rule failing to fire — which is what makes #1471 a separate issue rather than a missed
    /// case of #1348.
    /// </summary>
    [Fact]
    public void SafeDump_OfEachDocumentAlone_CarriesTheMarkerThatDumpAllSuppresses()
    {
        Assert.Equal("1.0\n...\n", Yaml.SafeDump(1.0));
        Assert.Equal("hello\n...\n", Yaml.SafeDump("hello"));
    }

    /// <summary>
    /// A single-document stream takes the same excluded path, so it too keeps its pre-#1348 bytes.
    /// Stated separately because it is the case where "one document" and "one stream" coincide and
    /// an implementer of #1471 has to decide it explicitly: PyYAML gives <c>1.0\n...\n</c> here,
    /// agreeing with <c>safe_dump</c>.
    /// </summary>
    [Fact]
    public void SafeDumpAll_SingleScalarDocument_AlsoTakesTheExcludedPath_Pending1471()
    {
        var documents = new List<object?> { 1.0 };

        Assert.Equal("1.0\n", Yaml.SafeDumpAll(documents));
    }

    /// <summary>
    /// Collections were never in the marker rule's scope, so multi-document collection output is
    /// unaffected by #1348 in either direction and matches PyYAML apart from the free-standing
    /// separator. Present so a future change that widens the rule to collections fails here first.
    /// </summary>
    [Fact]
    public void SafeDumpAll_CollectionDocuments_AreUnaffectedByTheMarkerRule()
    {
        var first = new Dict<string, object?>();
        first["a"] = 1;
        var second = new List<object?> { 2 };
        var documents = new List<object?> { first, second };

        Assert.Equal("a: 1\n---\n- 2\n", Yaml.SafeDumpAll(documents));
    }
}
