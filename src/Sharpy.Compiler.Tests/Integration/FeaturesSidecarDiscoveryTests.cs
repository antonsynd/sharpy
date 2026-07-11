using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Sharpy.TestInfrastructure.Integration;
using Xunit;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Unit tests for the <c>.features</c> fixture sidecar (#1046) discovered by
/// <see cref="FixtureDiscoveryHelper"/>. The sidecar lists experimental feature names (one
/// per line, <c>#</c> comments and blank lines tolerated) that are enabled compilation-wide
/// when the fixture compiles. Unknown names must fail discovery loudly rather than silently
/// leaving a gated feature disabled.
/// </summary>
public class FeaturesSidecarDiscoveryTests : IDisposable
{
    private readonly string _tempDir;

    public FeaturesSidecarDiscoveryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_features_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    [Fact]
    public void ReadFeaturesFile_Absent_ReturnsEmpty()
    {
        var missing = Path.Combine(_tempDir, "nope.features");
        FixtureDiscoveryHelper.ReadFeaturesFile(missing).Should().BeEmpty();
    }

    [Fact]
    public void ReadFeaturesFile_KnownNames_ParsedSkippingCommentsAndBlanks()
    {
        var path = Path.Combine(_tempDir, "sample.features");
        File.WriteAllText(path, "# a comment\n\nmatmul\n   defer   \n");

        FixtureDiscoveryHelper.ReadFeaturesFile(path)
            .Should().Equal("matmul", "defer");
    }

    [Fact]
    public void ReadFeaturesFile_UnknownName_ThrowsLoudlyNamingFileAndFeature()
    {
        var path = Path.Combine(_tempDir, "bogus.features");
        File.WriteAllText(path, "not_a_real_feature\n");

        var act = () => FixtureDiscoveryHelper.ReadFeaturesFile(path);

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain(path).And.Contain("not_a_real_feature");
    }

    [Fact]
    public void DiscoverFixtures_SingleFileWithFeatures_PopulatesFeatures()
    {
        File.WriteAllText(Path.Combine(_tempDir, "prog.spy"), "def main() -> None:\n    pass\n");
        File.WriteAllText(Path.Combine(_tempDir, "prog.expected"), "\n");
        File.WriteAllText(Path.Combine(_tempDir, "prog.features"), "matmul\n");

        var fixture = FixtureDiscoveryHelper.DiscoverFixtures(_tempDir).Single();
        fixture.Features.Should().Equal("matmul");
    }

    [Fact]
    public void DiscoverFixtures_NoSidecar_HasEmptyFeatures()
    {
        File.WriteAllText(Path.Combine(_tempDir, "prog.spy"), "def main() -> None:\n    pass\n");
        File.WriteAllText(Path.Combine(_tempDir, "prog.expected"), "\n");

        var fixture = FixtureDiscoveryHelper.DiscoverFixtures(_tempDir).Single();
        fixture.Features.Should().BeEmpty();
    }

    [Fact]
    public void DiscoverFixtures_BogusFeatureName_FailsDiscoveryLoudly()
    {
        File.WriteAllText(Path.Combine(_tempDir, "prog.spy"), "def main() -> None:\n    pass\n");
        File.WriteAllText(Path.Combine(_tempDir, "prog.expected"), "\n");
        File.WriteAllText(Path.Combine(_tempDir, "prog.features"), "totally_made_up\n");

        // Discovery is lazy (yield return), so enumeration is what triggers validation.
        var act = () => FixtureDiscoveryHelper.DiscoverFixtures(_tempDir).ToList();

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("totally_made_up");
    }
}
