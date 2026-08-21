using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Semantic;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// Warm ≡ cold: analyzing with a cached analysis context (ModuleRegistry + BuiltinRegistry)
/// must produce identical results to analyzing with a fresh context. The cache key is the
/// reference + module-path + package-reference set with mtimes; touching a reference or
/// changing a package reference invalidates.
/// </summary>
public class SharedAnalysisContextDifferentialTests
{
    private readonly ITestOutputHelper _output;

    public SharedAnalysisContextDifferentialTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private const string MathSource = @"
import math

def get_pi() -> float:
    return math.pi
";

    private const string SimpleSource = @"
def greet() -> str:
    return ""hello""
";

    [Fact]
    public void CachedAnalysis_ProducesIdenticalDiagnostics_ToFreshAnalysis()
    {
        var api = new CompilerApi();

        var cold = api.Analyze(MathSource);
        var warm = api.Analyze(MathSource);

        warm.Success.Should().Be(cold.Success,
            "cached analysis must agree with fresh analysis on success");

        var coldCodes = cold.Diagnostics.Select(d => d.Code).OrderBy(c => c).ToList();
        var warmCodes = warm.Diagnostics.Select(d => d.Code).OrderBy(c => c).ToList();
        warmCodes.Should().BeEquivalentTo(coldCodes,
            "cached analysis must produce the same diagnostic codes as fresh analysis");
    }

    [Fact]
    public void CachedAnalysis_ResolvesModuleExports_Identically()
    {
        var api = new CompilerApi();

        var cold = api.Analyze(MathSource);
        var warm = api.Analyze(MathSource);

        cold.SymbolTable.Should().NotBeNull("cold analysis must produce a symbol table");
        warm.SymbolTable.Should().NotBeNull("warm analysis must produce a symbol table");

        var coldFn = cold.SymbolTable!.Lookup("get_pi") as FunctionSymbol;
        var warmFn = warm.SymbolTable!.Lookup("get_pi") as FunctionSymbol;
        coldFn.Should().NotBeNull();
        warmFn.Should().NotBeNull();

        coldFn!.ReturnType.ToString().Should().Be(warmFn!.ReturnType.ToString(),
            "cached analysis must resolve the same return types as fresh analysis");
    }

    private const string AltSource = @"
def add(a: int, b: int) -> int:
    return a + b
";

    [Fact]
    public void DifferentSources_ShareCachedContext_WithIdenticalReferences()
    {
        var api = new CompilerApi();

        var result1 = api.Analyze(SimpleSource);
        var result2 = api.Analyze(AltSource);
        var result3 = api.Analyze(SimpleSource);

        result1.Success.Should().BeTrue();
        result2.Success.Should().BeTrue();
        result3.Success.Should().BeTrue();

        result3.Diagnostics.Select(d => d.Code).OrderBy(c => c).Should().BeEquivalentTo(
            result1.Diagnostics.Select(d => d.Code).OrderBy(c => c),
            "third analysis (same source as first) must match first analysis");
    }

    /// <summary>
    /// Exercises the shared-TypeSymbol materialization path a bare function cannot: stdlib
    /// modules whose types carry interfaces (math, collections.Counter), a user class
    /// inheriting a CLR base (Exception), and calls forcing member resolution.
    /// </summary>
    private const string InheritanceSource = @"
import math
import collections

class ShapeError(Exception):
    def __init__(self, message: str) -> None:
        super().__init__(message)

def circle_area(radius: float) -> float:
    if radius < 0.0:
        raise ShapeError(""negative radius"")
    return math.pi * radius * radius

def count_words(words: list[str]) -> int:
    counter = collections.Counter[str](words)
    pairs = counter.most_common(1)
    return len(pairs)
";

    private static string[] StdlibReferences()
    {
        var baseDir = Path.GetDirectoryName(
            typeof(SharedAnalysisContextDifferentialTests).Assembly.Location)!;
        return new[]
        {
            Path.Combine(baseDir, "Sharpy.Core.dll"),
            Path.Combine(baseDir, "Sharpy.Stdlib.dll")
        };
    }

    private static (string? BaseName, int InterfaceCount, string? AreaReturn, string? CountReturn)
        ProbeMemberResolution(SemanticResult result)
    {
        var shapeError = result.SymbolTable?.Lookup("ShapeError") as TypeSymbol;
        var area = result.SymbolTable?.Lookup("circle_area") as FunctionSymbol;
        var count = result.SymbolTable?.Lookup("count_words") as FunctionSymbol;
        return (shapeError?.BaseType?.Name, shapeError?.Interfaces.Count ?? -1,
            area?.ReturnType?.ToString(), count?.ReturnType?.ToString());
    }

    [Fact]
    public void ConcurrentAnalyses_OnSharedContext_MatchFreshContextAnalysis()
    {
        var refs = StdlibReferences();
        refs.Should().OnlyContain(r => File.Exists(r), "stdlib assemblies must be in the test output");

        var sharedApi = new CompilerApi(null, refs);
        sharedApi.Analyze(InheritanceSource);

        const int parallelism = 4;
        var results = new SemanticResult[parallelism];
        var exceptions = new Exception?[parallelism];

        Parallel.For(0, parallelism, i =>
        {
            try
            {
                results[i] = sharedApi.Analyze(InheritanceSource);
            }
            catch (Exception ex)
            {
                exceptions[i] = ex;
            }
        });

        var fresh = new CompilerApi(null, refs).Analyze(InheritanceSource);
        fresh.Success.Should().BeTrue(
            "fresh-context analysis of the inheritance source must succeed; diagnostics: {0}",
            string.Join("; ", fresh.Diagnostics.Select(d => $"{d.Code}: {d.Message}")));
        var freshCodes = fresh.Diagnostics.Select(d => d.Code).OrderBy(c => c).ToList();
        var freshProbe = ProbeMemberResolution(fresh);
        freshProbe.BaseName.Should().NotBeNull("ShapeError's CLR base must resolve");
        freshProbe.AreaReturn.Should().NotBeNull("circle_area's return type must resolve");

        for (int i = 0; i < parallelism; i++)
        {
            exceptions[i].Should().BeNull($"shared-context analysis {i} must not throw");
            results[i].Should().NotBeNull($"shared-context analysis {i} must produce a result");
            results[i].Success.Should().Be(fresh.Success,
                $"shared-context analysis {i} must agree with the fresh-context analysis on success");

            var codes = results[i].Diagnostics.Select(d => d.Code).OrderBy(c => c).ToList();
            codes.Should().BeEquivalentTo(freshCodes,
                $"shared-context analysis {i} must produce the fresh-context diagnostics");

            ProbeMemberResolution(results[i]).Should().Be(freshProbe,
                $"shared-context analysis {i} must resolve inheritance and member types "
                + "identically to a fresh context");
        }
    }

    [Fact]
    public void TouchedReference_InvalidatesCachedAnalysisContext()
    {
        var baseDir = Path.GetDirectoryName(
            typeof(SharedAnalysisContextDifferentialTests).Assembly.Location)!;
        var realCore = Path.Combine(baseDir, "Sharpy.Core.dll");
        File.Exists(realCore).Should().BeTrue($"Sharpy.Core.dll must be in the test output at {realCore}");

        var tempDir = Path.Combine(Path.GetTempPath(), "sharpy-mtime-cell-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            // A copy in a temp dir so bumping the mtime never touches real build outputs.
            var refCopy = Path.Combine(tempDir, "Sharpy.Core.dll");
            File.Copy(realCore, refCopy);

            var api = new CompilerApi();
            var config = new ProjectConfig
            {
                ProjectFilePath = Path.Combine(tempDir, "mtime.spyproj"),
                ProjectDirectory = tempDir,
                RootNamespace = "MtimeCell"
            };
            config.References.Add(refCopy);

            var (registry1, _) = api.GetOrBuildAnalysisContext(config);
            var (registry2, _) = api.GetOrBuildAnalysisContext(config);
            registry1.Should().NotBeNull("a config with a reference must build a registry");
            ReferenceEquals(registry1, registry2).Should().BeTrue(
                "an untouched reference set must hit the cached context (positive control)");

            File.SetLastWriteTimeUtc(refCopy, File.GetLastWriteTimeUtc(refCopy).AddSeconds(7));

            var (registry3, _) = api.GetOrBuildAnalysisContext(config);
            ReferenceEquals(registry1, registry3).Should().BeFalse(
                "touching a referenced assembly's mtime must invalidate the cached context");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static ProjectConfig ConfigWithPackages(params PackageRef[] packages)
    {
        var config = new ProjectConfig
        {
            ProjectFilePath = "/test/pkg.spyproj",
            ProjectDirectory = "/test",
            RootNamespace = "PkgDelta"
        };
        config.PackageReferences.AddRange(packages);
        return config;
    }

    [Fact]
    public void PackageReferenceDelta_ProducesDifferentKey_AndFreshContext()
    {
        // Two configs identical except for PackageReferences must not share a cached analysis
        // context: BuildModuleRegistry loads assemblies resolved from PackageReferences, so a
        // key that omitted them served a registry missing (or carrying) package modules.
        var api = new CompilerApi();

        var baseConfig = ConfigWithPackages(new PackageRef("Sharpy.Test.FakeA", "1.0.0"));
        var withExtraPackage = ConfigWithPackages(
            new PackageRef("Sharpy.Test.FakeA", "1.0.0"),
            new PackageRef("Sharpy.Test.FakeB", "2.0.0"));

        var baseKey = api.BuildAnalysisCacheKey(baseConfig);
        var extraKey = api.BuildAnalysisCacheKey(withExtraPackage);
        extraKey.SequenceEqual(baseKey).Should().BeFalse(
            "configs that differ only in PackageReferences must produce different cache keys");

        var (registry1, _) = api.GetOrBuildAnalysisContext(baseConfig);
        var (registry2, _) = api.GetOrBuildAnalysisContext(baseConfig);
        ReferenceEquals(registry1, registry2).Should().BeTrue(
            "an unchanged config must hit the cached context (positive control)");

        var (registry3, _) = api.GetOrBuildAnalysisContext(withExtraPackage);
        ReferenceEquals(registry1, registry3).Should().BeFalse(
            "a config with different PackageReferences must get a fresh registry");
    }

    [Fact]
    public void CallerOptions_NotMutated_AcrossMultipleAnalyses()
    {
        var api = new CompilerApi();
        var options = new CompilerOptions { OutputType = "library" };
        var originalRefs = options.References;

        api.Analyze(MathSource, options);
        api.Analyze(SimpleSource, options);
        api.Analyze(MathSource, options);

        options.References.Should().BeEquivalentTo(originalRefs,
            "caller options must not be mutated by analysis (#1140 H8)");
    }
}
