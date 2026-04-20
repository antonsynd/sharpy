using System.Diagnostics;
using System.Text;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests;

/// <summary>
/// Tests that CancellationToken is properly threaded through all pipeline stages
/// and that cancellation terminates compilation promptly.
/// </summary>
public class CancellationTests
{
    private readonly ITestOutputHelper _output;

    public CancellationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Generate a moderately large Sharpy program that exercises multiple pipeline stages.
    /// </summary>
    private static string GenerateLargeProgram(int functionCount = 50, int statementsPerFunction = 20)
    {
        var sb = new StringBuilder();

        for (int f = 0; f < functionCount; f++)
        {
            sb.AppendLine($"def func_{f}(x: int) -> int:");
            for (int s = 0; s < statementsPerFunction; s++)
            {
                sb.AppendLine($"    v{s}: int = x + {s}");
            }
            sb.AppendLine($"    return v0 + {f}");
            sb.AppendLine();
        }

        sb.AppendLine("def main():");
        sb.AppendLine("    result: int = func_0(42)");
        sb.AppendLine("    print(result)");

        return sb.ToString();
    }

    [Fact]
    public void Compile_WithAlreadyCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var source = GenerateLargeProgram();
        var compiler = new Compiler();
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        var result = compiler.Compile(source, "test.spy", cts.Token);

        // The compiler catches OperationCanceledException and returns a failed result
        // with a CompilationCancelled diagnostic
        result.Success.Should().BeFalse();
        result.Diagnostics.GetAll().Should().Contain(d =>
            d.Code == Sharpy.Compiler.Diagnostics.DiagnosticCodes.Infrastructure.CompilationCancelled);
    }

    [Fact]
    [Trait("Category", "Benchmark")]
    public void Compile_WithAlreadyCancelledToken_ReturnsQuickly()
    {
        // Arrange
        var source = GenerateLargeProgram(functionCount: 200, statementsPerFunction: 50);
        var compiler = new Compiler();
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act
        var sw = Stopwatch.StartNew();
        var result = compiler.Compile(source, "test.spy", cts.Token);
        sw.Stop();

        // Assert - should complete very quickly since it's already cancelled
        _output.WriteLine($"Cancelled compilation took {sw.ElapsedMilliseconds}ms");
        result.Success.Should().BeFalse();
        sw.ElapsedMilliseconds.Should().BeLessThan(1000,
            "compilation with a pre-cancelled token should terminate promptly");
    }

    [Fact]
    [Trait("Category", "Benchmark")]
    public void Compile_WithDelayedCancellation_TerminatesPromptly()
    {
        // Arrange - generate a large program that takes nontrivial time to compile
        var source = GenerateLargeProgram(functionCount: 200, statementsPerFunction: 50);
        var compiler = new Compiler();
        var cancelDelayMs = 50;
        using var cts = new CancellationTokenSource(cancelDelayMs);

        // Act
        var sw = Stopwatch.StartNew();
        var result = compiler.Compile(source, "test.spy", cts.Token);
        sw.Stop();

        _output.WriteLine($"Compilation with {cancelDelayMs}ms cancellation took {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"Success: {result.Success}");

        // Assert - if cancellation was triggered, it should terminate reasonably soon.
        // Use a generous multiplier (40x) to avoid flaky failures from GC pauses,
        // JIT warmup, and OS scheduling jitter on CI runners. The point is to catch
        // hangs (multi-second), not benchmark cancellation latency.
        // If the compilation finishes before the cancellation fires, that's also acceptable.
        if (!result.Success)
        {
            // Cancellation was triggered - verify it terminated promptly
            sw.ElapsedMilliseconds.Should().BeLessThan(cancelDelayMs * 40,
                $"cancelled compilation should terminate within ~{cancelDelayMs * 40}ms");
            result.Diagnostics.GetAll().Should().Contain(d =>
                d.Code == Sharpy.Compiler.Diagnostics.DiagnosticCodes.Infrastructure.CompilationCancelled);
        }
        else
        {
            // Compilation finished before cancellation - that's fine for small programs
            _output.WriteLine("Compilation completed before cancellation fired");
        }
    }

    [Fact]
    public void Compile_WithoutCancellation_CompletesSuccessfully()
    {
        // Sanity check: the same program compiles successfully without cancellation
        var source = GenerateLargeProgram();
        var compiler = new Compiler();

        var result = compiler.Compile(source, "test.spy", CancellationToken.None);

        result.Success.Should().BeTrue(
            string.Join("; ", result.Diagnostics.GetAll().Where(d => d.IsError).Select(d => d.Message)));
    }

    [Fact]
    public void Compile_SmallProgram_WithAlreadyCancelledToken_ReturnsCancelledResult()
    {
        // Even a trivial program should respect cancellation
        var source = "print(42)";
        var compiler = new Compiler();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = compiler.Compile(source, "test.spy", cts.Token);

        result.Success.Should().BeFalse();
        result.Diagnostics.GetAll().Should().Contain(d =>
            d.Code == Sharpy.Compiler.Diagnostics.DiagnosticCodes.Infrastructure.CompilationCancelled);
    }
}
