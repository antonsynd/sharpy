extern alias SharpyRT;

using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests;

/// <summary>
/// Unit tests for <see cref="RuntimeClosureResolver"/>. These resolve closures against the
/// test host's own output directory, which is a framework-dependent build output: it contains
/// the Sharpy stdlib monolith plus its transitive NuGet assemblies (MathNet, YamlDotNet,
/// Tomlyn, Microsoft.Data.Sqlite, SQLitePCLRaw.*) and their native assets under
/// <c>runtimes/&lt;rid&gt;/native/</c>, but never shared-framework assemblies. Tests that need
/// assemblies which may not be present skip gracefully, mirroring
/// <see cref="Integration.PerModuleAssemblyTests"/>.
/// </summary>
public class RuntimeClosureResolverTests
{
    private readonly ITestOutputHelper _output;

    private static readonly string CoreDir = Path.GetDirectoryName(
        typeof(SharpyRT::Sharpy.Builtins).Assembly.Location)!;

    private static readonly string PerModuleDir = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "src", "Sharpy.Stdlib", "modules", "bin", "Debug", "net10.0"));

    public RuntimeClosureResolverTests(ITestOutputHelper output) => _output = output;

    private static bool HasName(IEnumerable<string> paths, string fileName) =>
        paths.Any(p => Path.GetFileName(p).Equals(fileName, StringComparison.OrdinalIgnoreCase));

    private static bool HasPrefix(IEnumerable<string> paths, string prefix) =>
        paths.Any(p => Path.GetFileName(p).StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    [Fact]
    public void Monolith_Closure_ContainsMathNet()
    {
        var monolith = Path.Combine(CoreDir, "Sharpy.Stdlib.dll");
        if (!File.Exists(monolith))
        {
            _output.WriteLine($"Monolith not found: {monolith}");
            return;
        }

        var closure = RuntimeClosureResolver.Resolve(new[] { monolith });

        Assert.Contains("Sharpy.Stdlib.dll", closure.ManagedAssemblies.Select(Path.GetFileName));
        Assert.True(HasName(closure.ManagedAssemblies, "MathNet.Numerics.dll"),
            "numpy's MathNet.Numerics.dll must be in the transitive closure");
    }

    [Fact]
    public void Monolith_Closure_ContainsYamlAndTomlyn()
    {
        var monolith = Path.Combine(CoreDir, "Sharpy.Stdlib.dll");
        if (!File.Exists(monolith))
        {
            _output.WriteLine($"Monolith not found: {monolith}");
            return;
        }

        var closure = RuntimeClosureResolver.Resolve(new[] { monolith });

        // YamlDotNet was the assembly the hand-maintained map dropped entirely (#1084).
        Assert.True(HasName(closure.ManagedAssemblies, "YamlDotNet.dll"),
            "yaml's YamlDotNet.dll must be in the transitive closure");
        Assert.True(HasName(closure.ManagedAssemblies, "Tomlyn.dll"),
            "toml's Tomlyn.dll must be in the transitive closure");
    }

    [Fact]
    public void Monolith_Closure_ContainsSqliteManagedAndReportsNativeAsset()
    {
        var monolith = Path.Combine(CoreDir, "Sharpy.Stdlib.dll");
        if (!File.Exists(monolith))
        {
            _output.WriteLine($"Monolith not found: {monolith}");
            return;
        }

        var closure = RuntimeClosureResolver.Resolve(new[] { monolith });

        Assert.True(HasName(closure.ManagedAssemblies, "Microsoft.Data.Sqlite.dll"),
            "sqlite3's Microsoft.Data.Sqlite.dll must be in the transitive closure");
        Assert.True(HasPrefix(closure.ManagedAssemblies, "SQLitePCLRaw"),
            "sqlite3's SQLitePCLRaw.* assemblies must be in the transitive closure");

        // The provider-registration assembly is reachable only via the companion pass — nothing
        // in the reference graph names it, but without it SetProvider is never called and a
        // standalone sqlite3 program throws at connect time. Only assert where it is deployed.
        if (File.Exists(Path.Combine(CoreDir, "SQLitePCLRaw.batteries_v2.dll")))
        {
            Assert.True(HasName(closure.ManagedAssemblies, "SQLitePCLRaw.batteries_v2.dll"),
                "the companion pass must pull in SQLitePCLRaw.batteries_v2 (runtime provider registration)");
        }
        if (File.Exists(Path.Combine(CoreDir, "SQLitePCLRaw.provider.e_sqlite3.dll")))
        {
            Assert.True(HasName(closure.ManagedAssemblies, "SQLitePCLRaw.provider.e_sqlite3.dll"),
                "the forward walk from batteries_v2 must pull in the SQLitePCLRaw provider");
        }

        // The e_sqlite3 native library ships per-RID under runtimes/<rid>/native/. Assert the
        // resolver reports it only when the current RID's native layout is actually present.
        if (ExpectedNativeAvailable(CoreDir, "e_sqlite3"))
        {
            Assert.Contains(closure.NativeAssets,
                p => Path.GetFileName(p).Contains("e_sqlite3", StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            _output.WriteLine("No e_sqlite3 native asset for the current RID; skipping native assertion.");
        }
    }

    [Fact]
    public void Monolith_Closure_ExcludesFrameworkAssemblies()
    {
        var monolith = Path.Combine(CoreDir, "Sharpy.Stdlib.dll");
        if (!File.Exists(monolith))
        {
            _output.WriteLine($"Monolith not found: {monolith}");
            return;
        }

        var closure = RuntimeClosureResolver.Resolve(new[] { monolith });

        // Shared-framework assemblies resolve from the runtime pack, never appear as siblings,
        // and so must never enter the closure. If the sibling filter regressed, these would.
        foreach (var framework in new[]
        {
            "System.Runtime.dll",
            "System.Private.CoreLib.dll",
            "System.Console.dll",
            "System.Collections.dll",
            "System.Linq.dll",
            "netstandard.dll",
            "mscorlib.dll",
        })
        {
            Assert.False(HasName(closure.ManagedAssemblies, framework),
                $"framework assembly {framework} must not be in the closure");
        }
    }

    [Fact]
    public void PerModule_NumpyClosure_ContainsMathNet_WhenCoLocated()
    {
        var numpy = LocatePerModule("Sharpy.Stdlib.Numpy.dll");
        var mathNetSource = Path.Combine(CoreDir, "MathNet.Numerics.dll");
        if (numpy is null || !File.Exists(mathNetSource))
        {
            _output.WriteLine("Numpy per-module DLL or MathNet.Numerics.dll not available; skipping.");
            return;
        }

        // Per-module build outputs do not co-locate transitive NuGet deps, so synthesize the
        // deployable layout the CLI copy path and deps.json generation actually operate over
        // (numpy + MathNet as siblings) and prove the resolver walks numpy → MathNet.
        var temp = Directory.CreateTempSubdirectory("sharpy-closure-");
        try
        {
            var numpyDest = Path.Combine(temp.FullName, Path.GetFileName(numpy));
            File.Copy(numpy, numpyDest);
            File.Copy(mathNetSource, Path.Combine(temp.FullName, "MathNet.Numerics.dll"));

            var closure = RuntimeClosureResolver.Resolve(new[] { numpyDest });

            Assert.True(HasName(closure.ManagedAssemblies, "MathNet.Numerics.dll"),
                "numpy closure must contain the co-located MathNet.Numerics.dll");
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    [Fact]
    public void Resolve_MissingInputs_AreSkipped()
    {
        var closure = RuntimeClosureResolver.Resolve(new[]
        {
            Path.Combine(CoreDir, "Does.Not.Exist.dll"),
            string.Empty,
        });

        Assert.Empty(closure.ManagedAssemblies);
        Assert.Empty(closure.NativeAssets);
    }

    [Theory]
    [InlineData("osx-arm64", new[] { "osx-arm64", "osx", "unix-arm64", "unix", "any" })]
    [InlineData("linux-x64", new[] { "linux-x64", "linux", "unix-x64", "unix", "any" })]
    [InlineData("win-x64", new[] { "win-x64", "win", "any" })]
    [InlineData("osx.15-arm64", new[] { "osx.15-arm64", "osx-arm64", "osx", "unix-arm64", "unix", "any" })]
    public void GetRidCandidates_ProducesPortableFallbacks(string rid, string[] expected)
    {
        var candidates = RuntimeClosureResolver.GetRidCandidates(rid).ToArray();
        Assert.Equal(expected, candidates);
    }

    private static string? LocatePerModule(string fileName)
    {
        var inCore = Path.Combine(CoreDir, fileName);
        if (File.Exists(inCore))
            return inCore;

        var inPerModule = Path.Combine(PerModuleDir, fileName);
        return File.Exists(inPerModule) ? inPerModule : null;
    }

    private static bool ExpectedNativeAvailable(string directory, string nativeNameFragment)
    {
        var runtimesDir = Path.Combine(directory, "runtimes");
        if (!Directory.Exists(runtimesDir))
            return false;

        foreach (var rid in RuntimeClosureResolver.GetRidCandidates(
            System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier))
        {
            var nativeDir = Path.Combine(runtimesDir, rid, "native");
            if (Directory.Exists(nativeDir)
                && Directory.EnumerateFiles(nativeDir).Any(
                    f => Path.GetFileName(f).Contains(nativeNameFragment, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }
}
