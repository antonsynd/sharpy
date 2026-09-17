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
    /// A program that survives semantic analysis and is refused by ROSLYN — the only shape that can
    /// reach the C#-compile stage, and so the only positive control this harness contract can have.
    ///
    /// <para><b>#1868 is the reason this source is what it is.</b> <c>list(x)</c> types its result
    /// from the ELEMENT type of its argument; when the argument is not iterable there is no element
    /// type and the collection constructor falls back to <c>Unknown</c> type arguments rather than
    /// refusing. <c>Unknown</c> is assignable to anything, so the program type-checks clean and the
    /// emitted <c>List&lt;&gt;</c> reaches Roslyn with no type argument. Through <c>sharpyc run</c>:
    /// <c>error[SPY0908]: internal error: generated C# failed to compile (CS0305: Using the generic
    /// type 'List&lt;T&gt;' requires 1 type arguments)</c>.</para>
    ///
    /// <para><b>This control has to be re-based whenever its ICE is fixed</b>, as it has been twice
    /// already. It was <c>a: Animal = Dog() if c else "x"</c> (CS0029) until R-W arm 1 turned that
    /// into a semantic SPY0220 (#1677/#1743); then <c>assert b"hello" as? long</c> (CS8121) until
    /// #1713 made a statically-impossible coercion a semantic-time SPY0610 refusal — that program no
    /// longer reaches the C#-compile stage. The harness contract below is unchanged; only the input
    /// that reaches it moved. When #1868 is fixed (<c>list(x)</c> on a non-iterable becomes a
    /// semantic refusal), re-base onto whatever SPY0908 gap is still real then; any remaining one
    /// will do.</para>
    /// </summary>
    private const string RoslynFailingSource = """
        def main() -> None:
            x: int = 42
            items = list(x)
            print(items)
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
        Assert.Contains("CS0305", string.Join("\n", result.CompilationErrors));

        // … and the fact must also be recorded as a diagnostic, or the suite's
        // "no SPY0908" assertions cannot go red.
        var ice = Assert.Single(
            result.RawDiagnostics,
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError);

        Assert.Equal(CompilerDiagnosticSeverity.Error, ice.Severity);
        Assert.Equal(CompilerPhase.Assembly, ice.Phase);
        // Mapped through AssemblyCompiler, so the harness and the CLI carry the same message
        // shape and the original Roslyn id is not lost.
        Assert.Contains("CS0305", ice.Message);
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
            Assert.Contains("CS0305", string.Join("\n", result.CompilationErrors));

            var ice = Assert.Single(
                result.RawDiagnostics,
                d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError);

            Assert.Equal(CompilerDiagnosticSeverity.Error, ice.Severity);
            Assert.Equal(CompilerPhase.Assembly, ice.Phase);
            Assert.Contains("CS0305", ice.Message);
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
