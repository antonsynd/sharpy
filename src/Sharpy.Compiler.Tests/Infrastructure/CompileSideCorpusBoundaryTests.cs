using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Formatting;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Infrastructure;

/// <summary>
/// #1660 compile-side twin of <see cref="Integration.FixtureCorpusBoundaryTests"/>: verifies
/// that the compiler's own source glob, the formatter, and the multi-file test harness all
/// exclude files planted under <c>bin/</c>, <c>obj/</c>, and <c>.sharpy-crash/</c>.
///
/// <para>
/// A real <c>main.spy</c> sits at the project root; a planted copy sits under
/// <c>bin/Debug/net10.0/.sharpy-crash/x/sources/main.spy</c>. Without the
/// <see cref="CrashBundleWriter.IsNonSourceSegment"/> filter, the planted copy is compiled
/// as a second source, yielding CS0017 (multiple entry points) behind SPY0908.
/// </para>
/// </summary>
public class CompileSideCorpusBoundaryTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _root;

    public CompileSideCorpusBoundaryTests(ITestOutputHelper output)
    {
        _output = output;
        _root = Path.Combine(Path.GetTempPath(), $"sharpy_boundary_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch { }
    }

    /// <summary>
    /// Cell (b) of the plan's compile-side twin: a REAL <c>.spyproj</c> with
    /// <c>&lt;SourceFile Include="**/*.spy" /&gt;</c> goes through
    /// <c>ProjectFileParser.ResolveGlobPattern</c> — the production glob — and must select only the
    /// real source. The first version of this test re-implemented the filter inline over
    /// <c>Directory.GetFiles</c> and asserted on its own call, so deleting the predicate from
    /// <c>ResolveGlobPattern</c> left it green (found by the 2026-08-28 verification); the
    /// mutation the plan names ("remove the predicate from ResolveGlobPattern → CS0017") can
    /// only turn THIS shape red.
    /// </summary>
    [Theory]
    [InlineData("bin/Debug/net10.0/.sharpy-crash/20260827/sources")]
    [InlineData("bin/Debug/net10.0")]
    [InlineData("obj/Debug")]
    public void ProjectGlob_ExcludesPlantedSources(string plantedRelativePath)
    {
        PlantProject(plantedRelativePath);

        var config = global::Sharpy.Compiler.ProjectFileParser.Load(Path.Combine(_root, "planted.spyproj"));

        var sourceFile = Assert.Single(config.SourceFiles);
        Assert.Equal("main.spy", Path.GetFileName(sourceFile));
        Assert.Equal(_root, Path.GetDirectoryName(Path.GetFullPath(sourceFile)));
    }

    private void PlantProject(string plantedRelativePath)
    {
        File.WriteAllText(Path.Combine(_root, "main.spy"), "def main() -> None:\n    print(1)\n");
        File.WriteAllText(Path.Combine(_root, "planted.spyproj"),
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<Project>\n  <PropertyGroup>\n"
            + "    <RootNamespace>Planted</RootNamespace>\n    <OutputType>exe</OutputType>\n"
            + "    <TargetFramework>net10.0</TargetFramework>\n    <AssemblyName>Planted</AssemblyName>\n"
            + "    <EntryPoint>main.spy</EntryPoint>\n  </PropertyGroup>\n  <ItemGroup>\n"
            + "    <SourceFile Include=\"**/*.spy\" />\n  </ItemGroup>\n</Project>\n");

        var plantedDir = Path.Combine(_root,
            plantedRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(plantedDir);
        File.WriteAllText(Path.Combine(plantedDir, "main.spy"),
            "def main() -> None:\n    print(2)\n");
    }

    [Theory]
    [InlineData("bin/Debug/net10.0/.sharpy-crash/20260827/sources")]
    [InlineData("obj/Debug")]
    public void Formatter_ExcludesPlantedSources(string plantedRelativePath)
    {
        File.WriteAllText(Path.Combine(_root, "real.spy"), "x: int = 1\n");

        var plantedDir = Path.Combine(_root,
            plantedRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(plantedDir);
        File.WriteAllText(Path.Combine(plantedDir, "stale.spy"), "y: int = 2\n");

        var discovered = FormatRunner.FindSharpyFiles(_root);

        Assert.Single(discovered);
        Assert.EndsWith("real.spy", discovered[0]);
    }
}

/// <summary>
/// Cell (a) of the compile-side twin: the multi-file test harness
/// (<see cref="IntegrationTestBase.CompileAndExecuteProject"/>) globs the project directory
/// itself. With a crash-bundle copy of <c>main.spy</c> planted under <c>bin/</c>, the harness
/// must compile exactly the real source — the planted copy would otherwise be a second entry
/// point (CS0017 behind SPY0908), the #1660 symptom.
/// </summary>
public class CompileSideCorpusBoundaryHarnessTests : IntegrationTestBase
{
    public CompileSideCorpusBoundaryHarnessTests(ITestOutputHelper output) : base(output)
    {
    }

    [Theory]
    [InlineData("bin/Debug/net10.0/.sharpy-crash/20260827/sources")]
    [InlineData("obj/Debug")]
    public void MultiFileHarness_ExcludesPlantedSources(string plantedRelativePath)
    {
        var root = Path.Combine(Path.GetTempPath(), $"sharpy_boundary_harness_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "main.spy"), "def main() -> None:\n    print(1)\n");
            var plantedDir = Path.Combine(root,
                plantedRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(plantedDir);
            File.WriteAllText(Path.Combine(plantedDir, "main.spy"),
                "def main() -> None:\n    print(2)\n");

            var result = CompileAndExecuteProject(root, "main.spy");

            Assert.True(result.Success, string.Join("; ", result.CompilationErrors));
            Assert.Equal("1", result.StandardOutput.Trim());
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
            }
        }
    }
}
