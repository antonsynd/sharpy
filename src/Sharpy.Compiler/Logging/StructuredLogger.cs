using System.Collections.Concurrent;

namespace Sharpy.Compiler.Logging;

/// <summary>
/// A logger implementation that captures structured compiler events for analysis.
/// </summary>
/// <remarks>
/// <para>
/// This logger captures all <see cref="CompilerEvent"/> instances emitted during
/// compilation, making them available for trace replay, performance analysis,
/// and debugging. Events are stored in memory and can be retrieved via the
/// <see cref="Events"/> property.
/// </para>
/// <para>
/// This logger also forwards text-based logging to an optional inner logger,
/// allowing structured events to be captured while still producing console output.
/// </para>
/// </remarks>
public sealed class StructuredLogger : ICompilerLogger
{
    private readonly ConcurrentBag<CompilerEvent> _events = new();
    private readonly ICompilerLogger? _innerLogger;
    private readonly CompilerLogLevel _minLevel;

    /// <summary>
    /// Creates a new structured logger with no inner logger (events only).
    /// </summary>
    /// <param name="minLevel">Minimum level for text-based logging methods (default: Info)</param>
    public StructuredLogger(CompilerLogLevel minLevel = CompilerLogLevel.Info)
    {
        _innerLogger = null;
        _minLevel = minLevel;
    }

    /// <summary>
    /// Creates a new structured logger that also forwards to an inner logger.
    /// </summary>
    /// <param name="innerLogger">Logger to forward text-based logs to</param>
    public StructuredLogger(ICompilerLogger innerLogger)
    {
        _innerLogger = innerLogger ?? throw new ArgumentNullException(nameof(innerLogger));
        _minLevel = CompilerLogLevel.Trace; // Let inner logger decide
    }

    /// <summary>
    /// Gets all captured events in the order they were logged.
    /// </summary>
    /// <remarks>
    /// Events are captured using a thread-safe collection, but iteration order
    /// matches insertion order when accessed from a single thread after logging completes.
    /// </remarks>
    public IReadOnlyList<CompilerEvent> Events => _events.Reverse().ToList();

    /// <summary>
    /// Gets the count of captured events.
    /// </summary>
    public int EventCount => _events.Count;

    /// <summary>
    /// Clears all captured events.
    /// </summary>
    public void Clear() => _events.Clear();

    /// <inheritdoc/>
    public bool SupportsStructuredLogging => true;

    /// <inheritdoc/>
    public void LogEvent(CompilerEvent evt)
    {
        _events.Add(evt);
    }

    /// <summary>
    /// Gets all events of a specific type.
    /// </summary>
    public IEnumerable<T> GetEvents<T>() where T : CompilerEvent
        => Events.OfType<T>();

    /// <summary>
    /// Gets the total duration across all completed phases.
    /// </summary>
    public TimeSpan GetTotalPhaseDuration()
        => TimeSpan.FromTicks(GetEvents<PhaseEndEvent>().Sum(e => e.Duration.Ticks));

    // ICompilerLogger implementation - forward to inner logger or implement based on minLevel

    /// <inheritdoc/>
    public void LogTokenRead(string tokenType, int line, int column, string value)
        => _innerLogger?.LogTokenRead(tokenType, line, column, value);

    /// <inheritdoc/>
    public void LogIndentChange(int oldLevel, int newLevel)
        => _innerLogger?.LogIndentChange(oldLevel, newLevel);

    /// <inheritdoc/>
    public void LogParseEnter(string rule, int tokenPosition)
        => _innerLogger?.LogParseEnter(rule, tokenPosition);

    /// <inheritdoc/>
    public void LogParseExit(string rule, bool success)
        => _innerLogger?.LogParseExit(rule, success);

    /// <inheritdoc/>
    public void LogError(string message, int line, int column)
    {
        _innerLogger?.LogError(message, line, column);

        // Also capture as a diagnostic event
        LogEvent(new DiagnosticEvent(
            Code: "SHP0000", // Generic code for unstructured errors
            Message: message,
            Severity: DiagnosticEventSeverity.Error,
            Line: line,
            Column: column));
    }

    /// <inheritdoc/>
    public void LogWarning(string message, int line, int column)
    {
        _innerLogger?.LogWarning(message, line, column);

        // Also capture as a diagnostic event
        LogEvent(new DiagnosticEvent(
            Code: "SHP0000", // Generic code for unstructured warnings
            Message: message,
            Severity: DiagnosticEventSeverity.Warning,
            Line: line,
            Column: column));
    }

    /// <inheritdoc/>
    public void LogInfo(string message)
        => _innerLogger?.LogInfo(message);

    /// <inheritdoc/>
    public void LogDebug(string message)
        => _innerLogger?.LogDebug(message);

    /// <inheritdoc/>
    public void LogTrace(string message)
        => _innerLogger?.LogTrace(message);

    /// <inheritdoc/>
    public void LogMetrics(string metricsOutput)
        => _innerLogger?.LogMetrics(metricsOutput);

    /// <inheritdoc/>
    public bool IsEnabled(CompilerLogLevel level)
        => _innerLogger?.IsEnabled(level) ?? _minLevel >= level;
}
