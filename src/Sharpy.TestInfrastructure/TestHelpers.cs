using Sharpy.Compiler.Logging;
using Xunit.Abstractions;

namespace Sharpy.TestInfrastructure;

public static class TestHelpers
{
    public static readonly object ConsoleLock = new object();
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

    public class OutputTestLogger : ICompilerLogger
    {
        private readonly ITestOutputHelper _output;

        public OutputTestLogger(ITestOutputHelper output)
        {
            _output = output;
        }

        public void LogTokenRead(string tokenType, int line, int column, string value)
        {
        }

        public void LogIndentChange(int oldLevel, int newLevel)
        {
        }

        public void LogParseEnter(string rule, int tokenPosition)
        {
        }

        public void LogParseExit(string rule, bool success)
        {
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
        }

        public void LogMetrics(string metricsOutput)
        {
        }

        public bool IsEnabled(CompilerLogLevel level)
        {
            return level <= CompilerLogLevel.Info;
        }
    }
}
