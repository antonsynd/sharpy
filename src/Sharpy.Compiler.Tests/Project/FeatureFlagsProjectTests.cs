using System.IO;
using FluentAssertions;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// Tests for &lt;Features&gt; parsing in .spyproj and the CLI/project feature merge (C1).
/// </summary>
public class FeatureFlagsProjectTests
{
    private readonly ITestOutputHelper _output;

    public FeatureFlagsProjectTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void ProjectFeatures_AreParsedAndEffectiveInCompilation()
    {
        using var helper = new ProjectCompilationHelper(_output);
        helper.WithRootNamespace("FeatTest").WithEntryPoint("main.spy");
        helper.Options.Features.Add("__test_feature");
        helper.AddSourceFile("main.spy", "def main() -> None:\n    print(\"hi\")\n", isEntryPoint: true);

        var result = helper.Compile();

        result.Success.Should().BeTrue();
        result.EffectiveFeatures.IsEnabled("__test_feature").Should().BeTrue();
    }

    [Fact]
    public void CliAndProjectFeatures_AreUnioned()
    {
        using var helper = new ProjectCompilationHelper(_output);
        helper.WithRootNamespace("FeatMerge").WithEntryPoint("main.spy");
        // Project supplies the feature via <Features>; the CLI also supplies it. The
        // effective set is the union (here, the same single feature).
        helper.Options.Features.Add("__test_feature");
        helper.CliFeatures.Add("__test_feature");
        helper.AddSourceFile("main.spy", "def main() -> None:\n    print(\"hi\")\n", isEntryPoint: true);

        var result = helper.Compile();

        result.Success.Should().BeTrue();
        result.EffectiveFeatures.IsEnabled("__test_feature").Should().BeTrue();
    }

    [Fact]
    public void NoFeatures_LeavesEffectiveSetEmpty()
    {
        using var helper = new ProjectCompilationHelper(_output);
        helper.WithRootNamespace("FeatNone").WithEntryPoint("main.spy");
        helper.AddSourceFile("main.spy", "def main() -> None:\n    print(\"hi\")\n", isEntryPoint: true);

        var result = helper.Compile();

        result.Success.Should().BeTrue();
        result.EffectiveFeatures.IsEnabled("__test_feature").Should().BeFalse();
    }

    [Fact]
    public void UnknownProjectFeature_FailsFastAtLoad()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"sharpy_feat_{System.Guid.NewGuid()}");
        Directory.CreateDirectory(dir);
        try
        {
            var projPath = Path.Combine(dir, "bad.spyproj");
            File.WriteAllText(projPath,
                "<Project>\n  <PropertyGroup>\n    <RootNamespace>Bad</RootNamespace>\n" +
                "    <Features>not_a_feature</Features>\n  </PropertyGroup>\n" +
                "  <ItemGroup>\n    <SourceFile Include=\"main.spy\" />\n  </ItemGroup>\n</Project>\n");
            File.WriteAllText(Path.Combine(dir, "main.spy"), "def main() -> None:\n    pass\n");

            var act = () => ProjectFileParser.Load(projPath);
            act.Should().Throw<InvalidDataException>().WithMessage("*not_a_feature*");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void SpyProjectLoader_ParsesFeatures_AndMapsToProjectConfig()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"sharpy_feat_{System.Guid.NewGuid()}");
        Directory.CreateDirectory(dir);
        try
        {
            var projPath = Path.Combine(dir, "loader.spyproj");
            File.WriteAllText(projPath,
                "<Project>\n  <PropertyGroup>\n    <RootNamespace>Loader</RootNamespace>\n" +
                "    <Features>__test_feature</Features>\n  </PropertyGroup>\n" +
                "  <ItemGroup>\n    <SourceFile Include=\"main.spy\" />\n  </ItemGroup>\n</Project>\n");
            File.WriteAllText(Path.Combine(dir, "main.spy"), "def main() -> None:\n    pass\n");

            var project = SpyProjectLoader.Load(projPath);
            project.Features.Should().Contain("__test_feature");
            project.ToProjectConfig().Features.Should().Contain("__test_feature");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
