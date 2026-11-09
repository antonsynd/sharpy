using System.Diagnostics;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Sharpy.Cli.Tests.E2E;

/// <summary>
/// <c>--incremental</c> on a <c>.spyproj</c> whose every input is the last build's is an up-to-date
/// check, not a build (#2468): it exits as that build did, prints its warnings, leaves its output as
/// written, writes its C# when asked, and loads no reference (no <c>Module Discovery</c> line). Both
/// project routes, <c>sharpyc project</c> and <c>sharpyc compile x.spyproj</c>, each step a fresh
/// process as a user's builds are — the in-process theory (<c>ColdWarmUpToDateCheckTests</c>) cannot
/// see that the CLI's options factory turns the check on.
/// </summary>
public class UpToDateCheckE2ETests : IDisposable
{
    static readonly string CliDll = Path.Combine(AppContext.BaseDirectory, "sharpyc.dll");

    readonly TempWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    const string MainSource = "from lib import twice\n\n\ndef main() -> None:\n    print(twice(2))\n";
    const string LibSource = "def twice(x: int) -> int:\n    unused: int = 7\n    return x * 2\n";

    [Theory]
    [InlineData("project")]
    [InlineData("compile")]
    public void WarmNoChangeBuild_IsUpToDate_AndAnEditRebuilds(string route)
    {
        _ws.WriteFile($"{route}/main.spy", MainSource);
        _ws.WriteFile($"{route}/lib.spy", LibSource);
        var project = _ws.WriteFile($"{route}/u.spyproj", """
            <Project>
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <RootNamespace>UpToDateE2E</RootNamespace>
              </PropertyGroup>
              <ItemGroup>
                <SpyFile Include="main.spy" />
                <SpyFile Include="lib.spy" />
              </ItemGroup>
            </Project>
            """);
        var dir = Path.GetDirectoryName(project)!;
        var csA = Path.Combine(dir, "csA");
        var csC = Path.Combine(dir, "csC");

        ProcessResult Run(params string[] flags) => ExecCli(dir, new[] { route, project }.Concat(flags).ToArray());
        string[] EmitFlags(string to) => route == "project" ? new[] { "--emit-cs-to", to } : new[] { "--emit-csharp" };

        // A: clean. The positive control for every absence below.
        var a = Run(new[] { "--clean", "--incremental", "--metrics-format", "text" }.Concat(EmitFlags(csA)).ToArray());
        a.ExitCode.Should().Be(0, a.StdOut + a.StdErr);
        a.StdOut.Should().Contain("Module Discovery", "control: a clean build loads the references");
        a.StdOut.Should().NotContain("Up to date:", "control: a clean build is not up to date");
        Codes(a).Should().Contain("SPY0451", "non-vacuity: the build reports a warning to replay");
        var dll = Directory.GetFiles(Path.Combine(dir, "bin"), "UpToDateE2E.*", SearchOption.AllDirectories)
            .Single(f => f.EndsWith(".dll", StringComparison.Ordinal) || f.EndsWith(".exe", StringComparison.Ordinal));
        var writeTime = File.GetLastWriteTimeUtc(dll);
        var outDir = Path.GetDirectoryName(dll)!;
        var csharpA = Snapshot(route == "project" ? csA : outDir);
        csharpA.Should().NotBeEmpty("control: the clean build wrote its C#");
        if (route == "compile")
        {
            foreach (var file in Directory.GetFiles(outDir, "*.cs", SearchOption.AllDirectories))
                File.Delete(file);
        }

        // B: nothing changed.
        var b = Run("--incremental", "--metrics-format", "text");
        b.ExitCode.Should().Be(0, b.StdOut + b.StdErr);
        b.StdOut.Should().Contain("Up to date:", "every input is the last build's");
        b.StdOut.Should().Contain("Files Compiled: 0");
        b.StdOut.Should().NotContain("Module Discovery", "an up-to-date build loads no reference");
        Codes(b).Should().Equal(Codes(a), "the up-to-date build prints the clean build's warnings");
        WarningLines(b).Should().Equal(WarningLines(a));
        File.GetLastWriteTimeUtc(dll).Should().Be(writeTime, "the output is left as the clean build wrote it");

        // C: nothing changed, C# requested: the clean build's C#.
        var c = Run(new[] { "--incremental", "--metrics-format", "text" }.Concat(EmitFlags(csC)).ToArray());
        c.ExitCode.Should().Be(0, c.StdOut + c.StdErr);
        c.StdOut.Should().Contain("Up to date:");
        Snapshot(route == "project" ? csC : outDir).Should().Equal(csharpA,
            "the up-to-date build writes the C# the clean build wrote");

        // D: an edit is not up to date, and rebuilds the output.
        File.WriteAllText(Path.Combine(dir, "main.spy"), MainSource + "    print(\"edited\")\n");
        var d = Run("--incremental", "--metrics-format", "text");
        d.ExitCode.Should().Be(0, d.StdOut + d.StdErr);
        d.StdOut.Should().NotContain("Up to date:", "a source moved");
        File.GetLastWriteTimeUtc(dll).Should().NotBe(writeTime, "the edited build writes a new output");
    }

    static IReadOnlyList<KeyValuePair<string, string>> Snapshot(string dir)
        => Directory.Exists(dir)
            ? Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories)
                .OrderBy(f => f, StringComparer.Ordinal)
                .Select(f => new KeyValuePair<string, string>(Path.GetRelativePath(dir, f), File.ReadAllText(f)))
                .ToList()
            : new List<KeyValuePair<string, string>>();

    static IReadOnlyList<string> Codes(ProcessResult result)
        => System.Text.RegularExpressions.Regex.Matches(result.StdOut + result.StdErr, @"SPY\d{4}")
            .Select(m => m.Value).Distinct().OrderBy(c => c, StringComparer.Ordinal).ToList();

    static IReadOnlyList<string> WarningLines(ProcessResult result)
        => result.StdOut.Split('\n').Where(l => l.Contains("SPY0451", StringComparison.Ordinal))
            .Select(l => l.TrimEnd()).ToList();

    // ── harness (IncrementalOptionsKeyE2ETests' shape) ──────────────────────

    static ProcessResult ExecCli(string workingDirectory, params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = workingDirectory,
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
        process.OutputDataReceived += (_, e) => { if (e.Data != null) lock (stdout) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (stderr) stderr.AppendLine(e.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit(180_000).Should().BeTrue("the CLI process must not hang");
        process.WaitForExit(); // flush async readers
        return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    sealed record ProcessResult(int ExitCode, string StdOut, string StdErr);
}
