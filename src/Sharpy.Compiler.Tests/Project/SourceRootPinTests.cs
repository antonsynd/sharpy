using FluentAssertions;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// <c>&lt;SourceRoot&gt;</c> pins the module root that absolute imports, module namespaces and the
/// generated-C# unit keys are spelled from (<see cref="ProjectCompiler.ComputeSourceRootPath"/>).
/// </summary>
/// <remarks>
/// <para>
/// Without a pin the root is the longest common directory of the sources, so it floats: with every
/// source under <c>src/Scripts/</c>, <c>from Core.greeting import greet</c> compiles and the
/// namespace is <c>Game.Core.Greeting</c>; adding <c>src/Other/thing.spy</c> moves the root to
/// <c>src/</c>, the import fails with SPY0300 and the namespace becomes
/// <c>Game.Scripts.Core.Greeting</c>. The Unity plugin compiles a game's whole <c>Assets/</c> tree
/// as one project, where a renamed type breaks every serialized component reference.
/// </para>
/// <para>
/// Cells: the build {cold, incremental} × the step {before, after a source appears in a new
/// top-level folder}. Every cell compiles the same absolute import spelled from the pinned root,
/// keeps the same namespace and writes the unit under the same key. The load-time refusals (missing
/// root, a source outside it) and the unpinned default are pinned beside them.
/// </para>
/// </remarks>
[Collection("HeavyCompilation")]
public class SourceRootPinTests
{
    private readonly ITestOutputHelper _output;

    public SourceRootPinTests(ITestOutputHelper output) => _output = output;

    private const string Greeting = "def greet() -> str:\n    return \"hi\"\n";
    private const string Smoke = "from Scripts.Core.greeting import greet\n\ndef hello() -> str:\n    return greet()\n";

    private static string Key(params string[] segments) => Path.Combine(segments);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PinnedRoot_KeepsImportsNamespacesAndKeys_WhenANewTopLevelFolderAppears(bool incremental)
    {
        using var helper = new ProjectCompilationHelper(_output);
        helper.WithRootNamespace("Game").WithOutputType("library").WithIncremental(incremental);
        helper.Options.SourceRoot = "src";
        helper.AddSourceFile("Scripts/Core/greeting.spy", Greeting);
        helper.AddSourceFile("Scripts/Smoke/smoke.spy", Smoke);
        helper.CreateProjectFile();

        foreach (var step in new[] { "before", "after" })
        {
            if (step == "after")
            {
                helper.AddSourceFile("Other/thing.spy", "def thing() -> int:\n    return 1\n");
            }

            var result = helper.Compile();
            var cell = $"incremental={incremental}/{step}";
            result.Success.Should().BeTrue(
                $"[{cell}] the import is spelled from the pinned root\n"
                + string.Join("\n", result.Diagnostics.GetErrors().Select(d => d.Message)));

            var files = result.GeneratedCSharpFiles;
            files.Keys.Should().Contain(Key("Scripts", "Core", "greeting.cs"), $"[{cell}] keys mirror the pinned root");
            files.Keys.Should().Contain(Key("Scripts", "Smoke", "smoke.cs"), $"[{cell}]");
            files[Key("Scripts", "Core", "greeting.cs")].Should().Contain("namespace Game.Scripts.Core.Greeting", $"[{cell}]");
            files[Key("Scripts", "Smoke", "smoke.cs")].Should().Contain("namespace Game.Scripts.Smoke.Smoke", $"[{cell}]");
            if (step == "after")
            {
                files.Keys.Should().Contain(Key("Other", "thing.cs"), $"[{cell}]");
                files[Key("Other", "thing.cs")].Should().Contain("namespace Game.Other.Thing", $"[{cell}]");
            }
        }
    }

    [Fact]
    public void PinnedRoot_TwoModulesSharingAStem_GetDistinctKeysAndNamespaces()
    {
        using var helper = new ProjectCompilationHelper(_output);
        helper.WithRootNamespace("Game").WithOutputType("library");
        helper.Options.SourceRoot = "src";
        helper.AddSourceFile("Scripts/Core/greeting.spy", Greeting);
        helper.AddSourceFile("Scripts/Ui/greeting.spy", "def banner() -> str:\n    return \"ui\"\n");
        helper.CreateProjectFile();

        var result = helper.Compile();

        result.Success.Should().BeTrue(string.Join("\n", result.Diagnostics.GetErrors().Select(d => d.Message)));
        result.GeneratedCSharpFiles[Key("Scripts", "Core", "greeting.cs")]
            .Should().Contain("namespace Game.Scripts.Core.Greeting").And.Contain("Greet(");
        result.GeneratedCSharpFiles[Key("Scripts", "Ui", "greeting.cs")]
            .Should().Contain("namespace Game.Scripts.UI.Greeting").And.Contain("Banner(");
    }

    [Theory]
    [InlineData("relative")]
    [InlineData("relative-trailing-separator")]
    [InlineData("absolute")]
    public void Load_ResolvesTheRoot_AndComputeSourceRootPathReturnsIt(string spelling)
    {
        using var helper = new ProjectCompilationHelper(_output);
        var expected = Path.Combine(helper.ProjectDirectory, "src");
        helper.Options.SourceRoot = spelling switch
        {
            "relative" => "src",
            "relative-trailing-separator" => "src/",
            _ => expected,
        };
        helper.AddSourceFile("Scripts/Core/greeting.spy", Greeting);
        helper.CreateProjectFile();

        var config = ProjectFileParser.Load(Path.Combine(helper.ProjectDirectory, "TestProject.spyproj"));

        config.SourceRoot.Should().Be(expected);
        ProjectCompiler.ComputeSourceRootPath(config).Should().Be(expected,
            "the pin wins over the sources' common directory (src/Scripts/Core)");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Load_WithoutAPin_KeepsTheCommonDirectory(string? sourceRoot)
    {
        // The unpinned default is unchanged: the root is the sources' longest common directory.
        using var helper = new ProjectCompilationHelper(_output);
        helper.Options.SourceRoot = sourceRoot;
        helper.AddSourceFile("Scripts/Core/greeting.spy", Greeting);
        helper.AddSourceFile("Scripts/Smoke/smoke.spy", Smoke);
        helper.CreateProjectFile();

        var config = ProjectFileParser.Load(Path.Combine(helper.ProjectDirectory, "TestProject.spyproj"));

        config.SourceRoot.Should().BeNull();
        ProjectCompiler.ComputeSourceRootPath(config).Should().Be(Path.Combine(helper.SourceDirectory, "Scripts"));
    }

    [Fact]
    public void Load_SourceOutsideThePinnedRoot_IsRefused()
    {
        using var helper = new ProjectCompilationHelper(_output);
        helper.Options.SourceRoot = "src/Scripts";
        helper.AddSourceFile("Scripts/Core/greeting.spy", Greeting);
        helper.AddSourceFile("Other/thing.spy", "def thing() -> int:\n    return 1\n");
        helper.CreateProjectFile();

        var act = () => ProjectFileParser.Load(Path.Combine(helper.ProjectDirectory, "TestProject.spyproj"));

        act.Should().Throw<InvalidDataException>()
            .WithMessage("Invalid .spyproj file: 1 source file(s) outside <SourceRoot>*")
            .Which.Message.Should().Contain(Path.Combine(helper.SourceDirectory, "Other", "thing.spy"))
            .And.NotContain("greeting.spy");
    }

    [Fact]
    public void Load_SourcesInsideThePinnedRoot_AreAccepted()
    {
        // Positive control for the refusal above: the same tree, pinned one level up, loads.
        using var helper = new ProjectCompilationHelper(_output);
        helper.Options.SourceRoot = "src";
        helper.AddSourceFile("Scripts/Core/greeting.spy", Greeting);
        helper.AddSourceFile("Other/thing.spy", "def thing() -> int:\n    return 1\n");
        helper.CreateProjectFile();

        var config = ProjectFileParser.Load(Path.Combine(helper.ProjectDirectory, "TestProject.spyproj"));

        config.SourceFiles.Should().HaveCount(2);
    }

    [Fact]
    public void Load_AMissingPinnedRoot_IsRefused()
    {
        using var helper = new ProjectCompilationHelper(_output);
        helper.Options.SourceRoot = "no_such_dir";
        helper.AddSourceFile("Scripts/Core/greeting.spy", Greeting);
        helper.CreateProjectFile();

        var act = () => ProjectFileParser.Load(Path.Combine(helper.ProjectDirectory, "TestProject.spyproj"));

        act.Should().Throw<InvalidDataException>()
            .WithMessage("Invalid .spyproj file: <SourceRoot> 'no_such_dir' resolves to '*no_such_dir', which does not exist");
    }
}
