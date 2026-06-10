using Sharpy.Compiler.Project;
using Xunit;

namespace Sharpy.Compiler.Tests.Project;

public class NuGetResolverTests : IDisposable
{
    private readonly string _testDir;

    public NuGetResolverTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "sharpy-nuget-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    #region Helper Methods

    /// <summary>
    /// Creates a mock package directory structure with DLLs and optional .nuspec.
    /// </summary>
    private void CreateMockPackage(string name, string version, string tfm, string[]? dllNames = null, string? nuspecContent = null)
    {
        var packageDir = Path.Combine(_testDir, name.ToLowerInvariant(), version);
        Directory.CreateDirectory(packageDir);

        if (dllNames != null && dllNames.Length > 0)
        {
            var libTfmDir = Path.Combine(packageDir, "lib", tfm);
            Directory.CreateDirectory(libTfmDir);

            foreach (var dll in dllNames)
            {
                File.WriteAllText(Path.Combine(libTfmDir, dll), "fake-dll");
            }
        }

        if (nuspecContent != null)
        {
            var nuspecPath = Path.Combine(packageDir, $"{name.ToLowerInvariant()}.nuspec");
            File.WriteAllText(nuspecPath, nuspecContent);
        }
    }

    #endregion

    #region Direct Resolution (Leaf Packages)

    [Fact]
    public void ResolvePackage_LeafPackage_ReturnsDlls()
    {
        CreateMockPackage("MyLib", "1.0.0", "net10.0", new[] { "MyLib.dll" });

        var result = NuGetResolver.ResolvePackage(
            new PackageRef("MyLib", "1.0.0"), "net10.0", logger: null, nugetPackagesDir: _testDir);

        Assert.Single(result);
        Assert.EndsWith("MyLib.dll", result[0]);
    }

    [Fact]
    public void ResolvePackage_PackageNotFound_ReturnsEmpty()
    {
        var result = NuGetResolver.ResolvePackage(
            new PackageRef("NonExistent", "1.0.0"), "net10.0", logger: null, nugetPackagesDir: _testDir);

        Assert.Empty(result);
    }

    [Fact]
    public void ResolvePackage_NoLibDir_ReturnsEmpty()
    {
        // Create package dir without lib/
        var packageDir = Path.Combine(_testDir, "empty", "1.0.0");
        Directory.CreateDirectory(packageDir);

        var result = NuGetResolver.ResolvePackage(
            new PackageRef("Empty", "1.0.0"), "net10.0", logger: null, nugetPackagesDir: _testDir);

        Assert.Empty(result);
    }

    #endregion

    #region Transitive Resolution

    [Fact]
    public void ResolvePackage_MetaPackage_ResolvesTransitiveDependencies()
    {
        // Create leaf package with a DLL
        CreateMockPackage("leaf", "2.0.0", "net10.0", new[] { "Leaf.dll" });

        // Create meta-package with no DLLs but a .nuspec pointing to leaf
        var nuspec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
  <metadata>
    <id>meta</id>
    <version>1.0.0</version>
    <dependencies>
      <dependency id=""leaf"" version=""2.0.0"" />
    </dependencies>
  </metadata>
</package>";
        CreateMockPackage("meta", "1.0.0", "net10.0", dllNames: null, nuspecContent: nuspec);

        var result = NuGetResolver.ResolvePackage(
            new PackageRef("meta", "1.0.0"), "net10.0", logger: null, nugetPackagesDir: _testDir);

        Assert.Single(result);
        Assert.EndsWith("Leaf.dll", result[0]);
    }

    [Fact]
    public void ResolvePackage_TransitiveChain_ResolvesAllLevels()
    {
        // Create chain: A -> B -> C (each with a DLL)
        CreateMockPackage("pkgc", "1.0.0", "net10.0", new[] { "C.dll" });

        var nuspecB = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
  <metadata>
    <id>pkgb</id>
    <version>1.0.0</version>
    <dependencies>
      <dependency id=""pkgc"" version=""1.0.0"" />
    </dependencies>
  </metadata>
</package>";
        CreateMockPackage("pkgb", "1.0.0", "net10.0", new[] { "B.dll" }, nuspecB);

        var nuspecA = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
  <metadata>
    <id>pkga</id>
    <version>1.0.0</version>
    <dependencies>
      <dependency id=""pkgb"" version=""1.0.0"" />
    </dependencies>
  </metadata>
</package>";
        CreateMockPackage("pkga", "1.0.0", "net10.0", new[] { "A.dll" }, nuspecA);

        var result = NuGetResolver.ResolvePackage(
            new PackageRef("pkga", "1.0.0"), "net10.0", logger: null, nugetPackagesDir: _testDir);

        Assert.Equal(3, result.Count);
        Assert.Contains(result, p => p.EndsWith("A.dll", StringComparison.Ordinal));
        Assert.Contains(result, p => p.EndsWith("B.dll", StringComparison.Ordinal));
        Assert.Contains(result, p => p.EndsWith("C.dll", StringComparison.Ordinal));
    }

    #endregion

    #region Cycle Detection

    [Fact]
    public void ResolvePackage_CircularDependency_DoesNotLoop()
    {
        // A depends on B, B depends on A
        var nuspecA = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
  <metadata>
    <id>cyclea</id>
    <version>1.0.0</version>
    <dependencies>
      <dependency id=""cycleb"" version=""1.0.0"" />
    </dependencies>
  </metadata>
</package>";
        CreateMockPackage("cyclea", "1.0.0", "net10.0", new[] { "A.dll" }, nuspecA);

        var nuspecB = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
  <metadata>
    <id>cycleb</id>
    <version>1.0.0</version>
    <dependencies>
      <dependency id=""cyclea"" version=""1.0.0"" />
    </dependencies>
  </metadata>
</package>";
        CreateMockPackage("cycleb", "1.0.0", "net10.0", new[] { "B.dll" }, nuspecB);

        var result = NuGetResolver.ResolvePackage(
            new PackageRef("cyclea", "1.0.0"), "net10.0", logger: null, nugetPackagesDir: _testDir);

        // Should resolve both without infinite loop
        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.EndsWith("A.dll", StringComparison.Ordinal));
        Assert.Contains(result, p => p.EndsWith("B.dll", StringComparison.Ordinal));
    }

    #endregion

    #region TFM Group Selection

    [Fact]
    public void ParseNuspecDependencies_GroupedFormat_SelectsCorrectGroup()
    {
        var nuspec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
  <metadata>
    <id>grouped</id>
    <version>1.0.0</version>
    <dependencies>
      <group targetFramework=""net8.0"">
        <dependency id=""net8dep"" version=""1.0.0"" />
      </group>
      <group targetFramework=""netstandard2.0"">
        <dependency id=""nsdep"" version=""1.0.0"" />
      </group>
    </dependencies>
  </metadata>
</package>";

        var nuspecPath = Path.Combine(_testDir, "grouped.nuspec");
        File.WriteAllText(nuspecPath, nuspec);

        var deps = NuGetResolver.ParseNuspecDependencies(nuspecPath, "net8.0", logger: null);

        Assert.Single(deps);
        Assert.Equal("net8dep", deps[0].Name);
    }

    [Fact]
    public void ParseNuspecDependencies_GroupedFormat_FallsBackToCompatibleTfm()
    {
        var nuspec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
  <metadata>
    <id>grouped</id>
    <version>1.0.0</version>
    <dependencies>
      <group targetFramework=""netstandard2.0"">
        <dependency id=""nsdep"" version=""1.0.0"" />
      </group>
    </dependencies>
  </metadata>
</package>";

        var nuspecPath = Path.Combine(_testDir, "grouped.nuspec");
        File.WriteAllText(nuspecPath, nuspec);

        // net10.0 should fall back to netstandard2.0
        var deps = NuGetResolver.ParseNuspecDependencies(nuspecPath, "net10.0", logger: null);

        Assert.Single(deps);
        Assert.Equal("nsdep", deps[0].Name);
    }

    #endregion

    #region Ungrouped Format

    [Fact]
    public void ParseNuspecDependencies_UngroupedFormat_ReturnsAllDependencies()
    {
        var nuspec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
  <metadata>
    <id>ungrouped</id>
    <version>1.0.0</version>
    <dependencies>
      <dependency id=""dep1"" version=""1.0.0"" />
      <dependency id=""dep2"" version=""2.0.0"" />
    </dependencies>
  </metadata>
</package>";

        var nuspecPath = Path.Combine(_testDir, "ungrouped.nuspec");
        File.WriteAllText(nuspecPath, nuspec);

        var deps = NuGetResolver.ParseNuspecDependencies(nuspecPath, "net10.0", logger: null);

        Assert.Equal(2, deps.Count);
        Assert.Contains(deps, d => d.Name == "dep1" && d.Version == "1.0.0");
        Assert.Contains(deps, d => d.Name == "dep2" && d.Version == "2.0.0");
    }

    #endregion

    #region Version Normalization

    [Theory]
    [InlineData("2.9.3", "2.9.3")]
    [InlineData("[2.9.3]", "2.9.3")]
    [InlineData("[1.0.0, 2.0.0)", "1.0.0")]
    [InlineData("(,2.0.0)", "2.0.0")]
    [InlineData("[1.0.0,)", "1.0.0")]
    public void NormalizeVersion_HandlesAllFormats(string input, string expected)
    {
        var result = NuGetResolver.NormalizeVersion(input);
        Assert.Equal(expected, result);
    }

    #endregion

    #region No Dependencies

    [Fact]
    public void ParseNuspecDependencies_NoDependenciesElement_ReturnsEmpty()
    {
        var nuspec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
  <metadata>
    <id>nodeps</id>
    <version>1.0.0</version>
  </metadata>
</package>";

        var nuspecPath = Path.Combine(_testDir, "nodeps.nuspec");
        File.WriteAllText(nuspecPath, nuspec);

        var deps = NuGetResolver.ParseNuspecDependencies(nuspecPath, "net10.0", logger: null);

        Assert.Empty(deps);
    }

    [Fact]
    public void ParseNuspecDependencies_EmptyDependencies_ReturnsEmpty()
    {
        var nuspec = @"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"">
  <metadata>
    <id>emptydeps</id>
    <version>1.0.0</version>
    <dependencies />
  </metadata>
</package>";

        var nuspecPath = Path.Combine(_testDir, "emptydeps.nuspec");
        File.WriteAllText(nuspecPath, nuspec);

        var deps = NuGetResolver.ParseNuspecDependencies(nuspecPath, "net10.0", logger: null);

        Assert.Empty(deps);
    }

    #endregion
}
