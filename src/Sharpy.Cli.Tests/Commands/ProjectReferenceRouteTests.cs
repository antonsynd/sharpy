using FluentAssertions;
using Xunit;

namespace Sharpy.Cli.Tests.Commands;

/// <summary>
/// A .spyproj <c>&lt;Reference&gt;</c> on the two commands that build a project (#2223, R-EK):
/// the spelling {<c>Include</c>=absolute path, <c>Include</c>=name + absolute <c>&lt;HintPath&gt;</c>,
/// <c>&lt;HintPath&gt;</c> relative to the .spyproj directory} × the file {resolves, missing} × the
/// route {<c>compile App.spyproj -o out/App.dll</c>, <c>project App.spyproj</c>}.
/// </summary>
/// <remarks>
/// <para>Resolves: <c>from Tablegreeter import Greeter</c> binds and the build exits 0 with its
/// assembly written. The positive control per route is the same project without the item, which
/// does not compile.</para>
/// <para>Missing: SPY0305 and <c>Assembly not found: &lt;the path the parser names&gt;</c> on
/// stderr, exit 1, and nothing written. For <c>compile</c> the <c>-o</c> directory does not exist;
/// for <c>project</c> there is no <c>bin/</c>. The compile door refuses before the project
/// pipeline runs, so the assembly is never written.</para>
/// </remarks>
public sealed class ProjectReferenceRouteTests : IClassFixture<ReferenceLibraryFixture>
{
    const string GreeterMain = "from Tablegreeter import Greeter\n\ndef main():\n    print(Greeter().greet())\n";

    readonly ReferenceLibraryFixture _library;

    public ProjectReferenceRouteTests(ReferenceLibraryFixture library)
    {
        _library = library;
    }

    public static TheoryData<string, bool, string> Cells()
    {
        var data = new TheoryData<string, bool, string>();
        foreach (var spelling in new[] { "include", "hintpath-absolute", "hintpath-relative" })
        {
            foreach (var resolves in new[] { true, false })
            {
                foreach (var route in new[] { "compile", "project" })
                    data.Add(spelling, resolves, route);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cells))]
    public void Reference_BindsTheImportWhenItResolves_AndIsSpy0305BeforeAnythingIsWrittenWhenMissing(
        string spelling, bool resolves, string route)
    {
        using var ws = new TempWorkspace();
        var projectDir = ws.PathFor("app");

        // The resolving file is the fixture's one Tablegreeter.dll (a copy elsewhere would be a
        // same-name load); the missing one is the same spelling at a path that names no file.
        var target = resolves ? _library.DllPath : ws.PathFor(Path.Combine("libs", "Tablegreeter.dll"));
        var (item, expectedPath) = spelling switch
        {
            "include" => ($"<Reference Include=\"{target}\" />", target),
            "hintpath-absolute" => (
                $"<Reference Include=\"Tablegreeter\"><HintPath>{target}</HintPath></Reference>", target),
            "hintpath-relative" => (
                $"<Reference Include=\"Tablegreeter\"><HintPath>{Path.GetRelativePath(projectDir, target)}</HintPath></Reference>",
                Path.GetFullPath(target)),
            _ => throw new ArgumentOutOfRangeException(nameof(spelling), spelling, null),
        };
        var project = WriteProject(ws, item);
        var outDir = ws.PathFor("out");
        var cell = $"[{spelling}, resolves={resolves}, {route}]";

        var invocation = CliTestHarness.Invoke(CommandLine(route, project, outDir));
        var output = invocation.StdOut + invocation.StdErr;

        if (resolves)
        {
            invocation.ExitCode.Should().Be(0, $"{cell} the reference resolves, so the import binds\n{output}");
            WrittenAssemblies(route, projectDir, outDir).Should().ContainSingle($"{cell} the assembly is written");
        }
        else
        {
            invocation.ExitCode.Should().Be(1, $"{cell}\n{output}");
            invocation.StdErr.Should().Contain("SPY0305", cell)
                .And.Contain($"Assembly not found: {expectedPath}", $"{cell} the diagnostic names the path the parser resolved");
            WrittenAssemblies(route, projectDir, outDir).Should().BeEmpty($"{cell} refused before anything is written");
            if (route == "compile")
                System.IO.Directory.Exists(outDir).Should().BeFalse($"{cell} the -o directory is not created");
            else
                System.IO.Directory.Exists(Path.Combine(projectDir, "bin")).Should().BeFalse($"{cell} no bin/");
        }
    }

    /// <summary>
    /// A missing reference with <c>--clean</c>: the refusal comes before the clean, so a refused build
    /// has not deleted bin/ and obj/ (P46 verify: the compiler's own SPY0305 check ran after the CLI's
    /// clean). The control is the same project with the reference present: --clean then deletes them.
    /// </summary>
    [Theory]
    [InlineData("compile", false)]
    [InlineData("compile", true)]
    [InlineData("project", false)]
    [InlineData("project", true)]
    public void AMissingReference_IsRefusedBeforeCleanDeletesAnything(string route, bool resolves)
    {
        using var ws = new TempWorkspace();
        var target = resolves ? _library.DllPath : ws.PathFor(Path.Combine("libs", "Tablegreeter.dll"));
        var project = WriteProject(ws, $"<Reference Include=\"Tablegreeter\"><HintPath>{target}</HintPath></Reference>");
        var sentinels = new[] { ws.WriteFile(Path.Combine("app", "bin", "keep.txt"), "x"), ws.WriteFile(Path.Combine("app", "obj", "keep.txt"), "x") };

        var invocation = CliTestHarness.Invoke(CommandLine(route, project, ws.PathFor("out")) + " --clean");
        var cell = $"[{route} --clean, resolves={resolves}]";

        if (resolves)
        {
            invocation.ExitCode.Should().Be(0, $"{cell}\n{invocation.StdOut}{invocation.StdErr}");
            sentinels.Should().AllSatisfy(s => File.Exists(s).Should().BeFalse($"{cell} control: --clean deleted bin/ and obj/"));
        }
        else
        {
            invocation.ExitCode.Should().Be(1, $"{cell}\n{invocation.StdOut}{invocation.StdErr}");
            invocation.StdErr.Should().Contain("SPY0305").And.Contain($"Assembly not found: {target}");
            sentinels.Should().AllSatisfy(s => File.Exists(s).Should().BeTrue($"{cell} the refusal comes before --clean"));
        }
    }

    /// <summary>
    /// The positive control for the resolving cells: without the item the import does not bind.
    /// (In a fresh sharpyc this is SPY0300; in this test process the library may already be loaded
    /// by another test, and then it fails later, at the Roslyn step — either way it does not
    /// compile.)
    /// </summary>
    [Theory]
    [InlineData("compile")]
    [InlineData("project")]
    public void Control_WithoutTheReference_TheImportDoesNotCompile(string route)
    {
        using var ws = new TempWorkspace();
        var project = WriteProject(ws, referenceItem: "");

        var invocation = CliTestHarness.Invoke(CommandLine(route, project, ws.PathFor("out")));

        invocation.ExitCode.Should().NotBe(0, $"[{route}] control: the import needs the reference\n{invocation.StdOut}{invocation.StdErr}");
    }

    /// <summary>
    /// A rooted reference path is exact (owner ruling on #2223, 2026-10-14): never searched below a
    /// module path, never completed with `.dll`. `<Reference Include="…/Tablegreeter"/>` with a
    /// `<ModulePath>` used to load Tablegreeter.dll in the registry (the module-path loop appended
    /// `.dll` to the rooted path) while the Roslyn step got the raw item — SPY0908 CS0400. Now
    /// SPY0305 with nothing written. Positive control: the `.dll` spelling with the same module path
    /// binds the import.
    /// </summary>
    [Theory]
    [InlineData("compile")]
    [InlineData("project")]
    public void AnExtensionlessRootedInclude_IsSpy0305_EvenWithAModulePath(string route)
    {
        using var ws = new TempWorkspace();
        var projectDir = ws.PathFor("app");
        var outDir = ws.PathFor("out");
        var modulePath = ws.PathFor("mods");
        System.IO.Directory.CreateDirectory(modulePath);
        var extensionless = Path.Combine(_library.Directory, "Tablegreeter");
        var items = $"<Reference Include=\"{extensionless}\" />\n    <ModulePath Include=\"{modulePath}\" />";

        var project = WriteProject(ws, items);
        var refused = CliTestHarness.Invoke(CommandLine(route, project, outDir));

        var all = refused.StdOut + refused.StdErr;
        refused.ExitCode.Should().Be(1, all);
        refused.StdErr.Should().Contain("SPY0305").And.Contain($"Assembly not found: {extensionless}");
        all.Should().NotContain("SPY0908").And.NotContain("CS0400");
        WrittenAssemblies(route, projectDir, outDir).Should().BeEmpty("nothing is written");

        var control = WriteProject(ws, $"<Reference Include=\"{_library.DllPath}\" />\n    <ModulePath Include=\"{modulePath}\" />");
        var bound = CliTestHarness.Invoke(CommandLine(route, control, outDir));
        bound.ExitCode.Should().Be(0, "positive control: the .dll spelling binds the import\n" + bound.StdOut + bound.StdErr);
    }

    /// <summary>A non-rooted `-r` name keeps its module-path search and `.dll` completion.</summary>
    [Fact]
    public void ANonRootedReferenceName_IsStillSearchedAndCompleted()
    {
        using var ws = new TempWorkspace();
        var spy = ws.WriteSpy(GreeterMain);
        var outDir = ws.PathFor("out");

        var result = CliTestHarness.Invoke($"compile \"{spy}\" -r Tablegreeter -m \"{_library.Directory}\" -o \"{Path.Combine(outDir, "App.dll")}\" --no-deps");

        result.ExitCode.Should().Be(0, result.StdOut + result.StdErr);
        File.Exists(Path.Combine(outDir, "App.dll")).Should().BeTrue();
    }

    static string CommandLine(string route, string project, string outDir) => route switch
    {
        "compile" => $"compile \"{project}\" -o \"{Path.Combine(outDir, "App.dll")}\"",
        "project" => $"project \"{project}\"",
        _ => throw new ArgumentOutOfRangeException(nameof(route), route, null),
    };

    static IReadOnlyList<string> WrittenAssemblies(string route, string projectDir, string outDir)
    {
        var root = route == "compile" ? outDir : Path.Combine(projectDir, "bin");
        // `compile -o out/App.dll` writes that path; `project` writes bin/<Config>/<TFM>/App.exe for
        // an exe project (ProjectConfig.OutputAssemblyPath).
        return System.IO.Directory.Exists(root)
            ? System.IO.Directory.GetFiles(root, "App.*", SearchOption.AllDirectories)
                .Where(f => Path.GetExtension(f) is ".dll" or ".exe")
                .ToList()
            : Array.Empty<string>();
    }

    /// <summary>Writes <c>app/main.spy</c> plus an exe <c>app/App.spyproj</c> carrying <paramref name="referenceItem"/>.</summary>
    static string WriteProject(TempWorkspace ws, string referenceItem)
    {
        ws.WriteFile(Path.Combine("app", "main.spy"), GreeterMain);
        return ws.WriteFile(Path.Combine("app", "App.spyproj"),
            "<Project>\n" +
            "  <PropertyGroup>\n" +
            "    <RootNamespace>App</RootNamespace>\n" +
            "    <OutputType>exe</OutputType>\n" +
            "  </PropertyGroup>\n" +
            "  <ItemGroup>\n" +
            "    <SpyFile Include=\"main.spy\" />\n" +
            $"    {referenceItem}\n" +
            "  </ItemGroup>\n" +
            "</Project>\n");
    }
}
