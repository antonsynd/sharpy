using System.CommandLine;
using FluentAssertions;
using Sharpy.Cli.Commands;
using Xunit;
using OptionEffect = Sharpy.Cli.Commands.CompileCommand.OptionEffect;

namespace Sharpy.Cli.Tests.Commands;

/// <summary>
/// The #2173 contract: a CLI option that is parsed is honoured, refused for that input kind with a
/// message, or documented as ignored — never silently dropped — and a missing input path is an
/// error. <c>compile</c> carries the contract as one option-to-effect table
/// (<see cref="CompileCommand.OptionEffects"/>, one column per input kind {.spy, .spyproj}); these
/// tests check the table is total over the options the command really accepts, that its refused
/// and ignored cells are what the command does and what <c>--help</c> says, and the cells #2173
/// found dropped: <c>-r</c>/<c>-m</c> for a project, <c>-p</c> everywhere, a <c>-r</c> or <c>-m</c>
/// naming nothing, and <c>--log-file</c>.
/// </summary>
public class CompileOptionEffectTableTests : IClassFixture<CompileOptionEffectTableTests.ReferenceLibrary>
{
    readonly ReferenceLibrary _library;

    public CompileOptionEffectTableTests(ReferenceLibrary library)
    {
        _library = library;
    }

    /// <summary>
    /// A Sharpy library compiled once per class, so a <c>-r</c> has something real to resolve:
    /// <c>Tablegreeter.dll</c> (namespace <c>Tablegreeter</c>, class <c>Greeter</c>).
    /// </summary>
    public sealed class ReferenceLibrary : IDisposable
    {
        readonly TempWorkspace _ws = new();

        public string Directory { get; }

        public string DllPath { get; }

        public ReferenceLibrary()
        {
            var source = _ws.WriteSpy(
                "class Greeter:\n    def greet(self) -> str:\n        return \"hello from lib\"\n",
                "tablegreeter.spy");
            Directory = _ws.PathFor("lib");
            DllPath = Path.Combine(Directory, "Tablegreeter.dll");
            var invocation = CliTestHarness.Invoke($"compile \"{source}\" -t library -o \"{DllPath}\" --no-deps");
            if (invocation.ExitCode != 0 || !File.Exists(DllPath))
            {
                throw new InvalidOperationException(
                    $"could not build the reference library: exit {invocation.ExitCode}\n{invocation.StdOut}\n{invocation.StdErr}");
            }
        }

        public void Dispose() => _ws.Dispose();
    }

    const string GreeterMain = "from Tablegreeter import Greeter\n\ndef main():\n    print(Greeter().greet())\n";
    const string PlainMain = "def main():\n    print(\"plain\")\n";

    /// <summary>Writes <c>src/main.spy</c> plus an exe <c>App.spyproj</c> including <c>src/**/*.spy</c>.</summary>
    static string WriteProject(TempWorkspace ws, string mainSource)
    {
        ws.WriteFile(Path.Combine("src", "main.spy"), mainSource);
        return ws.WriteFile("App.spyproj",
            "<Project>\n" +
            "  <PropertyGroup>\n" +
            "    <RootNamespace>App</RootNamespace>\n" +
            "    <OutputType>exe</OutputType>\n" +
            "  </PropertyGroup>\n" +
            "  <ItemGroup>\n" +
            "    <SpyFile Include=\"src/**/*.spy\" />\n" +
            "  </ItemGroup>\n" +
            "</Project>\n");
    }

    /// <summary>The compile command plus the recursive options of its parent: every option it accepts.</summary>
    static (Command Compile, IReadOnlyList<Option> Own, IReadOnlyList<Option> Inherited) CompileOptions()
    {
        var (root, _, _) = CliTestHarness.BuildRoot();
        var compile = root.Subcommands.Single(c => c.Name == "compile");
        return (compile, compile.Options.ToList(), root.Options.Where(o => o.Recursive).ToList());
    }

    // ---- The table is total ----

    [Fact]
    public void EveryAcceptedOption_HasARow_AndEveryRowNamesAnAcceptedOption()
    {
        var (_, own, inherited) = CompileOptions();
        var accepted = own.Concat(inherited).Select(o => o.Name).ToList();

        // Positive control: the walk reaches the command's own options AND the recursive globals.
        accepted.Should().Contain(new[] { "--project-reference", "--clean", "--enable-feature", "--log-file" });
        accepted.Should().OnlyHaveUniqueItems();

        accepted.Except(CompileCommand.OptionEffects.Keys).Should().BeEmpty(
            "every option compile accepts needs a row in CompileCommand.OptionEffects saying whether it is "
            + "honoured, refused or ignored for a .spy and a .spyproj input (#2173)");
        CompileCommand.OptionEffects.Keys.Except(accepted).Should().BeEmpty(
            "a row must name an option compile actually accepts");
    }

    [Fact]
    public void NonHonouredCells_AreTheCommandsOwnOptions_AndHelpSaysSo()
    {
        var (_, own, inherited) = CompileOptions();

        foreach (var option in inherited)
        {
            var row = CompileCommand.OptionEffects[option.Name];
            (row.SpyFile, row.SpyProject).Should().Be((OptionEffect.Honoured, OptionEffect.Honoured),
                $"{option.Name} is a global option whose --help text is shared with every command, so compile "
                + "cannot document ignoring or refusing it there");
        }

        var documented = 0;
        foreach (var option in own)
        {
            var row = CompileCommand.OptionEffects[option.Name];
            var suffix = CompileCommand.DescriptionSuffix(row);
            if (row.SpyFile == OptionEffect.Honoured && row.SpyProject == OptionEffect.Honoured)
            {
                suffix.Should().BeEmpty();
                continue;
            }

            suffix.Should().NotBeEmpty();
            option.Description.Should().EndWith(suffix, $"--help must document what {option.Name} is not honoured for");
            documented++;
        }

        // --type, --project-reference, --self-contained, --incremental, --clean.
        documented.Should().Be(5);
    }

    // ---- Refused cells refuse, before anything is written ----

    /// <summary>A command-line spelling per refused option; a new refused cell must add one.</summary>
    static readonly IReadOnlyDictionary<string, string> RefusedOptionSpelling = new Dictionary<string, string>
    {
        ["--project-reference"] = "--project-reference lib.csproj",
        ["--self-contained"] = "--self-contained",
    };

    public static TheoryData<string, string> RefusedCells()
    {
        var data = new TheoryData<string, string>();
        foreach (var (name, row) in CompileCommand.OptionEffects)
        {
            if (row.SpyFile == OptionEffect.Refused)
                data.Add(name, ".spy");
            if (row.SpyProject == OptionEffect.Refused)
                data.Add(name, ".spyproj");
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(RefusedCells))]
    public void ARefusedCell_ExitsOneNamingTheOption_AndWritesNothing(string option, string kind)
    {
        RefusedOptionSpelling.Should().ContainKey(option, "every refused cell is exercised; add its spelling");
        using var ws = new TempWorkspace();
        var input = kind == ".spyproj" ? WriteProject(ws, PlainMain) : ws.WriteSpy(PlainMain);
        var output = ws.PathFor(Path.Combine("out", "App.dll"));

        var invocation = CliTestHarness.Invoke($"compile \"{input}\" -o \"{output}\" {RefusedOptionSpelling[option]}");

        invocation.ExitCode.Should().Be(1, invocation.StdOut + invocation.StdErr);
        invocation.StdErr.Should().Contain($"{option} is not supported");
        System.IO.Directory.Exists(ws.PathFor("out")).Should().BeFalse("a refusal happens before anything is compiled");
    }

    /// <summary>
    /// The control for the refusal above: the same project compiles when the refused option is
    /// absent, so the exit 1 is the refusal and not a broken input.
    /// </summary>
    [Theory]
    [InlineData(".spy")]
    [InlineData(".spyproj")]
    public void Control_TheSameInputWithoutARefusedOption_Compiles(string kind)
    {
        using var ws = new TempWorkspace();
        var input = kind == ".spyproj" ? WriteProject(ws, PlainMain) : ws.WriteSpy(PlainMain);
        var output = ws.PathFor(Path.Combine("out", "App.dll"));

        var invocation = CliTestHarness.Invoke($"compile \"{input}\" -o \"{output}\" --no-deps");

        invocation.ExitCode.Should().Be(0, invocation.StdOut + invocation.StdErr);
        File.Exists(output).Should().BeTrue();
    }

    /// <summary>
    /// <c>-p</c> has no implementation in any mode, so the single-file seam behind <c>build</c> and
    /// <c>run</c> refuses it too (it was parsed and dropped there as well).
    /// </summary>
    [Theory]
    [InlineData("build")]
    [InlineData("run")]
    public void ProjectReference_IsRefusedByBuildAndRun(string command)
    {
        using var ws = new TempWorkspace();
        var spy = ws.WriteSpy(PlainMain);

        var invocation = CliTestHarness.Invoke($"{command} \"{spy}\" -p lib.csproj");

        invocation.ExitCode.Should().Be(1, invocation.StdOut + invocation.StdErr);
        invocation.StdErr.Should().Contain("--project-reference is not supported");
        invocation.StdOut.Should().NotContain("plain", "nothing ran");
    }

    // ---- Honoured: -r and -m reach a project ----

    [Fact]
    public void Reference_IsHonouredForAProject()
    {
        using var ws = new TempWorkspace();
        var project = WriteProject(ws, GreeterMain);

        var without = CliTestHarness.Invoke($"compile \"{project}\" -o \"{ws.PathFor(Path.Combine("a", "App.dll"))}\"");
        var with = CliTestHarness.Invoke(
            $"compile \"{project}\" -r \"{_library.DllPath}\" -o \"{ws.PathFor(Path.Combine("b", "App.dll"))}\"");

        // Control: the import needs the reference. (In a fresh sharpyc this is SPY0300; in this test
        // process the library may already be loaded by another test, and then it fails later, at the
        // Roslyn step — either way it does not compile.)
        without.ExitCode.Should().NotBe(0, "control: the import needs the reference");
        with.ExitCode.Should().Be(0, with.StdOut + with.StdErr);
        File.Exists(ws.PathFor(Path.Combine("b", "App.dll"))).Should().BeTrue();
    }

    /// <summary>
    /// <c>-m</c> is observable through the reference resolution it feeds: a bare <c>-r</c> name is
    /// found on a module path. Without the <c>-m</c> the same name is SPY0305.
    /// </summary>
    [Theory]
    [InlineData(".spy")]
    [InlineData(".spyproj")]
    public void ModulePath_ResolvesABareReferenceName(string kind)
    {
        using var ws = new TempWorkspace();
        var input = kind == ".spyproj" ? WriteProject(ws, GreeterMain) : ws.WriteSpy(GreeterMain);
        var name = Path.GetFileName(_library.DllPath);

        var without = CliTestHarness.Invoke(
            $"compile \"{input}\" -r {name} -o \"{ws.PathFor(Path.Combine("a", "App.dll"))}\"");
        var with = CliTestHarness.Invoke(
            $"compile \"{input}\" -r {name} -m \"{_library.Directory}\" -o \"{ws.PathFor(Path.Combine("b", "App.dll"))}\"");

        without.ExitCode.Should().Be(1, without.StdOut + without.StdErr);
        without.StdErr.Should().Contain($"Assembly not found: {name}");
        // The reference reaches Roslyn as the file the registry found, not the bare name (which
        // Roslyn cannot resolve: SPY0908 CS0400).
        with.ExitCode.Should().Be(0, with.StdOut + with.StdErr);
        File.Exists(ws.PathFor(Path.Combine("b", "App.dll"))).Should().BeTrue();
    }

    // ---- A missing input path is an error ----

    public static TheoryData<string, bool> CommandLines() => new()
    {
        // {0} = the .spy file, {1} = the project, {2} = an output directory; bool = writes an assembly.
        { "compile \"{0}\" -o \"{2}/App.dll\"", true },
        { "compile \"{1}\" -o \"{2}/App.dll\"", true },
        { "build \"{0}\" -o \"{2}/App.dll\"", true },
        { "run \"{0}\"", false },
        { "emit csharp \"{0}\" --output \"{2}/main.cs\"", false },
    };

    [Theory]
    [MemberData(nameof(CommandLines))]
    public void AReferenceThatResolvesNowhere_IsSpy0305(string commandLine, bool writesAssembly)
    {
        using var ws = new TempWorkspace();
        var spy = ws.WriteSpy(PlainMain);
        var project = WriteProject(ws, PlainMain);
        var outDir = ws.PathFor("out");
        var missing = ws.PathFor(Path.Combine("does", "not", "exist.dll"));
        var line = string.Format(commandLine, spy, project, outDir);

        var control = CliTestHarness.Invoke(line);
        control.ExitCode.Should().Be(0, "control: the command succeeds without the reference\n" + control.StdErr);
        if (System.IO.Directory.Exists(outDir))
            System.IO.Directory.Delete(outDir, recursive: true);

        var invocation = CliTestHarness.Invoke($"{line} -r \"{missing}\"");

        invocation.ExitCode.Should().Be(1, invocation.StdOut + invocation.StdErr);
        invocation.StdErr.Should().Contain("SPY0305").And.Contain($"Assembly not found: {missing}");
        invocation.StdOut.Should().NotContain("plain", "nothing ran");
        if (writesAssembly)
            File.Exists(Path.Combine(outDir, "App.dll")).Should().BeFalse("nothing is compiled");
    }

    [Theory]
    [MemberData(nameof(CommandLines))]
    public void AModulePathThatDoesNotExist_IsAnError(string commandLine, bool writesAssembly)
    {
        using var ws = new TempWorkspace();
        var spy = ws.WriteSpy(PlainMain);
        var project = WriteProject(ws, PlainMain);
        var outDir = ws.PathFor("out");
        var missing = ws.PathFor(Path.Combine("no", "such", "dir"));
        var line = string.Format(commandLine, spy, project, outDir);

        var invocation = CliTestHarness.Invoke($"{line} -m \"{missing}\"");

        invocation.ExitCode.Should().Be(1, invocation.StdOut + invocation.StdErr);
        invocation.StdErr.Should().Contain($"Module path '{missing}' does not exist");
        invocation.StdOut.Should().NotContain("plain", "nothing ran");
        if (writesAssembly)
            File.Exists(Path.Combine(outDir, "App.dll")).Should().BeFalse("nothing is compiled");

        // Control: an existing module path is accepted.
        var existing = CliTestHarness.Invoke($"{line} -m \"{ws.Root}\"");
        existing.ExitCode.Should().Be(0, existing.StdOut + existing.StdErr);
    }

    // ---- --log-file keeps what was logged ----

    [Theory]
    [InlineData(".spy")]
    [InlineData(".spyproj")]
    public void LogFile_ReceivesTheLog(string kind)
    {
        using var ws = new TempWorkspace();
        var input = kind == ".spyproj" ? WriteProject(ws, PlainMain) : ws.WriteSpy(PlainMain);
        var log = ws.PathFor("compile.log");

        var invocation = CliTestHarness.Invoke(
            $"compile \"{input}\" -o \"{ws.PathFor(Path.Combine("out", "App.dll"))}\" --no-deps --log-level Info --log-file \"{log}\"");

        invocation.ExitCode.Should().Be(0, invocation.StdOut + invocation.StdErr);
        // The writer stays open (nothing disposes the logger), so read with a sharing mode that allows it.
        using var stream = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        reader.ReadToEnd().Should().Contain("[INFO]", "--log-file at Info must hold the Info log, not an empty file");
    }
}
