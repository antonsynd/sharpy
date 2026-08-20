using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Sharpy.Cli.Tests.E2E;

/// <summary>
/// B1 (#1038) regression guard: the same program must behave identically whether it
/// is compiled through <c>sharpyc run file.spy</c> (single-file, synthetic project)
/// or through <c>sharpyc project x.spyproj</c> (project mode). Each fixture runs
/// end-to-end through BOTH commands in real <c>dotnet</c> processes and asserts
/// identical program stdout (run mode's output is extracted after the
/// "=== Running Program ===" banner; project mode executes the produced assembly
/// directly, so its stdout is pure program output). Diagnostics fixtures compare
/// the SPY code + message line, which includes the shared source path.
/// Includes the #940 reproducer (os.sep/os.environ module static-field access,
/// which historically diverged between the two modes).
/// </summary>
public class ModeDivergenceTests : IDisposable
{
    private const string RunBanner = "=== Running Program ===";
    private static readonly string CliDll = Path.Combine(AppContext.BaseDirectory, "sharpyc.dll");

    private readonly TempWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    // ── fixtures ─────────────────────────────────────────────────────────────

    [Fact]
    public void HelloWorld_IsIdenticalInBothModes() =>
        AssertModesAgree("""
            def main():
                print("Hello from Sharpy")
            """);

    [Fact]
    public void Issue940_ModuleStaticFieldAccess_IsIdenticalInBothModes() =>
        // #940: os.sep / os.environ (module static fields/properties) resolved in run
        // mode but raised SPY0203 in project mode. Unified by #1038.
        AssertModesAgree("""
            import os

            def main():
                print(len(os.sep) > 0)
                print(len(os.pathsep) > 0)
                print(len(os.environ) >= 0)
            """);

    [Fact]
    public void LocalImport_IsIdenticalInBothModes() =>
        AssertModesAgree(
            """
            from util import helper

            def main():
                print(helper())
            """,
            ("util.spy", """
            def helper() -> str:
                return "from util"
            """));

    [Fact]
    public void ModuleLevelState_IsIdenticalInBothModes() =>
        AssertModesAgree("""
            GREETING: str = "state"
            COUNT: int = 3

            def main():
                for _ in range(COUNT):
                    print(GREETING)
            """);

    [Fact]
    public void StdlibImport_IsIdenticalInBothModes() =>
        AssertModesAgree("""
            import math

            def main():
                print(math.floor(3.7))
                print(math.ceil(3.2))
            """);

    [Fact]
    public void FunctoolsPartialPositional_LowersFromTheSpec_UnderRun()
    {
        // #1520: the partial lowering reads the recorded FunctoolsPartialSpec — target symbol,
        // remaining parameters, kwarg C# names all resolved at check time. This is the positional
        // cell; the CLI harness because the functools module resolves via ModuleRegistry only in
        // the deployed layout.
        WriteFixture("""
            import functools

            def add(a: int, b: int) -> int:
                return a + b

            def main():
                add5: (int) -> int = functools.partial(add, 5)
                print(add5(3))
            """);

        var run = ExecCli("run", _ws.PathFor("main.spy"));
        run.ExitCode.Should().Be(0, $"run mode failed:\n{run.StdOut}\n{run.StdErr}");
        Normalize(ExtractRunProgramOutput(run.StdOut)).Should().Be("8");
    }

    [Fact]
    public void FunctoolsPartialKeywordOnFirstParameter_BindsRemainingByName_UnderRun()
    {
        // #1520: fixing the FIRST parameter by keyword forces the remaining argument to bind by
        // its resolved C# parameter name from the spec — bound positionally it walks into the
        // keyword-fixed slot (CS1744 behind SPY0908, the live defect found by /verify-implementation
        // on 2026-08-20; the pre-fix emitter generated `Greet(name, greeting: "hi")`).
        // MUTATION-VERIFIED: with the spec's FixedKeywords blanked locally in
        // CheckFunctoolsPartialCall, this test fails (the kwarg vanishes from the lowering);
        // restored, it passes.
        WriteFixture("""
            import functools

            def greet(greeting: str, name: str) -> str:
                return greeting + ", " + name

            def main():
                hi: (str) -> str = functools.partial(greet, greeting="hi")
                print(hi("sam"))
            """);

        var run = ExecCli("run", _ws.PathFor("main.spy"));
        run.ExitCode.Should().Be(0, $"run mode failed:\n{run.StdOut}\n{run.StdErr}");
        Normalize(ExtractRunProgramOutput(run.StdOut)).Should().Be("hi, sam");
    }

    [Fact]
    public void OutOfSourceSetRenamedAlias_DispatchesImportedOverloads_UnderRun()
    {
        // #1525: the identity chain must hold OUT of the source set — `mylog` is a with-clone of
        // ModuleLoader's extracted math.log; Symbol.OriginSymbol links the clone back to the same
        // extraction object the overload list holds, so the renamed alias dispatches BOTH
        // overloads instead of being spuriously judged "shadowed". Lives in the CLI harness
        // because stdlib MODULE resolution (ModuleRegistry.LoadReference) only exists in the
        // deployed layout; the in-source-set twins are the compiler fixtures
        // cross_module_function_overloads_alias and cross_module_overload_shadow_1525.
        WriteFixture("""
            from math import log as mylog

            def main():
                print(mylog(8.0, 2.0))
                print(mylog(1.0))
            """);

        var run = ExecCli("run", _ws.PathFor("main.spy"));
        run.ExitCode.Should().Be(0, $"run mode failed:\n{run.StdOut}\n{run.StdErr}");
        Normalize(ExtractRunProgramOutput(run.StdOut)).Should().Be("3.0\n0.0",
            "both math.log overloads must dispatch through the renamed alias (#1525)");
    }

    [Fact]
    public void TestFunctionCalledFromMain_RunsFrameworkFreeInBothModes() =>
        // #1495/#1532: a @test program is an ordinary runnable program outside a test host. Both CLI
        // modes are non-test-host, so the @test function must be an ordinary module-level function a
        // caller can reach (else CS0103), and its asserts must lower framework-free and ENFORCE
        // (this program's asserts all hold, so it exits 0 and prints; a broken lowering would ICE at
        // compile time before any output). Historically every @test program was uncompilable here.
        // isinstance uses or-of-singles: (A, B) now denotes tuple[A, B] (#1532).
        AssertModesAgree("""
            @test
            def passing_assert() -> None:
                assert 2 == 2

            @test
            def isinstance_check() -> None:
                x: object = 42
                assert isinstance(x, int) or isinstance(x, str)
                assert not (isinstance(x, str) or isinstance(x, float))

            def main() -> None:
                passing_assert()
                isinstance_check()
                print("tests-ran")
            """);

    [Fact]
    public void TypeErrorDiagnostics_AreIdenticalInBothModes()
    {
        WriteFixture("""
            def main():
                x: int = "not an int"
                print(x)
            """);

        var run = ExecCli("run", _ws.PathFor("main.spy"));
        var project = ExecCli("project", _ws.PathFor("app.spyproj"));

        run.ExitCode.Should().NotBe(0, "run mode must fail on a type error");
        project.ExitCode.Should().NotBe(0, "project mode must fail on a type error");

        var runDiags = ExtractDiagnostics(run);
        var projectDiags = ExtractDiagnostics(project);

        runDiags.Should().NotBeEmpty("the type error must be reported");
        runDiags.Should().Equal(projectDiags,
            "both modes must report the same SPY diagnostics for the same source");
    }

    // ── harness ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Writes <c>main.spy</c> (+ any extra files) and a one-project .spyproj into the
    /// workspace, runs the program through both CLI modes, and asserts identical
    /// program stdout and exit codes.
    /// </summary>
    private void AssertModesAgree(string mainSource, params (string Name, string Source)[] extraFiles)
    {
        WriteFixture(mainSource, extraFiles);

        // Mode A: sharpyc run main.spy — program output follows the banner.
        var run = ExecCli("run", _ws.PathFor("main.spy"));
        run.ExitCode.Should().Be(0, $"run mode failed:\n{run.StdOut}\n{run.StdErr}");
        var runProgramOutput = ExtractRunProgramOutput(run.StdOut);

        // Mode B: sharpyc project app.spyproj, then execute the produced assembly.
        var project = ExecCli("project", _ws.PathFor("app.spyproj"));
        project.ExitCode.Should().Be(0, $"project mode failed:\n{project.StdOut}\n{project.StdErr}");
        // The produced file is an IL assembly (even with a .exe extension), so it is
        // executed through the muxer, exactly like RunCommand does for run mode.
        // Runtime deps sit next to the CLI, not the output — copy them like run does.
        var assemblyPath = ExtractOutputAssemblyPath(project.StdOut);
        CopyRuntimeDependencies(Path.GetDirectoryName(assemblyPath)!);
        var execution = Exec("dotnet", new[] { assemblyPath });
        execution.ExitCode.Should().Be(0, $"project-built program failed:\n{execution.StdOut}\n{execution.StdErr}");

        Normalize(runProgramOutput).Should().Be(
            Normalize(execution.StdOut),
            "run and project modes must produce identical program output (#1038, #940)");
    }

    private void WriteFixture(string mainSource, params (string Name, string Source)[] extraFiles)
    {
        _ws.WriteSpy(Dedent(mainSource));
        foreach (var (name, source) in extraFiles)
        {
            _ws.WriteFile(name, Dedent(source));
        }

        _ws.WriteFile("app.spyproj", """
            <Project>
              <PropertyGroup>
                <RootNamespace>ModeDivergence</RootNamespace>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>ModeDivergenceFixture</AssemblyName>
              </PropertyGroup>
              <ItemGroup>
                <SpyFile Include="**/*.spy" />
              </ItemGroup>
            </Project>
            """);
    }

    /// <summary>Program stdout is everything after the run banner and its blank line.</summary>
    private static string ExtractRunProgramOutput(string stdout)
    {
        var index = stdout.LastIndexOf(RunBanner, StringComparison.Ordinal);
        index.Should().BeGreaterThan(-1, $"run output must contain the '{RunBanner}' banner:\n{stdout}");
        var afterBanner = stdout[(index + RunBanner.Length)..];
        return afterBanner.TrimStart('\r', '\n');
    }

    private static string ExtractOutputAssemblyPath(string stdout)
    {
        // ProjectCommand prints "Output: <OutputType>" early and
        // "Output: <path>.exe|.dll" (the produced apphost/assembly) on success.
        var match = Regex.Matches(stdout, @"^Output:\s*(.+\.(?:dll|exe))\s*$", RegexOptions.Multiline)
            .LastOrDefault();
        match.Should().NotBeNull($"project output must name the produced assembly:\n{stdout}");
        return match!.Groups[1].Value.Trim();
    }

    /// <summary>
    /// Diagnostic identity = "SPYxxxx: message" plus the source location suffix when
    /// present. Both modes compile the same on-disk file, so paths are comparable;
    /// CLI chrome (banners, timings, summaries) is deliberately excluded.
    /// </summary>
    private static IReadOnlyList<string> ExtractDiagnostics(ProcessResult result)
    {
        var combined = result.StdOut + "\n" + result.StdErr;
        return Regex.Matches(combined, @"(error|warning)\[(SPY\d{4})\][^\r\n]*")
            .Select(m => m.Value.Trim())
            .Distinct()
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();
    }

    private static void CopyRuntimeDependencies(string outputDir)
    {
        foreach (var dep in new[] { "Sharpy.Core.dll", "Sharpy.Stdlib.dll" })
        {
            var source = Path.Combine(AppContext.BaseDirectory, dep);
            var target = Path.Combine(outputDir, dep);
            if (File.Exists(source) && !File.Exists(target))
            {
                File.Copy(source, target);
            }
        }
    }

    private ProcessResult ExecCli(params string[] args) =>
        Exec("dotnet", new[] { CliDll }.Concat(args).ToArray());

    private ProcessResult Exec(string fileName, string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = _ws.Root,
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

        process.WaitForExit(180_000).Should().BeTrue("the CLI process must not hang");
        process.WaitForExit(); // flush async readers
        return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private static string Normalize(string text) =>
        text.Replace("\r\n", "\n").TrimEnd('\n');

    /// <summary>Raw-string fixture sources arrive pre-dedented by C#; kept for clarity.</summary>
    private static string Dedent(string source) => source;

    private sealed record ProcessResult(int ExitCode, string StdOut, string StdErr);
}
