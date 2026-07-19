using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Services;
using Sharpy.Compiler.Shared;
using Xunit;

namespace Sharpy.Compiler.Tests.Services;

/// <summary>
/// Unit tests for <see cref="ReplSession"/> covering the core REPL contract:
/// bare expression auto-print, cumulative definitions, error reporting, and
/// recovery semantics.
/// </summary>
public class ReplSessionTests
{
    // -------------------------------------------------------------------------
    // Expression evaluation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EvaluateAsync_BareIntegerExpression_PrintsValue()
    {
        var session = new ReplSession();

        var result = await session.EvaluateAsync("1 + 2");

        Assert.True(result.Success, FormatDiagnostics(result));
        Assert.Equal("3" + Environment.NewLine, result.Output);
        Assert.Equal(1, session.EvaluationCount);
    }

    [Fact]
    public async Task EvaluateAsync_BareStringExpression_PrintsValue()
    {
        var session = new ReplSession();

        var result = await session.EvaluateAsync("\"hello\" + \" \" + \"world\"");

        Assert.True(result.Success, FormatDiagnostics(result));
        Assert.Equal("hello world" + Environment.NewLine, result.Output);
    }

    [Fact]
    public async Task EvaluateAsync_BooleanExpression_PrintsValue()
    {
        var session = new ReplSession();

        var result = await session.EvaluateAsync("1 < 2");

        Assert.True(result.Success, FormatDiagnostics(result));
        Assert.Equal("True" + Environment.NewLine, result.Output);
    }

    [Fact]
    public async Task EvaluateAsync_ExplicitPrintCall_NotDoubleWrapped()
    {
        var session = new ReplSession();

        var result = await session.EvaluateAsync("print(\"hello\")");

        Assert.True(result.Success, FormatDiagnostics(result));
        Assert.Equal("hello" + Environment.NewLine, result.Output);
    }

    // -------------------------------------------------------------------------
    // Variables and cumulative state
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EvaluateAsync_PriorTypedVariable_RemainsVisible()
    {
        var session = new ReplSession();

        var defResult = await session.EvaluateAsync("x: int = 42");
        Assert.True(defResult.Success, FormatDiagnostics(defResult));
        // Pure typed-variable declaration produces no new output.
        Assert.Equal(string.Empty, defResult.Output);

        var useResult = await session.EvaluateAsync("x");
        Assert.True(useResult.Success, FormatDiagnostics(useResult));
        Assert.Equal("42" + Environment.NewLine, useResult.Output);
    }

    [Fact]
    public async Task EvaluateAsync_MultipleEvaluations_AccumulatesHistory()
    {
        var session = new ReplSession();

        await session.EvaluateAsync("a: int = 1");
        await session.EvaluateAsync("b: int = 2");
        var sumResult = await session.EvaluateAsync("a + b");

        Assert.True(sumResult.Success, FormatDiagnostics(sumResult));
        Assert.Equal("3" + Environment.NewLine, sumResult.Output);
        Assert.Equal(3, session.EvaluationCount);
    }

    // -------------------------------------------------------------------------
    // Function definitions
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EvaluateAsync_FunctionDefinitionThenCall_Works()
    {
        var session = new ReplSession();

        var defResult = await session.EvaluateAsync(
            "def add(a: int, b: int) -> int:\n    return a + b\n");
        Assert.True(defResult.Success, FormatDiagnostics(defResult));
        // Defining a function emits no runtime output.
        Assert.Equal(string.Empty, defResult.Output);

        var callResult = await session.EvaluateAsync("add(2, 3)");
        Assert.True(callResult.Success, FormatDiagnostics(callResult));
        Assert.Equal("5" + Environment.NewLine, callResult.Output);
    }

    [Fact]
    public async Task EvaluateAsync_RecursiveFunction_Works()
    {
        var session = new ReplSession();

        var defResult = await session.EvaluateAsync(
            "def fact(n: int) -> int:\n" +
            "    if n <= 1:\n" +
            "        return 1\n" +
            "    return n * fact(n - 1)\n");
        Assert.True(defResult.Success, FormatDiagnostics(defResult));

        var callResult = await session.EvaluateAsync("fact(5)");
        Assert.True(callResult.Success, FormatDiagnostics(callResult));
        Assert.Equal("120" + Environment.NewLine, callResult.Output);
    }

    // -------------------------------------------------------------------------
    // Class definitions
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EvaluateAsync_ClassDefinitionThenInstantiation_Works()
    {
        var session = new ReplSession();

        var classResult = await session.EvaluateAsync(
            "class Point:\n" +
            "    x: int\n" +
            "    y: int\n" +
            "\n" +
            "    def __init__(self, x: int, y: int):\n" +
            "        self.x = x\n" +
            "        self.y = y\n" +
            "\n" +
            "    def sum(self) -> int:\n" +
            "        return self.x + self.y\n");
        Assert.True(classResult.Success, FormatDiagnostics(classResult));

        var useResult = await session.EvaluateAsync("Point(3, 4).sum()");
        Assert.True(useResult.Success, FormatDiagnostics(useResult));
        Assert.Equal("7" + Environment.NewLine, useResult.Output);
    }

    // -------------------------------------------------------------------------
    // Imports
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EvaluateAsync_FromImport_ExposesSymbol()
    {
        var session = new ReplSession();

        var importResult = await session.EvaluateAsync("from math import sqrt");
        Assert.True(importResult.Success, FormatDiagnostics(importResult));

        var useResult = await session.EvaluateAsync("sqrt(16.0)");
        Assert.True(useResult.Success, FormatDiagnostics(useResult));
        Assert.Equal("4.0" + Environment.NewLine, useResult.Output);
    }

    // -------------------------------------------------------------------------
    // Experimental feature gating (#1045)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EvaluateAsync_GatedSyntaxWithoutFeature_ReportsSpy0331()
    {
        var session = new ReplSession();

        // `defer` is parsed but gated; without --enable-feature the REPL must reject it
        // with SPY0331 like the batch pipelines, not silently accept it.
        var result = await session.EvaluateAsync(
            "def f() -> None:\n    defer print(\"bye\")\n    print(\"hi\")\n");

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == "SPY0331");
        Assert.Equal(0, session.EvaluationCount);
    }

    [Fact]
    public async Task EvaluateAsync_GatedSyntaxWithFeature_Compiles()
    {
        var session = new ReplSession(
            logger: null,
            features: FeatureFlags.None.Enable(new[] { "defer" }));

        // f returns a value because the REPL auto-prints bare call expressions; a
        // None-returning bare call would hit the pre-existing print(void) limitation (#1059).
        var defResult = await session.EvaluateAsync(
            "def f() -> int:\n"
            + "    defer print(\"bye\")\n"
            + "    print(\"hi\")\n"
            + "    return 1\n");
        Assert.True(defResult.Success, FormatDiagnostics(defResult));

        var callResult = await session.EvaluateAsync("f()");
        Assert.True(callResult.Success, FormatDiagnostics(callResult));
        // Deferred body runs at f's scope exit (after "hi", before the value is printed).
        Assert.Equal(
            "hi" + Environment.NewLine + "bye" + Environment.NewLine + "1" + Environment.NewLine,
            callResult.Output);
    }

    // -------------------------------------------------------------------------
    // Error handling
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EvaluateAsync_SyntaxError_ReturnsFailureWithDiagnostics()
    {
        var session = new ReplSession();

        var result = await session.EvaluateAsync("def 1bad():");

        Assert.False(result.Success);
        Assert.NotEmpty(result.Diagnostics);
        Assert.Contains(result.Diagnostics, d => d.Severity == CompilerDiagnosticSeverity.Error);
        // History should not advance on failure.
        Assert.Equal(0, session.EvaluationCount);
    }

    [Fact]
    public async Task EvaluateAsync_TypeError_ReturnsFailureWithErrorDiagnostic()
    {
        var session = new ReplSession();

        var result = await session.EvaluateAsync("x: int = \"not an int\"");

        Assert.False(result.Success);
        Assert.NotEmpty(result.Diagnostics);
        Assert.Contains(result.Diagnostics, d => d.Severity == CompilerDiagnosticSeverity.Error);
        // Diagnostic codes should be populated (e.g., SPY02xx for semantic errors).
        Assert.Contains(result.Diagnostics, d => !string.IsNullOrEmpty(d.Code));
    }

    [Fact]
    public async Task EvaluateAsync_UndefinedIdentifier_ReturnsFailure()
    {
        var session = new ReplSession();

        var result = await session.EvaluateAsync("undefined_var + 1");

        Assert.False(result.Success);
        Assert.NotEmpty(result.Diagnostics);
    }

    // -------------------------------------------------------------------------
    // Recovery semantics
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EvaluateAsync_ErrorAfterValidEval_DoesNotPolluteHistory()
    {
        var session = new ReplSession();

        // Establish a known-good baseline.
        var setup = await session.EvaluateAsync("y: int = 10");
        Assert.True(setup.Success, FormatDiagnostics(setup));
        Assert.Equal(1, session.EvaluationCount);

        // A failing evaluation must not advance the counter or poison history.
        var bad = await session.EvaluateAsync("y: str = 5  # type mismatch");
        Assert.False(bad.Success);
        Assert.Equal(1, session.EvaluationCount);

        // The earlier definition is still usable.
        var recovery = await session.EvaluateAsync("y * 2");
        Assert.True(recovery.Success, FormatDiagnostics(recovery));
        Assert.Equal("20" + Environment.NewLine, recovery.Output);
        Assert.Equal(2, session.EvaluationCount);
    }

    [Fact]
    public async Task EvaluateAsync_SyntaxErrorRecovery_NextValidInputSucceeds()
    {
        var session = new ReplSession();

        var bad = await session.EvaluateAsync("def @@@");
        Assert.False(bad.Success);

        var good = await session.EvaluateAsync("1 + 1");
        Assert.True(good.Success, FormatDiagnostics(good));
        Assert.Equal("2" + Environment.NewLine, good.Output);
    }

    // -------------------------------------------------------------------------
    // SPY0908 net: generated-C# emit failures never leak a raw CS code (#1059)
    // -------------------------------------------------------------------------

    [Fact]
    public void CompileCSharp_InvalidGeneratedCode_ReportsSpy0908_NeverRawCsCode()
    {
        // Deliberately invalid C# stands in for a compiler bug that emits code the final
        // Roslyn emit rejects. Every surfaced diagnostic must be SPY0908 — never a bare
        // CSxxxx code the user cannot act on.
        var (assembly, diagnostics) = ReplSession.CompileCSharp("class {", CancellationToken.None);

        Assert.Null(assembly);
        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, d => Assert.Equal("SPY0908", d.Code));
        Assert.DoesNotContain(diagnostics,
            d => d.Code is not null && d.Code.StartsWith("CS", StringComparison.Ordinal));
    }

    // -------------------------------------------------------------------------
    // Accumulated source / introspection
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAccumulatedSource_ContainsDefinitionsAndStatements()
    {
        var session = new ReplSession();

        await session.EvaluateAsync("def square(n: int) -> int:\n    return n * n\n");
        await session.EvaluateAsync("square(5)");

        var src = session.GetAccumulatedSource();

        // Definitions appear at module level...
        Assert.Contains("def square", src);
        // ...and the statement appears under a synthesized main().
        Assert.Contains("def main", src);
    }

    [Fact]
    public async Task EvaluateAsync_NullInput_Throws()
    {
        var session = new ReplSession();

        await Assert.ThrowsAsync<ArgumentNullException>(() => session.EvaluateAsync(null!));
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static string FormatDiagnostics(ReplResult result)
        => result.Diagnostics.Count == 0
            ? "(no diagnostics)"
            : string.Join("\n", result.Diagnostics.Select(d => d.ToString()));
}
