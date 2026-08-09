using System.Text.RegularExpressions;
using Xunit;
using IOPath = System.IO.Path;

namespace Sharpy.Stdlib.Tests.Conformance;

/// <summary>
/// Makes the NuGet-backed stdlib module class visible (#1300).
///
/// <para>
/// The fixture harness used to deploy a hand-maintained list of six DLL names. YamlDotNet was
/// never added to it, so a yaml fixture could not run — and because there were no yaml fixtures,
/// nothing ever noticed. The deployment is mechanical now, but the coverage hole was the thing
/// that hid it: a module with a NuGet dependency and zero fixtures is a module whose deployment
/// is untested.
/// </para>
///
/// <para>
/// This asserts the class, not the four instances: every NuGet package referenced by
/// <c>Sharpy.Stdlib</c> maps to a stdlib module, and every such module has at least one
/// integration fixture importing it. Adding a package without a fixture fails here.
/// </para>
/// </summary>
public class NuGetModuleCoverageTests
{
    /// <summary>
    /// NuGet package to the stdlib module(s) it backs. A package with no user-facing module of
    /// its own (a transitive provider bundle) maps to an empty list and is exempt.
    /// </summary>
    private static readonly Dictionary<string, string[]> PackageToModules = new()
    {
        ["MathNet.Numerics"] = new[] { "numpy" },
        ["Microsoft.Data.Sqlite"] = new[] { "sqlite3" },
        // The SQLitePCLRaw bundle is Microsoft.Data.Sqlite's native provider, not a module.
        ["SQLitePCLRaw.bundle_e_sqlite3"] = System.Array.Empty<string>(),
        ["Tomlyn"] = new[] { "toml" },
        ["YamlDotNet"] = new[] { "yaml" },
    };

    [Fact]
    public void EveryNuGetBackedStdlibModule_HasAnIntegrationFixture()
    {
        var packages = ReadStdlibPackageReferences();
        Assert.NotEmpty(packages);

        var unmapped = packages.Where(p => !PackageToModules.ContainsKey(p)).ToList();
        Assert.True(unmapped.Count == 0,
            $"Unmapped NuGet package(s): {string.Join(", ", unmapped)}. Every package must declare "
            + $"which stdlib module(s) it backs — add it to {nameof(PackageToModules)} (empty array "
            + "if it backs none) so this guard keeps covering the class.");

        var fixtureSources = Directory
            .GetFiles(FixturesDirectory(), "*.spy", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .ToList();

        var uncovered = new System.Collections.Generic.List<string>();
        foreach (var module in packages.SelectMany(p => PackageToModules[p]).Distinct())
        {
            var importPattern = new Regex(
                $@"^\s*(import\s+{Regex.Escape(module)}\b|from\s+{Regex.Escape(module)}\b)",
                RegexOptions.Multiline);

            if (!fixtureSources.Any(importPattern.IsMatch))
                uncovered.Add(module);
        }

        Assert.True(uncovered.Count == 0,
            $"NuGet-backed stdlib module(s) with no integration fixture: {string.Join(", ", uncovered)}. "
            + "An untested deployment path is exactly how yaml's missing DLL stayed invisible "
            + "(#1300). Add a fixture importing it under "
            + "Sharpy.Stdlib.Tests/Integration/TestFixtures/.");
    }

    private static IReadOnlyList<string> ReadStdlibPackageReferences()
    {
        var csproj = IOPath.GetFullPath(IOPath.Combine(
            RepoRoot(), "src", "Sharpy.Stdlib", "Sharpy.Stdlib.csproj"));
        Assert.True(File.Exists(csproj), $"Sharpy.Stdlib.csproj should exist at {csproj}");

        return Regex.Matches(File.ReadAllText(csproj), @"<PackageReference\s+Include=""([^""]+)""")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();
    }

    private static string FixturesDirectory() => IOPath.GetFullPath(IOPath.Combine(
        IOPath.GetDirectoryName(typeof(NuGetModuleCoverageTests).Assembly.Location)!,
        "..", "..", "..", "Integration", "TestFixtures"));

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(IOPath.GetDirectoryName(
            typeof(NuGetModuleCoverageTests).Assembly.Location)!);
        while (dir != null && !File.Exists(IOPath.Combine(dir.FullName, "sharpy.sln")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
