using FluentAssertions;
using Sharpy.Compiler.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// End-to-end execution tests for the <c>defer</c> pilot feature (#1023): the statement is
/// gated behind <c>&lt;Features&gt;defer&lt;/Features&gt;</c>, and once enabled its deferred
/// bodies run on every exit path in reverse declaration order (LIFO), lowered to try/finally.
/// </summary>
public class DeferExecutionTests
{
    private readonly ITestOutputHelper _output;

    public DeferExecutionTests(ITestOutputHelper output) => _output = output;

    private static ProjectCompilationHelper NewHelper(ITestOutputHelper output, string ns, bool enableDefer)
    {
        var helper = new ProjectCompilationHelper(output);
        helper.WithRootNamespace(ns).WithEntryPoint("main.spy").WithOutputType("exe");
        if (enableDefer)
            helper.Options.Features.Add("defer");
        return helper;
    }

    [Fact]
    public void Defer_WithoutFeature_ReportsSpy0331()
    {
        using var helper = NewHelper(_output, "DeferGated", enableDefer: false);
        helper.AddSourceFile("main.spy",
            "def main() -> None:\n    defer print(\"bye\")\n    print(\"hi\")\n", isEntryPoint: true);

        var result = helper.Compile();

        result.Success.Should().BeFalse();
        result.Diagnostics.GetErrors()
            .Should().Contain(d => d.Message.Contains("requires experimental feature 'defer'"));
    }

    [Fact]
    public void Defer_RunsOnFallThrough_InReverseOrder()
    {
        using var helper = NewHelper(_output, "DeferFallThrough", enableDefer: true);
        helper.AddSourceFile("main.spy",
            "def main() -> None:\n"
            + "    defer print(\"cleanup 1\")\n"
            + "    defer print(\"cleanup 2\")\n"
            + "    print(\"body\")\n", isEntryPoint: true);

        var result = helper.CompileAndExecute();

        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        // LIFO: cleanup 2 (declared last) runs before cleanup 1.
        result.StandardOutput.Should().Be("body\ncleanup 2\ncleanup 1\n");
    }

    [Fact]
    public void Defer_RunsOnEarlyReturn()
    {
        using var helper = NewHelper(_output, "DeferEarlyReturn", enableDefer: true);
        helper.AddSourceFile("main.spy",
            "def demo() -> None:\n"
            + "    defer print(\"cleanup\")\n"
            + "    print(\"before return\")\n"
            + "    return\n"
            + "    print(\"unreachable\")\n"
            + "\n"
            + "def main() -> None:\n"
            + "    demo()\n", isEntryPoint: true);

        var result = helper.CompileAndExecute();

        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Should().Be("before return\ncleanup\n");
    }

    [Fact]
    public void Defer_RunsOnExceptionUnwind()
    {
        using var helper = NewHelper(_output, "DeferException", enableDefer: true);
        helper.AddSourceFile("main.spy",
            "def risky() -> None:\n"
            + "    defer print(\"cleanup\")\n"
            + "    print(\"before raise\")\n"
            + "    raise ValueError(\"boom\")\n"
            + "\n"
            + "def main() -> None:\n"
            + "    try:\n"
            + "        risky()\n"
            + "    except ValueError:\n"
            + "        print(\"caught\")\n", isEntryPoint: true);

        var result = helper.CompileAndExecute();

        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Should().Be("before raise\ncleanup\ncaught\n");
    }

    [Fact]
    public void Defer_BlockForm_RunsAtScopeExit()
    {
        using var helper = NewHelper(_output, "DeferBlock", enableDefer: true);
        helper.AddSourceFile("main.spy",
            "def main() -> None:\n"
            + "    defer:\n"
            + "        print(\"cleanup a\")\n"
            + "        print(\"cleanup b\")\n"
            + "    print(\"body\")\n", isEntryPoint: true);

        var result = helper.CompileAndExecute();

        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        result.StandardOutput.Should().Be("body\ncleanup a\ncleanup b\n");
    }

    [Fact]
    public void Defer_AttachesToNearestEnclosingBlock_InLoop()
    {
        using var helper = NewHelper(_output, "DeferLoop", enableDefer: true);
        helper.AddSourceFile("main.spy",
            "def main() -> None:\n"
            + "    for i in range(2):\n"
            + "        defer print(\"loop cleanup\", i)\n"
            + "        print(\"iter\", i)\n", isEntryPoint: true);

        var result = helper.CompileAndExecute();

        result.Success.Should().BeTrue(string.Join("\n", result.CompilationErrors));
        // The defer's cleanup runs at the end of each loop-body iteration, not once at the end.
        result.StandardOutput.Should().Be("iter 0\nloop cleanup 0\niter 1\nloop cleanup 1\n");
    }

    [Fact]
    public void Defer_ReturnInsideDeferBody_ReportsSpy0333()
    {
        using var helper = NewHelper(_output, "DeferReturnEscape", enableDefer: true);
        helper.AddSourceFile("main.spy",
            "def main() -> None:\n    defer:\n        return\n    print(\"x\")\n", isEntryPoint: true);

        var result = helper.Compile();

        result.Success.Should().BeFalse();
        result.Diagnostics.GetErrors()
            .Should().Contain(d => d.Code == "SPY0333");
    }
}
