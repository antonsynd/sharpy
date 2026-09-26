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
/// <para>
/// The SPY0523 cells stop at semantic time, before the assembly phase, so they no longer reach the
/// verdict #2028 moved once #2039 retired SPY0520. The emitter-time cells (SPY0500) do: a refused
/// module that the entry does not reference drops out of the C# and Roslyn accepts the rest.
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
        WriteProject();

        var result = ExecCli("project", _ws.PathFor("app.spyproj"));
        var combined = result.StdOut + "\n" + result.StdErr;

        result.ExitCode.Should().NotBe(0, combined);
        result.StdErr.Should().Contain("Build FAILED.", combined);
        combined.Should().NotContain("Build succeeded.");
        combined.Should().Contain("error[SPY0523]: 'probe_module' compiles to 'ProbeModule', which is this module's members class");
        combined.Should().Contain("--> " + _ws.PathFor("probe.spy") + ":1:5",
            "the refusal names the file whose function collides and the function's name token (#2032)");

        ArtifactsOf("Probe").Should().BeEmpty(
            "a failed build writes no assembly, PDB or runtime files — at acd1d40a2 the un-imported "
            + "cell wrote a complete Probe.exe and the imported cell left 0-byte Probe.exe/.pdb (#2028)");
    }

    // Emitter-time refusals (SPY0500), each followed by a function the entry can reference. The
    // SPY0523 cells above fail at SEMANTIC time, before the assembly phase, so they never reach the
    // verdict #2028 moved. These are refused while the C# is emitted: the refused unit's C# is
    // dropped, and an entry that does not reference it compiles cleanly — the input on which only a
    // bag-keyed verdict fails the build (at acd1d40a2 the un-imported and the imported-unreferenced
    // cells printed `Build succeeded.` and wrote the assembly, measured).
    private const string ModuleHelper = "\n\ndef helper() -> int:\n    return 7\n";

    [Theory]
    [InlineData("@lru_cache\nasync def f() -> int:\n    return 1\n", "referenced", 2, 1)]
    [InlineData("@lru_cache\nasync def f() -> int:\n    return 1\n", "unreferenced", 2, 1)]
    [InlineData("@lru_cache\nasync def f() -> int:\n    return 1\n", "unimported", 2, 1)]
    [InlineData("@lru_cache\ndef g() -> int:\n    yield 1\n", "referenced", 2, 1)]
    [InlineData("@lru_cache\ndef g() -> int:\n    yield 1\n", "unreferenced", 2, 1)]
    [InlineData("@lru_cache\ndef g() -> int:\n    yield 1\n", "unimported", 2, 1)]
    [InlineData("class C:\n    @lru_cache\n    async def f(self) -> int:\n        return 1\n", "referenced", 3, 5)]
    [InlineData("class C:\n    @lru_cache\n    async def f(self) -> int:\n        return 1\n", "unreferenced", 3, 5)]
    [InlineData("class C:\n    @lru_cache\n    async def f(self) -> int:\n        return 1\n", "unimported", 3, 5)]
    public void Project_EmitterRefusedModule_FailsTheBuildAndWritesNoAssembly(
        string refusedModule, string importState, int line, int column)
    {
        _ws.WriteFile("probe.spy", refusedModule + ModuleHelper);
        _ws.WriteSpy(importState switch
        {
            "referenced" => "from probe import helper\n\ndef main() -> None:\n    print(helper())\n",
            "unreferenced" => "import probe\n\ndef main() -> None:\n    print(7)\n",
            _ => "def main() -> None:\n    print(7)\n",
        });
        WriteProject();

        var result = ExecCli("project", _ws.PathFor("app.spyproj"));
        var combined = result.StdOut + "\n" + result.StdErr;

        result.ExitCode.Should().NotBe(0, combined);
        result.StdErr.Should().Contain("Build FAILED.", combined);
        combined.Should().NotContain("Build succeeded.");
        combined.Should().Contain("error[SPY0500]: @lru_cache cannot be combined with", combined);
        combined.Should().Contain($"--> {_ws.PathFor("probe.spy")}:{line}:{column}",
            "the refusal names the refused file and the decorated function (#2032)");
        ArtifactsOf("Probe").Should().BeEmpty(
            $"[{importState}] a failed build writes no assembly, PDB or runtime files (#2028)");
    }

    private void WriteProject() => _ws.WriteFile("app.spyproj", """
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

    private string[] ArtifactsOf(string assemblyName)
        => Directory.Exists(_ws.PathFor("bin"))
            ? Directory.GetFiles(_ws.PathFor("bin"), assemblyName + ".*", SearchOption.AllDirectories)
            : Array.Empty<string>();

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
