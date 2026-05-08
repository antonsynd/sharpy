using Sharpy.Compiler.Services;
using Xunit;

namespace Sharpy.Compiler.Tests.Services;

/// <summary>
/// Smoke tests for <see cref="ReplSession"/> covering the core REPL contract:
/// bare expression auto-print, cumulative definitions, and error reporting.
/// </summary>
public class ReplSessionTests
{
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
    public async Task EvaluateAsync_ExplicitPrintCall_NotDoubleWrapped()
    {
        var session = new ReplSession();

        var result = await session.EvaluateAsync("print(\"hello\")");

        Assert.True(result.Success, FormatDiagnostics(result));
        Assert.Equal("hello" + Environment.NewLine, result.Output);
    }

    [Fact]
    public async Task EvaluateAsync_PriorDefinitions_RemainVisible()
    {
        var session = new ReplSession();

        var defResult = await session.EvaluateAsync("x: int = 42");
        Assert.True(defResult.Success, FormatDiagnostics(defResult));
        // Pure assignment produces no new output.
        Assert.Equal(string.Empty, defResult.Output);

        var useResult = await session.EvaluateAsync("x");
        Assert.True(useResult.Success, FormatDiagnostics(useResult));
        Assert.Equal("42" + Environment.NewLine, useResult.Output);
    }

    [Fact]
    public async Task EvaluateAsync_FunctionDefinitionThenCall_Works()
    {
        var session = new ReplSession();

        var defResult = await session.EvaluateAsync("def add(a: int, b: int) -> int:\n    return a + b\n");
        Assert.True(defResult.Success, FormatDiagnostics(defResult));

        var callResult = await session.EvaluateAsync("add(2, 3)");
        Assert.True(callResult.Success, FormatDiagnostics(callResult));
        Assert.Equal("5" + Environment.NewLine, callResult.Output);
    }

    [Fact]
    public async Task EvaluateAsync_SyntaxError_ReturnsFailureWithDiagnostics()
    {
        var session = new ReplSession();

        var result = await session.EvaluateAsync("def 1bad():");

        Assert.False(result.Success);
        Assert.NotEmpty(result.Diagnostics);
        // History should not advance on failure.
        Assert.Equal(0, session.EvaluationCount);
    }

    [Fact]
    public async Task EvaluateAsync_TypeError_ReturnsFailure()
    {
        var session = new ReplSession();

        var result = await session.EvaluateAsync("x: int = \"not an int\"");

        Assert.False(result.Success);
        Assert.NotEmpty(result.Diagnostics);
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

    [Fact]
    public async Task EvaluateAsync_NullInput_Throws()
    {
        var session = new ReplSession();

        await Assert.ThrowsAsync<ArgumentNullException>(() => session.EvaluateAsync(null!));
    }

    private static string FormatDiagnostics(ReplResult result)
        => result.Diagnostics.Count == 0
            ? "(no diagnostics)"
            : string.Join("\n", result.Diagnostics.Select(d => d.ToString()));
}
