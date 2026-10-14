using System.Diagnostics;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Sharpy.Cli.Tests.E2E;

/// <summary>
/// <c>-r</c> on the two analysis commands (#2223): <c>emit diagnostics</c> and <c>emit hover</c> on an
/// entry that imports a referenced Sharpy library. Before, both rejected <c>-r</c> as an unknown
/// option and the entry could not be analysed at all (<c>emit diagnostics src/main.spy</c> was
/// <c>error SPY0300: Cannot find module 'Greeter'</c>). The process is spawned, so the library is not
/// already loaded into the analysing process by another test (in process, a loaded assembly can
/// satisfy the import without <c>-r</c>, the #2250-family cell recorded on fa7e8aa63).
/// </summary>
public class AnalysisReferenceOptionTests : IClassFixture<ReferenceLibraryFixture>, IDisposable
{
    static readonly string CliDll = Path.Combine(AppContext.BaseDirectory, "sharpyc.dll");

    const string Main = "from Tablegreeter import Greeter\n\ndef main():\n    print(Greeter().greet())\n";

    readonly ReferenceLibraryFixture _library;
    readonly TempWorkspace _ws = new();

    public AnalysisReferenceOptionTests(ReferenceLibraryFixture library)
    {
        _library = library;
        _ws.WriteSpy(Main);
    }

    public void Dispose() => _ws.Dispose();

    static string[] CommandArgs(string command) => command == "diagnostics"
        ? new[] { "emit", "diagnostics", "main.spy" }
        // Hover the class name in `print(Greeter().greet())` (line 4, column 11).
        : new[] { "emit", "hover", "main.spy", "--line", "4", "--col", "11" };

    [Theory]
    [InlineData("diagnostics", "absolute")]
    [InlineData("diagnostics", "bare-name-on-module-path")]
    [InlineData("hover", "absolute")]
    [InlineData("hover", "bare-name-on-module-path")]
    public void AReference_LetsTheEntryBeAnalysed(string command, string spelling)
    {
        var referenceArgs = spelling == "absolute"
            ? new[] { "-r", _library.DllPath }
            : new[] { "-r", Path.GetFileName(_library.DllPath), "-m", _library.Directory };

        var without = ExecCli(CommandArgs(command));
        var with = ExecCli(CommandArgs(command).Concat(referenceArgs).ToArray());

        // Control: without the reference the import is unresolved.
        (without.StdOut + without.StdErr).Should().Contain("SPY0300", "control: the import needs the reference");

        var combined = with.StdOut + with.StdErr;
        combined.Should().NotContain("SPY0300").And.NotContain("was not matched");
        if (command == "diagnostics")
        {
            with.ExitCode.Should().Be(0, combined);
            with.StdOut.Should().Contain("No diagnostics.");
        }
        else
        {
            with.StdErr.Should().NotContain("error", combined);
            with.StdOut.Should().Contain("Greeter", "the hover names the referenced class");
        }
    }

    [Theory]
    [InlineData("diagnostics")]
    [InlineData("hover")]
    public void AReferenceThatResolvesNowhere_IsSpy0305(string command)
    {
        var missing = _ws.PathFor(Path.Combine("does", "not", "exist.dll"));

        var result = ExecCli(CommandArgs(command).Concat(new[] { "-r", missing }).ToArray());

        (result.StdOut + result.StdErr).Should().Contain("SPY0305").And.Contain($"Assembly not found: {missing}");
    }

    sealed record ProcessResult(int ExitCode, string StdOut, string StdErr);

    ProcessResult ExecCli(params string[] args)
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
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi)!;
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        if (!process.WaitForExit(TimeSpan.FromMinutes(2)))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"sharpyc {string.Join(' ', args)} did not exit within 2 minutes");
        }

        process.WaitForExit();
        return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }
}
