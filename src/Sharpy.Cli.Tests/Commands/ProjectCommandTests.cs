using FluentAssertions;
using Xunit;

namespace Sharpy.Cli.Tests.Commands;

public class ProjectCommandTests
{
    [Fact]
    public void Parses_WithProjectFileArgument()
    {
        var result = CliTestHarness.Parse("project app.spyproj");

        result.Errors.Should().BeEmpty();
        result.CommandResult.Command.Name.Should().Be("project");
        result.GetValue<FileInfo?>("project")!.Name.Should().Be("app.spyproj");
    }

    [Fact]
    public void Parses_WithoutProjectFile_ArgumentIsOptional()
    {
        // The project argument has ZeroOrOne arity (auto-discovery).
        var result = CliTestHarness.Parse("project");

        result.Errors.Should().BeEmpty();
        result.GetValue<FileInfo?>("project").Should().BeNull();
    }

    [Fact]
    public void Parses_IncrementalFlag()
    {
        var result = CliTestHarness.Parse("project app.spyproj --incremental");

        result.Errors.Should().BeEmpty();
        result.GetValue<bool>("--incremental").Should().BeTrue();
    }

    [Fact]
    public void Parses_CleanFlag()
    {
        var result = CliTestHarness.Parse("project app.spyproj --clean");

        result.Errors.Should().BeEmpty();
        result.GetValue<bool>("--clean").Should().BeTrue();
    }

    [Fact]
    public void Parses_EmitCsToDirectory()
    {
        var result = CliTestHarness.Parse("project app.spyproj --emit-cs-to generated");

        result.Errors.Should().BeEmpty();
        result.GetValue<DirectoryInfo?>("--emit-cs-to")!.Name.Should().Be("generated");
    }

    [Theory]
    [InlineData("Debug")]
    [InlineData("Release")]
    public void Parses_ConfigurationOption(string config)
    {
        var longForm = CliTestHarness.Parse($"project app.spyproj --configuration {config}");
        var shortForm = CliTestHarness.Parse($"project app.spyproj -c {config}");

        longForm.Errors.Should().BeEmpty();
        shortForm.Errors.Should().BeEmpty();
        longForm.GetValue<string?>("--configuration").Should().Be(config);
        shortForm.GetValue<string?>("--configuration").Should().Be(config);
    }

    [Fact]
    public void Rejects_UnknownOption()
    {
        var result = CliTestHarness.Parse("project app.spyproj --nope");

        result.Errors.Should().NotBeEmpty();
    }

    // ---- Invocation-level error tests ----

    [Fact]
    public void Project_MissingProjectFile_ReturnsExitCode1()
    {
        using var ws = new TempWorkspace();
        var missing = ws.PathFor("nope.spyproj");

        var invocation = CliTestHarness.Invoke($"project \"{missing}\"");

        invocation.ExitCode.Should().Be(1);
        invocation.StdErr.Should().Contain("Error");
    }

    [Fact]
    public void Project_BuildFailure_ReturnsExitCode1()
    {
        using var ws = new TempWorkspace();
        ws.WriteFile("lib.spy", "def 123invalid():\n    return 0\n");
        var proj = ws.WriteFile("BadProj.spyproj",
            "<Project>\n" +
            "  <PropertyGroup>\n" +
            "    <RootNamespace>BadProj</RootNamespace>\n" +
            "    <OutputType>library</OutputType>\n" +
            "    <TargetFramework>net10.0</TargetFramework>\n" +
            "  </PropertyGroup>\n" +
            "  <ItemGroup>\n" +
            "    <SourceFile Include=\"*.spy\" />\n" +
            "  </ItemGroup>\n" +
            "</Project>\n");

        var invocation = CliTestHarness.Invoke($"project \"{proj}\"");

        invocation.ExitCode.Should().Be(1);
    }

    // ---- --emit-cs-to layout (#2060) ----
    // Contract: every unit is written to a distinct path that mirrors its source path relative
    // to the project directory. Cells: flat module, package module, same stem in two directories.

    private static string WriteLibraryProject(TempWorkspace ws)
        => ws.WriteFile("EmitProj.spyproj",
            "<Project>\n" +
            "  <PropertyGroup>\n" +
            "    <RootNamespace>EmitProj</RootNamespace>\n" +
            "    <OutputType>library</OutputType>\n" +
            "    <TargetFramework>net10.0</TargetFramework>\n" +
            "  </PropertyGroup>\n" +
            "  <ItemGroup>\n" +
            "    <SourceFile Include=\"**/*.spy\" />\n" +
            "  </ItemGroup>\n" +
            "</Project>\n");

    private static string MarkerModule(string functionName)
        => $"def {functionName}() -> int:\n    return 1\n";

    private static CliInvocation EmitTo(string proj, string outDir)
    {
        var invocation = CliTestHarness.Invoke($"project \"{proj}\" --emit-cs-to \"{outDir}\"");
        invocation.ExitCode.Should().Be(0, $"stdout:\n{invocation.StdOut}\nstderr:\n{invocation.StdErr}");
        return invocation;
    }

    [Fact]
    public void EmitCsTo_FlatModule_WritesAtProjectRelativePath()
    {
        using var ws = new TempWorkspace();
        ws.WriteFile("lib.spy", MarkerModule("flat_marker"));
        var proj = WriteLibraryProject(ws);
        var outDir = ws.PathFor("gen");

        EmitTo(proj, outDir);

        var flat = Path.Combine(outDir, "lib.cs");
        File.Exists(flat).Should().BeTrue();
        File.ReadAllText(flat).Should().Contain("FlatMarker");
        Directory.GetFiles(outDir, "*.cs", SearchOption.AllDirectories).Should().HaveCount(1);
    }

    [Fact]
    public void EmitCsTo_PackageModule_MirrorsItsDirectory()
    {
        using var ws = new TempWorkspace();
        ws.WriteFile(Path.Combine("pkg", "util.spy"), MarkerModule("package_marker"));
        var proj = WriteLibraryProject(ws);
        var outDir = ws.PathFor("gen");

        EmitTo(proj, outDir);

        var mirrored = Path.Combine(outDir, "pkg", "util.cs");
        File.Exists(mirrored).Should().BeTrue();
        File.ReadAllText(mirrored).Should().Contain("PackageMarker");
        File.Exists(Path.Combine(outDir, "util.cs")).Should().BeFalse(
            "a package module is written under its directory, not flattened to the output root");
    }

    [Fact]
    public void EmitCsTo_SameStemInTwoDirectories_WritesBothWithTheirOwnContent()
    {
        using var ws = new TempWorkspace();
        ws.WriteFile(Path.Combine("src", "lib.spy"), MarkerModule("flat_marker"));
        ws.WriteFile(Path.Combine("src", "pkg", "lib.spy"), MarkerModule("package_marker"));
        var proj = WriteLibraryProject(ws);
        var outDir = ws.PathFor("gen");

        EmitTo(proj, outDir);

        var flat = Path.Combine(outDir, "src", "lib.cs");
        var package = Path.Combine(outDir, "src", "pkg", "lib.cs");
        File.Exists(flat).Should().BeTrue();
        File.Exists(package).Should().BeTrue();

        var flatCs = File.ReadAllText(flat);
        var packageCs = File.ReadAllText(package);
        flatCs.Should().Contain("FlatMarker").And.NotContain("PackageMarker");
        packageCs.Should().Contain("PackageMarker").And.NotContain("FlatMarker");
        Directory.GetFiles(outDir, "*.cs", SearchOption.AllDirectories).Should().HaveCount(2);
    }

    [Theory]
    [InlineData("lib.cs", "lib.cs")]
    [InlineData("pkg/lib.cs", "pkg/lib.cs")]
    [InlineData("src/pkg/lib.spy", "src/pkg/lib.cs")]
    // A source outside the project directory: leading `..` segments are dropped, as the
    // module path drops them (`../ext/outer.spy` is module `ext.outer`).
    [InlineData("../outside.cs", "outside.cs")]
    [InlineData("../../ext/outer.cs", "ext/outer.cs")]
    // A rooted key (a source on another drive) keeps its path below the root.
    [InlineData("/abs/x.cs", "abs/x.cs")]
    public void MirroredCSharpOutputPath_StaysUnderTheOutputDirectory(string unitKey, string expectedRelative)
    {
        using var ws = new TempWorkspace();
        var outDir = ws.PathFor("gen");

        var path = CliHelpers.MirroredCSharpOutputPath(outDir, unitKey);

        path.Should().Be(Path.GetFullPath(Path.Combine(outDir, expectedRelative)));
    }

    [Fact]
    public void MirroredCSharpOutputPath_ThrowsOnAMidPathEscape()
    {
        // Unreachable from Path.GetRelativePath keys; the check is a backstop that the helper
        // never returns a path outside the output directory.
        using var ws = new TempWorkspace();

        var act = () => CliHelpers.MirroredCSharpOutputPath(ws.PathFor("gen"), "pkg/../../outside.cs");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void EmitCsTo_SourceOutsideProjectDirectory_IsWrittenInsideTheOutputDirectory()
    {
        using var ws = new TempWorkspace();
        ws.WriteFile(Path.Combine("proj", "inner.spy"), MarkerModule("flat_marker"));
        ws.WriteFile(Path.Combine("ext", "outer.spy"), MarkerModule("package_marker"));
        var proj = ws.WriteFile(Path.Combine("proj", "EmitProj.spyproj"),
            "<Project>\n" +
            "  <PropertyGroup>\n" +
            "    <RootNamespace>EmitProj</RootNamespace>\n" +
            "    <OutputType>library</OutputType>\n" +
            "    <TargetFramework>net10.0</TargetFramework>\n" +
            "  </PropertyGroup>\n" +
            "  <ItemGroup>\n" +
            "    <SourceFile Include=\"*.spy\" />\n" +
            "    <SourceFile Include=\"../ext/*.spy\" />\n" +
            "  </ItemGroup>\n" +
            "</Project>\n");
        var outDir = ws.PathFor(Path.Combine("proj", "gen"));

        EmitTo(proj, outDir);

        File.ReadAllText(Path.Combine(outDir, "inner.cs")).Should().Contain("FlatMarker");
        File.ReadAllText(Path.Combine(outDir, "ext", "outer.cs")).Should().Contain("PackageMarker");
        Directory.GetFiles(ws.PathFor("proj"), "*.cs", SearchOption.TopDirectoryOnly).Should().BeEmpty(
            "the out-of-project unit must not be written above the output directory");
    }
    [Fact]
    public void EmitCsTo_TwoUnitsMappingToOnePath_RefusesTheSecondInsteadOfOverwriting()
    {
        // `../ext/lib.spy` (outside the project) and `ext/lib.spy` (inside) both mirror to
        // ext/lib.cs. The second unit in key order is refused loudly; the first keeps its content.
        using var ws = new TempWorkspace();
        ws.WriteFile(Path.Combine("proj", "ext", "lib.spy"), MarkerModule("flat_marker"));
        ws.WriteFile(Path.Combine("ext", "lib.spy"), MarkerModule("package_marker"));
        var proj = ws.WriteFile(Path.Combine("proj", "EmitProj.spyproj"),
            "<Project>\n" +
            "  <PropertyGroup>\n" +
            "    <RootNamespace>EmitProj</RootNamespace>\n" +
            "    <OutputType>library</OutputType>\n" +
            "    <TargetFramework>net10.0</TargetFramework>\n" +
            "  </PropertyGroup>\n" +
            "  <ItemGroup>\n" +
            "    <SourceFile Include=\"**/*.spy\" />\n" +
            "    <SourceFile Include=\"../ext/*.spy\" />\n" +
            "  </ItemGroup>\n" +
            "</Project>\n");
        var outDir = ws.PathFor(Path.Combine("proj", "gen"));

        var invocation = CliTestHarness.Invoke($"project \"{proj}\" --emit-cs-to \"{outDir}\"");

        var refusedKey = Path.Combine("ext", "lib.cs");
        var keptKey = Path.Combine("..", "ext", "lib.cs");
        invocation.StdErr.Should().Contain($"Not saving generated C# for '{refusedKey}'")
            .And.Contain($"already written for '{keptKey}'");
        var written = File.ReadAllText(Path.Combine(outDir, "ext", "lib.cs"));
        written.Should().Contain("PackageMarker").And.NotContain("FlatMarker");
    }
}
