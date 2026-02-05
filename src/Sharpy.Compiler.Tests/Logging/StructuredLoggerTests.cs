using FluentAssertions;
using Sharpy.Compiler.Logging;
using Xunit;

namespace Sharpy.Compiler.Tests.Logging;

public class StructuredLoggerTests
{
    [Fact]
    public void SupportsStructuredLogging_ReturnsTrue()
    {
        var logger = new StructuredLogger();

        logger.SupportsStructuredLogging.Should().BeTrue();
    }

    [Fact]
    public void LogEvent_CapturesEventsInOrder()
    {
        var logger = new StructuredLogger();

        logger.LogEvent(new PhaseStartEvent("Phase1"));
        logger.LogEvent(new PhaseEndEvent("Phase1", TimeSpan.FromMilliseconds(100)));
        logger.LogEvent(new PhaseStartEvent("Phase2"));

        logger.Events.Should().HaveCount(3);
        logger.Events[0].Should().BeOfType<PhaseStartEvent>()
            .Which.Phase.Should().Be("Phase1");
        logger.Events[1].Should().BeOfType<PhaseEndEvent>()
            .Which.Phase.Should().Be("Phase1");
        logger.Events[2].Should().BeOfType<PhaseStartEvent>()
            .Which.Phase.Should().Be("Phase2");
    }

    [Fact]
    public void Events_HaveTimestamps()
    {
        var beforeLog = DateTime.UtcNow;
        var logger = new StructuredLogger();

        logger.LogEvent(new PhaseStartEvent("Test"));

        var afterLog = DateTime.UtcNow;
        logger.Events.Should().HaveCount(1);
        logger.Events[0].Timestamp.Should().BeOnOrAfter(beforeLog);
        logger.Events[0].Timestamp.Should().BeOnOrBefore(afterLog);
    }

    [Fact]
    public void Events_PreserveFilePath()
    {
        var logger = new StructuredLogger();

        logger.LogEvent(new PhaseStartEvent("Test") { FilePath = "/path/to/file.spy" });

        logger.Events[0].FilePath.Should().Be("/path/to/file.spy");
    }

    [Fact]
    public void PhaseEndEvent_PreservesDuration()
    {
        var logger = new StructuredLogger();
        var duration = TimeSpan.FromMilliseconds(42);

        logger.LogEvent(new PhaseEndEvent("Test", duration, 3));

        var endEvent = logger.Events[0].Should().BeOfType<PhaseEndEvent>().Subject;
        endEvent.Duration.Should().Be(duration);
        endEvent.ErrorCount.Should().Be(3);
    }

    [Fact]
    public void DiagnosticEvent_CapturesAllProperties()
    {
        var logger = new StructuredLogger();

        logger.LogEvent(new DiagnosticEvent(
            Code: "SPY0201",
            Message: "Type mismatch",
            Severity: DiagnosticEventSeverity.Error,
            Line: 10,
            Column: 5)
        {
            FilePath = "/test.spy"
        });

        var evt = logger.Events[0].Should().BeOfType<DiagnosticEvent>().Subject;
        evt.Code.Should().Be("SPY0201");
        evt.Message.Should().Be("Type mismatch");
        evt.Severity.Should().Be(DiagnosticEventSeverity.Error);
        evt.Line.Should().Be(10);
        evt.Column.Should().Be(5);
        evt.FilePath.Should().Be("/test.spy");
    }

    [Fact]
    public void SymbolResolvedEvent_CapturesAllProperties()
    {
        var logger = new StructuredLogger();

        logger.LogEvent(new SymbolResolvedEvent(
            Name: "myVar",
            Kind: "Variable",
            Type: "int"));

        var evt = logger.Events[0].Should().BeOfType<SymbolResolvedEvent>().Subject;
        evt.Name.Should().Be("myVar");
        evt.Kind.Should().Be("Variable");
        evt.Type.Should().Be("int");
    }

    [Fact]
    public void ImportResolvedEvent_CapturesAllProperties()
    {
        var logger = new StructuredLogger();

        logger.LogEvent(new ImportResolvedEvent(
            ModuleName: "math",
            ResolvedPath: "/lib/math.spy",
            Success: true));

        var evt = logger.Events[0].Should().BeOfType<ImportResolvedEvent>().Subject;
        evt.ModuleName.Should().Be("math");
        evt.ResolvedPath.Should().Be("/lib/math.spy");
        evt.Success.Should().BeTrue();
    }

    [Fact]
    public void CodeGenEvent_CapturesAllProperties()
    {
        var logger = new StructuredLogger();

        logger.LogEvent(new CodeGenEvent(OutputType: "CSharp", ByteCount: 1024));

        var evt = logger.Events[0].Should().BeOfType<CodeGenEvent>().Subject;
        evt.OutputType.Should().Be("CSharp");
        evt.ByteCount.Should().Be(1024);
    }

    [Fact]
    public void GetEvents_FiltersCorrectly()
    {
        var logger = new StructuredLogger();

        logger.LogEvent(new PhaseStartEvent("Phase1"));
        logger.LogEvent(new PhaseEndEvent("Phase1", TimeSpan.Zero));
        logger.LogEvent(new DiagnosticEvent("SPY0001", "Error", DiagnosticEventSeverity.Error, 1, 1));
        logger.LogEvent(new PhaseStartEvent("Phase2"));

        var startEvents = logger.GetEvents<PhaseStartEvent>().ToList();
        startEvents.Should().HaveCount(2);
        startEvents[0].Phase.Should().Be("Phase1");
        startEvents[1].Phase.Should().Be("Phase2");

        logger.GetEvents<DiagnosticEvent>().Should().HaveCount(1);
        logger.GetEvents<PhaseEndEvent>().Should().HaveCount(1);
    }

    [Fact]
    public void GetTotalPhaseDuration_SumsPhaseEndDurations()
    {
        var logger = new StructuredLogger();

        logger.LogEvent(new PhaseEndEvent("Phase1", TimeSpan.FromMilliseconds(100)));
        logger.LogEvent(new PhaseEndEvent("Phase2", TimeSpan.FromMilliseconds(150)));
        logger.LogEvent(new PhaseEndEvent("Phase3", TimeSpan.FromMilliseconds(50)));

        logger.GetTotalPhaseDuration().Should().Be(TimeSpan.FromMilliseconds(300));
    }

    [Fact]
    public void EventCount_ReturnsCorrectCount()
    {
        var logger = new StructuredLogger();

        logger.EventCount.Should().Be(0);

        logger.LogEvent(new PhaseStartEvent("Phase1"));
        logger.EventCount.Should().Be(1);

        logger.LogEvent(new PhaseEndEvent("Phase1", TimeSpan.Zero));
        logger.EventCount.Should().Be(2);
    }

    [Fact]
    public void Clear_RemovesAllEvents()
    {
        var logger = new StructuredLogger();
        logger.LogEvent(new PhaseStartEvent("Phase1"));
        logger.LogEvent(new PhaseEndEvent("Phase1", TimeSpan.Zero));

        logger.Clear();

        logger.Events.Should().BeEmpty();
        logger.EventCount.Should().Be(0);
    }

    [Fact]
    public void LogError_CreatesdiagnosticEvent()
    {
        var logger = new StructuredLogger();

        logger.LogError("Test error", 5, 10);

        logger.Events.Should().HaveCount(1);
        var evt = logger.Events[0].Should().BeOfType<DiagnosticEvent>().Subject;
        evt.Message.Should().Be("Test error");
        evt.Severity.Should().Be(DiagnosticEventSeverity.Error);
        evt.Line.Should().Be(5);
        evt.Column.Should().Be(10);
    }

    [Fact]
    public void LogWarning_CreatesDiagnosticEvent()
    {
        var logger = new StructuredLogger();

        logger.LogWarning("Test warning", 3, 7);

        logger.Events.Should().HaveCount(1);
        var evt = logger.Events[0].Should().BeOfType<DiagnosticEvent>().Subject;
        evt.Message.Should().Be("Test warning");
        evt.Severity.Should().Be(DiagnosticEventSeverity.Warning);
        evt.Line.Should().Be(3);
        evt.Column.Should().Be(7);
    }

    [Fact]
    public void Constructor_WithInnerLogger_ForwardsLogs()
    {
        var inner = new StructuredLogger();
        var outer = new StructuredLogger(inner);

        outer.LogError("Error from outer", 1, 1);

        // Both loggers should have the diagnostic event
        outer.Events.Should().HaveCount(1);
        inner.Events.Should().HaveCount(1);
    }

    [Fact]
    public async Task LogEvent_IsThreadSafe()
    {
        var logger = new StructuredLogger();
        const int eventCount = 1000;
        const int threadCount = 10;

        var tasks = Enumerable.Range(0, threadCount)
            .Select(threadId => Task.Run(() =>
            {
                for (int i = 0; i < eventCount; i++)
                {
                    logger.LogEvent(new PhaseStartEvent($"Thread{threadId}_Phase{i}"));
                }
            }))
            .ToArray();

        await Task.WhenAll(tasks);

        // All events should be captured
        logger.EventCount.Should().Be(threadCount * eventCount);

        // All events should be accessible
        var events = logger.Events;
        events.Should().HaveCount(threadCount * eventCount);

        // All events should be PhaseStartEvents with valid data
        events.Should().AllBeOfType<PhaseStartEvent>();
        events.Cast<PhaseStartEvent>().Should().OnlyContain(e => e.Phase.StartsWith("Thread"));
    }

    [Fact]
    public void DefaultInterfaceMethod_LogEvent_DoesNothing()
    {
        // Create a minimal logger that only implements required methods
        // to verify the default LogEvent doesn't crash
        var minimalLogger = new MinimalTestLogger();
        ICompilerLogger logger = minimalLogger;

        // Should not throw
        logger.LogEvent(new PhaseStartEvent("Test"));
        logger.LogEvent(new DiagnosticEvent("SPY0001", "msg", DiagnosticEventSeverity.Error, 1, 1));

        // Default implementation returns false
        logger.SupportsStructuredLogging.Should().BeFalse();
    }

    private sealed class MinimalTestLogger : ICompilerLogger
    {
        public void LogTokenRead(string tokenType, int line, int column, string value) { }
        public void LogIndentChange(int oldLevel, int newLevel) { }
        public void LogParseEnter(string rule, int tokenPosition) { }
        public void LogParseExit(string rule, bool success) { }
        public void LogError(string message, int line, int column) { }
        public void LogWarning(string message, int line, int column) { }
        public void LogInfo(string message) { }
        public void LogDebug(string message) { }
        public void LogTrace(string message) { }
        public void LogMetrics(string metricsOutput) { }
        public bool IsEnabled(CompilerLogLevel level) => false;
    }
}
