using Xunit;

namespace Sharpy.Stdlib.Tests;

/// <summary>
/// Pins <c>safe_dump</c>'s current output for the two values whose emitted scalar is empty or
/// absent — the empty string and null — where Sharpy diverges from PyYAML (#1467).
///
/// <para>
/// A unit test rather than an integration fixture, deliberately. The behaviour pinned here is a
/// LIVE divergence from CPython, and the fixtures under <c>Integration/TestFixtures/</c> are
/// compared against CPython by the differential-execution oracle. A fixture printing these bytes
/// would either fail that sweep or require an allowlist entry — and adding an allowlist entry to
/// accommodate a pin ratchets the wrong way. Pinning it here keeps the deferral visible without
/// making a claim about CPython on the runtime-comparison surface.
/// </para>
///
/// <para>
/// The point of pinning a known-wrong behaviour is that fixing it announces itself: #1348 is about
/// to change document-marker emission, and if it moves these bytes incidentally that must surface
/// as a flipped assertion rather than as a mystery months later. When #1467 lands, these
/// assertions change to PyYAML's <c>''</c> and <c>null\n...\n</c> — that flip is the deliverable,
/// not a regression.
/// </para>
/// </summary>
public class YamlEmptyScalarDumpTests
{
    /// <summary>
    /// PyYAML emits <c>''</c>; Sharpy prefixes an explicit document-start marker. Distinct from
    /// #1348, which is the document-END marker.
    /// </summary>
    [Fact]
    public void SafeDump_EmptyString_EmitsADocumentStartMarker_Pending1467()
    {
        Assert.Equal("--- ''\n", Yaml.SafeDump(""));
    }

    /// <summary>
    /// The worse half of #1467: PyYAML emits <c>null\n...\n</c>, Sharpy emits the marker and NO
    /// value at all. The output round-trips within Sharpy, which is exactly why the round-trip
    /// property in <c>stdlib_yaml_round_trip</c> cannot see it — both halves agree with each
    /// other and neither agrees with PyYAML.
    /// </summary>
    [Fact]
    public void SafeDump_Null_EmitsAMarkerAndNoValue_Pending1467()
    {
        Assert.Equal("--- \n", Yaml.SafeDump(null));
    }

    /// <summary>
    /// Controls: every other member of the family already matches PyYAML, which is what scopes
    /// #1467 to the empty/absent scalar rather than to document emission generally. These must not
    /// move when #1467 is fixed.
    /// </summary>
    [Fact]
    public void SafeDump_NonEmptyScalarsAndEmptyCollections_AlreadyMatchPyYaml()
    {
        Assert.Equal("' '\n", Yaml.SafeDump(" "));
        Assert.Equal("[]\n", Yaml.SafeDump(new List<object?>()));
        Assert.Equal("{}\n", Yaml.SafeDump(new Dict<string, object?>()));
    }
}
