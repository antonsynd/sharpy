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
    public void RestorePackages_EmptyList_ReturnsTrueWithoutRestoring()
    {
        var result = NuGetRestorer.RestorePackages(
            Array.Empty<PackageRef>(), "net10.0", logger: null, packagesDir: _packagesDir);

        Assert.True(result);
        Assert.Empty(Directory.EnumerateFileSystemEntries(_packagesDir));
    }

    [Fact]
    [Trait("Category", "RequiresNetwork")]
    public void RestorePackages_KnownPackage_RestoresIntoPackagesDir()
    {
        var result = NuGetRestorer.RestorePackages(
            new[] { new PackageRef("Newtonsoft.Json", "13.0.3") },
            "net10.0", logger: null, packagesDir: _packagesDir);

        Assert.True(result);
        // Restoring into a fresh directory the test controls is what makes this
        // assertion non-vacuous: nothing can have pre-cached the package here.
        Assert.True(Directory.Exists(Path.Combine(_packagesDir, "newtonsoft.json", "13.0.3")));
    }

    [Fact]
    [Trait("Category", "RequiresNetwork")]
    public void RestorePackages_NonexistentPackage_ReturnsFalse()
    {
        var result = NuGetRestorer.RestorePackages(
            new[] { new PackageRef("Sharpy.Nonexistent.Package.For.Restore.Test", "99.99.99") },
            "net10.0", logger: null, packagesDir: _packagesDir);

        Assert.False(result);
    }
}
