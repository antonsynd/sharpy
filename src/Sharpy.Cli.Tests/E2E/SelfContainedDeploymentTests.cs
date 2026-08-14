using System.Diagnostics;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Sharpy.Cli.Tests.E2E;

/// <summary>
/// #1483 end to end: <c>sharpyc run --self-contained &lt;file&gt;.spy</c> must publish a self-contained
/// executable and run it. Before the fix EVERY publish failed to compile — the wrapper's entry call
/// interpolated the raw file stem (<c>sc_probe.Main()</c>) against the emitter's mangled module class
/// (<c>ScProbe</c>): CS0103. The two cells are the case-sensitivity trap the issue names: a
/// snake_case stem (<c>sc_probe</c> → <c>ScProbe</c>) and <c>main.spy</c> (→ <c>Program</c>, the
/// CS0542-avoiding special case). Each drives the real <c>sharpyc</c> process through
/// <c>dotnet publish --self-contained</c> and asserts the program's own stdout, so a regression in
/// the entry-name mangling OR the reflection/load-context wrapper surfaces here as a failed publish or
/// a missing type at run time. (<c>--self-contained</c> shells out to <c>dotnet publish</c> and is
/// necessarily slower than the framework-dependent StandaloneDeploymentTests.)
/// </summary>
public class SelfContainedDeploymentTests : IDisposable
{
    private const string RunBanner = "=== Running Program ===";
    private static readonly string CliDll = Path.Combine(AppContext.BaseDirectory, "sharpyc.dll");

    private readonly TempWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    [Fact]
    public void SnakeCaseStem_PublishesAndRuns() =>
        // sc_probe.spy → module class ScProbe. The raw-stem bug wrote sc_probe.Main() → CS0103.
        RunSelfContained("sc_probe.spy", """
            def main() -> None:
                print("hi from sc_probe")
            """).Should().Be("hi from sc_probe");

    [Fact]
    public void MainStem_PublishesAndRuns() =>
        // main.spy → module class Program (not Main), the CS0542-avoiding special case; the raw stem
        // would have reflected the non-existent type "main".
        RunSelfContained("main.spy", """
            def main() -> None:
                print("hi from main")
            """).Should().Be("hi from main");

    // ── harness ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Writes the source under <paramref name="fileName"/>, drives
    /// <c>sharpyc run --self-contained</c> (which publishes then runs the produced executable),
    /// asserts a zero exit code, and returns the program stdout that follows the run banner.
    /// </summary>
    private string RunSelfContained(string fileName, string source)
    {
        var path = _ws.WriteSpy(source, fileName);
        var run = Exec(_ws.Root, "dotnet", CliDll, "run", "--self-contained", path);
        run.ExitCode.Should().Be(0, $"self-contained run failed:\n{run.StdOut}\n{run.StdErr}");
        return ExtractRunProgramOutput(run.StdOut);
    }

    private static string ExtractRunProgramOutput(string stdout)
    {
        var index = stdout.LastIndexOf(RunBanner, StringComparison.Ordinal);
        index.Should().BeGreaterThan(-1, $"run output must contain the '{RunBanner}' banner:\n{stdout}");
        return stdout[(index + RunBanner.Length)..].Trim('\r', '\n', ' ');
    }

    private static ProcessResult Exec(string workingDirectory, string fileName, params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
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

        // A self-contained publish restores and builds a runtime pack, so it needs a generous budget.
        process.WaitForExit(300_000).Should().BeTrue("the publish/run process must not hang");
        process.WaitForExit(); // flush async readers
        return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private sealed record ProcessResult(int ExitCode, string StdOut, string StdErr);
}
