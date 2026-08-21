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

    [Fact]
    public void ConcurrentAnalyses_ProduceConsistentResults()
    {
        var api = new CompilerApi();

        api.Analyze(SimpleSource);

        const int parallelism = 4;
        var results = new SemanticResult[parallelism];
        var exceptions = new Exception?[parallelism];

        Parallel.For(0, parallelism, i =>
        {
            try
            {
                results[i] = api.Analyze(SimpleSource);
            }
            catch (Exception ex)
            {
                exceptions[i] = ex;
            }
        });

        for (int i = 0; i < parallelism; i++)
        {
            exceptions[i].Should().BeNull($"analysis {i} must not throw");
            results[i].Should().NotBeNull($"analysis {i} must produce a result");
            results[i].Success.Should().BeTrue($"analysis {i} must succeed");
        }

        var baseline = results[0].Diagnostics.Select(d => d.Code).OrderBy(c => c).ToList();
        for (int i = 1; i < parallelism; i++)
        {
            var codes = results[i].Diagnostics.Select(d => d.Code).OrderBy(c => c).ToList();
            codes.Should().BeEquivalentTo(baseline,
                $"concurrent analysis {i} must produce the same diagnostics as analysis 0");
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
