using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Sharpy.Cli.Tests.E2E;

/// <summary>
/// #1437 regression guard: a diagnostic produced while parsing a SIBLING file must be reported
/// against that sibling — its path, its line, its column, its source line — on every front door.
/// </summary>
/// <remarks>
/// <para>
/// Before the fix, <c>sharpyc run main.spy</c> handed the ENTRY file's buffer to the renderer for
/// every diagnostic. The renderer re-derived line/column from the diagnostic's absolute span offset
/// against that buffer, so a parse error at <c>lib.spy:4:13</c> surfaced as <c>main.spy:4:15</c>
/// with main.spy's line 4 underlined — the compiler pointing a caret at a line that does not say
/// what it claims. <c>sharpyc project</c> had the other half of the same hole: the position was
/// right but the file was the <c>&lt;source&gt;</c> placeholder, because parse-phase diagnostics
/// carried no file identity at all.
/// </para>
/// <para>
/// The <see cref="Run_SiblingParserError_LocationDoesNotMoveWhenTheEntryFileIsPadded"/> cell is the
/// instrument, and it is deliberately not a message assertion: the message was ALWAYS right, so a
/// message-only test passes with the bug present. What proves the diagnostic is anchored to the
/// sibling is that padding the entry file with ten comment lines does not move it. Measured at the
/// broken baseline the location moved from <c>main.spy:4:15</c> to <c>main.spy:7:5</c>; it is now
/// <c>lib.spy:4:13</c> under both. Every cell here asserts file AND line AND column AND the
/// underlined source text.
/// </para>
/// </remarks>
public class SiblingDiagnosticIdentityTests : IDisposable
{
    private static readonly string CliDll = Path.Combine(AppContext.BaseDirectory, "sharpyc.dll");

    /// <summary>The sibling's parse error: an unclosed parameter list at line 4, column 13.</summary>
    private const string BrokenSibling = """
        def value() -> int:
            return 1

        def broken( -> int:
            return 2
        """;

    private const string BrokenSiblingErrorLine = "def broken( -> int:";

    private const string Entry = """
        import lib

        def main() -> None:
            print(lib.value())
        """;

    /// <summary>The same entry program, shifted down by ten comment lines.</summary>
    private const string PaddedEntry = """
        # pad 1
        # pad 2
        # pad 3
        # pad 4
        # pad 5
        # pad 6
        # pad 7
        # pad 8
        # pad 9
        # pad 10
        import lib

        def main() -> None:
            print(lib.value())
        """;

    private readonly TempWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    [Fact]
    public void Run_SiblingParserError_NamesTheSiblingFilePositionAndSourceLine()
    {
        WriteFixture(Entry);

        var result = ExecCli("run", _ws.PathFor("main.spy"));

        result.ExitCode.Should().NotBe(0, "the sibling's parse error must fail the compile");
        var location = SoleDiagnosticLocation(result);

        location.File.Should().Be(_ws.PathFor("lib.spy"),
            "the error is in lib.spy — naming the entry file sends the user to an innocent file (#1437)");
        location.Line.Should().Be(4);
        location.Column.Should().Be(13);
        Combined(result).Should().Contain(BrokenSiblingErrorLine,
            "the snippet must be drawn from the file the location names");
    }

    [Fact]
    public void Run_SiblingParserError_LocationDoesNotMoveWhenTheEntryFileIsPadded()
    {
        // The padding control (#1437's own arithmetic proof). Identical inputs except for ten
        // comment lines prepended to the ENTRY file, which contains no error at all.
        WriteFixture(Entry);
        var unpadded = SoleDiagnosticLocation(ExecCli("run", _ws.PathFor("main.spy")));

        _ws.WriteSpy(PaddedEntry);
        var padded = SoleDiagnosticLocation(ExecCli("run", _ws.PathFor("main.spy")));

        padded.Should().Be(unpadded,
            "the reported location belongs to lib.spy, so editing main.spy cannot move it; "
            + "at the broken baseline it moved from main.spy:4:15 to main.spy:7:5 (#1437)");
        padded.File.Should().Be(_ws.PathFor("lib.spy"));
        padded.Line.Should().Be(4);
        padded.Column.Should().Be(13);
    }

    [Fact]
    public void Build_SiblingParserError_NamesTheSiblingFilePositionAndSourceLine()
    {
        // `run` compiles through `build`, but `build` is a front door of its own.
        WriteFixture(Entry);

        var result = ExecCli("build", _ws.PathFor("main.spy"), "--output", _ws.PathFor("out.exe"));

        result.ExitCode.Should().NotBe(0);
        var location = SoleDiagnosticLocation(result);

        location.File.Should().Be(_ws.PathFor("lib.spy"));
        location.Line.Should().Be(4);
        location.Column.Should().Be(13);
        Combined(result).Should().Contain(BrokenSiblingErrorLine);
    }

    [Fact]
    public void Project_SiblingParserError_NamesTheSiblingFileNotThePlaceholder()
    {
        // The other half of #1437: `project` positioned the diagnostic correctly but had no file
        // identity to print, so it rendered the "<source>" placeholder and no snippet.
        WriteFixture(Entry);
        _ws.WriteFile("app.spyproj", """
            <Project>
              <PropertyGroup>
                <RootNamespace>SiblingIdentity</RootNamespace>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>SiblingIdentityFixture</AssemblyName>
              </PropertyGroup>
              <ItemGroup>
                <SpyFile Include="**/*.spy" />
              </ItemGroup>
            </Project>
            """);

        var result = ExecCli("project", _ws.PathFor("app.spyproj"));

        result.ExitCode.Should().NotBe(0);
        var location = SoleDiagnosticLocation(result);

        location.File.Should().Be(_ws.PathFor("lib.spy"));
        Combined(result).Should().NotContain("<source>",
            "an on-disk compilation unit has a real path; the placeholder is for string input only");
        location.Line.Should().Be(4);
        location.Column.Should().Be(13);
        Combined(result).Should().Contain(BrokenSiblingErrorLine);
    }

    [Fact]
    public void Run_SiblingSemanticError_NamesTheSiblingFilePositionAndSourceLine()
    {
        // Control on the neighbouring class: semantic diagnostics already carried their file path,
        // so this cell was green on the location before the fix — but not on the snippet, which the
        // #1323 guard had to suppress while the renderer held the wrong buffer. Per-file rendering
        // restores it, and this cell pins that the parse-phase fix did not cost the semantic path.
        _ws.WriteSpy(Entry);
        _ws.WriteFile("lib.spy", """
            def value() -> int:
                return 1

            def bad() -> int:
                x: int = "not an int"
                return x
            """);

        var result = ExecCli("run", _ws.PathFor("main.spy"));

        result.ExitCode.Should().NotBe(0);
        var location = SoleDiagnosticLocation(result);

        location.File.Should().Be(_ws.PathFor("lib.spy"));
        location.Line.Should().Be(5);
        Combined(result).Should().Contain("x: int = \"not an int\"");
    }

    // ── harness ──────────────────────────────────────────────────────────────

    private void WriteFixture(string entrySource)
    {
        _ws.WriteSpy(entrySource);
        _ws.WriteFile("lib.spy", BrokenSibling);
    }

    private static string Combined(ProcessResult result) => result.StdOut + "\n" + result.StdErr;

    /// <summary>
    /// The single rendered location line (<c>--&gt; file:line:column</c>). Asserting there is
    /// exactly one keeps a cell from passing on some other diagnostic's location.
    /// </summary>
    private static DiagnosticLocation SoleDiagnosticLocation(ProcessResult result)
    {
        var combined = Combined(result);
        var matches = Regex.Matches(combined, @"-->\s*(?<file>.+):(?<line>\d+):(?<col>\d+)\s*$",
                RegexOptions.Multiline)
            .Select(m => new DiagnosticLocation(
                m.Groups["file"].Value.Trim(),
                int.Parse(m.Groups["line"].Value),
                int.Parse(m.Groups["col"].Value)))
            .Distinct()
            .ToList();

        matches.Should().ContainSingle($"exactly one diagnostic location is expected:\n{combined}");
        return matches[0];
    }

    private sealed record DiagnosticLocation(string File, int Line, int Column);

    private ProcessResult ExecCli(params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = _ws.Root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add(CliDll);
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi)!;
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        process.WaitForExit(180_000).Should().BeTrue("the CLI process must not hang");
        process.WaitForExit(); // flush async readers
        return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private sealed record ProcessResult(int ExitCode, string StdOut, string StdErr);
}
