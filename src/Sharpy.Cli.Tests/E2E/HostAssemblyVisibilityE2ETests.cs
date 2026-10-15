using System.Diagnostics;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Sharpy.Cli.Tests.E2E;

/// <summary>
/// #2250 (P54 final-verify F3), the sharpyc twin of
/// <c>Sharpy.Compiler.Tests.Conformance.FrameworkVisibilityTests</c>: the framework a compilation sees
/// is the shared framework plus Sharpy.Core, never sharpyc's own application assemblies
/// (System.CommandLine, Microsoft.CodeAnalysis*, Microsoft.Extensions.*). sharpyc's trusted-platform
/// list carries those packages, so this host discriminates a name filter from the shared-framework
/// directory.
/// </summary>
/// <remarks>
/// Measured with the built sharpyc: <c>from microsoft.extensions.primitives import StringValues</c> was
/// SPY0300 at base 486c9a88a and compiled, then died with FileNotFoundException, at effba7776;
/// <c>from system.command_line import RootCommand</c> compiled then died (exit 134,
/// FileNotFoundException System.CommandLine) at both. A refusal (SPY0301, the system.* prefix's
/// answer for a namespace that exists nowhere) is the fix by direction. The
/// <c>-r</c> cell is the positive control: the same import with the package given as a reference
/// compiles and runs.
/// </remarks>
public class HostAssemblyVisibilityE2ETests : IDisposable
{
    private static readonly string CliDll = Path.Combine(AppContext.BaseDirectory, "sharpyc.dll");

    private const string PrimitivesProgram =
        "from microsoft.extensions.primitives import StringValues\n\n"
        + "def main() -> None:\n    s = StringValues(\"abc\")\n    print(s.count)\n";

    private const string CommandLineProgram =
        "from system.command_line import RootCommand\n\n"
        + "def main() -> None:\n    r = RootCommand(\"hello\")\n    print(r.description)\n";

    private readonly TempWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    /// <summary>
    /// The refusal is the one a namespace that exists nowhere gets under the same prefix: SPY0300 for
    /// <c>microsoft.*</c>, SPY0301 for <c>system.*</c> (whose arm maps any name without an existence
    /// check — <c>from system.no_such_namespace import Foo</c> is SPY0301 at base 486c9a88a, measured).
    /// </summary>
    [Theory]
    [InlineData("extensions_primitives", PrimitivesProgram, "SPY0300")]
    [InlineData("command_line", CommandLineProgram, "SPY0301")]
    public void HostPackageImport_WithoutReference_IsRefusedAtImport(string cell, string program, string refusal)
    {
        _ws.WriteSpy(program);
        var result = ExecCli("run", "main.spy");
        var output = result.StdOut + result.StdErr;

        result.ExitCode.Should().Be(1, $"[{cell}] a host package is not the framework — refused, not run\n{output}");
        output.Should().Contain(refusal, $"[{cell}] refused at import\n{output}");
        output.Should().NotContain("FileNotFoundException", $"[{cell}] never a compile that dies at run\n{output}");
    }

    [Fact]
    public void HostPackageImport_WithReference_CompilesAndRuns()
    {
        var hostCopy = Path.Combine(AppContext.BaseDirectory, "System.CommandLine.dll");
        File.Exists(hostCopy).Should().BeTrue("INCONCLUSIVE: sharpyc ships System.CommandLine next to the test host");
        var userCopy = Path.Combine(_ws.Root, "libs", "System.CommandLine.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(userCopy)!);
        File.Copy(hostCopy, userCopy);

        _ws.WriteSpy(CommandLineProgram);
        var result = ExecCli("run", "main.spy", "-r", userCopy);
        var output = result.StdOut + result.StdErr;

        result.ExitCode.Should().Be(0, $"a host package given by reference is visible and deployed\n{output}");
        result.StdOut.Should().Contain("hello", output);
    }

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

        process.WaitForExit(180_000).Should().BeTrue("the process must not hang");
        process.WaitForExit(); // flush async readers
        return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private sealed record ProcessResult(int ExitCode, string StdOut, string StdErr);
}
