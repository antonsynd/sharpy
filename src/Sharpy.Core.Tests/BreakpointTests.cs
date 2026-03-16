using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class Breakpoint_Tests
{
    [Fact]
    public void Breakpoint_WhenNoDebuggerAttached_DoesNotThrow()
    {
        // Smoke test: calling Breakpoint() without a debugger should be a no-op
        FluentActions.Invoking(() => Breakpoint()).Should().NotThrow();
    }
}
