using System.Diagnostics;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Sharpy.Cli.Tests.E2E;

/// <summary>
/// #2028 regression guard: a project build is successful iff its diagnostic bag has no errors.
/// </summary>
/// <remarks>
/// <para>
/// A library module refused by name (SPY0520 until #2039; now SPY0523, a function spelled like the
/// module's members class) has its C# dropped. When nothing imports that module the remaining C# compiles cleanly, and the
/// driver used to key the verdict on Roslyn alone: <c>sharpyc project</c> printed
/// <c>Build succeeded.</c>, never showed the SPY0520 it held, saved the incremental cache and wrote
/// a complete <c>Probe.exe</c> (2560 bytes, measured at acd1d40a2). When the module IS imported the
/// build failed correctly, but the output files were opened before Roslyn ran, so the failed build
/// left a 0-byte <c>Probe.exe</c> and <c>Probe.pdb</c> behind (measured at acd1d40a2). Both cells
/// assert the printed verdict, the refusal line and the ABSENCE of every output artifact; the
/// artifacts present at the base commit are the positive controls for the absence assertions.
/// </para>
/// </remarks>
public class FailedBuildVerdictTests : IDisposable
{
    private static readonly string CliDll = Path.Combine(AppContext.BaseDirectory, "sharpyc.dll");

    // A library module refused by name: `probe_module` is spelled like the module's members class
    // `ProbeModule` (SPY0523, #2039). It was a struct named like its file (SPY0520) until every module
    // became a namespace and that shape stopped being a refusal.
    private const string RefusedModule = """
        def probe_module() -> int:
            return 1

        def helper() -> int:
            return 7
        """;

    private readonly TempWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Project_RefusedModule_FailsTheBuildAndWritesNoAssembly(bool imported)
    {
        _ws.WriteFile("probe.spy", RefusedModule);
        _ws.WriteSpy(imported
            ? "from probe import helper\n\ndef main() -> None:\n    print(helper())\n"
            : "def main() -> None:\n    print(7)\n");
        _ws.WriteFile("app.spyproj", """
            <Project>
              <PropertyGroup>
                <RootNamespace>Verdict</RootNamespace>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>Probe</AssemblyName>
                <EntryPoint>main.spy</EntryPoint>
              </PropertyGroup>
              <ItemGroup>
                <SpyFile Include="**/*.spy" />
              </ItemGroup>
            </Project>
            """);

        var result = ExecCli("project", _ws.PathFor("app.spyproj"));
        var combined = result.StdOut + "\n" + result.StdErr;

        result.ExitCode.Should().NotBe(0, combined);
        result.StdErr.Should().Contain("Build FAILED.", combined);
        combined.Should().NotContain("Build succeeded.");
        combined.Should().Contain("error[SPY0523]: 'probe_module' compiles to 'ProbeModule', which is this module's members class");
        combined.Should().Contain("--> " + _ws.PathFor("probe.spy") + ":1:5",
            "the refusal names the file whose function collides and the function's name token (#2032)");

        var artifacts = Directory.Exists(_ws.PathFor("bin"))
            ? Directory.GetFiles(_ws.PathFor("bin"), "Probe.*", SearchOption.AllDirectories)
            : Array.Empty<string>();
        artifacts.Should().BeEmpty(
            "a failed build writes no assembly, PDB or runtime files — at acd1d40a2 the un-imported "
            + "cell wrote a complete Probe.exe and the imported cell left 0-byte Probe.exe/.pdb (#2028)");
    }

    // ── harness ──────────────────────────────────────────────────────────────

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
