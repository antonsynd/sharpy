using FluentAssertions;
using Sharpy.Compiler.Logging;
using Xunit;

namespace Sharpy.Compiler.Tests.Logging;

public class CompilerStructuredLoggingTests
{
    // Valid Sharpy program with main() function
    private const string ValidProgram = @"
def main():
    print(42)
";

    private const string ValidProgramWithVar = @"
def main():
    x: int = 1
    print(x)
";

    [Fact]
    public void Compile_EmitsPhaseEvents()
    {
        var logger = new StructuredLogger();
        var compiler = new Compiler(new CompilerOptions(), logger);

        var result = compiler.Compile(ValidProgram, "test.spy");

        result.Success.Should().BeTrue();

        // Check that we got phase start and end events for each phase
        var startEvents = logger.GetEvents<PhaseStartEvent>().ToList();
        var endEvents = logger.GetEvents<PhaseEndEvent>().ToList();

        startEvents.Should().HaveCount(endEvents.Count, "every phase should have start and end events");
        startEvents.Should().HaveCountGreaterThanOrEqualTo(4, "should have at least 4 phases");

        // Verify expected phases exist
        startEvents.Select(e => e.Phase).Should().Contain("Lexical Analysis");
        startEvents.Select(e => e.Phase).Should().Contain("Syntax Analysis");
        startEvents.Select(e => e.Phase).Should().Contain("Type Checking");
        startEvents.Select(e => e.Phase).Should().Contain("Code Generation");

        // Verify matching start/end events
        foreach (var start in startEvents)
        {
            endEvents.Should().Contain(e => e.Phase == start.Phase,
                $"phase '{start.Phase}' should have an end event");
        }
    }

    [Fact]
    public void Compile_PhaseEventsHaveFilePath()
    {
        var logger = new StructuredLogger();
        var compiler = new Compiler(new CompilerOptions(), logger);

        compiler.Compile(ValidProgramWithVar, "myfile.spy");

        var events = logger.Events;
        events.Should().OnlyContain(e => e.FilePath == "myfile.spy");
    }

    [Fact]
    public void Compile_PhaseEventsHaveReasonableTimings()
    {
        var logger = new StructuredLogger();
        var compiler = new Compiler(new CompilerOptions(), logger);

        compiler.Compile(ValidProgram, "test.spy");

        var endEvents = logger.GetEvents<PhaseEndEvent>().ToList();

        foreach (var evt in endEvents)
        {
            // Each phase should take >= 0 time
            evt.Duration.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero,
                $"phase '{evt.Phase}' should have non-negative duration");

            // Each phase should complete in reasonable time (< 10 seconds for a simple program)
            evt.Duration.Should().BeLessThan(TimeSpan.FromSeconds(10),
                $"phase '{evt.Phase}' should complete quickly");
        }

        // Total time should be reasonable
        var totalTime = logger.GetTotalPhaseDuration();
        totalTime.Should().BeGreaterThan(TimeSpan.Zero, "total compilation should take some time");
    }

    [Fact]
    public void Compile_SuccessfulCompilation_HasZeroErrorsInPhases()
    {
        var logger = new StructuredLogger();
        var compiler = new Compiler(new CompilerOptions(), logger);

        var result = compiler.Compile(ValidProgram, "test.spy");

        result.Success.Should().BeTrue();

        var endEvents = logger.GetEvents<PhaseEndEvent>().ToList();
        endEvents.Should().OnlyContain(e => e.ErrorCount == 0,
            "successful compilation should have no errors in any phase");
    }

    [Fact]
    public void Compile_WithSyntaxError_ReportsErrorsInPhaseEvent()
    {
        var logger = new StructuredLogger();
        var compiler = new Compiler(new CompilerOptions(), logger);

        var result = compiler.Compile("def foo(", "test.spy");

        result.Success.Should().BeFalse();

        // Should have error count in the syntax analysis phase
        var endEvents = logger.GetEvents<PhaseEndEvent>().ToList();
        endEvents.Should().Contain(e => e.ErrorCount > 0,
            "failed compilation should have non-zero error count");
    }

    [Fact]
    public void Compile_WithTypeError_ReportsErrorsInTypeCheckingPhase()
    {
        var logger = new StructuredLogger();
        var compiler = new Compiler(new CompilerOptions(), logger);

        // Type error in a valid structure with main()
        var source = @"
def main():
    x: int = ""not an int""
    print(x)
";
        var result = compiler.Compile(source, "test.spy");

        result.Success.Should().BeFalse();

        var typeCheckEnd = logger.GetEvents<PhaseEndEvent>()
            .FirstOrDefault(e => e.Phase == "Type Checking");
        typeCheckEnd.Should().NotBeNull();
        typeCheckEnd!.ErrorCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Compile_EmitsCodeGenEvent()
    {
        var logger = new StructuredLogger();
        var compiler = new Compiler(new CompilerOptions(), logger);

        var result = compiler.Compile(ValidProgram, "test.spy");

        result.Success.Should().BeTrue();

        var codeGenEvents = logger.GetEvents<CodeGenEvent>().ToList();
        codeGenEvents.Should().HaveCount(1);
        codeGenEvents[0].OutputType.Should().Be("CSharp");
        codeGenEvents[0].ByteCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void NullLogger_IgnoresStructuredEvents()
    {
        // Verify NullLogger implements the default interface method correctly (no-op)
        // Access through interface to use default interface methods
        ICompilerLogger logger = NullLogger.Instance;

        // This should not throw
        logger.LogEvent(new PhaseStartEvent("Test"));
        logger.LogEvent(new PhaseEndEvent("Test", TimeSpan.Zero));

        // NullLogger should not support structured logging (uses default from interface)
        logger.SupportsStructuredLogging.Should().BeFalse();
    }

    [Fact]
    public void Compile_EventsInChronologicalOrder()
    {
        var logger = new StructuredLogger();
        var compiler = new Compiler(new CompilerOptions(), logger);

        compiler.Compile(ValidProgramWithVar, "test.spy");

        var events = logger.Events;
        for (int i = 1; i < events.Count; i++)
        {
            events[i].Timestamp.Should().BeOnOrAfter(events[i - 1].Timestamp,
                "events should be in chronological order");
        }
    }
}
