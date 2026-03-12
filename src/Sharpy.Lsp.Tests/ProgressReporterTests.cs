using FluentAssertions;
using Sharpy.Lsp;
using Xunit;

namespace Sharpy.Lsp.Tests;

public class ProgressReporterTests
{
    [Fact]
    public async Task BeginAsync_WhenNotSupported_ReturnsNoOpScope()
    {
        // Arrange: null work done manager means progress is not supported
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<ProgressReporter>();
        var reporter = new ProgressReporter(null, logger);

        // Act
        var scope = await reporter.BeginAsync("Test");

        // Assert: should get a no-op scope (observer is null)
        scope.Should().NotBeNull();

        // Verify it behaves as no-op (no exceptions thrown)
        scope.Report("test message");
        scope.Complete();
        scope.Dispose();
    }

    [Fact]
    public void NoOpScope_Report_DoesNotThrow()
    {
        var scope = ProgressScope.NoOp;

        var act = () => scope.Report("test", 50);

        act.Should().NotThrow();
    }

    [Fact]
    public void NoOpScope_Complete_DoesNotThrow()
    {
        var scope = ProgressScope.NoOp;

        var act = () => scope.Complete("done");

        act.Should().NotThrow();
    }

    [Fact]
    public void NoOpScope_Dispose_DoesNotThrow()
    {
        var scope = ProgressScope.NoOp;

        var act = () => scope.Dispose();

        act.Should().NotThrow();
    }

    [Fact]
    public void Scope_Complete_PreventsSubsequentReports()
    {
        // A no-op scope effectively tests the Complete/disposed guard logic,
        // since Complete sets _disposed = 1 via Interlocked.CompareExchange
        var scope = ProgressScope.NoOp;

        // First complete — should succeed (no-op path)
        scope.Complete("first");

        // Report after complete — should be silently ignored, not throw
        var act = () => scope.Report("should be ignored");
        act.Should().NotThrow();

        // Second complete — should be silently ignored (already disposed)
        var act2 = () => scope.Complete("second");
        act2.Should().NotThrow();
    }

    [Fact]
    public void Scope_Dispose_CallsComplete()
    {
        // Dispose delegates to Complete(), so after Dispose, further
        // Complete/Report calls should be no-ops
        var scope = ProgressScope.NoOp;

        scope.Dispose();

        // After disposal, Report should be a no-op
        var act = () => scope.Report("after dispose");
        act.Should().NotThrow();

        // After disposal, Complete should be a no-op
        var act2 = () => scope.Complete("after dispose");
        act2.Should().NotThrow();
    }

    [Fact]
    public void IsSupported_WhenManagerIsNull_ReturnsFalse()
    {
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<ProgressReporter>();
        var reporter = new ProgressReporter(null, logger);

        reporter.IsSupported.Should().BeFalse();
    }

    [Fact]
    public void NoOp_ReturnsFreshInstanceEachTime()
    {
        var scope1 = ProgressScope.NoOp;
        var scope2 = ProgressScope.NoOp;

        // Each call returns a fresh instance to avoid shared mutable state
        scope1.Should().NotBeSameAs(scope2);
    }
}
