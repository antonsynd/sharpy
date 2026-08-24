using Sharpy.Compiler.Project;
using Xunit;

namespace Sharpy.Compiler.Tests.Project;

public class NuGetRestorerTests : IDisposable
{
    private readonly string _packagesDir;

    public NuGetRestorerTests()
    {
        _packagesDir = Path.Combine(Path.GetTempPath(), "sharpy-restore-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_packagesDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_packagesDir))
            Directory.Delete(_packagesDir, recursive: true);
    }

    [Fact]
    public void RestorePackages_EmptyList_ReturnsSuccessWithEmptyVersions()
    {
        var result = NuGetRestorer.RestorePackages(
            Array.Empty<PackageRef>(), "net10.0", logger: null, packagesDir: _packagesDir);

        Assert.True(result.Success);
        Assert.Empty(result.ResolvedVersions);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_packagesDir));
    }

    [Fact]
    [Trait("Category", "RequiresNetwork")]
    public void RestorePackages_KnownPackage_RestoresIntoPackagesDir()
    {
        var result = NuGetRestorer.RestorePackages(
            new[] { new PackageRef("Newtonsoft.Json", "13.0.3") },
            "net10.0", logger: null, packagesDir: _packagesDir);

        Assert.True(result.Success);
        Assert.True(Directory.Exists(Path.Combine(_packagesDir, "newtonsoft.json", "13.0.3")));
        Assert.True(result.ResolvedVersions.ContainsKey("Newtonsoft.Json"));
        Assert.Equal("13.0.3", result.ResolvedVersions["Newtonsoft.Json"]);
    }

    [Fact]
    [Trait("Category", "RequiresNetwork")]
    public void RestorePackages_NonexistentPackage_ReturnsFalse()
    {
        var result = NuGetRestorer.RestorePackages(
            new[] { new PackageRef("Sharpy.Nonexistent.Package.For.Restore.Test", "99.99.99") },
            "net10.0", logger: null, packagesDir: _packagesDir);

        Assert.False(result.Success);
    }

    [Fact]
    public void ParseProjectAssetsJson_ExtractsLibraryVersions()
    {
        var assetsDir = Path.Combine(_packagesDir, "obj");
        Directory.CreateDirectory(assetsDir);
        var assetsPath = Path.Combine(assetsDir, "project.assets.json");
        File.WriteAllText(assetsPath, """
            {
              "version": 3,
              "libraries": {
                "Newtonsoft.Json/13.0.3": { "type": "package" },
                "Humanizer.Core/2.13.14": { "type": "package" }
              }
            }
            """);

        var versions = NuGetRestorer.ParseProjectAssetsJson(assetsPath, logger: null);

        Assert.Equal(2, versions.Count);
        Assert.Equal("13.0.3", versions["Newtonsoft.Json"]);
        Assert.Equal("2.13.14", versions["Humanizer.Core"]);
    }

    [Fact]
    public void ParseProjectAssetsJson_MissingFile_ReturnsEmpty()
    {
        var versions = NuGetRestorer.ParseProjectAssetsJson(
            Path.Combine(_packagesDir, "nonexistent.json"), logger: null);

        Assert.Empty(versions);
    }

    [Fact]
    public void ParseProjectAssetsJson_CaseInsensitiveLookup()
    {
        var assetsDir = Path.Combine(_packagesDir, "obj");
        Directory.CreateDirectory(assetsDir);
        var assetsPath = Path.Combine(assetsDir, "project.assets.json");
        File.WriteAllText(assetsPath, """
            {
              "libraries": {
                "Newtonsoft.Json/13.0.3": { "type": "package" }
              }
            }
            """);

        var versions = NuGetRestorer.ParseProjectAssetsJson(assetsPath, logger: null);

        Assert.True(versions.ContainsKey("newtonsoft.json"));
        Assert.True(versions.ContainsKey("NEWTONSOFT.JSON"));
    }
}
