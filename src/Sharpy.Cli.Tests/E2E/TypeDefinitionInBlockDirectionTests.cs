using System.Diagnostics;
using System.Text;
using FluentAssertions;
using Sharpy.TestInfrastructure.Registry;
using Xunit;

namespace Sharpy.Cli.Tests.E2E;

/// <summary>
/// Direction pin for P54 (#2250) final verification F2 @ effba7776, through <c>sharpyc run</c> (a refusal is verified
/// with <c>run</c>, not <c>emit</c>): a type definition inside a block or a function body
/// (<see cref="WarmInstanceTriggers.TypeDefinitionTexts"/>) is REFUSED — exit 1, SPY0708 (P21e, R-CS: a type definition
/// outside a declaring position; C# has no local types — it was SPY0202 "Class symbol … not found" from P54 until P21e),
/// SPY0340 for a module-level block — and never an internal error nor a crash bundle.
/// </summary>
/// <remarks>
/// Measured with the built <c>sharpyc</c>: the <c>@dataclass class str</c> in a module-level <c>try</c> was refused
/// SPY0340 at 486c9a88a, became SPY0909 (PhaseViolationException on the frozen master <c>str</c>) plus a
/// <c>.sharpy-crash</c> bundle under the source directory at effba7776, and was SPY0340 + SPY0202 after P54 (SPY0340 +
/// SPY0708 since P21e). The
/// function-local struct/interface/delegate/class texts were SPY0510 ("unrecognized statement type … was not emitted")
/// ICEs at both 486c9a88a and effba7776: the checker had bound them to the master <c>str</c>, so nothing refused them
/// before the emitter.
/// </remarks>
public sealed class TypeDefinitionInBlockDirectionTests : IDisposable
{
    private static readonly string CliDll = Path.Combine(AppContext.BaseDirectory, "sharpyc.dll");

    private readonly TempWorkspace _ws = new();

    public void Dispose() => _ws.Dispose();

    public static IEnumerable<object[]> TypeDefinitionTexts()
        => WarmInstanceTriggers.TypeDefinitionTexts.Select(t => new object[] { t.Name });

    [Theory]
    [MemberData(nameof(TypeDefinitionTexts))]
    public void Run_RefusesTheTypeDefinition_WithoutAnInternalErrorOrACrashBundle(string name)
    {
        var text = WarmInstanceTriggers.TypeDefinitionTexts.Single(t => t.Name == name).Text;
        var source = _ws.WriteSpy(text);

        var result = ExecCli("run", source);
        var combined = result.StdOut + "\n" + result.StdErr;

        result.ExitCode.Should().Be(1, combined);
        combined.Should().Contain("SPY0708", "a type definition outside a declaring position is refused (P21e, R-CS)");
        combined.Should().NotContain("SPY0202", "the checker declines without reporting; the refusal is DeclarationPositionValidator's");
        if (text.StartsWith("try:", StringComparison.Ordinal))
            combined.Should().Contain("SPY0340", "the base refusal of a module-level block stands");
        combined.Should().NotContain("SPY09", "a refusal is not an internal error");
        combined.Should().NotContain("SPY0510", "nothing the checker accepted reaches the emitter unlowerable");
        // `run` writes an ICE's bundle under its temporary build directory and names it in the diagnostic (measured at
        // effba7776: "A minimal-repro crash bundle was written to: …/sharpy_run_…/.sharpy-crash/…"); other doors write
        // it under the source directory.
        combined.Should().NotContain("crash bundle", "a refused program writes no crash bundle");
        CrashBundles(_ws.Root).Should().BeEmpty("a refused program writes no crash bundle beside its source");
    }

    /// <summary>The instrument's positive control: the crash-bundle search finds a bundle planted where sharpyc writes one.</summary>
    [Fact]
    public void CrashBundleSearch_FindsAPlantedBundle()
    {
        Directory.CreateDirectory(_ws.PathFor(Path.Combine("bin", "Debug", "net10.0", ".sharpy-crash", "20261015-101155-079-67735")));
        CrashBundles(_ws.Root).Should().ContainSingle();
    }

    private static string[] CrashBundles(string root)
        => Directory.GetDirectories(root, ".sharpy-crash", SearchOption.AllDirectories)
            .SelectMany(Directory.GetDirectories)
            .ToArray();

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
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi)!;
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        process.WaitForExit(180_000).Should().BeTrue("the CLI process must not hang");
        process.WaitForExit();
        return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private sealed record ProcessResult(int ExitCode, string StdOut, string StdErr);
}
