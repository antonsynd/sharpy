namespace Sharpy.Compiler.Logging;

/// <summary>
/// Interface for logging compiler operations (lexing, parsing, etc.)
/// </summary>
public interface ICompilerLogger
{
    /// <summary>
    /// Log a token read from the lexer
    /// </summary>
    void LogTokenRead(string tokenType, int line, int column, string value);

    /// <summary>
    /// Log an indentation level change
    /// </summary>
    void LogIndentChange(int oldLevel, int newLevel);

    /// <summary>
    /// Log entering a parse rule
    /// </summary>
    void LogParseEnter(string rule, int tokenPosition);

    /// <summary>
    /// Log exiting a parse rule
    /// </summary>
    void LogParseExit(string rule, bool success);

    /// <summary>
    /// Log an error
    /// </summary>
    void LogError(string message, int line, int column);

    /// <summary>
    /// Log a warning
    /// </summary>
    void LogWarning(string message, int line, int column);

    /// <summary>
    /// Log general information
    /// </summary>
    void LogInfo(string message);

    /// <summary>
    /// Log debug information
    /// </summary>
    void LogDebug(string message);

    /// <summary>
    /// Log trace-level information
    /// </summary>
    void LogTrace(string message);

    /// <summary>
    /// Log compilation metrics
    /// </summary>
    void LogMetrics(string metricsOutput);

    /// <summary>
    /// Check if logging is enabled at a specific level
    /// </summary>
    bool IsEnabled(CompilerLogLevel level);

    /// <summary>
    /// Log a structured compiler event.
    /// </summary>
    /// <remarks>
    /// This is an optional method for structured logging support.
    /// The default implementation does nothing, making it backward compatible
    /// with existing logger implementations.
    /// Structured events enable trace replay, performance profiling, and telemetry.
    /// </remarks>
    /// <param name="evt">The compiler event to log</param>
    void LogEvent(CompilerEvent evt) { }

    /// <summary>
    /// Check if structured event logging is supported by this logger.
    /// </summary>
    /// <remarks>
    /// Returns false by default. Loggers that capture events should override
    /// this to return true so callers can avoid constructing events unnecessarily.
    /// </remarks>
    bool SupportsStructuredLogging => false;
}

/// <summary>
/// Logging verbosity levels
/// </summary>
public enum CompilerLogLevel
{
    None = 0,      // No logging
    Error = 1,     // Only errors
    Warning = 2,   // Errors and warnings
    Info = 3,      // High-level phase information
    Debug = 4,     // Detailed operational info
    Trace = 5      // Every token, every parse rule
}
