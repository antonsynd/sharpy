using System.Diagnostics;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Sharpy.Cli.Tests.E2E;

/// <summary>
/// #1084 regression net: a program that imports a NuGet-backed stdlib module must run
/// standalone from a bare directory containing only what the CLI copied. Each fixture drives
/// the real <c>sharpyc.dll</c> process to <c>compile main.spy -o &lt;bare tmp dir&gt;/main.dll</c>
/// — exercising the CLI's own runtime-dependency copy and <c>deps.json</c> generation, NOT a
/// test helper — then executes <c>dotnet main.dll</c> from that directory and asserts exact
/// stdout and a zero exit code.
///
/// <para>
/// The <c>numpy</c> fixture is the #1084 regression proper: before the fix, <c>MathNet.Numerics</c>
/// was copied next to the output but never listed in <c>deps.json</c>, so the host never resolved
/// it and the program died at the first MathNet call. <c>toml</c> and <c>yaml</c> are the issue's
/// requested audit, executable. <c>sqlite3</c> skips until #1107 (the SQLitePCLRaw provider bundle
/// and native asset are not co-located with the CLI); the resolver already copies them once present.
/// </para>
/// </summary>
public class StandaloneDeploymentTests : IDisposable
{
    private static readonly string CliDll = Path.Combine(AppContext.BaseDirectory, "sharpyc.dll");

    private readonly TempWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    [Fact]
    public void Numpy_RunsStandalone() =>
        // The #1084 regression proper: forces a MathNet.Numerics call. Before the fix, MathNet was
        // copied next to the output but never listed in deps.json, so the host never resolved it.
        CompileAndRunStandalone(
            """
            import numpy as np


            def main():
                a = np.array([1.0, 2.0])
                print(np.sum(a))
            """).Should().Be("3.0");

    [Fact]
    public void PlainProgram_RunsStandalone() =>
        // No stdlib import at all. deps.json deliberately declares the full closure of the
        // default references — a superset of what the CLI copies beside the output — so this
        // pins that declared-but-absent entries stay harmless: the host only consults an entry
        // when the assembly actually loads.
        CompileAndRunStandalone(
            """
            def main():
                print("hello")
            """).Should().Be("hello");

    [Fact]
    public void Toml_RunsStandalone() =>
        CompileAndRunStandalone(
            """
            import toml


            def main():
                data = toml.loads("count = 42")
                print(data["count"])
            """).Should().Be("42");

    [Fact]
    public void Yaml_RunsStandalone() =>
        CompileAndRunStandalone(
            """
            import yaml


            def main():
                result: object = yaml.safe_load("42")
                print(result)
            """).Should().Be("42");

    [Fact(Skip = "Blocked by #1107: the SQLitePCLRaw provider bundle (batteries_v2/provider) and native "
        + "e_sqlite3 asset are not co-located with the CLI, so there is nothing for the copy path to pick "
        + "up. RuntimeClosureResolver already discovers and copies them once present (see "
        + "RuntimeClosureResolverTests); remove this Skip when #1107 lands.")]
    public void Sqlite3_RunsStandalone() =>
        // In-memory DB roundtrip — forces a native e_sqlite3 load.
        CompileAndRunStandalone(
            """
            import sqlite3


            def main():
                conn = sqlite3.connect(":memory:")
                conn.execute("CREATE TABLE t (x INTEGER)")
                conn.execute("INSERT INTO t VALUES (42)")
                conn.commit()
                cursor = conn.execute("SELECT x FROM t")
                match cursor.fetchone() to array[object]:
                    case list() as row:
                        print(row[0])
                    case _:
                        print("no row")
                conn.close()
            """).Should().Be("42");

    // ── harness ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Compiles <paramref name="source"/> into a fresh, otherwise-empty directory via the real
    /// <c>sharpyc compile</c>, executes the produced assembly from that bare directory, asserts a
    /// zero exit code, and returns the normalized program stdout.
    /// </summary>
    private string CompileAndRunStandalone(string source)
    {
        _ws.WriteSpy(source, "main.spy");
        var outDir = _ws.PathFor("out");
        Directory.CreateDirectory(outDir);
        var outPath = Path.Combine(outDir, "main.dll");

        var compile = ExecCli(_ws.Root, "compile", _ws.PathFor("main.spy"), "-o", outPath);
        compile.ExitCode.Should().Be(0, $"compilation failed:\n{compile.StdOut}\n{compile.StdErr}");
        File.Exists(outPath).Should().BeTrue("the CLI must produce the output assembly");

        // Run from the bare output directory: nothing is on the probing path but what the CLI
        // copied, which is exactly what distinguishes a real fix from "works on the dev machine".
        var run = Exec(outDir, "dotnet", outPath);
        run.ExitCode.Should().Be(0, $"standalone program failed:\n{run.StdOut}\n{run.StdErr}");
        return Normalize(run.StdOut);
    }

    private ProcessResult ExecCli(string workingDirectory, params string[] args) =>
        Exec(workingDirectory, "dotnet", new[] { CliDll }.Concat(args).ToArray());

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

        process.WaitForExit(180_000).Should().BeTrue("the process must not hang");
        process.WaitForExit(); // flush async readers
        return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private static string Normalize(string text) =>
        text.Replace("\r\n", "\n").TrimEnd('\n');

    private sealed record ProcessResult(int ExitCode, string StdOut, string StdErr);
}
