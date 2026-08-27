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

    [Theory]
    [InlineData("bin/Debug/net10.0/.sharpy-crash/20260827/sources")]
    [InlineData("bin/Debug/net10.0")]
    [InlineData("obj/Debug")]
    public void Compiler_ExcludesPlantedSources(string plantedRelativePath)
    {
        var realSource = "def main() -> None:\n    print(1)\n";
        File.WriteAllText(Path.Combine(_root, "main.spy"), realSource);

        var plantedDir = Path.Combine(_root,
            plantedRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(plantedDir);
        File.WriteAllText(Path.Combine(plantedDir, "main.spy"),
            "def main() -> None:\n    print(2)\n");

        var sourceFiles = Directory.GetFiles(_root, "*.spy", SearchOption.AllDirectories)
            .Where(f => !CrashBundleWriter.IsNonSourceSegment(
                Path.GetRelativePath(_root, f)))
            .ToList();

        Assert.Single(sourceFiles);
        Assert.Contains("main.spy", sourceFiles[0]);
        Assert.DoesNotContain(plantedRelativePath.Split('/')[0], sourceFiles[0]);
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
