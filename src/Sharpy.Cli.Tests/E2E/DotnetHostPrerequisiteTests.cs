using System.Diagnostics;
using FluentAssertions;
using Xunit;

namespace Sharpy.Cli.Tests.E2E;

/// <summary>
/// #2292: a command that needs the <c>dotnet</c> host either finds it or refuses BEFORE doing any work
/// with SPY0913 naming what to install — never compiles first and then dies on an unhandled
/// <c>Win32Exception</c>, which is what the self-contained release archive's <c>run</c> did on a
/// machine with no .NET install.
/// </summary>
/// <remarks>
/// <para>
/// The cells run the built <c>sharpyc</c> APPHOST as a child process. Not <c>dotnet sharpyc.dll</c>:
/// under the muxer the running executable's own directory holds <c>dotnet</c>, so that spelling can
/// never reach the no-dotnet cell. <c>DOTNET_ROOT</c> lets the apphost start on the runtime running
/// these tests; <c>PATH</c> is an empty directory. The child gets its own environment, never a
/// mutated one in this process (process-global state; the P54 class).
/// </para>
/// <para>
/// Instrument checks: neither lookup location (the apphost's directory, the PATH directory) holds a
/// <c>dotnet</c>; the controls compile under the same environment, so the apphost does start there;
/// and the positive control puts the runtime's <c>dotnet</c> back on PATH and the same <c>run</c>
/// prints the program.
/// </para>
/// <para>
/// MUTATIONS (recorded in the #2292 commit): delete the preflight in <c>RunCommand</c> → the
/// <c>run</c> row red (compiles, then the unhandled exception); in <c>CompileCommand</c> → the
/// <c>compile --self-contained</c> row red; in <c>ProjectCommand</c> → the <c>project</c> row red.
/// </para>
/// </remarks>
public class DotnetHostPrerequisiteTests : IDisposable
{
    private static readonly string Apphost =
        Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "sharpyc.exe" : "sharpyc");

    private static readonly string DotnetFileName = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";

    /// <summary>The install root of the runtime running these tests: shared/Microsoft.NETCore.App/&lt;version&gt;/ → root.</summary>
    private static readonly string DotnetRoot =
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "..", "..", ".."));

    private const string Program = "def main():\n    print(\"hi\")\n";

    private const string ProjectWithPackage = """
        <Project>
          <PropertyGroup>
            <RootNamespace>P</RootNamespace>
            <OutputType>Exe</OutputType>
          </PropertyGroup>
          <ItemGroup>
            <SpyFile Include="main.spy" />
            <PackageReference Include="Humanizer.Core" Version="2.14.1" />
          </ItemGroup>
        </Project>
        """;

    private const string ProjectWithoutPackage = """
        <Project>
          <PropertyGroup>
            <RootNamespace>P</RootNamespace>
            <OutputType>Exe</OutputType>
          </PropertyGroup>
          <ItemGroup>
            <SpyFile Include="main.spy" />
          </ItemGroup>
        </Project>
        """;

    private readonly TempWorkspace _ws = new();
    private readonly string _emptyBin;

    public DotnetHostPrerequisiteTests()
    {
        _emptyBin = Path.Combine(_ws.Root, "empty-bin");
        Directory.CreateDirectory(_emptyBin);
        _ws.WriteSpy(Program, "h.spy");
        _ws.WriteFile("pkg/p.spyproj", ProjectWithPackage);
        _ws.WriteFile("pkg/main.spy", Program);
        _ws.WriteFile("plain/p.spyproj", ProjectWithoutPackage);
        _ws.WriteFile("plain/main.spy", Program);
    }

    public void Dispose() => _ws.Dispose();

    /// <summary>
    /// Commands that start <c>dotnet</c>: (cell, working dir, args, what SPY0913 must name, a path the
    /// refused command must not have produced).
    /// </summary>
    public static TheoryData<string, string, string[], string, string?> RefusedWithoutDotnet => new()
    {
        { "run", "", new[] { "run", "h.spy" }, ".NET 10 runtime or SDK", null },
        { "run --self-contained", "", new[] { "run", "--self-contained", "h.spy" }, ".NET 10 SDK", null },
        { "compile --self-contained", "", new[] { "compile", "--self-contained", "h.spy", "-o", "sc/h.exe" }, ".NET 10 SDK", "sc" },
        { "project + PackageReference", "pkg", new[] { "project", "p.spyproj" }, ".NET 10 SDK", "pkg/bin" },
    };

    [Theory]
    [MemberData(nameof(RefusedWithoutDotnet))]
    public async Task WithoutDotnet_RefusesBeforeDoingAnyWork(string cell, string workDir, string[] args, string names, string? notProduced)
    {
        AssertNoDotnetReachable();

        var (exit, stdout, stderr) = await RunApphostAsync(workDir, args, pathValue: _emptyBin);

        exit.Should().Be(1, $"[{cell}] refuses with the ordinary-failure exit code; stdout:\n{stdout}\nstderr:\n{stderr}");
        stderr.Should().Contain("SPY0913", $"[{cell}]");
        stderr.Should().Contain(names, $"[{cell}] names what to install");
        stderr.Should().Contain("no `dotnet` executable was found on PATH", $"[{cell}]");
        (stdout + stderr).Should().NotContain("Unhandled exception", $"[{cell}]");
        stdout.Should().NotContain("Successfully compiled", $"[{cell}] refuses BEFORE compiling");
        stdout.Should().NotContain("Restoring", $"[{cell}] refuses before restoring");
        if (notProduced != null)
        {
            Path.Combine(_ws.Root, notProduced).Should().Match(p => !Directory.Exists(p) && !File.Exists(p),
                $"[{cell}] produced nothing");
        }
    }

    /// <summary>Commands that start nothing: the same environment, and they work.</summary>
    public static TheoryData<string, string, string[]> UnaffectedWithoutDotnet => new()
    {
        { "build", "", new[] { "build", "h.spy" } },
        { "compile", "", new[] { "compile", "h.spy", "-o", "out/h.exe" } },
        { "project without packages", "plain", new[] { "project", "p.spyproj" } },
    };

    [Theory]
    [MemberData(nameof(UnaffectedWithoutDotnet))]
    public async Task WithoutDotnet_CommandsThatStartNothing_StillWork(string cell, string workDir, string[] args)
    {
        AssertNoDotnetReachable();

        var (exit, stdout, stderr) = await RunApphostAsync(workDir, args, pathValue: _emptyBin);

        exit.Should().Be(0, $"[{cell}] needs no dotnet; stdout:\n{stdout}\nstderr:\n{stderr}");
        stderr.Should().NotContain("SPY0913", $"[{cell}]");
    }

    [Fact]
    public async Task WithDotnetOnPath_TheSameRunPrintsTheProgram()
    {
        // Positive control: the harness sees a dotnet when one is there.
        File.Exists(Path.Combine(DotnetRoot, DotnetFileName)).Should().BeTrue(
            $"the runtime running these tests is installed under {DotnetRoot}");

        var (exit, stdout, stderr) = await RunApphostAsync("", new[] { "run", "h.spy" }, pathValue: DotnetRoot);

        exit.Should().Be(0, $"stdout:\n{stdout}\nstderr:\n{stderr}");
        stdout.Should().Contain("hi");
        stderr.Should().NotContain("SPY0913");
    }

    private void AssertNoDotnetReachable()
    {
        File.Exists(Apphost).Should().BeTrue($"the apphost is built beside the tests ({Apphost})");
        File.Exists(Path.Combine(AppContext.BaseDirectory, DotnetFileName)).Should().BeFalse(
            "the apphost's own directory is the resolver's first lookup; a dotnet there would make every no-dotnet row vacuous");
        Directory.EnumerateFileSystemEntries(_emptyBin).Should().BeEmpty("PATH is an empty directory");
    }

    private async Task<(int Exit, string Stdout, string Stderr)> RunApphostAsync(string workDir, string[] args, string pathValue)
    {
        var psi = new ProcessStartInfo
        {
            FileName = Apphost,
            WorkingDirectory = Path.Combine(_ws.Root, workDir),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        psi.Environment["PATH"] = pathValue;
        psi.Environment["DOTNET_ROOT"] = DotnetRoot;
        psi.Environment.Remove("DOTNET_HOST_PATH");

        using var process = Process.Start(psi)!;
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var stdout = process.StandardOutput.ReadToEndAsync(deadline.Token);
        var stderr = process.StandardError.ReadToEndAsync(deadline.Token);
        await process.WaitForExitAsync(deadline.Token);
        return (process.ExitCode, await stdout, await stderr);
    }
}
