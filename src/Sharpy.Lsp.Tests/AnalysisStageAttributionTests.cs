using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Sharpy.Compiler;
using Xunit;
using SCG = System.Collections.Generic;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// What raising the server's minimum level to Debug actually buys, and what it must never
/// fabricate (#1225 / #1140).
/// </summary>
/// <remarks>
/// The per-stage attribution is gated on <c>ILogger.IsEnabled(Debug)</c> in
/// <c>SharpyWorkspace.FireAndForgetAnalysis</c>, so these drive the real workspace with a logger
/// whose enabled level is the one <c>Program</c> would have configured. The load-bearing case is
/// <see cref="Debug_logging_invents_no_stages_for_an_edit_the_fast_path_served"/>: an edit served
/// from the incremental fast path has no stages to attribute, and an empty breakdown is the signal
/// that the fast path worked — filling it in would destroy the served-from-cache vs analysed
/// distinction the instrument exists to show.
/// </remarks>
public class AnalysisStageAttributionTests : IDisposable
{
    private readonly CompilerApi _api = new();

    /// <summary>A body <c>AstFingerprint</c> can prove equal, so a comment-only edit classifies as
    /// no structural change and never reaches the compiler.</summary>
    private const string Source = "def main() -> int:\n    return 1\n";

    private const string Uri = "file:///attribution.spy";

    [Fact]
    public async Task Default_level_logs_latency_but_no_stage_attribution()
    {
        var logger = new LevelCapturingLogger<SharpyWorkspace>(ServerCommandLine.DefaultLogLevel);
        using var workspace = new SharpyWorkspace(_api, logger);

        workspace.OpenDocument(Uri, Source, 1);
        await WaitForAsync(() => CountOf(logger, AnalysisLatencyLog.Marker) >= 1);

        logger.Entries.Should().NotContain(e => e.Message.Contains(AnalysisLatencyLog.StagesMarker),
            "the attribution stays off until someone asks for Debug — it runs per keystroke");
        logger.Entries.Should().NotContain(e => e.Level == LogLevel.Debug);
    }

    [Fact]
    public async Task Debug_level_attributes_stages_on_an_analysed_edit()
    {
        var logger = new LevelCapturingLogger<SharpyWorkspace>(LogLevel.Debug);
        using var workspace = new SharpyWorkspace(_api, logger);

        workspace.OpenDocument(Uri, Source, 1);
        await WaitForAsync(() => CountOf(logger, AnalysisLatencyLog.StagesMarker) >= 1);

        var stageLine = logger.Entries.First(e => e.Message.Contains(AnalysisLatencyLog.StagesMarker));
        stageLine.Level.Should().Be(LogLevel.Debug,
            "which is why the attribution was unreachable while the minimum level was pinned to "
            + "Information (#1225)");
        stageLine.Message.Should().Contain($"path={AnalysisLatencyLog.SingleFilePath}")
            .And.Contain("stagesTotalMs=");
    }

    [Fact]
    public async Task Debug_logging_invents_no_stages_for_an_edit_the_fast_path_served()
    {
        var logger = new LevelCapturingLogger<SharpyWorkspace>(LogLevel.Debug);
        using var workspace = new SharpyWorkspace(_api, logger);

        workspace.OpenDocument(Uri, Source, 1);
        await WaitForAsync(() => CountOf(logger, AnalysisLatencyLog.Marker) >= 1);
        var stageLinesAfterFirstAnalysis = CountOf(logger, AnalysisLatencyLog.StagesMarker);
        stageLinesAfterFirstAnalysis.Should().BeGreaterThan(0, "the opening analysis is a real one");

        // A comment-only edit: same AST, so an incremental fast path serves it without a compiler
        // call. It still publishes, so it still logs a latency line.
        workspace.UpdateDocument(Uri, Source + "# a comment\n", 2);
        await WaitForAsync(() => CountOf(logger, AnalysisLatencyLog.Marker) >= 2);

        CountOf(logger, AnalysisLatencyLog.StagesMarker).Should().Be(stageLinesAfterFirstAnalysis,
            "an edit served without a compiler call has no stages to attribute; emitting a "
            + "breakdown for it would erase the served-from-cache vs analysed distinction");
    }

    private static int CountOf(LevelCapturingLogger<SharpyWorkspace> logger, string marker)
        => logger.Entries.Count(e => e.Message.Contains(marker, StringComparison.Ordinal));

    /// <summary>
    /// Polls until the condition holds, and throws if it never does. Analysis is debounced (300ms)
    /// and then runs off the timer thread, so there is nothing to await; the generous deadline is
    /// for a loaded CI machine.
    /// </summary>
    /// <remarks>
    /// Returning quietly on timeout would be safe only for a caller that goes on to assert
    /// something <i>appeared</i>. The assertions that matter most in this file check that something
    /// did <i>not</i> — no fabricated stages — and those pass for free when the edit under test was
    /// never processed at all. A wait that expires is a broken setup, not a satisfied expectation,
    /// so it fails here and names the condition rather than being laundered into a green absence
    /// assertion downstream.
    /// </remarks>
    private static async Task WaitForAsync(
        Func<bool> condition,
        [CallerArgumentExpression(nameof(condition))] string? description = null)
    {
        var timeout = TimeSpan.FromSeconds(30);
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return;
            await Task.Delay(25);
        }

        throw new TimeoutException(
            $"Waited {timeout.TotalSeconds:F0}s and this never became true: {description}. "
            + "The setup did not produce what the test then asserts about — treat this as a broken "
            + "arrangement, not as the absence the following assertion was going to check.");
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// An <see cref="ILogger{T}"/> that answers <see cref="ILogger.IsEnabled"/> exactly as a
    /// configured minimum level would, so a test can stand in for a server started at that level.
    /// </summary>
    private sealed class LevelCapturingLogger<T> : ILogger<T>
    {
        private readonly LogLevel _minimum;
        private readonly SCG.List<(LogLevel Level, string Message)> _entries = new();

        public LevelCapturingLogger(LogLevel minimum) => _minimum = minimum;

        public SCG.IReadOnlyList<(LogLevel Level, string Message)> Entries
        {
            get
            {
                lock (_entries)
                    return _entries.ToArray();
            }
        }

        public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= _minimum && _minimum != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            lock (_entries)
                _entries.Add((logLevel, formatter(state, exception)));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
