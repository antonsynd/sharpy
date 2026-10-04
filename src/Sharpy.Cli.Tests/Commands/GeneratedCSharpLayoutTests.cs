using FluentAssertions;
using Xunit;

namespace Sharpy.Cli.Tests.Commands;

/// <summary>
/// The #2060 contract for every command that writes generated C# (#2159): each unit is written to
/// a distinct path mirroring its source path relative to the module root (the project directory,
/// or the entry file's directory for a single file); two units that would map to one path are
/// refused, never silently overwritten. <c>project --emit-cs-to</c> is covered in
/// <see cref="ProjectCommandTests"/>; this matrix is {flat module, package module, same stem in two
/// directories} × {<c>compile app.spyproj --emit-csharp</c>, <c>compile main.spy --emit-csharp</c>,
/// <c>emit csharp main.spy --output</c>}. The content check (each file holds its own module's
/// marker and not the other's) is what makes the same-stem cell non-vacuous; the flat cell is the
/// positive control that the marker check finds a module's C# at all.
/// </summary>
public class GeneratedCSharpLayoutTests
{
    public enum Command
    {
        CompileProject,
        CompileFile,
        EmitCSharp,
    }

    public enum Cell
    {
        Flat,
        Package,
        SameStem,
    }

    const string FlatMarker = "FlatMarker";
    const string PackageMarker = "PackageMarker";

    /// <summary>
    /// Writes <c>src/main.spy</c> importing <c>lib</c> (<c>src/lib.spy</c>) and/or <c>pkg.lib</c>
    /// (<c>src/pkg/lib.spy</c>), plus a library project including <c>src/**/*.spy</c>.
    /// </summary>
    static void WriteSources(TempWorkspace ws, bool flat, bool package)
    {
        var imports = "";
        var body = "";
        if (flat)
        {
            ws.WriteFile(Path.Combine("src", "lib.spy"), "def flat_marker() -> int:\n    return 1\n");
            imports += "import lib\n";
            body += "    print(lib.flat_marker())\n";
        }
        if (package)
        {
            ws.WriteFile(Path.Combine("src", "pkg", "lib.spy"), "def package_marker() -> int:\n    return 2\n");
            imports += "import pkg.lib\n";
            body += "    print(pkg.lib.package_marker())\n";
        }
        ws.WriteFile(Path.Combine("src", "main.spy"), imports + "\ndef main():\n" + body);
        ws.WriteFile("App.spyproj",
            "<Project>\n" +
            "  <PropertyGroup>\n" +
            "    <RootNamespace>App</RootNamespace>\n" +
            "    <OutputType>library</OutputType>\n" +
            "    <TargetFramework>net10.0</TargetFramework>\n" +
            "  </PropertyGroup>\n" +
            "  <ItemGroup>\n" +
            "    <SourceFile Include=\"src/**/*.spy\" />\n" +
            "  </ItemGroup>\n" +
            "</Project>\n");
    }

    static void AssertOk(CliInvocation invocation)
        => invocation.ExitCode.Should().Be(0, $"stdout:\n{invocation.StdOut}\nstderr:\n{invocation.StdErr}");

    /// <summary>Runs the command; returns the directory the generated C# is rooted under and the
    /// prefix (relative to that directory) the module tree is mirrored below.</summary>
    static (string Root, string Prefix) Run(TempWorkspace ws, Command command)
    {
        var src = ws.PathFor("src");
        switch (command)
        {
            case Command.CompileProject:
                AssertOk(CliTestHarness.Invoke($"compile \"{ws.PathFor("App.spyproj")}\" --emit-csharp"));
                // Project units mirror their project-relative path beside the assembly.
                return (ws.PathFor(Path.Combine("bin", "Release", "net10.0")), "src");
            case Command.CompileFile:
                var dll = ws.PathFor(Path.Combine("out", "main.dll"));
                AssertOk(CliTestHarness.Invoke($"compile \"{Path.Combine(src, "main.spy")}\" -o \"{dll}\" --emit-csharp"));
                // Single-file units mirror their path relative to the entry file's directory.
                return (ws.PathFor("out"), "");
            case Command.EmitCSharp:
                // gen/ does not exist yet: emit creates the --output file's directory.
                var outFile = ws.PathFor(Path.Combine("gen", "main.cs"));
                AssertOk(CliTestHarness.Invoke($"emit csharp \"{Path.Combine(src, "main.spy")}\" --output \"{outFile}\""));
                return (ws.PathFor("gen"), "");
            default:
                throw new ArgumentOutOfRangeException(nameof(command));
        }
    }

    [Theory]
    [InlineData(Command.CompileProject, Cell.Flat)]
    [InlineData(Command.CompileProject, Cell.Package)]
    [InlineData(Command.CompileProject, Cell.SameStem)]
    [InlineData(Command.CompileFile, Cell.Flat)]
    [InlineData(Command.CompileFile, Cell.Package)]
    [InlineData(Command.CompileFile, Cell.SameStem)]
    [InlineData(Command.EmitCSharp, Cell.Flat)]
    [InlineData(Command.EmitCSharp, Cell.Package)]
    [InlineData(Command.EmitCSharp, Cell.SameStem)]
    public void EveryUnit_IsWrittenAtItsMirroredPath_WithItsOwnContent(Command command, Cell cell)
    {
        using var ws = new TempWorkspace();
        var flat = cell is Cell.Flat or Cell.SameStem;
        var package = cell is Cell.Package or Cell.SameStem;
        WriteSources(ws, flat, package);

        var (root, prefix) = Run(ws, command);

        var expected = new List<string> { Path.Combine(prefix, "main.cs") };
        var flatPath = Path.Combine(root, prefix, "lib.cs");
        var packagePath = Path.Combine(root, prefix, "pkg", "lib.cs");
        if (flat)
        {
            expected.Add(Path.Combine(prefix, "lib.cs"));
            File.Exists(flatPath).Should().BeTrue($"the flat module mirrors to {flatPath}");
            File.ReadAllText(flatPath).Should().Contain(FlatMarker).And.NotContain(PackageMarker);
        }
        if (package)
        {
            expected.Add(Path.Combine(prefix, "pkg", "lib.cs"));
            File.Exists(packagePath).Should().BeTrue($"the package module mirrors to {packagePath}");
            File.ReadAllText(packagePath).Should().Contain(PackageMarker).And.NotContain(FlatMarker);
        }

        Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(root, p))
            .Should().BeEquivalentTo(expected, "every unit is written exactly once, at its mirrored path");
    }

    [Fact]
    public void EmitCSharp_AModuleMappingToTheEntrysOutputFile_IsRefusedNotWrittenOverIt()
    {
        // `--output gen/lib.cs` and `import lib`: the module mirrors to the entry's own output
        // file. By file stem the module overwrote the entry's C#; now the module is refused.
        using var ws = new TempWorkspace();
        WriteSources(ws, flat: true, package: false);
        var outFile = ws.PathFor(Path.Combine("gen", "lib.cs"));

        var invocation = CliTestHarness.Invoke(
            $"emit csharp \"{ws.PathFor(Path.Combine("src", "main.spy"))}\" --output \"{outFile}\"");

        invocation.StdErr.Should().Contain("Not saving generated C# for 'lib.spy'")
            .And.Contain("already written for 'main.spy'");
        File.ReadAllText(outFile).Should().Contain("Main()", "the entry's C# keeps its output file");
    }

    [Fact]
    public void CompileProject_OutputOption_PlacesTheAssemblyAndItsCSharp()
    {
        // `compile app.spyproj -o X` parsed `-o` and dropped it: the assembly went to
        // bin/Release/net10.0 and X was never written (#2159).
        using var ws = new TempWorkspace();
        WriteSources(ws, flat: true, package: false);
        var dll = ws.PathFor(Path.Combine("custom", "App.dll"));

        var invocation = CliTestHarness.Invoke($"compile \"{ws.PathFor("App.spyproj")}\" -o \"{dll}\" --emit-csharp");

        AssertOk(invocation);
        File.Exists(dll).Should().BeTrue("-o names the project's output assembly");
        invocation.StdOut.Should().Contain($"Output: {dll}");
        File.ReadAllText(ws.PathFor(Path.Combine("custom", "src", "lib.cs"))).Should().Contain(FlatMarker);
        Directory.Exists(ws.PathFor("bin")).Should().BeFalse("nothing goes to the default bin/ when -o is given");
    }

    [Fact]
    public void CompileProject_PinnedSourceRoot_MirrorsBelowIt()
    {
        // With <SourceRoot>src</SourceRoot> the unit keys are relative to src/, the root the module
        // names are spelled from, so the C# lands at lib.cs, not src/lib.cs.
        using var ws = new TempWorkspace();
        WriteSources(ws, flat: true, package: true);
        var proj = ws.PathFor("App.spyproj");
        File.WriteAllText(proj, File.ReadAllText(proj).Replace(
            "  </PropertyGroup>", "    <SourceRoot>src</SourceRoot>\n  </PropertyGroup>"));

        AssertOk(CliTestHarness.Invoke($"compile \"{proj}\" --emit-csharp"));

        var root = ws.PathFor(Path.Combine("bin", "Release", "net10.0"));
        Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(root, p))
            .Should().BeEquivalentTo("main.cs", "lib.cs", Path.Combine("pkg", "lib.cs"));
        File.ReadAllText(Path.Combine(root, "pkg", "lib.cs")).Should().Contain(PackageMarker);
    }
}
