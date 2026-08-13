using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// The fixture harness's own tests (#1457).
///
/// <para>Every file-based arm funnels through <c>AssertFixtureOutcome</c>, so what that method
/// can and cannot express is what ~724 <c>.error</c> fixtures can and cannot assert. Before
/// <c>!</c> lines it could only say "this substring appears somewhere in the diagnostics": a
/// fixture emitting the expected error <em>plus three unrelated ones</em> was indistinguishable
/// from one emitting only the expected error, and 91 of the single-line sidecars pin 20
/// characters or fewer (the weakest being the 4-character <c>type</c>).</para>
///
/// <para>These tests drive the harness directly with synthetic results rather than through real
/// fixtures, because the interesting cases are the ones that must FAIL — which no fixture in a
/// green suite can demonstrate. Each new form is checked in both directions: it passes when it
/// should, and it fails when the thing it guards against actually happens. A guard only verified
/// in its passing direction is the defect this whole class of work exists to remove.</para>
/// </summary>
public class FixtureExpectationSyntaxTests : FileBasedIntegrationTestsBase, IDisposable
{
    private readonly string _fixturesDir;

    protected override string FixturesPath => _fixturesDir;

    public FixtureExpectationSyntaxTests(ITestOutputHelper output) : base(output)
    {
        _fixturesDir = Path.Combine(Path.GetTempPath(), $"sharpy_expect_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_fixturesDir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_fixturesDir, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp directory is not worth failing a test over.
        }

        GC.SuppressFinalize(this);
    }

    // ── the two new forms, both directions ───────────────────────────────────────────────

    [Fact]
    public void NegativeLine_Passes_WhenTheNamedDiagnosticIsAbsent()
        => AssertErrorFixture("SPY0220: cannot convert\n!SPY0483", Failed("SPY0220: cannot convert"));

    [Fact]
    public void NegativeLine_Fails_WhenTheNamedDiagnosticIsPresent()
    {
        var thrown = Assert.ThrowsAny<XunitException>(() =>
            AssertErrorFixture(
                "SPY0220: cannot convert\n!SPY0483",
                Failed("SPY0220: cannot convert", "SPY0483: builtin shadowed")));

        Assert.Contains("SPY0483", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CountLine_Passes_OnTheExactCount()
        => AssertErrorFixture("SPY0220: cannot convert\n!count 1", Failed("SPY0220: cannot convert"));

    [Fact]
    public void CountLine_Fails_WhenAnExtraDiagnosticAppears()
    {
        // The case #1457 was filed for. The positive line still matches — the expected error IS
        // there — so the sidecar was green before this change even though the compiler emitted a
        // second, unrelated diagnostic.
        var thrown = Assert.ThrowsAny<XunitException>(() =>
            AssertErrorFixture(
                "SPY0220: cannot convert\n!count 1",
                Failed("SPY0220: cannot convert", "SPY0401: unrelated cascade")));

        Assert.Contains("Expected exactly 1 error diagnostic(s), got 2", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CountLine_Fails_WhenTooFewDiagnosticsAppear()
    {
        var thrown = Assert.ThrowsAny<XunitException>(() =>
            AssertErrorFixture(
                "SPY0220: cannot convert\n!count 2",
                Failed("SPY0220: cannot convert")));

        Assert.Contains("Expected exactly 2 error diagnostic(s), got 1", thrown.Message, StringComparison.Ordinal);
    }

    // ── what the old format could not catch, stated as a test ────────────────────────────

    [Fact]
    public void AWeakPositivePattern_AloneCannotTellTheseApart_ButTheNewFormsCan()
    {
        // Both of these satisfy the historical sidecar `type`: one is the diagnostic the fixture
        // meant to pin, the other is an unrelated error that merely contains the word.
        AssertErrorFixture("type", Failed("SPY0220: cannot convert int to type str"));
        AssertErrorFixture("type", Failed("SPY0999: internal error while typechecking prototype"));

        // Adding what the fixture actually meant makes the second one fail.
        Assert.ThrowsAny<XunitException>(() =>
            AssertErrorFixture("type\n!count 1\n!internal error",
                Failed("SPY0999: internal error while typechecking prototype")));
    }

    // ── the historical behaviour is untouched ────────────────────────────────────────────

    [Fact]
    public void PositiveLine_StillMatchesASubstring()
        => AssertErrorFixture("cannot convert", Failed("SPY0220: cannot convert int to str"));

    [Fact]
    public void PositiveLine_StillFailsWhenTheDiagnosticIsMissing()
        => Assert.ThrowsAny<XunitException>(() =>
            AssertErrorFixture("cannot convert", Failed("SPY0401: something else entirely")));

    [Fact]
    public void CommentsAndBlankLinesAreStillIgnored()
        => AssertErrorFixture("# a comment\n\ncannot convert\n", Failed("SPY0220: cannot convert"));

    [Fact]
    public void ASucceedingCompilationStillFailsAnErrorFixture()
        => Assert.ThrowsAny<XunitException>(() =>
            AssertErrorFixture("!SPY0483", Succeeded()));

    // ── malformed sidecars fail on the sidecar, not on the compiler ──────────────────────

    [Fact]
    public void ABareBangFailsWithGuidance()
    {
        var thrown = Assert.ThrowsAny<XunitException>(() =>
            AssertErrorFixture("SPY0220: cannot convert\n!", Failed("SPY0220: cannot convert")));

        Assert.Contains("bare '!' line", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CountIsRejectedInARuntimeErrorSidecar()
    {
        var runtimeErrorFile = Path.Combine(_fixturesDir, "probe.runtime-error");
        File.WriteAllText(runtimeErrorFile, "!count 1\n");

        var thrown = Assert.ThrowsAny<XunitException>(() => AssertFixtureOutcome(
            new ExecutionResult
            {
                Success = false,
                GeneratedCSharp = "// generated",
                StandardError = "IndexError: list index out of range",
            },
            errorFilePath: Path.Combine(_fixturesDir, "probe.error"),
            expectedFilePath: Path.Combine(_fixturesDir, "probe.expected"),
            runtimeErrorFilePath: runtimeErrorFile,
            snapshotFilePath: Path.Combine(_fixturesDir, "probe.expected.cs"),
            sourceTextContent: null,
            verifyCSharpSnapshot: false));

        Assert.Contains("only meaningful in a .error sidecar", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NegativeLinesWorkInARuntimeErrorSidecarToo()
    {
        var runtimeErrorFile = Path.Combine(_fixturesDir, "probe.runtime-error");
        File.WriteAllText(runtimeErrorFile, "IndexError\n!KeyError\n");

        AssertFixtureOutcome(
            new ExecutionResult
            {
                Success = false,
                GeneratedCSharp = "// generated",
                StandardError = "IndexError: list index out of range",
            },
            errorFilePath: Path.Combine(_fixturesDir, "probe.error"),
            expectedFilePath: Path.Combine(_fixturesDir, "probe.expected"),
            runtimeErrorFilePath: runtimeErrorFile,
            snapshotFilePath: Path.Combine(_fixturesDir, "probe.expected.cs"),
            sourceTextContent: null,
            verifyCSharpSnapshot: false);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────

    private void AssertErrorFixture(string sidecarContent, ExecutionResult result)
    {
        var errorFile = Path.Combine(_fixturesDir, "probe.error");
        File.WriteAllText(errorFile, sidecarContent);

        AssertFixtureOutcome(
            result,
            errorFilePath: errorFile,
            expectedFilePath: Path.Combine(_fixturesDir, "probe.expected"),
            runtimeErrorFilePath: Path.Combine(_fixturesDir, "probe.missing-runtime-error"),
            snapshotFilePath: Path.Combine(_fixturesDir, "probe.expected.cs"),
            sourceTextContent: "def main():\n    pass\n",
            verifyCSharpSnapshot: false);
    }

    private static ExecutionResult Failed(params string[] errors) => new()
    {
        Success = false,
        CompilationErrors = errors.ToList(),
    };

    private static ExecutionResult Succeeded() => new()
    {
        Success = true,
        StandardOutput = string.Empty,
        GeneratedCSharp = "// generated",
    };
}
