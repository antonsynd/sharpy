using System.Diagnostics;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Sharpy.Cli.Tests.E2E;

/// <summary>
/// <c>sharpyc project --incremental</c> must build what a <c>--clean</c> build builds when only a
/// compilation OPTION or a REFERENCED ASSEMBLY changed (#2114). Neither is in a source file, so a
/// content hash cannot see it; the incremental cache's facts key carries both
/// (<c>IncrementalCompilationCache.ProjectFactsOf</c>).
///
/// <para>Each <c>sharpyc</c> is a fresh process, as a user's builds are. That matters for the
/// rebuilt-library cell: inside one process, a compile that loaded the old library leaves a later
/// cold compile binding its exports (#2114's process-lifetime cell), so the in-process harness
/// (<c>ColdWarmBuildDifferentialTests</c>) measures the reference axis with a dropped reference and
/// this spawned test measures the rebuild.</para>
/// </summary>
public class IncrementalOptionsKeyE2ETests : IDisposable
{
    static readonly string CliDll = Path.Combine(AppContext.BaseDirectory, "sharpyc.dll");

    readonly TempWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    static string Project(string outputType, string rootNamespace, string items, string extra = "")
        => $"""
           <Project>
             <PropertyGroup>
               <OutputType>{outputType}</OutputType>
               <RootNamespace>{rootNamespace}</RootNamespace>{extra}
             </PropertyGroup>
             <ItemGroup>
           {items}
             </ItemGroup>
           </Project>
           """;

    /// <summary>
    /// The reported reproduction (ee9a007), step for step: clean without the flag, warm with it,
    /// clean with it, warm without it. At ee9a007 step B emitted the unfolded expression and step D
    /// the folded constant — each warm build served the OTHER setting's C#.
    /// </summary>
    [Fact]
    public void TogglingAFeatureFlag_WarmEmitsWhatCleanEmits()
    {
        _ws.WriteFile("feat/main.spy", "def main() -> None:\n    x = 2 * 3 + 4\n    print(x)\n");
        var project = _ws.WriteFile("feat/t.spyproj",
            Project("Exe", "T", "    <SpyFile Include=\"main.spy\" />"));
        var dir = Path.GetDirectoryName(project)!;

        string Step(string csDir, params string[] flags)
        {
            var args = new[] { "project", project }
                .Concat(flags)
                .Concat(new[] { "--emit-cs-to", Path.Combine(dir, csDir) })
                .ToArray();
            var result = ExecCli(dir, args);
            result.ExitCode.Should().Be(0, $"{csDir}: {result.StdOut}{result.StdErr}");
            var line = File.ReadAllLines(Path.Combine(dir, csDir, "main.cs"))
                .Single(l => l.Contains("var x", StringComparison.Ordinal)).Trim();
            return line;
        }

        var a = Step("csA", "--clean", "--incremental");
        var b = Step("csB", "--incremental", "--enable-feature", "opt_const_fold");
        var c = Step("csC", "--clean", "--incremental", "--enable-feature", "opt_const_fold");
        var d = Step("csD", "--incremental");

        a.Should().Be("var x = 2 * 3 + 4;", "control: without the flag nothing folds");
        c.Should().Be("var x = 10;", "control: a clean build with the flag folds");
        b.Should().Be(c, "a warm build with the flag must emit what a clean build with it emits (ee9a007: unfolded)");
        d.Should().Be(a, "a warm build without the flag must emit what a clean build without it emits (ee9a007: folded)");
    }

    /// <summary>
    /// #2114's sequence: build a consumer against <c>Lib5.dll</c>, rebuild the library with <c>f</c>
    /// removed and <c>h</c> returning <c>str</c>, then build the consumer <c>--incremental</c> with no
    /// source change. At ee9a007 the warm build skipped both files and failed SPY0908 (CS0117,
    /// <c>'M5Module' does not contain a definition for 'F'</c>) while a clean build reports SPY0301.
    /// </summary>
    [Fact]
    public void RebuildingAReferencedLibrary_WarmReportsWhatCleanReports()
    {
        _ws.WriteFile("lib/m5.spy", "def f() -> int:\n    return 1\n\n\ndef h() -> int:\n    return 2\n");
        var libProject = _ws.WriteFile("lib/lib.spyproj",
            Project("Library", "Lib5", "    <SpyFile Include=\"m5.spy\" />", "\n    <AssemblyName>Lib5</AssemblyName>"));
        var libDir = Path.GetDirectoryName(libProject)!;
        var lib1 = ExecCli(libDir, "project", libProject);
        lib1.ExitCode.Should().Be(0, lib1.StdOut + lib1.StdErr);
        var dll = Directory.GetFiles(Path.Combine(libDir, "bin"), "Lib5.dll", SearchOption.AllDirectories).Single();

        _ws.WriteFile("app/main.spy", "from m5 import f, h\n\n\ndef main() -> None:\n    print(f())\n    print(h())\n");
        _ws.WriteFile("app/other.spy", "def value() -> int:\n    return 1\n");
        var appProject = _ws.WriteFile("app/app.spyproj", Project("Exe", "App",
            "    <SpyFile Include=\"main.spy\" />\n    <SpyFile Include=\"other.spy\" />\n"
            + $"    <Reference Include=\"Lib5\"><HintPath>{dll}</HintPath></Reference>"));
        var appDir = Path.GetDirectoryName(appProject)!;

        var first = ExecCli(appDir, "project", appProject, "--clean", "--incremental");
        first.ExitCode.Should().Be(0, "control: the consumer builds against v1. " + first.StdOut + first.StdErr);

        _ws.WriteFile("lib/m5.spy", "def h() -> str:\n    return \"two\"\n");
        var lib2 = ExecCli(libDir, "project", libProject);
        lib2.ExitCode.Should().Be(0, lib2.StdOut + lib2.StdErr);

        var warm = ExecCli(appDir, "project", appProject, "--incremental");
        var clean = ExecCli(appDir, "project", appProject, "--clean", "--incremental");

        Codes(clean).Should().Contain("SPY0301", "control: a clean build refuses the removed export. " + clean.StdErr);
        Codes(warm).Should().Equal(Codes(clean),
            "the warm build must report what the clean build reports (ee9a007: SPY0908 CS0117). Warm: "
            + warm.StdOut + warm.StdErr);
        warm.ExitCode.Should().Be(clean.ExitCode);
    }

    static IReadOnlyList<string> Codes(ProcessResult result)
        => System.Text.RegularExpressions.Regex.Matches(result.StdOut + result.StdErr, @"SPY\d{4}")
            .Select(m => m.Value).Distinct().OrderBy(c => c, StringComparer.Ordinal).ToList();

    // ── harness ──────────────────────────────────────────────────────────────

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
