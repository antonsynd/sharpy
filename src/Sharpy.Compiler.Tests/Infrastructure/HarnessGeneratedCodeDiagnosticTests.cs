using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Infrastructure;

/// <summary>
/// Holds the harness contract that roughly twenty test files depend on: when the generated C#
/// fails to compile, <c>ExecutionResult.RawDiagnostics</c> records that failure as SPY0908, exactly
/// as <c>sharpyc run</c> reports it.
///
/// <para>
/// The harness runs the Sharpy pipeline through <see cref="CompilerApi"/> and then emits the
/// generated C# itself, so it never reaches <c>AssemblyCompiler</c>, the only producer of SPY0908.
/// Until the emit-failure paths mapped their Roslyn errors, they returned a result whose
/// <c>RawDiagnostics</c> was its empty default — which made every assertion of the form
/// <c>RawDiagnostics.Should().NotContain(d =&gt; d.Code == GeneratedCodeCompilationError)</c> pass
/// for precisely the condition it names. The ICE those assertions forbid was the one input that
/// guaranteed them green.
/// </para>
///
/// <para>
/// Both halves are required by the falsifiability contract (verification-contract.md §2): the
/// positive control shows the probe hits on an input where the generated C# really does fail, and
/// the clean-run case shows the same probe stays silent otherwise, so a seam that unconditionally
/// stuffed SPY0908 into every result could not pass both.
/// </para>
/// </summary>
public class HarnessGeneratedCodeDiagnosticTests : IntegrationTestBase
{
    public HarnessGeneratedCodeDiagnosticTests(ITestOutputHelper output) : base(output)
    {
    }

    /// <summary>
    /// A ternary whose branches are <c>Dog</c> and <c>str</c> under an <c>Animal</c> slot survives
    /// semantic analysis at this commit and is refused by Roslyn with CS0029 — i.e. it reaches the
    /// C#-compile stage, which is the only stage that can produce SPY0908. Through
    /// <c>sharpyc run</c> the same source reports
    /// <c>error[SPY0908]: internal error: generated C# failed to compile (CS0029: Cannot implicitly
    /// convert type 'string' to '…Animal')</c>.
    /// </summary>
    private const string RoslynFailingSource = """
        class Animal:
            pass

        class Dog(Animal):
            pass

        def main() -> None:
            c: bool = True
            a: Animal = Dog() if c else "x"
            print(a)
        """;

    private const string CleanSource = """
        def main() -> None:
            print(42)
        """;

    [Fact]
    public void SingleFileArm_CSharpCompileFailure_RecordsSpy0908InRawDiagnostics()
    {
        var result = CompileAndExecute(RoslynFailingSource);

        Assert.False(result.Success);

        // The C# error text must survive on CompilationErrors (unchanged contract) …
        Assert.Contains("CS0029", string.Join("\n", result.CompilationErrors));

        // … and the fact must also be recorded as a diagnostic, or the suite's
        // "no SPY0908" assertions cannot go red.
        var ice = Assert.Single(
            result.RawDiagnostics,
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError);

        Assert.Equal(CompilerDiagnosticSeverity.Error, ice.Severity);
        Assert.Equal(CompilerPhase.Assembly, ice.Phase);
        // Mapped through AssemblyCompiler, so the harness and the CLI carry the same message
        // shape and the original Roslyn id is not lost.
        Assert.Contains("CS0029", ice.Message);
        Assert.Contains("generated C# failed to compile", ice.Message);
    }

    [Fact]
    public void ProjectArm_CSharpCompileFailure_RecordsSpy0908InRawDiagnostics()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"sharpy_harness_spy0908_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var entry = Path.Combine(dir, "main.spy");
            File.WriteAllText(entry, RoslynFailingSource);

            var result = CompileAndExecuteEntryFile(entry);

            Assert.False(result.Success);
            Assert.Contains("CS0029", string.Join("\n", result.CompilationErrors));

            var ice = Assert.Single(
                result.RawDiagnostics,
                d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError);

            Assert.Equal(CompilerDiagnosticSeverity.Error, ice.Severity);
            Assert.Equal(CompilerPhase.Assembly, ice.Phase);
            Assert.Contains("CS0029", ice.Message);
        }
        finally
        {
            try
            { Directory.Delete(dir, recursive: true); }
            catch { }
        }
    }

    /// <summary>
    /// The other direction: a program whose generated C# compiles records no SPY0908, so the
    /// positive controls above are detecting the failure rather than a seam that reports the ICE
    /// unconditionally.
    /// </summary>
    [Fact]
    public void SingleFileArm_CleanProgram_RecordsNoSpy0908()
    {
        var result = CompileAndExecute(CleanSource);

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("42\n", result.StandardOutput.Replace("\r\n", "\n"));
        Assert.DoesNotContain(
            result.RawDiagnostics,
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError);
    }
}
