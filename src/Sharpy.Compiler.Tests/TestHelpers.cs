using Sharpy.Compiler.Logging;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests;

/// <summary>
/// Shared test utilities and helper classes for compiler tests.
/// </summary>
public static class TestHelpers
{
    /// <summary>
    /// Global lock for console I/O operations during test execution.
    /// Tests that redirect Console.Out/Console.Error should acquire this lock
    /// to prevent interference from parallel test execution.
    /// </summary>
    public static readonly object ConsoleLock = new object();
    /// <summary>
    /// Test logger that collects errors and warnings for assertion in tests.
    /// Useful for testing error reporting behavior without requiring ITestOutputHelper.
    /// </summary>
    public class CollectingTestLogger : ICompilerLogger
    {
        public List<(string Message, int Line, int Column)> Errors { get; } = new();
        public List<(string Message, int Line, int Column)> Warnings { get; } = new();

        public void LogError(string message, int line, int column)
        {
            Errors.Add((message, line, column));
        }

        public void LogWarning(string message, int line, int column)
        {
            Warnings.Add((message, line, column));
        }

        public void LogTokenRead(string tokenType, int line, int column, string value) { }
        public void LogIndentChange(int oldLevel, int newLevel) { }
        public void LogParseEnter(string rule, int tokenPosition) { }
        public void LogParseExit(string rule, bool success) { }
        public void LogInfo(string message) { }
        public void LogDebug(string message) { }
        public void LogTrace(string message) { }
        public void LogMetrics(string metricsOutput) { }
        public bool IsEnabled(CompilerLogLevel level) => level <= CompilerLogLevel.Warning;
    }

    /// <summary>
    /// Test logger that writes to xUnit test output.
    /// Useful for debugging and viewing compilation details during test runs.
    /// </summary>
    public class OutputTestLogger : ICompilerLogger
    {
        private readonly ITestOutputHelper _output;

        public OutputTestLogger(ITestOutputHelper output)
        {
            _output = output;
        }

        public void LogTokenRead(string tokenType, int line, int column, string value)
        {
            // Don't log tokens during tests to avoid clutter
        }

        public void LogIndentChange(int oldLevel, int newLevel)
        {
            // Don't log indent changes during tests
        }

        public void LogParseEnter(string rule, int tokenPosition)
        {
            // Don't log parse enter during tests
        }

        public void LogParseExit(string rule, bool success)
        {
            // Don't log parse exit during tests
        }

        public void LogError(string message, int line, int column)
        {
            _output.WriteLine($"ERROR [{line},{column}]: {message}");
        }

        public void LogWarning(string message, int line, int column)
        {
            _output.WriteLine($"WARNING [{line},{column}]: {message}");
        }

        public void LogInfo(string message)
        {
            _output.WriteLine($"INFO: {message}");
        }

        public void LogDebug(string message)
        {
            _output.WriteLine($"DEBUG: {message}");
        }

        public void LogTrace(string message)
        {
            // Don't log trace during tests
        }

        public void LogMetrics(string metricsOutput)
        {
            // Don't log metrics during tests unless explicitly needed
        }

        public bool IsEnabled(CompilerLogLevel level)
        {
            return level <= CompilerLogLevel.Info;
        }
    }
}
