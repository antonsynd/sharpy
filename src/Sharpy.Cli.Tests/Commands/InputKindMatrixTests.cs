using FluentAssertions;
using Xunit;

namespace Sharpy.Cli.Tests.Commands;

/// <summary>
/// The input-kind axis of the #2223 contract: {.spy, .spyproj} × every command that takes a file.
/// A command given the kind it accepts performs it; given the other kind it refuses with a steer to
/// the command that does, with its usage exit code, BEFORE reading the file as the wrong thing —
/// a <c>.spyproj</c> given to <c>build</c>/<c>run</c>/<c>emit</c>/<c>format</c> used to be lexed as
/// Sharpy (SPY0013 on the XML's two-space indentation), and a <c>.spy</c> given to <c>project</c>
/// reached the XML loader ("Unexpected error: Data at the root level is invalid"). One classifier
/// (<c>CliHelpers.ClassifyInput</c>) and one refusal (<c>CliHelpers.RefuseInputKind</c>) decide.
/// </summary>
public class InputKindMatrixTests
{
    /// <summary>Exits 7, so a <c>run</c> that really ran is told apart from one that did nothing (exit 0) or refused (exit 1).</summary>
    const string Program = "import sys\n\n\ndef main():\n    sys.exit(7)\n";

    /// <summary>
    /// {0} = the input, {1} = an output directory that does not exist beforehand. The bool is whether
    /// the command accepts a .spyproj (compile and project) and a .spy (everything but project).
    /// </summary>
    public static TheoryData<string, string, bool, bool> Commands() => new()
    {
        { "build", "build \"{0}\" -o \"{1}/a.dll\"", true, false },
        { "run", "run \"{0}\"", true, false },
        { "emit tokens", "emit tokens \"{0}\"", true, false },
        { "emit ast", "emit ast \"{0}\"", true, false },
        { "emit csharp", "emit csharp \"{0}\" -o \"{1}/main.cs\"", true, false },
        { "emit parse", "emit parse \"{0}\"", true, false },
        { "emit diagnostics", "emit diagnostics \"{0}\"", true, false },
        { "emit hover", "emit hover \"{0}\" --line 1 --col 1", true, false },
        { "format", "format --check \"{0}\"", true, false },
        { "project", "project \"{0}\"", false, true },
        { "compile", "compile \"{0}\" -o \"{1}/a.dll\" --no-deps", true, true },
    };

    public static TheoryData<string, string, string> Cells()
    {
        var data = new TheoryData<string, string, string>();
        foreach (var row in Commands())
        {
            var (name, line) = ((string)row[0], (string)row[1]);
            data.Add(name, line, ".spy");
            data.Add(name, line, ".spyproj");
        }

        return data;
    }

    static (string Input, string Out) Write(TempWorkspace ws, string kind)
    {
        if (kind == ".spy")
            return (ws.WriteSpy(Program), ws.PathFor("out"));

        ws.WriteFile(Path.Combine("src", "main.spy"), Program);
        var project = ws.WriteFile("App.spyproj",
            "<Project>\n  <PropertyGroup>\n    <RootNamespace>App</RootNamespace>\n    <OutputType>exe</OutputType>\n"
            + "  </PropertyGroup>\n  <ItemGroup>\n    <SpyFile Include=\"src/**/*.spy\" />\n  </ItemGroup>\n</Project>\n");
        return (project, ws.PathFor("out"));
    }

    static (bool Spy, bool Project) Accepts(string command)
    {
        var row = Commands().Single(r => (string)r[0] == command);
        return ((bool)row[2], (bool)row[3]);
    }

    [Theory]
    [MemberData(nameof(Cells))]
    public void TheAcceptedKindRuns_TheOtherIsRefusedBeforeItIsRead(string command, string line, string kind)
    {
        using var ws = new TempWorkspace();
        var (input, outDir) = Write(ws, kind);
        var (acceptsSpy, acceptsProject) = Accepts(command);
        var accepted = kind == ".spy" ? acceptsSpy : acceptsProject;

        var invocation = CliTestHarness.Invoke(string.Format(line, input, outDir));
        var all = invocation.StdOut + invocation.StdErr;

        if (accepted)
        {
            // Positive control: the same command really performs on the kind it accepts.
            invocation.ExitCode.Should().Be(command == "run" ? 7 : 0, all);
            all.Should().NotContain("is a project file").And.NotContain("is not a .spyproj project file");
            switch (command)
            {
                case "build":
                case "compile":
                    File.Exists(Path.Combine(outDir, "a.dll")).Should().BeTrue();
                    break;
                case "emit csharp":
                    File.Exists(Path.Combine(outDir, "main.cs")).Should().BeTrue();
                    break;
                case "emit tokens":
                    invocation.StdOut.Should().Contain("Tokens for");
                    break;
                case "emit parse":
                    invocation.StdOut.Should().Contain("PARSE_OK");
                    break;
                case "emit diagnostics":
                    invocation.StdOut.Should().Contain("No diagnostics.");
                    break;
                case "project":
                    Directory.Exists(ws.PathFor("bin")).Should().BeTrue("project writes bin/{Configuration}/…");
                    break;
            }

            return;
        }

        var usageExit = command == "format" ? 2 : 1;
        invocation.ExitCode.Should().Be(usageExit, all);
        // The steer spells the input as it was given (here: its full path), so following it works.
        invocation.StdErr.Should().Contain(kind == ".spyproj"
            ? $"'{input}' is a project file; use 'sharpyc compile {input}' or 'sharpyc project {input}'."
            : $"'{input}' is not a .spyproj project file; use 'sharpyc build', 'run' or 'compile' for a .spy file.");
        // Read as the wrong kind, the XML lexed as Sharpy is SPY0013 and the Sharpy loaded as XML is an
        // "Unexpected error" — both mean the file was read before the refusal.
        all.Should().NotContain("SPY0013").And.NotContain("Unexpected error");
        Directory.Exists(outDir).Should().BeFalse("nothing is written");
        Directory.Exists(ws.PathFor("bin")).Should().BeFalse("nothing is written");
    }

    /// <summary>
    /// <c>project</c> given a directory gets a steer to the .spyproj inside it, not the .spy-file steer
    /// (P46 verify), and nothing is written.
    /// </summary>
    [Fact]
    public void Project_OfADirectory_IsRefusedWithADirectorySteer()
    {
        using var ws = new TempWorkspace();
        Write(ws, ".spyproj");

        var invocation = CliTestHarness.Invoke($"project \"{ws.Root}\"");

        invocation.ExitCode.Should().Be(1, invocation.StdOut + invocation.StdErr);
        invocation.StdErr.Should().Contain("is a directory; give the .spyproj file inside it");
        invocation.StdErr.Should().NotContain("Unexpected error");
        Directory.Exists(ws.PathFor("bin")).Should().BeFalse();
    }

    /// <summary>
    /// The steer spells the path as the user gave it, so the suggested command works from the same
    /// directory: `build ../app/App.spyproj` steered to `compile App.spyproj` — the bare name — which
    /// fails from that cwd (P46 verify, #2223).
    /// </summary>
    [Theory]
    [InlineData("build")]
    [InlineData("run")]
    [InlineData("emit csharp")]
    public void TheSteer_SpellsThePathAsGiven(string command)
    {
        using var ws = new TempWorkspace();
        var (project, _) = Write(ws, ".spyproj");
        var given = Path.Combine(Path.GetDirectoryName(project)!, "src", "..", Path.GetFileName(project));

        var invocation = CliTestHarness.Invoke($"{command} \"{given}\"");

        invocation.ExitCode.Should().Be(1, invocation.StdOut + invocation.StdErr);
        invocation.StdErr.Should().Contain($"'{given}' is a project file; use 'sharpyc compile {given}' or 'sharpyc project {given}'.");
        invocation.StdErr.Should().NotContain($"compile {Path.GetFileName(project)}'", "the bare file name would not resolve from the caller's directory");
    }

    /// <summary>
    /// The non-.spy warning is keyed on the one classifier, so it agrees with the refusal: an
    /// upper-case `.SPY` is a .spy file (no warning); positive control: `.txt` still warns.
    /// </summary>
    [Fact]
    public void AnUpperCaseSpyExtension_IsASpyFile_WithoutTheWarning()
    {
        using var ws = new TempWorkspace();
        var upper = ws.WriteFile("UP.SPY", Program);
        var text = ws.WriteFile("plain.txt", Program);

        var accepted = CliTestHarness.Invoke($"build \"{upper}\" -o \"{ws.PathFor("out/up.exe")}\"");
        accepted.ExitCode.Should().Be(0, accepted.StdOut + accepted.StdErr);
        accepted.StdErr.Should().NotContain("does not have .spy extension");

        var control = CliTestHarness.Invoke($"build \"{text}\" -o \"{ws.PathFor("out/txt.exe")}\"");
        control.ExitCode.Should().Be(0, control.StdOut + control.StdErr);
        control.StdErr.Should().Contain("does not have .spy extension", "positive control: another extension still warns");
    }

    /// <summary>
    /// A directory given to <c>format</c> is untouched by the classifier: only its <c>.spy</c> files
    /// are formatted, so a <c>.spyproj</c> beside them is no refusal.
    /// </summary>
    [Fact]
    public void Format_OfADirectoryHoldingAProject_FormatsItsSourceOnly()
    {
        using var ws = new TempWorkspace();
        Write(ws, ".spyproj");

        var invocation = CliTestHarness.Invoke($"format --check \"{ws.Root}\"");

        invocation.ExitCode.Should().Be(0, invocation.StdOut + invocation.StdErr);
        invocation.StdErr.Should().NotContain("is a project file");
    }
}
