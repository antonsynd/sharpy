using System.Reflection;
using System.Runtime.InteropServices;
using FluentAssertions;
using Xunit;

namespace Sharpy.Cli.Tests;

/// <summary>
/// #1107 tripwire: the SQLitePCLRaw provider bundle referenced directly by
/// <c>Sharpy.Cli.csproj</c> (<c>SQLitePCLRaw.bundle_e_sqlite3</c>, <c>ExcludeAssets="compile"</c>)
/// must stay version-locked with the <c>SQLitePCLRaw.core</c> that
/// <c>Microsoft.Data.Sqlite</c> pulls transitively into <c>Sharpy.Stdlib</c>. The Cli.Tests output
/// directory receives the CLI's runtime assets (Cli.Tests references Sharpy.Cli), so the same DLLs
/// that sit next to <c>sharpyc.dll</c> sit next to the test assembly at <see cref="AppContext.BaseDirectory"/>.
///
/// <para>
/// The likely future failure is a silent <c>Microsoft.Data.Sqlite</c> bump: NuGet unifies
/// <c>SQLitePCLRaw.core</c> to the higher of the bundle pin and the Sqlite-pulled version, so a
/// bumped Sqlite drags <c>core.dll</c> forward while a stale bundle pin leaves
/// <c>provider.e_sqlite3.dll</c>/<c>batteries_v2.dll</c> behind. This test fires when those versions
/// diverge, pointing at the <c>Version</c> that must be re-synced in <c>Sharpy.Cli.csproj</c>.
/// </para>
/// </summary>
public class SQLitePCLRawDeploymentTests
{
    private static readonly string CliDir = AppContext.BaseDirectory;

    [Fact]
    public void BundleAndCoreVersionsAreInLockStep()
    {
        var coreVersion = AssemblyVersionOf("SQLitePCLRaw.core.dll");
        var providerVersion = AssemblyVersionOf("SQLitePCLRaw.provider.e_sqlite3.dll");
        var batteriesVersion = AssemblyVersionOf("SQLitePCLRaw.batteries_v2.dll");

        // provider/batteries come from the direct bundle pin in Sharpy.Cli.csproj; core is
        // NuGet-unified with the Microsoft.Data.Sqlite transitive pull in Sharpy.Stdlib.csproj.
        // A drift means the bundle Version in Sharpy.Cli.csproj must be re-synced.
        providerVersion.Should().Be(coreVersion,
            "SQLitePCLRaw.bundle_e_sqlite3 in Sharpy.Cli.csproj must stay version-locked with the "
            + "SQLitePCLRaw.core that Microsoft.Data.Sqlite pulls into Sharpy.Stdlib.csproj (#1107)");
        batteriesVersion.Should().Be(coreVersion,
            "the bundle's batteries_v2 provider must match the resolved SQLitePCLRaw.core version (#1107)");
    }

    [Fact]
    public void RuntimeProviderAssetsAreCoLocatedWithCli()
    {
        // #1107 proper: these runtime-only assets never reached the CLI output before the direct
        // bundle reference (they don't flow through the ReferenceOutputAssembly="false" Stdlib ref).
        File.Exists(Path.Combine(CliDir, "SQLitePCLRaw.batteries_v2.dll")).Should().BeTrue(
            "the managed batteries_v2 provider must sit next to sharpyc");
        File.Exists(Path.Combine(CliDir, "SQLitePCLRaw.provider.e_sqlite3.dll")).Should().BeTrue(
            "the managed e_sqlite3 provider must sit next to sharpyc");

        var rid = RuntimeInformation.RuntimeIdentifier;
        var nativeDir = Path.Combine(CliDir, "runtimes", rid, "native");
        Directory.Exists(nativeDir).Should().BeTrue(
            $"the native e_sqlite3 runtime for host RID '{rid}' must be deployed next to sharpyc");
        Directory.GetFiles(nativeDir, "*e_sqlite3*").Should().NotBeEmpty(
            $"a native e_sqlite3 library must be deployed for host RID '{rid}'");
    }

    private static Version AssemblyVersionOf(string fileName)
    {
        var path = Path.Combine(CliDir, fileName);
        File.Exists(path).Should().BeTrue($"{fileName} must be co-located with the CLI (#1107)");
        return AssemblyName.GetAssemblyName(path).Version!;
    }
}
